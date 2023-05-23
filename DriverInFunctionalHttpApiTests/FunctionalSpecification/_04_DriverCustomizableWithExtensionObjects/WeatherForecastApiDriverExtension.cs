using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithExtensionObjects;

public class WeatherForecastApiDriverExtension
{
  private readonly IFlurlClient _httpClient;
  private readonly Maybe<ForecastCreationResultDto> _lastReportResult;
  private readonly Maybe<WeatherForecastDto> _lastInputForecastDto;
  private readonly string _userId;
  private readonly string _tenantId;
  private readonly IAppDriverContext _driverContext;
  private readonly DateTime _dateTime = Any.DateTime();
  private int _temperatureC = Any.Integer();
  private readonly string _summary = Any.String();

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
    var httpResponse = await AttemptToReportForecastViaHttp();
    var jsonResponse = await httpResponse.GetJsonAsync<ForecastCreationResultDto>();
    _driverContext.SaveAsLastForecastReportResult(jsonResponse);
  }

  public async Task<ReportForecastResponse> AttemptToReportForecast()
  {
    var httpResponse = await AttemptToReportForecastViaHttp();
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp()
  {
    var forecastDto = new WeatherForecastDto(
      _tenantId,
      _userId,
      _dateTime,
      _temperatureC,
      _summary);

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

  public WeatherForecastApiDriverExtension WithTemperatureOf(int temperature)
  {
    _temperatureC = temperature;
    return this;
  }
}