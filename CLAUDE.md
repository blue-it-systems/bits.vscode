# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

Monorepo containing two VS Code extensions by Blue IT Systems GmbH:

- **Gengora** (`gengora/`): Live code generation with hot-reload for .NET. Has a TypeScript extension frontend communicating via JSON-RPC with a .NET 10 language server backend.
- **C# Test Filter Helper** (`csharp-test-filter/`): Automatically detects C# test scope for TUnit debugging. TypeScript-only extension.

## Build Commands

### Gengora (full build)
```bash
# From repository root
cd gengora/server && dotnet build --configuration Release
cd ../extension && npm install && npm run compile
npm run bundle-server  # Copies server DLLs to extension/server/
npm run vsix           # Creates .vsix package
```

### Gengora Server Tests
```bash
cd gengora/server && dotnet test
```

### C# Test Filter
```bash
cd csharp-test-filter
npm install && npm run compile
npm run package  # Creates .vsix
```

### Linting
```bash
# Gengora extension
cd gengora/extension && npm run lint

# C# Test Filter
cd csharp-test-filter && npm run lint
# Or with auto-fix: npm run lint:fix
```

## Debugging Extensions

Press F5 with workspace open. Use these launch configurations:
- **Run Gengora Extension**: Builds all (server + extension) then launches with test-workspace
- **Run Extension**: Launches C# Test Filter with test-workspace

## Architecture

### Gengora Architecture

```
gengora/
├── extension/           # VS Code extension (TypeScript)
│   └── src/extension.ts # Entry point, uses vscode-languageclient
├── server/              # Language server (.NET 10, C# 14)
│   ├── Gengora.Server/
│   │   ├── Program.cs           # Entry point
│   │   ├── Core/
│   │   │   ├── GeneratorOrchestrator.cs     # Coordinates all components
│   │   │   ├── StateMachine/                # State management (Idle→GeneratorFound→Compiling→Ready→Running)
│   │   │   ├── Discovery/                   # ProjectMarkerScanner finds <IsGeneratorProject>true
│   │   │   ├── Compilation/                 # RoslynCompilationService
│   │   │   ├── Execution/                   # GeneratorExecutor runs compiled generators
│   │   │   ├── FileWatching/                # IgnorePatternMatcher, FileWatcherService
│   │   │   └── Messaging/                   # MessageParser, GeneratorMessage
│   │   └── Lsp/
│   │       └── GengoraLanguageServer.cs     # StreamJsonRpc server
│   └── Gengora.Server.Tests/                # TUnit tests
└── test-workspace/      # Sample generator projects for testing
```

**Communication**: Extension ↔ Server via stdin/stdout JSON-RPC (StreamJsonRpc). Generators communicate via JSON lines on stdout.

### Generator Project Detection

A .NET project becomes a generator when it contains:
```xml
<IsGeneratorProject>true</IsGeneratorProject>
```

## C# Server Coding Style

From `gengora/server/CODING_STYLE.adoc`:

- **Private fields**: `_PascalCase` with underscore prefix (e.g., `private readonly int _Width;`)
- **Always use `this.` prefix** for instance members
- **Local variables/parameters**: `camelCase`
- Uses .NET 10 / C# 14 features

## Publishing

Extensions are published independently via GitHub tags:
```bash
# Gengora
git tag gengora-v0.9.2
git push origin gengora-v0.9.2

# C# Test Filter
git tag csharp-test-filter-v1.0.5
git push origin csharp-test-filter-v1.0.5
```

## Test Framework

Server tests use Microsoft.Testing.Platform (TUnit) configured in `gengora/server/global.json`.
