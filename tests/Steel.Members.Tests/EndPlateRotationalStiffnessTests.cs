using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateRotationalStiffnessTests
{
    // JRC96658 (2015), section3.3.5/Table3.8, pp174-175. The area below is
    // independently derived from the stated HEA340 dimensions; JRC rounds Av to4495.
    private static ConnectionRolledISection Column() => new(330, 300, 9.5, 16.5, 27,
        2 * 300 * 16.5 + (330 - 33) * 9.5 + (4 - Math.PI) * 27 * 27, 235, 360);
    private static JointStiffnessEffectiveLength Length(params double[] values)
        => new(values, "JRC individual AND relevant group contribution of this row, Table3.8");
    private static EndPlateStiffnessRow Row(int i, double? lever = null, double? thickness = null,
        double? boltLength = null, JointStiffnessEffectiveLength? columnLength = null,
        JointStiffnessEffectiveLength? plateLength = null, string? source = "JRC geometry, grip/head/nut; washers absent in published idealization")
    {
        double[] arms = [527, 442, 362], lc = [166.05, 82.5, 163.55], lp = [120, 234.69, 175.98];
        return new(lever ?? arms[i], columnLength ?? Length(lc[i]), plateLength ?? Length(lp[i]),
            33.65, i == 0 ? 25.95 : 49.24, thickness ?? 15, 353, boltLength ?? 48.5, source!);
    }
    private static EndPlateRotationalStiffnessInput Input(bool ntc = false,
        IEnumerable<EndPlateStiffnessRow>? rows = null, double beta = 1, double n = 0,
        double moment = 0, double mrd = 300e6, string? source = "JRC column-flange unstiffened detail; chosen tension side",
        string? action = "NplRd1MN and MjRd300kNm are explicit TEST assumptions, not published JRC capacities",
        string? transformation = "Single-sided beta1, EN5.3/Table5.4",
        ColumnWebPanelConfiguration configuration = ColumnWebPanelConfiguration.SingleSidedJoint,
        ColumnWebEffectiveWidth? compression = null)
        => new(ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EC test policy"),
            Column(), compression ?? new(274.81, "JRC p157 beam flange+2weld+5column roots+sp"),
            configuration, beta, rows ?? new[] { Row(0), Row(1), Row(2) }, n, 1e6, moment, mrd,
            transformation!, action!, source!);
    private static double V(EndPlateRotationalStiffnessResult r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * 1e-10);
    private static void Unsupported(EndPlateRotationalStiffnessResult r, string diagnostic)
    {
        Assert.IsFalse(r.IsSupported); Assert.IsNull(r.InitialStiffness); Assert.IsNull(r.SecantStiffness);
        Assert.IsNull(r.EquivalentLeverArm); Assert.IsNull(r.EquivalentRowCoefficient); Assert.IsNull(r.StiffnessRatio);
        Assert.IsNull(r.ModelRotation); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(diagnostic)), string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void JrcWorkedExampleUsesUnroundedGeometryAndGeneralEquivalentArm(bool ntc)
    {
        var r = EndPlateRotationalStiffness.Calculate(Input(ntc));
        Assert.IsTrue(r.IsSupported, string.Join(";", r.Diagnostics));
        // Independent source-based recalculation; the published approximate panel
        // arm is distinct from the general zeq method. See derive_jrc96658_rotational_stiffness.py.
        Equal(4495.02895553304, V(r, "ColumnShearArea"));
        Equal(7.520520576131687, V(r, "K2"));
        Equal(1.6 * 353 / 48.5, V(r, "Row1.K10"));
        Equal(466.5802716683375, r.EquivalentLeverArm!.Value);
        Equal(5.2338398814415354, r.EquivalentRowCoefficient!.Value);
        Equal(3.660915616931064, V(r,"K1"));
        Equal(76552582907.97441,r.InitialStiffness!.Value);
        Equal(2.435266872227053,V(r,"Row1.Keff"));
        Equal(1.233797097937325,V(r,"Row2.Keff"));
        Equal(1.6941504828718683,V(r,"Row3.Keff"));
        Assert.IsTrue(r.EquivalentLeverArm > 362 && r.EquivalentLeverArm < 527);
        Assert.AreEqual(75.214e9, r.InitialStiffness!.Value, 1.5e9,
            "Published approximate panel arm and rounded coefficients differ from the general method.");
        Equal(r.InitialStiffness.Value, r.SecantStiffness!.Value); Equal(0, r.ModelRotation!.Value);
    }

    [TestMethod]
    public void RowMinimaIncludeGroupContributionsWithoutAveragingGroupTotals()
    {
        var r = EndPlateRotationalStiffness.Calculate(Input(rows: new[] {
            Row(0, columnLength: Length(211.43,166.05)),
            Row(1, columnLength: Length(211.43, 82.5, 166.05), plateLength: Length(259.04,234.69)),
            Row(2, columnLength: Length(211.43,163.55)) }));
        Assert.IsTrue(r.IsSupported);
        Equal(82.5, V(r, "Row2.ColumnLength")); Equal(234.69, V(r, "Row2.EndPlateLength"));
        Equal(.7 * 82.5 * 9.5 / 243, V(r, "Row2.K3"));
        Assert.AreNotEqual(412.1 / 3, V(r, "Row2.ColumnLength"));
    }

    [TestMethod]
    public void OneRowReducesToAllSixPhysicalComponentsInSeries()
    {
        var r = EndPlateRotationalStiffness.Calculate(Input(rows: new[] { Row(0) }));
        Assert.IsTrue(r.IsSupported); Equal(527, r.EquivalentLeverArm!.Value);
        double f = 1 / V(r,"K1") + 1 / V(r,"K2") + 1 / V(r,"Row1.K3")
            + 1 / V(r,"Row1.K4") + 1 / V(r,"Row1.K5") + 1 / V(r,"Row1.K10");
        Equal(210000d * 527 * 527 / f, r.InitialStiffness!.Value);
    }

    [TestMethod]
    public void OpposedBalancedMomentsRemovePanelFlexibilityButRetainColumnCompression()
    {
        var r = EndPlateRotationalStiffness.Calculate(Input(beta:0,
            configuration:ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths));
        Assert.IsTrue(r.IsSupported); Equal(0,V(r,"PanelFlexibility"));
        Assert.IsFalse(r.Trace.Any(t=>t.Name=="K1"));
        double z=r.EquivalentLeverArm!.Value, k=r.EquivalentRowCoefficient!.Value;
        Equal(210000*z*z/(1/V(r,"K2")+1/k),r.InitialStiffness!.Value);
        Assert.IsTrue(r.InitialStiffness.Value < 210000*z*z*k);
    }

    [DataTestMethod] [DataRow(0d)] [DataRow(100e6)] [DataRow(200e6)]
    public void UpToTwoThirdsResistanceRetainsInitialStiffness(double m)
    {
        var r=EndPlateRotationalStiffness.Calculate(Input(moment:m));
        Equal(1,r.StiffnessRatio!.Value); Equal(r.InitialStiffness!.Value,r.SecantStiffness!.Value);
        Equal(m/r.SecantStiffness.Value,r.ModelRotation!.Value);
    }

    [DataTestMethod] [DataRow(240e6,1.6360228471663996)] [DataRow(300e6,2.988452789872502)]
    public void NonlinearSecantBranchUsesEndPlateExponentTwoPointSeven(double m,double mu)
    {
        var r=EndPlateRotationalStiffness.Calculate(Input(moment:m));
        Equal(mu,r.StiffnessRatio!.Value); Equal(r.InitialStiffness!.Value/mu,r.SecantStiffness!.Value);
    }

    [DataTestMethod] [DataRow(-50000d)] [DataRow(50000d)]
    public void FivePercentAxialBoundaryIsAcceptedInBothSigns(double n)
        => Assert.IsTrue(EndPlateRotationalStiffness.Calculate(Input(n:n)).IsSupported);
    [DataTestMethod] [DataRow(-50000.001)] [DataRow(50000.001)]
    public void ActualAxialExcessCannotReturnStiffness(double n)
        => Unsupported(EndPlateRotationalStiffness.Calculate(Input(n:n)),"FivePercent");
    [TestMethod]
    public void MomentAboveQualifiedResistanceIsRejected()
        => Unsupported(EndPlateRotationalStiffness.Calculate(Input(moment:300000001)),"MomentExceeds");
    [TestMethod]
    public void ReversedOrDuplicateRowsAreNotSilentlyReordered()
    {
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(1),Row(0)})),"RowsMustDecrease");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0),Row(0)})),"RowsMustDecrease");
    }
    [DataTestMethod] [DataRow(1d)] [DataRow(31.5)]
    public void BoltLengthMustIncludeActualPlatesAndHardware(double length)
        => Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,boltLength:length)})),"BoltLength");
    [TestMethod]
    public void DifferentThicknessesCannotPretendToBeOneUniformPlate()
        => Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0),Row(1,thickness:16)})),"OneThickness");
    [TestMethod]
    public void PhysicalQualificationsRemainMandatoryAtZeroMoment()
    {
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(source:null)),"JointGeometrySource");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(action:null)),"ResistanceAndConcomitant");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(transformation:null)),"TransformationSource");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(compression:new(274.81,""))),"CompressionWidth");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,source:null)})),"GeometrySource");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,columnLength:new(new[]{166.05},""))})),"ColumnRowAndGroup");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,plateLength:new(new[]{120d},""))})),"EndPlateRowAndGroup");
    }
    [TestMethod]
    public void WidthFromAnotherColumnSnapshotIsRejected()
    {
        var width=ColumnWebEffectiveWidth.FromBoltedEndPlate(Column(),20,8,15,40,"other physical detail");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(compression:width)),"AnotherColumn");
    }
    [TestMethod]
    public void UnsupportedPanelConfigurationsAreExplicit()
    {
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(beta:0)),"BetaOne");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(beta:.5)),"BetaOne");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(configuration:ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths)),"UnequalBeamDepths");
    }
    [TestMethod]
    public void GeometryArraysAndOutputsAreDefensiveCopies()
    {
        double[] a=[166.05,200];var l=new JointStiffnessEffectiveLength(a,"source");a[0]=1;
        Equal(166.05,l.Minimum);
        var rows=new[]{Row(0)};var p=Input(rows:rows);rows[0]=Row(2);Equal(527,p.Rows[0].LeverArm);
        var r=EndPlateRotationalStiffness.Calculate(p);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<double>)l.RelevantRowLengths).Clear());
        Assert.ThrowsException<NotSupportedException>(()=>((IList<string>)r.Diagnostics).Clear());
    }
    [TestMethod]
    public void CalculationDoesNotCertifyStrengthOrRotationCapacity()
    {
        var r=EndPlateRotationalStiffness.Calculate(Input());
        Assert.IsFalse(r.CanQualifyWholeJoint);StringAssert.Contains(r.Scope,"Not rotation capacity");
        StringAssert.Contains(r.Scope,"global frame classification");StringAssert.Contains(r.Scope,"Nmm/rad");
    }
    [TestMethod]
    public void InvalidInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>EndPlateRotationalStiffness.Calculate(null!));
        Assert.ThrowsException<ArgumentException>(()=>Length());
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Length(double.NaN));
        Assert.ThrowsException<ArgumentException>(()=>Input(rows:Array.Empty<EndPlateStiffnessRow>()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(moment:-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(beta:2.01));
    }
    [TestMethod]
    public void ExtremeGeometryCannotLeakInfinityOrAFavorableZero()
    {
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,lever:1e308)})),"OutsideNumericRange");
        Unsupported(EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,thickness:1e-200)})),"Outside");
        var finite=EndPlateRotationalStiffness.Calculate(Input(rows:new[]{Row(0,boltLength:1e308)}));
        Assert.IsTrue(finite.IsSupported);Assert.IsTrue(finite.InitialStiffness>0);
        Assert.IsTrue(finite.Trace.All(t=>double.IsFinite(t.Value)));
    }
}
