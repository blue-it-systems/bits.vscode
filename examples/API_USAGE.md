# Example: Using the Extension API

If you want to programmatically access the test scope information, you can call the commands from your own extension or script.

## Structured Return Object

The extension now returns a structured object with all components:

```typescript
interface TestScopeInfo {
    assembly: string;      // e.g., "BITS.Terminal.Tests"
    className: string;     // e.g., "CalculatorTests"
    methodName?: string;   // e.g., "Add_ShouldReturnSum" (optional)
    filter: string;        // e.g., "*/CalculatorTests/Add_ShouldReturnSum"
}
```

## Example Usage in Code

```typescript
// Get test scope information
const testScopeInfo = await vscode.commands.executeCommand('csharp-test-filter.getTestFilterForInput');

// testScopeInfo will be a string (the filter) for backward compatibility
// For the full object, use the command directly in your extension

// Access individual components:
console.log(`Assembly: ${testScopeInfo.assembly}`);
console.log(`Class: ${testScopeInfo.className}`);
console.log(`Method: ${testScopeInfo.methodName || 'none'}`);
console.log(`Full Filter: ${testScopeInfo.filter}`);
```

## Display Format

When you use the "Get Current Test Scope" command, you'll see:

```text
Test Scope:
Assembly: BITS.Terminal.Tests
Class: CalculatorTests
Method: Add_ShouldReturnSum
Filter: */CalculatorTests/Add_ShouldReturnSum
```

Or if cursor is in class but not in a method:

```text
Test Scope:
Assembly: BITS.Terminal.Tests
Class: CalculatorTests
Method: (none - class scope)
Filter: */CalculatorTests/*
```

## Integration with launch.json

The filter string is automatically used:

```json
{
  "args": [
    "exec",
    "${workspaceFolder}/tests/BITS.Terminal.Tests/bin/Debug/net10.0/BITS.Terminal.Tests.dll",
    "--treenode-filter",
    "/*/*/${command:csharp-test-filter.getTestFilterForInput}"
  ]
}
```

This will resolve to something like:
```text
/*/*/CalculatorTests/Add_ShouldReturnSum
```
