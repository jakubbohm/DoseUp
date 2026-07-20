using System.Collections.Concurrent;
using System.Reflection;
using DoseUp.Api.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// The one Platform model-builder convention promised by <see cref="ITypedId{TSelf}"/>:
/// typed ids persist as native <c>uuid</c> through <see cref="TypedIdConverter{TId}"/>.
/// Mechanism only — each module context passes its own assembly and Domain namespace, so
/// a context registers only its own module's ids and no cross-module type knowledge
/// enters a foreign context (module boundary discipline, ADR-0002; review 2026-07-20).
/// A null <c>namespacePrefix</c> deliberately widens to the whole assembly — a hatch for
/// a future non-module use; module contexts always pass their Domain namespace.
/// </summary>
public static class TypedIdModelConventions {
  private static readonly ConcurrentDictionary<Assembly, Type[]> _typedIdsByAssembly = new();

  public static void ApplyTypedIdConversions(ModelConfigurationBuilder configurationBuilder, Assembly assembly, string? namespacePrefix = null) {
    ArgumentNullException.ThrowIfNull(configurationBuilder);
    ArgumentNullException.ThrowIfNull(assembly);

    IEnumerable<Type> typedIdTypes = _typedIdsByAssembly.GetOrAdd(assembly, static scanned => [
      .. scanned.GetTypes().Where(static type => type.IsValueType && type.GetInterfaces()
        .Any(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ITypedId<>))),
    ]);

    if (namespacePrefix is not null) {
      typedIdTypes = typedIdTypes.Where(type =>
        type.Namespace == namespacePrefix
        || (type.Namespace?.StartsWith(namespacePrefix + ".", StringComparison.Ordinal) ?? false));
    }

    foreach (Type typedIdType in typedIdTypes)
      configurationBuilder.Properties(typedIdType).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(typedIdType));
  }
}
