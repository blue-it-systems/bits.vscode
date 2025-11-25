# Changelog

All notable changes to the Gengora extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2025-11-25

### Added

- **Status Bar Quick Menu**: Click the status bar to access all commands
  - Show Output
  - Start/Stop/Restart Server
  - Recompile Generator
  - Set Log Level
  - View current status
- **New Commands**:
  - `Gengora: Show Commands` - Opens the quick pick menu
  - `Gengora: Start Server` - Manually start the language server
  - `Gengora: Restart Server` - Restart the language server
  - `Gengora: Set Log Level` - Change logging verbosity at runtime
- **Sample Generator Projects**:
  - `BasicGenerator` - Fully documented minimal generator example
  - `Gengora.Generator.Abstractions` - Type-safe library for generator authors
- **Extension Icon**: Added custom extension icon
- **Improved Documentation**: Comprehensive README with use cases and examples

### Changed

- Status bar now shows "Click for commands" in tooltip
- Default log level changed from "debug" to "info"
- Server path now points to bundled server location
- Updated publisher to "blue-it-systems"

### Fixed

- Marker change detection (false → true now activates server)
- Cancellation recovery (server no longer hangs in Compiling state)
- LSP initialization with proper parameter deserialization

### Technical

- Added `.vscodeignore` for optimized package size
- Added `@vscode/vsce` for extension packaging
- Extension package target: ~5-6 MB total

## [0.3.0] - 2024-12-XX

### Added

- Initial release of Gengora extension
- **Discovery**: Automatic detection of generator projects via `<IsGeneratorProject>true</IsGeneratorProject>` marker
- **State Machine**: Full lifecycle management with states: Idle, GeneratorFound, Compiling, Ready, Running, Error, Stopped
- **Hot-Reload**: Automatic recompilation and re-execution when source files change
- **File Watching**: Efficient file system monitoring with configurable ignore patterns
- **Roslyn Compilation**: Native compilation using Microsoft.CodeAnalysis.Workspaces.MSBuild
- **Message Protocol**: JSON lines protocol for generator communication
  - Status messages (`generator/status`)
  - File emit messages (`generator/file`)
  - Session ID validation
- **LSP Server**: StreamJsonRpc-based language server with stdin/stdout transport
- **VS Code Extension**: TypeScript extension with:
  - Status bar integration with state icons
  - Output channel for logging
  - Commands: Recompile, Stop, Show Output
  - Configuration options
- **Sample Generator**: Working example generator in test-workspace
- **TUnit Tests**: Comprehensive server-side tests
- **Extension Tests**: VS Code integration tests

### Architecture

- .NET 10.0 server with modern C# features
- Microsoft.CodeAnalysis.Workspaces.MSBuild for project loading
- StreamJsonRpc 2.22.23 for LSP communication
- vscode-languageclient 9.0.1 for VS Code integration
- TUnit 1.2.11 for server testing

### Known Limitations

- Single generator project support (v1)
- Single workspace root support
- No MSBuild SDK resolution hints (relies on MSBuildLocator)

## [Unreleased]

### Planned

- Multiple generator project support
- Multi-root workspace support
- Generator project templates
- Performance optimizations for large workspaces
- Incremental compilation support
- NuGet package for Gengora.Generator.Abstractions
