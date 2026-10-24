using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceFlangeNetPlasticIntegrationTests
{
    private static IBeamSpliceForceInput DenseForces(double n = 100000, double v = 0, double m = 0)
        => IBeamSpliceForceTests.Input(actions: new(n, v, 0, 0, 0, m),
            web: new(3, 3, 85, 120, 70), flange: new(5, 2, 80, 120, 60));

    private static IBeamSplicePlateInput Dense(bool ntc = false, double n = 100000,
        double v = 0, double m = 0, string source = "Primary concomitant ULS")
        => Input(ntc, forces: DenseForces(n, v, m), source: source);

    private static IBeamSpliceBoltSlipInput Slip(IBeamSplicePlateInput p,
        BoltSlipLimitState state = BoltSlipLimitState.Ultimate,
        IBeamSpliceForceInput? forces = null, string? source = "Independent concomitant ULS",
        ConnectionDesignCode? code = null, IBeamSpliceBoltSet? bolts = null,
        string? assembly = "Qualified preloaded assemblies")
    {
        code ??= p.Policy.Code;
        return new(code.IsNtc ? BoltSlipPolicy.Ntc2018(code) : BoltSlipPolicy.Eurocode3(code, 1.25, 1.1),
            forces ?? p.Forces, bolts ?? p.Bolts, state, .5, .5, BoltTighteningControl.Controlled,
            assembly, "Qualified friction surfaces", source);
    }

    private static void Blocked(IBeamSpliceComponentResult r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.ComponentAssessment.Status);
        Assert.AreEqual(0, r.ComponentAssessment.Checks.Count);
        Assert.IsNull(r.ComponentAssessment.Utilization);
        Assert.IsTrue(r.ComponentAssessment.Diagnostics.Any(d => d.Contains(reason)),
            string.Join(";", r.ComponentAssessment.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void OrdinaryBearingSpliceDoesNotAutomaticallyBecomeCategoryC()
    {
        var r = IBeamSpliceComponents.Verify(new(Dense()));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(0, r.FlangeNetPlasticCases.Count);
        Assert.IsFalse(r.ComponentAssessment.Checks.Any(c => c.Id.StartsWith("FlangeNetPlastic.")));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CategoryBOnlyDoesNotActivateTheCategoryCFlangeRequirement(bool ntc)
    {
        var p = Dense(ntc);
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Serviceability,
            new[] { Slip(p, BoltSlipLimitState.Serviceability, DenseForces(50000), "Only SLS") }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(0, r.FlangeNetPlasticCases.Count);
        Assert.IsFalse(r.MissingWholeJointChecks.Contains("ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE"));
    }

    [TestMethod]
    public void RequiredUltimateActivatesPrimaryButCannotReplaceMissingUltimateSlipEvidence()
    {
        var p = Dense(); var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate));
        Blocked(r, "REQUIRED_SLIP_LIMIT_STATE_CASE_MISSING");
        Assert.AreEqual(1, r.FlangeNetPlasticCases.Count);
        Assert.AreSame(p, r.FlangeNetPlasticCases[0].Input);
        Assert.AreEqual(ConnectionStatus.Passed, r.FlangeNetPlasticCases[0].Status);
    }

    [TestMethod]
    public void AnyUltimateCaseActivatesCategoryCWithoutTheRequiredStateFlag()
    {
        var p = Dense(); var u = Slip(p);
        var r = IBeamSpliceComponents.Verify(new(p, slipCases: new[] { u }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(2, r.FlangeNetPlasticCases.Count);
        Assert.AreSame(p, r.FlangeNetPlasticCases[0].Input);
        Assert.AreSame(u.Forces, r.FlangeNetPlasticCases[1].Input.Forces);
    }

    [TestMethod]
    public void MixedCasesKeepOriginalOneBasedIndexesAndExcludeEverySlsCase()
    {
        var p = Dense();
        var s1 = Slip(p, BoltSlipLimitState.Serviceability, DenseForces(20000), "SLS1");
        var u2 = Slip(p, forces: DenseForces(200000), source: "ULS2");
        var s3 = Slip(p, BoltSlipLimitState.Serviceability, DenseForces(30000), "SLS3");
        var u4 = Slip(p, forces: DenseForces(-300000), source: "ULS4");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { s1, u2, s3, u4 }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(3, r.FlangeNetPlasticCases.Count);
        CollectionAssert.AreEqual(new[] { p.UltimateCombinationSource, "ULS2", "ULS4" },
            r.FlangeNetPlasticCases.Select(c => c.Input.UltimateCombinationSource).ToArray());
        Assert.AreSame(u2.Forces, r.FlangeNetPlasticCases[1].Input.Forces);
        Assert.AreSame(u4.Forces, r.FlangeNetPlasticCases[2].Input.Forces);
        Assert.AreEqual(4, r.ComponentAssessment.Checks.Count(c => c.Id.StartsWith("FlangeNetPlastic.PrimaryULS.")));
        Assert.AreEqual(4, r.ComponentAssessment.Checks.Count(c => c.Id.StartsWith("FlangeNetPlastic.SlipCase2.")));
        Assert.AreEqual(4, r.ComponentAssessment.Checks.Count(c => c.Id.StartsWith("FlangeNetPlastic.SlipCase4.")));
        Assert.IsFalse(r.ComponentAssessment.Checks.Any(c => c.Id.StartsWith("FlangeNetPlastic.SlipCase1.")
            || c.Id.StartsWith("FlangeNetPlastic.SlipCase3.")));
    }

    [TestMethod]
    public void DuplicateUltimateCasesRemainSeparateEvidenceWithSeparateAggregateIds()
    {
        var p = Dense(); var same = Slip(p);
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { same, same }));
        Assert.AreEqual(3, r.FlangeNetPlasticCases.Count);
        Assert.AreEqual(12, r.ComponentAssessment.Checks.Count(c => c.Id.StartsWith("FlangeNetPlastic.")));
        Assert.AreEqual(r.ComponentAssessment.Checks.Count,
            r.ComponentAssessment.Checks.Select(c => c.Id).Distinct().Count());
        Assert.AreEqual(same.ActionCombinationSource, r.FlangeNetPlasticCases[1].Input.UltimateCombinationSource);
        Assert.AreEqual(same.ActionCombinationSource, r.FlangeNetPlasticCases[2].Input.UltimateCombinationSource);
    }

    [TestMethod]
    public void AdditionalUltimateCombinationCannotBorrowTheFavourablePrimaryNetCheck()
    {
        var p = Dense(); var u = Slip(p, forces: DenseForces(1600000), source: "Separate high axial ULS");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { u }));
        Assert.AreEqual(ConnectionStatus.Passed, r.FlangeNetPlasticCases[0].Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.FlangeNetPlasticCases[1].Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.SlipCases.Single().Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        StringAssert.StartsWith(r.ComponentAssessment.GoverningCheck.Id, "FlangeNetPlastic.SlipCase1.");
        Equal(459602.339181286574, Check(r.FlangeNetPlasticCases[1], "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(448800, Check(r.FlangeNetPlasticCases[1], "TopFlange.Cover.NetPlasticTension").Resistance);
        Assert.AreEqual(100000, p.Forces.Actions.Fx);
        Assert.AreEqual(1600000, r.FlangeNetPlasticCases[1].Input.Forces.Actions.Fx);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void NetPlasticAloneCanGovernWhileAllOrdinaryComponentsAndUltimateSlipPass(bool ntc)
    {
        // Five flange rows and three-by-three web grids give sufficient bolt
        // and friction resistance. They do not increase the two-hole net section.
        var p = Dense(ntc, n: 1600000); var r = IBeamSpliceComponents.Verify(new(p,
            BoltSlipLimitState.Ultimate, new[] { Slip(p) }));
        Assert.AreEqual(ConnectionStatus.Passed, r.BoltStrength.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Bearing.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.Flanges.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.WebSections.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.WebLongitudinal.Status);
        Assert.AreEqual(ConnectionStatus.Passed, r.SlipCases.Single().Status);
        Assert.AreEqual(ConnectionStatus.Failed, r.ComponentAssessment.Status);
        StringAssert.StartsWith(r.ComponentAssessment.GoverningCheck.Id, "FlangeNetPlastic.PrimaryULS.");
        double gamma = ntc ? 1.05 : 1;
        Equal(459602.339181286574 / (448800 / gamma), r.ComponentAssessment.Utilization!.Value);
        Equal(2, Trace(r.FlangeNetPlasticCases[0], "TopFlange.Cover.HolesAtSection"));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void MomentReversalInSeparateUltimateCasesRetainsThePhysicalTensionSide()
    {
        var p = Dense();
        var negative = Slip(p, forces: DenseForces(0, 0, -100e6), source: "ULS M negative");
        var positive = Slip(p, forces: DenseForces(0, 0, 100e6), source: "ULS M positive");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { negative, positive }));
        var a = r.FlangeNetPlasticCases[1]; var b = r.FlangeNetPlasticCases[2];
        double tension = (1 - 55535282.666666664183 / 294e6) * 100e6 / 440.7;
        Equal(tension, Check(a, "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(0, Check(a, "BottomFlange.Cover.NetPlasticTension").Demand);
        Equal(0, Check(b, "TopFlange.Cover.NetPlasticTension").Demand);
        Equal(tension, Check(b, "BottomFlange.Cover.NetPlasticTension").Demand);
        Assert.AreEqual("ULS M negative", a.Input.UltimateCombinationSource);
        Assert.AreEqual("ULS M positive", b.Input.UltimateCombinationSource);
    }

    [TestMethod]
    public void ConcomitantAxialAndMomentPairsAreNotReplacedBySeparateEnvelopes()
    {
        var p = Dense();
        var first = Slip(p, forces: DenseForces(200000, 15000, 100e6), source: "ULS pair1");
        var second = Slip(p, forces: DenseForces(-400000, -25000, -80e6), source: "ULS pair2");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { first, second }));
        for (int i = 0; i < 2; i++)
        {
            var original = new[] { first, second }[i]; var assessed = r.FlangeNetPlasticCases[i + 1];
            var a = original.Forces.Actions;
            double direct = (1 - 3638d / 8550) * a.Fx / 2;
            double bending = (1 - 55535282.666666664183 / 294e6) * a.Mz / 440.7;
            Equal(Math.Max(0, direct - bending), Check(assessed, "TopFlange.Cover.NetPlasticTension").Demand);
            Equal(Math.Max(0, direct + bending), Check(assessed, "BottomFlange.Cover.NetPlasticTension").Demand);
            Assert.AreSame(original.Forces, assessed.Input.Forces);
            Equal(a.Fy, assessed.Input.Forces.Actions.Fy);
        }
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)]
    public void DifferentCodeOrPartialFactorsCannotCreateAnAdditionalNetPlasticResult(int variant)
    {
        var p = Dense();
        var code = variant == 0 ? ConnectionDesignCode.Ntc2018()
            : ConnectionDesignCode.Eurocode3(1.05, 1.1, 1.25, "Different gammaM0");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { Slip(p, code: code) }));
        Blocked(r, "CODE_MISMATCH"); Assert.AreEqual(1, r.FlangeNetPlasticCases.Count);
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)]
    public void DifferentGeometryOrBoltsCannotBorrowThePrimaryPlateData(int variant)
    {
        var p = Dense(); var forces = DenseForces(); var bolts = p.Bolts;
        if (variant == 0) forces = new(forces.Section, forces.StrongAxisSecondMoment, forces.Actions,
            new(3, 3, 80, 120, 70), forces.FlangeBolts);
        if (variant == 1) forces = new(forces.Section, 294100000, forces.Actions, forces.WebBolts, forces.FlangeBolts);
        if (variant == 2) bolts = new(new(20, 22, 245, ConnectionBoltGrade.Class10_9, true),
            p.Bolts.WebBolt, 37, 37);
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate,
            new[] { Slip(p, forces: forces, bolts: bolts) }));
        Blocked(r, "PHYSICAL_DETAIL_MISMATCH"); Assert.AreEqual(1, r.FlangeNetPlasticCases.Count);
    }

    [TestMethod]
    public void IndependentEqualGeometrySnapshotIsAcceptedAndKeepsItsOwnActions()
    {
        var p = Dense(); var clone = Dense(n: 250000, source: "Clone ULS");
        var u = Slip(p, forces: clone.Forces, bolts: clone.Bolts, source: clone.UltimateCombinationSource);
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { u }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.AreEqual(2, r.FlangeNetPlasticCases.Count);
        Assert.AreSame(clone.Forces, r.FlangeNetPlasticCases[1].Input.Forces);
        Assert.AreEqual("Clone ULS", r.FlangeNetPlasticCases[1].Input.UltimateCombinationSource);
    }

    [TestMethod]
    public void BoundCaseWithMissingUltimateSourceRemainsUnsupportedInsteadOfBeingSkipped()
    {
        var p = Dense(); var u = Slip(p, source: "");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { u }));
        Blocked(r, "COMBINATION"); Assert.AreEqual(2, r.FlangeNetPlasticCases.Count);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.FlangeNetPlasticCases[1].Status);
        Assert.AreEqual("", r.FlangeNetPlasticCases[1].Input.UltimateCombinationSource);
    }

    [TestMethod]
    public void BoundOutOfPlaneCaseCannotReturnAFavourableNetPlasticPartialCheck()
    {
        var p = Dense(); var a = p.Forces;
        var forces = new IBeamSpliceForceInput(a.Section, a.StrongAxisSecondMoment,
            new(100000, 0, 1, 0, 0, 0), a.WebBolts, a.FlangeBolts);
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate,
            new[] { Slip(p, forces: forces) }));
        Blocked(r, "IN_PLANE_N_V_M_ONLY"); Assert.AreEqual(2, r.FlangeNetPlasticCases.Count);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.FlangeNetPlasticCases[1].Status);
        Assert.AreEqual(0, r.FlangeNetPlasticCases[1].Checks.Count);
    }

    [TestMethod]
    public void PassingNetPlasticCheckCannotReplaceMissingPreloadedAssemblyEvidence()
    {
        var p = Dense(); var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate,
            new[] { Slip(p, assembly: "") }));
        Blocked(r, "PRELOADED_ASSEMBLY_QUALIFICATION_REQUIRED");
        Assert.AreEqual(2, r.FlangeNetPlasticCases.Count);
        Assert.IsTrue(r.FlangeNetPlasticCases.All(c => c.Status == ConnectionStatus.Passed));
    }

    [TestMethod]
    public void CaseConstructionPreservesAllPhysicalPlateDataAndPolicy()
    {
        var p = Input(forces: DenseForces(), flange: new(180, 12, 355, 510),
            web: new(340, 10, 275, 430), gap: 10, beamFlangeFy: 265, beamFlangeFu: 400);
        var u = Slip(p, forces: DenseForces(300000), source: "Material-sensitive ULS");
        var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { u }));
        var q = r.FlangeNetPlasticCases[1].Input;
        Assert.AreSame(p.Policy, q.Policy); Assert.AreSame(p.Bolts, q.Bolts);
        Assert.AreSame(p.FlangeCover, q.FlangeCover); Assert.AreSame(p.WebCover, q.WebCover);
        Equal(p.FlangeCoverEnd, q.FlangeCoverEnd); Equal(p.WebCoverEnd, q.WebCoverEnd);
        Equal(p.ButtGap, q.ButtGap); Equal(p.BeamFlangeFy, q.BeamFlangeFy); Equal(p.BeamFlangeFu, q.BeamFlangeFu);
        Assert.AreEqual(p.Exposed, q.Exposed);
        Equal(579360, Check(r.FlangeNetPlasticCases[1], "TopFlange.Cover.NetPlasticTension").Resistance);
        Equal(491026.45, Check(r.FlangeNetPlasticCases[1], "TopFlange.Member.NetPlasticTension").Resistance);
    }

    [TestMethod]
    public void AggregateCopiesEveryCaseCheckWithoutLosingItsReferenceUnitOrDemand()
    {
        var p = Dense(); var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate,
            new[] { Slip(p, BoltSlipLimitState.Serviceability), Slip(p, forces: DenseForces(200000)) }));
        string[] prefixes = { "FlangeNetPlastic.PrimaryULS.", "FlangeNetPlastic.SlipCase2." };
        for (int i = 0; i < r.FlangeNetPlasticCases.Count; i++)
            foreach (var c in r.FlangeNetPlasticCases[i].Checks)
            {
                var aggregated = r.ComponentAssessment.Checks.Single(a => a.Id == prefixes[i] + c.Id);
                Equal(c.Demand, aggregated.Demand); Equal(c.Resistance, aggregated.Resistance);
                Assert.AreEqual(c.Reference, aggregated.Reference); Assert.AreEqual(c.Unit, aggregated.Unit);
            }
    }

    [TestMethod]
    public void InputAndNetPlasticResultCollectionsAreImmutableSnapshots()
    {
        var p = Dense(); var source = new List<IBeamSpliceBoltSlipInput> { Slip(p), Slip(p) };
        var input = new IBeamSpliceComponentInput(p, BoltSlipLimitState.Ultimate, source);
        source.Clear(); var r = IBeamSpliceComponents.Verify(input);
        Assert.AreEqual(3, r.FlangeNetPlasticCases.Count);
        Assert.ThrowsException<NotSupportedException>(() =>
            ((IList<ConnectionVerification<IBeamSplicePlateInput>>)r.FlangeNetPlasticCases).Clear());
        Assert.ThrowsException<NotSupportedException>(() =>
            ((IList<ConnectionCheck>)r.FlangeNetPlasticCases[1].Checks).Clear());
    }

    [TestMethod]
    public void SuccessfulFlangeCategoryCChecksKeepTheCompatibilityAndWebGapsExplicit()
    {
        var p = Dense(); var r = IBeamSpliceComponents.Verify(new(p, BoltSlipLimitState.Ultimate, new[] { Slip(p) }));
        Assert.AreEqual(ConnectionStatus.Passed, r.ComponentAssessment.Status);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(), "ULTIMATE_SLIP_CATEGORY_NET_PLASTIC_RESISTANCE");
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(), "WEB_ULTIMATE_SLIP_NET_PLASTIC_N_V_M");
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(), "WEB_COVER_BLOCK_TEARING_UNDER_N_V_M");
    }
}
