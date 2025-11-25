# Gengora.Generator.Abstractions

Type-safe abstractions for building Gengora code generators.

## Installation

```bash
dotnet add package Gengora.Generator.Abstractions
```

Or add to your `.csproj`:
```xml
<PackageReference Include="Gengora.Generator.Abstractions" Version="1.0.0" />
```

## Quick Start

```csharp
using Gengora.Generator;

// Create client from environment
var client = GengoraClient.FromEnvironment();

// Report start
client.ReportStatus(GengoraAction.Start, "Generator starting");

// Your generation logic here...
await File.WriteAllTextAsync("output.cs", generatedCode);

// Report emitted file
client.EmitFile("output.cs", "Generated service class");

// Report completion
client.ReportComplete();
```

## API Reference

### GengoraClient

The main class for communicating with the Gengora server.

```csharp
// Create from environment variable
var client = GengoraClient.FromEnvironment();

// Or with explicit session ID
var client = new GengoraClient(sessionId);
```

### Methods

| Method | Description |
|--------|-------------|
| `ReportStatus(action, message)` | Report a status update |
| `EmitFile(path, message?)` | Report a generated file |
| `ReportError(message, exception?)` | Report an error |
| `ReportComplete(message?)` | Report successful completion |

### GengoraAction Enum

| Value | Description |
|-------|-------------|
| `Start` | Generator has started |
| `Analyzing` | Analyzing workspace/inputs |
| `Generating` | Actively generating code |
| `Complete` | Successfully completed |
| `Error` | Encountered an error |

## Example Generator

```csharp
using Gengora.Generator;

var client = GengoraClient.FromEnvironment();

try
{
    client.ReportStatus(GengoraAction.Start, "Starting generation");
    
    client.ReportStatus(GengoraAction.Analyzing, "Reading configuration");
    var config = await ReadConfigAsync();
    
    client.ReportStatus(GengoraAction.Generating, "Generating entities");
    foreach (var entity in config.Entities)
    {
        var code = GenerateEntity(entity);
        var path = $"Generated/{entity.Name}.cs";
        await File.WriteAllTextAsync(path, code);
        client.EmitFile(path, $"Generated {entity.Name}");
    }
    
    client.ReportComplete($"Generated {config.Entities.Count} entities");
}
catch (Exception ex)
{
    client.ReportError("Generation failed", ex);
    Environment.Exit(1);
}
```

## Testing

The client accepts a `TextWriter` for testing:

```csharp
var output = new StringWriter();
var client = new GengoraClient("test-session", output);

client.ReportStatus(GengoraAction.Start, "Test");

var json = output.ToString();
Assert.Contains("generator/status", json);
```

## License

MIT License - © 2024 Blue IT Systems GmbH
