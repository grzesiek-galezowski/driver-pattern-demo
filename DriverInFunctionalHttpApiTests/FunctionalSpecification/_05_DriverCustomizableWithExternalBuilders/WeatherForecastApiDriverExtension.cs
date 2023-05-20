using DriverPatternDemo;

namespace FunctionalSpecification._05_DriverCustomizableWithExternalBuilders;

public class WeatherForecastApiDriverExtension
{
  private readonly IFlurlClient _httpClient;
  private readonly Maybe<ForecastCreationResultDto> _lastReportResult;
  private readonly IAppDriverContext _driverContext;

  public WeatherForecastApiDriverExtension(
    IAppDriverContext driverContext,
    IFlurlClient httpClient,
    Maybe<ForecastCreationResultDto> lastReportResult)
  {
    _driverContext = driverContext;
    _httpClient = httpClient;
    _lastReportResult = lastReportResult;
  }

  public async Task Report(WeatherForecastReportBuilder weatherForecastBuilder)
  {
    var httpResponse = await AttemptToReportForecastViaHttp(weatherForecastBuilder);
    var jsonResponse = await httpResponse.GetJsonAsync<ForecastCreationResultDto>();
    _driverContext.SaveAsLastForecastReportResult(jsonResponse);
  }

  public async Task<ReportForecastResponse> AttemptToReportForecast(
    WeatherForecastReportBuilder builder)
  {
    var httpResponse = await AttemptToReportForecastViaHttp(builder);
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp(
    WeatherForecastReportBuilder builder)
  {
    var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .AllowAnyHttpStatus()
      .PostJsonAsync(builder.Build());
    return httpResponse;
  }

  public async Task<RetrievedForecast> GetReportedForecast()
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(_lastReportResult.Value().Id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse);
  }
}