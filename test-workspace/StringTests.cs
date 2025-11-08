namespace SampleTests;

public class StringTests
{
    [Test]
    public async Task String_Concatenation_ShouldWork()
    {
        // Arrange
        var str1 = "Hello";
        var str2 = "World";
        
        // Act
        var result = str1 + " " + str2;
        
        // Assert
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    public async Task String_ToUpper_ShouldConvertToUpperCase()
    {
        // Arrange
        var input = "test";
        
        // Act
        var result = input.ToUpper();
        
        // Assert
        await Assert.That(result).IsEqualTo("TEST");
    }

    [Test]
    public async Task String_Contains_ShouldFindSubstring()
    {
        // Arrange
        var text = "The quick brown fox";
        
        // Act
        var result = text.Contains("quick");
        
        // Assert
        await Assert.That(result).IsTrue();
    }
}
