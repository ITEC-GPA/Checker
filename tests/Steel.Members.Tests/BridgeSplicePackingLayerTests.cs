using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

[TestClass]
public class BridgeSplicePackingLayerTests
{
    static WeldedSplicePacking Pack(double[]? outer = null) => WeldedSplicePacking.FromLayers(
        outer ?? new[] { 12d, 8d }, new[] { 3d, 2d }, new[] { 4d, 6d }, Array.Empty<double>(), new[] { 2d, 3d }, "Full-footprint fitted layers, qualified assembly");
    static WeldedIBeamSpliceForceInput Detail(WeldedSplicePacking? packing = null, double scale = 1)
    {
        var d = BridgeSpliceUnequalWidthTests.Detail(360, new(2e5 * scale, -1e5 * scale, 0, 0, 0, 50e6 * scale));
        var s = new WeldedIBeamSpliceSection(d.Section.TopFlange, new(360, 16, 355, 510), d.Section.BottomFlange, 8, 8);
        return new(s, d.Top, d.Bottom, d.EachWebCover, d.WebBolts, d.WebCoverEnd, d.ButtGap, d.Actions, packing ?? Pack());
    }
    static WeldedIBeamSpliceSlipInput Slip(bool ntc, WeldedIBeamSpliceForceInput d, BoltSlipLimitState state, double middleMu = .35, string? middleSource = "Packing/packing")
    {
        var q = WeldedIBeamSpliceSlipTests.Input(ntc, state, d);
        return new(q.Policy, q.Bolts, state, q.Surfaces.Select(s => new WeldedSpliceFrictionSurface(s.Cover,
            Enumerable.Range(0, d.Packing.Layers(s.Cover).Count + 1).Select(j => new WeldedSpliceFrictionInterface(
                j == 1 ? middleMu : .4, j == 1 ? middleSource! : "Outer interface")))),
            q.TighteningControl, q.AssemblyQualificationSource, q.ActionCombinationSource);
    }
    static WeldedIBeamSpliceStandardInput Workflow(bool ntc, SpliceBoltCategory cat, double scale = 1)
    {
        var d = Detail(scale: scale); var p = WeldedIBeamSpliceStandardTests.Input(ntc, d);
        return new(new[] { new WeldedSpliceStandardCase("U", WeldedIBeamSpliceStandardTests.Parents(p)) }, cat,
            cat == SpliceBoltCategory.A ? null : new[] { new WeldedSpliceSlipCase("S", Slip(ntc, d, cat == SpliceBoltCategory.B
                ? BoltSlipLimitState.Serviceability : BoltSlipLimitState.Ultimate), cat == SpliceBoltCategory.C ? "U" : null) },
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback, SpliceFlangeFaceModel.PackedIndependentFaceDimensions);
    }
    [DataTestMethod]
    [DataRow(false, SpliceBoltCategory.A)][DataRow(false, SpliceBoltCategory.B)][DataRow(false, SpliceBoltCategory.C)]
    [DataRow(true, SpliceBoltCategory.A)][DataRow(true, SpliceBoltCategory.B)][DataRow(true, SpliceBoltCategory.C)]
    public void EndToEndIncludesEveryLayer(bool ntc, SpliceBoltCategory cat)
    {
        var r = WeldedIBeamSpliceStandard.Verify(Workflow(ntc, cat));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);
    }
    [TestMethod] public void BetaUsesTotalOfThickerStackForBothPlanes()
    {
        var d = Detail(); var r = WeldedIBeamSpliceBolts.Verify(WeldedIBeamSpliceTests.BoltInput(detail: d)).ComponentAssessment;
        // EN1993-1-8 3.6.1(12)-(13): total20mm, NOT max(single layers)=12mm,
        // NOT sum(both faces)=25mm. M24 grade8.8 threaded As353, gamma1.25.
        foreach (string face in new[] { "TopOuter", "TopInnerPositive", "TopInnerNegative" })
            Close(.6 * 800 * 353 / 1.25 * (9d * 24 / (8 * 24 + 3 * 20)),
                r.Checks.First(c => c.Id.Contains("." + face + ".") && c.Id.EndsWith("ShearPlane")).Resistance);
        Close(30 + 20 + 5 + 20 + 20, d.Packing.Grip(d, WeldedSpliceCover.TopOuter));
    }
    [TestMethod] public void ForceEquilibriumDependsOnTotalOffsetsNotLayerCount()
    {
        var stack = WeldedIBeamSpliceForces.Calculate(Detail());
        var single = WeldedIBeamSpliceForces.Calculate(Detail(new(20, 5, 10, 0, 5, "Detail")));
        Assert.IsTrue(stack.IsBalanced); Assert.IsTrue(single.IsBalanced);
        CollectionAssert.AreEqual(single.CoverGroups.SelectMany(g => g.Distribution.Forces).Select(f => f.Fx).ToArray(),
            stack.CoverGroups.SelectMany(g => g.Distribution.Forces).Select(f => f.Fx).ToArray());
        Assert.IsTrue(stack.Trace.Where(t => t.Name.Contains(".Residual.")).All(t => Math.Abs(t.Value) < 1e-7));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void EverySerialInterfaceHasFullDemandAndCommonPreload(bool ntc)
    {
        var r = WeldedIBeamSpliceSlip.Verify(Slip(ntc, Detail(), BoltSlipLimitState.Ultimate));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        var prefix = "Left.TopOuter.Bolt0.";
        var a = r.Checks.Single(c => c.Id == prefix + "CoverToPacking.Slip.Ultimate");
        var b = r.Checks.Single(c => c.Id == prefix + "Packing1ToPacking2.Slip.Ultimate");
        var c = r.Checks.Single(c => c.Id == prefix + "PackingToParent.Slip.Ultimate");
        Close(a.Demand, b.Demand); Close(a.Demand, c.Demand);
        Close(.4 * .7 * 800 * 353 / 1.25, a.Resistance);
        Close(.35 * .7 * 800 * 353 / 1.25, b.Resistance); Close(a.Resistance, c.Resistance);
    }
    [TestMethod] public void WeakMiddleInterfaceFailsDespiteStrongOuterFaces()
    {
        var r = WeldedIBeamSpliceSlip.Verify(Slip(false, Detail(), BoltSlipLimitState.Ultimate, .00001));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Any(c => c.Id.Contains("Packing1ToPacking2") && !c.Passed));
    }
    [TestMethod] public void MiddleInterfaceNeedsItsOwnQualification()
    {
        var r = WeldedIBeamSpliceSlip.Verify(Slip(false, Detail(), BoltSlipLimitState.Ultimate, middleSource: null));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsTrue(r.Diagnostics.Any(c => c.Contains("Packing1ToPacking2")));
    }
    [TestMethod] public void TwoSurfaceLegacyInputCannotQualifyTwoLayers()
    {
        var q = Slip(false, Detail(), BoltSlipLimitState.Ultimate);
        var r = WeldedIBeamSpliceSlip.Verify(new(q.Policy, q.Bolts, q.LimitState, q.Surfaces.Select(s => s.Interfaces.Count > 1
            ? new WeldedSpliceFrictionSurface(s.Cover, .4, "Cover", .4, "Parent") : s), q.TighteningControl, q.AssemblyQualificationSource, q.ActionCombinationSource));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsTrue(r.Diagnostics.Any(x => x.Contains("INTERFACES_MISMATCH")));
    }
    [DataTestMethod][DataRow(20d, 0d)][DataRow(8d, 12d)]
    public void EnvelopeRejectsDifferentLayerTopologyEvenWithEqualTotal(double a, double b)
    {
        var q = Workflow(false, SpliceBoltCategory.C); var d = Detail(Pack(b == 0 ? new[] { a } : new[] { a, b }));
        var r = WeldedIBeamSpliceStandard.Verify(new(q.Cases, q.Envelope.Category,
            new[] { new WeldedSpliceSlipCase("S", Slip(false, d, BoltSlipLimitState.Ultimate), "U") }, q.BlockMethod, q.FaceModel));
        Assert.IsFalse(r.IsStaticResistanceComplete); Assert.IsTrue(r.MissingRequirements.Any(x => x.Contains("MISMATCH")));
    }
    [TestMethod] public void EnvelopeBindsIntermediateCoefficientsBetweenCases()
    {
        var q = Workflow(false, SpliceBoltCategory.B); var d = Detail();
        var r = WeldedIBeamSpliceStandard.Verify(new(q.Cases, q.Envelope.Category,
            new[] { new WeldedSpliceSlipCase("S1", Slip(false, d, BoltSlipLimitState.Serviceability)),
                new WeldedSpliceSlipCase("S2", Slip(false, d, BoltSlipLimitState.Serviceability, .3)) }, q.BlockMethod, q.FaceModel));
        Assert.IsFalse(r.IsStaticResistanceComplete); Assert.IsTrue(r.MissingRequirements.Any(x => x.Contains("MISMATCH")));
    }
    [TestMethod] public void BuilderAndReportRetainLayerOrderAndProvenance()
    {
        var d = Detail(); var template = WeldedIBeamSpliceStandardTests.Input(detail: d);
        var s = WeldedIBeamSpliceStandardTests.Section(template).Plan.Input.Section;
        var p = BridgeSpliceApplication.CreateBolted(template, s, s, BridgeSpliceApplicationTests.Actions(),
            WeldedIBeamSpliceStandardTests.Welds(), "Model", SpliceBoltCategory.C, BridgeSpliceApplicationTests.Prepare,
            Slip(false, d, BoltSlipLimitState.Ultimate), blockMethod: SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            faceModel: SpliceFlangeFaceModel.PackedIndependentFaceDimensions);
        Assert.IsTrue(p.Cases.All(c => ReferenceEquals(d.Packing, c.Parents.Detail.Plates.Bolts.Detail.Packing)));
        var r = BridgeSpliceReport.FromBolted(WeldedIBeamSpliceStandard.Verify(p)); Assert.IsTrue(r.IsComplete);
        Assert.IsTrue(r.Notes.Any(n => n.Contains("TopOuter:Packing1ToPacking2:mu=") && n.Contains("source=Packing/packing")));
        foreach (var c in r.Cases) { Close(12, c.InputValues.Single(t => t.Name == "Packing.TopOuter.Layer1").Value);
            Close(8, c.InputValues.Single(t => t.Name == "Packing.TopOuter.Layer2").Value); }
    }
    [TestMethod] public void InputCollectionsAreImmutableSnapshots()
    {
        var layers = new[] { 12d, 8d }; var packing = Pack(layers); layers[0] = 500;
        Close(20, packing.TopOuter); Close(12, packing.TopOuterLayers[0]);
        var surfaces = new[] { new WeldedSpliceFrictionInterface(.4, "A"), new WeldedSpliceFrictionInterface(.3, "B") };
        var s = new WeldedSpliceFrictionSurface(WeldedSpliceCover.TopOuter, surfaces); surfaces[0] = new(.2, "Changed");
        Close(.4, s.Coefficient);
    }
    [TestMethod] public void EmptyStacksRecoverNoPacking()
    {
        var p = WeldedSplicePacking.FromLayers(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), null!);
        Assert.IsFalse(p.HasPacking); Assert.AreEqual(0, p.TopOuterLayers.Count);
    }
    [DataTestMethod][DataRow(-1d)][DataRow(0d)][DataRow(double.NaN)][DataRow(double.PositiveInfinity)]
    public void InvalidLayerRejected(double t) => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pack(new[] { t }));
    [TestMethod] public void OverflowingTotalRejected() => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Pack(new[] { double.MaxValue, double.MaxValue }));
    [TestMethod] public void EmptyOrNullInterfacesRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => new WeldedSpliceFrictionSurface(WeldedSpliceCover.TopOuter, Array.Empty<WeldedSpliceFrictionInterface>()));
        Assert.ThrowsException<ArgumentException>(() => new WeldedSpliceFrictionSurface(WeldedSpliceCover.TopOuter, new WeldedSpliceFrictionInterface[] { null! }));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReversalAndRealOverloadRemainVisible(bool ntc)
    {
        Assert.AreEqual(ConnectionStatus.Passed, WeldedIBeamSpliceStandard.Verify(Workflow(ntc, SpliceBoltCategory.C, -1)).Status);
        Assert.AreEqual(ConnectionStatus.Failed, WeldedIBeamSpliceStandard.Verify(Workflow(ntc, SpliceBoltCategory.C, 100)).Status);
    }
    static void Close(double a, double b) => Assert.AreEqual(a, b, Math.Max(1e-8, Math.Abs(a) * 1e-10));
}
