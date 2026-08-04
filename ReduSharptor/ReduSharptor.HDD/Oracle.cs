using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReduSharptor.HDD
{
    /// <summary>
    /// The possible judgments for one candidate evaluation.
    /// Only Preserved means "this candidate still fails with the original failure."
    /// </summary>
    public enum Verdict
    {
        Preserved,        // target test failed with the fingerprinted message
        TestPassed,       // target test passed; the failure is gone
        DifferentFailure, // target test failed, but not with the original failure
        BuildFailed,      // candidate does not compile; skipped, never counted as preserved
        TestMissing,      // target test was not found in the results
        InfraError        // test host crash, timeout, or missing results file
    }

    /// <summary>
    /// The identity of the original failure, captured once before reduction.
    /// A candidate preserves the failure only when the same test fails with the
    /// same message (design decision 3).
    /// </summary>
    public class FailureFingerprint
    {
        public string FullTestName { get; }
        public string NormalizedMessage { get; }

        public FailureFingerprint(string fullTestName, string normalizedMessage)
        {
            FullTestName = fullTestName;
            NormalizedMessage = normalizedMessage;
        }
    }

    /// <summary>
    /// Decides whether the working copy, in its current state, still fails with
    /// the original failure. Builds the test project, runs exactly the one target
    /// test, and reads the result from the TRX results file rather than trusting
    /// the process exit code (design decision 3).
    /// </summary>
    public class Oracle
    {
        private const int BuildTimeoutMs = 180_000;
        private const int TestTimeoutMs = 120_000;

        private readonly string _workingTestProject;
        private readonly string _fullTestName;
        private readonly string _targetFramework;
        private readonly string _oracleDirectory;

        private FailureFingerprint? _fingerprint;
        private int _evaluations;

        // Verdict cache keyed by candidate content hash. Ddmin regenerates
        // identical configurations, so repeated states are answered from here
        // without a build or test run.
        private readonly Dictionary<string, Verdict> _verdictCache = new();

        public int Evaluations => _evaluations;
        public int CacheHits { get; private set; }

        /// <summary>
        /// A stable identity for a candidate: the hash of its statements' text.
        /// Two candidates with the same statements in the same order get the same
        /// key, so the cache answers the second one for free.
        /// </summary>
        public static string ComputeKey(IReadOnlyList<StatementSyntax> statements)
        {
            string combined = string.Join("\n", statements.Select(s => s.ToFullString()));
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Judges a candidate: answers from the cache when this exact candidate was
        /// seen before; otherwise applies it (writes the test file), evaluates the
        /// working copy, and caches the verdict.
        /// </summary>
        public Verdict JudgeCandidate(string candidateKey, Action applyCandidate, string label)
        {
            if (_verdictCache.TryGetValue(candidateKey, out Verdict cached))
            {
                CacheHits++;
                return cached;
            }

            applyCandidate();
            Verdict verdict = CheckCurrentState(label);
            _verdictCache[candidateKey] = verdict;
            return verdict;
        }

        public Oracle(string workingTestProject, string fullTestName, string targetFramework, string runDirectory)
        {
            _workingTestProject = workingTestProject;
            _fullTestName = fullTestName;
            _targetFramework = targetFramework;
            _oracleDirectory = Path.Combine(runDirectory, "oracle");
            Directory.CreateDirectory(_oracleDirectory);
        }

        /// <summary>
        /// Runs the untouched working copy once and records the failure identity.
        /// Fails loudly if the working copy does not build or the target test does
        /// not fail, because then there is nothing valid to reduce.
        /// </summary>
        public FailureFingerprint CaptureFingerprint()
        {
            ProcessResult build = RunProcess("dotnet",
                "build \"" + _workingTestProject + "\" -f " + _targetFramework + " -v quiet --nologo",
                BuildTimeoutMs);
            if (!build.Completed || build.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "The working copy does not build; cannot capture a fingerprint.\n" + build.Output);
            }

            TestOutcome outcome = RunTargetTest("fingerprint");
            if (outcome.Kind == TestOutcomeKind.Missing)
            {
                throw new InvalidOperationException(
                    "Target test '" + _fullTestName + "' was not found in the test results. Check the test name.");
            }
            if (outcome.Kind == TestOutcomeKind.Passed)
            {
                throw new InvalidOperationException(
                    "Target test '" + _fullTestName + "' PASSED. Reduction needs a failing test; is the mutant seeded?");
            }
            if (outcome.Kind != TestOutcomeKind.Failed)
            {
                throw new InvalidOperationException(
                    "Could not capture a fingerprint: " + outcome.Detail);
            }

            _fingerprint = new FailureFingerprint(_fullTestName, outcome.NormalizedMessage);
            File.WriteAllText(Path.Combine(_oracleDirectory, "fingerprint.txt"),
                "Test:    " + _fingerprint.FullTestName + Environment.NewLine +
                "Message: " + _fingerprint.NormalizedMessage + Environment.NewLine);

            return _fingerprint;
        }

        /// <summary>
        /// Judges the working copy in its current state against the fingerprint.
        /// The label names the TRX file so every evaluation stays inspectable.
        /// </summary>
        public Verdict CheckCurrentState(string label)
        {
            if (_fingerprint == null)
            {
                throw new InvalidOperationException("CaptureFingerprint must run before candidates are judged.");
            }

            _evaluations++;

            ProcessResult build = RunProcess("dotnet",
                "build \"" + _workingTestProject + "\" -f " + _targetFramework + " -v quiet --nologo",
                BuildTimeoutMs);
            if (!build.Completed)
            {
                return Verdict.InfraError;
            }
            if (build.ExitCode != 0)
            {
                return Verdict.BuildFailed;
            }

            TestOutcome outcome = RunTargetTest(label);
            return outcome.Kind switch
            {
                TestOutcomeKind.Failed when outcome.NormalizedMessage == _fingerprint.NormalizedMessage
                                   => Verdict.Preserved,
                TestOutcomeKind.Failed => Verdict.DifferentFailure,
                TestOutcomeKind.Passed => Verdict.TestPassed,
                TestOutcomeKind.Missing => Verdict.TestMissing,
                _ => Verdict.InfraError
            };
        }

        // ---------- target test execution and TRX parsing ----------

        private enum TestOutcomeKind { Passed, Failed, Missing, Infra }

        private class TestOutcome
        {
            public TestOutcomeKind Kind { get; init; }
            public string NormalizedMessage { get; init; } = "";
            public string Detail { get; init; } = "";
        }

        private TestOutcome RunTargetTest(string label)
        {
            string trxName = label + ".trx";
            string trxPath = Path.Combine(_oracleDirectory, trxName);
            if (File.Exists(trxPath))
            {
                File.Delete(trxPath);
            }

            // --no-build because the build already ran (and was judged) separately.
            // The exit code is ignored on purpose; the TRX file is the evidence.
            ProcessResult test = RunProcess("dotnet",
                "test \"" + _workingTestProject + "\" -f " + _targetFramework + " --no-build --nologo" +
                " --filter \"FullyQualifiedName=" + _fullTestName + "\"" +
                " --logger \"trx;LogFileName=" + trxName + "\"" +
                " --results-directory \"" + _oracleDirectory + "\"",
                TestTimeoutMs);

            if (!test.Completed)
            {
                return new TestOutcome { Kind = TestOutcomeKind.Infra, Detail = "test run timed out" };
            }
            if (!File.Exists(trxPath))
            {
                return new TestOutcome { Kind = TestOutcomeKind.Infra, Detail = "no TRX results file was produced" };
            }

            return ParseTrx(trxPath);
        }

        private TestOutcome ParseTrx(string trxPath)
        {
            XDocument doc = XDocument.Load(trxPath);
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

            var results = doc.Descendants(ns + "UnitTestResult").ToList();
            if (results.Count == 0)
            {
                return new TestOutcome { Kind = TestOutcomeKind.Missing };
            }

            // The exact-match filter runs one test, so one result is expected.
            XElement result = results[0];
            string outcome = result.Attribute("outcome")?.Value ?? "";

            if (outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase))
            {
                return new TestOutcome { Kind = TestOutcomeKind.Passed };
            }
            if (!outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                return new TestOutcome { Kind = TestOutcomeKind.Infra, Detail = "unexpected outcome '" + outcome + "'" };
            }

            string message = result.Descendants(ns + "Message").FirstOrDefault()?.Value ?? "";
            return new TestOutcome { Kind = TestOutcomeKind.Failed, NormalizedMessage = Normalize(message) };
        }

        /// <summary>
        /// Collapses all whitespace runs to single spaces so formatting noise never
        /// breaks a fingerprint comparison.
        /// </summary>
        private static string Normalize(string text)
        {
            return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        // ---------- process execution ----------

        private class ProcessResult
        {
            public bool Completed { get; init; }
            public int ExitCode { get; init; }
            public string Output { get; init; } = "";
        }

        /// <summary>
        /// Runs a command with output captured asynchronously and a timeout that
        /// actually kills the process tree. The baseline's runner waited on a
        /// blocking read, which made its timeout ineffective; this one does not.
        /// </summary>
        private static ProcessResult RunProcess(string fileName, string arguments, int timeoutMs)
        {
            var info = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var output = new StringBuilder();
            using var process = new Process { StartInfo = info };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return new ProcessResult { Completed = false, Output = "timed out after " + timeoutMs + " ms" };
            }

            process.WaitForExit(); // flushes the async output handlers

            lock (output)
            {
                return new ProcessResult { Completed = true, ExitCode = process.ExitCode, Output = output.ToString() };
            }
        }
    }
}
