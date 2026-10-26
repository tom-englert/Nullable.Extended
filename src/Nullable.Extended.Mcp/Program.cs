using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.Mcp;

using TomsToolbox.Composition;
using TomsToolbox.Composition.MicrosoftExtensions;

var builder = Host.CreateApplicationBuilder(args);

// stdout belongs to the protocol, so everything that is logged goes to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

ServerOptions options;

try
{
    options = ServerOptions.Create(builder.Configuration);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<WorkspaceProvider>();

// Compose the analyzer engine the same way as the Visual Studio extension does; the exports are registered with their contract types.
builder.Services.BindExports(typeof(IAnalyzerEngine).Assembly);
builder.Services.AddSingleton<IExportProvider>(sp => new ExportProviderAdapter(sp));

builder.Services.AddSingleton<NullForgivingScanner>();
builder.Services.AddSingleton<CodeContextReader>();

builder.Services
    .AddMcpServer(opt => opt.ServerInstructions = ServerInstructions.Text)
    .WithStdioServerTransport()
    .WithTools<NullForgivingTools>();

var app = builder.Build();

app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nullable.Extended.Mcp").LogInformation(
    "Starting {Version}; working directory '{WorkingDirectory}'; arguments [{Arguments}]; root {Root}; solution {Solution}",
    typeof(ServerOptions).Assembly.GetName().Version,
    Directory.GetCurrentDirectory(),
    string.Join(" ", args),
    options.Root ?? "(from client)",
    options.Solution ?? "(none)");

await app.RunAsync();

return 0;
