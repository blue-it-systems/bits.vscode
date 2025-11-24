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
- R2.8: Error → Compiling: User invokes retry/start command (NOT a terminal state)
- R2.9: Any state → Stopped: When user invokes stop command

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
- R4.3: No generated files MUST reside within the generator project directory tree
- R4.4: This isolation prevents the generator project from being re-triggered by its own generated files
- R4.5: Generators MUST be protected from recursively compiling when they emit files into the generator source tree (see R6.* for protection measures)

---

### 5. File Change Detection Filtering

**Specification**: File change events MUST be filtered to ignore build artifacts and non-source files.

**Ignore Pattern Rules**:

- R5.1: A default set of ignore patterns MUST be defined to exclude common non-source directories
- R5.2: Default ignore patterns MUST include: build outputs (/bin/, /obj/), package managers (/node_modules/, /packages/), version control (/.git/), IDE artifacts (/.vs/, /.vscode/.generator/), dependency caches, generated outputs (/gengora-output/), and IDE-specific directories
- R5.3: System MUST automatically merge patterns from `.gitignore` files in workspace and generator project directories with default patterns
- R5.4: `.gitignore` patterns MUST take precedence over default patterns
- R5.5: When a file change is detected, path MUST be tested against all patterns (default + .gitignore)
- R5.6: If path matches any pattern, file change event MUST be discarded
- R5.7: If path does not match any pattern, recompilation workflow MUST be triggered (R3.*)
- R5.8: Users MUST be able to customize ignore patterns via settings
- R5.9: Custom patterns MUST be merged with defaults and .gitignore patterns (user patterns extend, not replace)

---

### 6. Generator Interface Contract

**Specification**: Generators MUST implement a loose contract to enable safe recursive-loop prevention and output tracking.

**Generator Interface Requirements**:

- R6.1: Generators MUST support structured messaging via environment variable configuration
- R6.2: Generators MUST emit messages to stdout in a standard format (structured JSON)
- R6.3: Generators MUST emit action status messages to indicate what they are doing (discovery, generation, validation, etc.)
- R6.4: Generators MUST emit file tracking messages indicating which files they intend to create or have created
- R6.5: Generators MUST include session ID in all messages (passed via `GENGORA_SESSION_ID` environment variable)
- R6.6: Message format MUST include: timestamp, action type, message content, session ID, optional file paths
- R6.7: Server MUST validate session ID in all incoming messages to prevent cross-execution message confusion

**Recursive Loop Prevention**:

- R6.8: Server MUST track which directories generators declare as "output directories"
- R6.9: Server MUST NOT watch generator source tree for file changes that match declared output patterns
- R6.10: If generator emits files to a location within generator source tree, server MUST flag as error and prevent recursive recompilation
- R6.11: Server MUST warn user if generator attempts to write to its own source directory
- R6.12: Server configuration MUST allow blocking specific output paths as protected (generator source tree always protected)

**Message Contract Example Format** (JSON Lines, one per line):

```
{"type":"generator/status","action":"start","message":"Starting code generation","session_id":"uuid","timestamp":"ISO8601"}
{"type":"generator/status","action":"analyzing","message":"Analyzing input files","session_id":"uuid","timestamp":"ISO8601"}
{"type":"generator/file","action":"emit","path":"/absolute/path/to/generated.cs","session_id":"uuid","timestamp":"ISO8601"}
{"type":"generator/status","action":"complete","message":"Generation completed","session_id":"uuid","timestamp":"ISO8601"}
```

**Notification Trigger Rules** (Revised):

- R6.13: Server MUST monitor generator stdout for structured messages
- R6.14: Server MUST parse and validate each message format
- R6.15: When message type is "generator/file" with action "emit", server MUST record file path
- R6.16: Server MUST verify emitted file path is NOT within generator source directory
- R6.17: If file path is within generator source tree, server MUST emit error notification and NOT recompile
- R6.18: If file path passes validation, server MUST forward notification to extension
- R6.19: Multiple notification detection paths are NOT needed (single structured message path is sufficient)

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

### Technology Stack Requirements

**Specification**: The system MUST utilize modern, latest-generation technologies.

**Server Technology Requirements**:

