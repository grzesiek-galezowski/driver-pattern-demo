## Production code

```csharp
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
  [HttpGet("{id}")]
  public async Task<WeatherForecastDto> Get(Guid id) {...}

  [HttpGet("{tenantId}/{userId}")]
  public IEnumerable<WeatherForecastDto> GetAllUserForecasts(string tenantId, string userId) {...}
  
  [HttpPost]
  public async Task<ActionResult> ReportWeatherForecast(WeatherForecastDto forecastDto) {...}
}
```

## First stab

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  await driver.ReportWeatherForecast();

  //WHEN
  using var retrievedForecast = await driver.GetReportedForecast();

  //THEN
  await retrievedForecast.ShouldBeTheSameAsReported();

  //not really part of the scenario...
  driver.NotificationAboutForecastReportedShouldBeSent();
}
```

Deficiencies:
* The more methods the more bloated the driver becomes
* State is managed internally, which is fine by now, but what if we want to write a scenario where a weather report contains bad data? Or where two distinct users report weather?

If you are interested, this test without any abstractions looks like this:

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  var tenantId = Any.String();
  var userId = Any.String();

  using var notificationRecipient = WireMockServer.Start();
  notificationRecipient.Given(
    Request.Create()
      .WithPath("/notifications")
      .UsingPost()).RespondWith(
    Response.Create()
      .WithStatusCode(HttpStatusCode.OK));

  var inputForecastDto = new WeatherForecastDto(
    tenantId, 
    userId, 
    Any.Instance<DateTime>(),
    Any.Integer(),
    Any.String());
  using var host = Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(webBuilder =>
    {
      webBuilder
        .UseTestServer()
        .ConfigureAppConfiguration(appConfig =>
        {
          appConfig.AddInMemoryCollection(new Dictionary<string, string>
          {
            ["NotificationsConfiguration:BaseUrl"] = notificationRecipient.Urls.Single()
          });
        })
        .UseEnvironment("Development")
        .UseStartup<Startup>();
    }).Build();

  await host.StartAsync();

  var client = new FlurlClient(host.GetTestClient());

  using var postJsonAsync = await client.Request("WeatherForecast")
    .PostJsonAsync(inputForecastDto);
  var resultDto = await postJsonAsync.GetJsonAsync<ForecastCreationResultDto>();

  using var httpResponse = await client.Request("WeatherForecast")
    .AppendPathSegment(resultDto.Id)
    .AllowAnyHttpStatus()
    .GetAsync();

  var weatherForecastDto = await httpResponse.GetJsonAsync<WeatherForecastDto>();
  weatherForecastDto.Should().BeEquivalentTo(inputForecastDto);

  notificationRecipient.LogEntries.Should().ContainSingle(entry =>
    entry.RequestMatchResult.IsPerfectMatch
    && entry.RequestMessage.Path == "/notifications"
    && entry.RequestMessage.Method == "POST"
    && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
       entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
       tenantId, userId, inputForecastDto.TemperatureC));
}
```

