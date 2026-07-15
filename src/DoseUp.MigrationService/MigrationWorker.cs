using DoseUp.Api.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DoseUp.MigrationService;

/// <summary>
/// Applies pending EF migrations under the execution strategy, then stops the host —
/// Aspire's documented migration-runner pattern (design.md D8). The AppHost gates the api
/// on this resource's completion, so local dev and the test harness share the identical
/// schema path (testing.md §3a; never <c>EnsureCreated</c>).
/// </summary>
public sealed class MigrationWorker(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime) : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    using (IServiceScope scope = serviceProvider.CreateScope()) {
      DoseUpDbContext context = scope.ServiceProvider.GetRequiredService<DoseUpDbContext>();
      IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
      await strategy.ExecuteAsync(() => context.Database.MigrateAsync(stoppingToken)).ConfigureAwait(false);
    }

    lifetime.StopApplication();
  }
}