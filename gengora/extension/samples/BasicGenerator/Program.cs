// ============================================================================
// Gengora Basic Generator Sample
// ============================================================================
// This sample demonstrates how to create a code generator that communicates
// with the Gengora VS Code extension server.
//
// Key Concepts:
// 1. Session ID - Links all messages to the current generation session
// 2. Status Messages - Report progress to the server
// 3. File Emit Messages - Tell the server about generated files
// 4. JSON Lines Protocol - Each message is a single line of JSON on stdout
// ============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

// ============================================================================
// STEP 1: Get Session ID from Environment
// ============================================================================
// The Gengora server provides a unique session ID for each execution.
// All messages must include this ID to be processed correctly.

var sessionId = Environment.GetEnvironmentVariable("GENGORA_SESSION_ID") 
    ?? Guid.NewGuid().ToString("N");

// Create a type-safe client for communicating with the server
var client = new GengoraClient(sessionId);

// ============================================================================
// STEP 2: Report Start Status
// ============================================================================
// Always send a "start" status at the beginning of your generator.
// This lets the server know the generator has begun execution.

client.ReportStatus(GengoraAction.Start, "Basic Generator Starting");

try
{
    // ============================================================================
    // STEP 3: Perform Your Generation Logic
    // ============================================================================
    // This is where you implement your actual code generation.
    // You can:
    // - Read workspace files
    // - Parse configurations
    // - Generate code based on templates
    // - Create multiple output files

    client.ReportStatus(GengoraAction.Analyzing, "Analyzing workspace...");

    // Example: Generate a simple class file
    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Generated");
    Directory.CreateDirectory(outputDir);

    var timestamp = DateTimeOffset.UtcNow;
    var outputPath = Path.Combine(outputDir, "GeneratedService.cs");

    var generatedCode = GenerateServiceClass(timestamp, sessionId);
    await File.WriteAllTextAsync(outputPath, generatedCode);

    // ============================================================================
    // STEP 4: Report Emitted Files
    // ============================================================================
    // For each file you generate, send a "file emit" message.
    // This allows the server to track generated files and refresh VS Code.

    client.EmitFile(outputPath, "Generated service class");

    // Example: Generate a second file
    var configPath = Path.Combine(outputDir, "GeneratedConfig.cs");
    var configCode = GenerateConfigClass(timestamp);
    await File.WriteAllTextAsync(configPath, configCode);
    client.EmitFile(configPath, "Generated configuration class");

    // ============================================================================
    // STEP 5: Report Completion
    // ============================================================================
    // Always send a "complete" status when finished successfully.

    client.ReportStatus(GengoraAction.Complete, "Generation completed successfully!");
}
catch (Exception ex)
{
    // ============================================================================
    // ERROR HANDLING
    // ============================================================================
    // If something goes wrong, report an error status.
    // The server will display this in VS Code.

    client.ReportStatus(GengoraAction.Error, $"Generation failed: {ex.Message}");
    
    // Also write to stderr for debugging
    Console.Error.WriteLine($"Generator Error: {ex}");
    
    Environment.Exit(1);
}

// ============================================================================
// GENERATION METHODS
// ============================================================================
// These methods demonstrate generating actual C# code.
// In a real generator, you might:
// - Use Roslyn for syntax tree manipulation
// - Use T4 or Scriban templates
// - Parse source files and transform them

