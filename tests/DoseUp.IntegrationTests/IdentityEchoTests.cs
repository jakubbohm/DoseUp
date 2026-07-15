using System.Net;
using Shouldly;

namespace DoseUp.IntegrationTests;

public sealed class IdentityEchoTests
{
  [ClassDataSource<AspireAppFixture>(Shared = SharedType.PerTestSession)]
  public required AspireAppFixture Harness { get; init; }

  private sealed record IdentityEchoBody(string Oid);

  private sealed record ProblemBody(string? Type, string? Title, int? Status);

  [Test]
  public async Task A_valid_token_gets_its_oid_echoed()
  {
    // api-shell R3: the whole token pipeline — bearer middleware, test trust anchor,
    // MapInboundClaims off — proven over the wire.
    Guid oid = Guid.CreateVersion7();
    using HttpClient client = Harness.CreateAuthenticatedClient(oid);

    HttpResponseMessage response = await client.GetAsync(
      new Uri("/diagnostics/identity", UriKind.Relative)
    );

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    IdentityEchoBody? body = await response.Content.ReadFromJsonAsync<IdentityEchoBody>();
    body.ShouldNotBeNull().Oid.ShouldBe(oid.ToString());
  }

  [Test]
  public async Task A_missing_token_is_denied_with_a_problem_details_body()
  {
    // api-shell R1 scenario 1 + error-contract R1 — the §9.2 401-half verification:
    // the bare middleware denial carries an RFC 9457 body.
    using HttpClient client = Harness.CreateAnonymousClient();

    HttpResponseMessage response = await client.GetAsync(
      new Uri("/diagnostics/identity", UriKind.Relative)
    );

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    response
      .Content.Headers.ContentType.ShouldNotBeNull()
      .MediaType.ShouldBe("application/problem+json");
    ProblemBody? problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
    problem.ShouldNotBeNull().Status.ShouldBe(401);
  }

  [Test]
  public async Task A_token_signed_by_an_untrusted_key_is_denied()
  {
    // api-shell R1 scenario 2: local validation, no trust anchor match → 401.
    using HttpClient client = Harness.CreateUntrustedKeyClient(Guid.CreateVersion7());

    HttpResponseMessage response = await client.GetAsync(
      new Uri("/diagnostics/identity", UriKind.Relative)
    );

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }
}
