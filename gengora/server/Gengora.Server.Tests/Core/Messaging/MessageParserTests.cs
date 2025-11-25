namespace Gengora.Server.Tests.Core.Messaging;

using Gengora.Server.Core.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For MessageParser.
/// Verifies JSON Parsing And Validation Per Specification R6.*.
/// </summary>
public class MessageParserTests
{
    private readonly MessageParser _Parser;

    public MessageParserTests()
    {
        this._Parser = new MessageParser(NullLogger<MessageParser>.Instance);
    }

    [Test]
    public async Task Parse_ValidStatusMessage_ShouldSucceed()
    {
        // Arrange
        var json = """
            {
                "type": "generator/status",
                "action": "start",
                "message": "Starting",
                "session_id": "test-session-123",
                "timestamp": "2024-01-01T00:00:00Z"
            }
            """;

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo("generator/status");
        await Assert.That(result.Action).IsEqualTo("start");
        await Assert.That(result.Message).IsEqualTo("Starting");
        await Assert.That(result.SessionId).IsEqualTo("test-session-123");
        await Assert.That(result.IsStatusMessage).IsTrue();
    }

    [Test]
    public async Task Parse_ValidFileEmitMessage_ShouldSucceed()
    {
        // Arrange
        var json = """
            {
                "type": "generator/file",
                "action": "emit",
                "path": "/path/to/generated.cs",
                "session_id": "test-session-123",
                "timestamp": "2024-01-01T00:00:00Z"
            }
            """;

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo("generator/file");
        await Assert.That(result.Action).IsEqualTo("emit");
        await Assert.That(result.Path).IsEqualTo("/path/to/generated.cs");
        await Assert.That(result.IsFileEmitMessage).IsTrue();
    }

    [Test]
    public async Task Parse_InvalidJson_ShouldReturnNull()
    {
        // Arrange
        var json = "this is not valid json";

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Parse_MissingRequiredField_ShouldReturnNull()
    {
        // Arrange - Missing session_id
        var json = """
            {
                "type": "generator/status",
                "action": "start",
                "timestamp": "2024-01-01T00:00:00Z"
            }
            """;

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Parse_EmptyString_ShouldReturnNull()
    {
        // Arrange
        var json = "";

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Parse_NullString_ShouldReturnNull()
    {
        // Arrange
        string? json = null;

        // Act
        var result = this._Parser.Parse(json!);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Parse_FileEmitWithoutPath_ShouldReturnNull()
    {
        // Arrange - R6.16: File Emit Must Have Path
        var json = """
            {
                "type": "generator/file",
                "action": "emit",
                "session_id": "test-session-123",
                "timestamp": "2024-01-01T00:00:00Z"
            }
            """;

        // Act
        var result = this._Parser.Parse(json);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ValidateSessionId_MatchingSessionId_ShouldReturnTrue()
    {
        // Arrange
        var message = new GeneratorMessage
        {
            Type = "generator/status",
            Action = "start",
            SessionId = "expected-session-id",
            Timestamp = "2024-01-01T00:00:00Z"
        };

        // Act
        var result = this._Parser.ValidateSessionId(message, "expected-session-id");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidateSessionId_MismatchedSessionId_ShouldReturnFalse()
    {
        // Arrange - R6.7: Session ID Mismatch
        var message = new GeneratorMessage
        {
            Type = "generator/status",
            Action = "start",
            SessionId = "wrong-session-id",
            Timestamp = "2024-01-01T00:00:00Z"
        };

        // Act
        var result = this._Parser.ValidateSessionId(message, "expected-session-id");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPathInProtectedDirectory_PathInSourceTree_ShouldReturnTrue()
    {
        // Arrange - R6.16: Protected Directory Check
        var filePath = "/workspace/generator/src/output.cs";
        var sourceDirectory = "/workspace/generator/src";

        // Act
        var result = this._Parser.IsPathInProtectedDirectory(filePath, sourceDirectory);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPathInProtectedDirectory_PathOutsideSourceTree_ShouldReturnFalse()
    {
        // Arrange
        var filePath = "/workspace/output/generated.cs";
        var sourceDirectory = "/workspace/generator/src";

        // Act
        var result = this._Parser.IsPathInProtectedDirectory(filePath, sourceDirectory);

        // Assert
        await Assert.That(result).IsFalse();
    }
}
