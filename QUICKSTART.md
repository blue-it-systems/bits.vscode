# Quick Start Guide

Get started with the C# Test Filter Helper extension in 5 minutes!

## For Users

### Installation

1. Open VS Code
2. Press `Ctrl+Shift+X` (or `Cmd+Shift+X` on Mac) to open Extensions
3. Search for "C# Test Filter Helper"
4. Click "Install"

### Basic Usage

1. Open a C# test file (e.g., `CalculatorTests.cs`)
2. Place your cursor inside a test method
3. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
4. Type "C# Test Filter" and select:
   - **Get Current Test Scope**: Shows the detected scope
   - **Copy Test Filter to Clipboard**: Copies filter for use

### Example

Given this test class:

```csharp
public class CalculatorTests
{
    [Test]
    public void Add_ShouldReturnSum()
    {
        // Cursor here -> Filter: */CalculatorTests/Add_ShouldReturnSum
    }
}
```

### Integration with Debugging

Add to your `.vscode/launch.json`:

```json
{
  "name": "Run Test at Cursor",
  "type": "coreclr",
  "request": "launch",
  "program": "dotnet",
  "args": [
    "exec",
    "${workspaceFolder}/tests/YourProject.Tests/bin/Debug/net8.0/YourProject.Tests.dll",
    "--treenode-filter",
    "/*/*/${command:csharp-test-filter.getTestFilterForInput}"
  ]
}
```

Now press `F5` to debug the test at your cursor!

## For Developers

### Setup

```bash
# Clone the repository
git clone https://github.com/blue-it-systems/bits.vscode.git
cd bits.vscode

# Install dependencies
npm install

# Compile
npm run compile
```

### Test Locally

```bash
# Open in VS Code
code .

# Press F5 to launch Extension Development Host
# Test the extension in the new window
```

### Build Package

```bash
npm run package
# Creates: csharp-test-filter-0.0.1.vsix
```

### Publish

See [PUBLISHING.md](PUBLISHING.md) for detailed publishing instructions.

## Next Steps

- Read the full [README.md](README.md) for detailed documentation
- Check [TESTING.md](TESTING.md) for testing strategies
- Review [PUBLISHING.md](PUBLISHING.md) for marketplace publishing

## Support

- Issues: <https://github.com/blue-it-systems/bits.vscode/issues>
- Discussions: <https://github.com/blue-it-systems/bits.vscode/discussions>

## Tips

- Use keyboard shortcuts for quick access to commands
- Combine with VS Code tasks for even faster workflows
- Check the Output panel (Extension Host) for debugging
