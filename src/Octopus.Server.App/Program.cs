var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Octopus Server API",
        Version = "v1",
        Description = "BIM backend API for the Octopus platform"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Octopus Server API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }))
   .WithName("HealthCheck")
   .WithOpenApi();

app.Run();
