using DoseUp.Api.Platform.Persistence;
using DoseUp.MigrationService;
using DoseUp.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

string doseupDbConnectionString =
  builder.Configuration.GetConnectionString("doseupdb")
  ?? throw new InvalidOperationException(
    "Connection string 'doseupdb' is missing — run via the AppHost (aspire start) or the test harness."
  );

builder.Services.AddDbContext<DoseUpDbContext>(options =>
  options.UseNpgsql(doseupDbConnectionString)
);

builder.EnrichNpgsqlDbContext<DoseUpDbContext>(static settings =>
  settings.DisableHealthChecks = true
);

builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();