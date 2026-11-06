using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;

namespace GPC.Checkers.Concrete.Response
{
    /// <summary>Immutable, named load/shrinkage history. Input phases and options are immutable; no checker is retained here.</summary>
    public sealed class ConcretePhaseHistory
    {
        internal ConcretePhase[] Input { get; }
        public string Name { get; }
        public IReadOnlyList<ConcretePhase> Phases { get; }
        public ConcretePhaseOptions Options { get; }
        public ConcretePhaseHistory(string name, IEnumerable<ConcretePhase> phases, ConcretePhaseOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A history name is required.", nameof(name));
            if (phases == null) throw new ArgumentNullException(nameof(phases), "History phases are required.");
            Input = phases.ToArray();
            if (Input.Any(p => p == null)) throw new ArgumentException("A history cannot contain a null phase.", nameof(phases));
            Name = name; Phases = Array.AsReadOnly(Input); Options = options ?? new ConcretePhaseOptions();
        }
    }

    /// <summary>One independent section/history pair. The factory runs on a batch worker and must return an exclusive checker/section.
    /// Do not mutate its materials or geometry during the batch. Factories may be called concurrently.</summary>
    public sealed class ConcretePhaseBatchRequest
    {
        public ConcretePhaseHistory History { get; }
        public Func<SectionChecker> CheckerFactory { get; }
        public ConcretePhaseBatchRequest(ConcretePhaseHistory history, Func<SectionChecker> checkerFactory)
        {
            History = history ?? throw new ArgumentNullException(nameof(history), "A history is required.");
            CheckerFactory = checkerFactory ?? throw new ArgumentNullException(nameof(checkerFactory), "A checker factory is required.");
        }
    }

    /// <summary>One result in input order. Result is non-null for both completed and nonconverged analyses.
    /// On an exception Result is null and ErrorType/ErrorMessage explain the failure; other independent histories continue.</summary>
    public sealed class ConcretePhaseBatchItemResult
    {
        public int Index { get; }
        public string Name { get; }
        public ConcretePhaseAnalysisResult Result { get; }
        public bool Completed => Result != null && Result.Completed;
        public string ErrorType { get; }
        public string ErrorMessage { get; }
        /// <summary>Item execution including any checker/context construction assigned to this item, excluding queue time.</summary>
        public double ElapsedMilliseconds { get; }
        internal ConcretePhaseBatchItemResult(int index, string name, ConcretePhaseAnalysisResult result, Exception error, double milliseconds)
        { Index = index; Name = name; Result = result; ErrorType = error?.GetType().FullName; ErrorMessage = error?.Message; ElapsedMilliseconds = milliseconds; }
    }

    public sealed class ConcretePhaseBatchResult
    {
        public IReadOnlyList<ConcretePhaseBatchItemResult> Items { get; }
        public bool Completed { get; }
        /// <summary>Configured upper bound, limited to the number of inputs; zero for an empty batch.</summary>
        public int MaxDegreeOfParallelism { get; }
        public int PreparedSections { get; }
        /// <summary>Wall time of the whole batch, including factories and context preparation; not the sum of overlapping item times.</summary>
        public double ElapsedMilliseconds { get; }
        internal ConcretePhaseBatchResult(ConcretePhaseBatchItemResult[] items, int degree, int prepared, double milliseconds)
        { Items = Array.AsReadOnly(items); Completed = items.All(i => i.Completed); MaxDegreeOfParallelism = degree; PreparedSections = prepared; ElapsedMilliseconds = milliseconds; }
    }

    public static partial class ConcretePhaseAnalysis
    {
        /// <summary>Different sections: one exclusive checker per request, created just before execution, with bounded concurrency.
        /// Zero maxDegreeOfParallelism selects min(4, Environment.ProcessorCount), a conservative default for short histories.
        /// Cancellation aborts the batch by exception.</summary>
        public static ConcretePhaseBatchResult CalculateBatch(IEnumerable<ConcretePhaseBatchRequest> requests,
            int maxDegreeOfParallelism = 0, CancellationToken cancellationToken = default)
            => CalculateRequests(SnapshotBatch(requests, maxDegreeOfParallelism, cancellationToken), maxDegreeOfParallelism, cancellationToken);

        public static Task<ConcretePhaseBatchResult> CalculateBatchAsync(IEnumerable<ConcretePhaseBatchRequest> requests,
            int maxDegreeOfParallelism = 0, CancellationToken cancellationToken = default)
        {
            var input = SnapshotBatch(requests, maxDegreeOfParallelism, cancellationToken);
            return Task.Run(() => CalculateRequests(input, maxDegreeOfParallelism, cancellationToken), cancellationToken);
        }

        /// <summary>Many histories of the SAME fixed section: each worker keeps one exclusive checker, recreated after an exception.
        /// Geometry/material integration data are reused sequentially on that worker; every history starts from its own initial equilibrium.
        /// The factory must produce equivalent configurations with distinct checker/section instances. Materials must remain unchanged.</summary>
        public static ConcretePhaseBatchResult CalculateBatch(IEnumerable<ConcretePhaseHistory> histories, Func<SectionChecker> checkerFactory,
            int maxDegreeOfParallelism = 0, CancellationToken cancellationToken = default)
        {
            if (checkerFactory == null) throw new ArgumentNullException(nameof(checkerFactory), "A checker factory is required.");
            return RunBatch(SnapshotBatch(histories, maxDegreeOfParallelism, cancellationToken), i => checkerFactory(), true, maxDegreeOfParallelism, cancellationToken);
        }

