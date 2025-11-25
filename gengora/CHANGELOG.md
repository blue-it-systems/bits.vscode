# Changelog

All notable changes to the Gengora extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
- **Extension Tests**: VS Code integration tests using @vscode/test-cli

### Architecture

- .NET 10.0 server with modern C# features
- Microsoft.CodeAnalysis.Workspaces.MSBuild for project loading
- StreamJsonRpc 2.22.23 for LSP communication
- vscode-languageclient 9.0.1 for VS Code integration
- TUnit 1.2.11 for server testing

### Technical Details

- Implements specification rules R1.* through R8.*
- Default ignore patterns: `bin/`, `obj/`, `node_modules/`, `.git/`, `.vs/`, `.nuget/`
- Supports gitignore pattern loading
- Session ID-based message validation
- Protected directory validation for file emissions

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
