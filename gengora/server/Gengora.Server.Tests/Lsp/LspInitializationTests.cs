namespace Gengora.Server.Tests.Lsp;

using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Gengora.Server.Core;
using Gengora.Server.Lsp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StreamJsonRpc;

/// <summary>
/// Integration Tests For LSP Initialization.
/// These Tests Verify That The Server Can Handle Real LSP Initialize Requests
/// As Sent By VS Code's Language Client.
/// </summary>
public sealed class LspInitializationTests
{
    /// <summary>
    /// Tests That The Server Can Deserialize A Full LSP Initialize Request
    /// With All 9 Parameters That VS Code Sends.
    /// This Is The Exact Format That Was Causing The "initialize/9" Error.
    /// </summary>
    [Test]
    public async Task InitializeRequest_WithFullVsCodeParams_DeserializesCorrectly()
    {
        // Arrange - This Is The Exact JSON Structure VS Code Sends
        var initializeParams = new
        {
            processId = 12345,
            clientInfo = new
            {
                name = "Visual Studio Code",
                version = "1.85.0"
            },
            locale = "en",
            rootPath = "/test/workspace",
            rootUri = "file:///test/workspace",
            initializationOptions = new
            {
                capabilities = new
                {
                    statusBar = true,
                    diagnostics = true
                }
            },
            capabilities = new
            {
                workspace = new
                {
                    workspaceFolders = true,
                    configuration = true
                },
                textDocument = new { },
                window = new { },
                general = new { },
                experimental = (object?)null
            },
            trace = "verbose",
            workspaceFolders = new[]
            {
                new
                {
                    uri = "file:///test/workspace",
                    name = "test-workspace"
                }
            }
        };

        var json = JsonSerializer.Serialize(initializeParams);

        // Act - Deserialize Using The Same Options The Server Uses
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var result = JsonSerializer.Deserialize<InitializeParams>(json, options);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ProcessId).IsEqualTo(12345);
        await Assert.That(result.ClientInfo).IsNotNull();
        await Assert.That(result.ClientInfo!.Name).IsEqualTo("Visual Studio Code");
        await Assert.That(result.ClientInfo.Version).IsEqualTo("1.85.0");
        await Assert.That(result.Locale).IsEqualTo("en");
        await Assert.That(result.RootPath).IsEqualTo("/test/workspace");
        await Assert.That(result.RootUri).IsEqualTo("file:///test/workspace");
        await Assert.That(result.Trace).IsEqualTo("verbose");
        await Assert.That(result.WorkspaceFolders).IsNotNull();
        await Assert.That(result.WorkspaceFolders!.Count).IsEqualTo(1);
        await Assert.That(result.EffectiveRootPath).IsEqualTo("/test/workspace");
    }

    /// <summary>
    /// Tests That SystemTextJsonFormatter With UseSingleObjectParameterDeserialization
    /// Correctly Routes The Initialize Request To The Handler Method.
    /// This Simulates The Full StreamJsonRpc Pipeline.
    /// </summary>
    [Test]
    public async Task StreamJsonRpc_WithSystemTextJsonFormatter_HandlesInitializeRequest()
    {
        // Arrange - Create Pipes For Communication
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        // Configure Server Side With The Same Settings As GengoraLanguageServer
        var serverFormatter = new SystemTextJsonFormatter();
        serverFormatter.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        var serverHandler = new HeaderDelimitedMessageHandler(
            serverToClient.Writer.AsStream(),
            clientToServer.Reader.AsStream(),
            serverFormatter);

        var serverRpc = new JsonRpc(serverHandler);

        // Create A Mock Target That Captures The Initialize Call
        var mockTarget = new MockLspTarget();
        serverRpc.AddLocalRpcTarget(mockTarget, new JsonRpcTargetOptions
        {
            AllowNonPublicInvocation = false,
            UseSingleObjectParameterDeserialization = true
        });

        serverRpc.StartListening();

        // Configure Client Side - Use SAME formatter settings
        var clientFormatter = new SystemTextJsonFormatter();
        clientFormatter.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        var clientHandler = new HeaderDelimitedMessageHandler(
            clientToServer.Writer.AsStream(),
            serverToClient.Reader.AsStream(),
            clientFormatter);

        var clientRpc = new JsonRpc(clientHandler);
        clientRpc.StartListening();

        // Act - Send Initialize Request With Full VS Code Parameters
        // Use anonymous object to simulate how VS Code client sends the params
        var initParams = new
        {
            processId = 99999,
            clientInfo = new { name = "Test Client", version = "1.0.0" },
            locale = "en-US",
            rootPath = "/test/path",
            rootUri = "file:///test/path",
            initializationOptions = (object?)null,
            capabilities = new { },
            trace = "off",
            workspaceFolders = new[] { new { uri = "file:///test/path", name = "test" } }
        };

        try
        {
            // This is exactly how vscode-languageclient sends the request:
            // As a named parameters object (not positional array)
            var result = await clientRpc.InvokeWithParameterObjectAsync<InitializeResult>(
                "initialize",
                initParams,
                CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.ServerInfo.Name).IsEqualTo("Mock Server");
            await Assert.That(mockTarget.ReceivedParams).IsNotNull();
            await Assert.That(mockTarget.ReceivedParams!.ProcessId).IsEqualTo(99999);
            await Assert.That(mockTarget.ReceivedParams.RootUri).IsEqualTo("file:///test/path");
            await Assert.That(mockTarget.ReceivedParams.ClientInfo!.Name).IsEqualTo("Test Client");
        }
        finally
        {
            clientRpc.Dispose();
            serverRpc.Dispose();
        }
    }

    /// <summary>
    /// Tests That The InitializeParams EffectiveRootPath Property Correctly
    /// Converts File URIs To Local Paths.
    /// </summary>
    [Test]
    public async Task InitializeParams_EffectiveRootPath_ConvertsFileUri()
    {
        // Arrange
        var paramsWithUri = new InitializeParams
        {
            RootUri = "file:///Users/test/workspace",
            RootPath = "/fallback/path"
        };

        var paramsWithPathOnly = new InitializeParams
        {
            RootUri = null,
            RootPath = "/only/root/path"
        };

        // Act & Assert
        await Assert.That(paramsWithUri.EffectiveRootPath).IsEqualTo("/Users/test/workspace");
        await Assert.That(paramsWithPathOnly.EffectiveRootPath).IsEqualTo("/only/root/path");
    }

    /// <summary>
    /// Mock LSP Target For Testing StreamJsonRpc Method Routing.
    /// </summary>
    private sealed class MockLspTarget
    {
        public InitializeParams? ReceivedParams { get; private set; }

        [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
        public Task<InitializeResult> InitializeAsync(InitializeParams @params, CancellationToken cancellationToken)
        {
            this.ReceivedParams = @params;

            return Task.FromResult(new InitializeResult
            {
                ServerInfo = new ServerInfo
                {
                    Name = "Mock Server",
                    Version = "1.0.0"
                },
                Capabilities = new ServerCapabilities
                {
                    StateNotifications = true,
                    Diagnostics = true,
                    FileWatching = true
                }
            });
        }
    }
}
