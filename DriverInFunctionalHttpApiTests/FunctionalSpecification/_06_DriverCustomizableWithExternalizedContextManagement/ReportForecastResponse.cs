using DriverPatternDemo;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagement;

public class ReportForecastResponse : IDisposable
{
  private readonly IFlurlResponse _httpResponse;

  public ReportForecastResponse(IFlurlResponse httpResponse)
  {
    _httpResponse = httpResponse;
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }

  public void ShouldBeRejectedAsBadRequest()
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.BadRequest);
  }

  public void ShouldBeSuccessful()
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
  }

  public async Task<Guid> GetId()
  {
    return (await _httpResponse.GetJsonAsync<ForecastCreationResultDto>()).Id;
  }
}