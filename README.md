# C# Test Filter Helper

A VS Code extension that automatically detects the C# test scope (assembly, class, and method) at your cursor position, designed specifically for TUnit debugging workflows.

## Features

- 🎯 **Auto-detect test scope** from cursor position
- 📋 **Copy test filter** to clipboard with one command
- 🔧 **Seamless integration** with launch.json configurations
- ⚡ **Quick commands** for manual and automatic test filtering
- 🚀 **Optimized for TUnit** debugging workflows

## Usage

### Manual Commands

1. **Get Current Test Scope**: 
   - Place your cursor inside a test method or class
   - Run command: `C# Test Filter: Get Current Test Scope`
   - The extension will show the detected scope

2. **Copy Test Filter to Clipboard**:
   - Place your cursor inside a test method or class
   - Run command: `C# Test Filter: Copy Test Filter to Clipboard`
   - The filter will be copied and ready to paste

### Automatic Integration with launch.json

The extension can automatically provide the test filter to your debug configuration. Here's how to set it up:

#### Option 1: Using Auto-Detection with Individual Commands

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (TUnit - Auto Detect)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "dotnet",
      "args": [
        "exec",
        "${workspaceFolder}/tests/YourProject.Tests/bin/Debug/net8.0/YourProject.Tests.dll",
        "--treenode-filter",
        "${command:csharp-test-filter.getFilter}"
      ],
      "cwd": "${workspaceFolder}",
      "console": "internalConsole"
    }
  ]
}
```

**Available Commands:**
- `${command:csharp-test-filter.getFilter}` - Complete TUnit filter path
- `${command:csharp-test-filter.getClassName}` - Just the class name
- `${command:csharp-test-filter.getMethodName}` - Just the method name

#### Option 2: Using Prompt with Manual Entry

```json
{
  "version": "0.2.0",
  "inputs": [
    {
      "id": "testFilter",
      "type": "promptString",
      "description": "Test filter (or use auto-detect command first)",
      "default": ""
    }
  ],
  "configurations": [
    {
      "name": ".NET Core Launch (TUnit Tests)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "dotnet",
      "args": [
        "exec",
        "${workspaceFolder}/tests/YourProject.Tests/bin/Debug/net8.0/YourProject.Tests.dll",
        "--treenode-filter",
        "/*/*/${input:testFilter}"
      ],
      "cwd": "${workspaceFolder}",
      "console": "internalConsole"
    }
  ]
}
```

### Test Filter Format

The extension generates test filters in the TUnit format:

- **Class-level filter**: `*/ClassName/*` (when cursor is in class but not in a method)
- **Method-level filter**: `*/ClassName/MethodName` (when cursor is inside a method)

Example:
```
*/MyTestClass/MyTestMethod
*/CalculatorTests/AddNumbers_ShouldReturnSum
*/UserServiceTests/*
```

## Requirements

- VS Code 1.85.0 or higher
- C# language support (C# extension recommended)

## Extension Settings

This extension contributes the following settings:

- `csharpTestFilter.showNotifications`: Enable/disable notification messages (default: `true`)

## Local Testing

### Prerequisites

1. Install Node.js (v20 or higher)
2. Install dependencies:
   ```bash
   npm install
   ```

### Run Extension in Development Mode

1. Open this project in VS Code
2. Press `F5` or select `Run > Start Debugging`
3. A new VS Code window (Extension Development Host) will open
4. Open a C# test file in the new window
5. Test the extension commands

### Build the Extension

```bash
# Compile TypeScript
npm run compile

# Watch for changes (auto-compile)
npm run watch

# Lint code
npm run lint

# Package extension as .vsix
npm run package
```

### Install Locally

After packaging, you can install the `.vsix` file:

1. Build the package:
   ```bash
   npm run package
   ```
2. In VS Code, go to Extensions view
3. Click the `...` menu at the top
4. Select "Install from VSIX..."
5. Choose the generated `.vsix` file

## Publishing to Marketplace

### Prerequisites

1. Create a [Visual Studio Marketplace](https://marketplace.visualstudio.com/) account
2. Create a [Personal Access Token (PAT)](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#get-a-personal-access-token)
   - Go to https://dev.azure.com/
   - Create a new token with **Marketplace > Manage** scope
3. Add the PAT as a GitHub secret named `VSCE_PAT`

### Manual Publishing

```bash
# Login to marketplace (first time only)
npx vsce login blue-it-systems

# Package and publish
npx vsce publish
```

### Automated Publishing via GitHub Actions

The repository includes two GitHub Actions workflows:

#### 1. CI Workflow (`.github/workflows/ci.yml`)
- Runs on every push and PR to `main`
- Builds and tests the extension
- Creates a `.vsix` artifact for download

#### 2. Publish Workflow (`.github/workflows/publish.yml`)
- Triggers on GitHub releases or manual dispatch
- Builds, tests, and publishes to VS Code Marketplace
- Requires `VSCE_PAT` secret to be configured

**To publish a new version:**

1. Update version in `package.json`:
   ```json
   {
     "version": "0.1.0"
   }
   ```

2. Commit and push changes:
   ```bash
   git add package.json
   git commit -m "Bump version to 0.1.0"
   git push
   ```

3. Create a new release on GitHub:
   - Go to Releases > "Draft a new release"
   - Create a new tag (e.g., `v0.1.0`)
   - Publish the release
   - The GitHub Action will automatically publish to the marketplace

**Or trigger manually:**
- Go to Actions > Publish VS Code Extension
- Click "Run workflow"
- The extension will be published automatically

## Architecture

### Core Components

- **extension.ts**: Main extension logic
  - `getTestScope()`: Analyzes cursor position and extracts test context
  - `analyzeTestScope()`: Parses C# syntax to find class/method names
  - `findClassName()`: Searches backward for class declarations
  - `findMethodName()`: Detects method scope using brace counting

### How It Works

1. Gets the active text editor and cursor position
2. Verifies the file is C# (`.cs`)
3. Parses the file content line by line
4. Searches backward from cursor to find:
   - Class declaration
   - Method declaration (if cursor is inside a method)
5. Builds the TUnit filter format: `*/ClassName/MethodName`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

MIT

## Support

For issues, questions, or feature requests, please visit:
https://github.com/blue-it-systems/bits.vscode/issues
