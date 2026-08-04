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

            // Milestone 3: capture the failure fingerprint from the untouched
            // working copy, then judge that same untouched state as a self-check.
            // The only acceptable self-check verdict is Preserved.
            var testMethod = StatementTree.FindTestMethod(workspace.WorkingTestFile, testMethodName);
            string fullTestName = StatementTree.GetFullTestName(testMethod);
            Console.WriteLine("Full test name: " + fullTestName);
            Console.WriteLine("Capturing failure fingerprint (builds and runs the working copy)...");

            var oracle = new Oracle(workspace.WorkingTestProject, fullTestName, targetFramework, workspace.RunDirectory);
            FailureFingerprint fingerprint = oracle.CaptureFingerprint();

            Console.WriteLine("  Fingerprint message: " + fingerprint.NormalizedMessage);
            Console.WriteLine();
            Console.WriteLine("Self-check: judging the untouched working copy against the fingerprint...");
            Verdict verdict = oracle.CheckCurrentState("self-check");
            Console.WriteLine("  Verdict: " + verdict);
            Console.WriteLine();
            Console.WriteLine(verdict == Verdict.Preserved
                ? "Milestone 3 complete: oracle is working. Reduction arrives in milestone 4."
                : "PROBLEM: self-check should be Preserved. Something is wrong with the oracle or the setup.");

            return verdict == Verdict.Preserved ? 0 : 1;
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
