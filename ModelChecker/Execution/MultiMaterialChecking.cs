using GPC.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Structure.Members;

namespace GPC.Model.Checker
{
    public sealed partial class ModelChecker
    {
        private sealed class MaterialPlan
        {
            internal MultiMaterialCheckJob Job;
            internal BeamCheckPlan Plan;
            internal BeamCheckPlanRequest Request;
            internal Dictionary<string, IMaterialChecker> Routes;
            internal Dictionary<string, string> Keys;
            internal string Settings;
        }
        private static string EngineKey(IMaterialChecker c) => c == null ? null : ModelValues.Fingerprint(new object[] {
            c.GetType().AssemblyQualifiedName, c.Id, c.Version, c.Configuration, c.Standard?.Identity });
        private static string RouteId(int beam, CheckMechanism mechanism, SectionCheckSpecification check)
            => beam.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (check?.Key ?? mechanism.ToString());
        private static MaterialPlan PrepareMaterialJob(Models.Model model, MultiMaterialCheckJob job, CancellationToken token)
        {
            var result = ResolveMaterialJob(model, job);
            result.Plan = BeamCheckPlan.PrepareActions(model, result.Request, token);
            return result;
        }
        private static MaterialPlan ResolveMaterialJob(Models.Model model, MultiMaterialCheckJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.Name) || job.Plan == null || job.Assignments == null
                || job.Assignments.Any(a => a?.Checker == null || a.Mechanisms != null && a.Mechanisms.Any(m => !Enum.IsDefined(typeof(CheckMechanism), m))
                    || a.Checks != null && a.Checks.Any(c => c == null))) throw new ArgumentException("InvalidMaterialCheckJob");
            if (job.Assignments.Any(a => a.Selection != null && (a.Selection.Families == null || a.Selection.Families.Any(f => f != EntityFamily.Beam))))
                throw new NotSupportedException("MaterialAssignmentRequiresBeamSelection");
            var planRequest = job.Plan.Copy();
            var ids = BeamCheckPlan.ResolveBeamIds(model, planRequest);
            var routes = new Dictionary<string, IMaterialChecker>(StringComparer.Ordinal);
            var resolved = job.Assignments.Select(a => new { Assignment = a, a.Checker, Ids = a.Selection?.Resolve(model).OfType<BeamElement>().Select(b => b.Id).ToArray() }).ToArray();
            var checks = planRequest.SectionMechanisms.Select(m => new { Mechanism = m, Check = (SectionCheckSpecification)null })
                .Concat((planRequest.SectionChecks ?? new SectionCheckSpecification[0]).Select(c => new { c.Mechanism, Check = c })).ToArray();
            foreach (int id in ids)
            foreach (var check in checks)
            {
                var matches = resolved.Where(a => (a.Ids == null || a.Ids.Contains(id)) && a.Assignment.Matches(check.Mechanism, check.Check)
                    && a.Checker.Accepts(model.BeamElements[id]))
                    .Select(a => a.Checker).GroupBy(EngineKey, StringComparer.Ordinal).Select(g => g.First()).ToArray();
                if (matches.Length > 1) throw new ArgumentException("ConflictingMaterialAssignments: " + RouteId(id, check.Mechanism, check.Check) + "; select one engine for each check.");
                routes[RouteId(id, check.Mechanism, check.Check)] = matches.SingleOrDefault();
            }
            var keys = routes.ToDictionary(p => p.Key, p => EngineKey(p.Value));
            // Persist a digest of selectors AND resolved configurations through the existing scope/settings contract.
            planRequest.Settings = (planRequest.Settings ?? "") + "\nMaterialRouting:" + ModelValues.Fingerprint(new object[] {
                job.Assignments.Select(a => new object[] { a.Selection, EngineKey(a.Checker), a.Mechanisms, a.Checks }).ToArray(),
                keys.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new object[] { p.Key, p.Value }).ToArray() });
            return new MaterialPlan { Job = job, Routes = routes, Keys = keys, Settings = job.Plan.Settings, Request = planRequest };
        }
        private static bool Current(Models.Model model, MaterialPlan plan)
        {
            try { return plan.Plan.IsCurrentFor(ResolveMaterialJob(model, plan.Job).Request); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is KeyNotFoundException) { return false; }
        }
        /// <summary>One sequential run, several materials/standards. Ambiguous assignments fail before any engine is invoked.</summary>
        public ModelCheckReport Verify(Models.Model model, MultiMaterialCheckRequest request, CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
        {
            if (model == null || request == null) throw new ArgumentNullException();
            if (request.Jobs.Count == 0 || request.Jobs.Any(j => j == null) || request.Jobs.Select(j => j.Name).Distinct(StringComparer.Ordinal).Count() != request.Jobs.Count)
                throw new ArgumentException("UniqueNamedMaterialJobsRequired");
            var blocked = AnalysisGate(model, request.Jobs.Select(j => j.Name), out var provenance);
            if (blocked != null) return blocked;
            var plans = request.Jobs.Select(j => PrepareMaterialJob(model, j, token)).ToArray();
            return VerifyMaterialPlans(model, plans, provenance, token, progress);
        }
        private ModelCheckReport VerifyMaterialPlans(Models.Model model, MaterialPlan[] plans, VerificationProvenance provenance,
            CancellationToken token, IProgress<VerificationProgress> progress)
        {
            int total = plans.Sum(p => p.Plan.WorkItems.Count), completed = 0;
            var sessions = new Dictionary<string, IMaterialCheckSession>(StringComparer.Ordinal);
            var sessionErrors = new Dictionary<string, Exception>(StringComparer.Ordinal);
            var reports = new List<CheckReport>();
            foreach (var plan in plans)
            {
                var results = new List<CheckResult>();
                foreach (var item in plan.Plan.WorkItems)
                {
                    var route = item.Target.BeamId.HasValue ? RouteId(item.Target.BeamId.Value, item.Mechanism, item.Check) : null;
                    var engine = route != null && plan.Routes.TryGetValue(route, out var routed) ? routed : null;
                    var key = route != null && plan.Keys.TryGetValue(route, out var routedKey) ? routedKey : null;
                    var result = new CheckResult { Data = item.Data };
                    try
                    {
                        if (token.IsCancellationRequested) result.Execution = ExecutionStatus.Cancelled;
                        else if (!Current(model, plan)) result.Data = DataStatus.Stale;
                        else if (item.Scope == CheckScope.PhysicalMember) result = NativeResults.Unavailable("MaterialGlobalCheckerUnavailable");
                        else if (engine == null) result = NativeResults.Unavailable("NoMaterialCheckerAssignment");
                        else if (item.Data == DataStatus.Ready && item.Actions?.Input != null)
                        {
                            if (!sessions.ContainsKey(key) && !sessionErrors.ContainsKey(key))
                                try { sessions[key] = engine.CreateSession() ?? throw new InvalidOperationException("NullCheckerSession"); }
                                catch (Exception ex) { sessionErrors[key] = ex; }
                            if (sessionErrors.TryGetValue(key, out var error)) throw new InvalidOperationException("CheckerCreationFailed", error);
                            var session = sessions[key];
                            if (item.Check == null) result = session.Verify(item.Actions.Input, item.Mechanism, token) ?? throw new InvalidOperationException("NullCheckerOutcome");
                            else if (session is ISectionCheckSession specific && specific.Supports(item.Check))
                                result = specific.Verify(item.Actions.Input, item.Check.Copy(), token) ?? throw new InvalidOperationException("NullCheckerOutcome");
                            else result = NativeResults.Unavailable("UnsupportedSectionCheck", item.Check.Key);
                            token.ThrowIfCancellationRequested();
                            if (EngineKey(engine) != key || !Current(model, plan)) result = new CheckResult { Data = DataStatus.Stale };
                        }
                    }
                    catch (OperationCanceledException) { result = new CheckResult { Execution = ExecutionStatus.Cancelled, Data = DataStatus.Insufficient }; }
                    catch (NotSupportedException ex) { result = NativeResults.Unavailable("UnsupportedNativeMethod", ex.Message); }
                    catch (Exception ex) { result = new CheckResult { Execution = ExecutionStatus.Error, Data = DataStatus.Insufficient,
                        Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("MaterialCheckerError", message: ex.Message) } }; }
                    if (result.Diagnostics == null) result.Diagnostics = new List<ModelDiagnostic>();
                    result.Diagnostics.AddRange(item.Diagnostics);
                    if (item.Actions != null) result.Diagnostics.AddRange(item.Actions.Diagnostics);
                    result.SchemaVersion = 2; result.Target = item.Target; result.Scope = item.Scope; result.PlanItemId = item.Id;
                    result.MethodId = result.Details?.MethodId ?? item.MethodId ?? engine?.Id ?? "unresolved";
                    result.Job = plan.Job.Name; result.Mechanism = item.Mechanism; result.Check = item.Check?.Copy(); result.Settings = plan.Settings;
                    result.ElementId = item.Target.BeamId ?? 0; result.Family = EntityFamily.Beam;
                    result.MemberLocation = item.MemberLocation; result.MemberInput = item.Member?.Snapshot; result.CoverageAssessment = item.Coverage;
                    result.Coverage = item.Coverage.Limitation; result.Station = item.Station; result.Side = item.Side;
                    result.Dataset = item.Selection.Dataset; result.Case = item.Selection.Case; result.Phase = item.Selection.Phase; result.Step = item.Selection.Step;
                    result.ConcomitantStateId = item.Selection.ConcomitantState; result.Mode = item.Selection.Mode; result.MovingLoadPosition = item.Selection.MovingLoadPosition;
                    result.Input = result.Input ?? item.Actions?.Input?.Snapshot; result.SampleRevision = item.Actions?.Input?.Snapshot.SampleFingerprint;
                    result.VerificationRevision = model.VerificationFingerprint(result.Settings);
                    result.EngineVersion = engine?.Version; result.EngineConfiguration = engine?.Configuration; result.Standard = engine?.Standard;
                    if (item.Target.BeamId.HasValue && model.BeamElements.TryGetValue(item.Target.BeamId.Value, out var beam))
                    { result.Source = beam.Source; result.GroupNames = beam.Groups.Keys.OrderBy(g => g, StringComparer.Ordinal).ToArray(); }
                    result.SealEvidence();
                    // Reuse the shared decision validator; even a third-party adapter cannot certify an invalid outcome.
                    if (new CheckSummary(1, new[] { result }).Invalid != 0)
                    {
                        result.Execution = ExecutionStatus.Error; result.Data = DataStatus.Insufficient; result.Outcome = EngineeringOutcome.NotEvaluated;
                        result.Utilization = null; result.Applicability = CheckApplicability.Required; result.Diagnostics.Add(ModelDiagnostic.Error("InvalidMaterialCheckerOutcome")); result.SealEvidence();
                    }
                    results.Add(result); completed++;
                    progress?.Report(new VerificationProgress { Completed = completed, Total = total, Job = plan.Job.Name, Family = EntityFamily.Beam, ElementId = result.ElementId, Target = item.Target });
                }
                reports.Add(CheckReport.ForPlan(plan.Job.Name, plan.Plan, results));
            }
            for (int i = 0; i < plans.Length; i++) if (!Current(model, plans[i]))
                foreach (var result in reports[i].Results.Where(r => r.Outcome != EngineeringOutcome.NotEvaluated))
                { result.Data = DataStatus.Stale; result.Outcome = EngineeringOutcome.NotEvaluated; result.Diagnostics.Add(ModelDiagnostic.Error("MaterialInputsChangedDuringRun")); result.SealEvidence(); }
            return WithProvenance(new ModelCheckReport { Jobs = reports.AsReadOnly(), CreatedCheckers = sessions.Values.Sum(s => s.CreatedCheckers) }, model, provenance);
        }
        /// <summary>Validate persisted scopes against current model, routing, methods and external module parameters without recalculating.</summary>
        public static EngineeringOutcome CurrentOutcome(ModelCheckReport report, Models.Model model, MultiMaterialCheckRequest request)
        {
            if (report == null || model == null || request == null || report.Jobs.Count != request.Jobs.Count) return EngineeringOutcome.NotEvaluated;
            if (AnalysisCompatibilityValidator.Validate(model).Status != AnalysisCompatibility.Compatible) return EngineeringOutcome.NotEvaluated;
            try
            {
                for (int i = 0; i < request.Jobs.Count; i++)
                {
                    var current = PrepareMaterialJob(model, request.Jobs[i], new CancellationToken(true)); var saved = report.Jobs[i];
                    if (saved.Job != current.Job.Name || saved.ScopeFingerprint != current.Plan.ScopeFingerprint || !saved.HasUnchangedScope
                        || saved.Results.Count != current.Plan.WorkItems.Count || saved.Results.Any(r => r == null || !r.HasUnchangedEvidence || r.Provenance != null && !r.Provenance.IsCurrent(model))
                        || !saved.Results.Select(r => r.PlanItemId).SequenceEqual(current.Plan.WorkItems.Select(w => w.Id))) return EngineeringOutcome.NotEvaluated;
                }
                return report.Outcome;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is KeyNotFoundException) { return EngineeringOutcome.NotEvaluated; }
        }
        public static EngineeringOutcome CurrentOutcome(IEnumerable<CheckReport> reports, Models.Model model, MultiMaterialCheckRequest request)
            => reports == null ? EngineeringOutcome.NotEvaluated : CurrentOutcome(new ModelCheckReport { Jobs = Array.AsReadOnly(reports.ToArray()) }, model, request);
    }
}
