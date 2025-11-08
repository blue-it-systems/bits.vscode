namespace SampleTests;

public class CalculatorTests
{
    [Test]
    public async Task Add_ShouldReturnCorrectSum()
    {
        // Arrange
        var calculator = new Calculator();
        
        // Act
        var result = calculator.Add(5, 3);
        
        // Assert
        await Assert.That(result).IsEqualTo(8);
    }

    [Test]
    public async Task Subtract_ShouldReturnCorrectDifference()
    {
        // Arrange
        var calculator = new Calculator();
        
        // Act
        var result = calculator.Subtract(10, 4);
        
        // Assert
        await Assert.That(result).IsEqualTo(6);
    }

    [Test]
    public async Task Multiply_ShouldReturnCorrectProduct()
    {
        // Arrange
        var calculator = new Calculator();
        
        // Act
        var result = calculator.Multiply(6, 7);
        
        // Assert
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Divide_ShouldReturnCorrectQuotient()
    {
        // Arrange
        var calculator = new Calculator();
        
        // Act
        var result = calculator.Divide(20, 4);
        
        // Assert
        await Assert.That(result).IsEqualTo(5);
    }
}

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public int Multiply(int a, int b) => a * b;
    public int Divide(int a, int b) => a / b;
}
