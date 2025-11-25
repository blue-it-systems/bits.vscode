namespace Gengora.Server.Tests.Core.Orchestration;

using Gengora.Server.Core.Discovery;
using Gengora.Server.Core.FileWatching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For Bug Fix: Marker False → True Should Activate Server.
/// When A .csproj File Changes And The IsGeneratorProject Marker Is Set To True,
/// The Server Should Transition From Idle To GeneratorFound And Begin The Workflow.
/// </summary>
public sealed class WorkspaceWatcherActivationTests : IDisposable
{
    private string _TestDirectory = null!;
    private readonly ILoggerFactory _LoggerFactory;

    public WorkspaceWatcherActivationTests()
    {
        this._LoggerFactory = NullLoggerFactory.Instance;
    }

    [Before(Test)]
    public async Task SetUp()
    {
        this._TestDirectory = Path.Combine(Path.GetTempPath(), "gengora-workspace-watcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._TestDirectory);

        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (Directory.Exists(this._TestDirectory))
        {
            try
            {
                Directory.Delete(this._TestDirectory, recursive: true);
            }
            catch
            {
                // Ignore Cleanup Errors
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests That StartWatchingWorkspace Method Exists And Can Be Called.
    /// </summary>
    [Test]
    public async Task StartWatchingWorkspace_CanBeInvoked()
    {
        // Arrange
        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        var changedFiles = new List<string>();
        void Handler(object? sender, FileChangedEventArgs e) => changedFiles.Add(e.FilePath);

        // Act
        fileWatcher.StartWatchingWorkspace(this._TestDirectory, Handler);

        // Assert
        await Assert.That(fileWatcher.IsWatchingWorkspace).IsTrue();

        fileWatcher.StopWatchingWorkspace();
        await Assert.That(fileWatcher.IsWatchingWorkspace).IsFalse();

        fileWatcher.Dispose();
    }

    /// <summary>
    /// Tests That Workspace Watcher Detects .csproj File Changes.
    /// </summary>
    [Test]
    public async Task WorkspaceWatcher_Detects_CsprojFileChanges()
    {
        // Arrange
        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        var fileChangedEvent = new TaskCompletionSource<string>();
        void Handler(object? sender, FileChangedEventArgs e)
        {
            if (e.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                fileChangedEvent.TrySetResult(e.FilePath);
            }
        }

        fileWatcher.StartWatchingWorkspace(this._TestDirectory, Handler);

        // Act - Create A .csproj File
        await Task.Delay(100); // Give Watcher Time To Initialize

        var projectPath = Path.Combine(this._TestDirectory, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert
        var timeoutTask = Task.Delay(2000);
        var completedTask = await Task.WhenAny(fileChangedEvent.Task, timeoutTask);

        fileWatcher.StopWatchingWorkspace();

        await Assert.That(completedTask == fileChangedEvent.Task).IsTrue();
        await Assert.That(fileChangedEvent.Task.Result).IsEqualTo(projectPath);

        fileWatcher.Dispose();
    }

    /// <summary>
    /// Tests That Workspace Watcher Detects Changes To Existing .csproj Files.
    /// </summary>
    [Test]
    public async Task WorkspaceWatcher_Detects_CsprojFileModifications()
    {
        // Arrange - Create .csproj File First
        var projectPath = Path.Combine(this._TestDirectory, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        var fileChangedEvent = new TaskCompletionSource<string>();
        void Handler(object? sender, FileChangedEventArgs e)
        {
            if (e.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                fileChangedEvent.TrySetResult(e.FilePath);
            }
        }

        fileWatcher.StartWatchingWorkspace(this._TestDirectory, Handler);

        // Act - Modify The .csproj File To Set Marker True
        await Task.Delay(100);
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert
        var timeoutTask = Task.Delay(2000);
        var completedTask = await Task.WhenAny(fileChangedEvent.Task, timeoutTask);

        fileWatcher.StopWatchingWorkspace();

        await Assert.That(completedTask == fileChangedEvent.Task).IsTrue();
        await Assert.That(fileChangedEvent.Task.Result).IsEqualTo(projectPath);

        fileWatcher.Dispose();
    }

    /// <summary>
    /// Tests The Complete Flow: Workspace Watcher Detects Change, Scanner Finds New Generator.
    /// This Simulates The Activation Flow When Marker Changes From False To True.
    /// </summary>
    [Test]
    public async Task IntegrationTest_WorkspaceWatcher_ActivatesWhenMarkerBecomesTrue()
    {
        // Arrange - Start With A Non-Generator Project
        var projectDir = Path.Combine(this._TestDirectory, "TestProject");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var scanner = new ProjectMarkerScanner(this._LoggerFactory.CreateLogger<ProjectMarkerScanner>());
        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        // Verify Initial State - No Generator Found
        var initialProject = await scanner.ScanAsync(this._TestDirectory);
        await Assert.That(initialProject).IsNull();

        // Set Up Workspace Watcher (This Is What Happens When Server Is Idle)
        var generatorActivated = new TaskCompletionSource<GeneratorProjectInfo>();
        void Handler(object? sender, FileChangedEventArgs e)
        {
            // When A .csproj Changes, Re-Scan To Find Generator
            Task.Run(async () =>
            {
                var project = await scanner.ScanAsync(this._TestDirectory);
                if (project != null)
                {
                    generatorActivated.TrySetResult(project);
                }
            });
        }

        fileWatcher.StartWatchingWorkspace(this._TestDirectory, Handler);

        // Act - Change Marker To True
        await Task.Delay(100);
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert - Generator Should Be Found And Activated
        var timeoutTask = Task.Delay(3000);
        var completedTask = await Task.WhenAny(generatorActivated.Task, timeoutTask);

        fileWatcher.StopWatchingWorkspace();

        await Assert.That(completedTask == generatorActivated.Task).IsTrue();

        var activatedProject = generatorActivated.Task.Result;
        await Assert.That(activatedProject.ProjectPath).IsEqualTo(projectPath);
        await Assert.That(activatedProject.ProjectName).IsEqualTo("Test");

        fileWatcher.Dispose();
    }

    /// <summary>
    /// Tests That Workspace Watcher Ignores Non-.csproj Files.
    /// </summary>
    [Test]
    public async Task WorkspaceWatcher_IgnoresNonCsprojFiles()
    {
        // Arrange
        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        var changedFiles = new List<string>();
        void Handler(object? sender, FileChangedEventArgs e) => changedFiles.Add(e.FilePath);

        fileWatcher.StartWatchingWorkspace(this._TestDirectory, Handler);

        // Act - Create Non-.csproj Files
        await Task.Delay(100);
        await File.WriteAllTextAsync(Path.Combine(this._TestDirectory, "test.cs"), "// C# code");
        await File.WriteAllTextAsync(Path.Combine(this._TestDirectory, "readme.md"), "# Readme");
        await Task.Delay(500);

        // Assert - No Files Should Have Triggered The Handler
        fileWatcher.StopWatchingWorkspace();
        await Assert.That(changedFiles.Count).IsEqualTo(0);

        fileWatcher.Dispose();
    }

    /// <summary>
    /// Tests That Workspace Watcher And Project Watcher Can Coexist.
    /// </summary>
    [Test]
    public async Task WorkspaceWatcher_CanCoexistWithProjectWatcher()
    {
        // Arrange
        var projectDir = Path.Combine(this._TestDirectory, "TestGenerator");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        var workspaceChanges = new List<string>();
        var projectChanges = new List<string>();

        void WorkspaceHandler(object? sender, FileChangedEventArgs e) => workspaceChanges.Add(e.FilePath);

        fileWatcher.FileChanged += (s, e) => projectChanges.Add(e.FilePath);

        // Start Both Watchers
        fileWatcher.StartWatchingWorkspace(this._TestDirectory, WorkspaceHandler);

        var projectInfo = new GeneratorProjectInfo
        {
            ProjectPath = projectPath,
            ProjectName = "Test",
            ProjectDirectory = projectDir
        };
        fileWatcher.StartWatching(projectInfo);

        // Assert Both Are Active
        await Assert.That(fileWatcher.IsWatchingWorkspace).IsTrue();
        await Assert.That(fileWatcher.IsWatching).IsTrue();

        // Cleanup
        fileWatcher.StopWatching();
        fileWatcher.StopWatchingWorkspace();
        fileWatcher.Dispose();

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        // Cleanup
    }
}
