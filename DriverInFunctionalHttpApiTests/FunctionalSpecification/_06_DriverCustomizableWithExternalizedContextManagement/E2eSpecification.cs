namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagement;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();
    var weatherForecast = new WeatherForecastReportBuilder();

    using var reportForecastResponse =
      await driver.WeatherForecastApi.Report(weatherForecast);

    //WHEN
    using var retrievedForecast =
      await driver.WeatherForecastApi.GetReportedForecastBy(
        await reportForecastResponse.GetId());

    //THEN
    await retrievedForecast.ShouldBeTheSameAs(weatherForecast);

    //not really part of the scenario...
    driver.Notifications.ShouldIncludeNotificationAbout(weatherForecast);
  }

  [Fact]
  public async Task ShouldAllowRetrievingReportsFromAParticularUser()
  {
    //GIVEN
    var userId1 = Any.String();
    var userId2 = Any.String();
    var tenantId1 = Any.String();
    var tenantId2 = Any.String();
    var user1Tenant1ReportTemplate = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user1Forecast1 = user1Tenant1ReportTemplate
      .WithDistinctValidTemperatureC();
    var user1Forecast2 = user1Tenant1ReportTemplate
      .WithDistinctValidTemperatureC();
    var user2Forecast = new WeatherForecastReportBuilder()
      .WithUserId(userId2)
      .WithTenantId(tenantId2)
      .WithDistinctValidTemperatureC();

    await using var driver = new AppDriver();
    await driver.Start();

    using var responseForUser1Forecast1 =
      await driver.WeatherForecastApi.Report(user1Forecast1);
    using var responseForUser1Forecast2 =
      await driver.WeatherForecastApi.Report(user1Forecast2);
    using var responseForUser2Forecast =
      await driver.WeatherForecastApi.Report(user2Forecast);

    //WHEN
    using var retrievedForecasts =
      await driver.WeatherForecastApi.GetReportedForecastsFrom(userId1, tenantId1);

    //THEN
    await retrievedForecasts.ShouldConsistOf(user1Forecast1, user1Forecast2);
  }

  [Fact]
  public async Task ShouldAllowRetrievingAnyPastReport()
  {
    //GIVEN
    var userId1 = Any.String();
    var userId2 = Any.String();
    var tenantId1 = Any.String();
    var tenantId2 = Any.String();
    var user1Tenant1ReportTemplate = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user1Forecast1 = user1Tenant1ReportTemplate
      .WithDistinctValidTemperatureC();
    var user1Forecast2 = user1Tenant1ReportTemplate
      .WithDistinctValidTemperatureC();
    var user2Forecast = new WeatherForecastReportBuilder()
      .WithUserId(userId2)
      .WithTenantId(tenantId2)
      .WithDistinctValidTemperatureC();

    await using var driver = new AppDriver();
    await driver.Start();

    using var responseForUser1Forecast1 =
      await driver.WeatherForecastApi.Report(user1Forecast1);
    using var responseForUser1Forecast2 =
      await driver.WeatherForecastApi.Report(user1Forecast2);
    using var responseForUser2Forecast =
      await driver.WeatherForecastApi.Report(user2Forecast);

    //WHEN
    using var retrievedForecast =
      await driver.WeatherForecastApi.GetReportedForecastBy(
        await responseForUser1Forecast1.GetId());

    //THEN
    await retrievedForecast.ShouldBeTheSameAs(user1Forecast1);
  }

  [Fact]
  public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.Start();

    //WHEN
    using var reportForecastResponse =
      await driver.WeatherForecastApi
        .AttemptToReportForecast(
          new WeatherForecastReportBuilder().WithTemperatureC(-101));

    //THEN
    reportForecastResponse.ShouldBeRejectedAsBadRequest();
    driver.Notifications.ShouldNotIncludeAnything();
  }
}