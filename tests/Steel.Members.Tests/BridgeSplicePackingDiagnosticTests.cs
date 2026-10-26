using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSplicePackingDiagnosticTests
{
    [DataTestMethod][DataRow(1d)][DataRow(-1d)][DataRow(0d)]
    public void DiagnosticStrainsUseActualPackedFaceCoordinates(double scale)
    {
        var baseDetail = BridgeSpliceUnequalWidthTests.Detail(360, new(2e5 * scale, 0, 0, 0, 0, 50e6 * scale));
        var packing = WeldedSplicePacking.FromLayers(new[] { 12d, 8d }, new[] { 3d, 2d }, new[] { 10d }, Array.Empty<double>(), Array.Empty<double>(), "Detail");
        var d = new WeldedIBeamSpliceForceInput(new(baseDetail.Section.TopFlange, new(360, 16, 355, 510), baseDetail.Section.BottomFlange, 8, 8),
            baseDetail.Top, baseDetail.Bottom, baseDetail.EachWebCover, baseDetail.WebBolts, baseDetail.WebCoverEnd, baseDetail.ButtGap, baseDetail.Actions, packing);
        var p = WeldedIBeamSpliceStandardTests.Input(detail: d);
        var r = WeldedIBeamSpliceDetailing.Verify(p);
        Assert.IsTrue(r.Detailing.IsComplete, string.Join(";", r.Detailing.Diagnostics));
        var forces = WeldedIBeamSpliceForces.Calculate(d);
        foreach (bool top in new[] { true, false })
        {
            var c = top ? d.Top : d.Bottom; var parent = top ? d.Section.TopFlange : d.Section.BottomFlange;
            var name = top ? "Top" : "Bottom"; double sign = top ? 1 : -1;
            // Independent two-line interpolation at physical cover centroids, mm.
            double yo = sign * (parent.Thickness / 2 + c.Outer.Thickness / 2 + (top ? 20 : 10));
            double yi = -sign * (parent.Thickness / 2 + c.EachInner.Thickness / 2 + (top ? 5 : 0));
            double ao = c.Outer.Width * c.Outer.Thickness, ai = 2 * c.EachInner.Width * c.EachInner.Thickness;
            double fo = forces.Trace.Single(t => t.Name == "Left." + name + ".OuterForce").Value;
            double fi = forces.Trace.Single(t => t.Name == "Left." + name + ".BothInnerForce").Value;
            double eo = fo / (210000 * ao), ei = fi / (210000 * ai), k = (eo - ei) / (yo - yi);
            Close((ao * yo + ai * yi) / (ao + ai), D("AreaCentroidOffset"));
            Close(k, D("FaceStrainGradient"));
            Close(eo - k * yo - (fo + fi) / (210000 * parent.Width * parent.Thickness), D("AtParentCentroidStrainJump"));
            double D(string key) => r.ElasticFaceDiagnostics.Single(t => t.Name == name + "." + key).Value;
        }
        // Cover-to-nearest-contact offsets remain half the cover thickness: the
        // packing changes parent/cover centroids, not the cover's own contact face.
        foreach (var t in r.FaceTransfers)
            Close(t.Cover == WeldedSpliceCover.WebNegative || t.Cover == WeldedSpliceCover.WebPositive ? 12.5 : 10, Math.Abs(t.ContactOffsetW));
        Assert.IsFalse(r.CanQualifyLocalTransfer);
    }
    static void Close(double a, double b) => Assert.AreEqual(a, b, Math.Max(1e-14, Math.Abs(a) * 1e-10));
}
