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
            // Test with variable dependencies where some vars are needed, others not
            int unnecessary1 = 100; // Can be removed
            int unnecessary2 = 200; // Can be removed
            
            int x = 10; // REQUIRED - used in calculation
            int y = 5;  // Can be removed - not used in final result
            int z = 1;  // REQUIRED - used in calculation
            
            int temp1 = x + z; // REQUIRED - needed for result
            int temp2 = y * 2; // Can be removed - not used
            
            int result = temp1; // REQUIRED - final result
            
            Assert.False(result == 11); // REQUIRED - fails when result is 11 (10+1)
        }

        [Fact]
        public void NestedConditionals_ShouldFail()
        {
            // Test with nested if statements and variable dependencies
            int a = 5;    // REQUIRED
            int b = 10;   // Can be removed
            int c = 0;    // REQUIRED
            
            if (a > 0)    // REQUIRED - affects c
            {
                c = a;    // REQUIRED - sets c to 5
                int temp = b * 2; // Can be removed
            }
            
            if (b > 0)    // Can be removed - doesn't affect final result
            {
                int unused = b + 1; // Can be removed
            }
            
            Assert.True(c != 5); // REQUIRED - fails when c is 5
        }

        [Fact]
        public void ComplexLoops_ShouldFail()
        {
            // Test with loops where some iterations matter, others don't
            int sum = 0;      // REQUIRED
            int counter = 0;  // Can be removed
            
            for (int i = 0; i < 3; i++) // REQUIRED - affects sum
            {
                sum += i;     // REQUIRED - makes sum = 3
                counter++;    // Can be removed
            }
            
            for (int j = 0; j < 5; j++) // Can be removed - doesn't affect assertion
            {
                int temp = j * 2; // Can be removed
            }
            
            Assert.False(sum == 3); // REQUIRED - fails when sum is 3
        }

        [Fact]
        public void StringManipulation_ShouldFail()
        {
            // Test with string operations and dependencies
            string base1 = "Hello";  // REQUIRED
            string base2 = "World";  // Can be removed
            string result = "";      // REQUIRED
            
            result = base1;          // REQUIRED - sets result to "Hello"
            
            string temp = base2 + "!"; // Can be removed
            
            Assert.True(result != "Hello"); // REQUIRED - fails when result is "Hello"
        }

        [Fact]
        public void LambdaExpressions_ShouldFail()
        {
            // Test with lambda expressions and LINQ
            var numbers = new List<int> { 1, 2, 3, 4, 5 }; // REQUIRED
            var letters = new List<string> { "a", "b", "c" }; // Can be removed
            
            var evenNumbers = numbers.Where(n => n % 2 == 0).ToList(); // REQUIRED - creates [2, 4]
            var upperLetters = letters.Select(l => l.ToUpper()).ToList(); // Can be removed
            
            int count = evenNumbers.Count; // REQUIRED - count is 2
            
            Assert.False(count == 2); // REQUIRED - fails when count is 2
        }

        [Fact]
        public void DeeplyNestedStructures_ShouldFail()
        {
            // Test with deeply nested structures
            int level1 = 0; // REQUIRED
            
            if (true) // REQUIRED - outer condition
            {
                int level2 = 1; // Can be removed
                
                if (level1 == 0) // REQUIRED - inner condition
                {
                    level1 = 5; // REQUIRED - sets level1 to 5
                    
                    if (level2 == 1) // Can be removed - doesn't affect final result
                    {
                        int unused = level2 + 1; // Can be removed
                    }
                }
            }
            
            Assert.True(level1 != 5); // REQUIRED - fails when level1 is 5
        }

        [Fact]
        public void ExceptionHandling_ShouldFail()
        {
            // Test with try-catch blocks
            int result = 0; // REQUIRED
            int backup = 100; // Can be removed
            
            try
            {
                result = 10 / 2; // REQUIRED - sets result to 5
                int temp = backup * 2; // Can be removed
            }
            catch (Exception ex)
            {
                result = -1; // Can be removed - won't execute
            }
            
            Assert.False(result == 5); // REQUIRED - fails when result is 5
        }

        [Fact]
        public void TreeStructuredTest_ShouldFail()
        {
            // Test that benefits most from hierarchical reduction
            // Has clear parent-child relationships in the logic
            
            // Root level calculations
            int root = 1; // REQUIRED
            
            // Branch 1 - affects final result
            int branch1 = root * 2; // REQUIRED - makes branch1 = 2
            int leaf1a = branch1 + 1; // REQUIRED - makes leaf1a = 3
            int leaf1b = branch1 * 2; // Can be removed - not used in final calc
            
            // Branch 2 - doesn't affect final result
            int branch2 = root + 10; // Can be removed
            int leaf2a = branch2 - 5; // Can be removed
            int leaf2b = branch2 * 3; // Can be removed
            
            // Final calculation uses only branch1 path
            int finalResult = root + leaf1a; // REQUIRED - makes finalResult = 4
            
            Assert.True(finalResult != 4); // REQUIRED - fails when finalResult is 4
        }
    }
} 