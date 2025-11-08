# Changelog

All notable changes to this project will be documented in this file.

## [1.0.3] - 2025-11-08

### Added

- Last test method memory: Extension now remembers the last selected test
  method
- Automatic fallback to last test: When cursor is not on a test method, the
  extension automatically uses the last selected test filter
- Auto-breakpoint on method entry: Automatically adds a breakpoint at the
  start of a test method if no breakpoints are found in that method

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

