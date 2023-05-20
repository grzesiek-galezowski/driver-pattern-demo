namespace FunctionalSpecification._05_DriverCustomizableWithExternalBuilders;

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
}