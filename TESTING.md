# Local Testing Guide

This guide will help you test the extension locally before publishing.

## Prerequisites

- Node.js 20.x or higher
- VS Code 1.85.0 or higher
- Git

## Setup

1. **Install dependencies**:

   ```bash
   npm install
   ```

2. **Compile the extension**:

   ```bash
   npm run compile
   ```

## Testing the Extension Locally

### Method 1: Run in Extension Development Host (Recommended)

1. Open this project in VS Code
2. Press `F5` or go to `Run > Start Debugging`
3. A new VS Code window will open with the extension loaded
4. In the new window:
   - Open a C# test project
   - Open a test file (e.g., `MyTests.cs`)
   - Place your cursor inside a test method
   - Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
   - Type "C# Test Filter" and select a command

### Method 2: Install from VSIX

1. **Package the extension**:

   ```bash
   npm run package
   ```

   This creates a `.vsix` file (e.g., `csharp-test-filter-0.0.1.vsix`)

2. **Install the VSIX**:
   - Open VS Code
   - Go to Extensions view (`Ctrl+Shift+X`)
   - Click the `...` menu (top-right)
   - Select "Install from VSIX..."
   - Choose the generated `.vsix` file

3. **Test the installed extension**:
   - Reload VS Code
   - Open a C# test file
   - Use the extension commands

### Method 3: Watch Mode for Development

For continuous development with auto-compilation:

1. **Start watch mode**:

   ```bash
   npm run watch
   ```

2. **In another terminal, launch the Extension Development Host**:
   - Press `F5` in VS Code
   - Or use `Run > Start Debugging`

3. **Make changes**:
   - Edit `src/extension.ts`
   - TypeScript will automatically recompile
   - Press `Ctrl+R` (or `Cmd+R` on Mac) in the Extension Development Host to reload

## Testing with a Real C# Project

1. Create or open a C# test project with TUnit tests
2. Create a test file:

   ```csharp
   namespace MyProject.Tests
   {
       public class CalculatorTests
       {
           [Test]
           public void Add_ShouldReturnSum()
           {
               // Test code
           }

           [Test]
           public void Subtract_ShouldReturnDifference()
           {
               // Test code
           }
       }
   }
   ```

3. Test the extension:
   - Place cursor inside `Add_ShouldReturnSum` method
   - Run command: "C# Test Filter: Get Current Test Scope"
   - Expected output: `*/CalculatorTests/Add_ShouldReturnSum`

   - Place cursor inside the class but outside methods
   - Run command: "C# Test Filter: Get Current Test Scope"
   - Expected output: `*/CalculatorTests/*`

## Testing with launch.json Integration

1. Create a `.vscode/launch.json` in your test project:

   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": "Run Current Test",
         "type": "coreclr",
         "request": "launch",
         "program": "dotnet",
         "args": [
           "exec",
           "${workspaceFolder}/bin/Debug/net8.0/MyProject.Tests.dll",
           "--treenode-filter",
           "/*/*/${command:csharp-test-filter.getTestFilterForInput}"
         ],
         "cwd": "${workspaceFolder}",
         "console": "internalConsole"
       }
     ]
   }
   ```

2. Place cursor in a test method
3. Press `F5` to start debugging
4. The extension should automatically provide the test filter

## Debugging the Extension

### Debug Extension Code

1. Set breakpoints in `src/extension.ts`
2. Press `F5` to launch Extension Development Host
3. Use the extension in the new window
4. Breakpoints will be hit in your main VS Code window

### View Extension Logs

1. In the Extension Development Host window
2. Go to `View > Output`
3. Select "Extension Host" from dropdown
4. Look for log messages from the extension

### Debug Issues

Common issues and solutions:

- **Extension not activating**: Check the Output panel for errors
- **Commands not appearing**: Verify `package.json` contributions
- **TypeScript errors**: Run `npm run compile` to see compilation errors
- **Extension not detecting scope**: Add console.log statements and check Output

## Running Linter

```bash
npm run lint
```

Fix auto-fixable issues:

```bash
npx eslint src --ext ts --fix
```

## Building for Production

1. **Update version** in `package.json`
2. **Compile and package**:

   ```bash
   npm run compile
   npm run package
   ```

3. **Test the VSIX** before publishing
4. **Publish** (see main README for publishing steps)

## Uninstalling Local Extension

If you installed from VSIX:

1. Go to Extensions view
2. Find "C# Test Filter Helper"
3. Click the gear icon
4. Select "Uninstall"

## Next Steps

After local testing is successful:

1. Update `CHANGELOG.md`
2. Commit your changes
3. Create a GitHub release
4. Automatic publishing will trigger via GitHub Actions

For more details, see the main [README.md](README.md).
