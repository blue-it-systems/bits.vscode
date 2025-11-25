import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
let statusBarItem: vscode.StatusBarItem;
let outputChannel: vscode.OutputChannel;
let currentState: string = 'Idle';
let extensionContext: vscode.ExtensionContext;

// File logger for debugging
const LOG_FILE_PATH = path.join(os.tmpdir(), 'gengora-extension.log');

function logToFile(message: string): void {
    const timestamp = new Date().toISOString();
    const logMessage = `[${timestamp}] ${message}\n`;
    try {
        fs.appendFileSync(LOG_FILE_PATH, logMessage);
    } catch {
        // Ignore file write errors
    }
}

function log(message: string): void {
    logToFile(message);
    if (outputChannel) {
        outputChannel.appendLine(message);
    }
}

// State Icons
const STATE_ICONS: Record<string, string> = {
    'Idle': 'zZ',
    'GeneratorFound': '$(search)',
    'Compiling': '$(sync~spin)',
    'Ready': '$(check)',
    'Running': '$(play)',
    'Error': '$(error)',
    'Stopped': '$(debug-stop)'
};

// State Colors
const STATE_COLORS: Record<string, vscode.ThemeColor | undefined> = {
    'Idle': undefined,
    'GeneratorFound': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Compiling': new vscode.ThemeColor('statusBarItem.warningForeground'),
    'Ready': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Running': new vscode.ThemeColor('statusBarItem.prominentForeground'),
    'Error': new vscode.ThemeColor('statusBarItem.errorForeground'),
    'Stopped': undefined
};

// Log Levels
const LOG_LEVELS = ['trace', 'debug', 'info', 'warning', 'error'] as const;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    // LOG TO FILE FIRST - before anything else
    logToFile('========================================');
    logToFile('ACTIVATE CALLED');
    logToFile(`Extension Path: ${context.extensionPath}`);
    logToFile(`Process ID: ${process.pid}`);
    logToFile(`VS Code Version: ${vscode.version}`);
    
    try {
        extensionContext = context;
        outputChannel = vscode.window.createOutputChannel('Gengora');
        context.subscriptions.push(outputChannel);

        log('Gengora Extension Activating...');
        log(`Workspace Folders: ${vscode.workspace.workspaceFolders?.map(f => f.uri.fsPath).join(', ') ?? 'NONE'}`);
        outputChannel.show(true); // Force show output channel

        // Create Status Bar Item - Now With Quick Pick Menu
        statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        statusBarItem.command = 'gengora.showQuickPick';
        context.subscriptions.push(statusBarItem);

        updateStatusBar('Idle');
        log('Status bar item created and shown');

        // Register Commands
        context.subscriptions.push(
            vscode.commands.registerCommand('gengora.showQuickPick', showQuickPickMenu),
            vscode.commands.registerCommand('gengora.start', startCommand),
            vscode.commands.registerCommand('gengora.recompile', recompileCommand),
            vscode.commands.registerCommand('gengora.stop', stopCommand),
            vscode.commands.registerCommand('gengora.restart', restartCommand),
            vscode.commands.registerCommand('gengora.showOutput', () => outputChannel.show()),
            vscode.commands.registerCommand('gengora.setLogLevel', setLogLevelCommand),
            vscode.commands.registerCommand('gengora.createSampleGenerator', () => createSampleGeneratorCommand(context))
        );
        log('Commands registered');

        // Check If Auto-Start Is Enabled
        const config = vscode.workspace.getConfiguration('gengora');
        const autoStart = config.get<boolean>('autoStart', true);
        log(`AutoStart config: ${autoStart}`);

        if (autoStart) {
            try {
                await startLanguageServer(context);
            } catch (error) {
                log(`ERROR starting language server: ${error}`);
                vscode.window.showErrorMessage(`Gengora: Failed to start server - ${error}`);
            }
        }

        log('Gengora Extension Activated Successfully');
        log('========================================');
    } catch (error) {
        logToFile(`FATAL ERROR in activate: ${error}`);
        throw error;
    }
}

export function deactivate(): Thenable<void> | undefined {
    logToFile('DEACTIVATE CALLED');
    if (!client) {
        return undefined;
    }

    return client.stop();
}

