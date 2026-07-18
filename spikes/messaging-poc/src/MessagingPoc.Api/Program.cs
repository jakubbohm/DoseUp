using JasperFx;
using MessagingPoc.Api.Modules.Adherence;
using MessagingPoc.Api.Modules.Scheduling;
using MessagingPoc.Api.Modules.Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Aspire injects these (WithReference in the AppHost): Postgres db + the ASB emulator/namespace.
string db = builder.Configuration.GetConnectionString("messagingdb")
  ?? throw new InvalidOperationException("Connection string 'messagingdb' is missing — run via the AppHost.");
string serviceBus = builder.Configuration.GetConnectionString("messaging")
  ?? throw new InvalidOperationException("Connection string 'messaging' is missing — run via the AppHost.");

// ── Per-module DbContexts, each wired for Wolverine's EF transactional middleware ──
// The 3rd arg is each context's Wolverine envelope schema — matches its Ancillary store below.
builder.Services.AddDbContextWithWolverineIntegration<SchedulingDbContext>((_, o) => o.UseNpgsql(db), "scheduling");
builder.Services.AddDbContextWithWolverineIntegration<AdherenceDbContext>((_, o) => o.UseNpgsql(db), "adherence");

// ── Wolverine: the heart of spike #50 ──
builder.Host.UseWolverine(opts => {
  // One Main control store (node/leader-election/durability-agent tables) in its own `wolverine`
  // schema, plus ONE ANCILLARY store PER MODULE, each in the module's own schema, each enrolled to
  // that module's DbContext so its outbox/inbox envelopes join the module's transaction.
  opts.PersistMessagesWithPostgresql(db, "wolverine", MessageStoreRole.Main);
  opts.PersistMessagesWithPostgresql(db, "scheduling", MessageStoreRole.Ancillary).Enroll<SchedulingDbContext>();
  opts.PersistMessagesWithPostgresql(db, "adherence", MessageStoreRole.Ancillary).Enroll<AdherenceDbContext>();
  opts.UseEntityFrameworkCoreTransactions();
  // SPIKE FINDING: UseEntityFrameworkCoreTransactions() enables the capability but does NOT, on its
  // own, wrap the handlers — without this policy the handlers add entities that are never saved and
  // events publish OUTSIDE the outbox. AutoApplyTransactions() is what makes each handler run inside
  // its module DbContext's transaction and drains cascaded messages through that module's outbox.
  opts.Policies.AutoApplyTransactions();

  // Azure Service Bus, strictly QUEUE-ONLY (Basic-tier compatible): explicit listener + sender,
  // no topics/subscriptions/sessions, and no per-node system queues (a data-plane-only identity
  // can't create those in prod). AutoProvision is OFF — the `dose-events` queue is declared in the
  // AppHost (seeded into the emulator) and, in prod, by hand-authored Bicep.
  opts.UseAzureServiceBus(serviceBus).SystemQueuesAreEnabled(false);
  opts.ListenToAzureServiceBusQueue("dose-events");
  opts.PublishMessage<DoseRecorded>().ToAzureServiceBusQueue("dose-events");

  // Dev/test only: build the message-store tables at startup (testing.md §3d — AutoProvision is
  // dev/test-only; prod provisions via the CD migration bundle under a data-plane-only identity).
  opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
});

WebApplication app = builder.Build();

// Dev/test-only provisioning of the two AGGREGATE tables (Wolverine owns the envelope tables above).
// Per-context CreateTables avoids EnsureCreated's "database already exists → skip" multi-context trap;
// Npgsql emits CREATE SCHEMA IF NOT EXISTS, so it composes with Wolverine's own schema creation.
await InitializeAggregateSchemaAsync(app.Services);

app.MapGet("/", () => "MessagingPoc.Api — spikes #50/#51");

// Scheduling slice: records a dose and, in the SAME transaction, enqueues DoseRecorded via the outbox.
app.MapPost("/scheduling/record-dose", async (RecordDoseRequest request, IMessageBus bus) => {
  Guid doseRecordId = Guid.NewGuid();
  await bus.InvokeAsync(new RecordDose(doseRecordId, request.ProfileId));
  return Results.Ok(new RecordDoseResponse(doseRecordId));
});

