# Extension Development & Testing Guide

## Quick Start - Press F5!

Just press **F5** in VS Code. The Extension Development Host will automatically open with a pre-configured TUnit test workspace!

## What Happens When You Press F5

1. ✅ Extension compiles automatically (via preLaunchTask)
2. ✅ New VS Code window opens (Extension Development Host)
3. ✅ Test workspace loads automatically (`test-workspace/` folder)
4. ✅ Sample C# TUnit tests are ready to use
5. ✅ Your extension is active and ready to test!

## Testing the Extension

### In the Extension Development Host window:

1. **Open a test file**:
   - `CalculatorTests.cs` - Math operations tests
   - `StringTests.cs` - String manipulation tests

2. **Place cursor inside a test method**:
   ```csharp
   [Test]
   public async Task Add_ShouldReturnCorrectSum()  // ← Put cursor here
   {
       var result = calculator.Add(5, 3);
   }
   ```

3. **Try the commands**:
   - Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
   - Type "C# Test Filter"
   - Choose:
     - **Get Current Test Scope** - Shows all detected info
     - **Copy Test Filter to Clipboard** - Copies filter

4. **Expected output**:
   ```
   Assembly: SampleTests
   Class: CalculatorTests
   Method: Add_ShouldReturnCorrectSum
   Filter: */CalculatorTests/Add_ShouldReturnCorrectSum
   ```

## Test Workspace Structure

```
test-workspace/
├── SampleTests.csproj           # TUnit test project (net8.0)
├── CalculatorTests.cs           # Calculator tests (4 test methods)
├── StringTests.cs               # String tests (3 test methods)
├── .vscode/
│   ├── launch.json              # Debug configurations with extension integration
│   └── tasks.json               # Build tasks
└── README.md                    # Workspace guide
```

## Testing Different Scenarios

### Scenario 1: Method in Scope
1. Open `CalculatorTests.cs`
2. Place cursor inside `Add_ShouldReturnCorrectSum` method
3. Run "Get Current Test Scope"
4. ✅ Should show: `*/CalculatorTests/Add_ShouldReturnCorrectSum`

### Scenario 2: Class Scope (No Method)
1. Open `StringTests.cs`
2. Place cursor in the class body but **outside** any method
3. Run "Get Current Test Scope"
4. ✅ Should show: `*/StringTests/*` with message "No method in scope"

### Scenario 3: Different Test Methods
1. Try cursor in different methods:
   - `Subtract_ShouldReturnCorrectDifference`
   - `Multiply_ShouldReturnCorrectProduct`
   - `String_ToUpper_ShouldConvertToUpperCase`
2. ✅ Each should detect the correct method name

### Scenario 4: Copy to Clipboard
1. Place cursor in any test method
2. Run "Copy Test Filter to Clipboard"
3. ✅ Paste somewhere to verify it copied correctly

## Debugging Tests with the Extension

The test workspace includes launch configurations that use your extension:

1. In the Extension Development Host, press `F5` again
2. Select "Debug Current Test (Auto-detect)"
3. ✅ Your extension automatically provides the test filter!

## Modifying Test Workspace

You can freely edit the test files:

- Add new test methods
- Create new test classes
- Modify existing tests
- Test edge cases (nested classes, different namespaces, etc.)

Changes are **not** auto-committed - they're for local testing only.

## Build Artifacts

The following are gitignored (won't be committed):
- `test-workspace/bin/`
- `test-workspace/obj/`

The source files ARE committed so other developers get the same test setup.

## Troubleshooting

### Extension doesn't activate
- Check Output panel: View → Output → "Extension Host"
- Make sure the extension compiled (check preLaunchTask)

### Test workspace doesn't open
- The launch.json should have: `"${workspaceFolder}/test-workspace"`
- Check that folder exists

### Can't find test scope
- Make sure file is C# (`.cs` extension)
- Verify cursor is inside a class
- Check Output panel for errors

### Commands not appearing
- Extension might not be activated
- Try opening a `.cs` file first
- Check if extension is running in the Extensions view

## Manual Testing Checklist

- [ ] Extension activates on C# file open
- [ ] Detects class name correctly
- [ ] Detects method name when cursor is in method
- [ ] Shows "No method in scope" when outside methods
- [ ] Copy to clipboard works
- [ ] Filter format is correct: `*/ClassName/MethodName`
- [ ] Silent mode works (no messages in launch.json integration)
- [ ] Works with different test classes
- [ ] Works with different method names

## Next Steps

After testing locally:
1. Make changes to `src/extension.ts`
2. The watch task auto-compiles (or run `npm run compile`)
3. Reload Extension Development Host: `Ctrl+R` (or `Cmd+R`)
4. Test again!

## Tips

- **Keep Extension Development Host open** while developing
- **Use watch task** (`npm run watch`) for auto-compilation
- **Reload instead of restart** (`Ctrl+R`) - much faster!
- **Check Output panel** for extension logs and errors
- **Test edge cases** - try weird cursor positions, nested classes, etc.

---

**Ready to go! Just press F5!** 🚀
