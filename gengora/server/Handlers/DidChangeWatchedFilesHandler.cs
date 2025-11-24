using BITS.Gengora.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace BITS.Gengora.Server.Handlers;

/// <summary>
/// Handles workspace/didChangeWatchedFiles notifications to trigger generator recompilation.
/// Dynamically adjusts watch patterns based on ObservationManager state.
/// </summary>
public class DidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase
{
    private readonly IGeneratorService _GeneratorService;
    private readonly ObservationManager _ObservationManager;
    private readonly ILanguageServerFacade _LanguageServer;
    private CancellationTokenSource? _DebounceCts;
    
    // Standard ignore patterns for file watching
    private static readonly string[] IgnorePatterns = new[]
    {
        "/bin/",
        "/obj/",
        "/node_modules/",
        "/.git/",
        "/.vs/",
        "/.vscode/.generator/",
        "/packages/",
        "/.idea/",
        "/gengora-output/"
    };

    public DidChangeWatchedFilesHandler(
        IGeneratorService generatorService, 
        ObservationManager observationManager,
        ILanguageServerFacade languageServer)
    {
        this._GeneratorService = generatorService;
        this._ObservationManager = observationManager;
        this._LanguageServer = languageServer;
        
        // Subscribe to observation mode changes to re-register watchers
        this._ObservationManager.OnModeChanged += (oldMode, newMode) =>
        {
            // Note: Dynamic re-registration would require client.registerCapability
            // For now, we register broadly and filter in Handle()
        };
    }

    public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        // Filter changes based on current observation mode
        var relevantChanges = new List<FileEvent>();
        var projectFolder = this._ObservationManager.CurrentProjectFolder;
        
        foreach (var change in request.Changes)
        {
            var filePath = change.Uri.GetFileSystemPath();
            if (string.IsNullOrEmpty(filePath)) continue;
            
            // Do not ignore generator-produced files (special-case)
            var fileName = Path.GetFileName(filePath) ?? string.Empty;
            if (!fileName.StartsWith("generated-", StringComparison.OrdinalIgnoreCase))
            {
                // Check ignore patterns first
                if (this.ShouldIgnoreFile(filePath))
                {
                    continue;
                }
            }
            
            // In FullObservation mode, only watch files in the generator project folder
            if (this._ObservationManager.CurrentMode == ObservationMode.FullObservation)
            {
                if (!string.IsNullOrEmpty(projectFolder) && !filePath.StartsWith(projectFolder, StringComparison.OrdinalIgnoreCase))
                {
                    // Allow important generator output folders even if they're outside the project folder
                    var normalized = filePath.Replace('\\', '/');
                    if (!normalized.Contains("/gengora-output/") && !normalized.Contains("/.vscode/.generator/out/"))
                    {
                        continue;
                    }
                }
            }
            
            relevantChanges.Add(change);
        }
        
        if (relevantChanges.Count == 0)
        {
            return Unit.Value;
        }
        
        // Debounce file changes
        await (this._DebounceCts?.CancelAsync() ?? Task.CompletedTask);
        this._DebounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(Constants.Timeouts.WATCH_DEBOUNCE_MS, this._DebounceCts.Token);
            
            // First, detect any 'generated-*' files and forward them as generator/generated notifications
            var generatedFiles = new List<string>();
            foreach (var change in relevantChanges)
            {
                var p = change.Uri.GetFileSystemPath();
                if (string.IsNullOrEmpty(p)) continue;

                var name = Path.GetFileName(p) ?? string.Empty;
                if (!string.IsNullOrEmpty(name) && name.StartsWith("generated-", StringComparison.OrdinalIgnoreCase))
                {
                    // Only include create/changed events as actual generated activity
                    if (change.Type == OmniSharp.Extensions.LanguageServer.Protocol.Models.FileChangeType.Created || change.Type == OmniSharp.Extensions.LanguageServer.Protocol.Models.FileChangeType.Changed)
                    {
                        generatedFiles.Add(p);
                    }
                }
            }

            if (generatedFiles.Count > 0)
            {
                // Send a structured notification to the client so editors can surface where files were generated
                try
                {
                    this._LanguageServer.SendNotification(Constants.Notifications.GENERATOR_GENERATED, new
                    {
                        projectPath = projectFolder,
                        created = generatedFiles
                    });
                }
                catch
                {
                    // best effort - do not fail the handler
                }
            }

            // Handle file changes (check for marker changes, trigger recompilation if needed)
            foreach (var change in relevantChanges)
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
    
    private bool ShouldIgnoreFile(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        
        foreach (var pattern in IgnorePatterns)
        {
            if (normalizedPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
    {
        // Register broadly - we'll filter in Handle() based on current observation mode
        // This avoids needing dynamic re-registration when mode changes
        var watchers = new List<OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher>
        {
            new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
            {
                GlobPattern = new GlobPattern("**/*.csproj"),
                Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
            },
            new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
            {
                GlobPattern = new GlobPattern("**/*.cs"),
                Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
            },
            new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
            {
                GlobPattern = new GlobPattern("**/*.json"),
                Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
            }
        };
        
        return new DidChangeWatchedFilesRegistrationOptions
        {
            Watchers = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher>(watchers)
        };
    }
}
