namespace SampleTests;

public class CalculatorTests
{
    [Test]
    public async Task Add_TwoNumbers_ReturnsSum()
    {
        var result = 2 + 3;
        await Assert.That(result).IsEqualTo(5);
    }

    [Test]
    public async Task Subtract_TwoNumbers_ReturnsDifference()
    {
        var result = 5 - 3;
        await Assert.That(result).IsEqualTo(2);
    }

    [Test]
    public async Task Multiply_TwoNumbers_ReturnsProduct()
    {
        var result = 4 * 3;
        await Assert.That(result).IsEqualTo(12);
    }
}

public class StringTests
{
    [Test]
    public async Task Concat_TwoStrings_ReturnsCombined()
    {
        var result = "Hello" + " " + "World";
        await Assert.That(result).IsEqualTo("Hello World");
    }
}
