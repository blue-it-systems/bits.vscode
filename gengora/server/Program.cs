using System.Text;
using BITS.Gengora.Server.Handlers;
using BITS.Gengora.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

namespace BITS.Gengora.Server;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Determine workspace root from CLI args
        string workspaceRoot = Directory.GetCurrentDirectory();
        
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == Constants.CliArgs.WORKSPACE_ROOT && (i + 1) < args.Length)
            {
                workspaceRoot = args[i + 1];

                break;
            }
        }

        // Set minimum log level to Warning for useful diagnostics
        // Note: OmniSharp's ConfigurationProvider warning cannot be suppressed without setting to Error
        // This warning is harmless - we don't use client-side configuration
        var minLogLevel = LogLevel.Warning;

        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .SetMinimumLevel(minLogLevel)
                    .AddFilter("OmniSharp.Extensions.LanguageServer.Server.Configuration", LogLevel.Error)) // Suppress config warning
                .WithServices(services => ConfigureServices(services, workspaceRoot))
                .WithHandler<ExecuteCommandHandler>()
                .WithHandler<DidChangeWatchedFilesHandler>()
                .OnInitialize
                (
                    async (server, request, cancellationToken) =>
                    {
                        // Initialization (logging disabled - only warnings/errors)
                    }
                )
                .OnInitialized
                (
                    async (server, request, response, cancellationToken) =>
                    {
                        // Do NOT auto-start - wait for explicit user command
                        // This allows users to manually control when the generator starts
                    }
                )
        );

        await server.WaitForExit;
    }

    private static void ConfigureServices(IServiceCollection services, string workspaceRoot)
    {
        // Register core managers
        services.AddSingleton(new GeneratorManager(workspaceRoot));
        services.AddSingleton<ProcessManager>();
        services.AddSingleton(new ObservationManager(workspaceRoot));

        // Register application services
        services.AddSingleton<IGeneratorService, GeneratorService>();
    }
}
