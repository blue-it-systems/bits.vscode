# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

Repository containing the C# Test Filter Helper VS Code extension by Blue IT Systems GmbH.
It automatically detects C# test scope for TUnit debugging.

## Build Commands

### C# Test Filter
```bash
cd csharp-test-filter
npm install && npm run compile
npm run package  # Creates .vsix
```

### Linting
```bash
cd csharp-test-filter && npm run lint
# Or with auto-fix: npm run lint:fix
```

## Debugging Extensions

Press F5 with the workspace open to launch the C# Test Filter test workspace.

## Publishing

The extension is published through a GitHub tag:
```bash
git tag csharp-test-filter-v1.0.5
git push origin csharp-test-filter-v1.0.5
```
