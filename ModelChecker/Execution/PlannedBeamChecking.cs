using GPC.Model.Models;
using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Execution;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Structure.Members;

namespace GPC.Model.Checker
{
    internal static class PlannedBeamChecking
    {
        /// <param name="progressOnThisThread">Whether <paramref name="progress"/> may run code of the caller that may change the model
        /// (ModelChecker.ReportsOnThisThread): the next task then reads the model again.</param>
        /// <param name="sharedReads">False: every section task reads the model again (ModelChecker.SharedSectionReads).</param>
        internal static CheckReport Run(Models.Model model, ModelCheckJob job, BeamCheckPlan plan,
            Func<ModelCheckJob, IConcreteSectionVerifier> sectionFactory, Func<ModelCheckJob, IPhysicalMemberVerifier> memberFactory,
            CancellationToken token, Action<CheckWorkItem> progress, bool progressOnThisThread, bool sharedReads)
        {
            IConcreteSectionVerifier sectionEngine = null; IPhysicalMemberVerifier memberEngine = null;
            Exception sectionError = null, memberError = null;
            if (!token.IsCancellationRequested)
            {
                if (plan.WorkItems.Any(w => w.Section?.Input != null))
                    try { sectionEngine = sectionFactory(job); } catch (Exception ex) { sectionError = ex; }
                if (plan.WorkItems.Any(w => w.Scope == CheckScope.PhysicalMember) && memberFactory != null)
                    try { memberEngine = memberFactory(job); } catch (Exception ex) { memberError = ex; }
            }
            var results = new List<CheckResult>();
            // Whole-plan revalidation once (see OwnedCheckInputs), not before every task.
            bool planCurrent = !token.IsCancellationRequested && plan.IsCurrent;
            string revision = model.VerificationFingerprint(plan.Request.Settings);
            // The section tasks read the model through one run, created after the factories (see ModelChecker.Verify): read again after
            // every call of an engine that may read more than its input and every progress report run on this thread. The concrete adapter
            // with the built-in engine reads only the detached copies of its inputs, also as the member engine.
            var run = new SectionCheckRun(model);
            bool ownSectionEngine = (sectionEngine as ConcreteSectionVerifier)?.ReadsOnlyItsInput == true;
            bool ownMemberEngine = memberEngine == null || (memberEngine as ConcreteSectionVerifier)?.ReadsOnlyItsInput == true;
            foreach (var item in plan.WorkItems)
            {
                CheckResult result;
                if (token.IsCancellationRequested) result = new CheckResult { Execution = ExecutionStatus.Cancelled, Data = DataStatus.Insufficient };
                else if (!planCurrent) result = new CheckResult { Data = DataStatus.Stale, Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("StaleBeamPlan") } };
                else
                {
                    string owned = OwnedCheckInputs.Of(item);
                    if (item.Scope == CheckScope.PhysicalMember)
                    {
                        result = MemberChecking.Run(item, memberEngine, token);
                        if (!ownMemberEngine) run.ModelMayHaveChanged();
                    }
                    else if (!sharedReads) result = item.Check == null ? SectionCheckExecution.Run(item.Section, item.Mechanism, sectionEngine, token)
                        : SectionCheckExecution.Run(item.Section, item.Check, sectionEngine, token);
                    else result = item.Check == null ? run.Run(item.Section, item.Mechanism, sectionEngine, ownSectionEngine, token)
                        : run.Run(item.Section, item.Check, sectionEngine, ownSectionEngine, token);
                    if (!token.IsCancellationRequested && OwnedCheckInputs.Of(item) != owned)
                        result = new CheckResult { Data = DataStatus.Stale, Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("StaleBeamPlan") } };
                }
                var error = item.Scope == CheckScope.PhysicalMember ? memberError : sectionError;
                if (error != null && result.Execution != ExecutionStatus.Cancelled)
                { result.Execution = ExecutionStatus.Error; result.Outcome = EngineeringOutcome.NotEvaluated; result.Diagnostics.Add(ModelDiagnostic.Error("CheckerCreationFailed", message: error.Message)); }
                result.Diagnostics.AddRange(item.Diagnostics.Where(d => !result.Diagnostics.Any(existing => existing.Code == d.Code && existing.ElementId == d.ElementId)));
                result.SchemaVersion = 2; result.Scope = item.Scope; result.Target = item.Target; result.PlanItemId = item.Id; result.MethodId = item.MethodId;
                result.MemberLocation = item.MemberLocation; result.MemberInput = item.Member?.Snapshot; result.CoverageAssessment = item.Coverage;
                result.Job = job.Name; result.Settings = plan.Request.Settings; result.Mechanism = item.Mechanism; result.Check = item.Check?.Copy();
                result.Dataset = item.Selection.Dataset; result.Case = item.Selection.Case; result.Phase = item.Selection.Phase; result.Step = item.Selection.Step;
                result.ConcomitantStateId = item.Selection.ConcomitantState; result.Mode = item.Selection.Mode; result.MovingLoadPosition = item.Selection.MovingLoadPosition;
                result.Station = item.Station; result.Side = item.Side; result.Coverage = item.Coverage.Limitation;
                result.VerificationRevision = revision;
                if (item.Scope == CheckScope.SectionSample)
                {
                    var beam = model.BeamElements[item.Target.BeamId.Value]; result.ElementId = beam.Id; result.Family = EntityFamily.Beam;
                    result.Source = beam.Source; result.GroupNames = beam.Groups.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
                    if (result.Standard == null && sectionEngine is ConcreteSectionVerifier concrete) result.Standard = concrete.StandardContext;
                }
                else
                {
                    // Target is authoritative: this is not the first constituent beam's verification.
                    result.ElementId = default(int); result.Station = null;
                    result.GroupNames = item.Member.Snapshot.Definition.Parts.SelectMany(p => model.BeamElements[p.BeamId].Groups.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                }
                result.SealEvidence(); results.Add(result); progress?.Invoke(item);
                if (progressOnThisThread) run.ModelMayHaveChanged();
            }
            if (!token.IsCancellationRequested && !plan.IsCurrent)
                foreach (var result in results.Where(r => r.Outcome != EngineeringOutcome.NotEvaluated))
                { result.Data = DataStatus.Stale; result.Outcome = EngineeringOutcome.NotEvaluated; result.Diagnostics.Add(ModelDiagnostic.Error("InputsChangedDuringBeamPlan")); result.SealEvidence(); }
            return CheckReport.ForPlan(job.Name, plan, results);
        }
    }
}