static string GenerateServiceClass(DateTimeOffset timestamp, string sessionId)
{
    return $$"""
        // ============================================================================
        // AUTO-GENERATED CODE - DO NOT MODIFY
        // ============================================================================
        // Generator: BasicGenerator
        // Timestamp: {{timestamp:O}}
        // Session:   {{sessionId}}
        // ============================================================================
        
        namespace Generated;
        
        /// <summary>
        /// A sample generated service class.
        /// This demonstrates code generation with Gengora.
        /// </summary>
        public sealed class GeneratedService
        {
            /// <summary>
            /// Gets the timestamp when this code was generated.
            /// </summary>
            public static DateTimeOffset GeneratedAt => DateTimeOffset.Parse("{{timestamp:O}}");
            
            /// <summary>
            /// Gets the session ID of the generation run.
            /// </summary>
            public static string SessionId => "{{sessionId}}";
            
            /// <summary>
            /// Example method showing generated code can be fully functional.
            /// </summary>
            public string GetGreeting(string name)
            {
                return $"Hello, {name}! This code was generated at {GeneratedAt:g}";
            }
            
            /// <summary>
            /// Demonstrates that generated code can have complex logic.
            /// </summary>
            public IEnumerable<int> GenerateFibonacci(int count)
            {
                int a = 0, b = 1;
                for (int i = 0; i < count; i++)
                {
                    yield return a;
                    (a, b) = (b, a + b);
                }
            }
        }
        """;
}

static string GenerateConfigClass(DateTimeOffset timestamp)
{
    return $$"""
        // ============================================================================
        // AUTO-GENERATED CODE - DO NOT MODIFY
        // ============================================================================
        // Generator: BasicGenerator
        // Timestamp: {{timestamp:O}}
        // ============================================================================
        
        namespace Generated;
        
        /// <summary>
        /// Generated configuration constants.
        /// Modify the generator to change these values.
        /// </summary>
        public static class GeneratedConfig
        {
            /// <summary>
            /// Application name constant.
            /// </summary>
            public const string ApplicationName = "MyGeneratedApp";
            
            /// <summary>
            /// Version string.
            /// </summary>
            public const string Version = "1.0.0";
            
            /// <summary>
            /// Build timestamp.
            /// </summary>
            public const string BuildTimestamp = "{{timestamp:O}}";
            
            /// <summary>
            /// Feature flags that can be generated from configuration.
            /// </summary>
            public static class Features
            {
                public const bool EnableLogging = true;
                public const bool EnableMetrics = true;
                public const bool EnableTracing = false;
            }
        }
        """;
}

// ============================================================================
// GENGORA CLIENT - Type-Safe Server Communication
// ============================================================================
// This class provides a clean API for communicating with the Gengora server.
// You can copy this to your own generators or use the Gengora.Generator.Abstractions
// NuGet package when available.

/// <summary>
/// Type-safe client for communicating with the Gengora server.
/// </summary>
public sealed class GengoraClient
{
    private readonly string _sessionId;
    private readonly JsonSerializerOptions _jsonOptions;

    public GengoraClient(string sessionId)
    {
        _sessionId = sessionId;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Reports a status update to the server.
    /// </summary>
    public void ReportStatus(GengoraAction action, string message)
    {
        var msg = new GengoraStatusMessage
        {
            Type = "generator/status",
            Action = action.ToString().ToLowerInvariant(),
            Message = message,
            SessionId = _sessionId,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
        SendMessage(msg);
    }

    /// <summary>
    /// Reports a file emission to the server.
    /// </summary>
    public void EmitFile(string path, string? message = null)
    {
        var msg = new GengoraFileMessage
        {
            Type = "generator/file",
            Action = "emit",
            Path = Path.GetFullPath(path),
            Message = message,
            SessionId = _sessionId,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
        SendMessage(msg);
    }

    private void SendMessage(object message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        Console.WriteLine(json);
    }
}

/// <summary>
/// Available actions for status messages.
/// </summary>
public enum GengoraAction
{
    Start,
    Analyzing,
    Generating,
    Complete,
    Error
}

/// <summary>
/// Status message sent to the Gengora server.
/// </summary>
public sealed class GengoraStatusMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("action")]
    public required string Action { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}

/// <summary>
/// File emission message sent to the Gengora server.
/// </summary>
public sealed class GengoraFileMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    
    [JsonPropertyName("action")]
    public required string Action { get; init; }
    
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}
