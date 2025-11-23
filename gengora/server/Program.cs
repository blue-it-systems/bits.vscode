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

        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    .SetMinimumLevel(LogLevel.Debug))
                .WithServices(services => ConfigureServices(services, workspaceRoot))
                .WithHandler<ExecuteCommandHandler>()
                .WithHandler<DidChangeWatchedFilesHandler>()
                .OnInitialize
                (
                    async (server, request, cancellationToken) =>
                    {
                        // Log initialization
                        Console.Error.WriteLine($"[Gengora Server] Initializing for workspace: {workspaceRoot}");
                        await Task.CompletedTask;
                    }
                )
                .OnInitialized
                (
                    async (server, request, response, cancellationToken) =>
                    {
                        Console.Error.WriteLine("[Gengora Server] Initialized successfully");
                        await Task.CompletedTask;
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

        // Register application services
        services.AddSingleton<IGeneratorService, GeneratorService>();
    }
}
