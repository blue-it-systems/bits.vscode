# Gengora - Live Code Generator for VS Code

**Real-time code generation with hot-reload support**

Gengora is a Visual Studio Code extension that watches your generator project and automatically recompiles and reruns it whenever you make changes. Perfect for iterative development of code generators, scaffolding tools, and template processors.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## Features

- 🔄 **Hot Reload**: Automatically recompile and restart your generator on file changes
- 🎯 **Intelligent Watching**: Only watches your generator project, ignoring generated output
- 📊 **Live Status**: Real-time status bar indicator showing generator state
- 🔧 **Configurable**: Customize folder paths, ignore patterns, and log levels
- 🚀 **Auto-Start**: Optional automatic startup on workspace open
- 📝 **Rich Logging**: Configurable log levels (error, warning, info, debug)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         VS Code Extension Host                       │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                     Gengora Extension                          │  │
│  │  • File Watchers (.cs, .csproj in generator folder)          │  │
│  │  • Configuration Management                                    │  │
│  │  • Status Bar & Output Channel                                │  │
│  └─────────────────┬─────────────────────────────────────────────┘  │
│                    │ LSP Protocol (stdio)                            │
│                    ↓                                                 │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │              LSP Server (GeneratorServer.dll)                 │  │
│  │  • Receives file change notifications                         │  │
│  │  • Runs `dotnet build` on Generator project                   │  │
│  │  • Copies built assembly to .vscode/.generator/out/           │  │
│  │  • Spawns generator process                                   │  │
│  │  • Forwards stdout/stderr and structured JSON events          │  │
│  └─────────────────┬─────────────────────────────────────────────┘  │
│                    │ Process spawn & stdio                           │
│                    ↓                                                 │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │           Your Generator (Gengora/Generator.dll)              │  │
│  │  • Emits JSON handshake: generator/hello                      │  │
│  │  • Generates code/files in workspace                          │  │
│  │  • Emits progress events: generator/generated                 │  │
│  │  • Writes to stdout/stderr for logging                        │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

File change detected → Extension notifies server → Server builds & runs
→ Generator executes → Output visible in VS Code
```

---

## Getting Started

### Prerequisites

- Visual Studio Code 1.80.0 or higher
- .NET 8.0 SDK or higher
- A C# generator project with a `.csproj` file

### Installation

1. Install the extension from the VS Code Marketplace
2. Or build from source:
   ```bash
   cd extension
   npm install
   npm run build
   ```

### Quick Start

1. **Create your generator project folder** (default name: `Gengora`)
   ```bash
   mkdir Gengora
   cd Gengora
   dotnet new console
   ```

2. **Open the folder in VS Code**
   ```bash
   code .
   ```

3. **The extension auto-activates** and starts watching your generator project

4. **Make changes** to your `.cs` files - Gengora automatically rebuilds and reruns

---

## Configuration

Access settings via `File > Preferences > Settings` → search for "Gengora"

### Available Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `gengora.generatorFolderPath` | string | `"Gengora"` | Folder name or path containing the generator `.csproj` project (case-sensitive) |
| `gengora.ignorePatterns` | array | `["**/bin/**", "**/obj/**", "**/.vscode/**", "**/GeneratedProject-*/**"]` | Glob patterns for files/folders to ignore within the generator folder |
| `gengora.autoRunOnCompileSuccess` | boolean | `true` | Automatically start the generator after successful compilation |
| `gengora.logLevel` | string | `"info"` | Logging verbosity: `error`, `warning`, `info`, or `debug` |
| `gengora.serverPath` | string | `""` | Optional explicit path to the LSP server DLL (leave empty for auto-discovery) |

### Example Configuration

```json
{
  "gengora.generatorFolderPath": "MyGenerator",
  "gengora.logLevel": "debug",
  "gengora.ignorePatterns": [
    "**/bin/**",
    "**/obj/**",
    "**/output/**"
  ],
  "gengora.autoRunOnCompileSuccess": true
}
```

---

## Commands

Access via Command Palette (`Cmd+Shift+P` / `Ctrl+Shift+P`)

- **Gengora: Start Generator** - Manually start the generator
- **Gengora: Stop Generator** - Stop the running generator
- **Gengora: Show Output** - Display the Gengora output channel

---

## Generator Project Requirements

Your generator project must:

1. ✅ Be a valid .NET project with a `.csproj` file
2. ✅ Be located in the configured folder (default: `Gengora`)
3. ✅ Build successfully with `dotnet build`

### Generator Protocol (Optional)

For enhanced integration, your generator can emit JSON messages to stdout:

#### Handshake Message
```json
{
  "method": "generator/hello",
  "params": {
    "capabilities": {
      "publishDiagnostics": false,
      "watchMode": false
    }
  }
}
```

#### Generation Complete Message
```json
{
  "method": "generator/generated",
  "params": {
    "project": "/path/to/generated/folder",
    "created": ["/path/to/file1.cs", "/path/to/file2.cs"]
  }
}
```

---

## Troubleshooting

### Generator not starting

1. Check the **Gengora output panel** for errors
2. Verify your generator folder contains a `.csproj` file
3. Ensure .NET SDK is installed: `dotnet --version`
4. Check the status bar for error messages

### Endless recompilation

1. Add generated output folders to `gengora.ignorePatterns`
2. Ensure `bin/` and `obj/` are ignored (default)
3. Set log level to `debug` to see which files trigger rebuilds

### Extension not activating

1. Check `Extensions` view for error badges
2. Open Developer Tools: `Help > Toggle Developer Tools`
3. Look for errors in the Console tab

---

## Development

### Building the Server

```bash
cd server
dotnet build
```

### Building the Extension

```bash
cd extension
npm install
npm run build
```

### Debugging

1. Open the repository in VS Code
2. Press `F5` to launch the Extension Development Host
3. The extension will activate in the new window

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## License

MIT License

Copyright (c) 2025 Blue IT Systems GmbH

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

## Author

**Saqib Javed**  
Blue IT Systems GmbH  
📧 saqib.javed@blue-it.com

---

## Changelog

### 1.0.0 (2025-11-23)

- ✨ Initial release
- 🔄 Hot reload support for generator projects
- 🎯 Configurable generator folder path
- 📝 Configurable log levels
- 🚫 Configurable ignore patterns
- 🚀 Auto-start on activation
- 📊 Status bar integration
