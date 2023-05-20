using DriverPatternDemo;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagementAndRegistrationList;

public class AppDriver : IAsyncDisposable
{
  private readonly WireMockServer _notificationRecipient;
  private readonly Disposables _disposables = new();
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
              ["NotificationsConfiguration:BaseUrl"] = 
                _notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development")
      );

    _disposables.AddDisposable(_notificationRecipient);
    _disposables.AddAsyncDisposable(_webApplicationFactory);
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

  public NotificationsDriverExtension Notifications =>
    new(_notificationRecipient);

  public WeatherForecastApiDriverExtension WeatherForecastApi
    => new(_httpClient.Value(), _disposables);

  public async ValueTask DisposeAsync()
  {
    await _disposables.DisposeAsync();
  }

  private void ForceStart()
  {
    _httpClient = new FlurlClient(_webApplicationFactory.CreateClient()).Just();
  }
}