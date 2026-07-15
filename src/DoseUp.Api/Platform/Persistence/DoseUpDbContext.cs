using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// The single DbContext — the unit of work and the data-access API (no repositories,
/// PRE-7). Empty in c001: the initial migration proves the schema pipeline before any
/// table exists; modules add aggregates via <c>IEntityTypeConfiguration</c> from M1 on.
/// </summary>
public sealed class DoseUpDbContext(DbContextOptions<DoseUpDbContext> options) : DbContext(options);