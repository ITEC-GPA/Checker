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
        internal static CheckReport Run(Models.Model model, ModelCheckJob job, BeamCheckPlan plan,
            Func<ModelCheckJob, IConcreteSectionVerifier> sectionFactory, Func<ModelCheckJob, IPhysicalMemberVerifier> memberFactory,
            CancellationToken token, Action<CheckWorkItem> progress)
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
            foreach (var item in plan.WorkItems)
            {
                CheckResult result;
                if (token.IsCancellationRequested) result = new CheckResult { Execution = ExecutionStatus.Cancelled, Data = DataStatus.Insufficient };
                else if (!plan.IsCurrent) result = new CheckResult { Data = DataStatus.Stale, Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("StaleBeamPlan") } };
                else if (item.Scope == CheckScope.PhysicalMember) result = MemberCheckExecution.Run(plan, item, memberEngine, token);
                else result = item.Check == null ? SectionCheckExecution.Run(item.Section, item.Mechanism, sectionEngine, token)
                    : SectionCheckExecution.Run(item.Section, item.Check, sectionEngine, token);
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
                result.VerificationRevision = model.VerificationFingerprint(plan.Request.Settings);
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
            }
            if (!token.IsCancellationRequested && !plan.IsCurrent)
                foreach (var result in results.Where(r => r.Outcome != EngineeringOutcome.NotEvaluated))
                { result.Data = DataStatus.Stale; result.Outcome = EngineeringOutcome.NotEvaluated; result.Diagnostics.Add(ModelDiagnostic.Error("InputsChangedDuringBeamPlan")); result.SealEvidence(); }
            return CheckReport.ForPlan(job.Name, plan, results);
        }
    }
}
