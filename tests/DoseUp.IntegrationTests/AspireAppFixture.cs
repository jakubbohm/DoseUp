using System.Net.Http.Headers;
using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TUnit.Core.Interfaces;

namespace DoseUp.IntegrationTests;

/// <summary>
/// The session-shared harness (testing.md §3a): starts the real AppHost once — postgres,
/// the migration runner (the real schema path, §3a/§9.5), the api — and hands out HTTP
/// clients per caller class (§3c). The test JWT authority is injected into the api
/// resource before start; the bearer middleware, claim mapping, and 401 semantics stay
/// production code — the only fakes are Entra's signature and nothing else.
/// </summary>
public sealed class AspireAppFixture : IAsyncInitializer, IAsyncDisposable {
  private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);
  private readonly byte[] _untrustedKey = RandomNumberGenerator.GetBytes(32);
  private DistributedApplication? _app;

  public const string ISSUER = "https://test-authority.doseup.local";
  public const string AUDIENCE = "doseup-api";

  private DistributedApplication App =>
    _app ?? throw new InvalidOperationException("The fixture has not been initialized.");

  public async Task InitializeAsync() {
    // CI-stretched timeouts (design.md D9 — documented Aspire-in-CI hang mitigation).
    TimeSpan timeout = Environment.GetEnvironmentVariable("CI") is null
      ? TimeSpan.FromMinutes(2)
      : TimeSpan.FromMinutes(5);

    IDistributedApplicationTestingBuilder appHost =
      await DistributedApplicationTestingBuilder.CreateAsync<Projects.DoseUp_AppHost>();

    // §3c/§9.4: the second accepted authority rides configuration, injected into the api
    // resource before the app model builds.
    appHost
      .CreateResourceBuilder<ProjectResource>("api")
      .WithEnvironment("Auth__TestAuthority__Issuer", ISSUER)
      .WithEnvironment("Auth__TestAuthority__Audience", AUDIENCE)
      .WithEnvironment("Auth__TestAuthority__SigningKey", Convert.ToBase64String(_signingKey));

    appHost.Services.ConfigureHttpClientDefaults(static http =>
      http.AddStandardResilienceHandler()
    );

    _app = await appHost.BuildAsync().WaitAsync(timeout);
    await _app.StartAsync().WaitAsync(timeout);

    await _app
      .ResourceNotifications.WaitForResourceAsync("migration", KnownResourceStates.Finished)
      .WaitAsync(timeout);
    await _app.ResourceNotifications.WaitForResourceHealthyAsync("api").WaitAsync(timeout);
  }

  /// <summary>Caller class: anonymous — no token at all.</summary>
  public HttpClient CreateAnonymousClient() => App.CreateHttpClient("api");

  /// <summary>Caller class: authenticated — a valid token from the trusted test authority.</summary>
  public HttpClient CreateAuthenticatedClient(Guid oid) {
    HttpClient client = App.CreateHttpClient("api");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      MintToken(oid, _signingKey)
    );
    return client;
  }

  /// <summary>Caller class: untrusted key — a well-formed token no configured authority signed.</summary>
  public HttpClient CreateUntrustedKeyClient(Guid oid) {
    HttpClient client = App.CreateHttpClient("api");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      MintToken(oid, _untrustedKey)
    );
    return client;
  }

  public async Task<string> GetDatabaseConnectionStringAsync() =>
    await App.GetConnectionStringAsync("doseupdb")
    ?? throw new InvalidOperationException("The 'doseupdb' connection string is unavailable.");

  private static string MintToken(Guid oid, byte[] key) =>
    // No explicit Expires/IssuedAt — the handler stamps defaults itself, keeping the
    // banned wall-clock APIs out of test code (testing.md §6.4).
    new JsonWebTokenHandler().CreateToken(
      new SecurityTokenDescriptor {
        Issuer = ISSUER,
        Audience = AUDIENCE,
        Claims = new Dictionary<string, object> { ["oid"] = oid.ToString() },
        SigningCredentials = new SigningCredentials(
          new SymmetricSecurityKey(key),
          SecurityAlgorithms.HmacSha256
        ),
      }
    );

  public async ValueTask DisposeAsync() {
    if (_app is not null)
      await _app.DisposeAsync();
  }
}