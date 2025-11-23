# Examples

This directory contains practical examples for using the C# Test Filter
Helper extension.

## Files

### `launch.json`

Example VS Code debug configurations showing:

- **Auto-Detect**: Automatically detect test from cursor position
- **Manual Filter**: Prompt for manual test filter input

Copy the relevant configuration to your project's
`.vscode/launch.json` file.

### `TestExample.cs`

Sample TUnit test class demonstrating:

- Method-level filtering (run single test)
- Class-level filtering (run all tests in class)
- Comments explaining filter output at each cursor position

## Quick Start

1. Copy `launch.json` configurations to your project
2. Update the `program` path to match your test DLL location
3. Open any C# test file
4. Place cursor in a test method
5. Press **F5** to debug

The extension will automatically detect your test scope and generate
the appropriate TUnit filter.

## Command Variables for launch.json

The primary use case is with debug configurations:

```json
{
  "name": "Debug TUnit Test (Auto)",
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
  "console": "integratedTerminal"
}
```

The `${command:csharp-test-filter.getFilter}` variable automatically
resolves to the test filter based on your cursor position.

## Filter Output Format

### Method Level (cursor inside a test method)

```text
/*/YourNamespace/YourTestClass/YourTestMethod
```

### Class Level (cursor on class name or outside methods)

```text
/*/YourNamespace/YourTestClass/*
```

## Available Commands

### 1. Get Filter String

```typescript
const filter = await vscode.commands.executeCommand(
  'csharp-test-filter.getFilter'
);
// Returns: "/*/Namespace/ClassName/MethodName"
```

### 2. Get Test Scope Information

```typescript
const info = await vscode.commands.executeCommand(
  'csharp-test-filter.getCurrentTestScope'
);
// Shows notification with test scope details
```

### 3. Copy Filter to Clipboard

```typescript
await vscode.commands.executeCommand(
  'csharp-test-filter.copyTestFilter'
);
// Copies filter string to clipboard
```

## Programmatic API Access

For programmatic access from another extension:

```typescript
const scopeInfo = await vscode.commands.executeCommand(
  'csharp-test-filter.getTestFilterForInput'
);

// Returns object:
// {
//   assembly: string,
//   className: string,
//   methodName?: string,
//   filter: string
// }
```

## Language Server Integration

The extension uses VS Code's C# language server (via C# Dev Kit or
OmniSharp) for accurate symbol detection. If the language server is
unavailable, it falls back to regex-based detection.

## Additional Resources

- [Main README](../README.md)
- [Changelog](../CHANGELOG.md)
- [GitHub Repository](https://github.com/blue-it-systems/bits.vscode)

