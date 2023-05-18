using DriverPatternDemo;

namespace FunctionalSpecification._01_SimpleDriver;

public class E2ESpecification
{
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
}

//Four deficiencies of this driver:
//1) There may be a lot of methods, so the interface might get heavy,
//   especially when there are wiremocks
//2) All the values are decided internally, (see tenant id),
//   so it might be difficult to override default values
//3) _lastInput lifetime is managed internally
//4) _lastOutput lifetime is managed internally

public class AppDriver : IAsyncDisposable
{
  private Maybe<ForecastCreationResultDto> _lastReportResult; //not pretty
  private Maybe<WeatherForecastDto> _lastInputForecastDto; //not pretty
  private readonly string _tenantId = Any.String();
  private readonly string _userId = Any.String();
  private readonly WireMockServer _notificationRecipient;
  private readonly WebApplicationFactory<Program> _webApplicationFactory;

  private FlurlClient HttpClient => new(_webApplicationFactory.CreateClient());

  public AppDriver()
  {
    _notificationRecipient = WireMockServer.Start();
    _webApplicationFactory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(webBuilder =>
        webBuilder
          .UseTestServer()
          .ConfigureAppConfiguration(appConfig =>
          {
            appConfig.AddInMemoryCollection(new Dictionary<string, string>
            {
              ["NotificationsConfiguration:BaseUrl"] = _notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development")
      );
  }

  public async Task StartAsync()
  {
    _notificationRecipient.Given(
      Request.Create()
        .WithPath("/notifications")
        .UsingPost()).RespondWith(
      Response.Create()
        .WithStatusCode(HttpStatusCode.OK));

    ForceStart();
  }

  public async Task ReportWeatherForecast()
  {
    _lastInputForecastDto = new WeatherForecastDto(
      _tenantId,
      _userId,
      Any.DateTime(),
      Any.Integer(),
      Any.String()).Just();

    var httpResponse = await HttpClient
      .Request("WeatherForecast")
      .PostJsonAsync(_lastInputForecastDto.Value);
    _lastReportResult = await httpResponse.GetJsonAsync<ForecastCreationResultDto?>().JustAsync();
  }

  public async Task<RetrievedForecast> GetReportedForecast()
  {
    var httpResponse = await HttpClient.Request("WeatherForecast")
      .AppendPathSegment(_lastReportResult.Value.Id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse, _lastInputForecastDto.Value);
  }

  public async ValueTask DisposeAsync()
  {
    await _webApplicationFactory.DisposeAsync();
    _notificationRecipient.Dispose();
  }

  public void NotificationAboutForecastReportedShouldBeSent()
  {
    _notificationRecipient.LogEntries.Should().ContainSingle(entry =>
      entry.RequestMatchResult.IsPerfectMatch
      && entry.RequestMessage.Path == "/notifications"
      && entry.RequestMessage.Method == "POST"
      && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
        entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
        _tenantId, 
        _userId, 
        _lastInputForecastDto.Value.TemperatureC));
  }

  private void ForceStart()
  {
    _ = HttpClient;
  }

}

public class RetrievedForecast : IDisposable
{
  private readonly IFlurlResponse _httpResponse;
  private readonly WeatherForecastDto _lastInputForecastDto;

  public RetrievedForecast(IFlurlResponse httpResponse, WeatherForecastDto lastInputForecastDto)
  {
    _httpResponse = httpResponse;
    _lastInputForecastDto = lastInputForecastDto;
  }

  public async Task ShouldBeTheSameAsReported()
  {
    _httpResponse.StatusCode.Should().Be((int)HttpStatusCode.OK);
    var weatherForecastDto = await _httpResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(_lastInputForecastDto);
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }
}