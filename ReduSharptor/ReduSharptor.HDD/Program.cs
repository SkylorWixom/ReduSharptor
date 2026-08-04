namespace ReduSharptor.HDD
{
    /// <summary>
    /// Entry point for the HDD-enabled ReduSharptor.
    /// Takes the same four arguments as the original ReduSharptor, plus an optional
    /// fifth argument for the target framework the tests run against.
    /// </summary>
    internal class Program
    {
        static int Main(string[] args)
        {
            // --inspect prints the test's statement hierarchy and stops; nothing
            // is copied or modified. It can appear anywhere among the arguments.
            bool inspect = args.Contains("--inspect");
            args = args.Where(a => a != "--inspect").ToArray();

            if (args.Length < 4 || args.Length > 5)
            {
                PrintUsage();
                return 1;
            }

            string testFilePath = Path.GetFullPath(args[0]);
            string testMethodName = args[1];
            string testProjPath = Path.GetFullPath(args[2]);
            string outputDir = Path.GetFullPath(args[3]);
            string targetFramework = args.Length == 5 ? args[4] : "net45";

            if (!File.Exists(testFilePath))
            {
                Console.WriteLine("Test file not found: " + testFilePath);
                return 1;
            }
            if (!File.Exists(testProjPath))
            {
                Console.WriteLine("Test project not found: " + testProjPath);
                return 1;
            }

            Console.WriteLine("ReduSharptor.HDD");
            Console.WriteLine("  Test file:        " + testFilePath);
            Console.WriteLine("  Test name:        " + testMethodName);
            Console.WriteLine("  Test project:     " + testProjPath);
            Console.WriteLine("  Output directory: " + outputDir);
            Console.WriteLine("  Target framework: " + targetFramework);
            Console.WriteLine();

            if (inspect)
            {
                // Milestone 2: show the hierarchy the reducer will walk. Reads the
                // original file only; creates and changes nothing.
                var method = StatementTree.FindTestMethod(testFilePath, testMethodName);
                var hierarchy = StatementTree.Build(method);
                StatementTree.Print(hierarchy, testMethodName);
                return 0;
            }

            // Milestone 1: create the run folder and the working copy of the subject
            // project. The original source tree is never written to; every later
            // milestone (candidate edits, builds, test runs) happens inside the copy.
            RunWorkspace workspace = WorkspaceManager.Create(testFilePath, testProjPath, testMethodName, outputDir);

            Console.WriteLine("Run folder:          " + workspace.RunDirectory);
            Console.WriteLine("Working copy root:   " + workspace.WorkingRoot);
            Console.WriteLine("Copied test file:    " + workspace.WorkingTestFile);
            Console.WriteLine("Copied test project: " + workspace.WorkingTestProject);
            Console.WriteLine();

            // The rewriter owns the pristine parse of the working copy's test file.
            // The method it found, the hierarchy, and every candidate all come from
            // that single parse.
            var rewriter = new TestFileRewriter(workspace.WorkingTestFile, testMethodName);
            string fullTestName = StatementTree.GetFullTestName(rewriter.Method);
            Console.WriteLine("Full test name: " + fullTestName);
            Console.WriteLine("Capturing failure fingerprint (builds and runs the working copy)...");

            var oracle = new Oracle(workspace.WorkingTestProject, fullTestName, targetFramework, workspace.RunDirectory);
            FailureFingerprint fingerprint = oracle.CaptureFingerprint();
            Console.WriteLine("  Fingerprint message: " + fingerprint.NormalizedMessage);
            Console.WriteLine();

            // Snapshot the original test before anything is reduced.
            string resultsDir = Path.Combine(workspace.RunDirectory, "results");
            Directory.CreateDirectory(resultsDir);
            File.WriteAllText(Path.Combine(resultsDir, "Original_" + Path.GetFileName(testFilePath)), rewriter.PristineText);

            // Milestone 4: flat reduction over level 0, the baseline-equivalent pass.
            var level0 = StatementTree.Build(rewriter.Method);
            Console.WriteLine("Starting flat reduction (level 0): " + level0.Count + " statements");

            var reducer = new HddReducer(rewriter, oracle, workspace.WorkingTestFile);
            var reduced = reducer.ReduceFlat(level0);

            File.WriteAllText(Path.Combine(resultsDir, "Simplified_" + Path.GetFileName(testFilePath)),
                File.ReadAllText(workspace.WorkingTestFile));

            Console.WriteLine();
            Console.WriteLine("Reduction finished: " + level0.Count + " -> " + reduced.Count + " statements");
            Console.WriteLine("Oracle evaluations: " + oracle.Evaluations + " (cache hits: " + oracle.CacheHits + ")");
            foreach (var pair in reducer.VerdictCounts.OrderBy(p => p.Key))
            {
                Console.WriteLine("  " + pair.Key + ": " + pair.Value);
            }
            Console.WriteLine();
            Console.WriteLine("Reduced test:");
            foreach (var statement in reduced)
            {
                Console.WriteLine("  " + statement.ToString().Trim());
            }
            Console.WriteLine();
            Console.WriteLine("Results folder: " + resultsDir);
            Console.WriteLine("Milestone 4 complete: flat (level 0) reduction. HDD levels arrive in milestone 5.");

            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  ReduSharptor.HDD <testFilePath> <testMethodName> <testProjPath> <outputDir> [targetFramework]");
            Console.WriteLine();
            Console.WriteLine("  testFilePath     Full path to the .cs file containing the failing test.");
            Console.WriteLine("  testMethodName   Name of the failing test method.");
            Console.WriteLine("  testProjPath     Full path to the .csproj of the test project.");
            Console.WriteLine("  outputDir        Folder (outside the subject project) where run folders are created.");
            Console.WriteLine("  targetFramework  Optional. Framework passed to dotnet test. Defaults to net45.");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  --inspect        Print the test's statement hierarchy (levels, Tree/NonTree) and exit.");
        }
    }
}
