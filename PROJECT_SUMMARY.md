# Project Summary

## Overview

**C# Test Filter Helper** is a VS Code extension that automatically detects the C# test scope (assembly, class, and method) at your cursor position, designed specifically for TUnit debugging workflows.

## Key Features

- ✅ Auto-detect test scope from cursor position
- ✅ Copy test filter to clipboard
- ✅ Seamless integration with launch.json
- ✅ Support for TUnit test filter format
- ✅ Automatic method/class detection
- ✅ Command palette integration
- ✅ Silent mode for input variables

## Project Structure

```text
bits.vscode/
├── .github/
│   └── workflows/
│       ├── ci.yml                 # CI pipeline
│       └── publish.yml            # Publishing workflow
├── .vscode/
│   ├── launch.json                # Debug configuration
│   └── tasks.json                 # Build tasks
├── examples/
│   └── launch.json                # Example launch.json for users
├── src/
│   └── extension.ts               # Main extension code
├── out/                           # Compiled JavaScript (gitignored)
├── node_modules/                  # Dependencies (gitignored)
├── .eslintrc.js                   # ESLint configuration
├── .gitignore                     # Git ignore rules
├── CHANGELOG.md                   # Version history
├── LICENSE                        # MIT License
├── package.json                   # Extension manifest
├── PUBLISHING.md                  # Publishing guide
├── QUICKSTART.md                  # Quick start guide
├── README.md                      # Main documentation
├── TESTING.md                     # Testing guide
└── tsconfig.json                  # TypeScript configuration
```

## Technologies Used

- **TypeScript**: Main programming language
- **VS Code Extension API**: Core extension framework
- **Node.js**: Runtime environment
- **ESLint**: Code linting
- **GitHub Actions**: CI/CD pipeline
- **vsce**: VS Code extension packaging tool

## Commands

| Command | Description |
|---------|-------------|
| `csharp-test-filter.getCurrentTestScope` | Display the detected test scope |
| `csharp-test-filter.copyTestFilter` | Copy test filter to clipboard |
| `csharp-test-filter.getTestFilterForInput` | Get filter for launch.json (silent) |

## Usage Examples

### Manual Command

1. Open C# test file
2. Place cursor in test method
3. Run: `C# Test Filter: Get Current Test Scope`
4. Result: `*/ClassName/MethodName`

### Automatic in launch.json

```json
{
  "name": "Debug Test",
  "type": "coreclr",
  "request": "launch",
  "program": "dotnet",
  "args": [
    "exec",
    "${workspaceFolder}/tests/MyTests.dll",
    "--treenode-filter",
    "/*/*/${command:csharp-test-filter.getTestFilterForInput}"
  ]
}
```

## Development Workflow

### Local Testing

```bash
# Install dependencies
npm install

# Compile TypeScript
npm run compile

# Watch mode (auto-compile)
npm run watch

# Launch Extension Development Host
# Press F5 in VS Code
```

### Building

```bash
# Compile
npm run compile

# Lint
npm run lint

# Package
npm run package
```

### Publishing

#### Automated (GitHub Actions)

1. Update `package.json` version
2. Update `CHANGELOG.md`
3. Commit and push
4. Create GitHub release with tag (e.g., `v0.1.0`)
5. GitHub Actions automatically publishes to marketplace

#### Manual

```bash
vsce login blue-it-systems
vsce publish
```

## CI/CD Pipeline

### Workflows

1. **CI (`ci.yml`)**:
   - Triggers: Push/PR to main
   - Steps: Build, lint, package
   - Artifact: `.vsix` file

2. **Publish (`publish.yml`)**:
   - Triggers: GitHub release or manual dispatch
   - Steps: Build, lint, package, publish to marketplace
   - Requires: `VSCE_PAT` secret

## Test Filter Format

The extension generates filters in TUnit format:

- **Class level**: `*/ClassName/*`
- **Method level**: `*/ClassName/MethodName`

Pattern: `Assembly/Namespace.ClassName/MethodName`

For debugging: `/*/*/[filter]`

## Algorithm Overview

### Scope Detection

1. Get active editor and cursor position
2. Verify file is C# (`.cs`)
3. Parse file content into lines
4. Search backward from cursor for:
   - Class declaration: `class ClassName`
   - Method declaration: `ReturnType MethodName(`
5. Use brace counting to determine method scope
6. Build filter: `*/ClassName/MethodName`

### Brace Counting

- Track opening `{` and closing `}` braces
- Count backwards from cursor
- If in a method scope, braces will be balanced
- Stop at class declaration boundary

## Configuration

### Extension Settings

- `csharpTestFilter.showNotifications`: Show/hide notification messages (default: `true`)

### Package.json Key Sections

- **Engines**: VS Code ^1.85.0
- **Categories**: Testing, Debuggers
- **Activation**: On command execution
- **Main**: `./out/extension.js`

## Requirements

- VS Code 1.85.0 or higher
- Node.js 20.x or higher (for development)
- C# language support (recommended)

## Future Enhancements

Potential features for future versions:

- [ ] Support for nested classes
- [ ] Namespace detection
- [ ] Test framework auto-detection (NUnit, xUnit, MSTest)
- [ ] Multi-cursor support
- [ ] Test explorer integration
- [ ] Code lens integration (run test button)
- [ ] Configuration for custom filter formats
- [ ] Inline test results display

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) file

## Links

- **Repository**: <https://github.com/blue-it-systems/bits.vscode>
- **Issues**: <https://github.com/blue-it-systems/bits.vscode/issues>
- **Marketplace**: (Will be available after first publish)

## Contact

For questions or support, please open an issue on GitHub.

---

**Created**: November 8, 2025  
**Version**: 0.0.1  
**Status**: Ready for testing and publishing
