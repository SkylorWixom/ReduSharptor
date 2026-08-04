using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReduSharptor.HDD
{
    /// <summary>
    /// The result of a reduction: how many statements existed and survived, and
    /// how many sweeps the fixpoint loop needed.
    /// </summary>
    public class ReductionResult
    {
        public int StatementsBefore { get; init; }
        public int StatementsAfter { get; init; }
        public int Sweeps { get; init; }
        public string FinalMethodText { get; init; } = "";
    }

    /// <summary>
    /// Hierarchical Delta Debugging (design decision 1): walk the statement
    /// hierarchy top-down, run ddmin over the removable statements at each level,
    /// prune, descend, and repeat whole sweeps until one completes without
    /// removing anything (the HDD* fixpoint). Statements are only removable when
    /// they sit directly under a block (reducibility paper: only statements below
    /// BlockStmt are reduction units); an embedded statement like the body of
    /// "if (x) return;" is never removed alone, though its parent still can be.
    /// </summary>
    public class HddReducer
    {
        private readonly TestFileRewriter _rewriter;
        private readonly Oracle _oracle;
        private readonly string _workingTestFile;

        private readonly HashSet<SyntaxNode> _removed = new();
        private readonly Dictionary<Verdict, int> _verdictCounts = new();
        private int _candidateIndex;

        public IReadOnlyDictionary<Verdict, int> VerdictCounts => _verdictCounts;

        public HddReducer(TestFileRewriter rewriter, Oracle oracle, string workingTestFile)
        {
            _rewriter = rewriter;
            _oracle = oracle;
            _workingTestFile = workingTestFile;
        }

        public ReductionResult Reduce(List<StatementNode> hierarchy)
        {
            int statementsBefore = CountAlive(hierarchy);
            int sweep = 0;
            bool changedInSweep = true;

            while (changedInSweep)
            {
                sweep++;
                changedInSweep = false;
                Console.WriteLine("Sweep " + sweep + ":");

                for (int level = 0; ; level++)
                {
                    List<StatementNode> nodes = AliveRemovableAtLevel(hierarchy, level);
                    if (nodes.Count == 0)
                    {
                        break;
                    }

                    Console.WriteLine("  Level " + level + ": " + nodes.Count + " removable statement(s)");

                    // First try pruning the whole level at once; when a level is
                    // entirely junk this saves every finer-grained attempt, and it
                    // is also the only way a single-statement level can shrink,
                    // because ddmin never proposes the empty configuration.
                    if (JudgeKeptSubset(nodes, new List<StatementNode>(), level))
                    {
                        RemoveNodes(nodes, kept: new List<StatementNode>());
                        changedInSweep = true;
                        continue;
                    }

                    if (nodes.Count == 1)
                    {
                        continue; // the single statement is needed; nothing else to try
                    }

                    List<StatementNode> keptNodes = Ddmin.Reduce(nodes, kept => JudgeKeptSubset(nodes, kept, level));
                    if (keptNodes.Count < nodes.Count)
                    {
                        RemoveNodes(nodes, keptNodes);
                        changedInSweep = true;
                    }
                }
            }

            // The last written candidate may have been a rejected one; leave the
            // working copy holding the actual final state.
            string finalText = _rewriter.Render(_removed);
            File.WriteAllText(_workingTestFile, finalText);

            return new ReductionResult
            {
                StatementsBefore = statementsBefore,
                StatementsAfter = CountAlive(hierarchy),
                Sweeps = sweep,
                FinalMethodText = _rewriter.RenderMethod(_removed)
            };
        }

        /// <summary>
        /// Judges "keep only this subset of the level's statements" with every
        /// other level held in its current state.
        /// </summary>
        private bool JudgeKeptSubset(List<StatementNode> levelNodes, List<StatementNode> kept, int level)
        {
            var candidateRemoved = new HashSet<SyntaxNode>(_removed);
            foreach (StatementNode node in levelNodes)
            {
                if (!kept.Contains(node))
                {
                    candidateRemoved.Add(node.Syntax);
                }
            }

            string text = _rewriter.Render(candidateRemoved);
            string key = Oracle.ComputeKey(text);

            _candidateIndex++;
            string label = "cand_" + _candidateIndex.ToString("D3");

            Verdict verdict = _oracle.JudgeCandidate(key, () => File.WriteAllText(_workingTestFile, text), label);

            _verdictCounts[verdict] = _verdictCounts.GetValueOrDefault(verdict) + 1;
            Console.WriteLine("    [" + label + "] L" + level + " keep " + kept.Count + "/" + levelNodes.Count
                              + " -> " + verdict);

            return verdict == Verdict.Preserved;
        }

        private void RemoveNodes(List<StatementNode> levelNodes, List<StatementNode> kept)
        {
            foreach (StatementNode node in levelNodes)
            {
                if (!kept.Contains(node))
                {
                    _removed.Add(node.Syntax);
                }
            }
        }

        /// <summary>
        /// The statements at a level that are still alive (neither they nor any
        /// ancestor removed) and individually removable (parent is a block).
        /// </summary>
        private List<StatementNode> AliveRemovableAtLevel(List<StatementNode> hierarchy, int level)
        {
            var result = new List<StatementNode>();
            Collect(hierarchy, level, result);
            return result;
        }

        private void Collect(List<StatementNode> nodes, int level, List<StatementNode> result)
        {
            foreach (StatementNode node in nodes)
            {
                if (_removed.Contains(node.Syntax))
                {
                    continue; // removed subtree; nothing below it exists anymore
                }
                if (node.Level == level && node.Syntax.Parent is BlockSyntax)
                {
                    result.Add(node);
                }
                if (node.Level < level)
                {
                    Collect(node.Children, level, result);
                }
            }
        }

        private int CountAlive(List<StatementNode> nodes)
        {
            int count = 0;
            foreach (StatementNode node in nodes)
            {
                if (_removed.Contains(node.Syntax))
                {
                    continue;
                }
                count += 1 + CountAlive(node.Children);
            }
            return count;
        }
    }
}
