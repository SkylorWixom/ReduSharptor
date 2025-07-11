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

        [Fact]
        public void NestedConditionals_ShouldFail()
        {
            // This test has nested if statements that can be reduced hierarchically
            int value = 15;
            string message = "";
            
            if (value > 10)
            {
                message += "Greater than 10, ";
                
                if (value > 20)
                {
                    message += "and greater than 20, ";
                    
                    if (value > 30)
                    {
                        message += "and greater than 30";
                    }
                    else
                    {
                        message += "but not greater than 30";
                    }
                }
                else
                {
                    message += "but not greater than 20";
                }
            }
            else
            {
                message = "Not greater than 10";
            }
            
            System.Diagnostics.Debug.WriteLine($"Message: {message}");
            
            // This assertion should fail
            Assert.Equal("Unexpected message", message);
        }

        [Fact]
        public void ComplexLoops_ShouldFail()
        {
            // This test has nested loops with complex logic
            List<int> numbers = new List<int>();
            
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int value = i * j + (i + j);
                    numbers.Add(value);
                    
                    System.Diagnostics.Debug.WriteLine($"Added: {value} (i={i}, j={j})");
                }
            }
            
            int sum = 0;
            foreach (var num in numbers)
            {
                if (num % 2 == 0)
                {
                    sum += num;
                }
                else
                {
                    sum -= num;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Final sum: {sum}");
            
            // This assertion should fail
            Assert.Equal(999, sum); // Will fail since sum ≠ 999
        }

        [Fact]
        public void StringManipulation_ShouldFail()
        {
            // This test has complex string operations
            string baseString = "Hello";
            string suffix = "World";
            
            string result = baseString.ToUpper() + " " + suffix.ToLower();
            result = result.Replace("L", "X");
            result = result.Substring(0, Math.Min(result.Length, 10));
            
            char[] chars = result.ToCharArray();
            Array.Reverse(chars);
            string reversed = new string(chars);
            
            System.Diagnostics.Debug.WriteLine($"Original: {baseString}");
            System.Diagnostics.Debug.WriteLine($"Result: {result}");
            System.Diagnostics.Debug.WriteLine($"Reversed: {reversed}");
            
            // This assertion should fail
            Assert.StartsWith("GOODBYE", reversed);
        }

        [Fact]
        public void LambdaExpressions_ShouldFail()
        {
            // This test uses lambda expressions and LINQ
            var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            
            var evenNumbers = numbers.Where(n => n % 2 == 0).ToList();
            var doubledEvens = evenNumbers.Select(n => n * 2).ToList();
            var filteredDoubled = doubledEvens.Where(n => n > 5).ToList();
            
            var sum = filteredDoubled.Aggregate(0, (acc, n) => acc + n);
            var average = filteredDoubled.Any() ? filteredDoubled.Average() : 0;
            
            System.Diagnostics.Debug.WriteLine($"Even numbers: {string.Join(", ", evenNumbers)}");
            System.Diagnostics.Debug.WriteLine($"Doubled evens: {string.Join(", ", doubledEvens)}");
            System.Diagnostics.Debug.WriteLine($"Filtered doubled: {string.Join(", ", filteredDoubled)}");
            System.Diagnostics.Debug.WriteLine($"Sum: {sum}, Average: {average}");
            
            // This assertion should fail
            Assert.Equal(1000, sum); // Will fail since sum ≠ 1000
        }

        [Fact]
        public void DeeplyNestedStructures_ShouldFail()
        {
            // This test has deeply nested structures that benefit from hierarchical reduction
            Dictionary<string, List<Dictionary<int, string>>> complexData = 
                new Dictionary<string, List<Dictionary<int, string>>>();
            
            for (int group = 0; group < 3; group++)
            {
                string groupKey = $"Group{group}";
                complexData[groupKey] = new List<Dictionary<int, string>>();
                
                for (int subGroup = 0; subGroup < 2; subGroup++)
                {
                    var subDict = new Dictionary<int, string>();
                    
                    for (int item = 0; item < 3; item++)
                    {
                        int key = group * 100 + subGroup * 10 + item;
                        string value = $"Value_{group}_{subGroup}_{item}";
                        subDict[key] = value;
                        
                        System.Diagnostics.Debug.WriteLine($"Added {key}: {value}");
                    }
                    
                    complexData[groupKey].Add(subDict);
                }
            }
            
            int totalItems = 0;
            foreach (var group in complexData)
            {
                foreach (var subGroup in group.Value)
                {
                    totalItems += subGroup.Count;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Total items: {totalItems}");
            
            // This assertion should fail
            Assert.Equal(50, totalItems); // Will fail since totalItems ≠ 50
        }

        [Fact]
        public void ExceptionHandling_ShouldFail()
        {
            // This test has try-catch blocks that can be reduced
            int result = 0;
            string errorMessage = "";
            
            try
            {
                int[] numbers = { 1, 2, 3, 4, 5 };
                
                for (int i = 0; i <= numbers.Length; i++) // Intentionally goes out of bounds
                {
                    try
                    {
                        result += numbers[i] * 2;
                        System.Diagnostics.Debug.WriteLine($"Processing index {i}: {numbers[i]} * 2 = {numbers[i] * 2}");
                    }
                    catch (IndexOutOfRangeException inner)
                    {
                        errorMessage = $"Index {i} out of range";
                        System.Diagnostics.Debug.WriteLine(errorMessage);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Outer exception: {errorMessage}");
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine($"Final result: {result}");
            }
            
            // This assertion should fail
            Assert.Equal("No error occurred", errorMessage);
        }

        [Fact]
        public void TreeStructuredTest_ShouldFail()
        {
            // This test specifically benefits from hierarchical reduction (tree-structured)
            var root = CreateTree();
            int nodeCount = CountNodes(root);
            int leafCount = CountLeaves(root);
            int maxDepth = GetMaxDepth(root);
            
            System.Diagnostics.Debug.WriteLine($"Node count: {nodeCount}");
            System.Diagnostics.Debug.WriteLine($"Leaf count: {leafCount}");
            System.Diagnostics.Debug.WriteLine($"Max depth: {maxDepth}");
            
            // This assertion should fail
            Assert.Equal(100, nodeCount); // Will fail since nodeCount ≠ 100
        }

        private TreeNode CreateTree()
        {
            var root = new TreeNode("Root");
            
            var left = new TreeNode("Left");
            left.Children.Add(new TreeNode("Left-Left"));
            left.Children.Add(new TreeNode("Left-Right"));
            left.Children[0].Children.Add(new TreeNode("Left-Left-Left"));
            
            var right = new TreeNode("Right");
            right.Children.Add(new TreeNode("Right-Left"));
            right.Children.Add(new TreeNode("Right-Right"));
            right.Children[1].Children.Add(new TreeNode("Right-Right-Left"));
            right.Children[1].Children.Add(new TreeNode("Right-Right-Right"));
            
            root.Children.Add(left);
            root.Children.Add(right);
            
            return root;
        }

        private int CountNodes(TreeNode node)
        {
            if (node == null) return 0;
            
            int count = 1;
            foreach (var child in node.Children)
            {
                count += CountNodes(child);
            }
            return count;
        }

        private int CountLeaves(TreeNode node)
        {
            if (node == null) return 0;
            if (node.Children.Count == 0) return 1;
            
            int count = 0;
            foreach (var child in node.Children)
            {
                count += CountLeaves(child);
            }
            return count;
        }

        private int GetMaxDepth(TreeNode node)
        {
            if (node == null) return 0;
            if (node.Children.Count == 0) return 1;
            
            int maxChildDepth = 0;
            foreach (var child in node.Children)
            {
                maxChildDepth = Math.Max(maxChildDepth, GetMaxDepth(child));
            }
            return 1 + maxChildDepth;
        }

        private class TreeNode
        {
            public string Value { get; set; }
            public List<TreeNode> Children { get; set; }

            public TreeNode(string value)
            {
                Value = value;
                Children = new List<TreeNode>();
            }
        }
    }
} 