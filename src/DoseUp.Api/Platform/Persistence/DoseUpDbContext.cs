using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// Bootstrap placeholder — exists only to keep the schema pipeline proven (migration
/// runner, Aspire graph, harness path) while no module exists. Persistence is module
/// property (ADR-0002 § Persistence is module property): each module owns its DbContext,
/// schema, and migrations. This context maps nothing and is removed when the first
/// module context lands (roadmap M1).
/// </summary>
public sealed class DoseUpDbContext(DbContextOptions<DoseUpDbContext> options) : DbContext(options);