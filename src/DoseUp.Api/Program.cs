using DoseUp.Api.Platform.Persistence;
using DoseUp.Api.SharedKernel.Events;
using DoseUp.ServiceDefaults;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Persistence: DbContext = unit of work; events drain in the interceptor (D8) ──

builder.Services.AddScoped<DomainEventDispatcher>();
builder.Services.AddScoped<DomainEventDispatchInterceptor>();

string doseupDbConnectionString =
  builder.Configuration.GetConnectionString("doseupdb")
  ?? throw new InvalidOperationException(
    "Connection string 'doseupdb' is missing — run via the AppHost (aspire start) or the test harness."
  );

builder.Services.AddDbContext<DoseUpDbContext>(
  (services, options) =>
    options
      .UseNpgsql(doseupDbConnectionString)
      .AddInterceptors(services.GetRequiredService<DomainEventDispatchInterceptor>())
);

// Aspire client integration: OTel + connection retries; the DB health check stays off —
// the probe path never touches the database (ADR-0001, api-shell spec).
builder.EnrichNpgsqlDbContext<DoseUpDbContext>(static settings =>
  settings.DisableHealthChecks = true
);

// ── ProblemDetails: every non-2xx carries an RFC 9457 body (D7) ──

builder.Services.AddProblemDetails();

// ── Authentication: one bearer scheme, config-driven trust anchor (D5) ──

IConfigurationSection testAuthority = builder.Configuration.GetSection("Auth:TestAuthority");
if (testAuthority.Exists() && builder.Environment.IsProduction())
{
  throw new InvalidOperationException("Auth:TestAuthority must never be configured in Production.");
}

builder
  .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    // `oid` must arrive as `oid` — no legacy inbound claim-type mapping.
    options.MapInboundClaims = false;

    if (testAuthority.Exists())
    {
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidIssuer = Required(testAuthority, "Issuer"),
        ValidAudience = Required(testAuthority, "Audience"),
        IssuerSigningKey = new SymmetricSecurityKey(
          Convert.FromBase64String(Required(testAuthority, "SigningKey"))
        ),
      };
    }

    // With no authority configured, validation has no trust anchor and every token is
    // rejected — secure by default. M0 wires Entra External ID as the primary authority.
  });

// Secure by default (PRE-10 ring 0): every endpoint without explicit auth metadata
// requires an authenticated caller; anonymous endpoints opt out explicitly.
builder.Services.AddAuthorization(static options =>
  options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
);

builder.Services.AddFastEndpoints();

WebApplication app = builder.Build();

// Exception handler + status-code pages both write ProblemDetails via the registered
// service, so bare middleware denials (401) and unhandled exceptions (500, sanitized)
// carry RFC 9457 bodies too (error-contract spec).
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints();

app.MapDefaultEndpoints();

app.Run();

static string Required(IConfigurationSection section, string key) =>
  section[key]
  ?? throw new InvalidOperationException(
    $"Auth:TestAuthority:{key} is required when the section is present."
  );
