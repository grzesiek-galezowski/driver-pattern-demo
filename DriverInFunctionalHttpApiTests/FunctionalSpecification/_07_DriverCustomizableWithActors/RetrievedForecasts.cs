using DriverPatternDemo;

namespace FunctionalSpecification._07_DriverCustomizableWithActors;

public class RetrievedForecasts : IDisposable
{
  private readonly IFlurlResponse _flurlResponse;

  public RetrievedForecasts(IFlurlResponse flurlResponse)
  {
    _flurlResponse = flurlResponse;
  }

  public void Dispose()
  {
    _flurlResponse.Dispose();
  }

  public void ShouldIndicateSuccess()
  {
    _flurlResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
  }

  public async Task ShouldConsistOf(params WeatherForecastReportBuilder[] builders)
  {
    var expectedDtos = builders.Select(b => b.Build());
    var actualDtos = await _flurlResponse.GetJsonAsync<IEnumerable<WeatherForecastDto>>();

    actualDtos.Should().Equal(expectedDtos);
  }
}