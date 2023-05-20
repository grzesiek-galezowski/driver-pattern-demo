using DriverPatternDemo;

namespace FunctionalSpecification._05_DriverCustomizableWithExternalBuilders;

public interface IAppDriverContext
{
  void SaveAsLastForecastReportResult(ForecastCreationResultDto jsonResponse);
}