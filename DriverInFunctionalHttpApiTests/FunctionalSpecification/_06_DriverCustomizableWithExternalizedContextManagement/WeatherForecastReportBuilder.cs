using System.Threading;
using DriverPatternDemo;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagement;

public record WeatherForecastReportBuilder
{
  private const int MinTemperatureC = -100;
  private static int _nextValidTemperature = MinTemperatureC;
  private string TenantId { get; init; } = Any.String();
  private string UserId { get; init; } = Any.String();
  private DateTime Time { get; init; } = Any.DateTime();
  private int TemperatureC { get; init; } = MinTemperatureC;
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
  public WeatherForecastReportBuilder WithDistinctValidTemperatureC()
  {
    return this with { TemperatureC = Interlocked.Increment(ref _nextValidTemperature) };
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