        public static Task<ConcretePhaseBatchResult> CalculateBatchAsync(IEnumerable<ConcretePhaseHistory> histories, Func<SectionChecker> checkerFactory,
            int maxDegreeOfParallelism = 0, CancellationToken cancellationToken = default)
        {
            if (checkerFactory == null) throw new ArgumentNullException(nameof(checkerFactory), "A checker factory is required.");
            var input = SnapshotBatch(histories, maxDegreeOfParallelism, cancellationToken);
            return Task.Run(() => RunBatch(input, i => checkerFactory(), true, maxDegreeOfParallelism, cancellationToken), cancellationToken);
        }

        private static T[] SnapshotBatch<T>(IEnumerable<T> items, int degree, CancellationToken token) where T : class
        {
            token.ThrowIfCancellationRequested();
            if (items == null) throw new ArgumentNullException(nameof(items), "Batch inputs are required.");
            if (degree < 0) throw new ArgumentOutOfRangeException(nameof(degree), "Use zero for automatic parallelism, or a positive worker limit.");
            var input = items.ToArray();
            if (input.Any(i => i == null)) throw new ArgumentException("A batch cannot contain null inputs.", nameof(items));
            return input;
        }

        private static ConcretePhaseBatchResult CalculateRequests(ConcretePhaseBatchRequest[] requests, int degree, CancellationToken token)
            => RunBatch(requests.Select(r => r.History).ToArray(), i => requests[i].CheckerFactory(), false, degree, token);

        private static ConcretePhaseBatchResult RunBatch(ConcretePhaseHistory[] histories, Func<int, SectionChecker> create,
            bool reuse, int requestedDegree, CancellationToken token)
        {
            var watch = Stopwatch.StartNew();
            token.ThrowIfCancellationRequested();
            int workers = Math.Min(histories.Length, requestedDegree == 0 ? Math.Min(4, Environment.ProcessorCount) : requestedDegree);
            var results = new ConcretePhaseBatchItemResult[histories.Length];
            if (workers == 0) return new ConcretePhaseBatchResult(results, 0, 0, watch.Elapsed.TotalMilliseconds);
            int next = -1, prepared = 0;
            var owners = new ConcurrentDictionary<object, byte>(IdentityComparer.Instance);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = token };
            // A bounded set of workers draws dynamically from the input queue: no Task allocation per history,
            // no parallelism inside the phase loop, and no affinity to slow contiguous input partitions.
            Parallel.For(0, workers, parallelOptions, worker =>
            {
                SectionSolver.PhaseSection section = null;
                object ownedSolver = null, ownedSection = null;
                void Release()
                {
                    if (ownedSection != null) owners.TryRemove(ownedSection, out _);
                    if (ownedSolver != null) owners.TryRemove(ownedSolver, out _);
                    ownedSolver = ownedSection = null; section = null;
                }
                try
                {
                    int index;
                    while ((index = Interlocked.Increment(ref next)) < histories.Length)
                    {
                        token.ThrowIfCancellationRequested();
                        var history = histories[index]; var itemWatch = Stopwatch.StartNew();
                        try
                        {
                            if (section == null)
                            {
                                var checker = create(index);
                                token.ThrowIfCancellationRequested();
                                if (checker?.SectionSolver == null) throw new InvalidOperationException("The batch checker factory returned a null checker/solver.");
                                if (!owners.TryAdd(checker.SectionSolver, 0)) throw new InvalidOperationException("The checker factory shared a solver between concurrent batch workers.");
                                ownedSolver = checker.SectionSolver;
                                if (!owners.TryAdd(checker.SectionSolver.ConcreteSection, 0)) throw new InvalidOperationException("The checker factory shared a section between concurrent batch workers.");
                                ownedSection = checker.SectionSolver.ConcreteSection;
                                section = new SectionSolver.PhaseSection(checker.SectionSolver);
                                Interlocked.Increment(ref prepared);
                            }
                            section.ResetEvaluations();
                            var result = new Runner(section, history.Options, token).Run(history.Input);
                            results[index] = new ConcretePhaseBatchItemResult(index, history.Name, result, null, itemWatch.Elapsed.TotalMilliseconds);
                        }
                        catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException)
                            && !(ex is OperationCanceledException && token.IsCancellationRequested))
                        {
                            Release();
                            results[index] = new ConcretePhaseBatchItemResult(index, history.Name, null, ex, itemWatch.Elapsed.TotalMilliseconds);
                        }
                        finally { if (!reuse) Release(); }
                    }
                }
                finally { Release(); }
            });
            token.ThrowIfCancellationRequested();
            return new ConcretePhaseBatchResult(results, workers, prepared, watch.Elapsed.TotalMilliseconds);
        }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
