# BITS VS Code Extensions

This repository contains the C# Test Filter VS Code extension developed by Blue IT Systems GmbH.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Extension

### [C# Test Filter Helper](./csharp-test-filter/)

[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/bits.csharp-test-filter)](https://marketplace.visualstudio.com/items?itemName=bits.csharp-test-filter)

Automatically detect C# test scope for TUnit debugging

Located in `csharp-test-filter/` - **Latest version: 1.0.5**

Features:

- 🎯 Auto-detects test assembly/class/method scope
- 🐛 Seamless integration with VS Code debugger
- ⚡ Works with TUnit and other test frameworks

[Read more →](./csharp-test-filter/README.md)

## 🚀 Publishing

### Publishing C# Test Filter

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
- Visual Studio Code

### Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/blue-it-systems/bits.vscode.git
   cd bits.vscode
   ```

2. Install dependencies:

   ```bash
   cd csharp-test-filter
   npm install
   ```

3. Open VS Code at repository root:

   ```bash
   code .
   ```

4. Press F5 to launch Extension Development Host

The workspace is configured with a debug task for the extension.

### Building

#### Building C# Test Filter

```bash
cd csharp-test-filter
npm run compile
npm run package  # Creates .vsix
```

## 📝 License

MIT License - see individual extension folders for details.

## 🔗 Links

- [Blue IT Systems GmbH](https://it-blue.com)
- [Issue Tracker](https://github.com/blue-it-systems/bits.vscode/issues)
- [VS Code Marketplace](https://marketplace.visualstudio.com/publishers/bits)

---

Made with ❤️ by Blue IT Systems GmbH