/**
 * Shows the quick pick menu when clicking on the status bar icon.
 * Provides access to all Gengora commands and settings.
 */
async function showQuickPickMenu(): Promise<void> {
    const config = vscode.workspace.getConfiguration('gengora');
    const currentLogLevel = config.get<string>('logLevel', 'debug');
    const isServerRunning = client !== undefined;

    interface QuickPickItemWithAction extends vscode.QuickPickItem {
        action: string;
    }

    const items: QuickPickItemWithAction[] = [
        {
            label: '$(output) Show Output',
            description: 'Open the Gengora output channel',
            action: 'showOutput'
        },
        {
            label: '',
            kind: vscode.QuickPickItemKind.Separator,
            action: ''
        }
    ];

    // Add state-dependent commands
    if (!isServerRunning) {
        items.push({
            label: '$(play) Start Server',
            description: 'Start the Gengora language server',
            action: 'start'
        });
    } else {
        items.push(
            {
                label: '$(refresh) Recompile Generator',
                description: 'Force recompilation of the generator',
                action: 'recompile'
            },
            {
                label: '$(debug-stop) Stop Generator',
                description: 'Stop the current generator',
                action: 'stop'
            },
            {
                label: '$(debug-restart) Restart Server',
                description: 'Restart the language server',
                action: 'restart'
            }
        );
    }

    items.push(
        {
            label: '',
            kind: vscode.QuickPickItemKind.Separator,
            action: ''
        },
        {
            label: `$(settings-gear) Log Level: ${currentLogLevel}`,
            description: 'Change the logging verbosity',
            action: 'setLogLevel'
        },
        {
            label: '$(new-folder) Create Sample Generator',
            description: 'Scaffold a new generator project in workspace',
            action: 'createSampleGenerator'
        },
        {
            label: '',
            kind: vscode.QuickPickItemKind.Separator,
            action: ''
        },
        {
            label: `$(info) Status: ${currentState}`,
            description: 'Current generator state',
            action: 'info'
        }
    );

    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Gengora Commands',
        title: 'Gengora - Live Code Generation'
    });

    if (!selected) {
        return;
    }

    switch (selected.action) {
        case 'showOutput':
            outputChannel.show();
            break;
        case 'start':
            await vscode.commands.executeCommand('gengora.start');
            break;
        case 'recompile':
            await vscode.commands.executeCommand('gengora.recompile');
            break;
        case 'stop':
            await vscode.commands.executeCommand('gengora.stop');
            break;
        case 'restart':
            await vscode.commands.executeCommand('gengora.restart');
            break;
        case 'setLogLevel':
            await vscode.commands.executeCommand('gengora.setLogLevel');
            break;
        case 'createSampleGenerator':
            await vscode.commands.executeCommand('gengora.createSampleGenerator');
            break;
        case 'info':
            vscode.window.showInformationMessage(`Gengora State: ${currentState}`);
            break;
    }
}

/**
 * Command to change the log level.
 */
async function setLogLevelCommand(): Promise<void> {
    const config = vscode.workspace.getConfiguration('gengora');
    const currentLogLevel = config.get<string>('logLevel', 'debug');

    const items = LOG_LEVELS.map(level => ({
        label: level.charAt(0).toUpperCase() + level.slice(1),
        description: level === currentLogLevel ? '(current)' : undefined,
        picked: level === currentLogLevel,
        level
    }));

    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Select Log Level',
        title: 'Gengora Log Level'
    });

    if (selected) {
        await config.update('logLevel', selected.level, vscode.ConfigurationTarget.Global);
        outputChannel.appendLine(`Log level changed to: ${selected.level}`);
        vscode.window.showInformationMessage(`Gengora: Log level set to ${selected.level}`);
        
        // Notify server of log level change if connected
        if (client) {
            try {
                await client.sendNotification('gengora/setLogLevel', { level: selected.level });
            } catch {
                // Server might not support this notification yet
            }
        }
    }
}

/**
 * Command to start the language server.
 */
async function startCommand(): Promise<void> {
    if (client) {
        vscode.window.showInformationMessage('Gengora: Server is already running');
        return;
    }

    await startLanguageServer(extensionContext);
    vscode.window.showInformationMessage('Gengora: Server started');
}

/**
 * Command to restart the language server.
 */
