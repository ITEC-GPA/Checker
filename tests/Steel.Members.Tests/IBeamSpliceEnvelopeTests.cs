using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceEnvelopeTests
{
    private static IBeamSplicePlateInput Plate(double n = -75000, double v = -75000, double m = -100e6,
        bool ntc = false, bool dense = false, string source = "Explicit concomitant ULS")
        => IBeamSplicePlateTests.Input(ntc, forces: IBeamSpliceForceTests.Input(actions: new(n, v, 0, 0, 0, m),
            web: dense ? new(3, 3, 85, 120, 70) : null,
            flange: dense ? new(5, 2, 80, 120, 60) : null), source: source);
    private static IBeamSpliceUltimateCase U(string id, IBeamSplicePlateInput? p = null) => new(id, p ?? Plate());
    private static IBeamSpliceBoltSlipInput Slip(IBeamSplicePlateInput p,
        BoltSlipLimitState state = BoltSlipLimitState.Ultimate, IBeamSpliceForceInput? forces = null,
        double flangeMu = .5, double webMu = .5, double gammaU = 1.25, double gammaS = 1.1,
        BoltTighteningControl tightening = BoltTighteningControl.Controlled,
        ConnectionDesignCode? code = null, IBeamSpliceBoltSet? bolts = null, string? source = "Explicit separate slip state")
    {
        code ??= p.Policy.Code;
        return new(code.IsNtc ? BoltSlipPolicy.Ntc2018(code) : BoltSlipPolicy.Eurocode3(code, gammaU, gammaS),
            forces ?? p.Forces, bolts ?? p.Bolts, state, flangeMu, webMu, tightening,
            "Actual qualified assemblies", "Actual qualified surfaces", source);
    }
    private static IBeamSpliceSlipCase S(string id, IBeamSplicePlateInput p, string? linked = null,
        BoltSlipLimitState state = BoltSlipLimitState.Serviceability, IBeamSpliceForceInput? forces = null)
        => new(id, Slip(p, state, forces), linked);
    private static IBeamSplicePlateInput Copy(IBeamSplicePlateInput p, IBeamSplicePlatePolicy? policy = null,
        IBeamSpliceForceInput? forces = null, IBeamSpliceBoltSet? bolts = null, ConnectionPlate? flange = null,
        ConnectionPlate? web = null, double? flangeEnd = null, double? webEnd = null, double? gap = null,
        double? flangeFy = null, double? flangeFu = null, bool? exposed = null, string? source = null)
        => new(policy ?? p.Policy, forces ?? p.Forces, bolts ?? p.Bolts, flange ?? p.FlangeCover, web ?? p.WebCover,
            flangeEnd ?? p.FlangeCoverEnd, webEnd ?? p.WebCoverEnd, gap ?? p.ButtGap,
            flangeFy ?? p.BeamFlangeFy, flangeFu ?? p.BeamFlangeFu, exposed ?? p.Exposed, source ?? p.UltimateCombinationSource);
    private static IBeamSpliceForceInput Forces(IBeamSplicePlateInput p, PlanarConnectionActions? actions = null,
        ConnectionRolledISection? section = null, double? inertia = null,
        SpliceBoltLayout? web = null, SpliceBoltLayout? flange = null)
        => new(section ?? p.Forces.Section, inertia ?? p.Forces.StrongAxisSecondMoment, actions ?? p.Forces.Actions,
            web ?? p.Forces.WebBolts, flange ?? p.Forces.FlangeBolts);
    private static double Next(double value) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * 1e-10);
    private static void Blocked(IBeamSpliceEnvelopeResult r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        Assert.AreEqual(0, r.ComponentAssessment.Checks.Count);
        Assert.AreEqual(0, r.CheckSources.Count);
        Assert.IsNull(r.ComponentAssessment.Utilization); Assert.IsNull(r.ComponentAssessment.GoverningCheck);
        Assert.IsNull(r.GoverningSource); Assert.IsNull(r.GoverningSourceCheck); Assert.IsNull(r.GoverningCaseId);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Any(d => d.Contains(reason)), string.Join("; ", r.ComponentAssessment.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    private static ConnectionCheck Bearing(IBeamSpliceUltimateCaseResult c)
        => c.Components.Bearing.Checks.Single(k => k.Id == "Left.Web.Member.Bolt1.BearingInteraction");

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void EveryWholeUlsCaseIsExecutedAndTheWeakerNonPrimaryCaseGoverns(bool reverse)
    {
        var low = U("U-low"); var high = U("U-P398", Plate(-150000, -150000, -200e6));
        var input = new IBeamSpliceEnvelopeInput(reverse ? new[] { high, low } : new[] { low, high });
        var r = IBeamSpliceEnvelope.Verify(input);
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        Assert.IsTrue(r.HasFailedComponent); Assert.AreEqual("U-P398", r.GoverningCaseId);
        Equal(1.1865834167227851, r.ComponentAssessment.Utilization!.Value);
        Equal(.5932917083613926, Bearing(r.UltimateCases.Single(c => c.Input.Id == "U-low")).Utilization);
        Equal(1.1865834167227851, Bearing(r.UltimateCases.Single(c => c.Input.Id == "U-P398")).Utilization);
        foreach (var c in r.UltimateCases)
        {
            Assert.AreSame(c.Input.Plates, c.Components.ComponentAssessment.Input.Plates);
            Assert.AreSame(c.Input.Plates, c.Components.Bearing.Input);
            Assert.AreSame(c.Input.Plates, c.Components.Flanges.Input);
            Assert.AreSame(c.Input.Plates, c.Components.WebSections.Input);
            Assert.AreSame(c.Input.Plates, c.Components.WebLongitudinal.Input);
            Assert.AreSame(c.Input.Plates.Forces, c.Components.BoltStrength.Input.Forces);
            Assert.IsNull(c.FlangeNetPlastic);
        }
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void SeparateAxialAndShearMomentCasesNeverBecomeASyntheticCoordinatewiseMaximum()
    {
        var a = U("axial", Plate(-300000, 0, 0)); var b = U("shear-moment", Plate(0, -150000, -200e6));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { a, b }));
        Equal(.15261731564684067, Bearing(r.UltimateCases[0]).Utilization);
        Equal(1.1102747588993649, Bearing(r.UltimateCases[1]).Utilization);
        var bearingSources = r.CheckSources.Where(c => c.Component == "Bearing"
            && c.SourceCheck.Id.Contains(".Web.Member.") && c.SourceCheck.Id.EndsWith("BearingInteraction")).ToArray();
        Equal(1.1102747588993649, bearingSources.Max(c => c.SourceCheck.Utilization));
        Assert.IsFalse(bearingSources.Any(c => Math.Abs(c.SourceCheck.Utilization - 1.2628920745462056) < 1e-9),
            "1.262892... belongs to the forbidden synthetic N=-300kN,V=-150kN,M=-200kNm case.");
        Assert.AreSame(a.Plates.Forces.Actions, r.UltimateCases[0].Components.Bearing.Input.Forces.Actions);
        Assert.AreSame(b.Plates.Forces.Actions, r.UltimateCases[1].Components.Bearing.Input.Forces.Actions);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CategoryCChecksNetOncePerUlsAndRetainsTheOriginalGoverningLeaf(bool ntc)
    {
        var low = U("low", Plate(100000, 0, 0, ntc, true));
        var high = U("high", Plate(1600000, 0, 0, ntc, true));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { low, high }, BoltSlipLimitState.Ultimate,
            new[] { S("slip-low", low.Plates, "low", BoltSlipLimitState.Ultimate), S("slip-high", high.Plates, "high", BoltSlipLimitState.Ultimate) }));
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        Assert.AreEqual("high", r.GoverningCaseId); Assert.AreEqual("FlangeNetPlastic", r.GoverningSource.Component);
        var net = r.UltimateCases[1].FlangeNetPlastic;
        var original = net.Checks.Single(c => c.Id == "TopFlange.Cover.NetPlasticTension");
        Assert.AreSame(original, r.GoverningSourceCheck);
        Equal(459602.339181286574, original.Demand);
        Equal(448800 / (ntc ? 1.05 : 1), original.Resistance);
        Equal(original.Utilization, r.ComponentAssessment.Utilization!.Value);
        Assert.AreEqual(2, r.UltimateCases.Count); Assert.AreEqual(2, r.UltimateSlipCases.Count);
        Assert.AreEqual(8, r.CheckSources.Count(s => s.Component == "FlangeNetPlastic"));
        foreach (var c in r.UltimateCases)
        {
            Assert.AreEqual(ConnectionStatus.Passed, c.Components.ComponentAssessment.Status);
            Assert.AreEqual(0, c.Components.FlangeNetPlasticCases.Count);
            Assert.AreEqual(0, c.Components.SlipCases.Count);
            Assert.AreSame(c.Input.Plates, c.FlangeNetPlastic.Input);
        }
        Assert.IsTrue(r.UltimateSlipCases.All(c => c.Status == ConnectionStatus.Passed));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE"));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M"));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void EveryAggregateCheckMapsToAnOriginalLeafWithoutCountingSummaryAndChildrenTwice()
    {
        var p = Plate(100000, 0, 0, dense: true); var u = U("one", p);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { u }, BoltSlipLimitState.Ultimate,
            new[] { S("one-slip", p, "one", BoltSlipLimitState.Ultimate), S("one-SLS", p) }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        var c = r.UltimateCases.Single();
        int expected = c.Components.BoltStrength.Checks.Count + c.Components.Bearing.Checks.Count
            + c.Components.Flanges.Checks.Count + c.Components.WebSections.Checks.Count
            + c.Components.WebLongitudinal.Checks.Count + c.FlangeNetPlastic.Checks.Count
            + r.SlipCases.Sum(s => s.Assessment.Checks.Count);
        Assert.AreEqual(expected, r.ComponentAssessment.Checks.Count);
        Assert.AreEqual(expected, r.CheckSources.Count);
        Assert.AreEqual(expected, r.CheckSources.Select(s => s.AggregateCheckId).Distinct(StringComparer.Ordinal).Count());
        foreach (var source in r.CheckSources)
        {
            var copy = r.ComponentAssessment.Checks.Single(k => k.Id == source.AggregateCheckId);
            Equal(source.SourceCheck.Demand, copy.Demand); Equal(source.SourceCheck.Resistance, copy.Resistance);
            Assert.AreEqual(source.SourceCheck.Unit, copy.Unit); Assert.AreEqual(source.SourceCheck.Reference, copy.Reference);
            IReadOnlyList<ConnectionCheck> leaf = source.Component switch
            {
                "BoltStrength" => c.Components.BoltStrength.Checks, "Bearing" => c.Components.Bearing.Checks,
                "Flanges" => c.Components.Flanges.Checks, "WebSections" => c.Components.WebSections.Checks,
                "WebLongitudinal" => c.Components.WebLongitudinal.Checks, "FlangeNetPlastic" => c.FlangeNetPlastic.Checks,
                _ => r.SlipCases.Single(s => s.Input.Id == source.CaseId).Assessment.Checks
            };
            Assert.IsTrue(leaf.Any(k => ReferenceEquals(k, source.SourceCheck)));
        }
        Assert.AreEqual(r.ComponentAssessment.GoverningCheck.Id, r.GoverningSource.AggregateCheckId);
        Assert.AreSame(r.SlipCases[0], r.UltimateSlipCases.Single());
        Assert.AreSame(r.SlipCases[1], r.ServiceabilityCases.Single());
    }

    [TestMethod]
    public void AnExplicitSlsCanGovernWithoutGeneratingAnotherUlsOrAnyCategoryCCheck()
    {
        var p = Plate(); var s = S("P398-SLS", p, forces: IBeamSpliceForceTests.Input(true));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("ULS", p) }, BoltSlipLimitState.Serviceability, new[] { s }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Equal(.730956089233456, r.ComponentAssessment.Utilization!.Value);
        Assert.AreEqual("P398-SLS", r.GoverningCaseId); Assert.IsTrue(r.GoverningSource.IsServiceability);
        Assert.IsNull(r.GoverningSource.LinkedUltimateCaseId);
        Assert.AreEqual(1, r.UltimateCases.Count); Assert.IsNull(r.UltimateCases[0].FlangeNetPlastic);
        Assert.AreEqual(-75000d, r.UltimateCases[0].Components.Bearing.Input.Forces.Actions.Fx);
        Assert.AreEqual(-100000d, r.ServiceabilityCases[0].Assessment.Input.Forces.Actions.Fx);
        Assert.IsFalse(r.MissingWholeJointChecks.Contains("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M"));
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void RequiredLimitStatesAreNotOmittedOrReplacedByTheOtherState(int variant)
    {
        var p = Plate(100000, 0, 0, dense: true);
        var required = variant % 2 == 0 ? BoltSlipLimitState.Serviceability : BoltSlipLimitState.Ultimate;
        var slips = variant < 2 ? Array.Empty<IBeamSpliceSlipCase>()
            : variant == 2 ? new[] { S("U-slip", p, "U", BoltSlipLimitState.Ultimate) } : new[] { S("S-slip", p) };
        var input = new IBeamSpliceEnvelopeInput(new[] { U("U", p) }, required, slips);
        var r = IBeamSpliceEnvelope.Verify(input);
        Blocked(r, required == BoltSlipLimitState.Serviceability ? "REQUIRED_SERVICEABILITY" : "CATEGORY_C_BOUND_ULTIMATE");
        Assert.AreEqual(input.RequiresCategoryC, r.UltimateCases[0].FlangeNetPlastic != null);
        Assert.AreEqual(input.RequiresCategoryC, r.MissingWholeJointChecks.Contains("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M"));
    }

    [TestMethod]
    public void UnrequestedUltimateSlipStillActivatesCategoryCForEverySuppliedUlsOnNsBinding()
    {
        var p = Plate(100000, 0, 0, dense: true);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("A", p), U("B", p) },
            slipCases: new[] { S("A-slip", p, "A", BoltSlipLimitState.Ultimate) }));
        Blocked(r, "CATEGORY_C_BOUND_ULTIMATE_SLIP_MISSING");
        Assert.IsTrue(r.UltimateCases.All(c => c.FlangeNetPlastic != null));
        Assert.AreEqual(ConnectionStatus.Passed, r.UltimateCases[0].Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.UltimateCases[1].Status);
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE"));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M"));
    }

    [DataTestMethod] [DataRow(null)] [DataRow("missing")] [DataRow("uls")] [DataRow("")]
    public void UltimateSlipBindingIsExplicitAndOrdinal(string? linked)
    {
        var p = Plate();
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("ULS", p) }, slipCases: new[] { S("slip", p, linked, BoltSlipLimitState.Ultimate) }));
        Blocked(r, "ULTIMATE_SLIP_CASE_BINDING_MISSING_OR_UNKNOWN");
        Assert.AreEqual(1, r.UltimateSlipCases.Count); Assert.AreSame(p.Forces, r.UltimateSlipCases[0].Assessment.Input.Forces);
    }

    [TestMethod]
    public void TwoSlipIdsCannotSilentlyDuplicateTheSameUltimateBinding()
    {
        var p = Plate();
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] {
            S("slip1", p, "U", BoltSlipLimitState.Ultimate), S("slip2", p, "U", BoltSlipLimitState.Ultimate) }));
        Blocked(r, "CATEGORY_C_DUPLICATE_ULTIMATE_SLIP_BINDING");
        Assert.AreEqual(2, r.UltimateSlipCases.Count); Assert.IsNotNull(r.UltimateCases.Single().FlangeNetPlastic);
    }

    [DataTestMethod] [DataRow("U")] [DataRow("")]
    public void ServiceabilityCasesDoNotBorrowUltimateIdentity(string linked)
    {
        var p = Plate();
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] { S("S", p, linked) }));
        Blocked(r, "SERVICEABILITY_CASE_MUST_NOT_BIND_TO_ULTIMATE");
        Assert.IsNull(r.UltimateCases.Single().FlangeNetPlastic);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)] [DataRow(8)]
    public void EveryActionAndEveryApplicationCoordinateMustMatchTheBoundUltimateCaseExactly(int coordinate)
    {
        var p = Plate(); var a = p.Forces.Actions;
        var values = new[] { a.Fx, a.Fy, a.Fz, a.Mx, a.My, a.Mz, a.ApplicationX, a.ApplicationY, a.ApplicationZ };
        values[coordinate] += coordinate == 5 ? .000001 : .000000001;
        var altered = new PlanarConnectionActions(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        var slip = S("slip", p, "U", BoltSlipLimitState.Ultimate, Forces(p, altered));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] { slip }));
        Blocked(r, "CONCOMITANT_ACTIONS_OR_APPLICATION_POINT_MISMATCH");
        Assert.AreSame(altered, r.UltimateSlipCases.Single().Assessment.Input.Forces.Actions);
        Assert.AreSame(a, r.UltimateCases.Single().Components.Bearing.Input.Forces.Actions);
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void UnsupportedOutOfPlaneUltimateActionsCannotDisappearWhileAnotherCaseFails(int component)
    {
        var high = Plate(-150000, -150000, -200e6); var p = Plate(); var a = p.Forces.Actions;
        var altered = new PlanarConnectionActions(a.Fx, a.Fy, component == 0 ? 1e-9 : 0,
            component == 1 ? 1e-9 : 0, component == 2 ? 1e-9 : 0, a.Mz, 0, 0, component == 3 ? 1e-9 : 0);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("failed", high), U("unsupported", Copy(p, forces: Forces(p, altered))) }));
        Blocked(r, "IN_PLANE_N_V_M_ONLY"); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed, r.UltimateCases[0].Components.Bearing.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.UltimateCases[1].Status);
        Assert.AreSame(altered, r.UltimateCases[1].Components.BoltStrength.Input.Forces.Actions);
    }

    [TestMethod]
    public void AConcomitantCaseAtAnActualInPlanePoleIsPreservedAndTransportedByTheExistingKernel()
    {
        var p = Plate(); double x = 12, y = -17;
        var a = new PlanarConnectionActions(-75000, -75000, 0, 0, 0, -100e6 - x * -75000 + y * -75000, x, y, 0);
        var moved = Copy(p, forces: Forces(p, a));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("actual-pole", moved) }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Equal(.5932917083613926, Bearing(r.UltimateCases[0]).Utilization);
        Assert.AreSame(a, r.UltimateCases[0].Components.Bearing.Input.Forces.Actions);
        Assert.AreEqual(x, a.ApplicationX); Assert.AreEqual(y, a.ApplicationY);
    }

    [TestMethod]
    public void EvenEquivalentResultantsAtDifferentPolesNeedAnExplicitConsistentBinding()
    {
        var p = Plate(); var a = p.Forces.Actions; double x = 10;
        var translated = new PlanarConnectionActions(a.Fx, a.Fy, 0, 0, 0, a.Mz - x * a.Fy, x);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] {
            S("slip", p, "U", BoltSlipLimitState.Ultimate, Forces(p, translated)) }));
        Blocked(r, "CONCOMITANT_ACTIONS_OR_APPLICATION_POINT_MISMATCH");
        Assert.AreEqual(ConnectionStatus.Passed, r.UltimateCases[0].Components.ComponentAssessment.Status);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)]
    [DataRow(8)] [DataRow(9)] [DataRow(10)] [DataRow(11)] [DataRow(12)] [DataRow(13)] [DataRow(14)] [DataRow(15)]
    [DataRow(16)] [DataRow(17)] [DataRow(18)] [DataRow(19)] [DataRow(20)] [DataRow(21)] [DataRow(22)] [DataRow(23)]
    [DataRow(24)] [DataRow(25)] [DataRow(26)] [DataRow(27)] [DataRow(28)] [DataRow(29)] [DataRow(30)] [DataRow(31)]
    [DataRow(32)] [DataRow(33)] [DataRow(34)] [DataRow(35)] [DataRow(36)] [DataRow(37)] [DataRow(38)] [DataRow(39)] [DataRow(40)]
    public void ChangedMaterialsDetailAndFactorsCannotBorrowAnotherUltimateCasesQualification(int variant)
    {
        var p = Plate(); var changed = Mutate(p, variant);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("anchor", p), U("changed", changed) }));
        Blocked(r, variant < 5 ? "ULTIMATE_CASE_PLATE_POLICY_MISMATCH" : "ULTIMATE_CASE_PHYSICAL_DETAIL_MISMATCH");
        Assert.AreSame(changed, r.UltimateCases[1].Components.Bearing.Input);
        Assert.IsFalse(r.UltimateCases[1].IsBound);
    }

    private static IBeamSplicePlateInput Mutate(IBeamSplicePlateInput p, int variant)
    {
        var code = p.Policy.Code; var f = p.Forces; var s = f.Section; var b = p.Bolts.FlangeBolt;
        if (variant <= 4)
        {
            if (variant == 4) return Copy(p, policy: IBeamSplicePlatePolicy.Ntc2018());
            return Copy(p, policy: IBeamSplicePlatePolicy.Eurocode3(ConnectionDesignCode.Eurocode3(
                variant == 0 ? Next(code.Plates.GammaM0) : code.Plates.GammaM0,
                variant == 1 ? Next(code.Plates.GammaM2) : code.Plates.GammaM2,
                variant == 2 ? Next(code.GammaM2Connection) : code.GammaM2Connection, "Changed explicit policy"),
                variant == 3 ? Next(p.Policy.GammaM1) : p.Policy.GammaM1));
        }
        if (variant <= 12)
            return Copy(p, forces: Forces(p, section: new ConnectionRolledISection(
                variant == 5 ? Next(s.Height) : s.Height, variant == 6 ? Next(s.Width) : s.Width,
                variant == 7 ? Next(s.WebThickness) : s.WebThickness, variant == 8 ? Next(s.FlangeThickness) : s.FlangeThickness,
                variant == 9 ? Next(s.RootRadius) : s.RootRadius, variant == 10 ? Next(s.Area) : s.Area,
                variant == 11 ? Next(s.Fy) : s.Fy, variant == 12 ? Next(s.Fu) : s.Fu)));
        if (variant == 13) return Copy(p, forces: Forces(p, inertia: Next(f.StrongAxisSecondMoment)));
        if (variant <= 18)
        {
            var g = f.WebBolts;
            return Copy(p, forces: Forces(p, web: new(variant == 14 ? g.LongitudinalRows + 1 : g.LongitudinalRows,
                variant == 15 ? g.TransverseRows + 1 : g.TransverseRows,
                variant == 16 ? Next(g.LongitudinalPitch) : g.LongitudinalPitch,
                variant == 17 ? Next(g.TransversePitch) : g.TransversePitch,
                variant == 18 ? Next(g.FirstBoltDistance) : g.FirstBoltDistance)));
        }
        if (variant == 19)
        {
            var g = f.FlangeBolts;
            return Copy(p, forces: Forces(p, flange: new(g.LongitudinalRows, g.TransverseRows,
                Next(g.LongitudinalPitch), g.TransversePitch, g.FirstBoltDistance)));
        }
        if (variant <= 26)
        {
            var changed = new ConnectionBolt(variant == 20 ? Next(b.Diameter) : b.Diameter,
                variant == 21 ? Next(b.HoleDiameter) : b.HoleDiameter, variant == 22 ? Next(b.TensileArea) : b.TensileArea,
                variant == 23 ? ConnectionBoltGrade.Class10_9 : b.Grade, variant == 24 ? !b.ThreadedShearPlanes : b.ThreadedShearPlanes);
            return Copy(p, bolts: new(changed, p.Bolts.WebBolt,
                variant == 25 ? Next(p.Bolts.FlangeHardwareEnvelopeDiameter) : p.Bolts.FlangeHardwareEnvelopeDiameter,
                variant == 26 ? Next(p.Bolts.WebHardwareEnvelopeDiameter) : p.Bolts.WebHardwareEnvelopeDiameter,
                p.Bolts.HoleClearanceServiceabilitySource));
        }
        if (variant <= 34)
        {
            var q = variant <= 30 ? p.FlangeCover : p.WebCover; int field = (variant - 27) % 4;
            var changed = new ConnectionPlate(field == 0 ? Next(q.Width) : q.Width,
                field == 1 ? Next(q.Thickness) : q.Thickness, field == 2 ? Next(q.Fy) : q.Fy, field == 3 ? Next(q.Fu) : q.Fu);
            return variant <= 30 ? Copy(p, flange: changed) : Copy(p, web: changed);
        }
        return variant switch
        {
            35 => Copy(p, flangeEnd: Next(p.FlangeCoverEnd)), 36 => Copy(p, webEnd: Next(p.WebCoverEnd)),
            37 => Copy(p, gap: Next(p.ButtGap)), 38 => Copy(p, flangeFy: Next(p.BeamFlangeFy)),
            39 => Copy(p, flangeFu: Next(p.BeamFlangeFu)), _ => Copy(p, exposed: !p.Exposed)
        };
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void SlipFactorsSurfacesAndTighteningBelongToTheSamePhysicalDetail(int variant)
    {
        var p = Plate(); var changed = Slip(p, BoltSlipLimitState.Serviceability,
            gammaU: variant == 0 ? Next(1.25) : 1.25, gammaS: variant == 1 ? Next(1.1) : 1.1,
            flangeMu: variant == 2 ? Next(.5) : .5, webMu: variant == 3 ? Next(.5) : .5,
            tightening: variant == 4 ? BoltTighteningControl.Uncontrolled : BoltTighteningControl.Controlled);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] {
            S("S1", p), new IBeamSpliceSlipCase("S2", changed) }));
        Blocked(r, "SLIP_POLICY_SURFACE_OR_TIGHTENING_MISMATCH");
        Assert.AreSame(changed, r.ServiceabilityCases[1].Assessment.Input);
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)]
    public void SlipCasesCannotUseDifferentGeometryHardwareOrNationalCode(int variant)
    {
        var p = Plate(); var s = Slip(p, BoltSlipLimitState.Serviceability,
            forces: variant == 0 ? Forces(p, inertia: Next(p.Forces.StrongAxisSecondMoment)) : null,
            bolts: variant == 1 ? new(p.Bolts.FlangeBolt, p.Bolts.WebBolt, Next(37), 37) : null,
            code: variant == 2 ? ConnectionDesignCode.Ntc2018() : null);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] { new IBeamSpliceSlipCase("S", s) }));
        Blocked(r, variant == 2 ? "SLIP_CASE_DESIGN_CODE_MISMATCH" : "SLIP_CASE_PHYSICAL_DETAIL_MISMATCH");
    }

    [TestMethod]
    public void FailedLeavesRemainVisibleInsideAnUnsupportedUltimateSummary()
    {
        var p = IBeamSplicePlateTests.Input(forces: IBeamSpliceForceTests.Input(
            actions: new(-3000000, 0, 0, 0, 0, 0), web: new(2, 2, 85, 60, 70)), exposed: false);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("incomplete-failure", p) }));
        Blocked(r, "OUTSTAND_OUTSIDE_CLASS3");
        var c = r.UltimateCases.Single();
        Assert.AreEqual(ConnectionStatus.NotSupported, c.Components.ComponentAssessment.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, c.Components.WebLongitudinal.Status);
        Assert.AreEqual(ConnectionStatus.Failed, c.Components.Flanges.Status);
        Assert.IsTrue(c.Components.Flanges.Checks.Any(k => !k.Passed));
        Assert.IsTrue(c.HasFailedComponent); Assert.IsTrue(r.HasFailedComponent);
    }

    [TestMethod]
    public void BindingMismatchDoesNotDiscardARealFailureInTheMismatchedCase()
    {
        var low = Plate(); var high = Mutate(Plate(-150000, -150000, -200e6), 3);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("low", low), U("high-other-policy", high) }));
        Blocked(r, "ULTIMATE_CASE_PLATE_POLICY_MISMATCH");
        Assert.AreEqual(ConnectionStatus.Failed, r.UltimateCases[1].Components.Bearing.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.UltimateCases[1].Status);
        Assert.IsTrue(r.UltimateCases[1].HasFailedComponent); Assert.IsTrue(r.HasFailedComponent);
    }

    [TestMethod]
    public void FailedSlipRemainsAvailableWhenItsBindingOrAnotherCaseIsUnsupported()
    {
        var p = Plate(); var failed = Slip(p, BoltSlipLimitState.Serviceability, flangeMu: .1, webMu: .1);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("U", p) }, slipCases: new[] {
            S("normal", p), new IBeamSpliceSlipCase("failed-other-surface", failed) }));
        Blocked(r, "SLIP_POLICY_SURFACE_OR_TIGHTENING_MISMATCH");
        Assert.AreEqual(ConnectionStatus.Failed, r.ServiceabilityCases[1].Assessment.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ServiceabilityCases[1].Status);
        Assert.IsTrue(r.HasFailedComponent);
    }

    [TestMethod]
    public void MissingSourceInAnotherCaseDoesNotEraseAnEarlierFailure()
    {
        var high = U("failed", Plate(-150000, -150000, -200e6)); var empty = U("missing-source", Plate(source: " "));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { high, empty }));
        Blocked(r, "COMBINATION_SOURCE_REQUIRED");
        Assert.IsTrue(r.HasFailedComponent); Assert.AreEqual(ConnectionStatus.Failed, r.UltimateCases[0].Status);
    }

    [TestMethod]
    public void EqualIndependentSnapshotsAndDifferentSourceLabelsAreAcceptedWithoutMergingCases()
    {
        var a = U("ULS", Plate(source: "Source A")); var b = U("uls", Plate(source: "Source B"));
        var r = IBeamSpliceEnvelope.Verify(new(new[] { a, b }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(2, r.UltimateCases.Count);
        Assert.AreNotSame(a.Plates, b.Plates); Assert.AreNotSame(r.UltimateCases[0].Components, r.UltimateCases[1].Components);
        Assert.AreSame(a, r.UltimateCases[0].Input); Assert.AreSame(b, r.UltimateCases[1].Input);
        Assert.AreEqual("ULS", r.GoverningCaseId, "Identical check utilization ties keep the supplied case order.");
    }

    [TestMethod]
    public void ConsumerCanReportOriginalCasesAndKnownFailuresWithoutParsingCheckIdentifiers()
    {
        var p = Plate(-150000, -150000, -200e6);
        var r = IBeamSpliceEnvelope.Verify(new(new[] { U("revision/A:ULS.1", p), U("other", Plate(source: " ")) }));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        var failedCases = r.UltimateCases.Where(c => c.HasFailedComponent).Select(c => c.Input.Id).ToArray();
        CollectionAssert.AreEqual(new[] { "revision/A:ULS.1" }, failedCases);
        Assert.IsTrue(r.HasFailedComponent); Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("WEB_COVER_BLOCK_TEARING_UNDER_N_V_M"));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void LegacyPrimaryPathKeepsItsExistingScopeAndResultStructure()
    {
        var p = Plate(); var before = IBeamSpliceComponents.Verify(new(p));
        IBeamSpliceEnvelope.Verify(new(new[] { U("low", p), U("high", Plate(-150000, -150000, -200e6)) }));
        var after = IBeamSpliceComponents.Verify(new(p));
        Assert.AreEqual(ConnectionStatus.Passed, after.ComponentAssessment.Status);
        CollectionAssert.AreEqual(before.ComponentAssessment.Checks.Select(c => c.Id).ToArray(), after.ComponentAssessment.Checks.Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(before.ComponentAssessment.Checks.Select(c => c.Utilization).ToArray(), after.ComponentAssessment.Checks.Select(c => c.Utilization).ToArray());
        Assert.AreEqual(before.ComponentAssessment.Scope, after.ComponentAssessment.Scope);
        Assert.AreEqual(0, after.SlipCases.Count); Assert.AreEqual(0, after.FlangeNetPlasticCases.Count);
        Assert.AreSame(p, after.ComponentAssessment.Input.Plates);
    }

    [TestMethod]
    public void InputsResultsAndSourceMappingsAreImmutableSnapshots()
    {
        var p = Plate(); var ul = new List<IBeamSpliceUltimateCase> { U("U", p) };
        var sl = new List<IBeamSpliceSlipCase> { S("S", p) }; var input = new IBeamSpliceEnvelopeInput(ul, slipCases: sl);
        ul.Clear(); sl.Clear(); var r = IBeamSpliceEnvelope.Verify(input);
        Assert.AreEqual(1, input.UltimateCases.Count); Assert.AreEqual(1, input.SlipCases.Count);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<IBeamSpliceUltimateCase>)input.UltimateCases).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<IBeamSpliceSlipCase>)input.SlipCases).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<IBeamSpliceUltimateCaseResult>)r.UltimateCases).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<IBeamSpliceSlipCaseResult>)r.SlipCases).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<IBeamSpliceEnvelopeCheckSource>)r.CheckSources).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.UltimateCases[0].BindingDiagnostics).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.MissingWholeJointChecks).Clear());
    }

    [TestMethod]
    public void InvalidCaseContractsAndDuplicateIdsAreRejectedBeforeEvaluation()
    {
        var p = Plate();
        Assert.ThrowsException<ArgumentNullException>(() => IBeamSpliceEnvelope.Verify(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new IBeamSpliceEnvelopeInput(null!));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(Array.Empty<IBeamSpliceUltimateCase>()));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(new IBeamSpliceUltimateCase[] { null! }));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(new[] { U("U", p) }, slipCases: new IBeamSpliceSlipCase[] { null! }));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new IBeamSpliceEnvelopeInput(new[] { U("U", p) }, (BoltSlipLimitState)99));
        Assert.ThrowsException<ArgumentException>(() => U(" ", p));
        Assert.ThrowsException<ArgumentNullException>(() => new IBeamSpliceUltimateCase("U", null!));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceSlipCase(" ", Slip(p)));
        Assert.ThrowsException<ArgumentNullException>(() => new IBeamSpliceSlipCase("S", null!));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(new[] { U("U", p), U("U", p) }));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(new[] { U("U", p) }, slipCases: new[] { S("U", p) }));
        Assert.ThrowsException<ArgumentException>(() => new IBeamSpliceEnvelopeInput(new[] { U("U", p) }, slipCases: new[] { S("S", p), S("S", p) }));
    }
}
