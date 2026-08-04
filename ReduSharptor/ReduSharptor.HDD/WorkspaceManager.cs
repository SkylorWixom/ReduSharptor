namespace ReduSharptor.HDD
{
    /// <summary>
    /// The paths for one reduction run: the run folder, the working copy of the
    /// subject project inside it, and where the test file and test project landed
    /// within that copy.
    /// </summary>
    public class RunWorkspace
    {
        public string RunDirectory { get; }
        public string WorkingRoot { get; }
        public string WorkingTestFile { get; }
        public string WorkingTestProject { get; }

        public RunWorkspace(string runDirectory, string workingRoot, string workingTestFile, string workingTestProject)
        {
            RunDirectory = runDirectory;
            WorkingRoot = workingRoot;
            WorkingTestFile = workingTestFile;
            WorkingTestProject = workingTestProject;
        }
    }

    /// <summary>
    /// Creates the per-run working copy of the subject project (design decision 4).
    /// The original source tree is never written to. The copy is the only thing that
    /// gets edited, built, and tested during reduction.
    /// </summary>
    public static class WorkspaceManager
    {
        // Build artifacts and IDE state are excluded from the copy; they are
        // regenerated inside the working copy on the first build.
        private static readonly string[] SkippedDirectories = { "bin", "obj", ".git", ".vs" };

        public static RunWorkspace Create(string testFilePath, string testProjPath, string testMethodName, string outputDir)
        {
            string subjectRoot = FindSubjectRoot(testProjPath);

            string runName = testMethodName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string runDirectory = Path.Combine(outputDir, runName);
            string workingRoot = Path.Combine(runDirectory, "working");
            Directory.CreateDirectory(workingRoot);

            CopyTree(subjectRoot, workingRoot);

            return new RunWorkspace(
                runDirectory,
                workingRoot,
                MapIntoWorkingCopy(testFilePath, subjectRoot, workingRoot),
                MapIntoWorkingCopy(testProjPath, subjectRoot, workingRoot));
        }

        /// <summary>
        /// The subject root is the nearest ancestor folder of the test project that
        /// contains a solution file. For Fleck that is the src folder holding
        /// Fleck.sln, so the whole solution (Fleck and Fleck.Tests) gets copied and
        /// project references inside the copy keep working.
        /// </summary>
        private static string FindSubjectRoot(string testProjPath)
        {
            DirectoryInfo? current = new FileInfo(testProjPath).Directory;

            while (current != null)
            {
                if (current.GetFiles("*.sln").Length > 0)
                {
                    return current.FullName;
                }
                current = current.Parent;
            }

            throw new InvalidOperationException(
                "No solution file found in any folder above " + testProjPath +
                ". The subject project must live under a folder containing a .sln.");
        }

        private static void CopyTree(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(dir);
                if (SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                CopyTree(dir, Path.Combine(destinationDir, name));
            }
        }

        /// <summary>
        /// Translates a path inside the original subject tree to the matching path
        /// inside the working copy.
        /// </summary>
        private static string MapIntoWorkingCopy(string originalPath, string subjectRoot, string workingRoot)
        {
            string relative = Path.GetRelativePath(subjectRoot, originalPath);
            return Path.Combine(workingRoot, relative);
        }
    }
}
