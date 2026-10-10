using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Scenarios;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    public sealed partial class ModelChecker
    {
        /// <summary>Compiles all beam/plate routes and prepares all inputs before creating the first native session.</summary>
        public ModelCheckReport Verify(Models.Model model, VerificationConfiguration configuration, EngineCatalog catalog,
            CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
        {
            if (model == null || catalog == null) throw new ArgumentNullException();
            var frozen = ConfigurationArchive.Copy(configuration);
            var beams = catalog.Compile(frozen); var plates = catalog.CompilePlates(frozen);
            var blocked = AnalysisGate(model, beams.Jobs.Select(j => j.Name).Concat(plates.Jobs.Select(j => j.Name)), out var provenance);
            if (blocked != null) return blocked;
            var beamPlans = beams.Jobs.Select(j => PrepareMaterialJob(model, j, token)).ToArray();
            var platePlans = plates.Jobs.Count == 0 ? new PlateCheckPlan[0] : PreparePlates(model, plates, token);
            int total = beamPlans.Sum(p => p.Plan.WorkItems.Count) + platePlans.Sum(p => p.WorkItems.Count);
            var beamReport = VerifyMaterialPlans(model, beamPlans, provenance, token, new CombinedProgress(progress, 0, total));
            var plateReport = VerifyPlatePlans(model, platePlans, provenance, token, new CombinedProgress(progress, beamReport.Required, total));
            var jobs = beamReport.Jobs.Concat(plateReport.Jobs).ToArray();
            // A later engine or progress callback can change an earlier job's inputs.
            for (int i = 0; i < jobs.Length; i++)
                if (i < beamPlans.Length ? !Current(model, beamPlans[i]) : !platePlans[i - beamPlans.Length].IsCurrent)
                    foreach (var row in jobs[i].Results)
                    { row.Data = DataStatus.Stale; row.Outcome = EngineeringOutcome.NotEvaluated;
                      row.Diagnostics.Add(ModelDiagnostic.Error("ConfiguredInputsChangedDuringRun")); row.SealEvidence(); }
            return WithProvenance(new ModelCheckReport { Jobs = Array.AsReadOnly(jobs), CreatedCheckers = beamReport.CreatedCheckers + plateReport.CreatedCheckers }, model, provenance);
        }
        public ModelCheckReport Verify(VerificationSnapshot snapshot, VerificationConfiguration configuration, EngineCatalog catalog,
            CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
            => Verify((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).OpenModel(), configuration, catalog, token, progress);
        public ModelCheckReport Verify(VerificationSnapshot snapshot, PlateCheckRequest request,
            CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
            => Verify((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).OpenModel(), request, token, progress);
        public static EngineeringOutcome CurrentOutcome(ModelCheckReport report, Models.Model model, VerificationConfiguration configuration, EngineCatalog catalog)
        {
            if (report == null || catalog == null || model == null) return EngineeringOutcome.NotEvaluated;
            try
            {
            var frozen = ConfigurationArchive.Copy(configuration);
            var beams = catalog.Compile(frozen); var plates = catalog.CompilePlates(frozen);
            if (report.Jobs.Count != beams.Jobs.Count + plates.Jobs.Count) return EngineeringOutcome.NotEvaluated;
            if (beams.Jobs.Count > 0 && CurrentOutcome(report.Jobs.Take(beams.Jobs.Count), model, beams) == EngineeringOutcome.NotEvaluated) return EngineeringOutcome.NotEvaluated;
            if (plates.Jobs.Count > 0 && CurrentOutcome(new ModelCheckReport { Jobs = report.Jobs.Skip(beams.Jobs.Count).ToArray() }, model, plates) == EngineeringOutcome.NotEvaluated) return EngineeringOutcome.NotEvaluated;
            return report.Outcome;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException || ex is System.Runtime.Serialization.SerializationException)
            { return EngineeringOutcome.NotEvaluated; }
        }
        private sealed class CombinedProgress : IProgress<VerificationProgress>
        {
            private readonly IProgress<VerificationProgress> _target;
            private readonly int _offset, _total;
            internal CombinedProgress(IProgress<VerificationProgress> target, int offset, int total) { _target = target; _offset = offset; _total = total; }
            public void Report(VerificationProgress value) => _target?.Report(new VerificationProgress {
                Completed = _offset + value.Completed, Total = _total, Job = value.Job, Family = value.Family, ElementId = value.ElementId, Target = value.Target });
        }
    }
}
