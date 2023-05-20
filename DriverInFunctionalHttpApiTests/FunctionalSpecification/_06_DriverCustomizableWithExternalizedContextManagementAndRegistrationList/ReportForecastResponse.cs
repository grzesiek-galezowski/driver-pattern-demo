using DriverPatternDemo;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagementAndRegistrationList;

public class ReportForecastResponse
{
  private readonly IFlurlResponse _httpResponse;

  public ReportForecastResponse(IFlurlResponse httpResponse)
  {
    _httpResponse = httpResponse;
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