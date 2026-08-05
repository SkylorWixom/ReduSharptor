using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReduSharptor.HDD
{
    /// <summary>
    /// The run's written record: run.log (human-readable, every candidate with the
    /// statements it attempted to remove), candidates.csv (one machine-readable
    /// row per candidate), and summary.txt (run totals plus tool-computed
    /// cross-check measures). The console shows compact progress; --verbose mirrors
    /// the per-candidate detail there too.
    /// </summary>
    public class RunLog : IDisposable
    {
        private readonly StreamWriter _log;
        private readonly StreamWriter _csv;
        private readonly string _runDirectory;
        private readonly bool _verbose;

        public RunLog(string runDirectory, bool verbose)
        {
            _runDirectory = runDirectory;
            _verbose = verbose;
            _log = new StreamWriter(Path.Combine(runDirectory, "run.log")) { AutoFlush = true };
            _csv = new StreamWriter(Path.Combine(runDirectory, "candidates.csv")) { AutoFlush = true };
            _csv.WriteLine("candidate,sweep,level,keptCount,levelTotal,verdict,fromCache,durationMs");
        }

        /// <summary>Writes to both the console and run.log.</summary>
        public void Info(string message)
        {
            Console.WriteLine(message);
            _log.WriteLine(message);
        }

        public void Candidate(string label, int sweep, int level,
            IReadOnlyList<StatementNode> kept, IReadOnlyList<StatementNode> attemptedRemove,
            int levelTotal, Verdict verdict, bool fromCache, long durationMs)
        {
            string line = "    [" + label + "] L" + level + " keep " + kept.Count + "/" + levelTotal +
                          " -> " + verdict + (fromCache ? " (cache)" : "");
            Console.WriteLine(line);
            _log.WriteLine(line);

            _log.WriteLine("      attempted to remove:");
            foreach (StatementNode node in attemptedRemove)
            {
                _log.WriteLine("        - " + Snippet(node.Syntax));
            }

            if (_verbose)
            {
                foreach (StatementNode node in attemptedRemove)
                {
                    Console.WriteLine("        remove: " + Snippet(node.Syntax));
                }
            }

            _csv.WriteLine(label + "," + sweep + "," + level + "," + kept.Count + "," + levelTotal + "," +
                           verdict + "," + fromCache + "," + durationMs);
        }

        /// <summary>
        /// Writes summary.txt: run identity, oracle statistics, and the
        /// tool-computed measures. The measures are a CROSS-CHECK ONLY; the
        /// analysis of record is performed by hand.
        /// </summary>
        public void WriteSummary(ReductionResult result, FailureFingerprint fingerprint,
            int evaluations, int cacheHits, IReadOnlyDictionary<Verdict, int> verdictCounts, TimeSpan elapsed)
        {
            int totalBefore = result.StatementsBefore;
            int ars = result.StatementsBefore - result.StatementsAfter;
            int atrs = result.TreeBefore - result.TreeAfter;
            int antrs = result.NonTreeBefore - result.NonTreeAfter;

            var text = new List<string>
            {
                "Run summary",
                "===========",
                "Test:               " + fingerprint.FullTestName,
                "Fingerprint:        " + fingerprint.NormalizedMessage,
                "Statements:         " + result.StatementsBefore + " -> " + result.StatementsAfter,
                "Sweeps:             " + result.Sweeps,
                "Oracle evaluations: " + evaluations + " (cache hits: " + cacheHits + ")",
                "Verdicts:           " + string.Join(", ", verdictCounts.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)),
                "Wall time:          " + elapsed.ToString(@"hh\:mm\:ss"),
                "",
                "Tool-computed measures — CROSS-CHECK ONLY, verify and record by hand",
                "(definitions: Christi & Weber 2024, Section V.B; percentages use total statements)",
                "  Total statements: " + totalBefore + "  (#NTN " + result.NonTreeBefore + ", #TN " + result.TreeBefore + ")",
                "  ARS:   " + ars,
                "  PRS:   " + Percent(ars, totalBefore),
                "  ATRS:  " + atrs,
                "  PTRS:  " + Percent(atrs, totalBefore),
                "  ANTRS: " + antrs,
                "  PNTRS: " + Percent(antrs, totalBefore),
                "  PrNTRS: " + Percent(antrs, result.NonTreeBefore),
                "  PrTRS:  " + (result.TreeBefore == 0 ? "undefined (#TN = 0)" : Percent(atrs, result.TreeBefore))
            };

            File.WriteAllLines(Path.Combine(_runDirectory, "summary.txt"), text);

            Info("");
            foreach (string line in text)
            {
                Info(line);
            }
        }

        private static string Percent(int part, int whole)
        {
            return whole == 0 ? "n/a" : (100.0 * part / whole).ToString("0.00") + "%";
        }

        private static string Snippet(StatementSyntax statement)
        {
            string text = string.Join(" ",
                statement.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 70 ? text : text.Substring(0, 67) + "...";
        }

        public void Dispose()
        {
            _log.Dispose();
            _csv.Dispose();
        }
    }
}
