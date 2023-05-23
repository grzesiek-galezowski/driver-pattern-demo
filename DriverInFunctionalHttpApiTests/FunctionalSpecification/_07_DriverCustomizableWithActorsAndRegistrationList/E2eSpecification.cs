namespace FunctionalSpecification._07_DriverCustomizableWithActorsAndRegistrationList;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
    var user = new User(driver);

    await user.ReportNewForecast();

    //WHEN
    var retrievedForecast = await user.RetrieveLastReportedForecast();

    //THEN
    await retrievedForecast.ShouldBeTheSameAs(user.LastReportedForecast());

    //not really part of the scenario...
    driver.Notifications.ShouldIncludeNotificationAbout(user.LastReportedForecast());
  }

  [Fact]
  public async Task ShouldAllowRetrievingReportsFromAParticularUser()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
    var user1 = new User(driver);
    var user2 = new User(driver);

    await user1.ReportNewForecast();
    await user1.ReportNewForecast();
    await user2.ReportNewForecast();

    //WHEN
    var allForecastsFromUser1 = await user1.RetrieveAllReportedForecasts();

    //THEN
    await allForecastsFromUser1.ShouldConsistOf(user1.AllReportedForecasts());
  }

  [Fact]
  public async Task 
    ShouldRejectForecastReportAsBadRequestWhenTemperatureIsBelowAllowedMinimum()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
    var user = new User(driver);

    //WHEN
    var reportForecastResponse = 
      await user.AttemptToReportNewForecast(
        forecast => forecast.WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}