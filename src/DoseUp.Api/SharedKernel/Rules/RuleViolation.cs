namespace DoseUp.Api.SharedKernel.Rules;

/// <summary>
/// One broken domain rule. <paramref name="Code"/> is contract — stable
/// <c>&lt;aggregate&gt;.&lt;rule&gt;</c> kebab-case (e.g. <c>schedule.not-active</c>),
/// declared in the endpoint's OpenAPI 409 response. <paramref name="Message"/> is the
/// static rule text — developer-English, never interpolating user or dose data (NFR-5).
/// </summary>
public sealed record RuleViolation(string Code, string Message);