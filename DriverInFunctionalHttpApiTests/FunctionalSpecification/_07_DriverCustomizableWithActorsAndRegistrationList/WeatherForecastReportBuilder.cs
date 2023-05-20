using DriverPatternDemo;

namespace FunctionalSpecification._07_DriverCustomizableWithActorsAndRegistrationList;

public record WeatherForecastReportBuilder
{
  private string TenantId { get; init; } = Any.String();
  private string UserId { get; init; } = Any.String();
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