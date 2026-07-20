
namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// The domain layer's result union (the domain half of #38): a rule-guarded domain
/// operation either happened or was refused by its rules — nothing else. Request- and
/// resource-shaped outcomes (not found, forbidden, …) are edge vocabulary and never the
/// domain's; the dependency points strictly outward-in — outer layers convert from this
/// type, this type knows nothing above the domain (#99, arch rule 21). Bugs still
/// throw; domain methods are sync by convention, so no Task-shaped variants exist at
/// this layer.
/// </summary>
public readonly union DomainResult(DomainResult.Success, DomainResult.RuleViolations) : IEquatable<DomainResult> {
  /// <summary>The operation happened; there is nothing to report.</summary>
  public readonly record struct Success;

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record RuleViolations : RuleViolationCarrier {
    public RuleViolations(IReadOnlyList<RuleViolation> violations) : base(violations) { }

    public RuleViolations(string code, string message)
      : this([new RuleViolation(code, message)]) { }
  }

  #region Object overrides

  public static bool operator ==(DomainResult left, DomainResult right) => left.Equals(right);

  public static bool operator !=(DomainResult left, DomainResult right) => !left.Equals(right);

  public bool Equals(DomainResult other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is DomainResult other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}

/// <summary>
/// The value-carrying form of <see cref="DomainResult"/> for rule-guarded operations
/// that produce something: <c>Success</c> carries the operation's value and is consumed
/// by pattern matching — only the failure side is convertible by outer layers (their
/// success shapes carry no payload here). Shipped ahead of its first consumer by
/// explicit decision (c002 design D8) so the pair is complete.
/// </summary>
public readonly union DomainResult<T>(DomainResult<T>.Success, DomainResult<T>.RuleViolations) : IEquatable<DomainResult<T>> {
  /// <summary>The operation happened and produced <paramref name="Value"/>.</summary>
  public readonly record struct Success(T Value);

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record RuleViolations : RuleViolationCarrier {
    public RuleViolations(IReadOnlyList<RuleViolation> violations) : base(violations) { }

    public RuleViolations(string code, string message)
      : this([new RuleViolation(code, message)]) { }
  }

  #region Object overrides

  public static bool operator ==(DomainResult<T> left, DomainResult<T> right) => left.Equals(right);

  public static bool operator !=(DomainResult<T> left, DomainResult<T> right) => !left.Equals(right);

  public bool Equals(DomainResult<T> other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is DomainResult<T> other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}
