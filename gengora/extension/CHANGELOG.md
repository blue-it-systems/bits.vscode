# Change Log

<!-- markdownlint-disable MD024 -->

All notable changes to the Gengora extension will be documented in this file.

## [0.2.8] - 2025-11-24

### Fixed

- **Hot-reload now works**: Generator now correctly restarts when files are saved in the project. Fixed issue where `RestartGeneratorAsync()` was not triggering actual restart.
- **Window reload now works**: Generator auto-starts correctly after reloading VS Code. Fixed state management to properly clear `_IsManuallyStopped` flag on initialization.
- **Stable version**: Simplified lifecycle management - generator runs immediately and restarts on every file change (unless manually stopped by user).

### Changed

- Removed complex lock file system entirely (was causing coordination issues)
- Simplified state tracking in GeneratorService
- RestartGeneratorAsync() now unconditionally starts generator (instead of being a no-op)

## [0.2.7] - 2025-11-24

### Changed

- Server: removed file lock implementation. Multiple server instances can now independently manage generators without lock file coordination. Session handshake (GENGORA_SESSION_ID) remains for process validation.

## [0.2.6] - 2025-11-24

### Added

- Server: runtime session handshake and validation for generator messages. The server now generates a per-run session identifier and injects it into generator processes as environment variables (GENGORA_SESSION_ID / GENGORA_SERVER_ID). Incoming generator JSON messages that include a sessionId will be validated and only accepted when they match the owning server's active session. This prevents cross-instance / mirrored generator messages.

- Tests: unit tests added to cover GeneratorService session-handshake behavior (matching session accepted; mismatched session ignored; missing session accepted for backward compatibility).

### Notes

- Sample generator in the repo (test-workspace) has been updated to emit sessionId and serverId in generator/hello and generator/generated messages so the handshake is exercised end-to-end for modern generators.

## [0.1.8] - 2025-11-24

### Fixed

- Generator artifacts emitted next to discovered generator project: The server now emits the generator assembly into the generator project's folder (under `.vscode/.generator/out`) rather than the extension's first workspace `.vscode` directory. This fixes multi-root workspace scenarios where the generator project lives in a different workspace folder.

- Generator execution working directory: The server ensures the generator process is started with the generator project's directory as its working directory so generators reliably generate output in the expected workspace location.

### Notes

- This resolves cases where creating/pasting a generator project with the marker caused the server to report it's running even when no artifacts were generated because the emitted assembly landed in the wrong folder.

## [0.1.6] - 2025-11-24

## [0.1.9] - 2025-11-24

### Fixed

- Activation shouldn't error if no generator project is found: Server initialization no longer attempts to auto-start compilation when a generator project is absent. Instead, the server enters scan/minimal observation mode (waiting for a generator to appear) and avoids emitting an error status during activation.

### Notes

- This reduces noisy activation failures when opening workspaces that don't yet contain a generator project; extension will continue to register minimal file watchers and wait for a generator to be added.


## [0.2.0] - 2025-11-24

### Added

- Extension: surface generator structured notifications (`generator/hello`, `generator/generated`) in the Gengora output channel and small info prompts so users can quickly open the output or reveal generated files.
- Server: server-side detection of generated files — the language server watches common output locations and forwards `generator/generated` notifications when `generated-*` files appear. Servers coordinate with a per-project ownership lock file so only the server instance that started a generator forwards generated-file events to its client (prevents mirrored notifications across separate VS Code windows).
- Tooling: improved end-to-end test tooling and single-file C# E2E / smoke tests for CI-friendly runs.

### Notes

- Generated files are still excluded by default from file watchers to avoid rebuild loops. Watchers remain conservative and limited to reasonable candidate locations.


## [0.2.1] - 2025-11-24

### Fixed

- Multi-root discovery: the server now prefers an already-loaded project when the extension forwards a specific `.csproj` path, avoiding re-scanning the wrong workspace root and failing to start in multi-root scenarios.
- Resiliency: Start/restart now uses a conservative retry policy (5 attempts, 10s interval) for build/emit/run failures, making warm/retry cycles more robust.
- Diagnostics: Improved debug logging for .csproj scanning and TryOpenProjectAtPathAsync so discovery problems are visible in the Gengora output as useful traces.
- Error forwarding: the server sends `generator/error` notifications with details and stack traces; the extension will log full stacks at debug level so you can inspect failures easily.
- **Multi-root discovery**: Extension now scans all open workspace folders for .csproj files containing the `<IsGeneratorProject>true</IsGeneratorProject>` marker before starting the server. When a project is discovered it's passed to the server via the GENERATOR_PROJECT_PATH environment variable and the server workspace root is set to the project folder for reliable initialization.
- **Server: Pick up project on CSProj changes**: If no project was previously loaded, the server will now attempt to treat a newly created/changed `.csproj` file as a candidate generator project (if it contains the marker) and auto-start it. This fixes cases where the generator is in a non-primary or additional workspace folder.

### Notes

- This is a small patch release focused on reliability and diagnostics to make multi-root projects and flaky generator start more robust.
- Improved logging and troubleshooting information for multi-root workspaces.

