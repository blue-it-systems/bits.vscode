# Changelog

All notable changes to this project will be documented in this file.

## [1.1.2] - 2025-01-07

### Fixed

- Fixed debug configuration: use DLL path directly as `program` instead of
  `dotnet exec` for proper debugger attachment

## [1.1.1] - 2025-01-07

### Fixed

- Fixed "Add Configuration" snippet generating invalid JSON with escaped `$` characters

## [1.1.0] - 2025-01-07

### Added

- **Dynamic DLL Path Detection**: New `getDllPath` command that automatically
  finds the correct DLL path based on the current test file's project
- **Multi-Project Support**: Works seamlessly with multiple test projects in
  the same workspace - just open a test file from any project
- **Build Configuration Setting**: New `csharpTestFilter.buildConfiguration`
  setting to choose between Debug and Release builds
- **Auto-Generate Debug Configuration**: Use "Add Configuration" in launch.json
  and select "C# Test Filter: Debug TUnit Test" to automatically add the
  configuration

### How It Works

The new `getDllPath` command:
1. Finds the nearest `.csproj` file for the current test file
2. Parses the `TargetFramework` and `AssemblyName` from the project file
3. Constructs the correct DLL path automatically

**Easy Setup**: Open launch.json, click "Add Configuration...", and select
"C# Test Filter: Debug TUnit Test" - no manual configuration needed!

## [1.0.5] - 2025-11-10

### Added

- Configuration setting `csharpTestFilter.showDebugOutput` to control debug
  output visibility (default: false)

### Fixed

- Output window reuse: Now uses a single output channel instead of creating
  new ones every time
- Suppressed error logging when language server fallback occurs (only shows
  when debug output is enabled)
- Removed unnecessary warning messages during transparent fallback to regex
  detection

### Changed

- Debug output is now disabled by default to reduce noise
- Language server fallback is now completely silent unless debug mode is
  enabled

## [1.0.4] - 2025-11-08

### Added

- Auto breakpoint at test method opening brace when no breakpoint exists

## [1.0.3] - 2025-11-08

### Added

- Last test method memory: Extension now remembers the last selected test
  method
- Automatic fallback to last test: When cursor is not on a test method, the
  extension automatically uses the last selected test filter

### Changed

- Restored standard activation events (C# language and .csproj files) for
  better performance

## [1.0.2] - 2025-11-08

### Fixed

- Production release with icon support

## [1.0.1] - 2025-11-08

### Added

- Initial release
- Automatic test scope detection using C# Language Server
- TUnit filter generation for debugging
- Launch.json integration via command variables
- Class-level and method-level test filtering
- Fallback regex-based detection
- Manual test scope inspection commands
- Symbol caching for performance

### Commands

- Get TUnit filter for current position
- Display test scope information  
- Copy filter to clipboard

