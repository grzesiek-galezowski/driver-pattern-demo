using DriverPatternDemo;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagement;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();
    var weatherForecast = new WeatherForecastReportBuilder();

    using var reportForecastResponse = await driver.WeatherForecastApi.Report(weatherForecast);

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
    await using var driver = new AppDriver();
    await driver.StartAsync();
    var user1Forecast1 = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user1Forecast2 = new WeatherForecastReportBuilder()
      .WithUserId(userId1)
      .WithTenantId(tenantId1);
    var user2Forecast = new WeatherForecastReportBuilder()
      .WithUserId(userId2)
      .WithTenantId(tenantId2);

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
  public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();

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

public class AppDriver : IAsyncDisposable
{
  private readonly WireMockServer _notificationRecipient;
  private readonly WebApplicationFactory<Program> _webApplicationFactory;
  private Maybe<FlurlClient> _httpClient;

  public AppDriver()
  {
    _notificationRecipient = WireMockServer.Start();
    _webApplicationFactory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(webBuilder =>
        webBuilder
          .UseTestServer()
          .ConfigureAppConfiguration(appConfig =>
          {
            appConfig.AddInMemoryCollection(new Dictionary<string, string?>
            {
              ["NotificationsConfiguration:BaseUrl"] 
                = _notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development")
      );
  }

  public async Task StartAsync()
  {
    _notificationRecipient.Given(
      Request.Create()
        .WithPath("/notifications")
        .UsingPost()).RespondWith(
      Response.Create()
        .WithStatusCode(HttpStatusCode.OK));

    ForceStart();
  }

  public async ValueTask DisposeAsync()
  {
    _httpClient.Value().Dispose();
    await _webApplicationFactory.DisposeAsync();
    _notificationRecipient.Dispose();
  }

  public NotificationsDriverExtension Notifications =>
    new(_notificationRecipient);

  public WeatherForecastApiDriverExtension WeatherForecastApi
    => new(_httpClient.Value());

  private void ForceStart()
  {
    _httpClient = new FlurlClient(_webApplicationFactory.CreateClient()).Just();
  }
}

public class NotificationsDriverExtension
{
  private readonly WireMockServer _wireMockServer;

  public NotificationsDriverExtension(WireMockServer wireMockServer)
  {
    _wireMockServer = wireMockServer;
  }

  public void ShouldIncludeNotificationAbout(WeatherForecastReportBuilder builder)
  {
    var dto = builder.Build();
    _wireMockServer.LogEntries.Should().ContainSingle(entry =>
      entry.RequestMatchResult.IsPerfectMatch
      && entry.RequestMessage.Path == "/notifications"
      && entry.RequestMessage.Method == "POST"
      && JsonConvert.DeserializeObject<WeatherForecastSuccessfullyReportedEventDto>(
        entry.RequestMessage.Body) == new WeatherForecastSuccessfullyReportedEventDto(
        dto.TenantId,
        dto.UserId,
        dto.TemperatureC));
  }

  public void ShouldNotIncludeAnything()
  {
    _wireMockServer.LogEntries.Should().BeEmpty();
  }
}

public class WeatherForecastApiDriverExtension
{
  private readonly IFlurlClient _httpClient;

  public WeatherForecastApiDriverExtension(
    IFlurlClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<ReportForecastResponse> Report(WeatherForecastReportBuilder weatherForecastDto)
  {
    var response = await AttemptToReportForecast(weatherForecastDto);
    response.ShouldBeSuccessful();
    return response;
  }


  public async Task<ReportForecastResponse> AttemptToReportForecast(WeatherForecastReportBuilder builder)
  {
    var httpResponse = await AttemptToReportForecastViaHttp(builder);
    return new ReportForecastResponse(httpResponse);
  }

  private async Task<IFlurlResponse> AttemptToReportForecastViaHttp(WeatherForecastReportBuilder builder)
  {
    var httpResponse = await _httpClient
      .Request("WeatherForecast")
      .AllowAnyHttpStatus()
      .PostJsonAsync(builder.Build());
    return httpResponse;
  }

  public async Task<RetrievedForecast> GetReportedForecastBy(Guid id)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(id)
      .AllowAnyHttpStatus()
      .GetAsync();

    return new RetrievedForecast(httpResponse);
  }

  public async Task<RetrievedForecasts> GetReportedForecastsFrom(string userId, string tenantId)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(tenantId)
      .AppendPathSegment(userId)
      .AllowAnyHttpStatus()
      .GetAsync();

    var reportedForecasts = new RetrievedForecasts(httpResponse);
    reportedForecasts.ShouldIndicateSuccess();
    return reportedForecasts;
  }
}

public class RetrievedForecasts : IDisposable
{
  private readonly IFlurlResponse _flurlResponse;

  public RetrievedForecasts(IFlurlResponse flurlResponse)
  {
    _flurlResponse = flurlResponse;
  }

  public void Dispose()
  {
    _flurlResponse.Dispose();
  }

  public void ShouldIndicateSuccess()
  {
    _flurlResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
  }

  public async Task ShouldConsistOf(params WeatherForecastReportBuilder[] builders)
  {
    var expectedDtos = builders.Select(b => b.Build());
    var actualDtos = await _flurlResponse.GetJsonAsync<IEnumerable<WeatherForecastDto>>();

    actualDtos.Should().Equal(expectedDtos);
  }
}

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

  public void ShouldBeSuccessful()
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
  }

  public async Task<Guid> GetId()
  {
    return (await _httpResponse.GetJsonAsync<ForecastCreationResultDto>()).Id;
  }
}

public class RetrievedForecast : IDisposable
{
  private readonly IFlurlResponse _httpResponse;

  public RetrievedForecast(IFlurlResponse httpResponse)
  {
    _httpResponse = httpResponse;
  }

  public async Task ShouldBeTheSameAs(WeatherForecastReportBuilder expectedBuilder)
  {
    _httpResponse.StatusCode.Should().Be((int) HttpStatusCode.OK);
    var weatherForecastDto = await _httpResponse.GetJsonAsync<WeatherForecastDto>();
    weatherForecastDto.Should().BeEquivalentTo(expectedBuilder.Build());
  }

  public void Dispose()
  {
    _httpResponse.Dispose();
  }
}

public record WeatherForecastReportBuilder
{
  private string TenantId { get; init; } = Any.String();
  private string UserId { get; init; } = Any.String();
  private DateTime Time { get; init; } = Any.DateTime();
  private int TemperatureC { get; init; } = -100;
  private string Summary { get; init; } = Any.String();

  public WeatherForecastReportBuilder WithTenantId(string tenantId)
  {
    return this with { TenantId = tenantId };
  }
  public WeatherForecastReportBuilder WithUserId(string userId)
  {
    return this with { UserId = userId };
  }
  public WeatherForecastReportBuilder WithTime(DateTime time)
  {
    return this with { Time = time };
  }
  public WeatherForecastReportBuilder WithTemperatureC(int temperatureC)
  {
    return this with { TemperatureC = temperatureC };
  }
  public WeatherForecastReportBuilder WithSummary(string summary)
  {
    return this with { Summary = summary };
  }

  public WeatherForecastDto Build()
  {
    return new WeatherForecastDto(
      TenantId, 
      UserId, 
      Time,
      TemperatureC,
      Summary);
  }
}