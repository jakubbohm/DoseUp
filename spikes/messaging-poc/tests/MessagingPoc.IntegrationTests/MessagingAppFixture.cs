using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core.Interfaces;

namespace MessagingPoc.IntegrationTests;

/// <summary>
/// Session-shared harness (mirrors the production <c>AspireAppFixture</c>): starts the real AppHost
/// once — Postgres, the Azure Service Bus emulator (+ its mssql sidecar), and the api — and hands out
/// an HTTP client plus the live DB connection string. Everything here is production code path; the
/// only thing faked is "the cloud is a local container".
/// </summary>
public sealed class MessagingAppFixture : IAsyncInitializer, IAsyncDisposable {
  private DistributedApplication? _app;

  private DistributedApplication App => _app ?? throw new InvalidOperationException("The fixture has not been initialized.");

  public async Task InitializeAsync() {
    // The emulator + SQL Edge sidecar are heavy to start — be generous, more so in CI.
    TimeSpan timeout = Environment.GetEnvironmentVariable("CI") is null ? TimeSpan.FromMinutes(8) : TimeSpan.FromMinutes(15);

    IDistributedApplicationTestingBuilder appHost =
      await DistributedApplicationTestingBuilder.CreateAsync<Projects.MessagingPoc_AppHost>();

    appHost.Services.ConfigureHttpClientDefaults(static http => http.AddStandardResilienceHandler());

    _app = await appHost.BuildAsync().WaitAsync(timeout);
    await _app.StartAsync().WaitAsync(timeout);

    // api WaitFor(messaging) + WaitFor(messagingdb), so a healthy api implies the whole graph is up.
    await _app.ResourceNotifications.WaitForResourceHealthyAsync("api").WaitAsync(timeout);
  }

  public HttpClient CreateClient() => App.CreateHttpClient("api");

  public async Task<string> GetDatabaseConnectionStringAsync() =>
    await App.GetConnectionStringAsync("messagingdb")
    ?? throw new InvalidOperationException("The 'messagingdb' connection string is unavailable.");

  #region IAsyncDisposable

  public async ValueTask DisposeAsync() {
    if (_app is not null)
      await _app.DisposeAsync();
  }

  #endregion
}
