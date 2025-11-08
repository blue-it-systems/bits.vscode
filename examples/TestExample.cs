using System;

namespace BITS.Terminal.Tests
{
    public class CalculatorTests
    {
        [Test]
        public void Add_ShouldReturnSum()
        {
            // Place cursor here and test the extension
            // Expected output:
            // Assembly: BITS.Terminal.Tests
            // Class: CalculatorTests
            // Method: Add_ShouldReturnSum
            // Filter: */CalculatorTests/Add_ShouldReturnSum
            var result = 2 + 2;
            Assert.Equal(4, result);
        }

        [Test]
        public void Subtract_ShouldReturnDifference()
        {
            // Place cursor here for another test
            var result = 5 - 3;
            Assert.Equal(2, result);
        }

        // Place cursor here (outside methods)
        // Expected output:
        // Assembly: BITS.Terminal.Tests
        // Class: CalculatorTests
        // Method: (none - class scope)
        // Filter: */CalculatorTests/*
    }
}
