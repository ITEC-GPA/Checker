using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ColumnBaseBearingGeometryTests
{
    // Independent circle/semicircle/quadrant area and first-moment identities, mm and mm3.
    [DataTestMethod]
    [DataRow(-20, 20, -20, 20, 1, 0)]
    [DataRow(-20, 20, 0, 20, .5, 2.0 / 3)]
    [DataRow(-20, 20, -20, 0, .5, -2.0 / 3)]
    [DataRow(0, 20, 0, 20, .25, 1.0 / 3)]
    [DataRow(-20, 0, -20, 0, .25, -1.0 / 3)]
    [DataRow(-20, 20, 10, 20, 0, 0)]
    [DataRow(11, 20, -20, 20, 0, 0)]
    public void DiskFractionsHaveTheAnalyticalAreaAndMoment(double xl, double xr, double yl, double yr,
        double fraction, double momentFactor)
    {
        ColumnBaseBearingGeometry.CircleIntersection(xl, xr, yl, yr, 0, 0, 10, out var area, out var moment);
        Assert.AreEqual(fraction * Math.PI * 100, area, 1e-10);
        Assert.AreEqual(momentFactor * 1000, moment, 1e-10);
    }

    [TestMethod]
    public void RectangleInsideDiskRetainsItsOwnAreaAndCentroid()
    {
        ColumnBaseBearingGeometry.CircleIntersection(-2, 3, -1, 4, 0, 0, 10, out var area, out var moment);
        Assert.AreEqual(25, area, 1e-10); Assert.AreEqual(37.5, moment, 1e-10);
    }

    [TestMethod]
    public void TranslatedHalfDiskUsesTheParallelAxisIdentity()
    {
        ColumnBaseBearingGeometry.CircleIntersection(-30, 30, 100, 120, 5, 100, 10, out var area, out var moment);
        Assert.AreEqual(50 * Math.PI, area, 1e-10);
        Assert.AreEqual(5000 * Math.PI + 2000.0 / 3, moment, 1e-9);
    }

    [TestMethod]
    public void SplittingAClippedDiskPreservesAreaAndFirstMoment()
    {
        ColumnBaseBearingGeometry.CircleIntersection(-4, 9, -8, 7, 0, 0, 10, out var area, out var moment);
        ColumnBaseBearingGeometry.CircleIntersection(-4, 9, -8, 2, 0, 0, 10, out var a1, out var m1);
        ColumnBaseBearingGeometry.CircleIntersection(-4, 9, 2, 7, 0, 0, 10, out var a2, out var m2);
        Assert.AreEqual(area, a1 + a2, 1e-10); Assert.AreEqual(moment, m1 + m2, 1e-10);
    }
}

public partial class ColumnBaseTests
{
    [TestMethod]
    public void HolesCentredOnBearingBoundaryRemoveOnlySemicirclesAtTheirCentroids()
    {
        // Same HEB240 as Example1, choose c=50 mm. Rectangle [-170,170] x [53,170].
        // Two holes r=13 at y=170 cut two lower semicircles: removed area = pi*r²,
        // removed first moment = pi*r²*170 - 4*r³/3. Using whole disks at y=170 is wrong.
        double thickness = 50 * Math.Sqrt(3 * (200.0 / 9) / 275);
        double aGross = 340 * 117, aNet = aGross - Math.PI * 169;
        double firstMoment = aGross * 111.5 - (Math.PI * 169 * 170 - 4 * 2197.0 / 3);
        var result = ColumnBase.Verify((Example1() with { PlateThickness = thickness }).Build());
        Assert.AreEqual(0, result.MissingRequirements.Count, string.Join(";", result.MissingRequirements));
        Close(aNet, Trace(result, "Compression.FlangeTStub.EffectiveArea"), "net bearing area");
        Close(firstMoment / aNet, result.CompressionLeverArm!.Value, "centroid after half-hole cuts");
        Close(aNet * 200 / 9, result.SideCompressionResistance!.Value, "bearing resistance");
    }
}
