using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithChainedBuilders;

public class RetrievedForecast : IDisposable
{
  private readonly IFlurlResponse _httpResponse;
  private readonly WeatherForecastDto _lastInputForecastDto;

  public RetrievedForecast(IFlurlResponse httpResponse, WeatherForecastDto lastInputForecastDto)
  {
    _httpResponse = httpResponse;
    _lastInputForecastDto = lastInputForecastDto;
  }

  public async Task ShouldBeTheSameAsReported()
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
    var weatherForecastDto = await _httpResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(_lastInputForecastDto);
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }
}