namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagement;

public class WeatherForecastApiDriverExtension
{
  private readonly IFlurlClient _httpClient;

  public WeatherForecastApiDriverExtension(
    IFlurlClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<ReportForecastResponse> Report(WeatherForecastReportBuilder weatherForecastDto)
  {
    var response = await AttemptToReportForecast(weatherForecastDto);
    response.ShouldBeSuccessful();
    return response;
  }


  public async Task<ReportForecastResponse> AttemptToReportForecast(WeatherForecastReportBuilder builder)
  {
    var httpResponse = await AttemptToReportForecastViaHttp(builder);
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp(WeatherForecastReportBuilder builder)
  {
    var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .AllowAnyHttpStatus()
      .PostJsonAsync(builder.Build());
    return httpResponse;
  }

  public async Task<RetrievedForecast> GetReportedForecastBy(Guid id)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse);
  }

  public async Task<RetrievedForecasts> GetReportedForecastsFrom(string userId, string tenantId)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(tenantId)
      .AppendPathSegment(userId)
      .AllowAnyHttpStatus()
      .GetAsync();

    var reportedForecasts = new RetrievedForecasts(httpResponse);
    reportedForecasts.ShouldIndicateSuccess();
    return reportedForecasts;
  }
}