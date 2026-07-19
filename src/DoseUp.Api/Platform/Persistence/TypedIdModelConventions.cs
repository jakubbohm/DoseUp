using DoseUp.Api.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// The one Platform model-builder convention promised by <see cref="ITypedId{TSelf}"/>:
/// every typed id in the API assembly persists as native <c>uuid</c> through
/// <see cref="TypedIdConverter{TId}"/> — registered here once, so no per-aggregate
/// configuration exists to forget. Module contexts call this from their
/// <c>ConfigureConventions</c>.
/// </summary>
public static class TypedIdModelConventions {
  public static void ApplyTypedIdConversions(ModelConfigurationBuilder configurationBuilder) {
    ArgumentNullException.ThrowIfNull(configurationBuilder);

    IEnumerable<Type> typedIdTypes = typeof(ITypedId<>).Assembly.GetTypes()
      .Where(static type => type.IsValueType && type.GetInterfaces()
        .Any(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ITypedId<>)));

    foreach (Type typedIdType in typedIdTypes)
      configurationBuilder.Properties(typedIdType).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(typedIdType));
  }
}
