# Gengora Extension - Specification Document

**Version**: 1.0.0  
**Last Updated**: 2025-11-24  
**Status**: Pure Specification (Implementation-Agnostic)

---

## Executive Summary

Gengora is a VS Code extension that enables **live code generation** with hot-reload support. It automatically detects C# generator projects, compiles them, executes them, and notifies users when new files are generated. The system is designed to minimize compilation conflicts, prevent infinite rebuild loops, and provide real-time feedback on generator execution.

---

## Core Specifications

### 1. Generator Project Auto-Discovery

**Specification**: The system MUST automatically detect generator projects in the workspace.

**Rules**:

- R1.1: A generator project is identified by the presence of a marker: `<IsGeneratorProject>true</IsGeneratorProject>` in a project configuration file
- R1.2: Discovery MUST occur automatically when the extension activates
- R1.3: Discovery MUST search recursively from workspace root
- R1.4: No manual configuration is required; the marker is the sole detection criterion
- R1.5: If no generator marker is found, the system MUST remain in a ready state and continue scanning

---

### 2. System State Management

**Specification**: The system MUST maintain distinct operational states and transition between them based on discovery and user actions.

**System States**:

- **Idle**: Extension activated, waiting for generator discovery
- **Generator Found**: Marker detected in workspace, ready to compile
- **Compiling**: Build process active, assembly generation in progress
- **Ready**: Compiled assembly available, ready to execute
- **Running**: Generator process actively executing
- **Error**: Compilation or execution failure detected
- **Stopped**: User manually stopped generator or auto-stopped after completion

**State Transition Rules**:

- R2.1: Idle → Generator Found: When marker discovered (R1.1)
- R2.2: Generator Found → Compiling: When compilation requested (by user or automatic)
- R2.3: Compiling → Ready: When compilation succeeds
- R2.4: Ready → Running: When execution starts
- R2.5: Running → Idle: When execution completes and no auto-rerun configured
- R2.6: Any state → Error: When compilation or execution fails (R2.7)
- R2.7: Error state MUST include error message and allow retry
- R2.8: Any state → Stopped: When user invokes stop command

---

### 3. Hot-Reload Compilation Workflow

**Specification**: The system MUST automatically recompile and re-execute the generator when source files change.

**Workflow Rules**:

- R3.1: When a source file in the generator project changes, a recompilation MUST be triggered
- R3.2: File change detection MUST apply ignore patterns before triggering recompilation (see R5.*)
- R3.3: If compilation in progress, new changes MUST be queued for recompilation after current compilation completes
- R3.4: Recompilation MUST NOT execute until current execution completes (R3.5)
- R3.5: Only one execution MUST occur at a time; new runs queue if execution in progress
- R3.6: Compilation errors MUST transition state to Error (R2.7)
- R3.7: Compilation success MUST transition state to Ready (R2.3)
- R3.8: Execution success MUST emit generated files notification (R6.*)
- R3.9: Execution failure MUST transition to Error state with error message

---

### 4. Output Artifact Directory Isolation

**Specification**: Generated files MUST be stored separately from the generator project to prevent recompilation loops.

**Directory Structure Rules**:

- R4.1: Compiled generator assembly MUST be placed in a hidden build directory separate from the generator project
- R4.2: Generated output files MUST be placed in a sibling directory outside the generator project
- R4.3: Each generator execution MUST create a uniquely-named subdirectory with timestamp
- R4.4: No generated files MUST reside within the generator project directory tree
- R4.5: This isolation prevents the generator project from being re-triggered by its own generated files
- R4.6: The output directory path MUST be explicitly configured by the generator (communication via environment variable or convention)

---

### 5. File Change Detection Filtering

**Specification**: File change events MUST be filtered to ignore build artifacts and non-source files.

**Ignore Pattern Rules**:

- R5.1: A default set of ignore patterns MUST be defined to exclude common non-source directories
- R5.2: Default ignore patterns MUST include: build outputs (/bin/, /obj/), package managers (/node_modules/, /packages/), version control (/.git/), IDE artifacts (/.vs/, /.vscode/.generator/), dependency caches, generated outputs (/gengora-output/), and IDE-specific directories
- R5.3: When a file change is detected, path MUST be tested against all ignore patterns
- R5.4: If path matches any pattern, file change event MUST be discarded
- R5.5: If path does not match any pattern, recompilation workflow MUST be triggered (R3.*)
- R5.6: Users MUST be able to customize ignore patterns via settings
- R5.7: Custom patterns MUST be merged with defaults (user patterns extend, not replace)