## [0.2.2] - 2025-11-24

### Fixed

- Avoid noisy ERROR status when the generator is already running under another server instance. The server now treats an already-owned generator as an observational state and will not emit an ERROR status, which prevents confusing error messages when reloading multiple VS Code windows pointing at the same generator project.

## [0.2.3] - 2025-11-24

### Fixed

- Multi-root generator detection: when auto-discovering a generator project in a multi-root workspace, the extension now starts the language server with the top-level workspace folder that contains the discovered .csproj (instead of the csproj parent folder). This allows the server to correctly watch repository-level gengora-output directories and detect generated files in multi-root setups.

## [0.2.4] - 2025-11-24

### Fixed / Improved

- Make server 'skip' reasons visible: when a server refuses to start a generator because the project is outside its workspace root or because another server owns the generator, the extension will now display the server-provided message at Info level so you can see why generation didn't start.

## [0.2.5] - 2025-11-24

### Fixed / Improved

- Client-side output watchers: the extension now creates conservative watchers for candidate output locations (project-level .vscode/.generator/out and repository gengora-output folders) and will surface generated-* files directly in the client. This means you will see generated-file events even if the local server instance did not start the generator (e.g. another VS Code window owns the generator process). Duplicate events are Deduplicated locally for a short period to avoid noisy duplicates.






### Added

- **Dynamic File Watchers**: Intelligent file watching based on generator discovery state
  - Minimal mode (no generator found): Only watches `.csproj` files for marker detection
  - Full mode (generator found): Watches `.cs`, `.csproj`, and `.json` files
  - Automatically switches between modes as generator state changes
  - Reduced log spam and unnecessary file system event processing

- **Gitignore Integration**: Auto-merge `.gitignore` patterns with exclude patterns
  - New setting `gengora.mergeGitignore` (default: true)
  - Automatically parses `.gitignore` from generator project folder
  - Converts gitignore patterns to glob format for file watching
  - Users can disable merging if not needed

- **Generator Project Discovery Notifications**: Improved visibility into generator discovery
  - Logs full path when generator project is discovered
  - New notification: `GENERATOR_PROJECT_DISCOVERED` with project path
  - Status bar shows `OBSERVING_MINIMAL` and `OBSERVING_FULL` states

- **Observation Mode Tracking**: Extension tracks generator observation mode changes
  - New notification: `OBSERVATION_MODE_CHANGED` with mode and project folder
  - Automatically creates/disposes watchers based on mode
  - Better debugging with observation mode logging

### Changed

- **Removed Redundant Watchers**: Eliminated workspace-wide deletion watcher
  - No longer creates `**` pattern watcher for all file deletions
  - Marker-based system already handles cleanup properly
  - Reduced overhead and log noise

- **Deprecated autoRunOnCompileSuccess**: Setting now marked as deprecated
  - Server handles auto-start internally
  - Changed from INFO warning to DEBUG message
  - Kept for backward compatibility

### Technical Improvements

- Added `.gitignore` pattern parsing and conversion utilities
- Implemented dynamic file watcher lifecycle management
- Added comprehensive observation mode change handling
- Improved extension logging with clearer state transitions

## [0.1.5] - 2025-11-24

### Added

- **Smart Auto-Start**: Generator now auto-starts on extension activation
  - Automatically starts on first install and fresh workspaces
  - Respects user's decision when manually stopped (persists across VS Code reloads)
  - Clear manual start restores auto-start behavior

### Changed

- **VSIX Size Optimization**: Reduced package size from 11.32 MB to 10.15 MB (10% smaller)
  - Removed PDB debug symbols via `DebugType=none` in Release builds
  - File count reduced from 176 to 84 files

- **Manual Stop Persistence**: Manual stop state now persists across VS Code sessions
  - Uses workspace state storage for persistence
  - Independent state per workspace
  - Prevents unwanted auto-restarts after explicit user stop

- **Log Level Management**: Improved logging system
  - Default log level changed from "info" to "warning"
  - LSP client output now respects log level settings
  - Custom FilteredOutputChannel for LSP messages at Debug level

### Fixed

- **Build Stability**: Added automatic clean operation when toggling `IsGeneratorProject` marker
  - Prevents "CreateAppHost" build errors from corrupted obj folder
  - Executes `dotnet clean` before starting/stopping generator

- **Auto-Restart Control**: Server no longer auto-restarts when manually stopped
  - Added `_IsManuallyStopped` flag to GeneratorService
  - Checks flag before auto-restart on file changes or project switches
  - Preserves user intent for manual control

### Performance

- Release build optimizations in GeneratorServer.csproj:
  - `Optimize=true` for better runtime performance
  - `DebugType=none` to exclude debug symbols
  - `SatelliteResourceLanguages=en` to exclude unused localizations

## [0.1.4] - 2025-11-23

### Initial Release

- Real-time code generation with hot-reload support
- LSP-based architecture with Language Server Protocol
- Observation mode toggling via `IsGeneratorProject` marker
- File watching for .cs, .csproj, and .json files
- Status bar integration with generator state
- Output channel for generator stdout/stderr
- Commands: Start Generator, Stop Generator, Show Output
