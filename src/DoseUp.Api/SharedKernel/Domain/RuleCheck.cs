
namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// The outcome of checking one domain rule (domain-rules.md §2). A single failed rule is a
/// one-element <see cref="Fail"/>; <see cref="RuleSet"/> aggregates multiple checks.
/// </summary>
public readonly union RuleCheck(RuleCheck.Pass, RuleCheck.Fail) : IEquatable<RuleCheck> {
  /// <summary>The rule holds; nothing is observable.</summary>
  public readonly record struct Pass;

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record Fail : RuleViolationCarrier {
    public Fail(IReadOnlyList<RuleViolation> violations) : base(violations) { }

    public Fail(string code, string message)
      : this([new RuleViolation(code, message)]) { }

    /// <summary>
    /// Lossless bridge to the domain channel — the aggregate's re-assert returns its
    /// failed check as <see cref="DomainResult.RuleViolations"/> (domain-rules.md §3).
    /// No edge bridge exists here: rule vocabulary knows the domain, never the edge —
    /// outer layers convert from this type (#99, arch rule 21).
    /// </summary>
    public DomainResult ToDomainResult() => new DomainResult.RuleViolations(Violations);
  }

  #region Object overrides

  public static bool operator ==(RuleCheck left, RuleCheck right) => left.Equals(right);

  public static bool operator !=(RuleCheck left, RuleCheck right) => !left.Equals(right);

  public bool Equals(RuleCheck other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is RuleCheck other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}