- R-ARCH-1: Server MUST be built on .NET 10 (latest LTS release)
- R-ARCH-2: Server MUST use C# 14 language features
- R-ARCH-3: Server MUST utilize Roslyn Workspace API for code analysis and project system interaction
- R-ARCH-4: Server MUST utilize Roslyn API directly for diagnostic extraction and compilation analysis
- R-ARCH-5: Server SHOULD use OmniSharp if available for LSP protocol implementation, otherwise implement LSP directly
- R-ARCH-6: All async operations MUST use modern async/await patterns with ValueTask where appropriate
- R-ARCH-7: Server MUST target net10.0 framework

**Extension Technology Requirements**:

- R-ARCH-8: Extension MUST use latest TypeScript version (5.x+)
- R-ARCH-9: Extension MUST use VS Code API latest version

**Testing Framework Requirements**:

- R-ARCH-10: Unit tests MUST use TUnit framework (latest version)
- R-ARCH-11: All server logic MUST have unit test coverage independent of VS Code extension
- R-ARCH-12: Unit tests MUST NOT require VS Code runtime to execute
- R-ARCH-13: Unit tests MUST run in standard test runners (dotnet test)

### High-Level Architecture

The system MUST follow a client-server architecture:

**Client Layer (VS Code Extension)**:

- Responsible for UI presentation (status bar, notifications, commands)
- Responsible for local file watching (fallback detection)
- Communicates with server via standard protocol

**Server Layer (Backend Service)**:

- Responsible for generator discovery
- Responsible for compilation and execution orchestration
- Responsible for file change detection and event forwarding
- Manages generator process lifecycle
- Generates notifications
- Uses Roslyn APIs for project and compilation analysis

**Communication Protocol**:

- Server and client MUST communicate via standard Language Server Protocol (LSP)
- MUST support request-response patterns for commands
- MUST support notification patterns for status updates

---

## Test Requirements

**Specification**: The system MUST pass comprehensive testing, with test strategy independent of VS Code extension where possible.

### Server-Side Unit Tests (Independent, No VS Code Required)

**Specification**: Core server logic MUST be testable via TUnit without VS Code runtime.

**Unit Test Categories**:

- R-TEST-1: **Generator Discovery** - Test project scanning logic independently
  - Sub-test: Correctly identifies `<IsGeneratorProject>true</IsGeneratorProject>` marker
  - Sub-test: Handles missing marker correctly
  - Sub-test: Handles malformed project files
  - Sub-test: Recursively searches nested directories

- R-TEST-2: **State Machine** - Test all state transitions (R2.*) in isolation
  - Sub-test: All valid transitions allowed
  - Sub-test: Invalid transitions rejected or ignored
  - Sub-test: Error state can transition to Compiling on retry
  - Sub-test: State change notifications emitted correctly

- R-TEST-3: **File Change Filtering** - Test ignore pattern logic independently
  - Sub-test: Default patterns correctly filter build artifacts
  - Sub-test: User patterns merged with defaults
  - Sub-test: `.gitignore` patterns loaded and merged correctly
  - Sub-test: Case sensitivity handled per OS
  - Sub-test: Absolute and relative path matching works

- R-TEST-4: **Ignore Pattern Loading** - Test .gitignore integration
  - Sub-test: Workspace .gitignore loaded and parsed
  - Sub-test: Generator project .gitignore loaded and parsed
  - Sub-test: Multiple .gitignore files merged correctly
  - Sub-test: Invalid .gitignore lines handled gracefully

- R-TEST-5: **Output Directory Isolation** - Test artifact segregation logic
  - Sub-test: Generated output paths not in generator source tree
  - Sub-test: Paths within generator source tree detected as errors
  - Sub-test: Protected directories marked correctly

- R-TEST-6: **Generator Message Parsing** - Test message contract (R6.*)
  - Sub-test: Valid JSON message parsed correctly
  - Sub-test: Invalid JSON handled gracefully
  - Sub-test: Session ID extracted and validated
  - Sub-test: Required fields validated
  - Sub-test: Unknown message types ignored safely

- R-TEST-7: **Recursive Loop Prevention** - Test protection mechanisms
  - Sub-test: File emitted to generator source tree triggers error
  - Sub-test: File emitted to safe output directory passes validation
  - Sub-test: Recursive compilation prevented
  - Sub-test: User warned of recursive attempt

