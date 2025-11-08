using TUnit.Core;

namespace ExampleTests
{
    /// <summary>
    /// Example test class demonstrating C# Test Filter Helper usage.
    /// 
    /// Try this:
    /// 1. Place your cursor in Add_ShouldReturnSum method
    /// 2. Press F5 to debug - only this test will run
    /// 3. Place cursor on line 9 (class name)
    /// 4. Press F5 - all tests in this class will run
    /// </summary>
    public class CalculatorTests
    {
        [Test]
        public async Task Add_ShouldReturnSum()
        {
            // Cursor here → Filter: /*/ExampleTests/CalculatorTests/Add_ShouldReturnSum
            var result = 2 + 2;
            await Assert.That(result).IsEqualTo(4);
        }

        [Test]
        public async Task Subtract_ShouldReturnDifference()
        {
            // Cursor here → Filter: /*/ExampleTests/CalculatorTests/Subtract_ShouldReturnDifference
            var result = 5 - 3;
            await Assert.That(result).IsEqualTo(2);
        }

        [Test]
        public async Task Multiply_ShouldReturnProduct()
        {
            var result = 3 * 4;
            await Assert.That(result).IsEqualTo(12);
        }

        // Cursor here (outside methods) → Filter: /*/ExampleTests/CalculatorTests/*
        // This will run ALL tests in the class
    }
}