## Extension objects

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  await driver.WeatherForecastApi.ReportForecast();

  //WHEN
  using var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecast();

  //THEN
  await retrievedForecast.ShouldBeTheSameAsReported();

  //not really part of the scenario...
  driver.Notifications.ShouldIncludeNotificationAboutReportedForecast();
}
```

The extension objects are created anew every time to avoid state synchronization issues between them and driver. The driver holds all the data.

Has remaining two deficiencies.

## Lambda builders

Option 1.

We can then write a test that modifies the default values (in the WHEN section):

```csharp
[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  //WHEN
  using var reportForecastResponse = await driver.WeatherForecastApi.AttemptToReportForecast(
    request => request with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}
```

## Chaining methods on extension objects

```csharp
[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  //WHEN
  using var reportForecastResponse = await driver.WeatherForecastApi
    .WithTemperatureOf(-101)
    .AttemptToReportForecast();

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}
```

May result in mismatch of setups and calls. E.g. I may specify a parameter used only by call 1, but then invoke call 2.

## External builder

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  var weatherForecast = ForecastReport();

  await driver.WeatherForecastApi.Report(weatherForecast);

  //WHEN
  using var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecast();

  //THEN
  await retrievedForecast.ShouldBeTheSameAs(weatherForecast);

  //not really part of the scenario...
  driver.Notifications.ShouldIncludeNotificationAbout(weatherForecast);
}

[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  //WHEN
  using var reportForecastResponse = await driver.WeatherForecastApi
    .AttemptToReportForecast(ForecastReport() with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}

private static WeatherForecastReportBuilder ForecastReport() => new();
```

"last input" is no longer managed by the driver. "Last output" still is.

Builder is a simple record:

```csharp
public record WeatherForecastReportBuilder
{
  public string TenantId { private get; init; } = Any.String();
  public string UserId { private get; init; } = Any.String();
  public DateTime Time { private get; init; } = Any.Instance<DateTime>();
  public int TemperatureC { private get; init; } = Any.Integer();
  public string Summary { private get; init; } = Any.String();

  public WeatherForecastDto Build()
  {
    return new(
      TenantId, 
      UserId, 
      Time,
      TemperatureC,
      Summary);
  }
}
```

Still, the last response is held inside

## Externalized context management (??)

```csharp
private static WeatherForecastReportBuilder ForecastReport() => new();

[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  var weatherForecast = ForecastReport();

  using var reportForecastResponse = await driver.WeatherForecastApi.Report(weatherForecast);

  //WHEN
  using var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecastBy(
    await reportForecastResponse.GetId());

  //THEN
  await retrievedForecast.ShouldBeTheSameAs(weatherForecast);

  //not really part of the scenario...
  driver.Notifications.ShouldIncludeNotificationAbout(weatherForecast);
}

[Fact]
public async Task ShouldAllowRetrievingReportsFromAParticularUser()
{
  //GIVEN
  var userId1 = Any.String();
  var userId2 = Any.String();
  var tenantId1 = Any.String();
  var tenantId2 = Any.String();
  await using var driver = new AppDriver();
  await driver.StartAsync();
  var user1Forecast1 = ForecastReport() with {UserId = userId1, TenantId = tenantId1};
  var user1Forecast2 = ForecastReport() with {UserId = userId1, TenantId = tenantId1};
  var user2Forecast = ForecastReport() with {UserId = userId2, TenantId = tenantId2};

  using var responseForUser1Forecast1 = await driver.WeatherForecastApi.Report(user1Forecast1);
  using var responseForUser1Forecast2 = await driver.WeatherForecastApi.Report(user1Forecast2);
  using var responseForUser2Forecast = await driver.WeatherForecastApi.Report(user2Forecast);

  //WHEN
  using var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecastsFrom(userId1, tenantId1);

  //THEN
  await retrievedForecast.ShouldConsistOf(user1Forecast1, user1Forecast2);
}

[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  //WHEN
  using var reportForecastResponse = await driver.WeatherForecastApi
    .AttemptToReportForecast(ForecastReport() with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}
```

We can now automate multiple users without leaving garbage, but tests can be verbose

## Introducing actors

Actors in the sense of Screenplay pattern, not in "actor model" sense.

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  using var user = new User(driver);

  await user.ReportNewForecast();

  //WHEN
  using var retrievedForecast = await user.RetrieveLastReportedForecast();

  //THEN
  await retrievedForecast.ShouldBeTheSameAs(user.LastReportedForecast());

  //not really part of the scenario...
  driver.Notifications.ShouldIncludeNotificationAbout(user.LastReportedForecast());
}

[Fact]
public async Task ShouldAllowRetrievingReportsFromAParticularUser()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  using var user1 = new User(driver);
  using var user2 = new User(driver);

  await user1.ReportNewForecast();
  await user1.ReportNewForecast();
  await user2.ReportNewForecast();

  //WHEN
  using var allForecastsReportedByUser1 = await user1.RetrieveAllReportedForecasts();

  //THEN
  await allForecastsReportedByUser1.ShouldConsistOf(user1.AllReportedForecasts());
}

[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsBelowAllowedMinimum()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  using var user = new User(driver);

  //WHEN
  using var reportForecastResponse = 
    await user.AttemptToReportNewForecast(forecast => forecast with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}
```

RetrieveAll... and All.. could use some work.

Remember, this is a pattern. No canonical implementation.

TODO: links to each source file
TODO: page object
TODO: at which level is this pattern useful?
TODO describe production code - notification is sent via HTTP
TODO: links to existing posts on the driver pattern.
TODO: change the code so that HTTP responses are added to list and disposed in driver.
