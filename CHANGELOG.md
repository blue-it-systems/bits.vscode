# Change Log

All notable changes to the "C# Test Filter Helper" extension will be documented in this file.

## [0.0.1] - 2025-11-08

### Added

- Initial release
- Auto-detect C# test scope (class and method) from cursor position
- Command: `C# Test Filter: Get Current Test Scope`
- Command: `C# Test Filter: Copy Test Filter to Clipboard`
- Integration with launch.json via `${command:csharp-test-filter.getTestFilterForInput}`
- Support for TUnit test filter format: `*/ClassName/MethodName`
- Automatic detection when cursor is in class vs method scope
- Configuration setting for notification messages

### Features

- Parse C# syntax to identify class declarations
- Detect method scope using brace counting algorithm
- Silent mode for input variable integration
- Copy filter directly to clipboard

### Developer Tools

- TypeScript-based extension architecture
- ESLint configuration for code quality
- GitHub Actions CI/CD pipeline
- Local testing setup with launch.json and tasks.json
- Automated marketplace publishing workflow
