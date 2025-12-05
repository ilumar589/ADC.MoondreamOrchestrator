var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage emulator for storing video frames
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

var apiService = builder.AddProject<Projects.MoondreamOrchestrator_ApiService>("apiservice")
    .WithReference(blobs)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.MoondreamOrchestrator_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
