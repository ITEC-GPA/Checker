using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;

namespace GPC.Model.Checker
{
    /// <summary>A session of this assembly whose calculation is thread-safe and touches no session state:
    /// Verify(input) is Evaluate(input) followed, even on an exception, by Record of what it prepared.</summary>
    internal interface IConcurrentMaterialSession : IMaterialCheckSession
    {
        CheckResult Evaluate(BeamActionInput input, CheckMechanism mechanism, CancellationToken token, ref string prepared);
        /// <summary>Sequential, in task order: the observable session state (CreatedCheckers).</summary>
        void Record(string prepared);
    }

    /// <summary>The engine part of a section task computed ahead of the sequential pass: the owned copies
    /// fingerprinted before and after the call, the call itself and the engine-identity check, exactly as
    /// the sequential pass does them. The sequential pass keeps everything else: cancellation, staleness,
    /// stamping, sealing, validation, progress, and the session record, so a cancelled task records nothing.
    /// Between the computation of the block and the turn of the task, the sequential pass runs code of others:
    /// progress callbacks and the engines of the other tasks. They can change the engine (its configuration) or
    /// the owned copies of the task (an engine receives the prepared input shared by the tasks of its cut); the
    /// sequential run would then compute with the changed state. The result is therefore used only while the
    /// calculation would read the same thing (<see cref="ReadsTheSame"/>); otherwise the task is computed in the
    /// sequential pass and this result is dropped without being recorded.</summary>
    internal sealed class PrecomputedSection
    {
        internal IConcurrentMaterialSession Session;
        internal CheckResult Result;
        internal string Prepared;
        internal bool Changed;
        internal ExceptionDispatchInfo Error;
        /// <summary>Engine identity and owned copies the calculation read (taken before the call); null when the
        /// fingerprints themselves failed.</summary>
        internal string Engine, Owned;

        /// <summary>True when the engine and the owned copies of the task are those the calculation read: a
        /// deterministic engine then gives, now, the same result, record and staleness.</summary>
        internal bool ReadsTheSame(string engine, string owned) => Engine != null && Owned != null && Engine == engine && Owned == owned;

        /// <summary>Result of the engine call as the sequential pass would have obtained it.</summary>
        internal CheckResult Take()
        {
            Session.Record(Prepared);
            Error?.Throw();
            return Result;
        }

        /// <summary>Tasks in blocks: progress and cancellation of the sequential pass stay close to the
        /// calculation, and a cancelled run computes at most one block more.</summary>
        internal const int Block = 256;

        internal static void Run(IList<(PrecomputedSection Slot, BeamActionInput Input, CheckMechanism Mechanism, Func<string> Owned, Func<string> Engine, string Key)> work,
            CancellationToken token)
        {
            Parallel.For(0, work.Count, i => {
                var w = work[i]; var slot = w.Slot;
                try
                {
                    string engine = w.Engine(), owned = w.Owned();
                    slot.Engine = engine; slot.Owned = owned;
                    string prepared = null;
                    try { slot.Result = slot.Session.Evaluate(w.Input, w.Mechanism, token, ref prepared) ?? throw new InvalidOperationException("NullCheckerOutcome"); }
                    finally { slot.Prepared = prepared; }
                    token.ThrowIfCancellationRequested();
                    slot.Changed = w.Engine() != w.Key || w.Owned() != owned;
                }
                catch (Exception ex) { slot.Error = ExceptionDispatchInfo.Capture(ex); }
            });
        }
    }
}
