# Changelog - Gengora

All notable changes to the Gengora extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.4] - 2025-11-23

### Added

- **Generator output directory configuration**: Generated files now created outside generator project in `gengora-output/` folder to avoid compilation conflicts
- **Marker-based generator discovery**: Automatic detection of generator projects using `<IsGeneratorProject>true</IsGeneratorProject>` in .csproj
- **Observation mode system**: Three-level observation (GlobalScan → MinimalObservation → FullObservation) based on project marker
- **Smart file watching**: Intelligent ignore patterns to prevent infinite rebuild loops
  - Automatically ignores `/bin/`, `/obj/`, `/node_modules/`, `.git/`, `.vscode/.generator/`, `/gengora-output/`
  - User-configurable ignore patterns via `gengora.fileWatchIgnorePatterns` setting
- **Hot-reload support**: File changes in generator project trigger automatic rebuild and re-execution
- **JSON-based generator protocol**: Structured communication between generator and extension
  - `generator/hello` handshake with capabilities
  - `generator/generated` events with created file paths
  - `generator/error` for error reporting
- **Extension icon**: Added Gengora branding icon

### Changed

- **Logging improvements**: Reduced verbosity - now only shows warnings and errors by default
- **Build process**: Generator compiled to `.vscode/.generator/out/` folder within generator project
- **Working directory**: Generator process runs with correct workspace root as working directory
- **File watcher architecture**: Combination of LSP DidChangeWatchedFiles + explicit extension watchers for reliability

### Fixed

- **Double-build on startup**: Disabled extension auto-start since server already initializes generator
- **Generated code compilation conflicts**: Generator output excluded from generator project compilation
- **Infinite rebuild loops**: Proper filtering of build output files
- **Wrong workspace opening**: F5 debugging now opens correct test-workspace folder
- **File change detection**: Observation mode properly set on server initialization
- **Process working directory**: Generator creates files in correct location

### Technical Details

- **LSP Server**: OmniSharp.Extensions.LanguageServer 0.19.9, .NET 8.0
- **File Watching**: LSP DidChangeWatchedFiles registration with handler-level filtering
- **Generator Discovery**: Scans workspace for .csproj files with `<IsGeneratorProject>true</IsGeneratorProject>` marker
- **Build System**: `dotnet build` with diagnostic parsing and assembly emission
- **Process Management**: WorkingDirectory support for correct file generation location

## [0.1.0] - 2025-11-20

### Added

- Initial release of Gengora
- Basic LSP server architecture
- Generator project compilation
- File watching capabilities
- Status bar integration
- Output channel for logs

---

**Release Notes**:

- To publish: `git tag gengora-v0.1.4 && git push origin gengora-v0.1.4`
- This will trigger the GitHub Actions workflow to build and publish to VS Code Marketplace
