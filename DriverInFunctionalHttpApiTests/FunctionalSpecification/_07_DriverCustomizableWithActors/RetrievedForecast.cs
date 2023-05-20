using DriverPatternDemo;

namespace FunctionalSpecification._07_DriverCustomizableWithActors;

public class RetrievedForecast : IDisposable
{
  private readonly IFlurlResponse _httpResponse;

  public RetrievedForecast(IFlurlResponse httpResponse)
  {
    _httpResponse = httpResponse;
  }

  public async Task ShouldBeTheSameAs(WeatherForecastReportBuilder expectedBuilder)
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
    var weatherForecastDto = await _httpResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(expectedBuilder.Build());
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }
}