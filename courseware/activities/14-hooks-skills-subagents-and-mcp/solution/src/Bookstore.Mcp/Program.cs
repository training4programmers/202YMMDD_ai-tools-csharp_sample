using Bookstore.Domain;
using Bookstore.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol messages, so every log line must go to stderr instead.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<PriceCalculator>();
builder.Services.AddSingleton<IBookCatalog>(services =>
    new SqliteBookCatalog(BookstoreDatabase.GetConnectionString(services.GetRequiredService<IConfiguration>())));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<BookTools>();

await builder.Build().RunAsync();
