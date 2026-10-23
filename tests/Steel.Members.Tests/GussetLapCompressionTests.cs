using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class GussetLapCompressionTests
{
    private static ConnectionDesignCode Ec(double gamma0 = 1) => ConnectionDesignCode.Eurocode3(gamma0, 1.25, 1.25, "Explicit analytical parameter set");
    private static GussetLapCompressionInput Input(ConnectionDesignCode? code = null, ConnectionPlate? tab = null,
        ConnectionPlate? gusset = null, PlanarConnectionActions? actions = null, double lt = 40, double lg = 50,
        double pitch = 60, double gauge = 80, GussetLapEndRestraint tabRestraint = GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation,
        GussetLapEndRestraint gussetRestraint = GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation,
        GussetLapProportion proportion = GussetLapProportion.Compact, string? restraintSource = "Test detail: two rigid fully attached ends",
        string? compactSource = "Constructed compact detail, whole member checked separately")
        => new(code ?? Ec(), tab ?? new(160, 10, 275, 430), gusset ?? new(200, 12, 275, 430),
            actions ?? new(-100000, 0, 0, 0, 0, 0), lt, lg, pitch, gauge,
            tabRestraint, gussetRestraint, proportion, restraintSource!, compactSource!);
    private static ConnectionVerification<GussetLapCompressionInput> Verify(GussetLapCompressionInput? p = null)
        => GussetLapCompression.Verify(p ?? Input());
    private static double T(ConnectionVerification<GussetLapCompressionInput> r, string name) => r.Trace.Single(x => x.Name == name).Value;
    private static double Rd(ConnectionVerification<GussetLapCompressionInput> r, string part)
        => r.Checks.Single(x => x.Id == part + ".CompressionSwayInteraction").Resistance;
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-12));
    private static void Unsupported(ConnectionVerification<GussetLapCompressionInput> r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.GoverningCheck); Assert.IsNull(r.Utilization);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), issue);
    }

    [DataTestMethod]
    [DataRow(false, 126288.99672241799, 148986.22597402238)]
    [DataRow(true, 120275.2349737314, 141891.64378478323)]
    public void AsymmetricPlatesMatchIndependentRationalEquilibriumAndInteraction(bool ntc, double nt, double ng)
    {
        // Fixture derivation solves equal branch rotation and N/Npl + M/Mpartial = 1,
        // independently of the rearranged production capacity formula. P358 pp261-263.
        var r = Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec()));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(2, r.Checks.Count);
        Equal(13333.333333333334, T(r, "TabInertia")); Equal(28800, T(r, "GussetInertia"));
        Equal(159720, T(r, "LapInertia")); Equal(.3763745965774663, T(r, "TabMomentFraction"));
        Equal(.6236254034225337, T(r, "GussetMomentFraction"));
        Equal(11, T(r, "InitialLapEccentricity")); Equal(13.2, T(r, "AmplifiedLapEccentricity"));
        Equal(nt, Rd(r, "Tab")); Equal(ng, Rd(r, "Gusset"));
        Equal(496814.4674822556, T(r, "TabMomentMagnitude"));
        Equal(823185.5325177445, T(r, "GussetMomentMagnitude"));
        Assert.AreEqual("Tab.CompressionSwayInteraction", r.GoverningCheck!.Id);
        StringAssert.Contains(r.GoverningCheck.Reference, "supplementary model");
    }

    [DataTestMethod]
    [DataRow("A1", 158500.0)] [DataRow("A2", 186100.0)] [DataRow("A3", 159800.0)]
    [DataRow("B1", 175100.0)] [DataRow("C1", 165300.0)]
    public void KhooCompactPhysicalTestsAreComparedWithSciWithoutReplacingMeasuredLoads(string specimen, double measured)
    {
        // Khoo/Perera/Albermani IJASC6(2),2010, DOI10.18057/IJASC.2010.6.2.1:
        // fig2 p680 and tables1/2 p683. Actual coupon fy320, plates180x10,
        // b=c=50, Sp70, gauge110, fourM20. Only Lc170mm connection-failure cases.
        // This is a conservative physical comparison, NOT equality to an experimental failure.
        var r = Verify(Input(tab: new(180, 10, 320, 430), gusset: new(180, 10, 320, 430),
            lt: 50, lg: 50, pitch: 70, gauge: 110, actions: new(-measured, 0, 0, 0, 0, 0),
            restraintSource: "Khoo2010 figs2-4: slotted CHS260mm weld and full-penetration support weld",
            compactSource: "Khoo2010 table1 specimen " + specimen + ", Lc170mm"));
        Equal(.5, T(r, "TabMomentFraction")); Equal(144000, Rd(r, "Tab")); Equal(144000, Rd(r, "Gusset"));
        Assert.IsTrue(Rd(r, "Tab") < measured); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Equal(measured / 144000, r.Utilization!.Value);
    }

    [TestMethod]
    public void MomentPartitionSatisfiesEquilibriumAndEachCapacitySatisfiesItsInteraction()
    {
        var r = Verify();
        Equal(1, T(r, "TabMomentFraction") + T(r, "GussetMomentFraction"));
        Equal(100000 * 13.2, T(r, "TabMomentMagnitude") + T(r, "GussetMomentMagnitude"));
        foreach (string part in new[] { "Tab", "Gusset" })
        {
            double n = Rd(r, part);
            double m = n * T(r, part + "MomentMagnitude") / 100000;
            Equal(1, n / T(r, part + "GrossYieldResistance") + m / T(r, part + "PartialPlasticMomentResistance"));
        }
    }

    [TestMethod]
    public void StifferBranchReceivesMoreMomentFollowingP358Step4()
    {
        var r = Verify(Input(tab: new(180, 10, 275, 430), gusset: new(180, 10, 275, 430), lt: 40, lg: 50));
        Assert.IsTrue(T(r, "TabMomentFraction") > .5);
        Assert.IsTrue(Rd(r, "Tab") < Rd(r, "Gusset"));
    }

    [TestMethod]
    public void ExchangingPlatesAndFreeLengthsExchangesTheirResults()
    {
        var a = Verify(); var b = Verify(Input(tab: new(200, 12, 275, 430), gusset: new(160, 10, 275, 430), lt: 50, lg: 40));
        Equal(Rd(a, "Tab"), Rd(b, "Gusset")); Equal(Rd(a, "Gusset"), Rd(b, "Tab"));
        Equal(T(a, "TabMomentMagnitude"), T(b, "GussetMomentMagnitude"));
    }

    [DataTestMethod]
    [DataRow(.5)] [DataRow(1.5)]
    public void GeometricallySimilarDetailsScaleCapacityWithArea(double scale)
    {
        var a = Verify();
        var b = Verify(Input(tab: new(160 * scale, 10 * scale, 275, 430), gusset: new(200 * scale, 12 * scale, 275, 430),
            lt: 40 * scale, lg: 50 * scale, pitch: 60 * scale, gauge: 80 * scale));
        Equal(Rd(a, "Tab") * scale * scale, Rd(b, "Tab"));
        Equal(Rd(a, "Gusset") * scale * scale, Rd(b, "Gusset"));
        Equal(T(a, "TabMomentFraction"), T(b, "TabMomentFraction"));
    }

    [TestMethod]
    public void OneEdgeYieldLineWidthsAreCappedAtTwentyThicknesses()
    {
        var r = Verify(Input(tab: new(600, 10, 275, 430), gusset: new(700, 12, 275, 430)));
        var capped = Verify(Input(tab: new(200, 10, 275, 430), gusset: new(240, 12, 275, 430)));
        Equal(200, T(r, "TabEffectiveYieldLineWidth")); Equal(240, T(r, "GussetEffectiveYieldLineWidth"));
        Equal(Rd(capped, "Tab"), Rd(r, "Tab")); Equal(Rd(capped, "Gusset"), Rd(r, "Gusset"));
    }

    [TestMethod]
    public void ExplicitGammaM0ChangesResistanceOnlyAndOtherFactorsDoNotEnter()
    {
        var a = Verify(); var b = Verify(Input(code: ConnectionDesignCode.Eurocode3(1.1, 1.5, 1.8, "Independent factors")));
        Equal(Rd(a, "Tab") / 1.1, Rd(b, "Tab")); Equal(Rd(a, "Gusset") / 1.1, Rd(b, "Gusset"));
        Equal(T(a, "TabMomentFraction"), T(b, "TabMomentFraction")); Equal(1.2, T(b, "MomentAmplification"));
    }

    [DataTestMethod]
    [DataRow(0.0, ConnectionStatus.Passed)] [DataRow(100000.0, ConnectionStatus.Passed)] [DataRow(200000.0, ConnectionStatus.Failed)]
    public void DemandChangesStatusWithoutChangingResistance(double compression, ConnectionStatus status)
    {
        var r = Verify(Input(actions: new(-compression, 0, 0, 0, 0, 0)));
        Assert.AreEqual(status, r.Status); Equal(126288.99672241799, Rd(r, "Tab"));
        Equal(compression / Rd(r, "Tab"), r.Utilization!.Value);
    }

    [TestMethod]
    public void ExactResistanceBoundaryPasses()
    {
        double n = Verify().GoverningCheck!.Resistance;
        Assert.AreEqual(ConnectionStatus.Passed, Verify(Input(actions: new(-n, 0, 0, 0, 0, 0))).Status);
    }

    [TestMethod]
    public void ActionTransportPreservesEquivalentPureAxialState()
    {
        var r = Verify(Input(actions: new(-100000, 0, 0, 0, 1000000, -2000000, 70, 20, 10)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(Verify().Utilization!.Value, r.Utilization!.Value);
    }

    [DataTestMethod]
    [DataRow(0, 1.0)] [DataRow(1, 1.0)] [DataRow(2, 1.0)] [DataRow(3, 1.0)] [DataRow(4, 1.0)] [DataRow(4, 1e-12)]
    public void AdditionalConcomitantActionIsNotSilentlyDiscarded(int index, double value)
    {
        var a = new double[5]; a[index] = value;
        Unsupported(Verify(Input(actions: new(-100000, a[0], a[1], a[2], a[3], a[4]))), "PureAxialActionAtTabYieldLineRequired");
    }

    [TestMethod]
    public void AppliedOutOfPlaneEccentricityIsNotConfusedWithInherentLapEccentricity()
        => Unsupported(Verify(Input(actions: new(-100000, 0, 0, 0, 0, 0, applicationZ: 1))), "PureAxialActionAtTabYieldLineRequired");

    [TestMethod]
    public void TensionIsOutsideCompressionComponent()
        => Unsupported(Verify(Input(actions: new(100000, 0, 0, 0, 0, 0))), "CompressionOnly");

    [DataTestMethod]
    [DataRow(true, GussetLapEndRestraint.Unspecified)] [DataRow(true, GussetLapEndRestraint.Pinned)]
    [DataRow(false, GussetLapEndRestraint.Unspecified)] [DataRow(false, GussetLapEndRestraint.Pinned)]
    public void EachEndMustActuallyProvideFixedRestraint(bool tab, GussetLapEndRestraint restraint)
        => Unsupported(Verify(Input(tabRestraint: tab ? restraint : GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation,
            gussetRestraint: tab ? GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation : restraint)), "BothYieldLinesRequireRealFixedEndRestraint");

    [DataTestMethod]
    [DataRow(GussetLapProportion.Unspecified)] [DataRow(GussetLapProportion.Elongated)]
    public void UnqualifiedAndElongatedDetailsHaveNoFavourablePartialResult(GussetLapProportion proportion)
        => Unsupported(Verify(Input(proportion: proportion)), "CompactConnectionRequired");

    [TestMethod]
    public void MissingRestraintEvidenceBlocksAssessment()
        => Unsupported(Verify(Input(restraintSource: null)), "RestraintAssessmentSourceRequired");

    [TestMethod]
    public void MissingCompactDetailEvidenceBlocksAssessment()
        => Unsupported(Verify(Input(compactSource: " ")), "CompactDetailSourceRequired");

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void TwentyMillimetreThicknessBoundaryAppliesToBothPlates(bool tab)
    {
        var at = Verify(Input(tab: tab ? new(160, 20, 275, 430) : null, gusset: tab ? null : new(200, 20, 275, 430)));
        Assert.IsTrue(at.IsComplete);
        Unsupported(Verify(Input(tab: tab ? new(160, 20.001, 275, 430) : null, gusset: tab ? null : new(200, 20.001, 275, 430))),
            "P358MaximumPlateThickness20mm");
    }

    [TestMethod]
    public void HigherSteelGradeIsExplicitlyOutsideFirstImplementation()
        => Unsupported(Verify(Input(tab: new(160, 10, 460, 550))), "ImplementationCarbonSteelUpToS355Only");

    [TestMethod]
    public void GaugeMustKeepBoltCentresInsideBothPhysicalPlates()
        => Unsupported(Verify(Input(gauge: 160)), "BoltCentresOutsidePlateWidths");

    [TestMethod]
    public void NumericalOverflowReturnsNoResistanceChecks()
        => Unsupported(Verify(Input(tab: new(160, 10, double.MaxValue, double.MaxValue))), "ResistanceOutsideNumericRange");

    [TestMethod]
    public void NumericalUnderflowReturnsNoResistanceChecks()
        => Unsupported(Verify(Input(tab: new(160, double.Epsilon, 275, 430))), "ResistanceOutsideNumericRange");

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(-1.0)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidFreeLengthCannotEnterTheModel(double value)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(lt: value));

    [TestMethod]
    public void UnknownRestraintCannotEnterTheModel()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(tabRestraint: (GussetLapEndRestraint)77));

    [TestMethod]
    public void NullInputIsRejected() => Assert.ThrowsException<ArgumentNullException>(() => GussetLapCompression.Verify(null!));

    [TestMethod]
    public void ResultExplicitlyRemainsAComponentAndLabelsSciSupplementaryForNtc()
    {
        var r = Verify(Input(code: ConnectionDesignCode.Ntc2018()));
        StringAssert.Contains(r.Scope, "Plate sway mechanism only"); StringAssert.Contains(r.Scope, "Not a whole-joint");
        StringAssert.Contains(r.GoverningCheck!.Reference, "SCI P358:2014 8.5");
        StringAssert.Contains(r.GoverningCheck.Reference, "NTC2018 gammaM0");
        Assert.AreEqual(2, r.Checks.Count);
    }
}
