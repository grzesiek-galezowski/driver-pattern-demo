using DriverPatternDemo;

namespace FunctionalSpecification._02_DriverWithExtensionObjects;

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
    var forecastDto = new WeatherForecastDto(
      _tenantId, 
      _userId,
      Any.DateTime(),
      Any.Integer(),
      Any.String());

    using var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .PostJsonAsync(forecastDto);
    var jsonResponse = await httpResponse.GetJsonAsync<ForecastCreationResultDto>();

    _driverContext.SaveAsLastReportedForecast(forecastDto);
    _driverContext.SaveAsLastForecastReportResult(jsonResponse);
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