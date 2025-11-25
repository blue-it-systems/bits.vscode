namespace Gengora.Server.Tests.Core.Discovery;

using Gengora.Server.Core.Discovery;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For ProjectMarkerScanner.
/// Verifies Discovery Logic Per Specification R1.*.
/// </summary>
public class ProjectMarkerScannerTests
{
    private readonly ProjectMarkerScanner _Scanner;
    private string _TestDirectory = null!;

    public ProjectMarkerScannerTests()
    {
        this._Scanner = new ProjectMarkerScanner(NullLogger<ProjectMarkerScanner>.Instance);
    }

    [Before(Test)]
    public async Task SetUp()
    {
        this._TestDirectory = Path.Combine(Path.GetTempPath(), "gengora-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(this._TestDirectory);

        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (Directory.Exists(this._TestDirectory))
        {
            Directory.Delete(this._TestDirectory, recursive: true);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task ScanAsync_WithGeneratorProject_ShouldFindProject()
    {
        // Arrange - Create A Project With Generator Marker
        var projectDir = Path.Combine(this._TestDirectory, "MyGenerator");

        Directory.CreateDirectory(projectDir);

        var projectFile = Path.Combine(projectDir, "MyGenerator.csproj");

        await File.WriteAllTextAsync(projectFile, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ProjectName).IsEqualTo("MyGenerator");
        await Assert.That(result.ProjectPath).IsEqualTo(projectFile);
    }

    [Test]
    public async Task ScanAsync_WithNoGeneratorProjects_ShouldReturnNull()
    {
        // Arrange - Create A Regular Project Without Marker
        var projectDir = Path.Combine(this._TestDirectory, "RegularProject");

        Directory.CreateDirectory(projectDir);

        var projectFile = Path.Combine(projectDir, "RegularProject.csproj");

        await File.WriteAllTextAsync(projectFile, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ScanAsync_WithMultipleProjects_ShouldFindFirstGenerator()
    {
        // Arrange - Create Multiple Projects (generator first alphabetically)
        var generatorDir = Path.Combine(this._TestDirectory, "AGenerator");
        var libraryDir = Path.Combine(this._TestDirectory, "Library");

        Directory.CreateDirectory(generatorDir);
        Directory.CreateDirectory(libraryDir);

        await File.WriteAllTextAsync(Path.Combine(generatorDir, "AGenerator.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(libraryDir, "Library.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ProjectName).IsEqualTo("AGenerator");
    }

    [Test]
    public async Task ScanAsync_WithNestedGeneratorProject_ShouldFind()
    {
        // Arrange - Create Nested Project Structure
        var nestedDir = Path.Combine(this._TestDirectory, "src", "generators", "MyGen");

        Directory.CreateDirectory(nestedDir);

        var projectFile = Path.Combine(nestedDir, "MyGen.csproj");

        await File.WriteAllTextAsync(projectFile, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ProjectName).IsEqualTo("MyGen");
    }

    [Test]
    public async Task ScanAsync_WithIsGeneratorProjectFalse_ShouldNotFind()
    {
        // Arrange - Explicit False Value
        var projectDir = Path.Combine(this._TestDirectory, "NotAGenerator");

        Directory.CreateDirectory(projectDir);

        await File.WriteAllTextAsync(Path.Combine(projectDir, "NotAGenerator.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>false</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ScanAsync_ShouldIgnoreBinAndObjDirectories()
    {
        // Arrange - Create Project In bin Directory (Should Be Ignored)
        var binDir = Path.Combine(this._TestDirectory, "bin", "Debug");

        Directory.CreateDirectory(binDir);

        await File.WriteAllTextAsync(Path.Combine(binDir, "ShouldIgnore.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Create Valid Project
        var srcDir = Path.Combine(this._TestDirectory, "src");

        Directory.CreateDirectory(srcDir);

        await File.WriteAllTextAsync(Path.Combine(srcDir, "Valid.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsGeneratorProject>true</IsGeneratorProject>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = await this._Scanner.ScanAsync(this._TestDirectory);

        // Assert - Only The Valid Project Should Be Found
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ProjectName).IsEqualTo("Valid");
    }
}
