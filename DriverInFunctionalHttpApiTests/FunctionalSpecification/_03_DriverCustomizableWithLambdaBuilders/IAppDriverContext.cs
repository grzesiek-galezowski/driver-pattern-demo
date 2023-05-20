using DriverPatternDemo;

namespace FunctionalSpecification._03_DriverCustomizableWithLambdaBuilders;

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto jsonResponse);
  void SaveAsLastReportedForecast(WeatherForecastDto forecastDto);
}