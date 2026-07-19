using DoseUp.Api.SharedKernel.Rules;

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
  /// </summary>
  public sealed record Validation(IReadOnlyDictionary<string, string[]> Errors);

  /// <summary>
  /// Class 5 — resource miss: the id is nonexistent or not the caller's — deliberately
  /// indistinguishable (anti-enumeration — ADR-0002 § Authorization) → 404.
  /// </summary>
  public readonly record struct NotFound;

  /// <summary>
  /// Class 6 — domain rule violations, aggregated by the handler's RuleSet or raised by
  /// the aggregate's self-check → 409 with the <c>violations</c> array.
  /// </summary>
  public sealed record RuleViolations(IReadOnlyList<RuleViolation> Violations);

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

  #region Object overrides

  public static bool operator ==(ApiResult left, ApiResult right) => left.Equals(right);

  public static bool operator !=(ApiResult left, ApiResult right) => !left.Equals(right);

  public bool Equals(ApiResult other) => Equals(Value, other.Value);

  public override bool Equals(object? obj) => obj is ApiResult other && Equals(other);

  public override int GetHashCode() => Value?.GetHashCode() ?? 0;

  #endregion
}