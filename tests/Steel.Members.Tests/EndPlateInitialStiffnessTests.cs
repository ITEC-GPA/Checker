using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateInitialStiffnessTests
{
    private static JointStiffnessEffectiveLength L(params double[] values) => new(values, "JRC Table3.8 row/group lengths");
    private static EndPlateInitialStiffnessInput Jrc(bool ntc = false, double beta = 1,
        string geometry = "JRC96658 section3.3.5", string transform = "One-sided beta1",
        double thickness = 15, double boltLength = 48.5, double firstLever = 527,
        string lengths = "JRC row/group qualification", double? plateM = null,
        ColumnWebPanelConfiguration config = ColumnWebPanelConfiguration.SingleSidedJoint)
    {
        var column = new ConnectionRolledISection(330, 300, 9.5, 16.5, 27,
            2 * 300 * 16.5 + (330 - 33) * 9.5 + (4 - Math.PI) * 27 * 27, 235, 360);
        return new(ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EC"),
            column, new(274.81, "JRC p157"), config, beta, new[] {
                new EndPlateStiffnessRow(firstLever, new(new[] { 211.43, 166.05 }, lengths), L(120), 33.65,
                    plateM ?? 25.95, thickness, 353, boltLength, geometry),
                new(442, L(211.43, 82.5, 166.05), L(259.04, 234.69), 33.65, 49.24, 15, 353, 48.5, geometry),
                new(362, L(211.43, 163.55), L(175.98), 33.65, 49.24, 15, 353, 48.5, geometry)
            }, transform, geometry);
    }
    private static double T(EndPlateInitialStiffnessResult r, string n) => r.Trace.Single(v => v.Name == n).Value;
    private static void Close(double e, double a) => Assert.AreEqual(e, a, Math.Max(1, Math.Abs(e)) * 1e-10);

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void PublicJrcInitialStiffnessNeedsNoArtificialCapacityOrLoad(bool ntc)
    {
        var r = EndPlateInitialStiffness.Calculate(Jrc(ntc)); Assert.IsTrue(r.IsSupported, string.Join(";", r.Diagnostics));
        Close(76552582907.97441, r.InitialStiffness!.Value);
        Close(466.5802716683375, r.EquivalentLeverArm!.Value); Close(5.2338398814415354, r.EquivalentRowCoefficient!.Value);
        Close(4495.02895553304, T(r, "ColumnShearArea")); Close(7.520520576131687, T(r, "K2"));
        // Published75.214 uses rounded coefficients and the permitted approximate panel arm.
        Assert.AreEqual(75.214e9, r.InitialStiffness.Value, 1.5e9);
        Assert.IsFalse(r.Trace.Any(v => v.Name.Contains("Moment") || v.Name.Contains("Resistance") || v.Name.Contains("Rotation")));
        Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [DataTestMethod] [DataRow(0.0)] [DataRow(.5)] [DataRow(1.0)] [DataRow(2.0)]
    public void ExistingQualifiedKernelAndInitialOnlyPathRetainIdenticalArithmetic(double beta)
    {
        var p = Jrc(beta: beta, config: ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths);
        var initial = EndPlateInitialStiffness.Calculate(p);
        // Genuine explicit test capacities for the OLD contract only; new input has none.
        var existing = EndPlateRotationalStiffness.Calculate(new(p.Code, p.Column, p.CompressionWidth,
            p.Configuration, p.Beta, p.Rows, 0, 1e6, 100e6, 300e6, p.TransformationSource,
            "External capacities for isolated legacy-kernel comparison", p.JointGeometrySource));
        Assert.IsTrue(existing.IsSupported); Assert.IsTrue(initial.IsSupported);
        Assert.AreEqual(existing.InitialStiffness, initial.InitialStiffness);
        Assert.AreEqual(existing.EquivalentLeverArm, initial.EquivalentLeverArm);
        foreach (var value in initial.Trace)
            Assert.AreEqual(existing.Trace.Single(v => v.Name == value.Name).Value, value.Value, value.Name);
    }

    [TestMethod]
    public void InputAndResultDoNotExposeSecantMomentResistanceOrRotation()
    {
        foreach (var type in new[] { typeof(EndPlateInitialStiffnessInput), typeof(EndPlateInitialStiffnessResult) })
            foreach (string name in new[] { "JointMomentResistance", "BeamPlasticAxialResistance", "Moment", "SecantStiffness", "ModelRotation" })
                Assert.IsNull(type.GetProperty(name), type.Name + "." + name);
    }

    [DataTestMethod]
    [DataRow("geometry", "JointGeometrySourceRequired")]
    [DataRow("transform", "TransformationSourceRequired")]
    [DataRow("lengths", "ColumnRowAndGroupLengthsRequired")]
    [DataRow("thickness", "OneEndPlateMustHaveOneThickness")]
    [DataRow("bolt", "BoltLengthMustIncludePlatesAndAdditionalHardware")]
    [DataRow("lever", "RowsMustDecreaseInLeverArm")]
    [DataRow("beta", "SingleSidedJointRequiresBetaOne")]
    [DataRow("config", "UnequalBeamDepthsOutsidePanelModel")]
    [DataRow("overflow", "OutsideNumericRange")]
    public void InvalidGeometryOrMissingEvidenceDoesNotYieldInitialStiffness(string failure, string issue)
    {
        var p = Jrc(geometry: failure == "geometry" ? "" : "Actual detail", transform: failure == "transform" ? "" : "beta",
            lengths: failure == "lengths" ? "" : "Qualified lengths", thickness: failure == "thickness" ? 16 : 15,
            boltLength: failure == "bolt" ? 31.5 : 48.5, firstLever: failure == "lever" ? 400 : 527,
            beta: failure == "beta" ? 0 : 1, plateM: failure == "overflow" ? 1e-308 : null,
            config: failure == "config" ? ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths : ColumnWebPanelConfiguration.SingleSidedJoint);
        var r = EndPlateInitialStiffness.Calculate(p);
        Assert.IsFalse(r.IsSupported); Assert.IsNull(r.InitialStiffness); Assert.IsNull(r.EquivalentLeverArm);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void CompressionWidthDerivedForAnotherColumnCannotBeReboundByEqualNumbers()
    {
        var p = Jrc(); var c = p.Column;
        var other = new ConnectionRolledISection(c.Height, c.Width, c.WebThickness, c.FlangeThickness, c.RootRadius, c.Area, c.Fy, c.Fu);
        var width = ColumnWebEffectiveWidth.FromBoltedEndPlate(other, 16, 8, 15, 50, "Actual spread");
        var r = EndPlateInitialStiffness.Calculate(new(p.Code, c, width, p.Configuration, p.Beta, p.Rows, "beta", "detail"));
        Assert.IsFalse(r.IsSupported); Assert.IsTrue(r.Diagnostics.Contains("CompressionWidthDerivedForAnotherColumn"));
    }

    [TestMethod]
    public void ConstructorRetainsSnapshotOfRowCollectionAndRejectsEmptyOrNullRows()
    {
        var p = Jrc(); var rows = p.Rows.ToArray();
        var q = new EndPlateInitialStiffnessInput(p.Code, p.Column, p.CompressionWidth, p.Configuration, p.Beta, rows, "beta", "geometry");
        rows[0] = rows[1]; Assert.AreEqual(527, q.Rows[0].LeverArm);
        Assert.ThrowsException<ArgumentException>(() => new EndPlateInitialStiffnessInput(p.Code, p.Column, p.CompressionWidth,
            p.Configuration, p.Beta, Array.Empty<EndPlateStiffnessRow>(), "beta", "geometry"));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateInitialStiffness.Calculate(null!));
    }
}
