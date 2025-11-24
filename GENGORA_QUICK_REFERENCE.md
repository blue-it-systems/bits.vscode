# Gengora v0.2.9 - Quick Reference

## 📦 What's New

**Debug Logging** for troubleshooting generated file notifications

- Watcher initialization logs
- File detection logs
- Session ID validation logs
- Notification forwarding logs
- Raw generator output logs

## 🚀 Installation

✅ **Already installed**: `bits.gengora` v0.2.9

## 📋 Testing Workflow

### 1. Open Extension Host
```bash
F5  # or Run → Run Gengora Extension
```

### 2. Watch Logs
```
Output → Gengora LSP channel
```

### 3. Trigger Generator
```
Cmd+Shift+P → Gengora: Start Generator
```

### 4. Check Output
```
Look for these messages:
✓ [Gengora] StartOutputWatchers called with projectFolder:
✓ [Gengora] Setting up file watcher for:
✓ [Gengora] DEBUG: Checking session ID
✓ [Gengora] Forwarding generator notification:
```

## 🔍 Debug Log Locations

| Event | Log Message |
|-------|------------|
| Watcher Init | `[Gengora] StartOutputWatchers called with projectFolder:` |
| Watcher Setup | `[Gengora] Setting up file watcher for:` |
| File Detected | `[Gengora] File detected by watcher:` |
| Session Check | `[Gengora] DEBUG: Checking session ID - owned: X incoming: Y` |
| Session Mismatch | `[Gengora] Ignoring generator message due to session mismatch` |
| Forward | `[Gengora] Forwarding generator notification:` |
| Raw Output | `[Gengora] HandleGeneratorStdoutLine received:` |

## 📄 Documentation

- **Full Spec**: `GENGORA_SPECIFICATION.md` (622 lines)
- **Deployment**: `DEPLOYMENT_SUMMARY_v0.2.9.md`
- **Changelog**: `gengora/CHANGELOG.md`

## 🎯 Key Features

| Feature | Status |
|---------|--------|
| Auto-discovery | ✅ Working |
| Hot-reload | ✅ Working |
| File watching | ✅ Working |
| Notifications | ✅ + Debug logs |
| Multi-root support | ✅ With session IDs |
| Error handling | ✅ Graceful |

## 🐛 Troubleshooting

**Problem**: No notifications appearing

**Solution**: Check logs in Output → Gengora LSP for:
1. Session ID mismatch? → Generator not claimed by server
2. Watcher not initialized? → Path issue
3. File pattern not matching? → Generated-* naming issue
4. Notification not forwarded? → Server crash or error

## 📊 Version Info

| Item | Value |
|------|-------|
| Current Version | 0.2.9 |
| Previous Version | 0.2.8 |
| Build Date | 2025-11-24 |
| .NET Version | 8.0 |
| TypeScript Version | 5.x |

## 🔗 Related Files

- Server: `gengora/server/Services/GeneratorService.cs` (debug logging)
- Extension: `gengora/extension/src/extension.ts`
- Constants: `gengora/server/Constants.cs`
- Test Generator: `gengora/test-workspace/Program.cs`

## 💡 Remember

- Debug logging is **always-on** in v0.2.9
- Check **Output panel** for all diagnostics
- **Session IDs** prevent cross-instance conflicts
- **Ignore patterns** prevent infinite rebuilds
- **Separate output** avoids compilation conflicts

---

**Ready to test?** Open F5 and check the Output panel! 🚀

