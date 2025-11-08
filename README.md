# C# Test Filter Helper

Automatically detect C# test scope (assembly, class, and method) at your
cursor position for seamless TUnit debugging in VS Code.

## Features

- **Automatic Test Scope Detection**: Uses C# Language Server for accurate
  symbol detection
- **TUnit Filter Generation**: Generates proper TUnit `--treenode-filter`
  format
- **Debug Integration**: Works seamlessly with VS Code's launch.json
- **Class & Method Level**: Run entire test classes or individual methods
- **Fallback Support**: Works even without C# Dev Kit installed

## Quick Start

### 1. Add to Your Debug Configuration

Add this to your `.vscode/launch.json`:

```json
{
  "name": "Debug TUnit Test",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "build",
  "program": "dotnet",
  "args": [
    "exec",
    "${workspaceFolder}/bin/Debug/net9.0/YourProject.dll",
    "--treenode-filter",
    "${command:csharp-test-filter.getFilter}"
  ],
  "cwd": "${workspaceFolder}",
  "console": "integratedTerminal",
  "stopAtEntry": false
}
```

### 2. Set Breakpoints & Debug

1. Open any C# test file
2. Place cursor in a test method (or on class name for all tests)
3. Set breakpoints
4. Press **F5** to debug

The extension automatically detects your test scope and runs only the
relevant tests.

## Commands

- **C# Test Filter: Get Current Test Scope** - Shows detected test
  information
- **C# Test Filter: Copy Test Filter to Clipboard** - Copies the filter
  string

Access via Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)

## Filter Format

The extension generates TUnit-compatible filters:

- **Method Level**: `/*/Namespace/ClassName/MethodName`
- **Class Level**: `/*/Namespace/ClassName/*`

## Requirements

- VS Code 1.85.0 or higher
- .NET SDK with C# project
- Recommended: C# Dev Kit extension for best results

## Extension Settings

- `csharpTestFilter.showNotifications`: Show notification messages
  (default: `true`)

## Development

### Building from Source

```bash
npm install
npm run compile
```

### Debugging the Extension

1. Open this project in VS Code
2. Press **F5** to start Extension Development Host
3. The test workspace will automatically load

### Project Structure

```
├── src/
│   └── extension.ts       # Main extension logic
├── examples/              # Usage examples and configurations
├── test-workspace/        # Example TUnit project for testing
├── package.json           # Extension manifest
└── tsconfig.json          # TypeScript configuration
```

## Examples

See the [examples](examples/) directory for:

- Complete `launch.json` configurations
- Sample test files with TUnit
- API usage documentation

## Support & Feedback

- [Report Issues](https://github.com/blue-it-systems/bits.vscode/issues)
- [View Source](https://github.com/blue-it-systems/bits.vscode)
- Email: info@it-blue.com

## About

Developed by [Blue IT Systems GmbH](https://it-blue.com) - A German software
development company specializing in custom solutions, DevOps, and digital
transformation.

**Headquarters**: Odenwaldstrasse 16, D-63526 Erlensee, Germany

## License

MIT License - see [LICENSE](LICENSE) file for details.
