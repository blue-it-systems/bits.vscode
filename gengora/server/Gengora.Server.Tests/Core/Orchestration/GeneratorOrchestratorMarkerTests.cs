namespace Gengora.Server.Tests.Core.Orchestration;

using Gengora.Server.Core.Discovery;
using Gengora.Server.Core.FileWatching;
using Gengora.Server.Core.StateMachine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Integration Tests For Generator Marker Change Detection.
/// Verifies That When IsGeneratorProject Marker Changes, The System Responds Correctly.
/// These Tests Focus On The ProjectMarkerScanner And FileWatcher Integration.
/// </summary>
public sealed class GeneratorOrchestratorMarkerTests : IDisposable
{
    private string _TestDirectory = null!;
    private readonly ILoggerFactory _LoggerFactory;
    private readonly List<string> _FileChanges;

    public GeneratorOrchestratorMarkerTests()
    {
        this._LoggerFactory = NullLoggerFactory.Instance;
        this._FileChanges = new List<string>();
    }

    [Before(Test)]
    public async Task SetUp()
    {
        this._TestDirectory = Path.Combine(Path.GetTempPath(), "gengora-marker-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._TestDirectory);
        this._FileChanges.Clear();

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
    /// Tests The Core Marker Detection Logic:
    /// 1. Initial Discovery Finds Generator With Marker=True
    /// 2. After Changing Marker To False, IsStillGeneratorProjectAsync Returns False
    /// This Verifies The Foundation Of The Marker Change Detection Feature.
    /// </summary>
    [Test]
    public async Task MarkerChangeDetection_FromTrueToFalse_IsDetected()
    {
        // Arrange - Create A Generator Project With Marker = True
        var projectDir = Path.Combine(this._TestDirectory, "TestGenerator");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "TestGenerator.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var scanner = new ProjectMarkerScanner(this._LoggerFactory.CreateLogger<ProjectMarkerScanner>());

        // Act 1 - Initial Scan Should Find The Generator
        var initialProject = await scanner.ScanAsync(new[] { this._TestDirectory });

        // Assert 1 - Project Should Be Found
        await Assert.That(initialProject).IsNotNull();
        await Assert.That(initialProject!.ProjectName).IsEqualTo("TestGenerator");
        await Assert.That(await scanner.IsStillGeneratorProjectAsync(projectPath)).IsTrue();

        // Act 2 - Change The Marker To False
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert 2 - IsStillGeneratorProjectAsync Should Now Return False
        var isStillGenerator = await scanner.IsStillGeneratorProjectAsync(projectPath);
        await Assert.That(isStillGenerator).IsFalse();

        // Act 3 - A New Scan Should NOT Find The Generator
        var rescanProject = await scanner.ScanAsync(new[] { this._TestDirectory });
        await Assert.That(rescanProject).IsNull();
    }

    /// <summary>
    /// Tests That Marker Removal Is Detected.
    /// </summary>
    [Test]
    public async Task MarkerChangeDetection_MarkerRemoved_IsDetected()
    {
        // Arrange
        var projectDir = Path.Combine(this._TestDirectory, "TestGenerator");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "TestGenerator.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var scanner = new ProjectMarkerScanner(this._LoggerFactory.CreateLogger<ProjectMarkerScanner>());

        // Initial State - Should Be A Generator
        await Assert.That(await scanner.IsStillGeneratorProjectAsync(projectPath)).IsTrue();

        // Act - Remove The Marker Entirely
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);

        // Assert - Should No Longer Be Detected As Generator
        await Assert.That(await scanner.IsStillGeneratorProjectAsync(projectPath)).IsFalse();
    }

    /// <summary>
    /// Tests The File Watcher Detects .csproj Changes.
    /// </summary>
    [Test]
    public async Task FileWatcher_DetectsProjectFileChanges()
    {
        // Arrange
        var projectDir = Path.Combine(this._TestDirectory, "TestGenerator");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "TestGenerator.csproj");
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

        var fileChangedEvent = new TaskCompletionSource<string>();
        fileWatcher.FileChanged += (s, e) =>
        {
            this._FileChanges.Add(e.FilePath);
            if (e.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                fileChangedEvent.TrySetResult(e.FilePath);
            }
        };

        var projectInfo = new GeneratorProjectInfo
        {
            ProjectPath = projectPath,
            ProjectName = "TestGenerator",
            ProjectDirectory = projectDir
        };

        fileWatcher.StartWatching(projectInfo);

        // Act - Modify The Project File
        await Task.Delay(100); // Give Watcher Time To Initialize

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert - File Change Should Be Detected Within 2 Seconds
        var timeoutTask = Task.Delay(2000);
        var completedTask = await Task.WhenAny(fileChangedEvent.Task, timeoutTask);

        fileWatcher.StopWatching();

        await Assert.That(completedTask == fileChangedEvent.Task).IsTrue();
        await Assert.That(fileChangedEvent.Task.Result).IsEqualTo(projectPath);
    }

