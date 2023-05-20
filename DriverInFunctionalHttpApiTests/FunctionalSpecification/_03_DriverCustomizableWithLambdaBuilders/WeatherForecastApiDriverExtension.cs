using DriverPatternDemo;

namespace FunctionalSpecification._03_DriverCustomizableWithLambdaBuilders;

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