using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Data.Steel;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Automatic NcrT/NcrFT, beam-column critical values and segment-wise fork preparation.
/// Fixture member S1: one element 0..2000 mm along Z, cuts at 0/500/1000/1500/2000, H300x12/150x18 S355,
/// section axes X (principal 1, lateral), Y (principal 2, in-plane), Z (member axis).</summary>
[TestClass]
public class AutomaticCriticalValuesTest
{
    private static double Value(CheckResult r, string key) => r.Details.Trace.Single(t => t.Key == key).Value!.Value;
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(d => d.Code + ":" + d.Message))
        + " | " + string.Join(";", r.Details?.Trace.Where(t => t.Key.EndsWith(".Status") || t.Key.EndsWith("MissingInputs") || t.Key.Contains("Utilization")).Select(t => t.Key + "=" + t.Value + " " + t.Expression) ?? Array.Empty<string>());
    // Flags in the global frame = member section frame: X lateral, Y in-plane, Z axial, then rotations about X, Y, Z (twist).
    private static MemberRestraint Restraint(double distance, bool? lateral, bool? inPlane, bool? twist)
        => new(distance, CoordinateSystem.Global, new[] { lateral, inPlane, null, null, null, twist }, "Bracing at " + distance + " mm");
    private static (GPC.Model.Models.Model Model, BeamCheckPlanRequest Plan) Fixture(double n, double m1, IEnumerable<MemberRestraint>? restraints = null,
        Func<SteelSection>? section = null, double? effectiveLength = null)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture(sectionFactory: section);
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { sample.ResultBeamForces.N = n; sample.ResultBeamForces.M1 = m1; sample.ResultBeamForces.V2 = 0; }
        plan.MemberChecks[0].Context = new MemberDesignContext(restraints, effectiveLength, effectiveLength, lengthSource: effectiveLength.HasValue ? "Explicit test lengths" : null,
            assumptions: "Explicit fork test declaration", globalStateConfirmed: true);
        return (model, plan);
    }
    private static Dictionary<string, string> Options(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, string mode, bool automatic = true)
    {
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (var k in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(k);
        p["AnalysisMode"] = mode;
        if (automatic) { p["CriticalValues"] = "UniformFork"; p["CriticalValuesSource"] = "Explicit member-end forks, free warping, loads through the shear centre"; }
        if (mode != "Bending") p["CurveSelection"] = "Automatic";
        if (mode != "Compression") p["McrBasis"] = "UniformMoment";
        if (mode == "BeamColumn")
        {
            p["TorsionSusceptible"] = "true"; p["Cm1"] = p["Cm2"] = p["CmLT"] = "1";
            p["MomentFactorSource"] = "Explicit conservative Cm=1"; p["StabilityModelSource"] = "First-order nonsway member, no plastic hinges";
        }
        return p;
    }
    private static CheckResult Run(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, Dictionary<string, string> p, bool ntc = false)
        => new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, ntc ? new StandardNTC2018Steel() : new StandardEN1993p11())).Jobs[0].Results.Single();
    private static ISteelSection Section(GPC.Model.Models.Model model) => (ISteelSection)model.BeamElements[250].BeamProperty;
    // Closed-form uniform-moment fork Mcr: (pi/L) sqrt(E Iz G It) sqrt(1 + pi^2 E Iw / (L^2 G It)).
    private static double Mcr(ISteelSection s, double l)
    {
        double e = s.SteelMaterial.E, g = e / (2 * (1 + s.SteelMaterial.Ni));
        return Math.PI / l * Math.Sqrt(e * s.J22 * g * s.Jt) * Math.Sqrt(1 + Math.PI * Math.PI * e * s.Jw / (l * l * g * s.Jt));
    }
    private static double NcrT(ISteelSection s, double l)
    {
        double e = s.SteelMaterial.E, g = e / (2 * (1 + s.SteelMaterial.Ni));
        return (g * s.Jt + Math.PI * Math.PI * e * s.Jw / (l * l)) / ((s.J11 + s.J22) / s.Area);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MidSpanForkRestraintGivesHalfSpanMcrAndVerdict(bool ntc)
    {
        // H300x12/150x18: Iz=1.0163e7, It=7.35e5, Iw=2.013e11, G=80769 -> Mcr(1000)=3.17e9, Mcr(2000)=9.28e8 Nmm.
        // Wpl1=9.70e5mm3, Mpl=3.445e8: lambda_LT=0.330 on the half span instead of 0.609 on the whole member.
        const double m = 2e8;
        var (model, plan) = Fixture(0, m, new[] { Restraint(1000, true, false, true) });
        var p = Options(model, plan, "Bending");
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        var s = Section(model); double half = Mcr(s, 1000);
        Assert.AreEqual(3.17e9, half, 3.17e9 * .01); Assert.AreEqual(9.28e8, Mcr(s, 2000), 9.28e8 * .01); // hand values above
        Assert.AreEqual(2, Value(r, "Member.Segment.Count")); Assert.AreEqual(half, Value(r, "Member.Segment.1.Mcr"), half * 1e-12);
        Assert.AreEqual(half, Value(r, "Member.Segment.2.Mcr"), half * 1e-12); Assert.AreEqual(half, Value(r, "Member.External.Mcr"), half * 1e-12);
        Assert.AreEqual(1, Value(r, "Member.Segment.Governing")); // equal segments: the first governs
        Assert.AreEqual(1000, Value(r, "Member.Segment.Governing.End"));
        Assert.AreEqual(Math.Sqrt(s.Wpl1 * 355 / half), Value(r, "MemberStability.SlendernessLT"), 1e-12);
        StringAssert.Contains(r.Details.Trace.Single(t => t.Key == "Member.Restraint.1.Distance").Expression, "fork");
        // Verdict: identical to an explicit Mcr of the half span; not the whole-member value.
        var (single, singlePlan) = Fixture(0, m); var explicitHalf = Options(single, singlePlan, "Bending", false);
        explicitHalf["Mcr.Value"] = half.ToString("R", System.Globalization.CultureInfo.InvariantCulture); explicitHalf["Mcr.Unit"] = "Nmm"; explicitHalf["Mcr.Source"] = "closed form, L=1000";
        var reference = Run(single, singlePlan, explicitHalf, ntc);
        Assert.AreEqual(reference.Utilization!.Value, r.Utilization!.Value, 1e-12);
        var whole = Run(single, singlePlan, Options(single, singlePlan, "Bending"), ntc);
        Assert.AreEqual(Mcr(s, 2000), Value(whole, "Member.External.Mcr"), Mcr(s, 2000) * 1e-10);
        Assert.IsTrue(whole.Utilization > r.Utilization * 1.05, whole.Utilization + " vs " + r.Utilization);
    }

    [TestMethod]
    public void SegmentedResultsArchiveAndRevalidateWithTheRestraintContext()
    {
        var (model, plan) = Fixture(0, 2e8, new[] { Restraint(1000, true, false, true) });
        var request = SteelMemberPipelineTest.Request(plan, Options(model, plan, "Bending"));
        var report = new Service().Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Jobs[0].Results.Single().Outcome, Diagnostics(report.Jobs[0].Results.Single()));
        using var stream = new MemoryStream(); GPC.Model.Persistence.CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var archived = GPC.Model.Persistence.CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(archived, model, request));
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]).ElementAt(3).ResultBeamForces.M1 *= 2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(archived, model, request));
    }

    [TestMethod]
    public void ActualDiagramGovernsTheLoadedSegment()
    {
        // Linear M1 from 2e8 (x=0) to 0 (x=2000), V2=-dM1/dz=1e5: segment 1 carries 2e8..1e8, segment 2 1e8..0.
        var (model, plan) = Fixture(0, 0, new[] { Restraint(1000, true, false, true) });
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { sample.ResultBeamForces.M1 = 2e8 * (1 - sample.ParametricDistance); sample.ResultBeamForces.V2 = 1e5; }
        var p = Options(model, plan, "Bending"); p["CriticalValues"] = "ForkDiagram"; p["ForkDiagram"] = "EndMoments";
        var r = Run(model, plan, p);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(1, Value(r, "Member.Segment.Governing")); Assert.AreEqual(2e8, Value(r, "Input.M1"), 1e-6);
        // psi=0.5 on both halves: SN003b Table 3.1 C1=1.31 for the actual diagram (EC3); basis is UniformMoment here.
        Assert.AreEqual(Mcr(Section(model), 1000), Value(r, "Member.Segment.1.Mcr"), Mcr(Section(model), 1000) * 1e-12);
        Assert.IsTrue(Value(r, "Member.Segment.2.Utilization") < Value(r, "Member.Segment.1.Utilization"));
    }

    [DataTestMethod]
    [DataRow(true, null, false, "SteelMember.Restraint.LateralWithoutTwist")] // one flange: lateral without twist
    [DataRow(false, null, true, "SteelMember.Restraint.TwistWithoutLateral")]
    [DataRow(true, null, null, "SteelMember.Restraint.Unknown")]
    public void PartialOneFlangeOrUnknownRestraintsAreRefused(bool? lateral, bool? inPlane, bool? twist, string code)
    {
        var (model, plan) = Fixture(0, 2e8, new[] { Restraint(1000, lateral, inPlane, twist) });
        var r = Run(model, plan, Options(model, plan, "Bending"));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Code == code), Diagnostics(r));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MidSpanForkInCompressionHalvesMinorAndTorsionalLengthsOnly(bool ntc)
    {
        var (model, plan) = Fixture(-1e5, 0, new[] { Restraint(1000, true, false, true) });
        var p = Options(model, plan, "Compression");
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count);
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(2000, Value(r, "MemberStability.Ncr1.EffectiveLength")); // no in-plane restraint: full member
        Assert.AreEqual(1000, Value(r, "MemberStability.Ncr2.EffectiveLength"));
        Assert.AreEqual(1000, Value(r, "MemberStability.NcrT.Length"));
        var s = Section(model); Assert.AreEqual(NcrT(s, 1000), Value(r, "MemberStability.NcrT"), NcrT(s, 1000) * 1e-12);
        Assert.AreEqual(1, Value(r, "MemberStability.NcrFT.AutomaticExclusion"));
        Assert.AreEqual(Math.PI * Math.PI * s.SteelMaterial.E * s.J22 / 1e6, Value(r, "MemberStability.Ncr2"), 1e-3);
        // An in-plane (vertical) support restrains Ncr1 only and needs no twist flag.
        (model, plan) = Fixture(-1e5, 0, new[] { Restraint(1000, false, true, false) });
        r = Run(model, plan, Options(model, plan, "Compression"), ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(1000, Value(r, "MemberStability.Ncr1.EffectiveLength")); Assert.AreEqual(2000, Value(r, "MemberStability.Ncr2.EffectiveLength"));
        Assert.AreEqual(2000, Value(r, "MemberStability.NcrT.Length")); Assert.AreEqual(1, Value(r, "Member.Segment.Count"));
        // Unknown in-plane flag at an interior restraint: refused, never assumed.
        (model, plan) = Fixture(-1e5, 0, new[] { Restraint(1000, true, null, true) });
        Assert.IsTrue(Run(model, plan, Options(model, plan, "Compression"), ntc).Diagnostics.Any(d => d.Code == "SteelMember.Restraint.Unknown"));
    }

    [DataTestMethod] [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
    public void BeamColumnPreparesAllCriticalValuesAutomatically(bool ntc, bool braced)
    {
        // N=-100kN, uniform M1=50kNm: Ncr1/Ncr2/NcrT from the fork lengths, Mcr uniform; FT not distinct.
        var (model, plan) = Fixture(-1e5, 5e7, braced ? new[] { Restraint(1000, true, false, true) } : null);
        var p = Options(model, plan, "BeamColumn");
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count, string.Join(";", SteelParameterSchemas.Member.Validate(p).Select(i => i.Code)));
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        double l = braced ? 1000 : 2000; var s = Section(model);
        Assert.AreEqual(Mcr(s, l), Value(r, "Member.External.Mcr"), Mcr(s, l) * 1e-12);
        Assert.AreEqual(2000, Value(r, "MemberStability.Ncr1.EffectiveLength")); Assert.AreEqual(l, Value(r, "MemberStability.Ncr2.EffectiveLength"));
        Assert.AreEqual(NcrT(s, l), Value(r, "MemberStability.NcrT"), NcrT(s, l) * 1e-12);
        Assert.AreEqual(1, Value(r, "MemberStability.NcrFT.AutomaticExclusion")); Assert.AreEqual(braced ? 2 : 1, Value(r, "Member.Segment.Count"));
        Assert.AreEqual(Math.Sqrt(s.Wpl1 * 355 / Mcr(s, l)), Value(r, "MemberStability.LambdaLT"), 1e-12);
        // A varying moment needs the declared diagram: UniformFork refuses it.
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { sample.ResultBeamForces.M1 = 5e7 * (1 - sample.ParametricDistance); sample.ResultBeamForces.V2 = 5e7 / 2000; }
        p["MemberFingerprint"] = SteelMemberPipelineTest.Options(model, plan)["MemberFingerprint"];
        Assert.IsTrue(Run(model, plan, p, ntc).Diagnostics.Any(d => d.Code == "SteelMember.Segment.UniformMomentRequired"));
        p["CriticalValues"] = "ForkDiagram"; p["ForkDiagram"] = "EndMoments"; p["ShearBucklingExclusionSource"] = "Stocky web: hw/tw=22 < 72 eps";
        var diagram = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, diagram.Outcome, Diagnostics(diagram));
    }

    [TestMethod]
    public void ClosedHollowCompressionExcludesTorsionalModesAutomatically()
    {
        var (model, plan) = Fixture(-1e5, 0, section: () => new SteelSection(new SectionRHS(200, 100, 8, 8, 8, 8, "RHS"), SteelMaterialEN1993Data.S355));
        var r = Run(model, plan, Options(model, plan, "Compression"));
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(1, Value(r, "MemberStability.NcrT.AutomaticExclusion")); Assert.AreEqual(1, Value(r, "MemberStability.NcrFT.AutomaticExclusion"));
        Assert.IsTrue(Value(r, "MemberStability.NcrT.LowerBoundSlenderness") < .2);
    }

    [TestMethod]
    public void ExplicitTorsionalExclusionWinsOverTheAutomaticFork()
    {
        var (model, plan) = Fixture(-1e5, 0); var p = Options(model, plan, "Compression");
        p["TorsionalExclusionSource"] = "Continuous slab prevents twist";
        Assert.AreEqual(0, SteelParameterSchemas.Member.Validate(p).Count);
        var r = Run(model, plan, p);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(1, Value(r, "MemberStability.NcrT.Excluded")); Assert.IsFalse(r.Details.Trace.Any(t => t.Key == "Member.External.NcrT"));
        Assert.IsTrue(r.Details.Trace.Any(t => t.Key == "Member.External.Ncr1")); // the historical full-span values otherwise unchanged
    }

    [TestMethod]
    public void ExplicitModelTwistRestraintsGiveTheTorsionalLengthWithoutAutomaticPreparation()
    {
        var ends = new[] { Restraint(0, null, null, true), Restraint(2000, null, null, true) };
        var (model, plan) = Fixture(-1e5, 0, ends, effectiveLength: 2000);
        var p = Options(model, plan, "Compression", false);
        var r = Run(model, plan, p);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(2000, Value(r, "Member.Torsional.Length")); Assert.AreEqual(2000, Value(r, "MemberStability.NcrT.Length"));
        Assert.AreEqual(NcrT(Section(model), 2000), Value(r, "MemberStability.NcrT"), NcrT(Section(model), 2000) * 1e-12);
        (model, plan) = Fixture(-1e5, 0, new[] { ends[0] }, effectiveLength: 2000); // one end only: not derived
        r = Run(model, plan, Options(model, plan, "Compression", false));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsTrue(r.Details.Trace.Any(t => t.Key == "Member.Torsional.LengthNotDerived"));
    }

    [TestMethod]
    public void DoublySymmetricHColumnResolvesFlexuralTorsionalModeWithUnchangedUtilization()
    {
        // Replaces the FlexuralTorsionalExclusionSource row of MissingMethodEvidenceCannotBecomeAPassingMember:
        // H300 column, x0=y0=0, so FT has no distinct root and the explicit exclusion changes nothing.
        var model = SteelBeamColumnFrame.Create();
        CheckResult Single(Dictionary<string, string> options)
        {
            var input = BeamCheckPlan.PrepareActions(model, SteelReferenceFrame.Plan("C1", "ULS-A", false)).WorkItems.Single().Member;
            options["MemberFingerprint"] = ModelValues.Fingerprint(new object[] { input.Snapshot });
            return new SteelMemberPipelineChecker(new StandardEN1993p11(), "2005", parameters: options).Verify(input, CancellationToken.None);
        }
        var explicitFt = Single(SteelBeamColumnFrame.Configure(model, false).Engines[0].Parameters);
        var options = SteelBeamColumnFrame.Configure(model, false).Engines[0].Parameters; options.Remove("FlexuralTorsionalExclusionSource");
        var automatic = Single(options);
        Assert.AreEqual(EngineeringOutcome.Satisfied, explicitFt.Outcome, Diagnostics(explicitFt));
        Assert.AreEqual(EngineeringOutcome.Satisfied, automatic.Outcome, Diagnostics(automatic));
        Assert.AreEqual(explicitFt.Utilization!.Value, automatic.Utilization!.Value, 1e-15);
        Assert.AreEqual(1, Value(automatic, "MemberStability.NcrFT.AutomaticExclusion"));
    }
}
