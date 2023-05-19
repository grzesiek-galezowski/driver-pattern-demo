using DriverPatternDemo;

namespace FunctionalSpecification._07_DriverCustomizableWithActorsAndRegistrationList;

public class E2ESpecification
{
  [Fact]
  public async Task ShouldAllowRetrievingReportedForecast()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();
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
    await driver.StartAsync();
    var user1 = new User(driver);
    var user2 = new User(driver);

    await user1.ReportNewForecast();
    await user1.ReportNewForecast();
    await user2.ReportNewForecast();

    //WHEN
    var allForecastsReportedByUser1 = await user1.RetrieveAllReportedForecasts();

    //THEN
    await allForecastsReportedByUser1.ShouldConsistOf(user1.AllReportedForecasts());
  }

  [Fact]
  public async Task 
    ShouldRejectForecastReportAsBadRequestWhenTemperatureIsBelowAllowedMinimum()
  {
    //GIVEN
    await using var driver = new AppDriver();
    await driver.StartAsync();
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

public class User
{
  private readonly AppDriver _driver;
  private readonly string _tenantId1 = Any.String();
  private readonly string _userId1 = Any.String();
  private readonly List<WeatherForecastReportBuilder> _reportedForecasts = new();
  private readonly List<ReportForecastResponse> _forecastCreationResponses = new();

  public User(AppDriver driver)
  {
    _driver = driver;
  }

  public async Task ReportNewForecast()
  {
    await ReportNewForecast(_ => _);
  }

  public async Task ReportNewForecast(
    Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var reportForecastResponse = await AttemptToReportNewForecast(customize);
    _forecastCreationResponses.Add(reportForecastResponse);
  }

  public async Task<ReportForecastResponse> AttemptToReportNewForecast(
    Func<WeatherForecastReportBuilder, WeatherForecastReportBuilder> customize)
  {
    var forecast = customize(CreateForecast());
    _reportedForecasts.Add(forecast);
    return await _driver.WeatherForecastApi.AttemptToReportForecast(forecast);
  }

  public async Task<RetrievedForecasts> RetrieveAllReportedForecasts()
  {
    return await _driver.WeatherForecastApi.GetReportedForecastsFrom(
      _userId1, 
      _tenantId1);
  }

  public WeatherForecastReportBuilder[] AllReportedForecasts()
  {
    return _reportedForecasts.ToArray();
  }

  private WeatherForecastReportBuilder CreateForecast()
  {
    return new WeatherForecastReportBuilder()
      .WithUserId(_userId1)
      .WithTenantId(_tenantId1);
  }

  public ReportForecastResponse LastReportedForecastResponse()
  {
    return _forecastCreationResponses.Last();
  }

  public async Task<RetrievedForecast> RetrieveLastReportedForecast()
  {
    var retrievedForecast = await _driver.WeatherForecastApi.GetReportedForecastBy(
      await LastReportedForecastResponse().GetId());
    return retrievedForecast;
  }

  public WeatherForecastReportBuilder LastReportedForecast()
  {
    return _reportedForecasts.Last();
  }
}

public class Disposables
{
  private readonly List<IDisposable> _disposables = new();
  private readonly List<IAsyncDisposable> _asyncDisposables = new();

  public void AddDisposable(IDisposable disposable)
  {
    _disposables.Add(disposable);
  }

  public async Task DisposeAsync()
  {
    foreach (var disposable in _disposables)
    {
      // in real-life scenario, wrapping the line below in a try-catch
      // would help ensure all disposables are disposed of.
      disposable.Dispose();
    }

    // in real-life scenario, wrapping the line below in a try-catch
    // would help ensure all disposables are disposed of.
    await Task.WhenAll(_asyncDisposables.Select(d => d.DisposeAsync().AsTask()));
  }

  public void AddAsyncDisposable(IAsyncDisposable asyncDisposable)
  {
    _asyncDisposables.Add(asyncDisposable);
  }
}

public class AppDriver : IAsyncDisposable
{
  private readonly WireMockServer _notificationRecipient;
  private readonly Disposables _disposables = new();
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
              ["NotificationsConfiguration:BaseUrl"] = _notificationRecipient.Urls.Single()
            });
          })
          .UseEnvironment("Development")
      );

    _disposables.AddDisposable(_notificationRecipient);
    _disposables.AddAsyncDisposable(_webApplicationFactory);
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
    await _disposables.DisposeAsync();
  }

  public NotificationsDriverExtension Notifications =>
    new(_notificationRecipient);

  public WeatherForecastApiDriverExtension WeatherForecastApi
    => new(_httpClient.Value(), _disposables);

  private void ForceStart()
  {
    _httpClient = new FlurlClient(_webApplicationFactory.CreateClient()).Just();
    _disposables.AddDisposable(_httpClient.Value());
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
  private readonly Disposables _disposables;

  public WeatherForecastApiDriverExtension(
    IFlurlClient httpClient, Disposables disposables)
  {
    _httpClient = httpClient;
    _disposables = disposables;
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
    _disposables.AddDisposable(httpResponse);
    return httpResponse;
  }

  public async Task<RetrievedForecast> GetReportedForecastBy(Guid id)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(id)
      .AllowAnyHttpStatus()
      .GetAsync();
    _disposables.AddDisposable(httpResponse);
    return new RetrievedForecast(httpResponse);
  }

  public async Task<RetrievedForecasts> GetReportedForecastsFrom(string userId, string tenantId)
  {
    var httpResponse = await _httpClient.Request("WeatherForecast")
      .AppendPathSegment(tenantId)
      .AppendPathSegment(userId)
      .AllowAnyHttpStatus()
      .GetAsync();
    _disposables.AddDisposable(httpResponse);
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