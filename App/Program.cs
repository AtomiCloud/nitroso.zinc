using App.StartUp;
using App.StartUp.Options;

var landscape =
  Environment.GetEnvironmentVariable("LANDSCAPE")?.ToLower()
  ?? throw new ApplicationException("LANDSCAPE not defined");

/*----------------------------------------*/
// Bootstrap

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder
  .Configuration.AddYamlFile("Config/settings.yaml", optional: false, reloadOnChange: true)
  .AddYamlFile($"Config/settings.{landscape}.yaml", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables(prefix: "Atomi_");

var services = builder.Services;

// Options
services.AddStartupOptions();

// Register Server
services.AddSingleton<Server, Server>();

// Start
var app = builder.Build();
var server = app.Services.GetService<Server>();
var s = server ?? throw new ApplicationException("Server not found");

s.Start(landscape, args);
