# Changelog - Gengora

<!-- markdownlint-disable MD024 -->

All notable changes to the Gengora extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.4] - 2025-11-23

## [0.1.8] - 2025-11-24

### Fixed

- Multi-root output placement: generated assemblies and artifacts are now emitted under the generator project's folder (`<generator-project>/.vscode/.generator/out`) instead of depending on the server process CWD. This ensures multi-root workspaces place artifacts next to the discovered generator project.

- Generator execution working directory: the language server sets its working directory to the workspace root provided at startup so generator processes run with the generator project's folder as their working directory.

## [0.2.9] - 2025-11-24

### Fixed

- **Generated file notifications**: Added comprehensive debug logging to diagnose why `GENERATOR_GENERATED` notifications were not reaching the client
  - Logs watcher initialization with directory paths
  - Logs file detection attempts and pattern matching
  - Logs session ID validation for security checks
  - Logs notification forwarding to client
  - Logs raw generator stdout for troubleshooting

### Added

- **Diagnostic logging**: Extended server logging to help troubleshoot notification issues
  - `StartOutputWatchers` initialization logging
  - Session ID validation logging
  - Generator stdout capture logging
  - File watcher event logging

## [0.1.9] - 2025-11-24

### Fixed

- Server initialization no longer triggers an error status when no generator project is found. The server will enter scan/minimal observation mode and wait for generators to appear instead of attempting to compile immediately and emitting an error.

- Double-build on startup: Disabled extension auto-start since server already initializes generator
- Generated code compilation conflicts: Generator output excluded from generator project compilation
- Infinite rebuild loops: Proper filtering of build output files
- Wrong workspace opening: F5 debugging now opens correct test-workspace folder
- File change detection: Observation mode properly set on server initialization
- Process working directory: Generator creates files in correct location

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


## [0.2.0] - 2025-11-24

### Added

- Extension: surface generator structured notifications (`generator/hello`, `generator/generated`) in the Gengora output channel and small info prompts so users can quickly open the output or reveal generated files.
- Server: server-side detection of generated files — the language server will watch common output locations and send `generator/generated` notifications when `generated-*` files appear. Importantly, servers now coordinate using an ownership lock so only the server instance that started the generator will forward generated-file events to its client (prevents mirrored notifications across multiple VS Code windows).
- Tooling: improved end-to-end test tooling and added single-file C# E2E and smoke test scripts that avoid broad filesystem scanning and permission issues.

### Notes

- Watchers attempt to be conservative and watch a limited set of candidate directories (project .vscode/.generator/out, parent directories and repository gengora-output) to avoid broad system scans.

## [0.2.1] - 2025-11-24

### Fixed

- Multi-root discovery: server now prefers pre-loaded .csproj paths forwarded by the client so projects living in additional workspace folders (e.g. bits.tenancy/test-workspace) are recognized correctly instead of being missed by a workspace scan.
- Resilience: added retry behavior for StartGeneratorAsync (5 attempts, 10s intervals) to handle flaky builds/starts and emit helpful generator/error notifications during failures.
- Diagnostics: much improved debug logging when scanning .csproj files and attempting to open projects so issues are visible in the Gengora output channel.

## [0.2.2] - 2025-11-24

### Fixed

- Avoid noisy error state when a generator is already owned by another server instance: the server will now treat an already-owned generator as an observational state instead of emitting an ERROR status. This prevents mirrored error messages when reloading multiple VS Code windows that point at the same generator project.



<!-- NOTE: older 0.1.x release notes consolidated above; continuing from 0.1.9 → 0.2.0 -->

---

**Release Notes**:

- To publish: `git tag gengora-v0.1.4 && git push origin gengora-v0.1.4`
- This will trigger the GitHub Actions workflow to build and publish to VS Code Marketplace
