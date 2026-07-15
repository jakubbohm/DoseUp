using System.Net;
using FastEndpoints;
using Shouldly;

namespace DoseUp.IntegrationTests.Authorization;

public sealed record MatrixCell(string Name, string Route, string Caller, string Expectation);

/// <summary>
/// The authorization-matrix scaffold (testing.md §4): a reflection census over the API
/// assembly's FastEndpoints endpoint classes, a kind-classification table (writing a row
/// IS the deliberate act of classification), a completeness gate, and one test case per
/// (endpoint × caller class). M0 adds the ProfileScoped/AdminOnly kinds with the account
/// table; M3 `harden-authz` completes the catalog.
/// </summary>
public sealed class AuthorizationMatrixTests {
  [ClassDataSource<AspireAppFixture>(Shared = SharedType.PerTestSession)]
  public required AspireAppFixture Harness { get; init; }

  private enum EndpointKind {
    AnonymousAllowed,
    Authenticated,
  }

  private enum CallerClass {
    Anonymous,
    Authenticated,
    UntrustedKey,
  }

  // ── The classification table: endpoint type → (kind, route). ──
  private static readonly IReadOnlyDictionary<Type, (EndpointKind Kind, string Route)> CLASSIFIED = new Dictionary<Type, (EndpointKind, string)> {
    [typeof(DoseUp.Api.Platform.Diagnostics.IdentityEchoEndpoint)] = (EndpointKind.Authenticated, "/diagnostics/identity"),
  };

  // ── Manual rows for anonymous surface that is not a FastEndpoints endpoint (design.md
  // D6): the health probes ride ASP.NET health checks, outside the reflection census. ──
  private static readonly (string Name, string Route)[] MANUAL_ANONYMOUS_ROWS = [
    ("health (non-FE)", "/health"),
    ("alive (non-FE)", "/alive"),
  ];

  private static List<Type> Census() =>
    [.. typeof(DoseUp.Api.Platform.Diagnostics.IdentityEchoEndpoint).Assembly.GetTypes().Where(static type => typeof(BaseEndpoint).IsAssignableFrom(type) && !type.IsAbstract)];

  [Test]
  public void Every_endpoint_in_the_census_is_classified() {
    // ADR-0002 § Authorization: "new endpoints fail the matrix until classified" — made
    // mechanical: the reflection census and the classification table must match exactly.
    List<string> unclassified = [.. Census().Except(CLASSIFIED.Keys).Select(static type => type.FullName!)];
    List<string> stale = [.. CLASSIFIED.Keys.Except(Census()).Select(static type => type.FullName!)];

    unclassified.ShouldBeEmpty();
    stale.ShouldBeEmpty();
  }

  public static IEnumerable<MatrixCell> Cells() {
    foreach (KeyValuePair<Type, (EndpointKind Kind, string Route)> entry in CLASSIFIED) {
      string name = entry.Key.Name;
      switch (entry.Value.Kind) {
        case EndpointKind.Authenticated:
          yield return new MatrixCell(name, entry.Value.Route, nameof(CallerClass.Anonymous), "401");
          yield return new MatrixCell(name, entry.Value.Route, nameof(CallerClass.UntrustedKey), "401");
          yield return new MatrixCell(name, entry.Value.Route, nameof(CallerClass.Authenticated), "2xx");
          break;
        case EndpointKind.AnonymousAllowed:
          yield return new MatrixCell(name, entry.Value.Route, nameof(CallerClass.Anonymous), "2xx");
          break;
        default:
          throw new InvalidOperationException($"Unclassifiable kind for {name}.");
      }
    }

    foreach ((string name, string route) in MANUAL_ANONYMOUS_ROWS)
      yield return new MatrixCell(name, route, nameof(CallerClass.Anonymous), "2xx");
  }

  [Test]
  [MethodDataSource(nameof(Cells))]
  public async Task The_matrix_holds(MatrixCell cell) {
    ArgumentNullException.ThrowIfNull(cell);

    using HttpClient client = cell.Caller switch {
      nameof(CallerClass.Anonymous) => Harness.CreateAnonymousClient(),
      nameof(CallerClass.Authenticated) => Harness.CreateAuthenticatedClient(Guid.CreateVersion7()),
      nameof(CallerClass.UntrustedKey) => Harness.CreateUntrustedKeyClient(Guid.CreateVersion7()),
      _ => throw new InvalidOperationException($"Unknown caller class '{cell.Caller}'."),
    };

    HttpResponseMessage response = await client.GetAsync(new Uri(cell.Route, UriKind.Relative));

    if (cell.Expectation == "2xx")
      ((int)response.StatusCode).ShouldBeInRange(200, 299);
    else
      response.StatusCode.ShouldBe((HttpStatusCode)int.Parse(cell.Expectation, System.Globalization.CultureInfo.InvariantCulture));
  }
}