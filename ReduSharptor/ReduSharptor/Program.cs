using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReduSharptor;

namespace ReduceFailingInput
{
    class Program
    {
        /// <summary>
        /// File path for the test example being simplified
        /// </summary>
        private static string testExample { get; set; }

        /// <summary>
        /// Method name for test in file
        /// </summary>
        private static string testName { get; set; }

        /// <summary>
        /// Test solution to be compiled
        /// </summary>
        private static string testProj { get; set; }

        /// <summary>
        /// Output file path for the samples to be written to. Will create additional folders inside
        /// </summary>
        private static string outputFilePath { get; set; }

        /// <summary>
        /// Temporary test file path used during algorithm execution
        /// </summary>
        private static string tempTestFile { get; set; }

        /// <summary>
        /// Build and run the test. Return the result
        /// </summary>
        /// <param name="testStatements">Test statements to test if successful</param>
        /// <returns>True if the test is successful. False if unsuccessful</returns>
        static public bool BuildAndRunTest(List<StatementSyntax> testStatements)
        {
            // NEVER modify the original source file - use temporary copy
            if (string.IsNullOrEmpty(tempTestFile))
            {
                // Create temporary file path
                string tempDir = Path.Combine(Path.GetTempPath(), "ReduSharptor_Temp", DateTime.Now.Ticks.ToString());
                Directory.CreateDirectory(tempDir);
                tempTestFile = Path.Combine(tempDir, Path.GetFileName(testExample));
                
                // Copy original file to temp location
                File.Copy(testExample, tempTestFile, true);
            }

            // Write out statements to TEMPORARY file only
            Extentions.SetTestStatements(testExample, tempTestFile, testName, testStatements);

            Console.WriteLine($"Building current version of test with {testStatements.Count} statements...");

            // Run the build command
            if (!Extentions.ExecuteCommand("dotnet", "build \"" + testProj + "\""))
            {
                Console.WriteLine("Build failed. Continue searching for failing test.");

                // We don't want to record build failures, so we return true to not remember them in the algorithm
                return true;
            }

            Console.WriteLine("Running test for failure...");

            bool isSuccessful = Extentions.ExecuteCommand("dotnet", "test \"" + testProj + "\" --filter \"FullyQualifiedName=" + Extentions.GetTestCallString(tempTestFile, testName) + "\"");

            if (isSuccessful)
            {
                Console.WriteLine("Test was successful. Continue looking for failing test.");
            }
            else
            {
                Console.WriteLine("Test was unsuccessful. Shrink test statements.");
            }

            // Run the test
            return isSuccessful;
        }

        /// <summary>
        /// Shows a few examples about using the Adaptive Extention methods
        /// </summary>
        /// <param name="args">Arguments to control what to simplify; (Path to test, name of test, path to testProj, output path)</param>
        static void Main(string[] args)
        {
            Console.WriteLine("\n\nReduce Failing Input.");
            bool hasOutputFile = false;

            if (args.Length < 3 || args.Length > 4)
            {
                Console.WriteLine("Incorrect arguments\n");
                return;
            }
            else
            {
                Console.WriteLine("Using command line arguments");
                testExample = Path.GetFullPath(args[0]);
                testName = args[1];
                testProj = Path.GetFullPath(args[2]);
            } 

            // Give the option to pass output params. Otherwise use original file.
            if (args.Length == 4)
            {
                outputFilePath = Path.GetFullPath(args[3]);
                hasOutputFile = true;
            }
            else
            {
                outputFilePath = testExample;
            }


            // Validate that user file import already exist
            if (!File.Exists(testExample))
            {
                Console.Write("Test file doesn't exist\n");
                return;
            }
            if (!File.Exists(testProj))
            {
                Console.Write("Sln file doesn't exist\n");
                return;
            }

            // Get test statements in a list
            SyntaxList<StatementSyntax> testStatementsRaw = Extentions.GetTestStatements(testExample, testName);

            List<StatementSyntax> testStatements = new List<StatementSyntax>(testStatementsRaw);

            // Create the function to edit file, build, and run test
            Func<List<StatementSyntax>, bool> buildAndCompareTest = BuildAndRunTest;

            // Copy original file to keep a record
            if (hasOutputFile)
            {
                Extentions.SetTestStatements(testExample, Path.Combine(outputFilePath, "Original", testName + "_" + Path.GetFileName(testExample)), testName, testStatements);
                Console.WriteLine("Here is the starting file.");
            }

            List<StatementSyntax> simplifiedStatements = new List<StatementSyntax>();

            try
            {
                // Run HDD algorithm with parameters
                Console.WriteLine("Using Hierarchical Delta Debugging (HDD) for test reduction...");
                simplifiedStatements = Extentions.FindSmallestFailingInputHDD(testExample, testName, buildAndCompareTest);
                
                // Fallback to original DD if HDD fails
                if (simplifiedStatements == null || simplifiedStatements.Count == 0)
                {
                    Console.WriteLine("HDD failed, falling back to original Delta Debugging...");
                    simplifiedStatements = Extentions.FindSmallestFailingInput<StatementSyntax>(testStatements, buildAndCompareTest);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HDD Error: {ex.Message}");
                Console.WriteLine("Falling back to original Delta Debugging...");
                try
                {
                    simplifiedStatements = Extentions.FindSmallestFailingInput<StatementSyntax>(testStatements, buildAndCompareTest);
                }
                catch (Exception ddEx)
                {
                    Console.WriteLine($"DD Error: {ddEx.Message}");
                    simplifiedStatements = testStatements; // Use original if both fail
                }
            }
            finally
            {
                // Original source file was NEVER modified - no need to revert
                Console.WriteLine("Original source file was never modified - preserved intact.");
                
                // Clean up temporary file
                if (!string.IsNullOrEmpty(tempTestFile) && File.Exists(tempTestFile))
                {
                    try
                    {
                        string tempDir = Path.GetDirectoryName(tempTestFile);
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                        Console.WriteLine("Cleaned up temporary test files.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not clean up temporary files: {ex.Message}");
                    }
                }
                
                if (hasOutputFile)
                {
                    Console.WriteLine("Here is the original file (unchanged)");
                }
            }


            // Output test results
            if (hasOutputFile)
            {
                Extentions.SetTestStatements(testExample, Path.Combine(outputFilePath, "Simplified", testName + "_" + Path.GetFileName(testExample)), testName, simplifiedStatements);
            }
            else
            {
                Extentions.SetTestStatements(testExample, testExample, testName, simplifiedStatements);
            }
            Console.WriteLine("Here are the simpified results.");

        }
    }
}
