# Gengora - Live Code Generator for VS Code

## Real-time code generation with hot-reload support

Gengora is a Visual Studio Code extension that watches your generator project and automatically recompiles and reruns it whenever you make changes. Perfect for iterative development of code generators, scaffolding tools, and template processors.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/bits.gengora)](https://marketplace.visualstudio.com/items?itemName=bits.gengora)

---

## 🚀 Features

- 🔄 **Hot Reload**: Automatically recompile and restart your generator on file changes
- 🎯 **Smart Discovery**: Auto-detects generator projects using `<IsGeneratorProject>true</IsGeneratorProject>` marker
- 📊 **Live Status**: Real-time status bar showing compilation and execution state
- 🔧 **Configurable**: Customize ignore patterns and log levels
- 📝 **Structured Output**: JSON-based protocol for generator events and diagnostics
- 🛡️ **Isolated Builds**: Generated code kept separate to avoid compilation conflicts

---

## 📦 Installation

### From VS Code Marketplace

1. Open VS Code
2. Press `Ctrl+P` / `Cmd+P`
3. Type: `ext install bits.gengora`
4. Press Enter

### From VSIX

1. Download the latest `.vsix` from [Releases](https://github.com/blue-it-systems/bits.vscode/releases)
2. In VS Code: `Extensions` → `...` → `Install from VSIX...`

---

## 🏁 Quick Start

### 1. Create Your Generator Project

```bash
mkdir MyGenerator
cd MyGenerator
dotnet new console
```

### 2. Mark as Generator Project

Add this to your `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <IsGeneratorProject>true</IsGeneratorProject>
  </PropertyGroup>
</Project>
```

### 3. Write Your Generator

**Program.cs**:

```csharp
using System;
using System.IO;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Send handshake
        var hello = new
        {
            method = "generator/hello",
            @params = new
            {
                capabilities = new
                {
                    publishDiagnostics = false,
                    watchMode = false
                }
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(hello));

        // 2. Generate your code
        var outputDir = Path.Combine(
            Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName,
            "gengora-output",
            "GeneratedProject-" + DateTime.UtcNow.ToString("yyyyMMddHHmm")
        );
        
        Directory.CreateDirectory(outputDir);
        var generatedFile = Path.Combine(outputDir, "Generated.cs");
        
        File.WriteAllText(generatedFile, @"
public class GeneratedClass
{
    public string Message => ""Hello from Generator!"";
}");

        // 3. Notify extension
        var generated = new
        {
            method = "generator/generated",
            @params = new
            {
                project = outputDir,
                created = new[] { generatedFile }
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(generated));
    }
}
```

### 4. Open in VS Code

```bash
code .
```

Gengora will:
✅ Auto-detect your generator project  
✅ Build it automatically  
✅ Run your generator  
✅ Watch for changes and hot-reload  

---

## 📖 How It Works

### Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                    VS Code Extension                         │
│  • File Watchers (*.cs, *.csproj)                          │
│  • Status Bar & Output Channel                             │
└────────────┬────────────────────────────────────────────────┘
             │ LSP Protocol
             ↓
┌─────────────────────────────────────────────────────────────┐
│                   LSP Server (.NET 8.0)                      │
│  • Observes generator project folder                        │
│  • Runs: dotnet build --configuration Debug                │
│  • Copies to: .vscode/.generator/out/                      │
│  • Spawns generator process with workspace as working dir  │
└────────────┬────────────────────────────────────────────────┘
             │ Process spawn
             ↓
┌─────────────────────────────────────────────────────────────┐
│              Your Generator Process                          │
│  • Runs in workspace root directory                         │
│  • Generates files (typically to ../gengora-output/)       │
│  • Sends JSON events to stdout                             │
└─────────────────────────────────────────────────────────────┘
```

### Observation Modes

Gengora uses a three-level observation system:

```text
1. **GlobalScan**: Initial workspace scan to find generator project
2. **MinimalObservation**: Watches only `.csproj` file (when marker is absent)
3. **FullObservation**: Watches all files in generator folder (when marker is present)
```

### File Watching

The extension automatically ignores:

- `/bin/` - Build outputs
- `/obj/` - Intermediate build files
- `/node_modules/` - Node dependencies
- `/.git/` - Git metadata
- `/.vscode/.generator/` - Bundled generator assemblies
- `/gengora-output/` - Generated output (configurable)

You can add custom ignore patterns:

```json
{
  "gengora.fileWatchIgnorePatterns": [
    "/MyCustomFolder/",
    "*.tmp"
  ]
}
```

---

## ⚙️ Configuration

### Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `gengora.generatorProjectPath` | `string` | `""` | Path to generator .csproj (auto-detected if empty) |
| `gengora.serverPath` | `string` | `""` | Custom LSP server path (for development) |
| `gengora.fileWatchIgnorePatterns` | `string[]` | `[]` | Additional glob patterns to ignore |
| `gengora.autoRunOnCompileSuccess` | `boolean` | `false` | Auto-start on compile (deprecated - smart auto-start enabled) |
| `gengora.logLevel` | `string` | `"warning"` | Log verbosity: `error`, `warning`, `info`, `debug` |

### Example workspace settings.json

```json
{
  "gengora.generatorProjectPath": "MyGenerator",
  "gengora.logLevel": "warning",
  "gengora.fileWatchIgnorePatterns": [
    "/temp/",
    "*.bak"
  ]
}
```

---

## 🔌 Generator Protocol

Generators communicate with Gengora via JSON messages on stdout:

### 1. Handshake (Required)

```json
{
  "method": "generator/hello",
  "params": {
    "capabilities": {
      "publishDiagnostics": false,
      "watchMode": false,
      "watchGlobs": ["**/*"],
      "watchDebounceMs": 500
    }
  }
}
```

### 2. Progress Events

```json
{
  "method": "generator/generated",
  "params": {
    "project": "/path/to/generated/project",
    "created": [
      "/path/to/File1.cs",
      "/path/to/File2.cs"
    ]
  }
}
```

### 3. Error Reporting

```json
{
  "method": "generator/error",
  "params": {
    "message": "Something went wrong",
    "stack": "Stack trace here..."
  }
}
```

---

## 🎯 Best Practices

### ✅ Do

- **Use the marker**: Add `<IsGeneratorProject>true</IsGeneratorProject>` to enable full observation
- **Generate outside**: Create files in `../gengora-output/` or another parent directory
- **Send handshake**: Always emit `generator/hello` JSON first
- **Structured events**: Use JSON for `generator/generated` to track created files
- **Error handling**: Wrap your Main() in try-catch and emit `generator/error` JSON

### ❌ Don't

- **Don't generate in same folder**: Avoid creating files inside your generator project (causes compilation conflicts)
- **Don't ignore errors**: Always handle exceptions and report them
- **Don't use blocking I/O**: Avoid long-running processes without progress updates

### Example .gitignore

```gitignore
# Generator outputs
gengora-output/

# VS Code
.vscode/.generator/

# .NET
bin/
obj/
*.dll
*.pdb
```

---

## 🛠️ Troubleshooting

### Generator not discovered?

1. Check `.csproj` has `<IsGeneratorProject>true</IsGeneratorProject>`
2. Gengora will scan across all open workspace folders (multi-root workspaces) and will try to auto-discover any `.csproj` containing the marker before starting the server. If your project lives in an additional workspace folder (not the primary one) it will now be detected automatically; the extension will set the discovered project path and instruct the server to use that folder as the workspace.
3. Check Output → Gengora for discovery logs
4. Try manual configuration: `"gengora.generatorProjectPath": "path/to/project.csproj"`

### Files not triggering rebuild?

1. Check if files are in ignore patterns
2. Verify observation mode in logs (should be `FullObservation`)
3. Check file extension (must be `.cs`, `.csproj`, or `.json`)

### Build failures?

1. Check Output → Gengora for build errors
2. Verify .NET SDK version: `dotnet --version` (needs 8.0+)
3. Try manual build: `dotnet build YourGenerator.csproj`

### Generator output not appearing?

1. Ensure generator sends `generator/hello` JSON first
2. Check working directory is correct (should be workspace root)
3. Verify output path is outside generator project folder

---

## 📚 Examples

See the `/gengora/test-workspace/` folder for a complete working example.

---

## 🤝 Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

---

## 📄 License

MIT License - see [LICENSE](../LICENSE) file

---

## 🔗 Links

- [GitHub Repository](https://github.com/blue-it-systems/bits.vscode)
- [Issue Tracker](https://github.com/blue-it-systems/bits.vscode/issues)
- [Changelog](CHANGELOG.md)
- [Blue IT Systems](https://it-blue.com)

---

## 🙏 Acknowledgments

Built with:

- [OmniSharp Language Server Protocol](https://github.com/OmniSharp/csharp-language-server-protocol)
- [VS Code Extension API](https://code.visualstudio.com/api)
- [.NET SDK](https://dotnet.microsoft.com/)

---

Made with ❤️ by Blue IT Systems GmbH
