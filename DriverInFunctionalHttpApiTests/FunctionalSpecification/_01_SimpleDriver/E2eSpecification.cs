namespace FunctionalSpecification._01_SimpleDriver;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();
      
    await driver.ReportWeatherForecast();

    //WHEN
    using var retrievedForecast = await driver.GetReportedForecast();

    //THEN
    await retrievedForecast.ShouldBeTheSameAsReported();
      
    //not really part of the scenario...
    driver.NotificationAboutForecastReportedShouldBeSent();
  }
}

//Four deficiencies of this driver:
//1) There may be a lot of methods, so the interface might get heavy,
//   especially when there are wiremocks
//2) All the values are decided internally, (see tenant id),
//   so it might be difficult to override default values
//3) _lastInput lifetime is managed internally
//4) _lastOutput lifetime is managed internally