using GPC.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Model.Checker.Configuration;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Results.Locations;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;

namespace GPC.Model.Checker
{
    public sealed partial class ModelChecker
    {
        private sealed class PlateRouting
        {
            internal PreparationRequest Request;
            internal Dictionary<string, IPlateChecker> Engines;
        }
        private static string PlateEngineKey(IPlateChecker checker) => checker == null ? null : ModelValues.Fingerprint(new object[] {
            checker.GetType().AssemblyQualifiedName, checker.Id, checker.Version, checker.Configuration, checker.Standard?.Identity });
        private static string PlateRouteKey(int element, string check) => element.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + check;
        private static PlateRouting ResolvePlateJob(Models.Model model, PlateCheckJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.Name) || job.Preparation?.Selection?.Families == null
                || job.Preparation.Selection.Families.Any(f => f != EntityFamily.Shell) || job.Preparation.Results == null || job.Preparation.Results.Any(r => r == null)
                || job.Checks == null || job.Checks.Length == 0
                || job.Checks.Any(c => c == null) || job.Checks.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != job.Checks.Length
                || job.Assignments == null || job.Assignments.Any(a => a == null || a.Checker == null && string.IsNullOrWhiteSpace(a.EngineId)
                    || a.Mechanisms != null && a.Mechanisms.Any(m => !Enum.IsDefined(typeof(CheckMechanism), m))
                    || a.CheckIds != null && a.CheckIds.Any(id => !job.Checks.Any(c => c.Id == id))
                    || a.Selection != null && (a.Selection.Families == null || a.Selection.Families.Any(f => f != EntityFamily.Shell))))
                throw new ArgumentException("ExplicitPlateJobRequired");
            foreach (var check in job.Checks)
            {
                check.Validate();
                if (check.Category != CombinationCategory.Unspecified && !job.Preparation.Results.Any(s => s.Category == check.Category))
                    throw new ArgumentException("NoResultSelectionForPlateCategory");
            }
            var selected = job.Preparation.Selection.Resolve(model).Cast<AreaElement>().ToArray();
            if (selected.Length == 0) throw new ArgumentException("EmptyPlateScope");
            var assignments = job.Assignments.Select(a => new { Value = a, Elements = a.Selection?.Resolve(model).Select(e => e.Id).ToArray() }).ToArray();
            var routes = new Dictionary<string, IPlateChecker>(StringComparer.Ordinal);
            foreach (var element in selected)
            foreach (var check in job.Checks)
            {
                var matches = assignments.Where(a => (a.Elements == null || a.Elements.Contains(element.Id))
                    && (a.Value.Mechanisms == null || a.Value.Mechanisms.Contains(check.Mechanism))
                    && (a.Value.CheckIds == null || a.Value.CheckIds.Contains(check.Id))
                    && (a.Value.Checker == null || a.Value.Checker.Accepts(element))).Select(a => a.Value)
                    .GroupBy(a => PlateEngineKey(a.Checker) ?? "unavailable:" + a.EngineId, StringComparer.Ordinal).Select(g => g.First()).ToArray();
                if (matches.Length > 1) throw new ArgumentException("ConflictingPlateAssignments: " + PlateRouteKey(element.Id, check.Id));
                routes.Add(PlateRouteKey(element.Id, check.Id), matches.SingleOrDefault()?.Checker);
            }
            var request = job.Preparation.Copy(); request.Mechanisms = job.Checks.Select(c => c.Mechanism).Distinct().ToArray();
            request.Settings = (request.Settings ?? "") + "\nPlateRouting:" + ModelValues.Fingerprint(new object[] { job.Name, job.AxesKind, job.Axes, job.Checks,
                job.Assignments.Select(a => new object[] { a.EngineId, a.Selection, a.Mechanisms, a.CheckIds, PlateEngineKey(a.Checker) }).ToArray(),
                routes.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => new object[] { r.Key, PlateEngineKey(r.Value) }).ToArray() });
            return new PlateRouting { Request = request, Engines = routes };
        }
        public static IReadOnlyList<PlateCheckPlan> PreparePlates(Models.Model model, PlateCheckRequest request, CancellationToken token = default(CancellationToken))
        {
            if (model == null || request == null || request.Jobs.Count == 0 || request.Jobs.Any(j => j == null)
                || request.Jobs.Select(j => j.Name).Distinct(StringComparer.Ordinal).Count() != request.Jobs.Count) throw new ArgumentException("UniquePlateJobsRequired");
            var plans = new List<PlateCheckPlan>();
            foreach (var job in request.Jobs)
            {
                var routing = ResolvePlateJob(model, job);
                var plan = new PlateCheckPlan { Job = job, Preparation = ModelPreparation.PrepareShellActions(model, routing.Request, job.AxesKind, job.Axes, token) };
                var tasks = new List<PlateCheckTask>();
                foreach (var row in plan.Preparation.Samples)
                foreach (var check in job.Checks.Where(c => c.Category == CombinationCategory.Unspecified || c.Category == row.Selection.Category))
                {
                    var engine = routing.Engines[PlateRouteKey(row.Element.Id, check.Id)];
                    var task = new PlateCheckTask { Row = row, Specification = check.Copy(), Engine = engine, EngineKey = PlateEngineKey(engine),
                        Id = ModelValues.Fingerprint(new object[] { "PlateTask-v1", tasks.Count, row.Element, row.Selection, check }) };
                    task.Evidence = CapturePlateEvidence(model, plan, task); tasks.Add(task);
                }
                plan.WorkItems = tasks.AsReadOnly();
                var revision = model.VerificationFingerprint(routing.Request.Settings);
                plan.Current = () => {
                    try { return plan.ScopeFingerprint == ModelPreparation.FingerprintShellScope(model, ResolvePlateJob(model, job).Request)
                            && revision == model.VerificationFingerprint(plan.Preparation.Request.Settings)
                            && plan.WorkItems.All(t => (t.Input == null || t.Input.IsCurrent) && t.EngineKey == PlateEngineKey(t.Engine)); }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is KeyNotFoundException) { return false; }
                };
                plans.Add(plan);
            }
            return plans.AsReadOnly();
        }
        private static CheckResult CapturePlateEvidence(Models.Model model, PlateCheckPlan plan, PlateCheckTask task)
        {
            var sample = task.Row.Sample as PointResultPlateForces; var element = model.AreaElements[task.ElementId];
            var result = new CheckResult();
            if (task.Row.Shell != null) result.Diagnostics.AddRange(task.Row.Shell.Diagnostics);
            result.SchemaVersion = 4;
            var specification = task.Specification;
            result.ShellCheck = new ShellCheckSnapshot(specification.Id, specification.MethodId, specification.Mechanism, specification.Category,
                task.Row.Selection.Category, specification.Direction, specification.Face?.PhysicalName,
                specification.Face == null ? (int?)null : specification.Face.NormalFace == ShellNormalFace.Positive ? 1 : -1, specification.ReinforcementRequired); result.Job = plan.Name; result.Family = EntityFamily.Shell; result.ElementId = task.ElementId;
            result.Target = new CheckTargetReference(EntityFamily.Shell, task.ElementId); result.Scope = CheckScope.ShellPoint; result.PlanItemId = task.Id;
            result.MethodId = task.Specification.MethodId; result.Mechanism = task.Specification.Mechanism; result.Face = task.Specification.Face?.PhysicalName;
            result.Settings = plan.Preparation.Request.Settings; result.Dataset = sample?.State?.DatasetId ?? task.Row.Selection.Dataset;
            result.Case = sample?.Case?.Name ?? task.Row.Selection.Case; result.Phase = sample?.State?.Phase ?? task.Row.Selection.Phase;
            result.Step = sample?.State?.Step ?? task.Row.Selection.Step; result.ConcomitantStateId = sample?.State?.ConcomitantStateId ?? task.Row.Selection.ConcomitantState;
            result.MovingLoadPosition = sample?.State?.MovingLoadPosition ?? task.Row.Selection.MovingLoadPosition; result.Mode = sample?.State?.Mode ?? task.Row.Selection.Mode;
            result.ShellPoint = sample?.Location; result.ShellPointKind = sample?.PointKind ?? ShellResultPointKind.Unknown;
            result.ShellCoordinateKind = sample?.CoordinateKind ?? ResultCoordinateKind.Unknown;
            result.CoverageAssessment = new CoverageAssessment(BeamCoveragePolicy.ExportedSamples, 1, sample == null ? 0 : 1);
            result.Coverage = "Only exported plate points and concomitant states; no continuous panel maximum is certified.";
            result.Source = element.Source; result.GroupNames = element.Groups.Keys.OrderBy(g => g, StringComparer.Ordinal).ToArray();
            result.EngineVersion = task.Engine?.Version; result.EngineConfiguration = task.Engine?.Configuration; result.Standard = task.Engine?.Standard;
            result.VerificationRevision = task.Input?.VerificationRevision; result.SampleRevision = task.Input?.SampleRevision;
            if (task.Input != null)
            {
                var physicalSurface = model.SurfaceForElement(element.Id);
                result.ShellInput = new ShellInputSnapshot(ModelValues.Fingerprint(new object[] { element.PlateProperty, element.Assignments }),
                    task.Input.SampleRevision, ModelValues.Fingerprint(new object[] { task.Input.LocalForces }), task.Input.LocalForces, task.Input.Thickness,
                    physicalSurface?.Id, physicalSurface?.ZoneAt(element.Id));
            }

            return ConfigurationArchive.CopyData(result);
        }
        private static ModelDiagnostic PlateFaceDiagnostic(PlateCheckTask task)
        {
            var check = task.Specification; var input = task.Input; var layers = input.Reinforcement;
            if (check.ReinforcementRequired && (layers.Count == 0 || layers.Any(l => l.Steel == null))) return ModelDiagnostic.Error("MissingPlateReinforcement", input.Element);
            if (check.Face == null || layers.Count == 0 && !check.ReinforcementRequired) return null;
            var face = layers.Where(l => l.PhysicalFace == check.Face.PhysicalName).ToArray();
            if (face.Length == 0) return ModelDiagnostic.Error("UnknownPhysicalPlateFace", input.Element);
            Axes.Validate(input.SectionAxes);
            double alignment = Axes.Dot(input.SectionAxes.V3, input.LocalForces.CoordinateSystem.V3);
            double sign = check.Face.NormalFace == ShellNormalFace.Positive ? 1 : -1;
            return Math.Abs(alignment) < 1 - 1e-8 || face.Any(l => l.AxisPositionThroughThickness * alignment * sign <= 0)
                ? ModelDiagnostic.Error("PlateFaceNormalMismatch", input.Element) : null;
        }
        public ModelCheckReport Verify(Models.Model model, PlateCheckRequest request, CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
        {
            if (model == null || request == null) throw new ArgumentNullException();
            var blocked = AnalysisGate(model, request.Jobs.Select(j => j.Name), out var provenance);
            if (blocked != null) return blocked;
            return VerifyPlatePlans(model, PreparePlates(model, request, token), provenance, token, progress);
        }
        private ModelCheckReport VerifyPlatePlans(Models.Model model, IReadOnlyList<PlateCheckPlan> plans, VerificationProvenance provenance,
            CancellationToken token, IProgress<VerificationProgress> progress)
        {
            var sessions = new Dictionary<string, IPlateCheckSession>(StringComparer.Ordinal);
            var errors = new Dictionary<string, Exception>(StringComparer.Ordinal);
            var reports = new List<CheckReport>(); int completed = 0, total = plans.Sum(p => p.WorkItems.Count);
            foreach (var plan in plans)
            {
                var results = new List<CheckResult>();
                foreach (var task in plan.WorkItems)
                {
                    var result = new CheckResult { Data = task.Data };
                    try
                    {
                        if (token.IsCancellationRequested) result.Execution = ExecutionStatus.Cancelled;
                        else if (!plan.IsCurrent) result.Data = DataStatus.Stale;
                        else if (task.Input != null)
                        {
                            var diagnostic = PlateFaceDiagnostic(task);
                            if (diagnostic != null) { result.Data = DataStatus.Insufficient; result.Diagnostics.Add(diagnostic); }
                            else if (task.Engine == null) { result.Data = DataStatus.MissingDependency; result.Diagnostics.Add(ModelDiagnostic.Error("PlateCheckerUnavailable")); }
                            else if (!task.Engine.Supports(task.Specification.Copy())) result = NativeResults.Unavailable("UnsupportedPlateMethod", task.Specification.MethodId);
                            else
                            {
                                if (!sessions.ContainsKey(task.EngineKey) && !errors.ContainsKey(task.EngineKey))
                                    try { sessions.Add(task.EngineKey, task.Engine.CreateSession() ?? throw new InvalidOperationException("NullPlateSession")); }
                                    catch (Exception ex) { errors.Add(task.EngineKey, ex); }
                                if (errors.TryGetValue(task.EngineKey, out var error)) throw new InvalidOperationException("PlateCheckerCreationFailed", error);
                                result = sessions[task.EngineKey].Verify(task.Input, task.Specification.Copy(), token) ?? throw new InvalidOperationException("NullPlateOutcome");
                                token.ThrowIfCancellationRequested();
                                if (!plan.IsCurrent) result = new CheckResult { Data = DataStatus.Stale };
                            }
                        }
                    }
                    catch (OperationCanceledException) { result = new CheckResult { Execution = ExecutionStatus.Cancelled, Data = DataStatus.Insufficient }; }
                    catch (Exception ex) { result = new CheckResult { Execution = ExecutionStatus.Error, Data = DataStatus.Insufficient,
                        Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("PlateCheckerError", message: ex.Message) } }; }
                    var decision = result;
                    result = ConfigurationArchive.CopyData(task.Evidence);
                    result.Execution = decision.Execution; result.Data = decision.Data; result.Outcome = decision.Outcome;
                    result.Utilization = decision.Utilization; result.Details = decision.Details;
                    result.Applicability = decision.Applicability; result.ApplicabilityReason = decision.ApplicabilityReason;
                    if (decision.Diagnostics != null) result.Diagnostics.AddRange(decision.Diagnostics);
                    result.SealEvidence();
                    if (new CheckSummary(1, new[] { result }).Invalid != 0)
                    { result.Execution = ExecutionStatus.Error; result.Data = DataStatus.Insufficient; result.Outcome = EngineeringOutcome.NotEvaluated; result.Utilization = null;
                        result.Applicability = CheckApplicability.Required; result.Diagnostics.Add(ModelDiagnostic.Error("InvalidPlateOutcome")); result.SealEvidence(); }
                    results.Add(result); completed++;
                    progress?.Report(new VerificationProgress { Completed = completed, Total = total, Job = plan.Name, Family = EntityFamily.Shell, ElementId = task.ElementId, Target = result.Target });
                }
                reports.Add(CheckReport.ForShellPlan(plan.Name, plan.Preparation, plan.WorkItems.Select(t => t.Id), results));
            }
            for (int i = 0; i < plans.Count; i++) if (!plans[i].IsCurrent)
                foreach (var result in reports[i].Results)
                { result.Data = DataStatus.Stale; result.Outcome = EngineeringOutcome.NotEvaluated; result.Diagnostics.Add(ModelDiagnostic.Error("PlateInputsChangedDuringRun")); result.SealEvidence(); }
            return WithProvenance(new ModelCheckReport { Jobs = reports.AsReadOnly(), CreatedCheckers = sessions.Values.Sum(s => s.CreatedCheckers) }, model, provenance);
        }
        public static EngineeringOutcome CurrentOutcome(ModelCheckReport report, Models.Model model, PlateCheckRequest request)
        {
            if (report == null || model == null || request == null || report.Jobs.Count != request.Jobs.Count) return EngineeringOutcome.NotEvaluated;
            if (AnalysisCompatibilityValidator.Validate(model).Status != AnalysisCompatibility.Compatible) return EngineeringOutcome.NotEvaluated;
            try
            {
                var plans = PreparePlates(model, request);
                for (int i = 0; i < plans.Count; i++)
                {
                    var saved = report.Jobs[i]; var plan = plans[i];
                    if (!plan.IsCurrent || saved.Job != plan.Name || !saved.HasUnchangedScope || saved.ScopeFingerprint != plan.ScopeFingerprint
                        || !saved.Results.Select(r => r.PlanItemId).SequenceEqual(plan.WorkItems.Select(t => t.Id))) return EngineeringOutcome.NotEvaluated;
                    for (int j = 0; j < plan.WorkItems.Count; j++)
                    {
                        var result = saved.Results[j]; var task = plan.WorkItems[j];
                        if (!result.HasUnchangedEvidence || result.Provenance == null || !result.Provenance.IsCurrent(model)
                            || result.EngineVersion != task.Engine?.Version || result.EngineConfiguration != task.Engine?.Configuration
                            || result.Standard?.Identity != task.Engine?.Standard?.Identity) return EngineeringOutcome.NotEvaluated;
                    }
                }
                return report.Outcome;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is KeyNotFoundException) { return EngineeringOutcome.NotEvaluated; }
        }
    }
}
