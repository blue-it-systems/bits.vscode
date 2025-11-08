# TUnit Test Debugging Guide

## Problem Fixed
TUnit tests were hanging during debug because the original configuration used `dotnet test` which doesn't work properly with TUnit's execution model. TUnit generates its own test runner executable and needs to be run directly.

## Solution
The launch.json has been updated to run the TUnit test executable directly instead of through `dotnet test`.

## How to Debug Tests

### Option 1: Debug All Tests
1. Set breakpoints in your test methods
2. Press `F5` or select **"Debug All TUnit Tests"** from the debug dropdown
3. All tests will run and stop at your breakpoints

### Option 2: Debug Specific Test
1. Set breakpoints in your test method
2. Select **"Debug Specific TUnit Test"** from the debug dropdown
3. Enter the filter pattern when prompted (see filter formats below)
4. The debugger will run only the filtered test(s)

### Option 3: Debug Calculator Tests Only
1. Set breakpoints in Calculator test methods
2. Select **"Debug Calculator Tests"** from the debug dropdown
3. Only Calculator tests will run

## TUnit Filter Formats

TUnit uses the `--treenode-filter` option with these patterns:

### Run a Specific Test
```
/*/*/ClassName/MethodName
```
Example: `/*/*/CalculatorTests/Add_ShouldReturnCorrectSum`

### Run All Tests in a Class
```
/*/*/ClassName/*
```
Example: `/*/*/CalculatorTests/*`

### Run Multiple Patterns (OR)
```
Pattern1|Pattern2
```
Example: `/*/*/CalculatorTests/Add*|/*/*/StringTests/*`

### Filter by Properties
```
/*/*/*/*[PropertyName=Value]
```
Example: `/*/*/*/*[Category=Integration]`

## Command Line Testing

You can also run tests from the terminal:

```powershell
# Run all tests
./bin/Debug/net10.0/SampleTests

# Run specific test
./bin/Debug/net10.0/SampleTests --treenode-filter "/*/*/CalculatorTests/Add_ShouldReturnCorrectSum"

# Run all tests in a class
./bin/Debug/net10.0/SampleTests --treenode-filter "/*/*/CalculatorTests/*"

# List all available tests
./bin/Debug/net10.0/SampleTests --list-tests
```

## Key Points

1. **TUnit runs differently** than xUnit/NUnit - it generates a self-contained executable
2. **Always build first** before debugging (the preLaunchTask handles this)
3. **Filter format matters** - use `/*/*/ClassName/MethodName` not just `MethodName`
4. **Breakpoints work** in both test methods and the code being tested

## Troubleshooting

If debugging still doesn't work:

1. Clean and rebuild:
   ```powershell
   dotnet clean
   dotnet build
   ```

2. Check that the executable exists:
   ```powershell
   ls bin/Debug/net10.0/SampleTests
   ```

3. Try running tests from command line first to verify they work

4. Make sure you're using the correct filter format (see examples above)
