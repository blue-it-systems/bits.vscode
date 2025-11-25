# Gengora - Live Code Generation with Hot-Reload Support

[![Version](https://img.shields.io/badge/version-0.3.0-blue.svg)](./CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](./LICENSE)

Gengora is a Visual Studio Code extension that enables live code generation with hot-reload support for .NET projects. Write your code generators in C# and see generated code update automatically as you edit.

## Features

- **Live Code Generation**: Write generators that produce code files automatically
- **Hot-Reload**: Generators recompile and re-run when source files change
- **Status Bar Integration**: Real-time feedback on generator state
- **Diagnostic Reporting**: Compilation errors displayed in VS Code
- **File Watching**: Automatic detection of source file changes

## Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                         VS Code Extension Host                         │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  ┌─────────────────┐    ┌──────────────────┐    ┌──────────────────┐   │
│  │   Status Bar    │    │  Output Channel  │    │    Commands      │   │
│  │   (Gengora:     │    │  (Logs, Events)  │    │  (Recompile,     │   │
│  │    Ready)       │    │                  │    │   Stop, etc.)    │   │
│  └────────┬────────┘    └────────┬─────────┘    └────────┬─────────┘   │
│           │                      │                       │             │
│           └──────────────────────┼───────────────────────┘             │
│                                  │                                     │
│                      ┌───────────▼───────────┐                         │
│                      │   Language Client     │                         │
│                      │(vscode-languageclient)│                         │
│                      └───────────┬───────────┘                         │
│                                  │                                     │
└──────────────────────────────────┼─────────────────────────────────────┘
                                   │
                          stdin/stdout (JSON-RPC)
                                   │
┌──────────────────────────────────┼─────────────────────────────────────┐
│                                  │                                     │
│               ┌──────────────────▼──────────────────┐                  │
│               │     Gengora Language Server         │                  │
│               │     (StreamJsonRpc)                 │                  │
│               └──────────────────┬──────────────────┘                  │
│                                  │                                     │
│               ┌──────────────────▼──────────────────┐                  │
│               │     Generator Orchestrator          │                  │
│               └──────────────────┬──────────────────┘                  │
│                                  │                                     │
│    ┌─────────────┬───────────────┼───────────────┬─────────────┐       │
│    │             │               │               │             │       │
│    ▼             ▼               ▼               ▼             ▼       │
│ ┌───────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐  │
│ │State  │   │ Project  │   │  File    │   │ Roslyn   │   │Generator │  │
│ │Machine│   │ Scanner  │   │ Watcher  │   │Compiler  │   │ Executor │  │
│ └───────┘   └──────────┘   └──────────┘   └──────────┘   └──────────┘  │
│                                                                        │
│                       .NET 10 Language Server                          │
└────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ dotnet run
                                   ▼
                    ┌──────────────────────────────┐
                    │    Generator Process         │
                    │    (User's Generator Code)   │
                    │                              │
                    │  stdout: JSON Lines          │
                    │  ┌────────────────────────┐  │
                    │  │ {"type":"generator/    │  │
                    │  │  status","action":     │  │
                    │  │  "emit",...}           │  │
                    │  └────────────────────────┘  │
                    └──────────────────────────────┘
```

## Quick Start

### 1. Create a Generator Project

Create a new .NET console application with the `<IsGeneratorProject>` marker:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsGeneratorProject>true</IsGeneratorProject>
  </PropertyGroup>
</Project>
```

### 2. Write Your Generator

```csharp
using System.Text.Json;

// Get session ID from environment
var sessionId = Environment.GetEnvironmentVariable("GENGORA_SESSION_ID") 
    ?? Guid.NewGuid().ToString("N");

// Send status message
SendMessage(new {
    type = "generator/status",
    action = "start",
    message = "Generator starting",
    session_id = sessionId,
    timestamp = DateTimeOffset.UtcNow.ToString("O")
});

// Generate your code...
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Generated.cs");
await File.WriteAllTextAsync(outputPath, "// Generated code here");

// Report emitted file
SendMessage(new {
    type = "generator/file",
    action = "emit",
    path = outputPath,
    session_id = sessionId,
    timestamp = DateTimeOffset.UtcNow.ToString("O")
});

// Send completion message
SendMessage(new {
    type = "generator/status",
    action = "complete",
    message = "Generation complete",
    session_id = sessionId,
    timestamp = DateTimeOffset.UtcNow.ToString("O")
});

void SendMessage(object message) {
    Console.WriteLine(JsonSerializer.Serialize(message));
}
```

### 3. Open in VS Code

Open the folder containing your generator project. Gengora will automatically:

1. Discover the generator project
2. Compile it with Roslyn
3. Execute it
4. Watch for file changes
5. Hot-reload when you make changes

## Generator State Machine

```
┌───────┐
│ Idle  │◄────────────────────────────────────────┐
└───┬───┘                                         │
    │ Generator project discovered                │
    ▼                                             │
┌───────────────┐                                 │
│GeneratorFound │                                 │
└───────┬───────┘                                 │
        │ Start compilation                       │
        ▼                                         │
┌───────────────┐    Compilation error     ┌─────┴─────┐
│  Compiling    │─────────────────────────►│   Error   │
└───────┬───────┘                          └─────┬─────┘
        │ Compilation success                    │
        ▼                                        │ Retry
┌───────────────┐◄───────────────────────────────┘
│    Ready      │◄────────────────────┐
└───────┬───────┘                     │
        │ Execute generator           │
        ▼                             │
┌───────────────┐    Execution        │
│   Running     │    complete         │
└───────┬───────┘─────────────────────┘
        │
        │ File change detected (hot-reload)
        ▼
     Back to Compiling
```

## Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `gengora.serverPath` | string | "" | Path to custom language server |
| `gengora.logLevel` | string | "debug" | Log verbosity level |
| `gengora.autoStart` | boolean | true | Auto-start on workspace open |

## Commands

| Command | Description |
|---------|-------------|
| `Gengora: Recompile Generator` | Force recompilation |
| `Gengora: Stop Generator` | Stop the current generator |
| `Gengora: Show Output` | Show output channel |

## Message Protocol

Generators communicate via JSON lines on stdout:

### Status Message
```json
{
    "type": "generator/status",
    "action": "start|analyzing|complete|error",
    "message": "Human readable message",
    "session_id": "unique-session-id",
    "timestamp": "2024-01-01T00:00:00Z"
}
```

### File Emit Message
```json
{
    "type": "generator/file",
    "action": "emit",
    "path": "/absolute/path/to/generated.cs",
    "session_id": "unique-session-id",
    "timestamp": "2024-01-01T00:00:00Z"
}
```

## Development

### Prerequisites

- .NET 10.0 SDK
- Node.js 18+
- VS Code 1.85+

### Build

```bash
# Build server
dotnet build ./server

# Build extension
cd extension
npm install
npm run compile
```

### Test

```bash
# Run server tests
dotnet test ./server

# Run extension tests
cd extension
npm test
```

### Debug

Press F5 in VS Code with the workspace open. This will:
1. Build the server and extension
2. Launch a new VS Code instance with the extension loaded
3. Open the test-workspace folder

## License

MIT - See [LICENSE](./LICENSE) for details.
