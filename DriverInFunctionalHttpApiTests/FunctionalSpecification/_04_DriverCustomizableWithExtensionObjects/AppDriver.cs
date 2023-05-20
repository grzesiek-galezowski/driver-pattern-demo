using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithExtensionObjects;

public class AppDriver : IAsyncDisposable, IAppDriverContext
{
  private Maybe<ForecastCreationResultDto> _lastReportResult; //not pretty
  private Maybe<WeatherForecastDto> _lastInputForecastDto; //not pretty
  private readonly string _tenantId = Any.String();
  private readonly string _userId = Any.String();
  private readonly WireMockServer _notificationRecipient;
  private readonly WebApplicationFactory<Program> _webApplicationFactory;
  private Maybe<FlurlClient> _httpClient;

  public AppDriver()
  {
    _notificationRecipient = WireMockServer.Start();
    _webApplicationFactory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(webBuilder =>
        webBuilder
          .UseTestServer()
          .ConfigureAppConfiguration(appConfig =>
          {
            appConfig.AddInMemoryCollection(new Dictionary<string, string?>
            {
              ["NotificationsConfiguration:BaseUrl"] = _notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development")
      );
  }

  public async Task Start()
  {
    _notificationRecipient.Given(
      Request.Create()
        .WithPath("/notifications")
        .UsingPost()).RespondWith(
      Response.Create()
        .WithStatusCode(HttpStatusCode.OK));

    ForceStart();
  }

  public NotificationsDriverExtension Notifications =>
    new(_userId,
      _tenantId,
      _notificationRecipient,
      _lastInputForecastDto.Value());

  public WeatherForecastApiDriverExtension WeatherForecastApi =>
    new(this,
      _tenantId,
      _userId,
      _httpClient.Value(),
      _lastReportResult,
      _lastInputForecastDto);

  //Note explicit implementation
  void IAppDriverContext.SaveAsLastForecastReportResult(ForecastCreationResultDto dto)
  {
    _lastReportResult = dto.Just();
  }

  //Note explicit implementation
  void IAppDriverContext.SaveAsLastReportedForecast(WeatherForecastDto forecastDto)
  {
    _lastInputForecastDto = forecastDto.Just();
  }

  public async ValueTask DisposeAsync()
  {
    _httpClient.Value().Dispose();
    await _webApplicationFactory.DisposeAsync();
    _notificationRecipient.Dispose();
  }

  private void ForceStart()
  {
    _httpClient = new FlurlClient(_webApplicationFactory.CreateClient()).Just();
  }
}