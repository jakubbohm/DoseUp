using System.Net;
using Npgsql;
using Shouldly;

namespace DoseUp.IntegrationTests;

public sealed class HealthEndpointTests {
  [ClassDataSource<AspireAppFixture>(Shared = SharedType.PerTestSession)]
  public required AspireAppFixture Harness { get; init; }

  [Test]
  public async Task The_anonymous_health_probe_succeeds() {
    // api-shell R2 scenario 1 — and §9.4/§9.5/§9.6 mechanism proof: the session-shared
    // AppHost is reachable, migrations applied through the real path, PerTestSession held.
    using HttpClient client = Harness.CreateAnonymousClient();

    HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Test]
  public async Task The_anonymous_aliveness_probe_succeeds() {
    using HttpClient client = Harness.CreateAnonymousClient();

    HttpResponseMessage response = await client.GetAsync(new Uri("/alive", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Test]
  public async Task Probing_health_executes_no_database_command() {
    // api-shell R2 scenario 2: probe traffic (incl. bot-triggered wakes) never reaches the
    // database. Nothing in c001 opens an application connection, so after a probe burst the
    // database must show zero backends from the api — any probe-path DB touch would leave
    // a pooled connection visible in pg_stat_activity (§3e direct-DB exception, stated).
    using HttpClient client = Harness.CreateAnonymousClient();
    for (int i = 0; i < 3; i++)
      (await client.GetAsync(new Uri("/health", UriKind.Relative))).EnsureSuccessStatusCode();

    await using NpgsqlConnection connection = new(await Harness.GetDatabaseConnectionStringAsync());
    await connection.OpenAsync();
    await using NpgsqlCommand command = new(
      "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()",
      connection
    );
    long backends = (long)(await command.ExecuteScalarAsync() ?? -1L);

    backends.ShouldBe(0);
  }
}