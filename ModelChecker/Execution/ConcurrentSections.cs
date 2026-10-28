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
    /// stamping, sealing, validation, progress, and the session record, so a cancelled task records nothing.</summary>
    internal sealed class PrecomputedSection
    {
        internal IConcurrentMaterialSession Session;
        internal CheckResult Result;
        internal string Prepared;
        internal bool Changed;
        internal ExceptionDispatchInfo Error;

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

        internal static void Run(IList<(PrecomputedSection Slot, BeamActionInput Input, CheckMechanism Mechanism, Func<string> Owned, Func<bool> EngineChanged)> work,
            CancellationToken token)
        {
            Parallel.For(0, work.Count, i => {
                var w = work[i]; var slot = w.Slot;
                try
                {
                    string owned = w.Owned();
                    string prepared = null;
                    try { slot.Result = slot.Session.Evaluate(w.Input, w.Mechanism, token, ref prepared) ?? throw new InvalidOperationException("NullCheckerOutcome"); }
                    finally { slot.Prepared = prepared; }
                    token.ThrowIfCancellationRequested();
                    slot.Changed = w.EngineChanged() || w.Owned() != owned;
                }
                catch (Exception ex) { slot.Error = ExceptionDispatchInfo.Capture(ex); }
            });
        }
    }
}