// Atomicity probe: same slice, but the handler throws after enqueue. Expect 500, and afterwards
// neither the dose record nor any adherence entry exists — the envelope rolled back with the aggregate.
app.MapPost("/scheduling/record-dose-then-fail", async (RecordDoseRequest request, IMessageBus bus) => {
  Guid doseRecordId = Guid.NewGuid();
  try {
    await bus.InvokeAsync(new RecordDoseThenFail(doseRecordId, request.ProfileId));
    return Results.Ok(new RecordDoseResponse(doseRecordId)); // unreachable — the handler always throws
  }
  catch (InvalidOperationException) {
    return Results.Json(new { doseRecordId, rolledBack = true }, statusCode: StatusCodes.Status500InternalServerError);
  }
});

// Lookup used by the atomicity test to assert the aggregate was NOT persisted.
app.MapGet("/scheduling/dose-records/{id:guid}", async (Guid id, SchedulingDbContext scheduling) => {
  DoseRecord? record = await scheduling.DoseRecords.FirstOrDefaultAsync(r => r.Id == id);
  return record is null ? Results.NotFound() : Results.Ok(record);
});

// Adherence read side: the test polls this until the round-trip lands.
app.MapGet("/adherence/entries", async (Guid profileId, AdherenceDbContext adherence) => {
  List<AdherenceEntry> entries = await adherence.AdherenceEntries
    .Where(e => e.ProfileId == profileId)
    .ToListAsync();
  return Results.Ok(entries);
});

// Consumer-idempotency test hook: publishes DoseRecorded straight to the ASB queue (bypassing the
// outbox) so the test can deliver the SAME business id twice and prove one-effect.
app.MapPost("/test/publish-dose-recorded", async (DoseRecorded message, IMessageBus bus) => {
  await bus.PublishAsync(message);
  return Results.Accepted();
});

// Diagnostics: list Wolverine's tables grouped by schema — makes the per-module topology visible
// to a human running the app (and to the topology test).
app.MapGet("/diag/topology", async (SchedulingDbContext scheduling) => {
  await using NpgsqlConnection conn = (NpgsqlConnection)scheduling.Database.GetDbConnection();
  await conn.OpenAsync();
  await using NpgsqlCommand cmd = conn.CreateCommand();
  cmd.CommandText = """
    SELECT table_schema, table_name
    FROM information_schema.tables
    WHERE table_schema IN ('wolverine','scheduling','adherence')
    ORDER BY table_schema, table_name;
    """;
  Dictionary<string, List<string>> bySchema = new();
  await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
  while (await reader.ReadAsync()) {
    string schema = reader.GetString(0);
    if (!bySchema.TryGetValue(schema, out List<string>? tables))
      bySchema[schema] = tables = [];
    tables.Add(reader.GetString(1));
  }
  return Results.Ok(bySchema);
});

app.Run();

static async Task InitializeAggregateSchemaAsync(IServiceProvider services) {
  using IServiceScope scope = services.CreateScope();
  // Scheduling first ensures the database exists; Adherence then just adds its tables.
  await CreateTablesForAsync(scope.ServiceProvider.GetRequiredService<SchedulingDbContext>());
  await CreateTablesForAsync(scope.ServiceProvider.GetRequiredService<AdherenceDbContext>());

  static async Task CreateTablesForAsync(DbContext context) {
    IRelationalDatabaseCreator creator = context.GetService<IRelationalDatabaseCreator>();

    // Runs before app.Run() (so it precedes Wolverine's message-store provisioning), but Postgres
    // can still be dropping connections just after WaitFor reports healthy — retry transient errors.
    for (int attempt = 1; ; attempt++) {
      try {
        if (!await creator.ExistsAsync())
          await creator.CreateAsync();
        break;
      }
      catch (NpgsqlException) when (attempt < 20) {
        await Task.Delay(TimeSpan.FromSeconds(2));
      }
    }

    try {
      await creator.CreateTablesAsync();
    }
    catch (PostgresException ex) when (ex.SqlState is "42P07" or "42P06") {
      // 42P07 duplicate_table / 42P06 duplicate_schema — tables already present from a prior run.
    }
  }
}

public sealed record RecordDoseRequest(Guid ProfileId);
public sealed record RecordDoseResponse(Guid DoseRecordId);