- R-TEST-8: **Roslyn Integration** - Test .NET Roslyn API usage
  - Sub-test: Project loading via Roslyn Workspace API works
  - Sub-test: Compilation diagnostics extracted correctly
  - Sub-test: Error/warning messages formatted properly
  - Sub-test: Multiple target frameworks handled

- R-TEST-9: **Async Operation Sequencing** - Test compilation/execution queuing
  - Sub-test: Multiple file changes queued correctly
  - Sub-test: Executions don't overlap
  - Sub-test: Cancellation tokens work properly
  - Sub-test: Timeouts enforced

- R-TEST-10: **Logging** - Test log output independently
  - Sub-test: All log levels work (DEBUG, INFO, WARN, ERROR)
  - Sub-test: Log format includes required fields
  - Sub-test: Sensitive data not leaked in logs

- R-TEST-11: **Error Handling** - Test exception scenarios
  - Sub-test: Compilation errors caught and reported
  - Sub-test: Process crashes handled gracefully
  - Sub-test: Disk space errors detected
  - Sub-test: Permission errors reported user-friendly

### Extension Integration Tests (VS Code Simulation)

**Specification**: Extension behavior MUST be testable with VS Code API mock/simulation.

**Extension Test Strategy**:

- R-TEST-EXT-1: **VS Code Mock Layer** - Create minimal VS Code API mock
  - Status bar mock with setText/setColor methods
  - Output channel mock with appendLine method
  - Command registry mock with registerCommand
  - Settings mock with configuration provider
  - File watcher mock with onDidChange event

- R-TEST-EXT-2: **Status Bar Updates** - Test against mock VS Code
  - Sub-test: Status bar updates on state change
  - Sub-test: Color coding applied correctly
  - Sub-test: Text format correct: "Gengora: [state] ([project])"

- R-TEST-EXT-3: **Notification Display** - Test against mock VS Code
  - Sub-test: Notifications shown on generator output
  - Sub-test: Deduplication prevents duplicate notifications
  - Sub-test: Action buttons (Show/Reveal) present

- R-TEST-EXT-4: **Command Execution** - Test against mock VS Code
  - Sub-test: Start command triggers state change
  - Sub-test: Stop command terminates process
  - Sub-test: Reset command clears state

- R-TEST-EXT-5: **Settings Integration** - Test against mock VS Code
  - Sub-test: Settings read from mock configuration
  - Sub-test: Changes take effect immediately
  - Sub-test: Defaults applied when unset

- R-TEST-EXT-6: **Local File Watcher** - Test against mock file watcher
  - Sub-test: File watcher created on startup
  - Sub-test: Change events processed
  - Sub-test: Ignore patterns applied

### End-to-End Test Scenarios

**Specification**: Complete workflows MUST be tested end-to-end.

**E2E Test Scenarios**:

- R-TEST-E2E-1: **Happy Path**
  - Project with marker discovered → Compiles → Executes → Output files notified

- R-TEST-E2E-2: **Compilation Failure Recovery**
  - File change → Compilation fails → Error state → User retries → Success

- R-TEST-E2E-3: **Recursive Loop Prevention**
  - Generator emits file to source tree → Error detected → Recompile prevented

- R-TEST-E2E-4: **Multi-Root Workspace**
  - Two workspace roots with generators → Independent discovery and execution

- R-TEST-E2E-5: **Ignore Pattern Effectiveness**
  - Change in /bin/ directory → Ignored, no recompile
  - Change in /src/Program.cs → Triggers recompile

**Test Execution Requirements**:

- R-TEST-EXEC-1: All unit tests MUST run via `dotnet test`
- R-TEST-EXEC-2: No external dependencies required (use mocks)
- R-TEST-EXEC-3: Tests MUST be parallelizable
- R-TEST-EXEC-4: Test runtime MUST be < 30 seconds for full suite
- R-TEST-EXEC-5: Code coverage MUST be >= 80% for server logic
- R-TEST-EXEC-6: CI/CD MUST run tests on every commit

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
- C7: .gitignore parsing MUST be compatible with standard Git ignore format
- C8: Recursive loop detection MUST prevent compilation if output written to generator source tree

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