async function restartCommand(): Promise<void> {
    if (client) {
        await client.stop();
        client = undefined;
    }

    await startLanguageServer(extensionContext);
    vscode.window.showInformationMessage('Gengora: Server restarted');
}

/**
 * Command to create a sample generator project in the current workspace.
 */
async function createSampleGeneratorCommand(context: vscode.ExtensionContext): Promise<void> {
    // Check if workspace is open
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showErrorMessage('Gengora: Please open a workspace folder first');
        return;
    }

    // Let user select target folder if multiple workspace folders
    let targetFolder: vscode.Uri;
    if (workspaceFolders.length === 1) {
        targetFolder = workspaceFolders[0].uri;
    } else {
        const selected = await vscode.window.showWorkspaceFolderPick({
            placeHolder: 'Select workspace folder for the sample generator'
        });
        if (!selected) {
            return;
        }
        targetFolder = selected.uri;
    }

    // Show multi-select quick pick for generator types
    interface GeneratorQuickPickItem extends vscode.QuickPickItem {
        id: 'simple' | 'advanced';
    }

    const generatorOptions: GeneratorQuickPickItem[] = [
        {
            id: 'simple',
            label: '$(file-code) Simple Generator',
            description: 'Basic file generator',
            detail: 'A minimal generator that creates companion files for each .cs file. Perfect for learning Gengora.',
            picked: true
        },
        {
            id: 'advanced',
            label: '$(symbol-class) Advanced Generator',
            description: 'Class-based code generator',
            detail: 'A more sophisticated generator that parses C# classes and generates extension methods.',
            picked: false
        }
    ];

    const selectedGenerators = await vscode.window.showQuickPick(generatorOptions, {
        canPickMany: true,
        placeHolder: 'Select generator template(s) to create',
        title: 'Gengora: Create Sample Generator'
    });

    if (!selectedGenerators || selectedGenerators.length === 0) {
        return;
    }

    // Helper function to convert string to Uint8Array
    const stringToBytes = (str: string): Uint8Array => {
        const bytes = new Uint8Array(str.length);
        for (let i = 0; i < str.length; i++) {
            bytes[i] = str.charCodeAt(i);
        }
        return bytes;
    };

    const createdProjects: string[] = [];

    for (const generator of selectedGenerators) {
        try {
            if (generator.id === 'simple') {
                await createSimpleGenerator(targetFolder, stringToBytes);
                createdProjects.push('SimpleGenerator');
            } else if (generator.id === 'advanced') {
                await createAdvancedGenerator(targetFolder, stringToBytes);
                createdProjects.push('AdvancedGenerator');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Gengora: Failed to create ${generator.label} - ${error}`);
        }
    }

    if (createdProjects.length > 0) {
        vscode.window.showInformationMessage(
            `Gengora: Created sample generator(s): ${createdProjects.join(', ')}`,
            'Open Folder'
        ).then((selection: string | undefined) => {
            if (selection === 'Open Folder') {
                const firstProject = vscode.Uri.joinPath(targetFolder, createdProjects[0]);
                vscode.commands.executeCommand('revealInExplorer', firstProject);
            }
        });

        log(`Created sample generator projects: ${createdProjects.join(', ')}`);
    }
}

/**
 * Creates a simple generator project.
 */
async function createSimpleGenerator(
    targetFolder: vscode.Uri,
    stringToBytes: (str: string) => Uint8Array
): Promise<void> {
    const projectName = 'SimpleGenerator';
    const projectFolder = vscode.Uri.joinPath(targetFolder, projectName);

    // Check if folder already exists
    try {
        await vscode.workspace.fs.stat(projectFolder);
        throw new Error(`Folder '${projectName}' already exists`);
    } catch (e) {
        if (e instanceof Error && e.message.includes('already exists')) {
            throw e;
        }
        // Folder doesn't exist - good
    }

    await vscode.workspace.fs.createDirectory(projectFolder);

    const csprojContent = `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- Gengora Marker: This project is a code generator -->
  <PropertyGroup>
    <GengoraGeneratorMarker>true</GengoraGeneratorMarker>
  </PropertyGroup>

</Project>
`;

    const programContent = `using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace SimpleGenerator;

/// <summary>
/// Simple Gengora code generator.
/// Reads JSON input from stdin and writes generated C# code to files.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Read input from stdin
        var inputJson = Console.In.ReadToEnd();
        
        // Parse input (Gengora sends context as JSON)
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var input = JsonSerializer.Deserialize<GeneratorInput>(inputJson, options);
        
        if (input?.Files == null || input.Files.Count == 0)
        {
            Console.Error.WriteLine("No files provided in input");
            return;
        }

        // Generate code for each input file
        foreach (var file in input.Files)
        {
            GenerateCode(file, input.OutputDirectory);
        }
    }

    private static void GenerateCode(InputFile file, string? outputDir)
    {
        // Example: Generate a companion file for each .cs file
        var outputPath = outputDir ?? Path.GetDirectoryName(file.Path) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(file.Path);
        var outputFile = Path.Combine(outputPath, $"{fileName}.Generated.cs");

        var code = $@"// <auto-generated>
// Generated by SimpleGenerator
// Source: {file.Path}
// </auto-generated>

namespace Generated;

public static partial class {fileName}Extensions
{{
    public static string GetSourcePath() => @""{file.Path}"";
}}
";

        File.WriteAllText(outputFile, code);
        
        // Output the emitted file path for Gengora to track
        Console.WriteLine($"EMIT: {outputFile}");
    }
}

// Input types that Gengora sends
public record GeneratorInput(List<InputFile> Files, string? OutputDirectory);
public record InputFile(string Path, string Content);
`;

    const readmeContent = `# Simple Generator

A minimal Gengora code generator that creates companion files.

## How it works

1. Gengora watches for changes in your workspace
2. When .cs files change, it compiles and runs this generator
3. The generator reads input from stdin (JSON) and writes files to disk
4. Generated files are tracked and refreshed automatically

## Input Format

The generator receives JSON input on stdin:
\`\`\`json
{
  "files": [
    { "path": "/path/to/file.cs", "content": "..." }
  ],
  "outputDirectory": "/path/to/output"
}
\`\`\`

## Output Convention

Print \`EMIT: /path/to/generated/file.cs\` to stdout for each generated file.
`;

    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, `${projectName}.csproj`),
        stringToBytes(csprojContent)
    );
    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, 'Program.cs'),
        stringToBytes(programContent)
    );
    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, 'README.md'),
        stringToBytes(readmeContent)
    );
}

