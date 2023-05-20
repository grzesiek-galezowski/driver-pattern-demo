using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithExtensionObjects;

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto jsonResponse);
  void SaveAsLastReportedForecast(WeatherForecastDto forecastDto);
}