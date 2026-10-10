using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Analysis;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    public sealed partial class ModelChecker
    {
        public ModelCheckReport Verify(PreparedVerification prepared, ModelCheckRequest request, CancellationToken token = default,
            IProgress<VerificationProgress> progress = null)
        {
            if (prepared == null || request == null) throw new ArgumentNullException();
            if (!prepared.IsCurrent) return Blocked(request.Jobs.Select(j => j?.Name), prepared.Provenance,
                prepared.Compatibility.Status == AnalysisCompatibility.Compatible ? "StalePreparedScenario" : prepared.Compatibility.Code,
                "The prepared scenario is incompatible, unknown or changed; no checker was created.");
            return Verify(prepared.Model, request, token, progress);
        }
        public ModelCheckReport Verify(PreparedVerification prepared, MultiMaterialCheckRequest request, CancellationToken token = default,
            IProgress<VerificationProgress> progress = null)
        {
            if (prepared == null || request == null) throw new ArgumentNullException();
            if (!prepared.IsCurrent) return Blocked(request.Jobs.Select(j => j?.Name), prepared.Provenance,
                prepared.Compatibility.Status == AnalysisCompatibility.Compatible ? "StalePreparedScenario" : prepared.Compatibility.Code,
                "The prepared scenario is incompatible, unknown or changed; no checker was created.");
            return Verify(prepared.Model, request, token, progress);
        }
        private static ModelCheckReport AnalysisGate(Models.Model model, IEnumerable<string> jobs, out VerificationProvenance provenance)
        {
            model.Analysis?.OpenModel();
            provenance = VerificationPreparation.CurrentProvenance(model);
            var compatibility = AnalysisCompatibilityValidator.Validate(model);
            if (compatibility.Status != AnalysisCompatibility.Compatible) return Blocked(jobs, provenance, compatibility.Code, compatibility.Message);
            if (!provenance.IsCurrent(model)) return Blocked(jobs, provenance, "StalePreparedScenario", "The materialized scenario changed after preparation.");
            return null;
        }
        private static ModelCheckReport Blocked(IEnumerable<string> jobs, VerificationProvenance provenance, string code, string message)
        {
            var reports = jobs.Select(name => {
                var result = new CheckResult { Job = name, Data = DataStatus.Insufficient, Provenance = provenance,
                    Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error(code, message: message) } };
                result.SealEvidence(); return CheckReport.ForSingleResult(result);
            }).ToArray();
            if (reports.Length == 0) throw new ArgumentException("At least one verification job is required.");
            return new ModelCheckReport { Jobs = Array.AsReadOnly(reports), CreatedCheckers = 0 };
        }
        private static ModelCheckReport WithProvenance(ModelCheckReport report, Models.Model model, VerificationProvenance provenance)
        {
            bool current = provenance.IsCurrent(model);
            foreach (var result in report.Jobs.SelectMany(j => j.Results))
            {
                result.Provenance = provenance;
                if (!current) { result.Data = DataStatus.Stale; result.Outcome = EngineeringOutcome.NotEvaluated;
                    result.Diagnostics.Add(ModelDiagnostic.Error("ScenarioChangedDuringVerification")); }
                result.SealEvidence();
            }
            return report;
        }
    }
}
