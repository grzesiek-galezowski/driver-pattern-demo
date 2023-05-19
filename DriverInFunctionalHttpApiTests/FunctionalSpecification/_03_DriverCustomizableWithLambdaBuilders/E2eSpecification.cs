using DriverPatternDemo;

namespace FunctionalSpecification._03_DriverCustomizableWithLambdaBuilders;

public class E2ESpecification
{
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

  [Fact]
  public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();

    //WHEN
    using var reportForecastResponse = 
      await driver.WeatherForecastApi
        .AttemptToReportForecast(request => request.WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}

//Two deficiencies of this driver:
//1) _lastInput lifetime is managed internally
//2) _lastOutput lifetime is managed internally

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

public class NotificationsDriverExtension
{
  private readonly WeatherForecastDto _weatherForecastDto;
  private readonly WireMockServer _wireMockServer;
  private readonly string _tenantId;
  private readonly string _userId;

  public NotificationsDriverExtension(
    string userId,
    string tenantId,
    WireMockServer wireMockServer,
    WeatherForecastDto weatherForecastDto)
  {
    _userId = userId;
    _tenantId = tenantId;
    _wireMockServer = wireMockServer;
    _weatherForecastDto = weatherForecastDto;
  }

  public void ShouldIncludeNotificationAboutReportedForecast()
  {
    _wireMockServer.LogEntries.Should().ContainSingle(entry =>
      entry.RequestMatchResult.IsPerfectMatch
      && entry.RequestMessage.Path == "/notifications"
      && entry.RequestMessage.Method == "POST"
      && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
        entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
        _tenantId,
        _userId,
        _weatherForecastDto.TemperatureC));
  }

  public void ShouldNotIncludeAnything()
  {
    _wireMockServer.LogEntries.Should().BeEmpty();
  }
}

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto jsonResponse);
  void SaveAsLastReportedForecast(WeatherForecastDto forecastDto);
}

public record WeatherForecastReportBuilder(string UserId, string TenantId)
{
  private DateTime Time { get; init; } = Any.DateTime();
  private int TemperatureC { get; init; } = -100;
  private string Summary { get; init; } = Any.String();

  public WeatherForecastReportBuilder WithTenantId(string tenantId)
  {
    return this with { TenantId = tenantId };
  }
  public WeatherForecastReportBuilder WithUserId(string userId)
  {
    return this with { UserId = userId };
  }
  public WeatherForecastReportBuilder WithTime(DateTime time)
  {
    return this with { Time = time };
  }
  public WeatherForecastReportBuilder WithTemperatureC(int temperatureC)
  {
    return this with { TemperatureC = temperatureC };
  }
  public WeatherForecastReportBuilder WithSummary(string summary)
  {
    return this with { Summary = summary };
  }

  public WeatherForecastDto Build()
  {
    return new WeatherForecastDto(
      TenantId, 
      UserId, 
      Time,
      TemperatureC,
      Summary);
  }
}

public class WeatherForecastApiDriverExtension
{
  private readonly IFlurlClient _httpClient;
  private readonly Maybe<ForecastCreationResultDto> _lastReportResult;
  private readonly Maybe<WeatherForecastDto> _lastInputForecastDto;
  private readonly string _userId;
  private readonly string _tenantId;
  private readonly IAppDriverContext _driverContext;

  public WeatherForecastApiDriverExtension(
    IAppDriverContext driverContext,
    string tenantId,
    string userId,
    IFlurlClient httpClient,
    Maybe<ForecastCreationResultDto> lastReportResult,
    Maybe<WeatherForecastDto> lastInputForecastDto)
  {
    _driverContext = driverContext;
    _tenantId = tenantId;
    _userId = userId;
    _httpClient = httpClient;
    _lastReportResult = lastReportResult;
    _lastInputForecastDto = lastInputForecastDto;
  }

  public async Task ReportForecast()
  {
    var httpResponse = await AttemptToReportForecastViaHttp(_ => _);
    var jsonResponse = await httpResponse.GetJsonAsync<ForecastCreationResultDto>();
    _driverContext.SaveAsLastForecastReportResult(jsonResponse);
  }

  public Task<ReportForecastResponse> AttemptToReportForecast()
  {
    return AttemptToReportForecast(_ => _);
  }

  public async Task<ReportForecastResponse> AttemptToReportForecast(Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var httpResponse = await AttemptToReportForecastViaHttp(customize);
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp(Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var forecastDto = customize(
      new WeatherForecastReportBuilder(_userId, _tenantId)).Build();
    _driverContext.SaveAsLastReportedForecast(forecastDto);

    var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .AllowAnyHttpStatus()
      .PostJsonAsync(forecastDto);
    return httpResponse;
  }

  public async Task<RetrievedForecast> GetReportedForecast()
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(_lastReportResult.Value().Id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse, _lastInputForecastDto.Value());
  }
}

public class ReportForecastResponse : IDisposable
{
  private readonly IFlurlResponse _httpResponse;

  public ReportForecastResponse(IFlurlResponse httpResponse)
  {
    _httpResponse = httpResponse;
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }

  public void ShouldBeRejectedAsBadRequest()
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.BadRequest);
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
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
    var weatherForecastDto = await _httpResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(_lastInputForecastDto);
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }
}