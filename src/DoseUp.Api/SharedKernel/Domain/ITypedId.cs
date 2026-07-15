namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// Shape contract for per-aggregate typed ids — hand-written
/// <c>readonly record struct XxxId(Guid Value) : ITypedId&lt;XxxId&gt;</c> (conventions
/// § Domain model base types). The shape lets one generic value converter and one Platform
/// model-builder convention cover every id; ids are compile-time non-interchangeable
/// across aggregates, which is what makes account-scoped <c>Where</c> clauses (ring 2,
/// PRE-10) refuse to compile when ids are swapped.
/// </summary>
/// <remarks>
/// The v7 mint is each id type's own one-line <c>Create()</c>
/// (<c>public static XxxId Create() => new(Guid.CreateVersion7());</c>) — a static
/// virtual interface default is unreachable on the concrete type (C# exposes static
/// virtuals only through constrained type parameters), so the interface carries only
/// what the converter needs.
/// </remarks>
public interface ITypedId<TSelf> where TSelf : struct, ITypedId<TSelf> {
  Guid Value { get; }

  /// <summary>Rehydrates an id from its stored uuid (the converter's read side).</summary>
  static abstract TSelf From(Guid value);
}