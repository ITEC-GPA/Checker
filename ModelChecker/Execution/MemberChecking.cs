using System;
using System.Threading;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    /// <summary>MemberCheckExecution.Run without its whole-plan revalidations, for the orchestrators of
    /// this assembly only. They iterate the plan's own work items, revalidate the whole plan once at the
    /// start of each plan and again at the end of the run, and compare the owned copies of every task
    /// around the engine call (OwnedCheckInputs). MemberCheckExecution.Run fingerprints EVERY task of the
    /// plan twice per member task: 200 members in a plan of 1200 tasks cost 0.6 s per member row.
    /// Every other step, status and diagnostic is the one of MemberCheckExecution.Run.</summary>
    internal static class MemberChecking
    {
        internal static CheckResult Run(CheckWorkItem item, IPhysicalMemberVerifier verifier, CancellationToken token)
        {
            if (item?.Scope != CheckScope.PhysicalMember) throw new ArgumentException("ForeignMemberWorkItem");
            var result = new CheckResult { Data = item.Data };
            result.Diagnostics.AddRange(item.Diagnostics);
            if (token.IsCancellationRequested) { result.Execution = ExecutionStatus.Cancelled; return result; }
            try
            {
                if (verifier == null || !verifier.Supports(item.MethodId, item.Mechanism))
                { result.Data = DataStatus.NotSupported; result.Diagnostics.Add(ModelDiagnostic.Error("MemberCheckerUnavailable", message: item.MethodId)); return result; }
                result.EngineVersion = verifier.Version; result.EngineConfiguration = verifier.Configuration; result.Standard = verifier.Standard;
                if (item.Data != DataStatus.Ready) return result;
                var evaluated = verifier.Verify(item.Member, token);
                token.ThrowIfCancellationRequested();
                if (result.EngineVersion != verifier.Version || result.EngineConfiguration != verifier.Configuration
                    || result.Standard?.Identity != verifier.Standard?.Identity)
                { result.Data = DataStatus.Stale; result.Diagnostics.Add(ModelDiagnostic.Error("MemberInputsChangedDuringVerification")); return result; }
                if (!ValidDecision(evaluated)) throw new InvalidOperationException("Invalid member verifier outcome.");
                evaluated.EngineVersion = result.EngineVersion; evaluated.EngineConfiguration = result.EngineConfiguration; evaluated.Standard = result.Standard;
                return evaluated;
            }
            catch (OperationCanceledException) { result.Execution = ExecutionStatus.Cancelled; }
            catch (Exception ex) { result.Execution = ExecutionStatus.Error; result.Diagnostics.Add(ModelDiagnostic.Error("MemberVerifierError", message: ex.Message)); }
            return result;
        }

        // Model's decision rules (CheckResultRules.ValidDecision) through its public summary. An unsealed
        // row always has unchanged evidence, so the summary counts it invalid exactly when the rules reject it.
        // A row sealed and then changed by its engine is left to the orchestrator's check after stamping.
        private static bool ValidDecision(CheckResult evaluated)
            => evaluated != null && (new CheckSummary(1, new[] { evaluated }).Invalid == 0 || !evaluated.HasUnchangedEvidence);
    }
}
