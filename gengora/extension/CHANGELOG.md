# Change Log

All notable changes to the Gengora extension will be documented in this file.

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
