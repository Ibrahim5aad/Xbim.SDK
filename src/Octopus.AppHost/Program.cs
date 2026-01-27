var builder = DistributedApplication.CreateBuilder(args);

// Add the Octopus API with external HTTP endpoints
var server = builder.AddProject<Projects.Octopus_Server_App>("octopus-server")
    .WithExternalHttpEndpoints();

// Add the Octopus Web app with reference to the server
// The web app uses service discovery to connect to the server via "http://octopus-server"
var web = builder.AddProject<Projects.Octopus_Web>("octopus-web")
    .WithExternalHttpEndpoints()
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
