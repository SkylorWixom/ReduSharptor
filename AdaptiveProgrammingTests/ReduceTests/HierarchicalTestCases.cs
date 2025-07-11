using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReduceTests
{
    public class HierarchicalTestCases
    {
        [Fact]
        public void SimpleArithmetic_ShouldFail()
        {
            // This test has multiple arithmetic operations that can be reduced hierarchically
            int x = 10;
            int y = 5;
            int z = 3;
            
            int result1 = x + y * z;
            int result2 = (x - y) * (z + 2);
            int result3 = result1 + result2;
            
            System.Diagnostics.Debug.WriteLine($"Result1: {result1}");
            System.Diagnostics.Debug.WriteLine($"Result2: {result2}");
            System.Diagnostics.Debug.WriteLine($"Result3: {result3}");
            
            // This assertion should fail, making this a failing test
            Assert.True(result3 == 100); // Will fail since result3 ≠ 100
        }
    }
} 