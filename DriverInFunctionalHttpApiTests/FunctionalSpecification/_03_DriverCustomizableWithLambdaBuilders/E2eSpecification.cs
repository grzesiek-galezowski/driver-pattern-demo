namespace FunctionalSpecification._03_DriverCustomizableWithLambdaBuilders;

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

  [Fact]
  public async Task 
    ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();

    //WHEN
    using var reportForecastResponse = 
      await driver.WeatherForecastApi
        .AttemptToReportForecast(request => request.WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}

//Two deficiencies of this driver:
//1) _lastInput lifetime is managed internally
//2) _lastOutput lifetime is managed internally