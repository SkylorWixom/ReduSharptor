using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ReduSharptor
{
    internal class Extentions
    {
        #region Private Methods

        /// <summary>
        /// Divides the array into equal parts
        /// </summary>
        /// <typeparam name="T">Type of the list to be split</typeparam>
        /// <param name="sizeOfArrays">Size of the parts to be split into</param>
        /// <param name="array">Array to be split</param>
        /// <returns>A list of equal parts of the array</returns>
        static private List<List<T>> GetDividedSections<T>(int numSections, List<T> array)
        {
            List<List<T>> result = new List<List<T>>();

            // Add all sub lists in list array
            for (int i = 0; i < numSections; i++)
            {
                result.Add(new List<T>());
            }

            int split = array.Count / numSections;
            int innerList = 0;

            if (split == 0)
            {
                return result;
            }

            for (int i = 0; i < array.Count; i += split)
            {
                for (int j = i; j < array.Count && j < i + split; j++)
                {
                    if (innerList >= numSections)
                    {
                        innerList--;
                    }
                    result[innerList].Add(array[j]);
                }


                innerList++;
            }

            return result;
        }

        /// <summary>
        /// Gets the compliment of the section provided
        /// </summary>
        /// <typeparam name="T">Type of the list to get the compliment of</typeparam>
        /// <param name="array">Array to get compliment from</param>
        /// <param name="sectionIndex">Index of the section to get the compliment of</param>
        /// <returns>Compliment of the section index provided</returns>
        static private List<T> GetSectionCompliment<T>(List<List<T>> array, int sectionIndex)
        {
            List<T> compliment = new List<T>();

            foreach (List<T> section in array)
            {
                if (array.IndexOf(section) == sectionIndex)
                {
                    continue;
                }

                compliment.AddRange(section);
            }

            return compliment;
        }

        #endregion

        #region Public Methods

        #region File Editing

        /// <summary>
        /// Gets the statement list for the test file provided
        /// </summary>
        /// <param name="testFilePath">Test file path for the statement list</param>
        /// <returns>Statement list for the test provided</returns>
        static public SyntaxList<StatementSyntax> GetTestStatements(string testFilePath, string testName)
        {
            string text = File.ReadAllText(testFilePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);

            CompilationUnitSyntax input = tree.GetCompilationUnitRoot();
            var nameSpaceOriginal = ((NamespaceDeclarationSyntax)input.Members[0]);
            var classOriginal = (ClassDeclarationSyntax)nameSpaceOriginal.Members[0];

            var classMembers = classOriginal.DescendantNodes().OfType<MemberDeclarationSyntax>();
            MethodDeclarationSyntax method = null;

            foreach (var member in classMembers)
            {
                var potentialMethod = member as MethodDeclarationSyntax;
                if (potentialMethod != null)
                {
                    if (potentialMethod.Identifier.ToString() == testName)
                    {
                        method = potentialMethod;
                    }
                }
            }

            var blockX = (BlockSyntax)method?.Body;

            return blockX.Statements;
        }

        /// <summary>
        /// Gets the statement list for the test file provided
        /// </summary>
        /// <param name="testFilePath">Test file path for the statement list</param>
        /// <returns>Statement list for the test provided</returns>
        static public bool SetTestStatements(string testFilePath, string outputFilePath, string testName, List<StatementSyntax> statementsToReplace)
        {
            if (!File.Exists(outputFilePath))
            {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(outputFilePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                    }

                    FileStream file = File.Create(outputFilePath);
                    file.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("File was not created at " + outputFilePath + ". " + ex.Message);
                }
            }

            string text = File.ReadAllText(testFilePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);

            CompilationUnitSyntax input = tree.GetCompilationUnitRoot();
            var nameSpaceOriginal = ((NamespaceDeclarationSyntax)input.Members[0]);
            var classOriginal = (ClassDeclarationSyntax)nameSpaceOriginal.Members[0];

            MethodDeclarationSyntax methodSyntax = null;
            var classMembers = classOriginal.DescendantNodes().OfType<MemberDeclarationSyntax>();

            foreach (var member in classMembers)
            {
                var method = member as MethodDeclarationSyntax;
                if (method != null)
                {
                    if (method.Identifier.ToString() == testName)
                    {
                        methodSyntax = method;
                        break;
                    }
                }
            }

            if (methodSyntax == null)
            {
                return false;
            }

            var blockX = (BlockSyntax)methodSyntax.Body;

            var statements = blockX.RemoveNodes(blockX.Statements, SyntaxRemoveOptions.KeepNoTrivia);

            var x = statements.AddStatements(statementsToReplace.ToArray());

            MethodDeclarationSyntax tempMethod = methodSyntax.WithBody(x);
            var newClass = classOriginal.ReplaceNode(methodSyntax, tempMethod);
            var output = input.ReplaceNode(classOriginal, newClass);
            System.Diagnostics.Debug.WriteLine(tempMethod.ToString());

            System.Console.WriteLine("\n\n--------------------------------------\n" + tempMethod.ToString());

            WaitForFile(outputFilePath);
            Console.Write(output.ToString());
            File.WriteAllText(outputFilePath, output.ToString());

            return true;
        }

        /// <summary>
        /// Gets the string to only build the one test instead of the entire project.
        /// </summary>
        /// <param name="testFilePath">Path to the test file</param>
        /// <param name="testName">Name of the test</param>
        /// <returns></returns>
        static public string GetTestCallString(string testFilePath, string testName)
        {
            string text = File.ReadAllText(testFilePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);

            CompilationUnitSyntax input = tree.GetCompilationUnitRoot();
            var nameSpaceOriginal = ((NamespaceDeclarationSyntax)input.Members[0]);
            var classOriginal = (ClassDeclarationSyntax)nameSpaceOriginal.Members[0];

            return nameSpaceOriginal.Name + "." + classOriginal.Identifier + "." + testName;
        }

        /// <summary>
        /// Blocks until the file is not locked any more.
        /// </summary>
        /// <param name="fullPath"></param>
        public static bool WaitForFile(string fullPath)
        {
            int numTries = 0;
            while (true)
            {
                ++numTries;
                try
                {
                    // Attempt to open the file exclusively.
                    using (FileStream fs = new FileStream(fullPath,
                        FileMode.Open, FileAccess.ReadWrite,
                        FileShare.None, 100))
                    {
                        fs.ReadByte();

                        // If we got this far the file is ready
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(500);
                    if (numTries > 100)
                    {
                        return false;
                    }

                    // Wait for the lock to be released
                    System.Threading.Thread.Sleep(500);
                }
            }

            return true;
        }

        /// <summary>
        /// Runs a cmd command from another process
        /// </summary>
        /// <param name="fileName">Command to run</param>
        /// <param name="arguments">Arguments to run with the command</param>
        /// <returns>True if successful; False if unsucessful</returns>
        static public bool ExecuteCommand(string fileName, string arguments, int timeout = 5000)
        {
            try
            {
                ProcessStartInfo processInfo;
                Process process;

                processInfo = new ProcessStartInfo(fileName, arguments);
                //processInfo.CreateNoWindow = false;
                //processInfo.UseShellExecute = false;
                processInfo.RedirectStandardOutput = true;

                process = new Process();
                process.StartInfo = processInfo;

                process.Start();

                process.WaitForExit(timeout);
                string output = process.StandardOutput.ReadToEnd();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Finds the smallest input for the test and input provided for the test to continue to fail
        /// </summary>
        /// <typeparam name="T">Type of the list in the input</typeparam>
        /// <param name="array">Input array for the failing test</param>
        /// <param name="compareTestInput">Function to compare the test input against</param>
        /// <returns>A list of the smallest failing input for the test to continue to fail</returns>
        static public List<T> FindSmallestFailingInput<T>(List<T> array, Func<List<T>, bool> compareTestInput)
        {
            // Amount of items to split the groups into
            int numSections = 2;

            while (true)
            {
                bool isSuccessful = true;

                // Divides the array into equal sized sections
                List<List<T>> sectionedArray = GetDividedSections(numSections, array);

                // Test the sections for failing input
                foreach (List<T> arrSection in sectionedArray)
                {
                    isSuccessful = compareTestInput(arrSection);

                    if (!isSuccessful && arrSection.Any())
                    {
                        // Section off failing input and try again
                        array = arrSection;
                        numSections = 2;

                        break;
                    }

                }

                if (!isSuccessful)
                {
                    continue;
                }

                // Test the compliments of the sections for failing input
                foreach (List<T> arrSection in sectionedArray)
                {
                    List<T> compliment = GetSectionCompliment(sectionedArray, sectionedArray.IndexOf(arrSection));
                    isSuccessful = compareTestInput(compliment);

                    if (!isSuccessful && compliment.Any())
                    {
                        // Section off failing input and try again
                        array = compliment;

                        // n = max(n-1, 2)
                        numSections = Math.Max(numSections - 1, 2);

                        break;
                    }

                }

                if (!isSuccessful)
                {
                    continue;
                }

                // If all previous inputs pass, increase granularity, create more equal parts
                // array = array;
                // Math.Min(2 * numSections, array.Count);
                numSections = 2 * numSections;

                if (numSections > array.Count)
                {
                    return array;
                    //break;
                }

                continue;
            }

            return array;
        }

        #endregion

        #region Hierarchical Delta Debugging (HDD)

        /// <summary>
        /// Represents different granularity levels for hierarchical reduction
        /// </summary>
        public enum GranularityLevel
        {
            Method = 1,      // Coarsest - whole methods
            Statement = 2,   // Current DD level - individual statements  
            Expression = 3,  // Expressions within statements
            Token = 4        // Finest - individual tokens
        }

        /// <summary>
        /// Represents a hierarchical node in the AST that can be reduced
        /// </summary>
        public class HierarchicalNode
        {
            public SyntaxNode Node { get; set; }
            public GranularityLevel Level { get; set; }
            public List<HierarchicalNode> Children { get; set; }
            public HierarchicalNode Parent { get; set; }
            public int Position { get; set; }

            public HierarchicalNode()
            {
                Children = new List<HierarchicalNode>();
            }

            public override string ToString()
            {
                return $"{Level}: {Node.GetType().Name} - {Node.ToString().Substring(0, Math.Min(50, Node.ToString().Length))}...";
            }
        }

        /// <summary>
        /// Builds a hierarchical representation of the test method's AST
        /// </summary>
        /// <param name="testFilePath">Path to the test file</param>
        /// <param name="testName">Name of the test method</param>
        /// <returns>Root hierarchical node representing the test method</returns>
        static public HierarchicalNode BuildHierarchicalAST(string testFilePath, string testName)
        {
            string text = File.ReadAllText(testFilePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            
            var nameSpace = ((NamespaceDeclarationSyntax)root.Members[0]);
            var classDecl = (ClassDeclarationSyntax)nameSpace.Members[0];
            
            // Find the test method
            MethodDeclarationSyntax testMethod = null;
            var classMembers = classDecl.DescendantNodes().OfType<MemberDeclarationSyntax>();
            foreach (var member in classMembers)
            {
                var method = member as MethodDeclarationSyntax;
                if (method != null && method.Identifier.ToString() == testName)
                {
                    testMethod = method;
                    break;
                }
            }

            if (testMethod == null) return null;

            // Build hierarchical structure starting from method level
            var rootNode = new HierarchicalNode
            {
                Node = testMethod,
                Level = GranularityLevel.Method,
                Position = 0
            };

            BuildHierarchyRecursive(rootNode);
            return rootNode;
        }

        /// <summary>
        /// Recursively builds the hierarchical structure of the AST
        /// </summary>
        /// <param name="parentNode">Parent node to build children for</param>
        static private void BuildHierarchyRecursive(HierarchicalNode parentNode)
        {
            switch (parentNode.Level)
            {
                case GranularityLevel.Method:
                    // Method level -> Statement level
                    if (parentNode.Node is MethodDeclarationSyntax method && method.Body != null)
                    {
                        var statements = method.Body.Statements;
                        for (int i = 0; i < statements.Count; i++)
                        {
                            var childNode = new HierarchicalNode
                            {
                                Node = statements[i],
                                Level = GranularityLevel.Statement,
                                Parent = parentNode,
                                Position = i
                            };
                            parentNode.Children.Add(childNode);
                            BuildHierarchyRecursive(childNode);
                        }
                    }
                    break;

                case GranularityLevel.Statement:
                    // Statement level -> Expression level
                    var expressions = GetExpressionsFromStatement(parentNode.Node);
                    for (int i = 0; i < expressions.Count; i++)
                    {
                        var childNode = new HierarchicalNode
                        {
                            Node = expressions[i],
                            Level = GranularityLevel.Expression,
                            Parent = parentNode,
                            Position = i
                        };
                        parentNode.Children.Add(childNode);
                        BuildHierarchyRecursive(childNode);
                    }
                    break;

                case GranularityLevel.Expression:
                    // Expression level -> Token level (for fine-grained reduction)
                    var tokens = GetTokensFromExpression(parentNode.Node);
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        var childNode = new HierarchicalNode
                        {
                            Node = tokens[i],
                            Level = GranularityLevel.Token,
                            Parent = parentNode,
                            Position = i
                        };
                        parentNode.Children.Add(childNode);
                    }
                    break;

                case GranularityLevel.Token:
                    // Token is the finest level - no children
                    break;
            }
        }

        /// <summary>
        /// Extracts expressions from a statement
        /// </summary>
        /// <param name="statement">Statement to extract expressions from</param>
        /// <returns>List of expressions found in the statement</returns>
        static private List<SyntaxNode> GetExpressionsFromStatement(SyntaxNode statement)
        {
            var expressions = new List<SyntaxNode>();
            
            // Get all expression nodes within the statement
            var expressionNodes = statement.DescendantNodes()
                .Where(node => node is ExpressionSyntax && node.Parent == statement)
                .ToList();
            
            expressions.AddRange(expressionNodes);
            
            // Handle specific statement types with multiple expressions
            switch (statement)
            {
                case LocalDeclarationStatementSyntax localDecl:
                    // Variable declarations can have initializers
                    foreach (var variable in localDecl.Declaration.Variables)
                    {
                        if (variable.Initializer != null)
                        {
                            expressions.Add(variable.Initializer.Value);
                        }
                    }
                    break;
                    
                case IfStatementSyntax ifStmt:
                    expressions.Add(ifStmt.Condition);
                    break;
                    
                case WhileStatementSyntax whileStmt:
                    expressions.Add(whileStmt.Condition);
                    break;
                    
                case ForStatementSyntax forStmt:
                    if (forStmt.Condition != null) expressions.Add(forStmt.Condition);
                    expressions.AddRange(forStmt.Initializers);
                    expressions.AddRange(forStmt.Incrementors);
                    break;
            }
            
            return expressions.Distinct().ToList();
        }

        /// <summary>
        /// Extracts tokens from an expression for fine-grained reduction
        /// </summary>
        /// <param name="expression">Expression to extract tokens from</param>
        /// <returns>List of token nodes</returns>
        static private List<SyntaxNode> GetTokensFromExpression(SyntaxNode expression)
        {
            var tokens = new List<SyntaxNode>();
            
            // For now, we'll consider immediate children as "tokens"
            // This can be refined to actual token-level analysis
            tokens.AddRange(expression.ChildNodes());
            
            return tokens;
        }

        /// <summary>
        /// Core HDD algorithm that reduces failing input hierarchically
        /// </summary>
        /// <param name="testFilePath">Path to test file</param>
        /// <param name="testName">Name of test method</param>
        /// <param name="compareTestInput">Function to test if input still fails</param>
        /// <returns>Hierarchically reduced minimal failing test</returns>
        static public List<StatementSyntax> FindSmallestFailingInputHDD(string testFilePath, string testName, Func<List<StatementSyntax>, bool> compareTestInput)
        {
            Console.WriteLine("Starting Hierarchical Delta Debugging (HDD)...");
            
            // Get all statements from the test method
            var allStatements = GetTestStatements(testFilePath, testName);
            var currentStatements = new List<StatementSyntax>(allStatements);
            
            Console.WriteLine($"Original test has {currentStatements.Count} statements");
            
            // For now, just call the original DD algorithm directly
            // The "hierarchy" aspect will be added later after we ensure basic functionality works
            var reducedStatements = FindSmallestFailingInput(currentStatements, compareTestInput);
            
            Console.WriteLine($"HDD completed. Final test has {reducedStatements.Count} statements (reduced by {allStatements.Count - reducedStatements.Count})");
            return reducedStatements;
        }

        /// <summary>
        /// Applies HDD at a specific granularity level
        /// </summary>
        static private List<StatementSyntax> ApplyHDDAtLevel(List<StatementSyntax> statements, HierarchicalNode rootNode, GranularityLevel level, Func<List<StatementSyntax>, bool> compareTestInput)
        {
            var currentStatements = new List<StatementSyntax>(statements);
            
            // Get nodes at the specified level
            var nodesAtLevel = GetNodesAtLevel(rootNode, level);
            
            Console.WriteLine($"Applying HDD at {level} level with {nodesAtLevel.Count} nodes");

            // Apply standard DD algorithm at this level
            return FindSmallestFailingInput(currentStatements, compareTestInput);
        }

        /// <summary>
        /// Applies HDD at expression level for finer-grained reduction
        /// </summary>
        static private List<StatementSyntax> ApplyHDDAtExpressionLevel(List<StatementSyntax> statements, string testFilePath, string testName, Func<List<StatementSyntax>, bool> compareTestInput)
        {
            var currentStatements = new List<StatementSyntax>(statements);
            bool madeProgress = true;

            while (madeProgress && currentStatements.Count > 1)
            {
                madeProgress = false;
                
                // Try to simplify each statement by removing or simplifying expressions
                for (int i = 0; i < currentStatements.Count; i++)
                {
                    var simplifiedStatement = TrySimplifyStatement(currentStatements[i]);
                    if (simplifiedStatement != null && !simplifiedStatement.IsEquivalentTo(currentStatements[i]))
                    {
                        var testStatements = new List<StatementSyntax>(currentStatements);
                        testStatements[i] = simplifiedStatement;
                        
                        // Test if the simplified version still fails
                        if (!compareTestInput(testStatements))
                        {
                            currentStatements[i] = simplifiedStatement;
                            madeProgress = true;
                            Console.WriteLine($"Simplified statement {i + 1}: {simplifiedStatement.ToString().Trim()}");
                        }
                    }
                }
            }

            return currentStatements;
        }

        /// <summary>
        /// Attempts to simplify a statement by removing or simplifying expressions
        /// </summary>
        static private StatementSyntax TrySimplifyStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case LocalDeclarationStatementSyntax localDecl:
                    // Try to remove initializers
                    if (localDecl.Declaration.Variables.Any(v => v.Initializer != null))
                    {
                        var newVariables = localDecl.Declaration.Variables.Select(v => 
                            v.Initializer != null ? v.WithInitializer(null) : v).ToArray();
                        var newDeclaration = localDecl.Declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
                        return localDecl.WithDeclaration(newDeclaration);
                    }
                    break;

                case ExpressionStatementSyntax exprStmt:
                    // Try to simplify complex expressions
                    if (exprStmt.Expression is AssignmentExpressionSyntax assignment)
                    {
                        // Try to simplify the right-hand side
                        var simplifiedRhs = TrySimplifyExpression(assignment.Right);
                        if (simplifiedRhs != null && !simplifiedRhs.IsEquivalentTo(assignment.Right))
                        {
                            var newAssignment = assignment.WithRight(simplifiedRhs);
                            return exprStmt.WithExpression(newAssignment);
                        }
                    }
                    break;

                case IfStatementSyntax ifStmt:
                    // Try to simplify the condition
                    var simplifiedCondition = TrySimplifyExpression(ifStmt.Condition);
                    if (simplifiedCondition != null && !simplifiedCondition.IsEquivalentTo(ifStmt.Condition))
                    {
                        return ifStmt.WithCondition(simplifiedCondition);
                    }
                    break;
            }

            return null; // No simplification possible
        }

        /// <summary>
        /// Attempts to simplify an expression
        /// </summary>
        static private ExpressionSyntax TrySimplifyExpression(ExpressionSyntax expression)
        {
            switch (expression)
            {
                case BinaryExpressionSyntax binary:
                    // Try to replace with simple literals
                    if (binary.OperatorToken.IsKind(SyntaxKind.PlusToken) ||
                        binary.OperatorToken.IsKind(SyntaxKind.MinusToken) ||
                        binary.OperatorToken.IsKind(SyntaxKind.AsteriskToken) ||
                        binary.OperatorToken.IsKind(SyntaxKind.SlashToken))
                    {
                        // Replace with a simple number
                        return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1));
                    }
                    else if (binary.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||
                             binary.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken))
                    {
                        // Replace with simple boolean
                        return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
                    }
                    break;

                case InvocationExpressionSyntax invocation:
                    // Try to simplify method calls by removing arguments
                    if (invocation.ArgumentList.Arguments.Count > 1)
                    {
                        var firstArg = invocation.ArgumentList.Arguments[0];
                        var newArgs = SyntaxFactory.SeparatedList(new[] { firstArg });
                        var newArgList = invocation.ArgumentList.WithArguments(newArgs);
                        return invocation.WithArgumentList(newArgList);
                    }
                    break;
            }

            return null; // No simplification possible
        }

        /// <summary>
        /// Gets all nodes at a specific granularity level
        /// </summary>
        static private List<HierarchicalNode> GetNodesAtLevel(HierarchicalNode root, GranularityLevel level)
        {
            var nodesAtLevel = new List<HierarchicalNode>();
            CollectNodesAtLevel(root, level, nodesAtLevel);
            return nodesAtLevel;
        }

        /// <summary>
        /// Recursively collects nodes at a specific level
        /// </summary>
        static private void CollectNodesAtLevel(HierarchicalNode node, GranularityLevel targetLevel, List<HierarchicalNode> result)
        {
            if (node.Level == targetLevel)
            {
                result.Add(node);
            }

            foreach (var child in node.Children)
            {
                CollectNodesAtLevel(child, targetLevel, result);
            }
        }

        #endregion
    }
}
