using DoseUp.Api.SharedKernel.Rules;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Rules;

public sealed class RuleSetTests
{
  private static RuleCheck Fail(string code) => new RuleCheck.Fail(code, "Static rule text.");

  private static RuleCheck Pass() => new RuleCheck.Pass();

  [Test]
  public async Task Two_failures_in_one_stage_both_surface_in_registration_order()
  {
    RuleCheck outcome = await RuleSet.Add(Fail("a.one")).Add(Fail("a.two")).CheckAsync();

    outcome.ShouldBeFailWith("a.one", "a.two");
  }

  [Test]
  public async Task The_params_entry_point_seeds_one_aggregating_stage()
  {
    RuleCheck outcome = await RuleSet.Add(Fail("a.one"), Pass(), Fail("a.two")).CheckAsync();

    outcome.ShouldBeFailWith("a.one", "a.two");
  }

  [Test]
  public async Task An_all_passing_set_passes()
  {
    RuleCheck outcome = await RuleSet
      .Add(Pass())
      .Then(Pass())
      .Then(static () => Task.FromResult<RuleCheck>(new RuleCheck.Pass()))
      .CheckAsync();

    outcome.ShouldBePass();
  }

  [Test]
  public async Task An_empty_set_passes()
  {
    RuleCheck outcome = await RuleSet.Add().CheckAsync();

    outcome.ShouldBePass();
  }

  [Test]
  public async Task A_failed_stage_stops_the_pipeline_and_reports_only_its_own_violations()
  {
    bool laterStageExecuted = false;

    RuleCheck outcome = await RuleSet
      .Add(Fail("a.one"))
      .Then(() =>
      {
        laterStageExecuted = true;
        return Task.FromResult<RuleCheck>(new RuleCheck.Fail("b.never", "Never seen."));
      })
      .CheckAsync();

    outcome.ShouldBeFailWith("a.one");
    laterStageExecuted.ShouldBeFalse();
  }

  [Test]
  public async Task A_later_stage_runs_once_all_earlier_stages_pass()
  {
    bool laterStageExecuted = false;

    RuleCheck outcome = await RuleSet
      .Add(Pass())
      .Then(() =>
      {
        laterStageExecuted = true;
        return Task.FromResult<RuleCheck>(new RuleCheck.Pass());
      })
      .CheckAsync();

    outcome.ShouldBePass();
    laterStageExecuted.ShouldBeTrue();
  }

  [Test]
  public void Nothing_runs_before_check_time()
  {
    bool executed = false;

    _ = RuleSet
      .Add(Pass())
      .Add(() =>
      {
        executed = true;
        return Task.FromResult<RuleCheck>(new RuleCheck.Pass());
      })
      .Then(() =>
      {
        executed = true;
        return Task.FromResult<RuleCheck>(new RuleCheck.Pass());
      });

    executed.ShouldBeFalse();
  }

  [Test]
  public async Task Async_checks_in_one_stage_all_run_and_aggregate_even_when_earlier_ones_fail()
  {
    RuleCheck outcome = await RuleSet
      .Add()
      .Add(static () => Task.FromResult<RuleCheck>(new RuleCheck.Fail("a.one", "One.")))
      .Add(static () => Task.FromResult<RuleCheck>(new RuleCheck.Fail("a.two", "Two.")))
      .CheckAsync();

    outcome.ShouldBeFailWith("a.one", "a.two");
  }

  [Test]
  public async Task Async_checks_are_awaited_strictly_sequentially_in_registration_order()
  {
    List<string> log = [];

    RuleCheck outcome = await RuleSet
      .Add()
      .Add(async () =>
      {
        log.Add("first:start");
        await Task.Delay(20);
        log.Add("first:end");
        return new RuleCheck.Pass();
      })
      .Add(async () =>
      {
        log.Add("second:start");
        await Task.Delay(1);
        log.Add("second:end");
        return new RuleCheck.Pass();
      })
      .CheckAsync();

    outcome.ShouldBePass();
    log.ShouldBe(["first:start", "first:end", "second:start", "second:end"]);
  }
}
