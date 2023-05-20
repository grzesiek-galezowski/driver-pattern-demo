namespace FunctionalSpecification._02_DriverWithExtensionObjects;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
      
    await driver.WeatherForecastApi.ReportForecast();

    //WHEN
    using var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecast();

    //THEN
    await retrievedForecast.ShouldBeTheSameAsReported();
      
    //not really part of the scenario...
    driver.Notifications.ShouldIncludeNotificationAboutReportedForecast();
  }
}

//Three deficiencies of this driver:
//1) All the values are decided internally, (see tenant id),
//   so it might be difficult to override default values
//2) _lastInput lifetime is managed internally
//3) _lastOutput lifetime is managed internally