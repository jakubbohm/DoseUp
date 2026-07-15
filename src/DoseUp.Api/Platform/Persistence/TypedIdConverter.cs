using DoseUp.Api.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// The one generic typed-id ⇄ uuid converter (conventions § Domain model base types):
/// any <see cref="ITypedId{TSelf}"/> persists as provider type <see cref="Guid"/> —
/// native Postgres <c>uuid</c>, never a string. Registered per id type by the Platform
/// model-builder convention. (The static-abstract call is wrapped in plain methods —
/// expression trees cannot reference static abstract interface members.)
/// </summary>
public sealed class TypedIdConverter<TId>() : ValueConverter<TId, Guid>(static id => ToProvider(id), static value => FromProvider(value)) where TId : struct, ITypedId<TId> {
  private static Guid ToProvider(TId id) => id.Value;

  private static TId FromProvider(Guid value) => TId.From(value);
}