IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres");
IResourceBuilder<PostgresDatabaseResource> doseupDb = postgres.AddDatabase("doseupdb");

// Schema path (design.md D8): postgres → migration runner → api. Local dev and the test
// harness apply migrations through this identical path (testing.md §3a/§9.5).
IResourceBuilder<ProjectResource> migration = builder
  .AddProject<Projects.DoseUp_MigrationService>("migration")
  .WithReference(doseupDb)
  .WaitFor(doseupDb);

builder.AddProject<Projects.DoseUp_Api>("api").WithReference(doseupDb).WaitForCompletion(migration);

builder.Build().Run();
