using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReduSharptor.HDD
{
    /// <summary>
    /// One statement in the test method's hierarchy: its Roslyn syntax node, its
    /// level (0 = directly in the method body), its category, and the statements
    /// nested inside it. A statement is a Tree statement when at least one
    /// statement is nested anywhere inside it; otherwise it is NonTree
    /// (reducibility paper, Section IV).
    /// </summary>
    public class StatementNode
    {
        public StatementSyntax Syntax { get; }
        public int Level { get; }
        public List<StatementNode> Children { get; } = new();
        public bool IsTree => Children.Count > 0;

        public StatementNode(StatementSyntax syntax, int level)
        {
            Syntax = syntax;
            Level = level;
        }
    }

    /// <summary>
    /// Holds the pristine parse of the test file and writes candidate versions of
    /// it (design decision 4: the working copy's test file is the only file ever
    /// written). Every candidate is produced from the pristine tree, never from a
    /// previously written candidate, so no reduction step can corrupt the next.
    /// </summary>
    public class TestFileRewriter
    {
        private readonly SyntaxNode _pristineRoot;

        public MethodDeclarationSyntax Method { get; }
        public string PristineText { get; }

        public TestFileRewriter(string testFilePath, string testMethodName)
        {
            PristineText = File.ReadAllText(testFilePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(PristineText);
            _pristineRoot = tree.GetRoot();
            Method = StatementTree.FindTestMethod(tree, testMethodName, testFilePath);
        }

        /// <summary>
        /// Writes a version of the test file in which the test method's body
        /// contains exactly the given statements, in the given order.
        /// </summary>
        public void Write(IReadOnlyList<StatementSyntax> statements, string destinationPath)
        {
            MethodDeclarationSyntax newMethod = Method.WithBody(SyntaxFactory.Block(statements));
            SyntaxNode newRoot = _pristineRoot.ReplaceNode(Method, newMethod);
            File.WriteAllText(destinationPath, newRoot.ToFullString());
        }

        /// <summary>
        /// Renders the full test file with the given statements removed, wherever
        /// they live in the hierarchy (top level, inside blocks, inside lambda
        /// bodies). All removed nodes must come from this rewriter's pristine
        /// parse. Roslyn removes them all in one pass, so nested and top-level
        /// removals combine safely.
        /// </summary>
        public string Render(IReadOnlyCollection<SyntaxNode> removedNodes)
        {
            if (removedNodes.Count == 0)
            {
                return _pristineRoot.ToFullString();
            }

            MethodDeclarationSyntax? newMethod = Method.RemoveNodes(removedNodes, SyntaxRemoveOptions.KeepNoTrivia);
            if (newMethod == null)
            {
                // Cannot happen while removal is restricted to block-parented
                // statements, but fail loudly rather than write a broken file.
                throw new InvalidOperationException("Removing the requested nodes destroyed the method itself.");
            }

            SyntaxNode newRoot = _pristineRoot.ReplaceNode(Method, newMethod);
            return newRoot.ToFullString();
        }

        /// <summary>
        /// The method body with the given nodes removed, for display.
        /// </summary>
        public string RenderMethod(IReadOnlyCollection<SyntaxNode> removedNodes)
        {
            MethodDeclarationSyntax? newMethod = removedNodes.Count == 0
                ? Method
                : Method.RemoveNodes(removedNodes, SyntaxRemoveOptions.KeepNoTrivia);
            return newMethod?.ToString() ?? "";
        }
    }

    /// <summary>
    /// Builds the statement hierarchy for a test method (design decision 2).
    /// Statements are the only unit. BlockStmts are transparent containers and
    /// never appear as nodes themselves; the statements inside a block belong to
    /// the enclosing statement's next level. Statements inside lambda bodies
    /// (action statements) count as nested statements too.
    /// </summary>
    public static class StatementTree
    {
        /// <summary>
        /// Parses the test file and finds the named test method anywhere in it,
        /// regardless of namespace style or how many classes the file contains.
        /// </summary>
        public static MethodDeclarationSyntax FindTestMethod(string testFilePath, string testMethodName)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(testFilePath));
            return FindTestMethod(tree, testMethodName, testFilePath);
        }

        /// <summary>
        /// Same search over an already-parsed tree. The rewriter uses this so that
        /// the method (and the statement nodes built from it) belong to the exact
        /// tree it rewrites; mixing nodes from two parses of the same file would
        /// make Roslyn's ReplaceNode silently fail to match.
        /// </summary>
        public static MethodDeclarationSyntax FindTestMethod(SyntaxTree tree, string testMethodName, string sourceDescription)
        {
            var matches = tree.GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == testMethodName)
                .ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException(
                    "No method named '" + testMethodName + "' found in " + sourceDescription);
            }
            if (matches.Count > 1)
            {
                Console.WriteLine("Warning: " + matches.Count + " methods named '" + testMethodName +
                                  "' found; using the first one.");
            }

            return matches[0];
        }

        /// <summary>
        /// Derives the fully qualified test name (namespace.class.method) that
        /// dotnet test's FullyQualifiedName filter expects. Handles both
        /// block-scoped and file-scoped namespaces.
        /// </summary>
        public static string GetFullTestName(MethodDeclarationSyntax method)
        {
            var parts = new List<string> { method.Identifier.Text };

            SyntaxNode? current = method.Parent;
            while (current != null)
            {
                if (current is ClassDeclarationSyntax cls)
                {
                    parts.Insert(0, cls.Identifier.Text);
                }
                else if (current is BaseNamespaceDeclarationSyntax ns)
                {
                    parts.Insert(0, ns.Name.ToString());
                }
                current = current.Parent;
            }

            return string.Join(".", parts);
        }

        /// <summary>
        /// Builds the level-0 statement nodes for the method body and, recursively,
        /// the nested levels below each Tree statement.
        /// </summary>
        public static List<StatementNode> Build(MethodDeclarationSyntax method)
        {
            if (method.Body == null)
            {
                throw new InvalidOperationException(
                    "Test method '" + method.Identifier.Text + "' has no body block (expression-bodied methods are not supported).");
            }

            return BuildLevel(method.Body, level: 0);
        }

        private static List<StatementNode> BuildLevel(SyntaxNode container, int level)
        {
            var nodes = new List<StatementNode>();

            foreach (StatementSyntax statement in ChildStatements(container))
            {
                var node = new StatementNode(statement, level);
                node.Children.AddRange(BuildLevel(statement, level + 1));
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Enumerates the statements that sit directly under a node, treating
        /// blocks and other wrappers (else clauses, catch clauses, lambda bodies)
        /// as transparent. Does not descend into the statements it yields; the
        /// caller recurses on each one to build the next level.
        /// </summary>
        private static IEnumerable<StatementSyntax> ChildStatements(SyntaxNode node)
        {
            foreach (SyntaxNode child in node.ChildNodes())
            {
                if (child is BlockSyntax block)
                {
                    // A block is structural, never a unit itself; surface what is inside it.
                    foreach (var nested in ChildStatements(block))
                    {
                        yield return nested;
                    }
                }
                else if (child is StatementSyntax statement)
                {
                    yield return statement;
                }
                else
                {
                    // Conditions, else clauses, catch/finally clauses, invocation
                    // arguments, lambda bodies: recurse until statements appear.
                    foreach (var nested in ChildStatements(child))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>
        /// Prints the hierarchy with level numbers and Tree/NonTree tags, then the
        /// counts that RQ4 reports on: total statements, NonTree (#NTN), Tree (#TN).
        /// </summary>
        public static void Print(List<StatementNode> roots, string testMethodName)
        {
            Console.WriteLine("Statement hierarchy for " + testMethodName + ":");
            Console.WriteLine();

            int total = 0, treeCount = 0, maxLevel = 0;
            PrintNodes(roots, ref total, ref treeCount, ref maxLevel);

            Console.WriteLine();
            Console.WriteLine("Totals: " + total + " statements | NonTree (#NTN): " + (total - treeCount) +
                              " | Tree (#TN): " + treeCount + " | deepest level: " + maxLevel);
        }

        private static void PrintNodes(List<StatementNode> nodes, ref int total, ref int treeCount, ref int maxLevel)
        {
            foreach (StatementNode node in nodes)
            {
                total++;
                if (node.IsTree) treeCount++;
                if (node.Level > maxLevel) maxLevel = node.Level;

                string indent = new string(' ', node.Level * 4);
                string tag = node.IsTree ? "[Tree]   " : "[NonTree]";
                Console.WriteLine(indent + "L" + node.Level + " " + tag + " " + Snippet(node.Syntax));

                PrintNodes(node.Children, ref total, ref treeCount, ref maxLevel);
            }
        }

        /// <summary>
        /// First line of the statement, whitespace collapsed, trimmed to 70 characters.
        /// </summary>
        private static string Snippet(StatementSyntax statement)
        {
            string text = string.Join(" ",
                statement.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 70 ? text : text.Substring(0, 67) + "...";
        }
    }
}
