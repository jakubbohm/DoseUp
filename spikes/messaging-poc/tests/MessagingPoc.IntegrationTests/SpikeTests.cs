using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;

namespace MessagingPoc.IntegrationTests;

/// <summary>
/// Evidence for spikes #50 (Wolverine per-module outbox on the .NET 11 preview stack) and #51
/// (Wolverine × ASB emulator inside the Aspire harness). Each test states the exact precondition
/// it proves.
/// </summary>
public sealed class SpikeTests {
  [ClassDataSource<MessagingAppFixture>(Shared = SharedType.PerTestSession)]
  public required MessagingAppFixture Fixture { get; init; }

  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

  /// <summary>#51 + #50 end-to-end: a fact published from Scheduling's outbox travels through the real
  /// ASB emulator and is consumed by Adherence — no cloud connection involved.</summary>
  [Test]
  public async Task Recording_a_dose_delivers_the_event_across_modules_through_the_service_bus_emulator() {
    using HttpClient client = Fixture.CreateClient();
    Guid profileId = Guid.NewGuid();

    HttpResponseMessage recorded = await client.PostAsJsonAsync("/scheduling/record-dose", new { profileId });
    recorded.StatusCode.ShouldBe(HttpStatusCode.OK);
    Guid doseRecordId = (await recorded.Content.ReadFromJsonAsync<RecordDoseResponse>(Json))!.DoseRecordId;

    IReadOnlyList<AdherenceEntryDto> entries = await PollForEntriesAsync(client, profileId, atLeast: 1);

    entries.Count.ShouldBe(1);
    entries[0].DoseRecordId.ShouldBe(doseRecordId);
    entries[0].ProfileId.ShouldBe(profileId);
  }

  /// <summary>#50's hard precondition: each module keeps its outbox/inbox state in its OWN Postgres
  /// schema. Proven structurally by the live table topology after a round-trip.</summary>
  [Test]
  public async Task Each_module_keeps_its_wolverine_store_in_its_own_schema() {
    using HttpClient client = Fixture.CreateClient();
    Guid profileId = Guid.NewGuid();

    await client.PostAsJsonAsync("/scheduling/record-dose", new { profileId });
    await PollForEntriesAsync(client, profileId, atLeast: 1);

    ILookup<string, string> tablesBySchema = await ReadTableTopologyAsync();

    // Scheduling owns its aggregate AND its Wolverine envelope tables, both in the `scheduling` schema.
    tablesBySchema["scheduling"].ShouldContain("dose_records");
    tablesBySchema["scheduling"].ShouldContain(t => t.StartsWith("wolverine_", StringComparison.Ordinal),
      "Scheduling's outbox envelope tables must live in the scheduling schema");

    // Adherence owns its read model AND its inbox/idempotency tables, both in the `adherence` schema.
    tablesBySchema["adherence"].ShouldContain("adherence_entries");
    tablesBySchema["adherence"].ShouldContain(t => t.StartsWith("wolverine_", StringComparison.Ordinal),
      "Adherence's inbox tables must live in the adherence schema");

    // The single Main control store is isolated in its own `wolverine` schema.
    tablesBySchema["wolverine"].ShouldContain(t => t.StartsWith("wolverine_", StringComparison.Ordinal),
      "The Main control store must live in the wolverine schema");
  }

  /// <summary>#50: consumers are idempotent — delivering the same fact twice yields one effect
  /// (envelope-level inbox dedup + business-key re-query, both in the adherence schema).</summary>
  [Test]
  public async Task Delivering_the_same_dose_twice_produces_a_single_adherence_entry() {
    using HttpClient client = Fixture.CreateClient();
    Guid profileId = Guid.NewGuid();
    Guid doseRecordId = Guid.NewGuid();
    var message = new { doseRecordId, profileId };

    await client.PostAsJsonAsync("/test/publish-dose-recorded", message);
    await client.PostAsJsonAsync("/test/publish-dose-recorded", message);

    await PollForEntriesAsync(client, profileId, atLeast: 1);
    // Give any duplicate a chance to (wrongly) land, then assert exactly one.
    await Task.Delay(TimeSpan.FromSeconds(3));

    IReadOnlyList<AdherenceEntryDto> entries = await GetEntriesAsync(client, profileId);
    entries.Count.ShouldBe(1);
  }

  /// <summary>#50: the outbox envelope joins the aggregate's transaction. When the unit of work fails,
  /// neither the aggregate NOR the envelope is committed — so nothing is ever relayed.</summary>
  [Test]
  public async Task A_failed_unit_of_work_rolls_back_the_aggregate_and_the_outbox_together() {
    using HttpClient client = Fixture.CreateClient();
    Guid profileId = Guid.NewGuid();

    HttpResponseMessage failed = await client.PostAsJsonAsync("/scheduling/record-dose-then-fail", new { profileId });
    failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    Guid doseRecordId = JsonDocument.Parse(await failed.Content.ReadAsStringAsync())
      .RootElement.GetProperty("doseRecordId").GetGuid();

    // The aggregate rolled back.
    HttpResponseMessage lookup = await client.GetAsync($"/scheduling/dose-records/{doseRecordId}");
    lookup.StatusCode.ShouldBe(HttpStatusCode.NotFound);

    // And since the envelope shared that transaction, no event was ever relayed to Adherence.
    await Task.Delay(TimeSpan.FromSeconds(8));
    IReadOnlyList<AdherenceEntryDto> entries = await GetEntriesAsync(client, profileId);
    entries.ShouldBeEmpty();
  }

  // ── helpers ──

  private static async Task<IReadOnlyList<AdherenceEntryDto>> GetEntriesAsync(HttpClient client, Guid profileId) =>
    await client.GetFromJsonAsync<List<AdherenceEntryDto>>($"/adherence/entries?profileId={profileId}", Json) ?? [];

  private static async Task<IReadOnlyList<AdherenceEntryDto>> PollForEntriesAsync(HttpClient client, Guid profileId, int atLeast) {
    for (int attempt = 0; attempt < 60; attempt++) {
      IReadOnlyList<AdherenceEntryDto> entries = await GetEntriesAsync(client, profileId);
      if (entries.Count >= atLeast)
        return entries;
      await Task.Delay(TimeSpan.FromSeconds(1));
    }
    throw new TimeoutException($"Expected at least {atLeast} adherence entr(y/ies) for {profileId} within 60s.");
  }

  private async Task<ILookup<string, string>> ReadTableTopologyAsync() {
    await using NpgsqlConnection conn = new(await Fixture.GetDatabaseConnectionStringAsync());
    await conn.OpenAsync();
    await using NpgsqlCommand cmd = conn.CreateCommand();
    cmd.CommandText = """
      SELECT table_schema, table_name
      FROM information_schema.tables
      WHERE table_schema IN ('wolverine','scheduling','adherence');
      """;
    List<(string Schema, string Table)> rows = [];
    await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
      rows.Add((reader.GetString(0), reader.GetString(1)));
    return rows.ToLookup(r => r.Schema, r => r.Table);
  }

  private sealed record RecordDoseResponse(Guid DoseRecordId);
  private sealed record AdherenceEntryDto(Guid Id, Guid DoseRecordId, Guid ProfileId, DateTimeOffset ObservedAt);
}