/**
 * Creates an advanced generator project with class parsing.
 */
async function createAdvancedGenerator(
    targetFolder: vscode.Uri,
    stringToBytes: (str: string) => Uint8Array
): Promise<void> {
    const projectName = 'AdvancedGenerator';
    const projectFolder = vscode.Uri.joinPath(targetFolder, projectName);

    // Check if folder already exists
    try {
        await vscode.workspace.fs.stat(projectFolder);
        throw new Error(`Folder '${projectName}' already exists`);
    } catch (e) {
        if (e instanceof Error && e.message.includes('already exists')) {
            throw e;
        }
        // Folder doesn't exist - good
    }

    await vscode.workspace.fs.createDirectory(projectFolder);

    const csprojContent = `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- Gengora Marker: This project is a code generator -->
  <PropertyGroup>
    <GengoraGeneratorMarker>true</GengoraGeneratorMarker>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
  </ItemGroup>

</Project>
`;

    const programContent = `using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AdvancedGenerator;

/// <summary>
/// Advanced Gengora code generator that uses Roslyn to parse C# code.
/// Generates extension methods for classes with properties.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Read input from stdin
        var inputJson = Console.In.ReadToEnd();
        
        // Parse input
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var input = JsonSerializer.Deserialize<GeneratorInput>(inputJson, options);
        
        if (input?.Files == null || input.Files.Count == 0)
        {
            Console.Error.WriteLine("No files provided in input");
            return;
        }

        // Generate code for each input file
        foreach (var file in input.Files)
        {
            GenerateCode(file, input.OutputDirectory);
        }
    }

    private static void GenerateCode(InputFile file, string? outputDir)
    {
        // Parse the C# file using Roslyn
        var tree = CSharpSyntaxTree.ParseText(file.Content);
        var root = tree.GetCompilationUnitRoot();

        // Find all classes with properties
        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Members.OfType<PropertyDeclarationSyntax>().Any())
            .ToList();

        if (classes.Count == 0)
        {
            return; // No classes with properties to process
        }

        var outputPath = outputDir ?? Path.GetDirectoryName(file.Path) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(file.Path);
        var outputFile = Path.Combine(outputPath, $"{fileName}.Extensions.Generated.cs");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by AdvancedGenerator using Roslyn");
        sb.AppendLine($"// Source: {file.Path}");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();

        // Get namespace from source file
        var namespaceDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var namespaceName = namespaceDecl?.Name.ToString() ?? "Generated";
        
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>().ToList();

            sb.AppendLine($"public static class {className}Extensions");
            sb.AppendLine("{");
            
            // Generate a With method for each property
            foreach (var prop in properties)
            {
                var propName = prop.Identifier.Text;
                var propType = prop.Type.ToString();
                
                sb.AppendLine($"    /// <summary>Creates a copy with a new {propName} value.</summary>");
                sb.AppendLine($"    public static {className} With{propName}(this {className} source, {propType} value)");
                sb.AppendLine("    {");
                sb.AppendLine($"        return source with {{ {propName} = value }};");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Generate a ToDebugString method
            sb.AppendLine($"    /// <summary>Returns a debug string with all property values.</summary>");
            sb.AppendLine($"    public static string ToDebugString(this {className} source)");
            sb.AppendLine("    {");
            sb.Append("        return $\"");
            sb.Append(className);
            sb.Append(" {{ ");
            sb.Append(string.Join(", ", properties.Select(p => $"{p.Identifier.Text} = {{source.{p.Identifier.Text}}}")));
            sb.AppendLine(" }}\";");
            sb.AppendLine("    }");
            
            sb.AppendLine("}");
            sb.AppendLine();
        }

        File.WriteAllText(outputFile, sb.ToString());
        Console.WriteLine($"EMIT: {outputFile}");
    }
}

// Input types that Gengora sends
public record GeneratorInput(List<InputFile> Files, string? OutputDirectory);
public record InputFile(string Path, string Content);
`;

    const readmeContent = `# Advanced Generator

A Roslyn-powered Gengora code generator that creates extension methods for classes.

## Features

- Uses **Microsoft.CodeAnalysis.CSharp** to parse C# syntax
- Generates \`With*\` methods for immutable updates (works with records)
- Generates \`ToDebugString()\` for debugging

## Example

Given this input:
\`\`\`csharp
public record Person(string Name, int Age);
\`\`\`

Generates:
\`\`\`csharp
public static class PersonExtensions
{
    public static Person WithName(this Person source, string value)
        => source with { Name = value };
    
    public static Person WithAge(this Person source, int value)
        => source with { Age = value };
    
    public static string ToDebugString(this Person source)
        => $"Person {{ Name = {source.Name}, Age = {source.Age} }}";
}
\`\`\`

## How it works

1. Parses C# files using Roslyn's syntax API
2. Finds classes/records with properties
3. Generates extension methods for each property
`;

    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, `${projectName}.csproj`),
        stringToBytes(csprojContent)
    );
    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, 'Program.cs'),
        stringToBytes(programContent)
    );
    await vscode.workspace.fs.writeFile(
        vscode.Uri.joinPath(projectFolder, 'README.md'),
        stringToBytes(readmeContent)
    );
}