---

### 6. Generated File Detection and Notification

**Specification**: The system MUST reliably notify users when generator produces output files.

**Notification Trigger Rules**:

- R6.1: Generated file detection MUST support multiple independent detection paths for reliability
- R6.2: Detection Path A: Generator stdout parsing - Generator MUST emit structured event data (e.g., JSON) indicating files created; server MUST parse and validate this data
- R6.3: Detection Path B: File system watcher - Server MUST monitor output directory for new files matching convention (e.g., "generated-*" prefix)
- R6.4: Detection Path C: Extension watcher - Extension MUST create independent file watchers in UI context for redundancy
- R6.5: Multiple paths MUST use session validation to prevent duplicate notifications (R7.*)
- R6.6: Notification MUST include generator name, output location, file count, and timestamp
- R6.7: User MUST be able to view generated files directly from notification UI

**Session Validation for Deduplication (see R7 for details)**:

- R6.8: Each generator execution MUST have unique session identifier
- R6.9: Generator MUST include session ID in stdout event messages
- R6.10: Server MUST validate incoming session ID before forwarding notification
- R6.11: Mismatched session ID MUST be silently ignored (indicates stale notification)

---

### 7. Logging and Diagnostics

**Specification**: The system MUST provide comprehensive logging for troubleshooting.

**Logging Rules**:

- R7.1: All major operations MUST produce log entries with timestamp
- R7.2: Log levels MUST be supported: DEBUG, INFO, WARN, ERROR
- R7.3: Generator discovery events MUST be logged
- R7.4: File watcher initialization/errors MUST be logged
- R7.5: File change detection events MUST be logged with path
- R7.6: Compilation start/success/failure MUST be logged with error messages
- R7.7: Generator execution start/success/failure MUST be logged
- R7.8: Session ID validation results MUST be logged (matches/mismatches)
- R7.9: Notifications sent to UI MUST be logged
- R7.10: User MUST be able to configure log level via settings
- R7.11: Logs MUST be accessible in VS Code Output panel
- R7.12: Logs MUST be timestamped and include source context

---

### 8. Error Handling and Recovery

**Specification**: Failures MUST be handled gracefully without breaking user experience.

**Error Handling Rules**:

- R8.1: Compilation failures MUST result in state transition to Error (R2.7)
- R8.2: Compilation error messages MUST be extracted and displayed in VS Code Problems panel
- R8.3: Generator process crashes MUST be caught and logged, state MUST transition to Error
- R8.4: File watcher errors MUST NOT prevent extension startup; system MUST continue in degraded mode
- R8.5: Session ID mismatch MUST result in silent discard (logged but no user notification)
- R8.6: User MUST be able to manually retry after error via command
- R8.7: Error state MUST show in status bar with error indicator
- R8.8: Error message MUST be accessible from status bar click
- R8.9: Critical errors (disk space, permissions) MUST result in permanent Error state until manually reset

---

### 9. User Interface - Status Bar

**Specification**: The status bar MUST provide real-time visual feedback on generator state.

**Status Bar Display Rules**:

- R9.1: Status bar MUST always be visible in VS Code
- R9.2: Status bar MUST show current system state (R2.*)
- R9.3: Status bar MUST show associated generator project name/path
- R9.4: Status bar MUST use color coding: gray (idle), blue (observing), yellow (compiling), green (ready/running), red (error)
- R9.5: Status bar MUST include visual indicator icon corresponding to state
- R9.6: Status bar text format: "Gengora: [state] ([project name])"
- R9.7: Clicking status bar MUST open Output panel showing logs
- R9.8: Status bar MUST update immediately on state change

---

### 10. User Commands

**Specification**: Users MUST be able to control generator lifecycle via commands.

**Command Requirements**:

- R10.1: "Start Generator" command MUST trigger compilation and execution
- R10.2: "Stop Generator" command MUST terminate running generator process
- R10.3: "Reset Extension" command MUST clear all state and reinitialize
- R10.4: All commands MUST be accessible via Command Palette
- R10.5: Command shortcuts MUST be customizable by user
- R10.6: Commands MUST be disabled when not applicable to current state (e.g., Stop disabled when not running)
- R10.7: Command execution MUST update status bar and logs
- R10.8: User MUST be able to access commands from status bar context menu

---

### 11. Multi-Root Workspace Support

**Specification**: The system MUST handle VS Code workspaces with multiple folders correctly.

**Multi-Root Rules**:

