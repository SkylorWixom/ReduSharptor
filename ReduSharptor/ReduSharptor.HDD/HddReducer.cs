using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReduSharptor.HDD
{
    /// <summary>
    /// Orchestrates the reduction: takes the statement hierarchy, runs ddmin over
    /// it, and judges every candidate through the oracle. Milestone 4 scope: the
    /// flat pass over level 0 only, which makes this tool equivalent to the
    /// baseline DD. The level walker and fixpoint sweeps arrive in milestone 5.
    /// </summary>
    public class HddReducer
    {
        private readonly TestFileRewriter _rewriter;
        private readonly Oracle _oracle;
        private readonly string _workingTestFile;

        private int _candidateIndex;
        private readonly Dictionary<Verdict, int> _verdictCounts = new();

        public IReadOnlyDictionary<Verdict, int> VerdictCounts => _verdictCounts;

        public HddReducer(TestFileRewriter rewriter, Oracle oracle, string workingTestFile)
        {
            _rewriter = rewriter;
            _oracle = oracle;
            _workingTestFile = workingTestFile;
        }

        /// <summary>
        /// Flat reduction: ddmin over the level-0 statements only. Tree statements
        /// are kept or removed whole, exactly like the baseline. Returns the final
        /// statement list and leaves the working copy holding exactly that list.
        /// </summary>
        public List<StatementSyntax> ReduceFlat(List<StatementNode> level0)
        {
            List<StatementSyntax> statements = level0.Select(n => n.Syntax).ToList();

            List<StatementSyntax> reduced = Ddmin.Reduce(statements, IsPreserved);

            // The last candidate ddmin tried may have been a rejected one, so the
            // working copy must be rewritten with the actual final result.
            _rewriter.Write(reduced, _workingTestFile);

            return reduced;
        }

        private bool IsPreserved(List<StatementSyntax> candidate)
        {
            _candidateIndex++;
            string label = "cand_" + _candidateIndex.ToString("D3");
            string key = Oracle.ComputeKey(candidate);

            Verdict verdict = _oracle.JudgeCandidate(key, () => _rewriter.Write(candidate, _workingTestFile), label);

            _verdictCounts[verdict] = _verdictCounts.GetValueOrDefault(verdict) + 1;
            Console.WriteLine("  [" + label + "] " + candidate.Count + " statements -> " + verdict);

            return verdict == Verdict.Preserved;
        }
    }
}
