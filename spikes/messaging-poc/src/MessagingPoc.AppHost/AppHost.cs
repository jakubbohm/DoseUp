// Spike #51: the whole async stack runs locally — Postgres + the Azure Service Bus emulator —
// so messaging features are testable without any cloud connection.

using Aspire.Hosting.Azure;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres");
IResourceBuilder<PostgresDatabaseResource> messagingDb = postgres.AddDatabase("messagingdb");

// AddAzureServiceBus models a real Basic-tier namespace for publish/prod (hand-authored Bicep);
// RunAsEmulator swaps in the official servicebus-emulator container (+ its mssql sidecar) locally.
IResourceBuilder<AzureServiceBusResource> serviceBus = builder
  .AddAzureServiceBus("messaging")
  .RunAsEmulator();

// Declaring the queue seeds it into the emulator's config AND models it for Bicep generation, so
// Wolverine runs with AutoProvision OFF everywhere — local mirrors prod (Aspire issue #14041: the
// emulator's admin/management connection string isn't surfaced, so we never rely on it).
serviceBus.AddServiceBusQueue("dose-events");

builder.AddProject<Projects.MessagingPoc_Api>("api")
  .WithReference(messagingDb)
  .WithReference(serviceBus)
  .WaitFor(messagingDb)
  .WaitFor(serviceBus);

builder.Build().Run();
