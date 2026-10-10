using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Checker;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Results.Queries;
using GPC.Model.Structure.Members;

namespace ModelChecker.Tests;

/// <summary>
/// Member T1 of the step-3 fixture (FEM 250 from I to J and FEM 251 reversed, cantilever 5 m along Z, RC 300x500 with 4Ø16 and one bar moved by
/// 40 mm) with every section datum of step 4: links, torsion profile, crack data and detailing data.
/// </summary>
internal static class Step4Fixture
{
    internal static GPC.Model.Models.Model Model(double torque = 8e6, double axial = 0, double shear = 1000, string exposure = "XC3", bool sensitive = false)
    {
        var m = ElementScopeCharacterizationTest.Model(torque, axial, shear);
        m.PhysicalMembers.Add("T1", new PhysicalMemberDefinition("T1", new[] { new BeamMemberPart(250, true), new BeamMemberPart(251, false) }, "NodeToNode", source: "fixture"));
        var section = m.BeamElements[250].Assignments.Sections[0].Section;
        section.ShearData = new ConcreteShearData(new ConcreteShearReinforcement(8, 200, section.Rebars.First().RebarMaterial),
            new ConcreteShearDirection(500, 250, 402, true, 2, longitudinalCover: 42), new ConcreteShearDirection(300, 450, 402, true, 2, longitudinalCover: 42), "fixture drawings", 20);
        section.TorsionData = new ConcreteTorsionData(80000, 1200, 100, 402, true, "fixture drawings");
        section.CrackData = new ConcreteCrackData(exposure, sensitive, 42, "fixture drawings", maximumBarSpacing: 200);
        section.DetailingData = new ConcreteDetailingData(34, 25, 5, 300, 300, 300, 20, "fixture drawings", compressionBarsRestrained: true, endZonesConfirmed: true);
        return m;
    }

    internal static ResultSelection Selection(CombinationCategory category) => new() { Dataset = "synthetic-member", Case = "LC1", ConcomitantState = "LC1", Category = category };

    internal static readonly SectionCheckSpecification[] AllSectionChecks =
    {
        new SectionCheckSpecification(CheckMechanism.UlsBiaxialSection, CombinationCategory.Ultimate), SectionCheckSpecification.ShearAxis1(), SectionCheckSpecification.ShearAxis2(),
        SectionCheckSpecification.Torsion(), SectionCheckSpecification.StressLimits(CombinationCategory.Characteristic), SectionCheckSpecification.StressLimits(CombinationCategory.QuasiPermanent),
        SectionCheckSpecification.CrackWidth(CombinationCategory.Characteristic), SectionCheckSpecification.CrackWidth(CombinationCategory.QuasiPermanent)
    };

    internal static BeamCheckPlanRequest Plan(SectionCheckSpecification[]? checks = null, string? memberMethod = ConcreteSectionVerifier.BeamDetailingMethod, params CombinationCategory[] categories)
    {
        if (categories.Length == 0) categories = new[] { CombinationCategory.Ultimate, CombinationCategory.Characteristic, CombinationCategory.QuasiPermanent };
        return new BeamCheckPlanRequest
        {
            MemberIds = new[] { "T1" }, Settings = "step4 fixture", SectionMechanisms = new CheckMechanism[0], SectionChecks = checks ?? AllSectionChecks,
            Results = categories.Select(Selection).ToArray(),
            MemberChecks = memberMethod == null ? new MemberCheckSpecification[0] : new[] { new MemberCheckSpecification { MemberId = "T1", MethodId = memberMethod,
                Mechanism = CheckMechanism.Detailing, Context = new MemberDesignContext(globalStateConfirmed: true) } }
        };
    }

    internal static ConcreteVerificationOptions Options(StandardModelCode2010? standard = null, double cotTheta = 1.5) => new()
    {
        Standard = standard ?? new StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
        ServiceabilityAnalysis = SectionSolver.StressAnalysisTypes.Linear, PsiRebar = 2, ShearCotTheta = cotTheta
    };

    internal static ModelCheckRequest Request(BeamCheckPlanRequest plan, params (string Name, ConcreteVerificationOptions Options)[] jobs)
    {
        var request = new ModelCheckRequest();
        if (jobs.Length == 0) jobs = new[] { ("NTC 2018", Options()) };
        foreach (var (name, options) in jobs) request.Jobs.Add(new ModelCheckJob { Name = name, BeamPlan = plan.Copy(), Options = options });
        return request;
    }

    internal static string Describe(CheckResult r) => (r.Check?.Key ?? r.MethodId) + " @" + r.ElementId + "/" + r.Station + " " + r.Applicability + " " + r.Data + " " + r.Outcome
        + " u=" + r.Utilization + " " + string.Join(",", r.Diagnostics.Select(d => d.Code));
}
