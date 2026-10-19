using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Data;
using ToolsApp.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// The connection string is read when the DbContext is resolved (not here), so tests can override it.
builder.Services.AddDbContext<ToolsAppDbContext>((services, options) => options
    .UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("ToolsApp"))
    .UseSeeding((context, _) => SeedData.Seed((ToolsAppDbContext)context))
    .UseAsyncSeeding((context, _, cancellationToken) =>
        SeedData.SeedAsync((ToolsAppDbContext)context, cancellationToken)));

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ToolsAppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapColorEndpoints();
app.MapPaletteEndpoints();

app.Run();
