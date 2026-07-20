using DoseUp.Api.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DoseUp.ArchitectureTests;

/// <summary>
/// Reflection-side discovery behind the persistence catalog rules (testing.md §5 rows 4, 10,
/// 17–19): every concrete <see cref="DbContext"/> subclass in the Api assembly, with each
/// model built offline through the context's design-time factory — the same path the EF
/// tooling uses; no database is touched (design.md D11).
/// </summary>
internal static class DbContextDiscovery {
  private const string ModulesPrefix = "DoseUp.Api.Modules.";

  /// <summary>Every concrete <see cref="DbContext"/> subclass in the Api assembly.</summary>
  public static IReadOnlyList<Type> AllContexts { get; } = [
    .. typeof(ApiResult).Assembly.GetTypes().Where(static type => !type.IsAbstract && typeof(DbContext).IsAssignableFrom(type)),
  ];

  /// <summary>The module-owned subset of <see cref="AllContexts"/>, paired with the owning module's name.</summary>
  public static IReadOnlyList<(string Module, Type ContextType)> ModuleContexts { get; } = [
    .. AllContexts
      .Select(static contextType => (Module: ModuleOf(contextType), ContextType: contextType))
      .Where(static pair => pair.Module is not null)
      .Select(static pair => (pair.Module!, pair.ContextType)),
  ];

  public static List<string> CollectOffendersAcrossAllModels(Func<DbContext, IEnumerable<string>> offendersOf) {
    ArgumentNullException.ThrowIfNull(offendersOf);

    List<string> offenders = [];

    foreach (Type contextType in AllContexts) {
      using DbContext context = CreateOffline(contextType);

      offenders.AddRange(offendersOf(context));
    }

    return offenders;
  }

  public static List<string> CollectOffendersAcrossModuleModels(Func<string, DbContext, IEnumerable<string>> offendersOf) {
    ArgumentNullException.ThrowIfNull(offendersOf);

    List<string> offenders = [];

    foreach ((string module, Type contextType) in ModuleContexts) {
      using DbContext context = CreateOffline(contextType);

      offenders.AddRange(offendersOf(module, context));
    }

    return offenders;
  }

  /// <summary>
  /// Instantiates the context through its design-time factory (every context ships one —
  /// ADR-0002 § Persistence is module property); <c>Model</c> is then built from
  /// configuration alone, no connection opened.
  /// </summary>
  private static DbContext CreateOffline(Type contextType) {
    Type factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
    Type factoryType = contextType.Assembly.GetTypes().SingleOrDefault(type => !type.IsAbstract && factoryInterface.IsAssignableFrom(type))
      ?? throw new InvalidOperationException($"{contextType.FullName} ships no IDesignTimeDbContextFactory — every context carries one (ADR-0002 § Persistence is module property).");
    object factory = Activator.CreateInstance(factoryType)!;

    return (DbContext)factoryInterface.GetMethod(nameof(IDesignTimeDbContextFactory<>.CreateDbContext))!.Invoke(factory, [Array.Empty<string>()])!;
  }

  private static string? ModuleOf(Type contextType) {
    if (contextType.Namespace is not { } contextNamespace || !contextNamespace.StartsWith(ModulesPrefix, StringComparison.Ordinal))
      return null;

    string remainder = contextNamespace[ModulesPrefix.Length..];
    int firstDot = remainder.IndexOf('.', StringComparison.Ordinal);

    return firstDot < 0 ? remainder : remainder[..firstDot];
  }
}