async function startLanguageServer(context: vscode.ExtensionContext): Promise<void> {
    log('Starting Language Server...');
    
    const config = vscode.workspace.getConfiguration('gengora');

    // Determine Server Path
    let serverPath = config.get<string>('serverPath', '');

    if (!serverPath) {
        // Use Bundled Server
        serverPath = context.asAbsolutePath(
            path.join('server', 'Gengora.Server.dll')
        );
    }

    log(`Server Path: ${serverPath}`);
    
    // Check if server DLL exists
    if (!fs.existsSync(serverPath)) {
        const errorMsg = `Server DLL not found at: ${serverPath}`;
        log(`ERROR: ${errorMsg}`);
        throw new Error(errorMsg);
    }
    log('Server DLL exists: OK');

    // Check if dotnet is available
    log('Checking dotnet availability...');

    // Server Options
    const serverOptions: ServerOptions = {
        run: {
            command: 'dotnet',
            args: [serverPath],
            transport: TransportKind.stdio
        },
        debug: {
            command: 'dotnet',
            args: [serverPath],
            transport: TransportKind.stdio
        }
    };

    // Client Options
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'csharp' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.cs')
        },
        outputChannel: outputChannel,
        initializationOptions: {
            capabilities: {
                statusBar: true,
                diagnostics: true
            },
            logLevel: config.get<string>('logLevel', 'debug')
        }
    };

    log('Creating Language Client...');
    
    // Create And Start Client
    client = new LanguageClient(
        'gengora',
        'Gengora Language Server',
        serverOptions,
        clientOptions
    );

    // Handle Notifications
    client.onNotification('gengora/stateChanged', handleStateChanged);
    client.onNotification('gengora/diagnostics', handleDiagnostics);
    client.onNotification('gengora/fileEmitted', handleFileEmitted);

    log('Starting Language Client...');
    
    // Start Client
    try {
        await client.start();
        log('Language Server Started Successfully');
    } catch (error) {
        log(`ERROR starting client: ${error}`);
        client = undefined!;
        throw error;
    }
}