    /// <summary>
    /// Tests The Complete Flow: FileWatcher Detects Change, Scanner Verifies Marker Is False.
    /// This Simulates What The Orchestrator Does When A .csproj File Changes.
    /// </summary>
    [Test]
    public async Task IntegrationTest_FileWatcherAndScanner_DetectMarkerChange()
    {
        // Arrange
        var projectDir = Path.Combine(this._TestDirectory, "TestGenerator");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "TestGenerator.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        var scanner = new ProjectMarkerScanner(this._LoggerFactory.CreateLogger<ProjectMarkerScanner>());
        var ignoreMatcher = new IgnorePatternMatcher(this._LoggerFactory.CreateLogger<IgnorePatternMatcher>());
        var fileWatcher = new FileWatcherService(
            this._LoggerFactory.CreateLogger<FileWatcherService>(),
            ignoreMatcher);

        // This Simulates What OnFileChanged In GeneratorOrchestrator Does
        var shouldResetToIdle = new TaskCompletionSource<bool>();
        fileWatcher.FileChanged += async (s, e) =>
        {
            if (e.FilePath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                // Check If The Marker Is Still True
                var isStillGenerator = await scanner.IsStillGeneratorProjectAsync(projectPath);
                if (!isStillGenerator)
                {
                    shouldResetToIdle.TrySetResult(true);
                }
            }
        };

        var projectInfo = new GeneratorProjectInfo
        {
            ProjectPath = projectPath,
            ProjectName = "TestGenerator",
            ProjectDirectory = projectDir
        };

        fileWatcher.StartWatching(projectInfo);

        // Verify Initial State
        await Assert.That(await scanner.IsStillGeneratorProjectAsync(projectPath)).IsTrue();

        // Act - Change Marker To False
        await Task.Delay(100);
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Assert - Should Detect That Reset To Idle Is Needed
        var timeoutTask = Task.Delay(2000);
        var completedTask = await Task.WhenAny(shouldResetToIdle.Task, timeoutTask);

        fileWatcher.StopWatching();

        await Assert.That(completedTask == shouldResetToIdle.Task).IsTrue();
        await Assert.That(shouldResetToIdle.Task.Result).IsTrue();
    }

    /// <summary>
    /// Tests That State Machine Reset Functionality Works As Expected.
    /// </summary>
    [Test]
    public async Task StateMachine_Reset_ReturnsToIdle()
    {
        // Arrange
        var stateMachine = new GeneratorStateMachine(this._LoggerFactory.CreateLogger<GeneratorStateMachine>());

        // Progress Through States
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);

        stateMachine.TryTransition(GeneratorState.GeneratorFound);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.GeneratorFound);

        stateMachine.TryTransition(GeneratorState.Compiling);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Compiling);

        stateMachine.TryTransition(GeneratorState.Ready);
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Ready);

        // Act - Reset
        stateMachine.Reset();

        // Assert - Should Be Back To Idle
        await Assert.That(stateMachine.CurrentState).IsEqualTo(GeneratorState.Idle);
    }

    public void Dispose()
    {
        // Cleanup
    }
}
