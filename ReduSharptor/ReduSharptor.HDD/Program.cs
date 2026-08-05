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
            // Flags can appear anywhere among the arguments.
            // --inspect prints the test's statement hierarchy and stops.
            // --verbose mirrors per-candidate removal detail onto the console.
            bool inspect = args.Contains("--inspect");
            bool verbose = args.Contains("--verbose");
            args = args.Where(a => a != "--inspect" && a != "--verbose").ToArray();

            var totalTime = System.Diagnostics.Stopwatch.StartNew();

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
                var inspected = StatementTree.Build(method);
                StatementTree.Print(inspected, testMethodName);
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

            // Full Hierarchical Delta Debugging: top-down levels, ddmin per level,
            // whole sweeps repeated until nothing changes. Every candidate is
            // recorded in run.log and candidates.csv.
            var hierarchy = StatementTree.Build(rewriter.Method);

            using var runLog = new RunLog(workspace.RunDirectory, verbose);
            runLog.Info("Starting HDD reduction...");
            runLog.Info("");

            var reducer = new HddReducer(rewriter, oracle, workspace.WorkingTestFile, runLog);
            ReductionResult result = reducer.Reduce(hierarchy);

            File.WriteAllText(Path.Combine(resultsDir, "Simplified_" + Path.GetFileName(testFilePath)),
                File.ReadAllText(workspace.WorkingTestFile));

            totalTime.Stop();
            runLog.WriteSummary(result, fingerprint, oracle.Evaluations, oracle.CacheHits,
                reducer.VerdictCounts, totalTime.Elapsed);

            runLog.Info("");
            runLog.Info("Reduced test method:");
            runLog.Info(result.FinalMethodText);
            runLog.Info("");
            runLog.Info("Run folder contents: working\\ (reduced copy), results\\ (snapshots), oracle\\ (fingerprint + TRX), run.log, candidates.csv, summary.txt");

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
            Console.WriteLine("  --verbose        Also print each candidate's attempted removals on the console.");
        }
    }
}
