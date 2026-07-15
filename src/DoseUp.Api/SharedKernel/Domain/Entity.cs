namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// Base for domain entities: equality is identity — concrete type + id (conventions
/// § Domain model base types). Entities are born with their identity (client-generated
/// Guid v7 in the aggregate factory), so there is no transient-id state.
/// </summary>
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>> where TId : struct, IEquatable<TId> {
  public TId Id { get; } = id;

  #region Object overrides

  public bool Equals(Entity<TId>? other) => other is not null && GetType() == other.GetType() && Id.Equals(other.Id);

  public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

  public override int GetHashCode() => HashCode.Combine(GetType(), Id);

  #endregion
}