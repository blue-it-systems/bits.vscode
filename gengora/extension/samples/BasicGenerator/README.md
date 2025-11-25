# BasicGenerator - Gengora Sample Project

A minimal, well-documented code generator that demonstrates all core Gengora concepts.

## Quick Start

1. Open this folder in VS Code with Gengora installed
2. The generator will automatically be detected (status bar shows "GeneratorFound")
3. Wait for compilation (status bar shows "Compiling")
4. The generator runs automatically (status bar shows "Running" → "Ready")
5. Check the `Generated/` folder for output files

## Project Structure

```
BasicGenerator/
├── BasicGenerator.csproj    # Project file with IsGeneratorProject marker
├── Program.cs               # Generator implementation
├── README.md                # This file
└── Generated/               # Output folder (created on first run)
    ├── GeneratedService.cs  # Generated service class
    └── GeneratedConfig.cs   # Generated configuration
```

## How It Works

### 1. Project Marker

The `.csproj` file contains:
```xml
<IsGeneratorProject>true</IsGeneratorProject>
```
This tells Gengora to treat this project as a code generator.

### 2. Communication Protocol

The generator communicates with Gengora using JSON Lines on stdout:

**Status Messages:**
```json
{"type":"generator/status","action":"start","message":"Starting...","session_id":"abc123","timestamp":"2024-01-01T00:00:00Z"}
```

**File Emission:**
```json
{"type":"generator/file","action":"emit","path":"/full/path/to/file.cs","session_id":"abc123","timestamp":"2024-01-01T00:00:00Z"}
```

### 3. Session ID

The `GENGORA_SESSION_ID` environment variable links all messages to the current session:
```csharp
var sessionId = Environment.GetEnvironmentVariable("GENGORA_SESSION_ID");
```

### 4. GengoraClient Class

The sample includes a type-safe `GengoraClient` class for server communication:
```csharp
var client = new GengoraClient(sessionId);
client.ReportStatus(GengoraAction.Start, "Generator starting");
client.EmitFile("/path/to/generated/file.cs", "Generated service");
client.ReportStatus(GengoraAction.Complete, "Done!");
```

## Customization

### Adding More Generated Files

1. Create your generation logic in `Program.cs`
2. Write the file to disk
3. Call `client.EmitFile(path, description)` to notify Gengora

### Reading Workspace Files

Access the workspace via the current directory:
```csharp
var workspaceFiles = Directory.GetFiles(
    Directory.GetCurrentDirectory(), 
    "*.cs", 
    SearchOption.AllDirectories
);
```

### Error Handling

Always wrap your logic in try-catch and report errors:
```csharp
try
{
    // Your generation logic
    client.ReportStatus(GengoraAction.Complete, "Success!");
}
catch (Exception ex)
{
    client.ReportStatus(GengoraAction.Error, ex.Message);
    Environment.Exit(1);
}
```

## Hot Reload

Gengora watches for file changes. When you modify `Program.cs`:

1. Gengora detects the change
2. Recompiles the generator
3. Re-executes with a new session ID
4. Generated files are updated

Try it: Change the `ApplicationName` constant and save!

## Useful Use Cases

This basic pattern can be extended for:

- **Entity Generation**: Read database schema, generate entity classes
- **API Clients**: Parse OpenAPI specs, generate HTTP clients
- **Mappers**: Generate AutoMapper profiles from conventions
- **Validators**: Create FluentValidation rules from attributes
- **Documentation**: Extract XML docs into Markdown files

## Troubleshooting

### Generator Not Detected

- Ensure `<IsGeneratorProject>true</IsGeneratorProject>` is in the `.csproj`
- Check the Gengora output channel for errors
- Verify .NET SDK is installed

### Compilation Errors

- Check the Gengora output channel for compiler diagnostics
- Ensure all NuGet packages are restored
- Verify target framework is supported

### Files Not Generated

- Check that `client.EmitFile()` is called for each file
- Verify the output path is valid
- Check file system permissions

## Support

- Documentation: https://github.com/blue-it-systems/bits.vscode/tree/main/gengora
- Issues: https://github.com/blue-it-systems/bits.vscode/issues
- License: MIT

---

© 2024 Blue IT Systems GmbH
