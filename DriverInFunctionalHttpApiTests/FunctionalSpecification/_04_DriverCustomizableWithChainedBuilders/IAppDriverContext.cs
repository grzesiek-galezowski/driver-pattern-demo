using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithChainedBuilders;

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto jsonResponse);
  void SaveAsLastReportedForecast(WeatherForecastDto forecastDto);
}