using System.Text.Json.Serialization;
using Bookstore.Api.Data;
using Bookstore.Api.Endpoints;
using Bookstore.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The connection string is read when the DbContext is resolved (not here), so tests can override it.
builder.Services.AddDbContext<BookstoreDbContext>((services, options) => options
    .UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Bookstore"))
    .UseSeeding((context, _) => SeedData.Seed((BookstoreDbContext)context))
    .UseAsyncSeeding((context, _, cancellationToken) =>
        SeedData.SeedAsync((BookstoreDbContext)context, cancellationToken)));

builder.Services.AddSingleton<PriceCalculator>();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookstoreDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapBookEndpoints();
app.MapAuthorEndpoints();
app.MapQuoteEndpoints();
app.MapBookLookupEndpoints();

app.Run();
