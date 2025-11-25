namespace Gengora.Server.Tests.Core.FileWatching;

using Gengora.Server.Core.FileWatching;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
/// Tests For IgnorePatternMatcher.
/// Verifies Pattern Matching Per Specification R5.*.
/// </summary>
public class IgnorePatternMatcherTests
{
    private readonly IgnorePatternMatcher _Matcher;

    public IgnorePatternMatcherTests()
    {
        this._Matcher = new IgnorePatternMatcher(NullLogger<IgnorePatternMatcher>.Instance);
    }

    [Test]
    public async Task ShouldIgnore_BinDirectory_ShouldReturnTrue()
    {
        // Arrange - R5.2: Default Pattern For bin/
        var path = "/workspace/project/bin/Debug/output.dll";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldIgnore_ObjDirectory_ShouldReturnTrue()
    {
        // Arrange - R5.2: Default Pattern For obj/
        var path = "/workspace/project/obj/Debug/net10.0/project.dll";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldIgnore_NodeModulesDirectory_ShouldReturnTrue()
    {
        // Arrange - R5.2: Default Pattern For node_modules/
        var path = "/workspace/extension/node_modules/some-package/index.js";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldIgnore_GitDirectory_ShouldReturnTrue()
    {
        // Arrange - R5.2: Default Pattern For .git/
        var path = "/workspace/.git/objects/pack/pack-123.idx";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldIgnore_SourceFile_ShouldReturnFalse()
    {
        // Arrange - Source Files Should Not Be Ignored
        var path = "/workspace/project/src/Program.cs";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldIgnore_ProjectFile_ShouldReturnFalse()
    {
        // Arrange - Project Files Should Not Be Ignored
        var path = "/workspace/project/MyProject.csproj";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldIgnore_VsDirectory_ShouldReturnTrue()
    {
        // Arrange - R5.2: Default Pattern For .vs/
        var path = "/workspace/.vs/project/v17/Browse.VC.db";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task AddPatterns_ShouldBeRespected()
    {
        // Arrange
        var matcher = new IgnorePatternMatcher(NullLogger<IgnorePatternMatcher>.Instance);

        matcher.AddPatterns(["**/custom-ignored/**"]);

        var path = "/workspace/project/custom-ignored/file.txt";

        // Act
        var result = matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Patterns_ShouldContainDefaultPatterns()
    {
        // Arrange
        var matcher = new IgnorePatternMatcher(NullLogger<IgnorePatternMatcher>.Instance);

        // Act
        var patterns = matcher.Patterns;

        // Assert - Verify Default Patterns Are Present (R5.2)
        await Assert.That(patterns).Contains("**/bin/**");
        await Assert.That(patterns).Contains("**/obj/**");
        await Assert.That(patterns).Contains("**/node_modules/**");
    }

    [Test]
    public async Task ShouldIgnore_NuGetPackages_ShouldReturnTrue()
    {
        // Arrange
        var path = "/Users/user/.nuget/packages/some-package/1.0.0/lib/net10.0/package.dll";

        // Act
        var result = this._Matcher.ShouldIgnore(path);

        // Assert
        await Assert.That(result).IsTrue();
    }
}
