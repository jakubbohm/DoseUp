using DoseUp.Api.Platform.Persistence;
using DoseUp.UnitTests.SharedKernel.Domain;
using Shouldly;

namespace DoseUp.UnitTests.Platform.Persistence;

public sealed class TypedIdConverterTests {
  [Test]
  public void The_provider_type_is_guid_never_a_string() {
    TypedIdConverter<TestId> converter = new();

    converter.ModelClrType.ShouldBe(typeof(TestId));
    converter.ProviderClrType.ShouldBe(typeof(Guid));
  }

  [Test]
  public void A_typed_id_round_trips_losslessly_through_the_converter() {
    TypedIdConverter<TestId> converter = new();
    TestId original = TestId.Create();

    object? provider = converter.ConvertToProvider(original);
    Guid providerValue = provider.ShouldBeOfType<Guid>();
    providerValue.ShouldBe(original.Value);

    object? restored = converter.ConvertFromProvider(providerValue);
    restored.ShouldBeOfType<TestId>().ShouldBe(original);
  }
}