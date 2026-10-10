using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    public sealed class ModelCheckReport
    {
        public IReadOnlyList<CheckReport> Jobs { get; internal set; } = new CheckReport[0];
        public int CreatedCheckers { get; internal set; }
        public int Required => Jobs.Sum(j => j.Required);
        public int Executed => Jobs.Sum(j => j.Executed);
        public CheckSummary Summary => new CheckSummary(Required, Jobs.SelectMany(j => j.Results), Jobs.All(j => j.HasUnchangedScope && j.Results.Count == j.Required));
        public IReadOnlyList<ElementCheckReport> Elements => CheckReportViews.ByElement(Jobs.SelectMany(j => j.Results));
        public IReadOnlyList<CheckResultGroup> Members => CheckReportViews.ByMember(Jobs.SelectMany(j => j.Results));
        public IReadOnlyList<CheckResultGroup> Groups => CheckReportViews.ByGroup(Jobs.SelectMany(j => j.Results));
        public IReadOnlyList<CheckResultGroup> Standards => CheckReportViews.ByStandard(Jobs.SelectMany(j => j.Results));
        public IReadOnlyList<CheckResultGroup> Mechanisms => CheckReportViews.ByMechanism(Jobs.SelectMany(j => j.Results));
        public EngineeringOutcome Outcome => Jobs.Any(j => !j.Summary.IsComplete) ? EngineeringOutcome.NotEvaluated : Summary.Outcome;

        public EngineeringOutcome CurrentOutcome(Models.Model model, Func<string, IConcreteSectionVerifier> verifierForJob)
            => CurrentOutcome(model, verifierForJob, null);
        public EngineeringOutcome CurrentOutcome(Models.Model model, Func<string, IConcreteSectionVerifier> verifierForJob, Func<string, IPhysicalMemberVerifier> memberVerifierForJob)
            => CurrentOutcome(model, verifierForJob, memberVerifierForJob, null);
        public EngineeringOutcome CurrentOutcome(Models.Model model, Func<string, IConcreteSectionVerifier> verifierForJob, Func<string, IPhysicalMemberVerifier> memberVerifierForJob,
            Func<string, BeamCheckPlanRequest> currentPlanForJob)
        {
            if (Jobs.Any(j => j.CurrentOutcome(model, verifierForJob?.Invoke(j.Job), memberVerifierForJob?.Invoke(j.Job), currentPlanForJob?.Invoke(j.Job)) == EngineeringOutcome.NotEvaluated)) return EngineeringOutcome.NotEvaluated;
            return Outcome;
        }
    }
}
