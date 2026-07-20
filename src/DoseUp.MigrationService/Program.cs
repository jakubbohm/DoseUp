using DoseUp.Api.Modules.Membership.Infrastructure.Persistence;
using DoseUp.MigrationService;
using DoseUp.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

string doseupDbConnectionString = builder.Configuration.GetConnectionString("doseupdb")
  ?? throw new InvalidOperationException("Connection string 'doseupdb' is missing — run via the AppHost (aspire start) or the test harness.");

// Registers the single module context directly — options come from the module's one
// authoring point, so the runner migrates exactly the model the app runs (snake_case,
// module-owned history table). Generalizes to iterate every module context when a
// second module exists (d18 — issue #39).
builder.Services.AddDbContext<MembershipDbContext>(options => MembershipDbContextOptions.Apply(options, doseupDbConnectionString));

builder.EnrichNpgsqlDbContext<MembershipDbContext>(static settings => settings.DisableHealthChecks = true);

builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
