using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferRoundedStripTests
{
    [DataTestMethod]
    [DataRow(true, 15d, 7.5)]
    [DataRow(false, 15d, 7.5)]
    [DataRow(true, 15.3, 7.7)]
    [DataRow(false, 15.3, 7.7)]
    public void ModelArcTangencyRoundoffDoesNotLoseOrEnlargeTheActualFlatStrip(
        bool verticalBacks, double rootRadius, double toeRadius)
    {
        var angle = new SectionL(verticalBacks ? 100 : 150, 10,
            verticalBacks ? 150 : 100, 10, "Actual rolled unequal angle", rootRadius, toeRadius);
        var pair = SectionBuiltUp.DoubleAngle(angle, 20, verticalBacks
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "Actual identical pair");
        var snapshot = new SteelSectionInput(new SteelSection(pair,
            new SteelMaterial("Actual S275", 210000, 275, 430)));
        var adapted = BraceAngleModelAdapter.FromPair(snapshot,
            ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit factors for actual rolled geometry"),
            new SteelActions(200000, 0, 0, 0, 0, 0), 100000, 100000,
            "Exact physical centroidal tension; no moment computed from rounded centroids",
            new ConnectionBolt(16, 18, 157, ConnectionBoltGrade.Class8_8, true), 4, 60, 75);
        var input = new BraceAnglePairBoltedTransferInput(adapted, new ConnectionPlate(200, 20, 275, 430),
            40, 40, 100, 30, false, false, "Actual curved-root and curved-toe rolled angle; hardware envelope",
            "Conditional plane model", "One concomitant axial case");

        // Locate the actual Model line independently, using a loose diagnostic
        // search only. Its REAL endpoint coordinates are compared below; this
        // tolerance does not enter any resistance or hardware-distance oracle.
        var copied = (SectionBuiltUp)snapshot.CreateSectionCopy().SectionShape;
        var lines = copied.Parts[0].Section.GetCurveOutlines()
            .SelectMany(o => Segments(o.Boundary)).OfType<LineCurve3d>()
            .Where(l => Math.Abs(Normal(l.StartPoint) - 10) < 1e-9
                && Math.Abs(Normal(l.EndPoint) - 10) < 1e-9).ToArray();
        Assert.AreEqual(1, lines.Length);
        var line = lines[0];
        double from = Math.Min(Along(line.StartPoint), Along(line.EndPoint));
        double to = Math.Max(Along(line.StartPoint), Along(line.EndPoint));
        Assert.AreEqual(10 + rootRadius, from, 1e-10);
        Assert.AreEqual(150 - toeRadius, to, 1e-10);
        double normalError = Math.Max(Math.Abs(Normal(line.StartPoint) - 10), Math.Abs(Normal(line.EndPoint) - 10));
        Assert.IsTrue(normalError <= 8 * 2.2204460492503131e-16 * 150);
        if (verticalBacks && rootRadius == 15)
            Assert.IsTrue(normalError > 0, "The original failing Model endpoint is not bit-exactly on X=10.");

        var result = BraceAnglePairBoltedTransfer.Verify(input);
        Assert.AreEqual(ConnectionStatus.Passed, result.InPlaneComponents.Status,
            string.Join("; ", result.InPlaneComponents.Diagnostics) + "; actual tangent normal error=" + normalError.ToString("R"));
        double Value(string name) => result.InPlaneComponents.Trace.Single(v => v.Name == name).Value;
        Assert.AreEqual(from, Value("AngleFlatStripLow"));
        Assert.AreEqual(to, Value("AngleFlatStripHigh"));
        Assert.AreEqual(75 - from, Value("AngleHeelTangentEdge"));
        Assert.AreEqual(to - 75, Value("AngleToeTangentEdge"));
        Assert.AreEqual(ConnectionStatus.NotSupported, result.LocalThreeDimensionalStatus);
        Assert.IsFalse(result.CanQualifyWholeJoint);
        double Normal(Point3d p) => verticalBacks ? p.X : p.Y;
        double Along(Point3d p) => verticalBacks ? p.Y : p.X;
    }

    private static IEnumerable<Curve3d> Segments(Curve3d curve)
    {
        if (curve is PolyCurve3d poly)
            for (int i = 0; i < poly.SegmentCount; i++)
                foreach (var child in Segments(poly.GetSegment(i))) yield return child;
        else yield return curve;
    }
}
