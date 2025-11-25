// ============================================================================
// Gengora.Generator.Abstractions
// Type-safe abstractions for building Gengora code generators
// ============================================================================
// Copyright (c) 2024 Blue IT Systems GmbH
// Licensed under the MIT License
// ============================================================================

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gengora.Generator
{
    /// <summary>
    /// Type-safe client for communicating with the Gengora VS Code extension server.
    /// Use this class to report status updates and file emissions from your generator.
    /// </summary>
    /// <example>
    /// <code>
    /// var sessionId = Environment.GetEnvironmentVariable("GENGORA_SESSION_ID");
    /// var client = new GengoraClient(sessionId);
    /// 
    /// client.ReportStatus(GengoraAction.Start, "Generator starting");
    /// // ... your generation logic ...
    /// client.EmitFile("/path/to/output.cs", "Generated entity class");
    /// client.ReportStatus(GengoraAction.Complete, "Generation finished");
    /// </code>
    /// </example>
    public sealed class GengoraClient : IGengoraClient
    {
        private readonly string _sessionId;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly TextWriter _output;

        /// <summary>
        /// Creates a new GengoraClient instance.
        /// </summary>
        /// <param name="sessionId">
        /// The session ID from the GENGORA_SESSION_ID environment variable.
        /// If null or empty, a new GUID will be generated.
        /// </param>
        /// <param name="output">
        /// Optional output writer. Defaults to Console.Out.
        /// Useful for testing.
        /// </param>
        public GengoraClient(string? sessionId = null, TextWriter? output = null)
        {
            _sessionId = string.IsNullOrEmpty(sessionId) 
                ? Guid.NewGuid().ToString("N") 
                : sessionId;
            _output = output ?? Console.Out;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Gets the current session ID.
        /// </summary>
        public string SessionId => _sessionId;

        /// <summary>
        /// Creates a GengoraClient using the session ID from the environment.
        /// </summary>
        /// <returns>A new GengoraClient instance.</returns>
        public static GengoraClient FromEnvironment()
        {
            var sessionId = Environment.GetEnvironmentVariable("GENGORA_SESSION_ID");
            return new GengoraClient(sessionId);
        }

        /// <summary>
        /// Reports a status update to the Gengora server.
        /// </summary>
        /// <param name="action">The type of status action.</param>
        /// <param name="message">A descriptive message for the status.</param>
        public void ReportStatus(GengoraAction action, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            var msg = new GengoraStatusMessage
            {
                Type = "generator/status",
                Action = GetActionString(action),
                Message = message,
                SessionId = _sessionId,
                Timestamp = DateTimeOffset.UtcNow.ToString("O")
            };
            SendMessage(msg);
        }

        /// <summary>
        /// Reports a file emission to the Gengora server.
        /// Call this after writing each generated file.
        /// </summary>
        /// <param name="path">The full path to the emitted file.</param>
        /// <param name="message">Optional descriptive message.</param>
        public void EmitFile(string path, string? message = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            var fullPath = Path.GetFullPath(path);
            var msg = new GengoraFileMessage
            {
                Type = "generator/file",
                Action = "emit",
                Path = fullPath,
                Message = message,
                SessionId = _sessionId,
                Timestamp = DateTimeOffset.UtcNow.ToString("O")
            };
            SendMessage(msg);
        }

        /// <summary>
        /// Reports an error to the Gengora server.
        /// This is a convenience method equivalent to ReportStatus(Error, message).
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">Optional exception to include details from.</param>
        public void ReportError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null 
                ? $"{message}: {exception.Message}" 
                : message;
            ReportStatus(GengoraAction.Error, fullMessage);
        }

        /// <summary>
        /// Reports completion to the Gengora server.
        /// This is a convenience method equivalent to ReportStatus(Complete, message).
        /// </summary>
        /// <param name="message">The completion message.</param>
        public void ReportComplete(string message = "Generation completed successfully")
        {
            ReportStatus(GengoraAction.Complete, message);
        }

        private void SendMessage(object message)
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            _output.WriteLine(json);
            _output.Flush();
        }

        private static string GetActionString(GengoraAction action)
        {
            return action switch
            {
                GengoraAction.Start => "start",
                GengoraAction.Analyzing => "analyzing",
                GengoraAction.Generating => "generating",
                GengoraAction.Complete => "complete",
                GengoraAction.Error => "error",
                _ => action.ToString().ToLowerInvariant()
            };
        }
    }

    /// <summary>
    /// Interface for Gengora client implementations.
    /// Useful for dependency injection and testing.
    /// </summary>
    public interface IGengoraClient
    {
        /// <summary>
        /// Gets the current session ID.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Reports a status update to the server.
        /// </summary>
        void ReportStatus(GengoraAction action, string message);

        /// <summary>
        /// Reports a file emission to the server.
        /// </summary>
        void EmitFile(string path, string? message = null);

        /// <summary>
        /// Reports an error to the server.
        /// </summary>
        void ReportError(string message, Exception? exception = null);

        /// <summary>
        /// Reports completion to the server.
        /// </summary>
        void ReportComplete(string message = "Generation completed successfully");
    }

    /// <summary>
    /// Available actions for status messages.
    /// </summary>
    public enum GengoraAction
    {
        /// <summary>
        /// Generator has started execution.
        /// </summary>
        Start,

        /// <summary>
        /// Generator is analyzing the workspace or inputs.
        /// </summary>
        Analyzing,

        /// <summary>
        /// Generator is actively generating code.
        /// </summary>
        Generating,

        /// <summary>
        /// Generator has completed successfully.
        /// </summary>
        Complete,

        /// <summary>
        /// Generator encountered an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Status message sent to the Gengora server.
    /// </summary>
    internal sealed class GengoraStatusMessage
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
    internal sealed class GengoraFileMessage
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
}
