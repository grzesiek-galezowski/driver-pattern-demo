# (My) Driver pattern description

One description of the driver pattern can be found at http://leitner.io/2015/11/14/driver-pattern-empowers-your-specflow-step-definitions/. It's a description of the pattern for UI testing under SpecFlow framework.

The funny thing is that I probably discovered the pattern separately as I've been using it since ca. 2014 and definitely not for GUI (although surprisingly, I can agree with many of the things written in the mentioned blog post). My discovery of the driver pattern came probably from melting two classes from the Growing Object-Oriented Software Guided By Tests book: `ApplicationRunner` and `AuctionSniperDriver`, on which the end-to-end automation in the book was based.

My definition of the driver pattern is:

> The goal of the driver pattern is to provide an intention layer that allows an automated test to be written in terms of the writer's intention and translates the intention into lower-level mechanics used to communicate with code under test. 

So far, I was able to use the driver pattern to automate the following kinds of tests:

* End-to-end tests
* TestHost-based ASP.Net Core tests
* Application logic tests (in terms of hexagonal architecture - tests that test the inner hexagon with real adapters replaced by test adapters)
* Adapter tests (in terms of hexagonal architecture)

Also, I was able to use it when writing the following kinds of apps:

* Console utilities (in both end-to-end and application logic tests)
* WPF Gui application (although only in application logic tests)
* ASP.Net Core Web API applications

I used this pattern in C# and Java, but suspect it can be used in others.

