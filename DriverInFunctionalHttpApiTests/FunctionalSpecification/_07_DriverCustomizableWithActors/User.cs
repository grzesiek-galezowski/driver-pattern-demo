namespace FunctionalSpecification._07_DriverCustomizableWithActors;

public class User : IDisposable
{
  private readonly AppDriver _driver;
  private readonly string _tenantId1 = Any.String();
  private readonly string _userId1 = Any.String();
  private readonly List<WeatherForecastReportBuilder> _reportedForecasts = new();
  private readonly List<ReportForecastResponse> _forecastCreationResponses = new();

  public User(AppDriver driver)
  {
    _driver = driver;
  }

  public async Task ReportNewForecast()
  {
    await ReportNewForecast(_ => _);
  }

  public async Task ReportNewForecast(
    Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var reportForecastResponse = await AttemptToReportNewForecast(customize);
    _forecastCreationResponses.Add(reportForecastResponse);
  }

  public async Task<ReportForecastResponse> AttemptToReportNewForecast(
    Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var forecast = customize(CreateForecast());
    _reportedForecasts.Add(forecast);
    return await _driver.WeatherForecastApi.AttemptToReportForecast(forecast);
  }

  public async Task<RetrievedForecasts> RetrieveAllReportedForecasts()
  {
    return await _driver.WeatherForecastApi.GetReportedForecastsFrom(
      _userId1, 
      _tenantId1);
  }

  public WeatherForecastReportBuilder[] AllReportedForecasts()
  {
    return _reportedForecasts.ToArray();
  }

  private WeatherForecastReportBuilder CreateForecast()
  {
    return new WeatherForecastReportBuilder()
      .WithUserId(_userId1)
      .WithTenantId(_tenantId1);
  }

  public void Dispose()
  {
    foreach (var response in _forecastCreationResponses)
    {
      response.Dispose();
    }
  }

  public ReportForecastResponse LastReportedForecastResponse()
  {
    return _forecastCreationResponses.Last();
  }

  public async Task<RetrievedForecast> RetrieveLastReportedForecast()
  {
    var retrievedForecast = await _driver.WeatherForecastApi.GetReportedForecastBy(
      await LastReportedForecastResponse().GetId());
    return retrievedForecast;
  }

  public WeatherForecastReportBuilder LastReportedForecast()
  {
    return _reportedForecasts.Last();
  }
}