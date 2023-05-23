namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagementAndRegistrationList;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    var weatherForecast = new WeatherForecastReportBuilder();
    await using var driver = new AppDriver();
    await driver.Start();

    var reportForecastResponse = 
      await driver.WeatherForecastApi.Report(weatherForecast);

    //WHEN
    var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecastBy(
      await reportForecastResponse.GetId());

    //THEN
    await retrievedForecast.ShouldBeTheSameAs(weatherForecast);

    //not really part of the scenario...
    driver.Notifications.ShouldIncludeNotificationAbout(weatherForecast);
  }
    
  [Fact]
  public async Task 
    ShouldAllowRetrievingReportsFromAParticularUser()
  {
    //GIVEN
    var userId1 = Any.String();
    var userId2 = Any.String();
    var tenantId1 = Any.String();
    var tenantId2 = Any.String();
    var user1Forecast1 = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user1Forecast2 = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user2Forecast = new WeatherForecastReportBuilder()
      .WithUserId(userId2)
      .WithTenantId(tenantId2);

    await using var driver = new AppDriver();
    await driver.Start();

    await driver.WeatherForecastApi.Report(user1Forecast1);
    await driver.WeatherForecastApi.Report(user1Forecast2);
    await driver.WeatherForecastApi.Report(user2Forecast);

    //WHEN
    var retrievedForecast = 
      await driver.WeatherForecastApi.GetReportedForecastsFrom(userId1, tenantId1);

    //THEN
    await retrievedForecast.ShouldConsistOf(user1Forecast1, user1Forecast2);
  }

  [Fact]
  public async Task 
    ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();

    //WHEN
    var reportForecastResponse = 
      await driver.WeatherForecastApi.AttemptToReportForecast(
        new WeatherForecastReportBuilder().WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}