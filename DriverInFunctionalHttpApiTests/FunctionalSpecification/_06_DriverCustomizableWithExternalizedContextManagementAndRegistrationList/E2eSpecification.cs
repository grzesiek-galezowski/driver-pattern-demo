using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DriverPatternDemo;
using FluentAssertions;
using Flurl.Http;
using Functional.Maybe;
using Functional.Maybe.Just;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using Newtonsoft.Json;
using TddXt.AnyRoot.Numbers;
using TddXt.AnyRoot.Strings;
using TddXt.AnyRoot.Time;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using static TddXt.AnyRoot.Root;

namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagementAndRegistrationList
{
  public class E2ESpecification
  {
    private static WeatherForecastReportBuilder ForecastReport() => new();

    [Fact]
    public async Task ShouldAllowRetrievingReportedForecast()
    {
      //GIVEN
      await using var driver = new AppDriver();
      await driver.StartAsync();
      var weatherForecast = ForecastReport();

      var reportForecastResponse = await driver.WeatherForecastApi.Report(weatherForecast);

      //WHEN
      var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecastBy(
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
      var user1Forecast1 = ForecastReport() with {UserId = userId1, TenantId = tenantId1};
      var user1Forecast2 = ForecastReport() with {UserId = userId1, TenantId = tenantId1};
      var user2Forecast = ForecastReport() with {UserId = userId2, TenantId = tenantId2};

      await driver.WeatherForecastApi.Report(user1Forecast1);
      await driver.WeatherForecastApi.Report(user1Forecast2);
      await driver.WeatherForecastApi.Report(user2Forecast);

      //WHEN
      var retrievedForecast = await driver.WeatherForecastApi.GetReportedForecastsFrom(userId1, tenantId1);

      //THEN
      await retrievedForecast.ShouldConsistOf(user1Forecast1, user1Forecast2);
    }

    [Fact]
    public async Task ShouldRejectForecastReportAsBadRequestWhenTemperatureIsLessThanMinus100()
    {
      //GIVEN
      await using var driver = new AppDriver();
      await driver.StartAsync();

      //WHEN
      var reportForecastResponse = await driver.WeatherForecastApi
        .AttemptToReportForecast(ForecastReport() with {TemperatureC = -101});

      //THEN
      reportForecastResponse.ShouldBeRejectedAsBadRequest();
      driver.Notifications.ShouldNotIncludeAnything();
    }
  }

  public class AppDriver : IAsyncDisposable
  {
    private Maybe<IHost> _host;
    private readonly WireMockServer _notificationRecipient;
    private readonly Disposables _disposables = new();

    private FlurlClient HttpClient => new(_host.Value.GetTestClient());

    public AppDriver()
    {
      _notificationRecipient = WireMockServer.Start();
      _disposables.Add(_notificationRecipient);
    }

    public async Task StartAsync()
    {
      _notificationRecipient.Given(
        Request.Create()
          .WithPath("/notifications")
          .UsingPost()).RespondWith(
        Response.Create()
          .WithStatusCode(HttpStatusCode.OK));

      _host = Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webBuilder =>
        {
          webBuilder
            .UseTestServer()
            .ConfigureAppConfiguration(appConfig =>
            {
              appConfig.AddInMemoryCollection(new Dictionary<string, string>
              {
                ["NotificationsConfiguration:BaseUrl"] = _notificationRecipient.Urls.Single()
              });
            })
            .UseEnvironment("Development")
            .UseStartup<Startup>();
        }).Build().Just();
      await _host.Value.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
      await _host.DoAsync(async host =>
      {
        await host.StopAsync();
        host.Dispose();
      });
      _disposables.Dispose();
    }

    public NotificationsDriverExtension Notifications =>
      new(_notificationRecipient);

    public WeatherForecastApiDriverExtension WeatherForecastApi
      => new(HttpClient, _disposables);
  }

  public class Disposables
  {
    private readonly List<IDisposable> _disposables = new();

    public void Add(IDisposable disposable)
    {
      _disposables.Add(disposable);
    }

    public void Dispose()
    {
      foreach (var disposable in _disposables)
      {
        // in real-life scenario, wrapping the line below in a try-catch
        // would help ensure all disposables are disposed of.
        disposable.Dispose();
      }
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

    public WeatherForecastApiDriverExtension(IFlurlClient httpClient, Disposables disposables)
    {
      _httpClient = httpClient;
      _disposables = disposables;
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
      
      _disposables.Add(httpResponse);
      
      return httpResponse;
    }

    public async Task<RetrievedForecast> GetReportedForecastBy(Guid id)
    {
      var httpResponse = await _httpClient.Request("WeatherForecast")
        .AppendPathSegment(id)
        .AllowAnyHttpStatus()
        .GetAsync();
      
      _disposables.Add(httpResponse);

      return new RetrievedForecast(httpResponse);
    }

    public async Task<RetrievedForecasts> GetReportedForecastsFrom(string userId, string tenantId)
    {
      var httpResponse = await _httpClient.Request("WeatherForecast")
        .AppendPathSegment(tenantId)
        .AppendPathSegment(userId)
        .AllowAnyHttpStatus()
        .GetAsync();

      _disposables.Add(httpResponse);

      var reportedForecasts = new RetrievedForecasts(httpResponse);
      reportedForecasts.ShouldIndicateSuccess();
      return reportedForecasts;
    }
  }

  public class RetrievedForecasts
  {
    private readonly IFlurlResponse _flurlResponse;

    public RetrievedForecasts(IFlurlResponse flurlResponse)
    {
      _flurlResponse = flurlResponse;
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

  public class ReportForecastResponse
  {
    private readonly IFlurlResponse _httpResponse;

    public ReportForecastResponse(IFlurlResponse httpResponse)
    {
      _httpResponse = httpResponse;
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

  public class RetrievedForecast
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
  }

  public record WeatherForecastReportBuilder
  {
    public string TenantId { private get; init; } = Any.String();
    public string UserId { private get; init; } = Any.String();
    public DateTime Time { private get; init; } = Any.DateTime();
    public int TemperatureC { private get; init; } = Any.Integer();
    public string Summary { private get; init; } = Any.String();

    public WeatherForecastDto Build()
    {
      return new(
        TenantId, 
        UserId, 
        Time,
        TemperatureC,
        Summary);
    }
  }
}