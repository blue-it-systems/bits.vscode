using BITS.Gengora.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

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
        await (this._DebounceCts?.CancelAsync() ?? Task.CompletedTask);
        this._DebounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(Constants.Timeouts.WATCH_DEBOUNCE_MS, this._DebounceCts.Token);
            
            // Handle file changes (check for marker changes, trigger recompilation if needed)
            foreach (var change in request.Changes)
            {
                var filePath = change.Uri.GetFileSystemPath();
                if (!string.IsNullOrEmpty(filePath))
                {
                    await this._GeneratorService.HandleFileChangeAsync(filePath, cancellationToken);
                }
            }
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
                    GlobPattern = new GlobPattern("**/*.cs"),
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                },
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
                {
                    GlobPattern = new GlobPattern("**/*.csproj"),
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                }
            )
        };
    }
}
