using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

public sealed class SharedKernelRuleTests
{
  [Test]
  public void Rule_05_shared_kernel_references_nothing_project_internal()
  {
    // ADR-0002 dependency rule 5: "SharedKernel references nothing project-internal."
    IArchRule rule = Types()
      .That()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.SharedKernel")
      .Should()
      .NotDependOnAnyTypesThat()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.(Modules|Platform)");

    rule.ShouldHold();
  }
}
