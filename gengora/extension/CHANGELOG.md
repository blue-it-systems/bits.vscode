# Change Log

All notable changes to the Gengora extension will be documented in this file.

## [0.1.7] - 2025-11-24

## [0.1.6] - 2025-11-24

### Fixed

- **Multi-root discovery**: Extension now scans all open workspace folders for .csproj files containing the `<IsGeneratorProject>true</IsGeneratorProject>` marker before starting the server. When a project is discovered it's passed to the server via the GENERATOR_PROJECT_PATH environment variable and the server workspace root is set to the project folder for reliable initialization.

- **Server: Pick up project on CSProj changes**: If no project was previously loaded, the server will now attempt to treat a newly created/changed `.csproj` file as a candidate generator project (if it contains the marker) and auto-start it. This fixes cases where the generator is in a non-primary or additional workspace folder.

### Notes

- Improved logging and troubleshooting information for multi-root workspaces.


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
