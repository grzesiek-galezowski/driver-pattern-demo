namespace FunctionalSpecification._05_DriverCustomizableWithExternalBuilders;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
    var weatherForecast = new WeatherForecastReportBuilder();

    await driver.WeatherForecastApi.Report(weatherForecast);

    //WHEN
    using var retrievedForecast = 
      await driver.WeatherForecastApi.GetReportedForecast();

    //THEN
    await retrievedForecast.ShouldBeTheSameAs(weatherForecast);

    //not really part of the scenario...
    driver.Notifications.ShouldIncludeNotificationAbout(weatherForecast);
  }

  [Fact]
  public async Task 
    ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();

    //WHEN
    using var reportForecastResponse = await driver.WeatherForecastApi
      .AttemptToReportForecast(
        new WeatherForecastReportBuilder()
          .WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}

//Two deficiencies of this driver:
//1) _lastOutput lifetime is managed internally
//2) so issue with more than one "entity"