using FastEndpoints;

namespace DoseUp.Api.Platform.Diagnostics;

public sealed record IdentityEchoResponse(string Oid);

/// <summary>
/// Authenticated diagnostic proving the token pipeline end to end: echoes the caller's
/// <c>oid</c> claim straight from the validated token — no database (api-shell spec).
/// M0 layers <c>ActiveAccount</c>/<c>CallerContext</c> on top. Authorization-matrix
/// classification: FastEndpoints kind <c>Authenticated</c> (testing.md §4).
/// </summary>
public sealed class IdentityEchoEndpoint : EndpointWithoutRequest<IdentityEchoResponse> {
  public override void Configure() =>
    // Authenticated by default: FastEndpoints' secure-by-default plus the app-wide
    // authorization FallbackPolicy — no AllowAnonymous here, ever.
    Get("/diagnostics/identity");

  public override async Task HandleAsync(CancellationToken ct) {
    // A validated token without `oid` is a trust-anchor misconfiguration — a bug (class 8),
    // not an expected failure.
    string oid = User.FindFirst("oid")?.Value ?? throw new InvalidOperationException("The validated token carries no 'oid' claim.");

    await Send.OkAsync(new IdentityEchoResponse(oid), ct);
  }
}