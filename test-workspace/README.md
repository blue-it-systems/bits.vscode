# Sample TUnit Test Workspace

This workspace is automatically loaded when you press F5 to debug the extension.

## Quick Start

1. **Press F5** - The extension is now active in this workspace
2. **Open any test file** (CalculatorTests.cs or StringTests.cs)
3. **Place cursor inside a test method**
4. **Test the extension**:
   - `Ctrl+Shift+P` → "C# Test Filter: Get Current Test Scope"
   - Or `Ctrl+Shift+P` → "C# Test Filter: Copy Test Filter to Clipboard"

## Example Usage

### 1. View Current Test Scope

1. Open `CalculatorTests.cs`
2. Place cursor inside `Add_ShouldReturnCorrectSum` method
3. Press `Ctrl+Shift+P` → "C# Test Filter: Get Current Test Scope"
4. You should see:
   ```
   Assembly: SampleTests
   Class: CalculatorTests
   Method: Add_ShouldReturnCorrectSum
   Filter: */CalculatorTests/Add_ShouldReturnCorrectSum
   ```

### 2. Copy Test Filter

1. Place cursor in any test method
2. Press `Ctrl+Shift+P` → "C# Test Filter: Copy Test Filter to Clipboard"
3. The filter is now in your clipboard!

### 3. Debug Specific Test

1. Place cursor in a test method
2. Press `F5` to use the "Debug Current Test (Auto-detect)" configuration
3. The extension automatically provides the filter to the debugger

## Test Files

- **CalculatorTests.cs** - Basic math operation tests
- **StringTests.cs** - String manipulation tests

## Project Structure

```
test-workspace/
├── SampleTests.csproj    # TUnit test project
├── CalculatorTests.cs    # Calculator test class
├── StringTests.cs        # String test class
├── .vscode/
│   ├── launch.json       # Debug configurations
│   └── tasks.json        # Build tasks
└── README.md            # This file
```

## Notes

- This workspace uses TUnit as the test framework
- The extension auto-detects test scope from cursor position
- No method in scope? The filter will be `*/ClassName/*`
