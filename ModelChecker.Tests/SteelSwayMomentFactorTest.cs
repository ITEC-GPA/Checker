using System.Globalization;
using GPC.Examples;
using GPC.Geometry;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Declared sway buckling planes in the beam-column: EN 1993-1-1:2005 Annex B Table B.3 note (Circolare 2019
/// Table C4.2.VI, same note assumed): Cm=0.9 in the declared plane whatever the diagram, the other plane and CmLT keep
/// their own preparation; the Ncr of a sway plane is an explicit value or a declared effective length, automatic
/// nonsway fork-segment values are refused. Columns: H300x150x12/18 S355 (R=0), class 1 under these actions.</summary>
[TestClass]
public class SteelSwayMomentFactorTest
{
    // By hand: A=2*150*18+264*12=8568 mm2, I1=(150*300^3-138*264^3)/12=125902944, I2=2*18*150^3/12+264*12^3/12=10163016 mm4,
    // Wpl1=2*150*18*141+12*132^2=970488, Wpl2=2*18*150^2/4+264*12^2/4=212004 mm3, fy=355, E=210000 MPa; curves b (.34), c (.49).
    private const double A = 8568, I1 = 125902944, I2 = 10163016, Wpl1 = 970488, Wpl2 = 212004, Fy = 355, E = 210000;
    private const string SwaySource = "Test portal unbraced in the declared plane(s): global sway buckling mode, first-order design forces (equivalent column)";
    private static double Euler(double inertia, double length) => Math.PI * Math.PI * E * inertia / (length * length);
    private static double Chi(double lambda, double alpha)
    { double p = .5 * (1 + alpha * (lambda - .2) + lambda * lambda); return Math.Min(1, 1 / (p + Math.Sqrt(p * p - lambda * lambda))); }

    /// <summary>EC3 Annex B Table B.1 / Circolare Table C4.2.IV with the NTC kzz index envelope: classes 1-2, torsion
    /// restrained, kyz=.6 kzz, kzy=.6 kyy; n compression and the |M1|, |M2| whole-member maxima of one state.</summary>
    private static (double U, double K11, double K22) Restrained(bool ntc, double ncr1, double ncr2, double cm1, double cm2, double n, double m1, double m2)
    {
        double g = ntc ? 1.05 : 1, nrk = A * Fy, l1 = Math.Sqrt(nrk / ncr1), l2 = Math.Sqrt(nrk / ncr2);
        double r1 = n * g / (Chi(l1, .34) * nrk), r2 = n * g / (Chi(l2, .49) * nrk), b1 = m1 * g / (Wpl1 * Fy), b2 = m2 * g / (Wpl2 * Fy);
        double k11 = cm1 * (1 + Math.Min(l1 - .2, .8) * r1), k22 = cm2 * (1 + Math.Min(2 * l2 - .6, 1.4) * r2);
        if (ntc) k22 = Math.Max(k22, cm2 * (1 + Math.Min(2 * l1 - .6, 1.4) * r1));
        return (Math.Max(r1 + k11 * b1 + .6 * k22 * b2, r2 + .6 * k11 * b1 + k22 * b2), k11, k22);
    }

    private static double Value(CheckResult r, string key) => r.Details.Trace.Single(t => t.Key == key).Value!.Value;
    private static string Expression(CheckResult r, string key) => r.Details.Trace.Single(t => t.Key == key).Expression;
    private static string Diagnostics(CheckResult r) => string.Join(";", r.Diagnostics.Select(d => d.Code + ":" + d.Message));
    private static IReadOnlyList<EngineParameterIssue> Validate(Dictionary<string, string> p) => SteelParameterSchemas.Member.Validate(p);
    private static string Issues(Dictionary<string, string> p) => string.Join(";", Validate(p).Select(i => i.Code + ":" + i.Key));
    private static void Sway(Dictionary<string, string> p, string planes)
    {
        p["SwayBucklingPlanes"] = planes; p["SwayModeSource"] = SwaySource;
        p["StabilityModelSource"] = "Straight prismatic member, first-order forces; sway buckling mode in plane(s) " + planes + "; no plastic hinges";
    }
    private static void Ncr(Dictionary<string, string> p, int plane, double value, string source)
    {
        p["Ncr" + plane + ".Value"] = value.ToString("R", CultureInfo.InvariantCulture); p["Ncr" + plane + ".Unit"] = "N"; p["Ncr" + plane + ".Source"] = source;
    }
    private static void RemoveFactors(Dictionary<string, string> p) { foreach (string key in new[] { "Cm1", "Cm2", "CmLT" }) p.Remove(key); }

    // SteelBeamColumnFrame column C1, L=3000: ULS-A N=-20kN, M1=10kNm*(1-x/L), M2=8kNm*x/L (or 8kNm constant);
    // ULS-B N=-30kN, M1=30kNm*(1-x/L), M2=5kNm*x/L (or 5kNm constant). Torsion restrained (no LTB).
    private static Dictionary<string, string> FrameOptions(GPC.Model.Models.Model model, string state, bool ntc)
        => SteelBeamColumnFrame.Configure(model, ntc).Engines.Single(e => e.Id == "C1-" + state + "-member").Parameters;
    private static CheckResult Frame(GPC.Model.Models.Model model, string state, Dictionary<string, string> p, bool ntc, MemberDesignContext? context = null)
    {
        var plan = SteelReferenceFrame.Plan("C1", state, false);
        if (context != null) plan.MemberChecks[0].Context = context;
        var input = BeamCheckPlan.PrepareActions(model, plan).WorkItems.Single().Member;
        p["MemberFingerprint"] = ModelValues.Fingerprint(new object[] { input.Snapshot });
        return new SteelMemberPipelineChecker(ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(), ntc ? "2018" : "2005", parameters: p)
            .Verify(input, CancellationToken.None);
    }
    private static MemberDesignContext Lengths(double? l1, double? l2, string source)
        => new(effectiveLength1: l1, effectiveLength2: l2, lateralTorsionalLength: 3000, lengthSource: source,
            assumptions: "Test portal column; actions assigned, first-order", globalStateConfirmed: true);

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SwayPlaneOneTakesPointNineWhileLinearDiagramsSetThePlaneTwoAndLtbFactors(bool ntc)
    {
        // Plane 1 sway with an explicit Ncr1 (assigned Leff=2L=6000 mm); plane 2 braced, Model EffectiveLength2=3000.
        // Table B.3 / C4.2.VI linear diagrams, psi=0: Cm2=CmLT=max(.4,.6+.4*0)=.6; Cm1=.9 whatever the M1 diagram.
        var model = SteelBeamColumnFrame.Create(false, false, true); var p = FrameOptions(model, "ULS-A", ntc); RemoveFactors(p);
        p["MomentFactorPreparation"] = "LinearDiagrams";
        p["MomentFactorSource"] = "Same full physical span for both bending planes and LTB; plane 2 braced (nonsway)";
        Sway(p, "1"); double ncr1 = Euler(I1, 6000), ncr2 = Euler(I2, 3000);
        Ncr(p, 1, ncr1, "Sway buckling analysis of the test portal, Ncr1=pi^2 E I1/(2L)^2");
        var context = Lengths(null, 3000, "Braced out of plane at both column ends");
        Assert.AreEqual(0, Validate(p).Count, Issues(p));
        var r = Frame(model, "ULS-A", p, ntc, context);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(.9, Value(r, "MemberStability.Cm1"), 1e-15);
        Assert.AreEqual(.6, Value(r, "MemberStability.Cm2"), 1e-12); Assert.AreEqual(.6, Value(r, "MemberStability.CmLT"), 1e-12);
        Assert.AreEqual(ncr1, Value(r, "MemberStability.Ncr1"), ncr1 * 1e-12); Assert.AreEqual(ncr2, Value(r, "MemberStability.Ncr2"), ncr2 * 1e-9);
        var hand = Restrained(ntc, ncr1, ncr2, .9, .6, 2e4, 10e6, 8e6);
        Assert.AreEqual(hand.K11, Value(r, "MemberStability.K11"), 1e-12); Assert.AreEqual(hand.K22, Value(r, "MemberStability.K22"), 1e-12);
        Assert.AreEqual(hand.U, r.Utilization!.Value, hand.U * 1e-9);
        // Per-plane trace of the factors and of the sway Ncr.
        StringAssert.Contains(Expression(r, "Member.MomentFactor.Cm1"), ntc ? "TableC4.2.VI" : "TableB.3 note");
        StringAssert.Contains(Expression(r, "Member.MomentFactor.Cm1"), SwaySource);
        StringAssert.Contains(Expression(r, "Member.MomentFactor.Cm2"), "linear nonsway diagram");
        StringAssert.Contains(Expression(r, "Member.MomentFactor.CmLT"), "not affected by the sway declaration");
        Assert.AreEqual(ncr1, Value(r, "Member.Sway.Plane1.Ncr"), ncr1 * 1e-12);
        StringAssert.Contains(Expression(r, "Member.Sway.Plane1.Ncr"), "Sway buckling analysis");
        Assert.IsFalse(r.Details.Trace.Any(t => t.Key.StartsWith("Member.Sway.Plane2")));
        StringAssert.Contains(Expression(r, "Member.MomentFactorSource"), "Cm1=0.9 (");
        if (ntc) StringAssert.Contains(Expression(r, "Member.MomentFactorSource"), "GPC assumption");
        // Without the declaration the same configuration keeps the nonsway linear Cm1=.6 and no sway rows.
        p.Remove("SwayBucklingPlanes"); p.Remove("SwayModeSource");
        var nonsway = Frame(model, "ULS-A", p, ntc, context); var expected = Restrained(ntc, ncr1, ncr2, .6, .6, 2e4, 10e6, 8e6);
        Assert.AreEqual(.6, Value(nonsway, "MemberStability.Cm1"), 1e-12); Assert.AreEqual(expected.U, nonsway.Utilization!.Value, expected.U * 1e-9);
        Assert.IsTrue(hand.U > expected.U);
        Assert.IsFalse(nonsway.Details.Trace.Any(t => t.Key.StartsWith("Member.Sway.") || t.Key.StartsWith("Member.MomentFactor.")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BothSwayPlanesWithConservativeEnvelopeUseDeclaredSwayEffectiveLengths(bool ntc)
    {
        // Split member with reversed second element, ULS-B, M2=5kNm constant. Both planes sway: Cm1=Cm2=.9, CmLT=1 (envelope).
        // Ncr1, Ncr2 from the declared MemberDesignContext sway lengths 2L=6000 mm in both planes.
        var model = SteelBeamColumnFrame.Create(true, true); var p = FrameOptions(model, "ULS-B", ntc); RemoveFactors(p);
        p["MomentFactorPreparation"] = "ConservativeEnvelope";
        p["MomentFactorSource"] = "Generic same-case FEM diagrams; no favourable gradient";
        p["MomentDiagramCoverageSource"] = "Assigned linear diagrams; endpoints and both element faces exported";
        Sway(p, "1,2");
        var context = Lengths(6000, 6000, "Sway effective lengths 2L in both planes, cantilever-type test column");
        Assert.AreEqual(0, Validate(p).Count, Issues(p));
        var r = Frame(model, "ULS-B", p, ntc, context);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(.9, Value(r, "MemberStability.Cm1"), 1e-15); Assert.AreEqual(.9, Value(r, "MemberStability.Cm2"), 1e-15);
        Assert.AreEqual(1, Value(r, "MemberStability.CmLT"));
        Assert.AreEqual(6000, Value(r, "MemberStability.Ncr1.EffectiveLength")); Assert.AreEqual(6000, Value(r, "MemberStability.Ncr2.EffectiveLength"));
        var hand = Restrained(ntc, Euler(I1, 6000), Euler(I2, 6000), .9, .9, 3e4, 30e6, 5e6);
        Assert.AreEqual(hand.K11, Value(r, "MemberStability.K11"), 1e-12); Assert.AreEqual(hand.K22, Value(r, "MemberStability.K22"), 1e-12);
        Assert.AreEqual(hand.U, r.Utilization!.Value, hand.U * 1e-9);
        foreach (int plane in new[] { 1, 2 })
        {
            Assert.AreEqual(6000, Value(r, "Member.Sway.Plane" + plane + ".EffectiveLength"));
            StringAssert.Contains(Expression(r, "Member.Sway.Plane" + plane + ".EffectiveLength"), "Sway effective lengths 2L");
        }
        StringAssert.Contains(Expression(r, "Member.MomentFactor.CmLT"), "generic same-case FEM diagram");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SwayPlaneTwoCombinesWithExplicitFactorsOfTheOtherPlaneAndRefusesItsOwn(bool ntc)
    {
        // Explicit route: Cm1=.85 and CmLT=1 explicit, Cm2=.9 from the sway declaration; explicit sway Ncr2 (Leff=6000);
        // Ncr1 from the frame's declared EffectiveLength1=3000 (plane 1 braced).
        var model = SteelBeamColumnFrame.Create(false, false, true); var p = FrameOptions(model, "ULS-A", ntc);
        p["Cm1"] = "0.85"; p.Remove("Cm2"); Sway(p, "2"); double ncr2 = Euler(I2, 6000);
        Ncr(p, 2, ncr2, "Sway buckling analysis of the test portal, Ncr2=pi^2 E I2/(2L)^2");
        Assert.AreEqual(0, Validate(p).Count, Issues(p));
        var r = Frame(model, "ULS-A", p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(.85, Value(r, "MemberStability.Cm1")); Assert.AreEqual(.9, Value(r, "MemberStability.Cm2"), 1e-15);
        var hand = Restrained(ntc, Euler(I1, 3000), ncr2, .85, .9, 2e4, 10e6, 8e6);
        Assert.AreEqual(hand.K11, Value(r, "MemberStability.K11"), 1e-12); Assert.AreEqual(hand.K22, Value(r, "MemberStability.K22"), 1e-12);
        Assert.AreEqual(hand.U, r.Utilization!.Value, hand.U * 1e-9);
        StringAssert.Contains(Expression(r, "Member.MomentFactor.Cm1"), "explicit factor");
        Assert.AreEqual(ncr2, Value(r, "Member.Sway.Plane2.Ncr"), ncr2 * 1e-12);
        // An explicit factor of the sway plane conflicts with the computed 0.9.
        p["Cm2"] = "1";
        Assert.IsTrue(Validate(p).Any(i => i.Code == "SwayMomentFactorConflict" && i.Key == "Cm2"));
        var conflict = Frame(model, "ULS-A", p, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, conflict.Outcome); Assert.IsNull(conflict.Utilization);
        Assert.IsTrue(conflict.Diagnostics.Any(d => d.Code == "SteelMember.InputDetail" && d.Message.Contains("plane 2 is declared sway")), Diagnostics(conflict));
    }

    [DataTestMethod]
    [DataRow(false, "computed-and-explicit")] [DataRow(true, "computed-and-explicit")] [DataRow(false, "missing-source")]
    [DataRow(false, "invalid-planes")] [DataRow(false, "missing-ncr")] [DataRow(true, "missing-ncr")]
    public void IncompleteOrConflictingSwayDeclarationsDoNotPass(bool ntc, string problem)
    {
        var model = SteelBeamColumnFrame.Create(false, false, true); var p = FrameOptions(model, "ULS-A", ntc); MemberDesignContext? context = null;
        Sway(p, "1"); p.Remove("Cm1");
        if (problem == "computed-and-explicit") { p["MomentFactorPreparation"] = "LinearDiagrams"; p.Remove("Cm2"); p.Remove("CmLT"); p["Cm1"] = "0.9"; }
        if (problem == "missing-source") p.Remove("SwayModeSource");
        if (problem == "invalid-planes") p["SwayBucklingPlanes"] = "1;2";
        if (problem == "missing-ncr") context = Lengths(null, 3000, "Braced out of plane at both column ends");
        var issues = Validate(p);
        if (problem == "computed-and-explicit") Assert.IsTrue(issues.Any(i => i.Code == "SwayMomentFactorConflict" && i.Key == "Cm1") && issues.Any(i => i.Code == "AutomaticMomentFactorsConflict"));
        if (problem == "missing-source") Assert.IsTrue(issues.Any(i => i.Code == "SwayDeclarationPair"));
        if (problem == "invalid-planes") Assert.IsTrue(issues.Any(i => i.Code == "InvalidParameterValue" && i.Key == "SwayBucklingPlanes"));
        if (problem == "missing-ncr") Assert.AreEqual(0, issues.Count, Issues(p)); // the effective length lives in the model context
        var r = Frame(model, "ULS-A", p, ntc, context);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        string detail = problem == "computed-and-explicit" ? "Choose computed or explicit moment factors, not both."
            : problem == "missing-source" ? "MissingSteelMemberParameter: SwayModeSource" : problem == "invalid-planes" ? "InvalidSwayBucklingPlanes" : null!;
        if (detail != null) Assert.IsTrue(r.Diagnostics.Any(d => d.Code == "SteelMember.InputDetail" && d.Message.Contains(detail)), Diagnostics(r));
        else
        {
            // No silent nonsway default: neither an explicit Ncr1 nor a declared length, the library reports the missing value.
            StringAssert.Contains(Expression(r, "MemberStability.MissingInputs"), "Ncr1");
            StringAssert.Contains(Expression(r, "Member.Sway.Plane1.Ncr"), "Missing");
        }
    }

    [TestMethod]
    public void SchemaDeclaresTheSwayVocabularyOnlyForBeamColumns()
    {
        var model = SteelBeamColumnFrame.Create(); var p = FrameOptions(model, "ULS-A", false);
        Assert.AreEqual(0, Validate(p).Count, Issues(p));
        CollectionAssert.AreEqual(new[] { "1", "2", "1,2" }, SteelParameterSchemas.Member.Parameters.Single(x => x.Key == "SwayBucklingPlanes").Choices.ToArray());
        Sway(p, "1"); p.Remove("Cm1"); Assert.AreEqual(0, Validate(p).Count, Issues(p));
        p.Remove("Cm2"); Assert.IsTrue(Validate(p).Any(i => i.Code == "ExplicitMomentFactors")); p["Cm2"] = "1";
        p["SwayBucklingPlanes"] = "1,2"; Assert.IsTrue(Validate(p).Any(i => i.Code == "SwayMomentFactorConflict" && i.Key == "Cm2"));
        p.Remove("Cm2"); Assert.AreEqual(0, Validate(p).Count, Issues(p));
        p.Remove("CmLT"); Assert.IsTrue(Validate(p).Any(i => i.Code == "ExplicitMomentFactors"));
        var compression = new Dictionary<string, string> {
            ["AnalysisMode"] = "Compression", ["Unperforated"] = "true", ["MemberFingerprint"] = "prepared-snapshot",
            ["LoadPatternReference"] = "constant concentric compression", ["ParameterSet"] = "EC3 declared coefficients",
            ["CompressionCurve1"] = "B", ["CompressionCurve2"] = "C", ["TorsionalCurve"] = "B",
            ["TorsionalExclusionSource"] = "independently restrained", ["FlexuralTorsionalExclusionSource"] = "independent assessment",
            ["SwayBucklingPlanes"] = "1", ["SwayModeSource"] = SwaySource };
        Assert.IsTrue(Validate(compression).Any(i => i.Code == "ParameterOnAnotherMode" && i.Key == "SwayBucklingPlanes"));
        Assert.IsTrue(SteelParameterSchemas.Member.ExternalRequirements.Any(r => r.Contains("SwayBucklingPlanes")));
    }

    // Segment route: member S1 of SteelMemberPipelineTest, one element 0..2000 mm, H300x12/150x18 S355, N=-100kN, uniform M1=50kNm,
    // section axes X (principal 1, lateral), Y (principal 2, in-plane), Z (member axis); torsion susceptible, CriticalValues=UniformFork.
    private static (GPC.Model.Models.Model Model, BeamCheckPlanRequest Plan) Segment(MemberDesignContext context)
    {
        var (model, plan) = SteelMemberPipelineTest.Fixture();
        foreach (var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250], plan.Results[0]))
        { sample.ResultBeamForces.N = -1e5; sample.ResultBeamForces.M1 = 5e7; sample.ResultBeamForces.V2 = 0; }
        plan.MemberChecks[0].Context = context; return (model, plan);
    }
    private static MemberDesignContext Forks(IEnumerable<MemberRestraint>? restraints = null, double? l1 = null, string? source = null)
        => new(restraints, effectiveLength1: l1, lengthSource: source, assumptions: "Declared fork ends; plane 1 sway", globalStateConfirmed: true);
    private static Dictionary<string, string> SegmentOptions(GPC.Model.Models.Model model, BeamCheckPlanRequest plan)
    {
        var p = SteelMemberPipelineTest.Options(model, plan);
        foreach (var k in new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }) p.Remove(k);
        p["AnalysisMode"] = "BeamColumn"; p["CriticalValues"] = "UniformFork";
        p["CriticalValuesSource"] = "Explicit member-end forks (lateral and twist), free warping, loads through the shear centre";
        p["CompressionCurve1"] = "B"; p["CompressionCurve2"] = "C"; p["McrBasis"] = "UniformMoment"; p["TorsionSusceptible"] = "true";
        p["Cm2"] = p["CmLT"] = "1"; p["MomentFactorSource"] = "Explicit conservative Cm2=CmLT=1";
        Sway(p, "1"); return p;
    }
    private static CheckResult Run(GPC.Model.Models.Model model, BeamCheckPlanRequest plan, Dictionary<string, string> p, bool ntc)
        => new Service().Verify(model, SteelMemberPipelineTest.Request(plan, p, ntc ? new StandardNTC2018Steel() : new StandardEN1993p11())).Jobs[0].Results.Single();

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ForkSegmentsKeepNcr2NcrTAndMcrWhileTheSwayPlaneTakesItsExplicitNcr1(bool ntc)
    {
        var (model, plan) = Segment(Forks()); var p = SegmentOptions(model, plan); double ncr1 = Euler(I1, 4000);
        Ncr(p, 1, ncr1, "Sway buckling analysis: Leff=2L=4000 mm");
        Assert.AreEqual(0, Validate(p).Count, Issues(p));
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(.9, Value(r, "MemberStability.Cm1"), 1e-15); Assert.AreEqual(1, Value(r, "MemberStability.Cm2")); Assert.AreEqual(1, Value(r, "MemberStability.CmLT"));
        Assert.AreEqual(ncr1, Value(r, "MemberStability.Ncr1"), ncr1 * 1e-12);
        Assert.IsFalse(r.Details.Trace.Any(t => t.Key == "MemberStability.Ncr1.EffectiveLength" || t.Key == "Member.Segment.1.InPlaneLength"));
        Assert.AreEqual(2000, Value(r, "MemberStability.Ncr2.EffectiveLength"));
        Assert.IsTrue(r.Details.Trace.Any(t => t.Key == "Member.External.Mcr") && r.Details.Trace.Any(t => t.Key == "MemberStability.NcrT"));
        // By hand, Table B.2 / C4.2.V classes 1-2, torsion susceptible, lambda2>=.4: kyy=Cm1(1+min(l1-.2,.8)n1),
        // kzz=Cm2(1+min(2l2-.6,1.4)n2), kzy=max(1-.1 l2 n2/(CmLT-.25), 1-.1 n2/(CmLT-.25)).
        double g = ntc ? 1.05 : 1, nrk = A * Fy, l1 = Math.Sqrt(nrk / ncr1), l2 = Math.Sqrt(nrk / Euler(I2, 2000));
        double n1 = 1e5 * g / (Chi(l1, .34) * nrk), n2 = 1e5 * g / (Chi(l2, .49) * nrk);
        Assert.IsTrue(l2 >= .4);
        Assert.AreEqual(.9 * (1 + Math.Min(l1 - .2, .8) * n1), Value(r, "MemberStability.K11"), 1e-12);
        Assert.AreEqual(1 + Math.Min(2 * l2 - .6, 1.4) * n2, Value(r, "MemberStability.K22"), 1e-9);
        Assert.AreEqual(Math.Max(1 - .1 * l2 * n2 / .75, 1 - .1 * n2 / .75), Value(r, "MemberStability.K21"), 1e-9);
        // Interaction1=n1+kyy m1 (M2=0); chiLT of the automatic fork Mcr is read back from the trace.
        double m1 = 5e7 * g / (Value(r, "MemberStability.ChiLT") * Wpl1 * Fy);
        Assert.AreEqual(n1 + Value(r, "MemberStability.K11") * m1, Value(r, "MemberStability.Interaction1"), 1e-9);
        // Without the sway declaration the explicit Ncr1 conflicts with the automatic critical values.
        p.Remove("SwayBucklingPlanes"); p.Remove("SwayModeSource"); p["Cm1"] = "1";
        Assert.IsTrue(Validate(p).Any(i => i.Code == "AutomaticCriticalConflict" && i.Key == "Ncr1.Value"));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Run(model, plan, p, ntc).Outcome);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void DeclaredSwayEffectiveLengthLetsTheMemberEndTranslateInPlaneOne(bool ntc)
    {
        // Top end: lateral translation and twist held (fork), in-plane translation explicitly free; Model EffectiveLength1=4000.
        var top = new MemberRestraint(2000, CoordinateSystem.Global, new bool?[] { true, false, null, null, null, true }, "Purlin and fly brace at the top");
        var (model, plan) = Segment(Forks(new[] { top }, 4000, "Sway effective length 2L in plane 1 (test portal)"));
        var p = SegmentOptions(model, plan);
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.Satisfied, r.Outcome, Diagnostics(r));
        Assert.AreEqual(4000, Value(r, "MemberStability.Ncr1.EffectiveLength")); Assert.AreEqual(4000, Value(r, "Member.Sway.Plane1.EffectiveLength"));
        StringAssert.Contains(Expression(r, "Member.Sway.Plane1.EffectiveLength"), "Sway effective length 2L");
        // The same sway Ncr1 given explicitly gives the same verdict.
        var (other, otherPlan) = Segment(Forks()); var q = SegmentOptions(other, otherPlan);
        Ncr(q, 1, Euler(I1, 4000), "Sway buckling analysis: Leff=2L=4000 mm");
        Assert.AreEqual(Run(other, otherPlan, q, ntc).Utilization!.Value, r.Utilization!.Value, 1e-9);
        // Without the declaration the free in-plane end conflicts with the nonsway fork idealization of Ncr1.
        p.Remove("SwayBucklingPlanes"); p.Remove("SwayModeSource"); p["Cm1"] = "1";
        Assert.IsTrue(Run(model, plan, p, ntc).Diagnostics.Any(d => d.Code == "SteelMember.Restraint.EndConflictsWithFork"));
    }

    [DataTestMethod] [DataRow(false, "1")] [DataRow(true, "1")] [DataRow(false, "2")] [DataRow(true, "2")] [DataRow(false, "1,2")] [DataRow(true, "1,2")]
    public void NonswayForkSegmentValuesAreRefusedForADeclaredSwayPlane(bool ntc, string planes)
    {
        // CriticalValues idealizes every in-plane span and fork segment with both end translations fixed (nonsway):
        // plane 1 needs its explicit sway Ncr1 or a declared EffectiveLength1, plane 2 is refused (forks fix the lateral translation).
        var (model, plan) = Segment(Forks()); var p = SegmentOptions(model, plan); p["SwayBucklingPlanes"] = planes;
        if (planes != "1") p.Remove("Cm2");
        if (planes == "2") p["Cm1"] = "1";
        var r = Run(model, plan, p, ntc);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Code == (planes == "1" ? "SteelMember.Sway.NonswayNcr1" : "SteelMember.Sway.NonswayNcr2")), Diagnostics(r));
        if (planes == "1") Assert.AreEqual(0, Validate(p).Count, Issues(p)); // Ncr1 or EffectiveLength1: execution check
        else Assert.IsTrue(Validate(p).Any(i => i.Code == "SwayForkSegmentsConflict" && i.Key == "CriticalValues"));
    }
}