- R11.1: System MUST support multiple workspace roots simultaneously
- R11.2: Each workspace root MUST have independent generator discovery and management
- R11.3: Each workspace root MUST have separate compilation and execution processes
- R11.4: Each generator execution MUST have unique session ID even within same workspace
- R11.5: Session IDs MUST prevent cross-instance notification conflicts
- R11.6: Workspace root MUST be passed to server as configuration parameter
- R11.7: Artifacts (compiled assemblies, outputs) MUST be isolated per workspace root
- R11.8: Logs MUST indicate which workspace root they apply to

---

### 12. User Configuration Settings

**Specification**: Users MUST be able to customize system behavior via settings.

**Configuration Rules**:

- R12.1: All settings MUST be accessible via VS Code Settings UI
- R12.2: Settings MUST support workspace and user scopes
- R12.3: Setting: `gengora.fileWatchIgnorePatterns` - Array of path patterns to ignore during file watching
- R12.4: Setting: `gengora.generatorProjectPath` - Optional override to force specific generator project path
- R12.5: Setting: `gengora.outputChannelLogLevel` - Log verbosity: "debug", "info", "warn", "error"
- R12.6: Setting: `gengora.enableAutoCompilation` - Boolean to enable/disable automatic recompilation on file changes
- R12.7: Setting changes MUST take effect immediately or on next reload
- R12.8: Default values for all settings MUST be reasonable for typical use cases
- R12.9: Settings MUST be documented in extension package manifest

---

## Architecture

### High-Level Architecture

The system MUST follow a client-server architecture:

**Client Layer (VS Code Extension)**:
- Responsible for UI presentation (status bar, notifications, commands)
- Responsible for local file watching (redundancy/fallback detection)
- Communicates with server via standard protocol

**Server Layer (Backend Service)**:
- Responsible for generator discovery
- Responsible for compilation and execution orchestration
- Responsible for file change detection and event forwarding
- Manages generator process lifecycle
- Generates notifications

**Communication Protocol**:
- Server and client MUST communicate via standard Language Server Protocol (LSP)
- MUST support request-response patterns for commands
- MUST support notification patterns for status updates

---

## Test Requirements

**Specification**: The system MUST pass comprehensive testing.

**Required Test Coverage**:

- R-TEST-1: Auto-discovery correctly identifies generator projects with marker
- R-TEST-2: System transitions between states (R2.*) correctly
- R-TEST-3: File changes within generator project trigger recompilation
- R-TEST-4: Generated files are created in isolated output directory
- R-TEST-5: Ignore patterns prevent false triggers on ignored files
- R-TEST-6: Multiple notification detection paths work independently
- R-TEST-7: Session ID validation prevents duplicate notifications
- R-TEST-8: Multi-root workspaces manage independent generators without interference
- R-TEST-9: Log messages appear at correct levels in output channel
- R-TEST-10: User commands execute correctly and update state
- R-TEST-11: Error messages display user-friendly information
- R-TEST-12: Extension survives generator process crashes
- R-TEST-13: Status bar updates reflect current state
- R-TEST-14: Settings changes take effect immediately
- R-TEST-15: Compilation errors appear in Problems panel

---

## System Constraints

**Specification**: The system MUST operate within defined constraints.

**Constraints**:

- C1: One generator project per workspace root (future enhancement: multi-generator support)
- C2: Generator state is not persistent across restarts
- C3: Very large generator output (millions of events) may cause brief UI delays
- C4: File watchers require local file system access (no remote SSH support)
- C5: Ignore pattern matching behavior depends on OS file system case sensitivity
- C6: Generator execution timeout should be configurable but default to reasonable value (e.g., 5 minutes)

---

## Known Limitations

**Specification**: Known limitations that exist by design or due to technical constraints.

**Limitations**:

- L1: Only one generator project actively manages per workspace folder
- L2: Generator state not persisted; each activation starts fresh
- L3: Very large JSON output streams may cause brief UI stalls
- L4: Remote SSH workspaces not supported due to file system watcher limitations
- L5: Case sensitivity of ignore patterns depends on OS file system behavior

---

## Future Enhancements

**Specification**: Planned future capabilities not in current scope.

**Planned Features**:

- E1: Multiple independent generators per workspace root
- E2: Configurable output directory per generator
- E3: Output filtering and transformation pipeline
- E4: Integration with VS Code build tasks
- E5: Performance metrics and execution profiling
- E6: Generator project templates marketplace
- E7: Remote SSH workspace support
- E8: Custom notification templates
- E9: Persistent generator configuration per workspace
- E10: Parallel generator execution
