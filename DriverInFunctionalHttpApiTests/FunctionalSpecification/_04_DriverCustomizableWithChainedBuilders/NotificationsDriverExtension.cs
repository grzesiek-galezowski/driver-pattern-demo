using DriverPatternDemo;

namespace FunctionalSpecification._04_DriverCustomizableWithChainedBuilders;

public class NotificationsDriverExtension
{
  private readonly WeatherForecastDto _weatherForecastDto;
  private readonly WireMockServer _wireMockServer;
  private readonly string _tenantId;
  private readonly string _userId;

  public NotificationsDriverExtension(
    string userId,
    string tenantId,
    WireMockServer wireMockServer,
    WeatherForecastDto weatherForecastDto)
  {
    _userId = userId;
    _tenantId = tenantId;
    _wireMockServer = wireMockServer;
    _weatherForecastDto = weatherForecastDto;
  }

  public void ShouldIncludeNotificationAboutReportedForecast()
  {
    _wireMockServer.LogEntries.Should().ContainSingle(entry =>
      entry.RequestMatchResult.IsPerfectMatch
      && entry.RequestMessage.Path == "/notifications"
      && entry.RequestMessage.Method == "POST"
      && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
        entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
        _tenantId,
        _userId,
        _weatherForecastDto.TemperatureC));
  }

  public void ShouldNotIncludeAnything()
  {
    _wireMockServer.LogEntries.Should().BeEmpty();
  }
}