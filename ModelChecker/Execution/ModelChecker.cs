using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Execution;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using GPC.Model.Results.Locations;

namespace GPC.Model.Checker
{

    /// <summary>Headless orchestration over the shared Model. Real section Checkers are created lazily and reused within this run.</summary>
    public sealed partial class ModelChecker
    {
        private readonly Func<ModelCheckJob, IConcreteSectionVerifier> _factory;
        private readonly Func<ModelCheckJob, IPhysicalMemberVerifier> _memberFactory;
        /// <summary>Concrete section verifier from the job options, also for the member detailing tasks (other member methods: not supported).</summary>
        public ModelChecker() : this(job => job.Options?.CreateVerifier(), job => job.Options?.CreateVerifier()) { }
        public ModelChecker(Func<ModelCheckJob, IConcreteSectionVerifier> factory) { _factory = factory ?? throw new ArgumentNullException(nameof(factory)); }
        public ModelChecker(Func<ModelCheckJob, IConcreteSectionVerifier> sectionFactory, Func<ModelCheckJob, IPhysicalMemberVerifier> memberFactory) : this(sectionFactory)
        { _memberFactory = memberFactory ?? throw new ArgumentNullException(nameof(memberFactory)); }

        private IConcreteSectionVerifier ResolveSectionEngine(ModelCheckJob job, Dictionary<string, IConcreteSectionVerifier> engines)
        {
            var verifier = _factory(job);
            if (verifier == null) return null;
            var key = verifier.GetType().AssemblyQualifiedName + "\n" + verifier.Version + "\n" + (verifier as IConfiguredSectionVerifier)?.Configuration;
            if (verifier is ConcreteSectionVerifier && engines.TryGetValue(key, out var existing)) return existing;
            engines[job.Name] = verifier;
            if (verifier is ConcreteSectionVerifier) engines[key] = verifier;
            return verifier;
        }