function updateStatusBar(state: string, message?: string): void {
    currentState = state;
    const icon = STATE_ICONS[state] ?? '$(question)';

    statusBarItem.text = `${icon} Gengora: ${state}`;
    statusBarItem.color = STATE_COLORS[state];
    statusBarItem.tooltip = `Gengora Generator State: ${state}${message ? `\n${message}` : ''}\n\nClick for commands`;
    statusBarItem.show();
}

function handleStateChanged(notification: StateChangedNotification): void {
    outputChannel.appendLine(`State Changed: ${notification.previousState} → ${notification.state}`);

    if (notification.message) {
        outputChannel.appendLine(`  Message: ${notification.message}`);
    }

    updateStatusBar(notification.state, notification.message);

    // Show Error Notification
    if (notification.state === 'Error' && notification.message) {
        vscode.window.showErrorMessage(`Gengora: ${notification.message}`);
    }
}

function handleDiagnostics(notification: DiagnosticsNotification): void {
    outputChannel.appendLine(`Received ${notification.diagnostics.length} Diagnostic(s)`);

    for (const diagnostic of notification.diagnostics) {
        const location = diagnostic.filePath
            ? `${diagnostic.filePath}:${diagnostic.line}:${diagnostic.column}`
            : '(unknown)';

        outputChannel.appendLine(`  [${diagnostic.severity}] ${diagnostic.id}: ${diagnostic.message} at ${location}`);
    }

    if (notification.isCompilationError) {
        vscode.window.showErrorMessage(
            `Gengora: Compilation Failed With ${notification.diagnostics.length} Error(s)`,
            'Show Output'
        ).then((selection: string | undefined) => {
            if (selection === 'Show Output') {
                outputChannel.show();
            }
        });
    }
}

function handleFileEmitted(notification: FileEmittedNotification): void {
    outputChannel.appendLine(`File Emitted: ${notification.path}`);
}

async function recompileCommand(): Promise<void> {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }

    try {
        const result = await client.sendRequest<RecompileResult>('gengora/recompile');

        if (result.success) {
            vscode.window.showInformationMessage('Gengora: Recompilation Succeeded');
        } else {
            vscode.window.showErrorMessage(`Gengora: ${result.message ?? 'Recompilation Failed'}`);
        }
    } catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}

async function stopCommand(): Promise<void> {
    if (!client) {
        vscode.window.showWarningMessage('Gengora: Language Server Not Running');
        return;
    }

    try {
        await client.sendRequest('gengora/stop');
        vscode.window.showInformationMessage('Gengora: Generator Stopped');
    } catch (error) {
        vscode.window.showErrorMessage(`Gengora: ${error}`);
    }
}

// Type Definitions For Notifications
interface StateChangedNotification {
    state: string;
    previousState: string;
    message?: string;
    timestamp: string;
}

interface DiagnosticsNotification {
    diagnostics: LspDiagnostic[];
    isCompilationError: boolean;
}

interface LspDiagnostic {
    id: string;
    message: string;
    severity: string;
    filePath?: string;
    line?: number;
    column?: number;
}

interface FileEmittedNotification {
    path: string;
    timestamp: string;
}

interface RecompileResult {
    success: boolean;
    message?: string;
}
