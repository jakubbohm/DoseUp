namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// The one guarded shape every violations-carrying case derives from —
/// <c>RuleCheck.Fail</c>, the domain result's <c>RuleViolations</c>, and the outer
/// layers' violation cases: never null, never empty — a failure that names no violated
/// rule is a bug — and snapshotted at construction, so a caller mutating its list
/// afterwards cannot break the invariant. Order is preserved: violations read in the
/// order they were determined.
/// </summary>
public abstract record RuleViolationCarrier {
  protected RuleViolationCarrier(IReadOnlyList<RuleViolation> violations) {
    ArgumentNullException.ThrowIfNull(violations);
    ArgumentOutOfRangeException.ThrowIfZero(violations.Count, nameof(violations));
    Violations = [.. violations];
  }

  public IReadOnlyList<RuleViolation> Violations { get; }
}
