using Gengora.Server.Core;
using Gengora.Server.Core.Compilation;
using Gengora.Server.Core.Discovery;
using Gengora.Server.Core.Execution;
using Gengora.Server.Core.FileWatching;
using Gengora.Server.Core.Messaging;
using Gengora.Server.Lsp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gengora.Server;

/// <summary>
/// Entry Point For Gengora Language Server.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Configure Services
        var services = new ServiceCollection();

        ConfigureLogging(services);
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();

        // Run Server
        using var server = serviceProvider.GetRequiredService<GengoraLanguageServer>();

        try
        {
            await server.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<GengoraLanguageServer>>();

            logger.LogCritical(ex, "Language Server Crashed");

            return 1;
        }
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);

            // Log To Stderr To Avoid Interfering With LSP Protocol On Stdout
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IgnorePatternMatcher>();
        services.AddSingleton<ProjectMarkerScanner>();
        services.AddSingleton<FileWatcherService>();
        services.AddSingleton<DotnetCompilationService>();
        services.AddSingleton<MessageParser>();
        services.AddSingleton<GeneratorExecutor>();
        services.AddSingleton<GeneratorOrchestrator>();

        // LSP Server
        services.AddSingleton<GengoraLanguageServer>();
    }
}
