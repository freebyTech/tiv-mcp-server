using System.Reflection;
using freebyTech.Common.Environment;
using freebyTech.Common.ExtensionMethods;
using freebyTech.Common.Interfaces;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Sinks.SystemConsole.Themes;

namespace tivMcp.Api;

/// <summary>
/// Main entry point for tivMcp.Api application.
/// </summary>
public class Program
{
  /// <summary>
  /// The static ExecutionEnvironment defined for the application.
  /// </summary>
  private static readonly IExecutionEnvironment ExecutionEnvironment = new ExecutionEnvironment(new EnvironmentManager(), Assembly.GetExecutingAssembly());

  /// <summary>
  /// The static Configuration defined for the application.
  /// </summary>
  private static IConfiguration Configuration { get; } = new ConfigurationBuilder().AddDefaultConfiguration(ExecutionEnvironment).Build();

  /// <summary>
  /// Main entry point for application.
  /// </summary>
  /// <param name="args"></param>
  /// <returns></returns>
  public static int Main(string[] args)
  {
    Log.Logger = new LoggerConfiguration()
      .ReadFrom.Configuration(Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder().WithDefaultDestructurers())
      .WriteTo.Console(outputTemplate: "[{Timestamp:MM-dd HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Literate)
      .CreateLogger();

    try
    {
      Log.Information("Starting web host");
      var builder = WebApplication.CreateBuilder(args);
      builder.Configuration.AddConfiguration(Configuration);
      builder.Host.UseSerilog();

      var startup = new Startup(ExecutionEnvironment, builder.Configuration);

      builder.Services.AddSingleton<IExecutionEnvironment>(ExecutionEnvironment);
      startup.ConfigureServices(builder.Services, builder.Configuration);

      var app = builder.Build();

      startup.ConfigureApplication(app);

      app.Run();
      return 0;
    }
    catch (Exception ex)
    {
      Log.Fatal(ex, "Host terminated unexpectedly");
      return 1;
    }
    finally
    {
      Log.CloseAndFlush();
    }
  }
}
