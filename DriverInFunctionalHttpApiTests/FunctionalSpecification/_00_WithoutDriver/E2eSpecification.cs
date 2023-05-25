using System.Diagnostics.CodeAnalysis;
using DriverPatternDemo;

namespace FunctionalSpecification._00_WithoutDriver;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    var tenantId = Any.String();
    var userId = Any.String();

    //Start and configure a wiremock server for notifications
    using var notificationRecipient = WireMockServer.Start();
    notificationRecipient.Given(
      Request.Create()
        .WithPath("/notifications")
        .UsingPost()).RespondWith(
    Response.Create()
        .WithStatusCode(HttpStatusCode.OK));
    
    //Define request data
    var inputForecastDto = new WeatherForecastDto(
      tenantId, 
      userId, 
      Any.DateTime(),
      Any.Integer(),
      Any.String());

    //Configure application startup (override configuration)
    await using var webApp = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(c =>
      {
        c.ConfigureAppConfiguration(appConfig =>
          {
            appConfig.AddInMemoryCollection(new Dictionary<string, string?>
            {
              ["NotificationsConfiguration:BaseUrl"] = notificationRecipient.Url
            });
          })
          .UseEnvironment("Development");
      });

    //Start the application and obtain HTTP client
    using var client = new FlurlClient(webApp.CreateClient());

    //Report weather forecast
    using var reportForecastResponse = await client
      .Request("WeatherForecast")
      .PostJsonAsync(inputForecastDto);
    var resultDto = await reportForecastResponse.GetJsonAsync<ForecastCreationResultDto>();

    //Obtain weather forecast by id
    using var getForecastResponse = await client
      .Request("WeatherForecast")
      .AppendPathSegment(resultDto.Id)
      .AllowAnyHttpStatus()
      .GetAsync();

    //Assert that obtained forecast is the same as reported
    var weatherForecastDto = await getForecastResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(inputForecastDto);

    //Assert notification was sent about the forecast
    notificationRecipient.LogEntries.Should().ContainSingle(entry =>
      entry.RequestMatchResult.IsPerfectMatch
      && entry.RequestMessage.Path == "/notifications"
      && entry.RequestMessage.Method == "POST"
      && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
        entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
        tenantId, userId, inputForecastDto.TemperatureC));
  }
}