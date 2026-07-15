using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DoseUp.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class ServiceDefaultsExtensions {
  private const string HEALTH_ENDPOINT_PATH = "/health";
  private const string ALIVENESS_ENDPOINT_PATH = "/alive";

  public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ConfigureOpenTelemetry();

    builder.AddDefaultHealthChecks();

    builder.Services.AddServiceDiscovery();

    builder.Services.ConfigureHttpClientDefaults(static http => {
      // Turn on resilience by default
      http.AddStandardResilienceHandler();

      // Turn on service discovery by default
      http.AddServiceDiscovery();
    });

    // Uncomment the following to restrict the allowed schemes for service discovery.
    // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
    // {
    //     options.AllowedSchemes = ["https"];
    // });

    return builder;
  }

  public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Logging.AddOpenTelemetry(static logging => {
      logging.IncludeFormattedMessage = true;
      logging.IncludeScopes = true;
    });

    builder
      .Services.AddOpenTelemetry()
      .WithMetrics(static metrics => {
        metrics
          .AddAspNetCoreInstrumentation()
          .AddHttpClientInstrumentation()
          .AddRuntimeInstrumentation();
      })
      .WithTracing(tracing => {
        tracing
          .AddSource(builder.Environment.ApplicationName)
          .AddAspNetCoreInstrumentation(static options =>
            // Exclude health check requests from tracing
            options.Filter = static context =>
              !context.Request.Path.StartsWithSegments(
                HEALTH_ENDPOINT_PATH,
                StringComparison.Ordinal
              )
              && !context.Request.Path.StartsWithSegments(
                ALIVENESS_ENDPOINT_PATH,
                StringComparison.Ordinal
              )
          )
          // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
          //.AddGrpcClientInstrumentation()
          .AddHttpClientInstrumentation();
      });

    builder.AddOpenTelemetryExporters();

    return builder;
  }

  private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder {
    bool useOtlpExporter = !string.IsNullOrWhiteSpace(
      builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    );

    if (useOtlpExporter) {
      builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
    //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    //{
    //    builder.Services.AddOpenTelemetry()
    //       .UseAzureMonitor();
    //}

    return builder;
  }

  public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder {
    ArgumentNullException.ThrowIfNull(builder);

    builder
      .Services.AddHealthChecks()
      // Add a default liveness check to ensure app is responsive
      .AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"]);

    return builder;
  }

  public static WebApplication MapDefaultEndpoints(this WebApplication app) {
    ArgumentNullException.ThrowIfNull(app);

    // Adding health checks endpoints to applications in non-development environments has security implications.
    // See https://aka.ms/aspire/healthchecks for details before enabling these endpoints in non-development environments.
    if (app.Environment.IsDevelopment()) {
      // Explicitly anonymous: the API's authorization FallbackPolicy would otherwise
      // require a token on probe traffic. These are the manual `AnonymousAllowed` rows of
      // the authorization matrix (testing.md §4 — non-FastEndpoints surface).

      // All health checks must pass for app to be considered ready to accept traffic after starting
      app.MapHealthChecks(HEALTH_ENDPOINT_PATH).AllowAnonymous();

      // Only health checks tagged with the "live" tag must pass for app to be considered alive
      app.MapHealthChecks(
          ALIVENESS_ENDPOINT_PATH,
          new HealthCheckOptions { Predicate = static r => r.Tags.Contains("live") }
        )
        .AllowAnonymous();
    }

    return app;
  }
}