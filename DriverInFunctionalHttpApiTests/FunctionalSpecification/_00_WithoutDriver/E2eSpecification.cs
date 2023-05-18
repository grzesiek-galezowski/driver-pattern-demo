using DriverPatternDemo;

namespace FunctionalSpecification._00_WithoutDriver;

public class E2ESpecification
{
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
      Any.DateTime(),
      Any.Integer(),
      Any.String());

    await using var webApp = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(c =>
      {
        c.ConfigureAppConfiguration(appConfig =>
          {
            appConfig.AddInMemoryCollection(new Dictionary<string, string>
            {
              ["NotificationsConfiguration:BaseUrl"] = notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development");
      });

    var client = new FlurlClient(webApp.CreateClient());

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
}