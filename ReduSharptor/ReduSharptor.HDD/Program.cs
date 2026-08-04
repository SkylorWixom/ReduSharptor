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

            // Milestone 1: create the run folder and the working copy of the subject
            // project. The original source tree is never written to; every later
            // milestone (candidate edits, builds, test runs) happens inside the copy.
            RunWorkspace workspace = WorkspaceManager.Create(testFilePath, testProjPath, testMethodName, outputDir);

            Console.WriteLine("Run folder:          " + workspace.RunDirectory);
            Console.WriteLine("Working copy root:   " + workspace.WorkingRoot);
            Console.WriteLine("Copied test file:    " + workspace.WorkingTestFile);
            Console.WriteLine("Copied test project: " + workspace.WorkingTestProject);
            Console.WriteLine();
            Console.WriteLine("Milestone 1 complete: working copy created. Reduction arrives in later milestones.");

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
        }
    }
}
