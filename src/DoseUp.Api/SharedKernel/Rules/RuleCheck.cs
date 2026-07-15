using DoseUp.Api.SharedKernel.Results;

namespace DoseUp.Api.SharedKernel.Rules;

/// <summary>
/// The outcome of checking one domain rule (domain-rules.md §2). A single failed rule is a
/// one-element <see cref="Fail"/>; <see cref="RuleSet"/> aggregates multiple checks.
/// </summary>
public readonly union RuleCheck(RuleCheck.Pass, RuleCheck.Fail) : IEquatable<RuleCheck>
{
  public static bool operator ==(RuleCheck left, RuleCheck right) => left.Equals(right);

  public static bool operator !=(RuleCheck left, RuleCheck right) => !left.Equals(right);

  public bool Equals(RuleCheck other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is RuleCheck other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  /// <summary>The rule holds; nothing is observable.</summary>
  public readonly record struct Pass;

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record Fail
  {
    public Fail(IReadOnlyList<RuleViolation> violations)
    {
      ArgumentNullException.ThrowIfNull(violations);
      ArgumentOutOfRangeException.ThrowIfZero(violations.Count, nameof(violations));
      Violations = violations;
    }

    public Fail(string code, string message)
      : this([new RuleViolation(code, message)]) { }

    public IReadOnlyList<RuleViolation> Violations { get; }

    /// <summary>
    /// Lossless bridge to the edge channel: the same violations, same order, as
    /// <see cref="Result.RuleViolations"/> → 409.
    /// </summary>
    public Result ToResult() => new Result.RuleViolations(Violations);
  }
}
