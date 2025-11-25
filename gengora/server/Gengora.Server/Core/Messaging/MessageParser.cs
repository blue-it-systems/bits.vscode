namespace Gengora.Server.Core.Messaging;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Parses And Validates Generator Messages.
/// Implements Specification R6.13-R6.19.
/// </summary>
public sealed class MessageParser
{
    private readonly ILogger<MessageParser> _Logger;
    private readonly JsonSerializerOptions _JsonOptions;

    public MessageParser(ILogger<MessageParser> logger)
    {
        this._Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        this._JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    /// <summary>
    /// Attempts To Parse A JSON Line Into A Generator Message.
    /// Returns Null If Parsing Fails Or Message Is Invalid.
    /// </summary>
    public GeneratorMessage? Parse(string jsonLine)
    {
        if (String.IsNullOrWhiteSpace(jsonLine))
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<GeneratorMessage>(jsonLine, this._JsonOptions);

            if (message == null)
            {
                this._Logger.LogDebug("Parsed Null Message From: {JsonLine}", jsonLine);

                return null;
            }

            // R6.14: Validate Message Format
            if (!this.IsValidMessage(message))
            {
                this._Logger.LogDebug("Invalid Message Format: {JsonLine}", jsonLine);

                return null;
            }

            this._Logger.LogDebug
            (
                "Parsed Message: Type={Type}, Action={Action}, SessionId={SessionId}",
                message.Type,
                message.Action,
                message.SessionId
            );

            return message;
        }
        catch (JsonException ex)
        {
            // R6.14: Invalid JSON Handled Gracefully
            this._Logger.LogDebug(ex, "Failed To Parse JSON: {JsonLine}", jsonLine);

            return null;
        }
    }

    /// <summary>
    /// Validates Session ID Against Expected Value.
    /// Per R6.7: Server MUST Validate Session ID In All Incoming Messages.
    /// </summary>
    public bool ValidateSessionId(GeneratorMessage message, string expectedSessionId)
    {
        if (message == null)
        {
            return false;
        }

        var isValid = String.Equals(message.SessionId, expectedSessionId, StringComparison.Ordinal);

        if (!isValid)
        {
            // R8.5: Session ID Mismatch Results In Silent Discard
            this._Logger.LogDebug
            (
                "Session ID Mismatch: Expected={Expected}, Actual={Actual}",
                expectedSessionId,
                message.SessionId
            );
        }

        return isValid;
    }

    /// <summary>
    /// Validates That A Message Has All Required Fields.
    /// </summary>
    private bool IsValidMessage(GeneratorMessage message)
    {
        if (String.IsNullOrWhiteSpace(message.Type))
        {
            return false;
        }

        if (String.IsNullOrWhiteSpace(message.Action))
        {
            return false;
        }

        if (String.IsNullOrWhiteSpace(message.SessionId))
        {
            return false;
        }

        if (String.IsNullOrWhiteSpace(message.Timestamp))
        {
            return false;
        }

        // File Emit Messages Must Have A Path
        if (message.IsFileEmitMessage && String.IsNullOrWhiteSpace(message.Path))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks If A File Path Is Within A Protected Directory (Generator Source Tree).
    /// Per R6.16: Server MUST Verify Emitted File Path Is NOT Within Generator Source Directory.
    /// </summary>
    public bool IsPathInProtectedDirectory(string filePath, string generatorSourceDirectory)
    {
        if (String.IsNullOrWhiteSpace(filePath) || String.IsNullOrWhiteSpace(generatorSourceDirectory))
        {
            return false;
        }

        var normalizedFilePath = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedSourceDir = Path.GetFullPath(generatorSourceDirectory).TrimEnd(Path.DirectorySeparatorChar);

        // Check If File Path Starts With Generator Source Directory
        var isInProtected = normalizedFilePath.StartsWith
        (
            normalizedSourceDir + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );

        if (isInProtected)
        {
            // R6.11: Warn User If Generator Attempts To Write To Its Own Source Directory
            this._Logger.LogWarning
            (
                "Generator Attempted To Emit File Within Source Tree: {FilePath} (Protected: {ProtectedDir})",
                filePath,
                generatorSourceDirectory
            );
        }

        return isInProtected;
    }
}