> ### Warning
> Do not confuse this pattern with [WebDriver](https://www.w3.org/TR/webdriver/) often used by UI tests to talk to a browser.

Through discussion with some good souls who acknowledged that the patterns is not overly popular and well-known and who encouraged me to write something about it, I decided to put together this article, documenting how I understand, use and evolve my drivers.

> ### Note
> All the code in this folder is companion content to this article. The article does not discuss every part of the sample code. I encourage you to inspect it and play with it in your IDE of choice.

# Examples

All the example driver implementations are written against a sample ASP.Net Core application. The application is based on the standard "weather forecast" template, to make it more familiar, although many parts were modified.

Some words of caution before we move on:

* In the production part, neither the code nor the API is pretty. The production part of this sample does not aim to demonstrate how to design good web APIs or services or classes. It is made only to serve as something executable for the tests.
* In one of the example tests, I added a check that isn't really part of the scenario. The purpose is only to decrease the volume of code for easier digestion, nothing more.
* The examples show an evolution process. The first example is by far not perfect and the final one still has some stuff that can be improved.
* I am not claiming that the driver pattern is the best way to automate tests. I am still learning, so everything written here is provided as a sort of RFC.

## [Production code](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/DriverPatternDemo/Controllers/WeatherForecastController.cs)

Here is the tested API in a form of a [controller](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/DriverPatternDemo/Controllers/WeatherForecastController.cs)

```csharp
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
  [HttpGet("{id}")]
  public async Task<WeatherForecastDto> Get(Guid id) 
  {
    //Retrieves forecast by specified ID, produced when
    //forecast is reported.
  }


  [HttpGet("{tenantId}/{userId}")]
  public IEnumerable<WeatherForecastDto> GetAllUserForecasts(
      string tenantId, 
      string userId) 
  {
    //Retrieves all forecasts for a combination of 
    //a user ID and a tenant ID
  }
  
  [HttpPost]
  public async Task<ActionResult> ReportWeatherForecast(
      WeatherForecastDto forecastDto) 
  {
    //Verifies if temperature is in a valid range
    //Saves a weather forecast in a database
    //Sends a notification via HTTP (sic!) 
    //that a new forecast was reported
  }
}
```

The notification is only added to the code to warrant usage of a wiremock to make the example a bit more complex.

## [First stab](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_01_SimpleDriver/E2eSpecification.cs)

The first driver-based test looks like this:

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

The steps are roughly:

1. Create a driver
1. Start a driver (initiate tested code's startup sequence)
1. Report a single weather forecast
1. Retrieve reported forecast
1. Assert that the retrieved forecast is the same as previously reported
1. Assert that a notification was sent to a wiremock about the reported forecast.

Several things to note about the above test:

1. Note how I separated creating a driver from starting it. The main reason is that typically I want to have a place to do some additional setup before starting the application, e.g. changing configuration or setting up some data in database. There are times when the behavior of starting application is interesting by itself and I want to have a place where I could influence this behavior. This place is exactly between creating the driver and starting the application.
1. The test does almost no state management. Note that in the assertion I say `retrievedForecast.ShouldBeTheSameAsReported()` and I don't pass any arguments. The driver holds all the state internally. I did it like this out of pure convenience - this is NOT an inherent property of driver pattern. The first test I would typically write using TDD and when sketching the scenario before the implementation, I don't want to think too much about the input data structures and output data structures. I want to specify my intention and then I'll introduce more control over the data as necessary.
1. Note the `retrievedForecast` - this isn't a DTO, but also a part of driver - it's an object made for tests and allows executing some assertions on the retrieved data. Again, when I TDD, I don't care yet what the data is going to be. I just want to state my intention.

By the way, if you are interested, this test without any abstractions looks like this:

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

Getting back to the driver - the way it is currently designed has some drawbacks that will become more evident as I add more tests. These drawbacks are:

1. The more methods are added to the driver, the more bloated the driver becomes. This doesn't become evident until about a dozen of scenarios, but later it makes picking the right methods from the driver more difficult.
1. State is managed internally, which is fine by now, but what if we want to write a scenario where a weather report contains bad data? How do we tell the driver to send bad data?
1. Or what about a scenario where two distinct users report weather? Currently there is no way to specify what user is making the request.

I'll start by addressing the first of those drawbacks.

## [Evolution - extension objects](https://github.com/grzesiek-galezowski/driver-pattern-demo/tree/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_02_DriverWithExtensionObjects)

To further partition the methods in the driver, I will introduce special objects that will take on parts of driver API. Take a look at the modified version of the above test:

```csharp
[Fact]
public async Task ShouldAllowRetrievingReportedForecast()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  await driver.WeatherForecastApi.ReportForecast();

  //WHEN
  using var retrievedForecast 
    = await driver.WeatherForecastApi.GetReportedForecast();

  //THEN
  await retrievedForecast.ShouldBeTheSameAsReported();

  //not really part of the scenario...
  driver.Notifications.ShouldIncludeNotificationAboutReportedForecast();
}
```

Note that this time, I am accessing the driver logic through properties: `WeatherForecastApi` (for operations on weather forecast API) and `Notifications` (for operations on notifications). This is a trick I use to make the driver itself leaner.

If you look at how these properties are defined:

```csharp
public NotificationsDriverExtension Notifications 
  => new(_userId, _tenantId, _notificationRecipient, _lastInputForecastDto.Value);

public WeatherForecastApiDriverExtension WeatherForecastApi 
  => new(this, _tenantId, _userId, HttpClient, _lastReportResult, _lastInputForecastDto);
```

You'll notice that the extension objects are always created anew every time the properties are accessed. This is on purpose, to avoid the necessity of state synchronization between them and the driver. The driver holds all the current data and feeds it to extension objects every time they are created. This way the extension objects get the most "fresh" data.

Also, if you read through the code, you'll notice that the extension objects feed some data back to the driver via an interface called `IAppDriverContext`. This is a consequence of the driver managing the state. Short term, it isn't that bad, but I will be dealing with it later.

First, though, let's see what we can do to allow testing with different data (e.g. simulate a bad request). To achieve this, there are three routes I usually choose from:

* Lambda customizations
* Chaining methods on extension objects
* Externally created builders

Let's take them one by one and analyze.

## [Lambda customizations](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_03_DriverCustomizableWithLambdaBuilders/E2eSpecification.cs)

In this approach, driver methods still use the default data held inside the driver, but allow customizing that data with a lambda, passed as a parameter. To demonstrate it, here is a test which states that temperatures below -100 Celcius degrees should be rejected (I know, I know...).

```csharp
[Fact]
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();

  //WHEN
  using var reportForecastResponse = 
    await driver.WeatherForecastApi
      .AttemptToReportForecast(
         report => report with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
}
```

Note this part of the code:

```csharp
  using var reportForecastResponse = 
    await driver.WeatherForecastApi
      .AttemptToReportForecast(
         report => report with {TemperatureC = -101});
```

Here, I am passing a lambda inside which I can customize a builder for a weather forecast. The builder is just a record:

```csharp
public record WeatherForecastReportBuilder(string UserId, string TenantId)
{
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

Note that the builder accepts `UserId` and `TenantId` as constructor arguments because this comes from the driver that holds the default values.

## [Chaining methods on extension objects](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_04_DriverCustomizableWithExtensionObjects/E2eSpecification.cs)

This is just another option. I consider this approach to be more crude, although it might lead to writing a bit less code at times. The approach takes advantage of the fact that every time we request an extension object from the driver, we get a new instance. Thus, we can use that instance to customize it with data needed for our single, custom request. Below is the same test as in the previous section, written with chaining methods on extension object.

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

The following lines:

```csharp
using var reportForecastResponse = await driver.WeatherForecastApi
  .WithTemperatureOf(-101)
  .AttemptToReportForecast();
```

Demonstrate how the chaining is used.

A disadvantage of this approach might be that an extension object must allow customizing data used by any of its chain finisher methods. So I could customize a parameter not used in the method that follows the customization.

## [Externally created builders](https://github.com/grzesiek-galezowski/driver-pattern-demo/tree/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_05_DriverCustomizableWithExternalBuilders)

Finally, we can just take the request data out of the driver and put it into a separate builder class. We could fill the builder with any data we want and then just pass it.

Here are the two tests we have so far, rewritten with external builder:

```csharp
private static WeatherForecastReportBuilder ForecastReport() => new();

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
```

Similarly to the lambda customization case, the builder is a simple record:

```csharp
public record WeatherForecastReportBuilder
{
  public string TenantId { private get; init; } = Any.String();
  public string UserId { private get; init; } = Any.String();
  public DateTime Time { private get; init; } = Any.DateTime();
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

This time, however, it does not accept `TenantId` and `UserId` but has some values defined. This means that if we need to preserve these two values across several requests, the test body must supply them every time to the external builders. We cannot rely on the driver doing it anymore.

If we can pull this off, however, it tends to simplify the driver (at the expense of test body, but it's often a good trade-off because we have other ways of simplifying the test body - coming shortly).

So the driver does not have to remember the reported forecasts anymore, but it still remembers the responses to reports. We can externalize that knowledge as well.

## [Externalized forecast report response management](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_06_DriverCustomizableWithExternalizedContextManagement/E2eSpecification.cs)

After pushing the responsibility of holding the responses from drive to the test, we get something like this:

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

This further simplifies the driver, but some tests can now become quite verbose. Look at the following test which says that when a user retrieves his reported forecasts, it should only contain the ones he reported and not ones that other users reported:

```csharp
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
  var user1Forecast1 = ForecastReport() 
    with {UserId = userId1, TenantId = tenantId1};
  var user1Forecast2 = ForecastReport() 
    with {UserId = userId1, TenantId = tenantId1};
  var user2Forecast = ForecastReport() 
    with {UserId = userId2, TenantId = tenantId2};

  using var responseForUser1Forecast1 
    = await driver.WeatherForecastApi.Report(user1Forecast1);
  using var responseForUser1Forecast2 
    = await driver.WeatherForecastApi.Report(user1Forecast2);
  using var responseForUser2Forecast 
    = await driver.WeatherForecastApi.Report(user2Forecast);

  //WHEN
  using var retrievedForecast = 
    await driver.WeatherForecastApi.GetReportedForecastsFrom(userId1, tenantId1);

  //THEN
  await retrievedForecast.ShouldConsistOf(user1Forecast1, user1Forecast2);
}
```

Let's try to get rid of some of the verbosity.

## [Introducing actors](https://github.com/grzesiek-galezowski/driver-pattern-demo/blob/main/DriverInFunctionalHttpApiTests/FunctionalSpecification/_07_DriverCustomizableWithActors/E2eSpecification.cs)

Here, I mean actors in a sense similar to that of [Screenplay pattern](https://www.infoq.com/articles/Beyond-Page-Objects-Test-Automation-Serenity-Screenplay/), not in the "actor model" sense.

As in our application a unit of isolation is "user", I will introduce a special class called `User` that holds the user context and can interact with the driver.

Here's how my tests look like after introducing the `User` actor:

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
public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsBelowAllowedMinimum()
{
  //GIVEN
  await using var driver = new AppDriver();
  await driver.StartAsync();
  using var user = new User(driver);

  //WHEN
  using var reportForecastResponse = 
    await user.AttemptToReportNewForecast(
      forecast => forecast with {TemperatureC = -101});

  //THEN
  reportForecastResponse.ShouldBeRejectedAsBadRequest();
  driver.Notifications.ShouldNotIncludeAnything();
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
  using var allForecastsReportedByUser1 = 
    await user1.RetrieveAllReportedForecasts();

  //THEN
  await allForecastsReportedByUser1.ShouldConsistOf(user1.AllReportedForecasts());
}
```

While the naming could be improved (a reader might be confused about e.g. `RetrieveAllReportedForecasts` vs `AllReportedForecasts`), I hope you get the idea.

This form of driver pattern with actors is typically the ultimate form I arrive at when a piece of code I maintain grows for a longer period of time. Every time I add new tests I refactor the automation layer to make it more "domain-oriented" and flexible at the same time.

This concludes my set of examples.

# Driver pattern - advantages

The biggest advantage is that it brings the test very close to a statement of intention - when done well, driver pattern removes a lot of noise from the test body. This may improve readability of the tests and (at some point) the ability to create new scenarios without digging through a lot of low-level code.

# Driver pattern - disadvantages

I guess the biggest disadvantage of the pattern is that a lot of complexity is pushed into the driver, so care needs to be taken to not make it an overcomplicated mess. Also, advanced fluent API design skills help, because it's now always obvious which evolution path would be the best for a driver, so having more options is always useful.
