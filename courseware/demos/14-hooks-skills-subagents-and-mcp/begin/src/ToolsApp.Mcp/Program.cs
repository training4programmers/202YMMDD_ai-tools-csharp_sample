using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToolsApp.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol messages, so every log line must go to stderr instead.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IColorCatalog>(services =>
    new SqliteColorCatalog(ToolsAppDatabase.GetConnectionString(services.GetRequiredService<IConfiguration>())));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ColorTools>();

await builder.Build().RunAsync();
