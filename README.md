# BITS VS Code Extensions

This repository contains VS Code extensions developed by Blue IT Systems GmbH.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## 📦 Extensions

### [Gengora - Live Code Generator](./gengora/)

[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/bits.gengora)](https://marketplace.visualstudio.com/items?itemName=bits.gengora)

**Real-time code generation with hot-reload support**

Located in `gengora/` - **Latest version: 0.1.4**

Features:
- 🔄 Automatic recompilation and restart on file changes
- 🎯 Smart generator project discovery with markers
- 📊 Live status bar and structured JSON protocol
- 🛡️ Isolated builds to prevent compilation conflicts

[Read more →](./gengora/README.md)

### [C# Test Filter Helper](./csharp-test-filter/)

[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/bits.csharp-test-filter)](https://marketplace.visualstudio.com/items?itemName=bits.csharp-test-filter)

**Automatically detect C# test scope for TUnit debugging**

Located in `csharp-test-filter/` - **Latest version: 1.0.5**

Features:
- 🎯 Auto-detects test assembly/class/method scope
- 🐛 Seamless integration with VS Code debugger
- ⚡ Works with TUnit and other test frameworks

[Read more →](./csharp-test-filter/README.md)

## 🚀 Publishing

### Independent Releases

Each extension can be released independently using GitHub tags:

#### Gengora

```bash
git tag gengora-v0.1.4
git push origin gengora-v0.1.4
```

This triggers the `publish-gengora.yml` workflow.

#### C# Test Filter

```bash
git tag csharp-test-filter-v1.0.5
git push origin csharp-test-filter-v1.0.5
```

This triggers the `publish-csharp-test-filter.yml` workflow.

### Manual Publishing

You can also trigger workflows manually from GitHub Actions with the `workflow_dispatch` event.

## 🛠️ Development

### Prerequisites

- Node.js 20+
- .NET 8.0 SDK (for Gengora)
- Visual Studio Code

### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/blue-it-systems/bits.vscode.git
   cd bits.vscode
   ```

2. Install dependencies for an extension:
   ```bash
   # For C# Test Filter
   cd csharp-test-filter
   npm install
   
   # For Gengora
   cd gengora/extension
   npm install
   cd ../server
   dotnet restore
   ```

3. Open VS Code at repository root:
   ```bash
   code .
   ```

4. Press F5 to launch Extension Development Host

The workspace is configured with debug tasks for each extension.

### Building

#### C# Test Filter

```bash
cd csharp-test-filter
npm run compile
npm run package  # Creates .vsix
```

#### Gengora

```bash
# Build server
cd gengora/server
dotnet build --configuration Release

# Build extension
cd ../extension
npm run compile
npm run bundle-server  # Bundles server DLL
npm run package        # Creates .vsix
```

## 📝 License

MIT License - see individual extension folders for details.

## 🔗 Links

- [Blue IT Systems GmbH](https://it-blue.com)
- [Issue Tracker](https://github.com/blue-it-systems/bits.vscode/issues)
- [VS Code Marketplace](https://marketplace.visualstudio.com/publishers/bits)

---

**Made with ❤️ by Blue IT Systems GmbH**
