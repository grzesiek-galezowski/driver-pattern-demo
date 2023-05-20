using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithChainedBuilders;

public record ReportForecastCommand
{
  private DateTime _time = Any.DateTime();
  private int _temperatureC = -100;
  private string _summary = Any.String();
  private string _userId;
  private string _tenantId;
  private readonly IAppDriverContext _driverContext;
  private readonly IFlurlClient _httpClient;

  public ReportForecastCommand(
    string userId,
    string tenantId,
    IAppDriverContext driverContext,
    IFlurlClient httpClient)
  {
    _userId = userId;
    _tenantId = tenantId;
    _driverContext = driverContext;
    _httpClient = httpClient;
  }

  public ReportForecastCommand WithTenantId(string tenantId)
  {
    return this with { _tenantId = tenantId };
  }
  
  public ReportForecastCommand WithUserId(string userId)
  {
    return this with { _userId = userId };
  }
  
  public ReportForecastCommand WithTime(DateTime time)
  {
    return this with { _time = time };
  }
  
  public ReportForecastCommand WithTemperatureC(int temperatureC)
  {
    return this with { _temperatureC = temperatureC };
  }

  public ReportForecastCommand WithSummary(string summary)
  {
    return this with { _summary = summary };
  }

  public async Task Run()
  {
    var httpResponse = await AttemptToReportForecastViaHttp();
    var jsonResponse = await httpResponse.GetJsonAsync<ForecastCreationResultDto>();
    _driverContext.SaveAsLastForecastReportResult(jsonResponse);
  }

  public async Task<ReportForecastResponse> Attempt()
  {
    var httpResponse = await AttemptToReportForecastViaHttp();
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp()
  {
    var forecastDto = new WeatherForecastDto(
      _tenantId,
      _userId,
      _time,
      _temperatureC,
      _summary);
    _driverContext.SaveAsLastReportedForecast(forecastDto);

    var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .AllowAnyHttpStatus()
      .PostJsonAsync(forecastDto);
    return httpResponse;
  }
}