        public ModelCheckReport Verify(Models.Model model, ModelCheckRequest request, CancellationToken cancellationToken = default,
            IProgress<VerificationProgress> progress = null)
        {
            if (model == null || request == null) throw new ArgumentNullException();
            var jobs = request.Jobs.ToArray();
            if (jobs.Length == 0 || jobs.Any(j => j == null || string.IsNullOrWhiteSpace(j.Name) || (j.Preparation == null) == (j.BeamPlan == null))
                || jobs.Select(j => j.Name).Distinct(StringComparer.Ordinal).Count() != jobs.Length) throw new ArgumentException("Unique named check jobs are required.");
            // Validate all selections before invoking an engine. No partial reports are attached to Model by this service.
            var blocked = AnalysisGate(model, jobs.Select(j => j.Name), out var provenance);
            if (blocked != null) return blocked;
            var plans = jobs.Select(j => j.Preparation == null ? null : ModelPreparation.Prepare(model, j.Preparation, cancellationToken)).ToArray();
            var beamPlans = jobs.Select(j => j.BeamPlan == null ? null : BeamCheckPlan.Prepare(model, j.BeamPlan, cancellationToken)).ToArray();
            int total = plans.Sum(p => p == null ? 0 : p.Samples.Count * p.Request.Mechanisms.Length) + beamPlans.Sum(p => p?.WorkItems.Count ?? 0), completed = 0;
            var reports = new List<CheckReport>(); var engines = new Dictionary<string, IConcreteSectionVerifier>(StringComparer.Ordinal);
            for (int i = 0; i < jobs.Length; i++)
            {
                var job = jobs[i]; var plan = plans[i]; var results = new List<CheckResult>();
                if (beamPlans[i] != null)
                {
                    reports.Add(PlannedBeamChecking.Run(model, job, beamPlans[i], j => ResolveSectionEngine(j, engines), _memberFactory, cancellationToken, item => {
                        completed++; progress?.Report(new VerificationProgress { Completed = completed, Total = total, Job = job.Name,
                            Family = EntityFamily.Beam, ElementId = item.Target.BeamId ?? default(int), Target = item.Target }); }));
                    continue;
                }
                IConcreteSectionVerifier verifier = null; Exception factoryError = null;
                if (!cancellationToken.IsCancellationRequested && plan.Samples.Any(s => s.Beam?.Status == DataStatus.Ready))
                {
                    try
                    {
                        verifier = ResolveSectionEngine(job, engines);
                    }
                    catch (Exception ex) { factoryError = ex; }
                }
                foreach (var row in plan.Samples)
                {
                    foreach (var mechanism in plan.Request.Mechanisms)
                    {
                        CheckResult result;
                        if (row.Cancelled || cancellationToken.IsCancellationRequested)
                            result = new CheckResult { Execution = ExecutionStatus.Cancelled, Data = DataStatus.Insufficient };
                        else if (row.Beam != null)
                        {
                            result = SectionCheckExecution.Run(row.Beam, mechanism, verifier, cancellationToken);
                            if (factoryError != null && row.Beam.Status == DataStatus.Ready)
                            { result.Execution = ExecutionStatus.Error; result.Diagnostics.Add(ModelDiagnostic.Error("CheckerCreationFailed", message: factoryError.Message)); }
                        }
                        else
                        {
                            result = new CheckResult { Data = row.Shell.Status };
                            result.Diagnostics.AddRange(row.Shell.Diagnostics);
                            if (row.Shell.Input != null)
                            {
                                result.Data = row.Shell.Input.IsCurrent ? DataStatus.NotSupported : DataStatus.Stale;
                                result.Diagnostics.Add(ModelDiagnostic.Error(result.Data == DataStatus.Stale ? "StaleShellPreparation" : "ShellCheckerUnavailable", row.Shell.Input.Element,
                                    "The installed Checker has no executable plate verifier. A validated shell design method and concrete adapter are required."));
                                result.VerificationRevision = row.Shell.Input.VerificationRevision; result.SampleRevision = row.Shell.Input.SampleRevision;
                            }
                        }
                        result.Job = job.Name; result.Family = row.Element.Family; result.ElementId = row.Element.Id; result.Mechanism = mechanism;
                        result.Settings = plan.Request.Settings;
                        var element = row.Element.Family == EntityFamily.Beam ? (Elements.Element)model.BeamElements[row.Element.Id] : model.AreaElements[row.Element.Id];
                        result.Source = element.Source;
                        result.GroupNames = element.Groups.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
                        if (result.Standard == null && verifier is ConcreteSectionVerifier concrete) result.Standard = concrete.StandardContext;
                        result.Dataset = row.Sample?.State?.DatasetId ?? row.Selection.Dataset; result.Case = row.Sample?.Case?.Name ?? row.Selection.Case;
                        result.Phase = row.Sample?.State?.Phase ?? row.Selection.Phase; result.Step = row.Sample?.State?.Step ?? row.Selection.Step;
                        result.ConcomitantStateId = row.Sample?.State?.ConcomitantStateId ?? row.Selection.ConcomitantState;
                        result.Mode = row.Sample?.State?.Mode ?? row.Selection.Mode; result.MovingLoadPosition = row.Sample?.State?.MovingLoadPosition ?? row.Selection.MovingLoadPosition;
                        result.Coverage = row.Sample?.State?.Coverage;
                        if (row.Sample is global::GPC.Model.Results.Locations.StationResultBeamForces beam) { result.Station = beam.ParametricDistance; result.Side = beam.Side; }
                        if (row.Sample is global::GPC.Model.Results.Locations.PointResultPlateForces shell)
                        { result.ShellPoint = shell.Location; result.ShellPointKind = shell.PointKind; result.ShellCoordinateKind = shell.CoordinateKind; }
                        foreach (var diagnostic in result.Diagnostics) { diagnostic.Family = result.Family; diagnostic.ElementId = result.ElementId;
                            diagnostic.Dataset = result.Dataset; diagnostic.Case = result.Case; diagnostic.Station = result.Station; }
                        result.SealEvidence();
                        results.Add(result); completed++;
                        progress?.Report(new VerificationProgress { Completed = completed, Total = total, Job = job.Name, Family = row.Element.Family, ElementId = row.Element.Id });
                    }
                }
                reports.Add(CheckReport.ForPreparedResults(job.Name, plan, results));
            }
            return WithProvenance(new ModelCheckReport { Jobs = reports.AsReadOnly(), CreatedCheckers = engines.Values.Distinct().OfType<ConcreteSectionVerifier>().Sum(e => e.CreatedCheckers) }, model, provenance);
        }
    }
}
