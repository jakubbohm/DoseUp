using DoseUp.Api.SharedKernel.Rules;

namespace DoseUp.Api.SharedKernel.Results;

/// <summary>
/// The domain layer's result union (the domain half of #38): a rule-guarded domain
/// operation either happened or was refused by its rules — nothing else. The edge
/// taxonomy (<c>NotFound</c>, <c>Forbidden</c>, …) is <see cref="ApiResult"/>'s
/// vocabulary and never the domain's (arch-tested — Domain namespaces must not
/// reference <see cref="ApiResult"/>). Bugs still throw; domain methods are sync by
/// convention, so no Task-shaped variants exist at this layer.
/// </summary>
public readonly union DomainResult(DomainResult.Success, DomainResult.RuleViolations) : IEquatable<DomainResult> {
  /// <summary>The operation happened; there is nothing to report.</summary>
  public readonly record struct Success;

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record RuleViolations {
    public RuleViolations(IReadOnlyList<RuleViolation> violations) {
      ArgumentNullException.ThrowIfNull(violations);
      ArgumentOutOfRangeException.ThrowIfZero(violations.Count, nameof(violations));
      Violations = violations;
    }

    public RuleViolations(string code, string message)
      : this([new RuleViolation(code, message)]) { }

    public IReadOnlyList<RuleViolation> Violations { get; }
  }

  /// <summary>
  /// Lossless map to the edge channel — the handler's last step (<c>Success</c> →
  /// <c>ApiResult.Success</c>; violations unchanged, same order → 409 downstream).
  /// </summary>
  public ApiResult ToApiResult() => this switch {
    Success => new ApiResult.Success(),
    RuleViolations violations => new ApiResult.RuleViolations(violations.Violations),
  };

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
/// by pattern matching — only the failure side converts to the edge (the edge union's
/// <c>Success</c> carries no payload), so the conversion lives on
/// <see cref="RuleViolations"/> where it is total. Shipped ahead of its first consumer
/// by explicit decision (c002 design D8) so the pair is complete.
/// </summary>
public readonly union DomainResult<T>(DomainResult<T>.Success, DomainResult<T>.RuleViolations) : IEquatable<DomainResult<T>> {
  /// <summary>The operation happened and produced <paramref name="Value"/>.</summary>
  public readonly record struct Success(T Value);

  /// <summary>One or more violations, in the order they were determined.</summary>
  public sealed record RuleViolations {
    public RuleViolations(IReadOnlyList<RuleViolation> violations) {
      ArgumentNullException.ThrowIfNull(violations);
      ArgumentOutOfRangeException.ThrowIfZero(violations.Count, nameof(violations));
      Violations = violations;
    }

    public RuleViolations(string code, string message)
      : this([new RuleViolation(code, message)]) { }

    public IReadOnlyList<RuleViolation> Violations { get; }

    /// <summary>Lossless failure-side map to the edge channel (violations unchanged, same order).</summary>
    public ApiResult ToApiResult() => new ApiResult.RuleViolations(Violations);
  }

  #region Object overrides

  public static bool operator ==(DomainResult<T> left, DomainResult<T> right) => left.Equals(right);

  public static bool operator !=(DomainResult<T> left, DomainResult<T> right) => !left.Equals(right);

  public bool Equals(DomainResult<T> other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is DomainResult<T> other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}
