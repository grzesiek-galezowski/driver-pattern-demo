using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithChainedBuilders;

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

  public async Task<RetrievedForecast> GetReportedForecast()
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(_lastReportResult.Value().Id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse, _lastInputForecastDto.Value());
  }

  public ReportForecastCommand ReportForecast()
  {
    return new ReportForecastCommand(
      _userId,
      _tenantId,
      _driverContext,
      _httpClient);
  }
}