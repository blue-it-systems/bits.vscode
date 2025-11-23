# Gengora Extension - Testing Guide

## Quick Start (F5 Debugging)

### Option 1: Run Extension Only

1. Open the `bits.vscode` workspace in VS Code
2. Press `F5` or select **"Run Gengora Extension"** from the debug menu
3. This will:
   - Build the server (dotnet)
   - Build the Gengora sample generator (dotnet)
   - Build the extension (TypeScript)
   - Launch a new Extension Development Host window with Gengora loaded

### Option 2: Run Extension + Attach Debugger (Full Stack Debugging)

1. Select **"Run Gengora Extension + Attach Debugger"** from the debug menu
2. Press `F5`
3. When prompted, select the `BITS.Gengora.Server` or `dotnet` process
4. Now you can debug both:
   - TypeScript extension code (set breakpoints in `extension/src/`)
   - C# server code (set breakpoints in `server/`)

## Available Launch Configurations

### `Run Gengora Extension`

- Builds everything (server + generator + extension)
- Opens workspace: `${workspaceFolder}/gengora`
- Extension loads and starts LSP server automatically

### `Attach to Gengora Generator`

- Attach to a running generator process
- Use this to debug your actual generator code in `Gengora/`

### `Run Gengora Extension + Attach Debugger` (Compound)

- Combines both configurations
- Full-stack debugging experience

## Available Tasks

### Build Tasks

- **`dotnet: build gengora server`** - Build LSP server only
- **`dotnet: build gengora generator`** - Build sample generator only
- **`npm: build gengora extension`** - Build TypeScript extension only
- **`npm: watch gengora extension`** - Watch mode for extension development
- **`build-gengora-all`** - Build everything in sequence (used by F5)

### Running Tasks

1. Press `Cmd+Shift+B` to see build tasks
2. Select the task you want to run
3. Or run from Command Palette: `Tasks: Run Task`

## Development Workflow

### For Extension Development

1. Open `extension/src/extension.ts`
2. Run task: `npm: watch gengora extension` (background watch mode)
3. Press `F5` to launch
4. Make changes to TypeScript files
5. Reload window (`Cmd+R` in Extension Development Host) to test changes

### For Server Development

1. Open `server/` files
2. Set breakpoints in C# code
3. Press `F5` with "Run Gengora Extension + Attach Debugger"
4. Select the server process when prompted
5. Make changes to C# files
6. Rebuild with `dotnet: build gengora server` task
7. Restart extension to test changes

### For Generator Development

1. Open `Gengora/Program.cs`
2. Set breakpoints
3. Use "Attach to Gengora Generator" configuration
4. The generator runs when you execute `Gengora: Start Generator` command

## Testing the Extension

1. **Launch Extension Development Host** (F5)
2. **In the new window**, open Command Palette (`Cmd+Shift+P`)
3. Run command: **`Gengora: Start Generator`**
4. Check **Output** panel → **Gengora** channel for logs
5. Watch for status bar updates (bottom left)

## Troubleshooting

### Extension doesn't activate

- Check Output → Gengora channel for errors
- Ensure server built successfully: `dotnet build gengora/server`

### Can't attach debugger

- Make sure the server process is running
- Look for process named `BITS.Gengora.Server` or `dotnet`
- Try running the extension first, then attach

### Build errors

- Run `dotnet restore gengora/server`
- Run `npm install` in `gengora/extension`
- Check for .NET 8.0 SDK installation

## File Locations

- **Extension Code**: `gengora/extension/src/extension.ts`
- **Server Code**: `gengora/server/Program.cs`, `Services/`, `Handlers/`
- **Models**: `gengora/server/Models/`
- **Constants**: `gengora/server/Constants.cs`
- **Sample Generator**: `gengora/Gengora/Program.cs`

## Keyboard Shortcuts

- `F5` - Start Debugging
- `Shift+F5` - Stop Debugging
- `Cmd+Shift+F5` - Restart Debugging
- `Cmd+R` - Reload Extension Development Host window
- `Cmd+Shift+B` - Run Build Task
- `Cmd+Shift+P` - Command Palette

## Next Steps

1. Press `F5` to test the extension
2. Try running `Gengora: Start Generator` command
3. Check the Output panel for server logs
4. Set breakpoints and explore the code!
