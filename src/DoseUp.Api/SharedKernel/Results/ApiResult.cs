using DoseUp.Api.SharedKernel.Domain;

namespace DoseUp.Api.SharedKernel.Results;

/// <summary>
/// The closed union every expected non-happy-path outcome travels as, end-to-end
/// (domain-rules.md §1). Expected failures are values, never exceptions; exceptions are
/// reserved for bugs and infrastructure (class 8). The single Platform mapper converts
/// failure cases to ProblemDetails at the edge — nothing else crafts error responses.
/// </summary>
public readonly union ApiResult(
  ApiResult.Success,
  ApiResult.Validation,
  ApiResult.NotFound,
  ApiResult.RuleViolations,
  ApiResult.Conflict,
  ApiResult.Forbidden,
  ApiResult.Unexpected) : IEquatable<ApiResult> {
  /// <summary>The operation completed; there is nothing to report.</summary>
  public readonly record struct Success;

  /// <summary>
  /// Class 4 — contract validation: the request shape is wrong (missing field, bad
  /// range/format). Carries every field's errors at once, never only the first → 400.
  /// Guarded like the violation carriers: a 400 naming no field — or a field naming no
  /// message — is a bug, and the dictionary <em>and its arrays</em> are snapshotted at
  /// construction (the arrays are mutable, so a pair-level copy alone would alias them).
  /// </summary>
  public sealed record Validation {
    public Validation(IReadOnlyDictionary<string, string[]> errors) {
      ArgumentNullException.ThrowIfNull(errors);
      ArgumentOutOfRangeException.ThrowIfZero(errors.Count, nameof(errors));

      Dictionary<string, string[]> snapshot = [];
      foreach ((string field, string[] messages) in errors) {
        ArgumentNullException.ThrowIfNull(messages, nameof(errors));
        ArgumentOutOfRangeException.ThrowIfZero(messages.Length, nameof(errors));
        snapshot[field] = [.. messages];
      }

      Errors = snapshot;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
  }

  /// <summary>
  /// Class 5 — resource miss: the id is nonexistent or not the caller's — deliberately
  /// indistinguishable (anti-enumeration — ADR-0002 § Authorization) → 404.
  /// </summary>
  public readonly record struct NotFound;

  /// <summary>
  /// Class 6 — domain rule violations, aggregated by the handler's RuleSet or raised by
  /// the aggregate's self-check → 409 with the <c>violations</c> array.
  /// </summary>
  public sealed record RuleViolations : RuleViolationCarrier {
    public RuleViolations(IReadOnlyList<RuleViolation> violations) : base(violations) { }
  }

  /// <summary>
  /// Class 7 — concurrency clash (reserved until optimistic concurrency is needed)
  /// → 409 with a ProblemDetails <c>type</c> distinct from rule violations.
  /// </summary>
  public readonly record struct Conflict;

  /// <summary>
  /// Class 2 narrowed — "visible but not permitted"; dormant until FR-21-style permission
  /// levels (request-class 403s are owned by Platform policies, not handlers) → 403.
  /// </summary>
  public readonly record struct Forbidden;

  /// <summary>
  /// Class 8 marker — the mapped-500 case. Bugs and infrastructure failures throw; this
  /// case exists so the mapper's matrix is total → 500, never leaking internals.
  /// </summary>
  public readonly record struct Unexpected;

  /// <summary>
  /// Lossless entry from the domain channel — the handler's last step (<c>Success</c> →
  /// <c>Success</c>; violations unchanged, same order → 409 downstream). Conversions
  /// live on the edge union so the domain never references the edge (#99, arch rule 21).
  /// </summary>
  public static ApiResult From(DomainResult result) => result switch {
    DomainResult.Success => new Success(),
    DomainResult.RuleViolations violations => new RuleViolations(violations.Violations),
  };

  /// <summary>
  /// Lossless failure-side entry from the value-carrying domain form — its
  /// <c>Success</c> payload is consumed by pattern matching and never crosses (the edge
  /// <c>Success</c> carries nothing), which keeps this conversion total.
  /// </summary>
  public static ApiResult From<T>(DomainResult<T>.RuleViolations violations) {
    ArgumentNullException.ThrowIfNull(violations);

    return new RuleViolations(violations.Violations);
  }

  /// <summary>Lossless entry from a failed rule check (same violations, same order → 409).</summary>
  public static ApiResult From(RuleCheck.Fail fail) {
    ArgumentNullException.ThrowIfNull(fail);

    return new RuleViolations(fail.Violations);
  }

  #region Object overrides

  public static bool operator ==(ApiResult left, ApiResult right) => left.Equals(right);

  public static bool operator !=(ApiResult left, ApiResult right) => !left.Equals(right);

  public bool Equals(ApiResult other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is ApiResult other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}