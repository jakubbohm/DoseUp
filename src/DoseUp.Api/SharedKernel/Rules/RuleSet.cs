namespace DoseUp.Api.SharedKernel.Rules;

/// <summary>
/// Handler-side composer of rule checks (domain-rules.md §5). Pure checks arrive as
/// already-evaluated <see cref="RuleCheck"/> values; async checks arrive as deferred
/// lambdas that never execute before <see cref="CheckAsync"/>. Violations aggregate
/// within a stage; a later stage (<see cref="Then(System.Func{System.Threading.Tasks.Task{RuleCheck}})"/>)
/// runs only when all previous stages passed. Async checks are awaited strictly
/// sequentially in registration order — never in parallel: the DbContext they close over
/// is not thread-safe, and with no repository layer the context is the query API.
/// </summary>
public sealed class RuleSet {
  private readonly List<List<Func<Task<RuleCheck>>>> _stages;

  private RuleSet() =>
    _stages = [
      [],
    ];

  /// <summary>Starts a set with the first stage's pure, already-evaluated checks.</summary>
  public static RuleSet Add(params RuleCheck[] checks) {
    ArgumentNullException.ThrowIfNull(checks);
    RuleSet set = new();
    foreach (RuleCheck check in checks)
      set.Add(check);

    return set;
  }

  /// <summary>Adds a pure, already-evaluated check to the current stage.</summary>
  public RuleSet Add(RuleCheck check) {
    _stages[^1].Add(() => Task.FromResult(check));
    return this;
  }

  /// <summary>Adds a deferred async check to the current stage.</summary>
  public RuleSet Add(Func<Task<RuleCheck>> check) {
    ArgumentNullException.ThrowIfNull(check);
    _stages[^1].Add(check);
    return this;
  }

  /// <summary>Starts a new stage with a pure check — it runs only if all previous stages passed.</summary>
  public RuleSet Then(RuleCheck check) {
    _stages.Add([]);
    return Add(check);
  }

  /// <summary>Starts a new stage with a deferred async check — it runs only if all previous stages passed.</summary>
  public RuleSet Then(Func<Task<RuleCheck>> check) {
    ArgumentNullException.ThrowIfNull(check);
    _stages.Add([]);
    return Add(check);
  }

  /// <summary>
  /// Evaluates the set: within a stage every check runs (violations aggregate in
  /// registration order); the first failing stage short-circuits the rest.
  /// </summary>
  public async Task<RuleCheck> CheckAsync() {
    foreach (List<Func<Task<RuleCheck>>> stage in _stages) {
      List<RuleViolation> violations = [];
      foreach (Func<Task<RuleCheck>> check in stage) {
        if (await check().ConfigureAwait(false) is RuleCheck.Fail fail)
          violations.AddRange(fail.Violations);
      }

      if (violations.Count > 0)
        return new RuleCheck.Fail(violations);
    }

    return new RuleCheck.Pass();
  }
}