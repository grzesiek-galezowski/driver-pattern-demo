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

# First stab

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

## Scaling the driver pattern - extension objects

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

TODO: links to each source file
TODO: page object
TODO: at which level is this pattern useful?
TODO describe production code - notification is sent via HTTP
TODO: links to existing posts on the driver pattern.
