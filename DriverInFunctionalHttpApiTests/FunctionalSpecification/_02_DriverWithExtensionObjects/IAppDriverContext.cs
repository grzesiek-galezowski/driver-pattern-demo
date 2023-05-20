using DriverPatternDemo;

namespace FunctionalSpecification._02_DriverWithExtensionObjects;

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto dto);
  void SaveAsLastReportedForecast(WeatherForecastDto forecastDto);
}