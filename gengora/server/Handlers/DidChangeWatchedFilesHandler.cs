namespace BITS.Gengora.Server.Handlers;

/// <summary>
/// Handles workspace/didChangeWatchedFiles notifications to trigger generator recompilation.
/// </summary>
public class DidChangeWatchedFilesHandler(IGeneratorService generatorService) : DidChangeWatchedFilesHandlerBase
{
    private readonly IGeneratorService _GeneratorService = generatorService;
    private CancellationTokenSource? _DebounceCts;

    public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        // Debounce file changes
        this._DebounceCts?.Cancel();
        this._DebounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(Constants.Timeouts.WATCH_DEBOUNCE_MS, this._DebounceCts.Token);
            await this._GeneratorService.RestartGeneratorAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Debounced - ignore
        }

        return Unit.Value;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DidChangeWatchedFilesRegistrationOptions
        {
            Watchers = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher>
            (
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
                {
                    GlobPattern = "**/*.cs",
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                },
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
                {
                    GlobPattern = "**/*.csproj",
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                }
            )
        };
    }
}
