using GPC.Design.Sections;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class ConcreteSectionTemplateChecks
{
    private static ConcreteTemplateInput Input(ConcreteTemplateKind kind) => new(kind,
        1000, 1200, 300, 300, 800, 250, 400, 500, 35, 20, 180, 24, 16, 10, 150);

    [TestMethod]
    public void AreasAndCentroidsHaveIndependentReferences()
    {
        foreach (var (kind, expected) in new[] {
            (ConcreteTemplateKind.I, 1000d * 300 + 800 * 250 + 300 * (1200 - 300 - 250)),
            (ConcreteTemplateKind.C, 1000d * 300 + 800 * 250 + 300 * (1200 - 300 - 250)),
            (ConcreteTemplateKind.L, 1000d * 300 + 300 * (1200 - 300)),
            (ConcreteTemplateKind.HollowRectangle, 1000d * 1200 - 400 * 500) })
        {
            var geometry = ConcreteSectionTemplates.Create(Input(kind));
            Assert.AreEqual(expected, geometry.Area, 1e-7, kind.ToString());
            Assert.IsTrue(geometry.Bars.Count > 8);
            Assert.IsTrue(geometry.Links.Count >= 2);
            Assert.IsTrue(geometry.Fibers.Count > 0);
            double area = 0, sx = 0, sy = 0;
            foreach (var (polygon, sign) in new[] { (geometry.Shape.Fill2d, 1d) }
                .Concat((geometry.Shape.Holes2d ?? []).Select(h => (h, -1d))))
            {
                double a = sign * Math.Abs(polygon.GetSignedArea());
                area += a; sx += a * polygon.GetCentroid().X; sy += a * polygon.GetCentroid().Y;
            }
            Assert.AreEqual(0, sx / area, 1e-8);
            Assert.AreEqual(0, sy / area, 1e-8);
            CheckPaths(geometry, 35 + 10d / 2);
        }
    }

    [TestMethod]
    public void CurvesUseRadialBarsAndRealCovers()
    {
        foreach (var kind in new[] { ConcreteTemplateKind.Ellipse, ConcreteTemplateKind.Stadium, ConcreteTemplateKind.HollowCircle })
        {
            var input = Input(kind) with { Height = kind == ConcreteTemplateKind.HollowCircle ? 1000 : 1600, InnerHeight = 400 };
            var geometry = ConcreteSectionTemplates.Create(input);
            Assert.AreEqual(kind == ConcreteTemplateKind.HollowCircle ? 40 : 24, geometry.Bars.Count);
            for (int i = 0; i < 24; i++)
            {
                var bar = geometry.Bars[i]; double angle = 2 * Math.PI * i / 24;
                Assert.AreEqual(0, bar.X * Math.Sin(angle) - bar.Y * Math.Cos(angle), 1e-7);
                Assert.AreEqual(Math.PI * 100, bar.Area, 1e-10);
            }
            double expected = kind switch
            {
                ConcreteTemplateKind.Ellipse => Math.PI * 500 * 800,
                ConcreteTemplateKind.Stadium => Math.PI * 500 * 500 + 1000d * 600,
                _ => Math.PI * (500d * 500 - 200 * 200)
            };
            // Native curved sections use polygon discretization; the comparison is deliberately analytic.
            Assert.AreEqual(expected, geometry.Area, expected * .01);
            CheckPaths(geometry, 40);
        }
    }

    private static void CheckPaths(ConcreteTemplateGeometry geometry, double cover)
    {
        foreach (var link in geometry.Links)
            for (int i = 0; i < link.Points.Count; i++)
            {
                var a = link.Points[i]; var b = link.Points[(i + 1) % link.Points.Count];
                for (int j = 0; j <= 100; j++)
                {
                    var point = new Point2d(a.X + (b.X - a.X) * j / 100, a.Y + (b.Y - a.Y) * j / 100);
                    Assert.IsTrue(geometry.Shape.Fill2d.IsPointInside(point, 0));
                    Assert.IsTrue(geometry.Shape.Fill2d.DistanceTo(point) >= cover - 2e-5);
                    foreach (var hole in geometry.Shape.Holes2d ?? [])
                    {
                        Assert.IsFalse(hole.IsPointInside(point, 0));
                        Assert.IsTrue(hole.DistanceTo(point) >= cover - 2e-5);
                    }
                }
            }
    }

    [TestMethod]
    public void InvalidAndUnbuildableLayoutsAreRefused()
    {
        var p = Input(ConcreteTemplateKind.C);
        foreach (var bad in new[] { p with { Width = double.NaN }, p with { Web = 0 },
            p with { Cover = 200 }, p with { TopFlange = 1000 }, p with { BarDiameter = 300 },
            p with { RadialCount = 0 }, p with { CurveSides = 33 },
            Input(ConcreteTemplateKind.HollowRectangle) with { InnerWidth = 990 },
            Input(ConcreteTemplateKind.HollowCircle) with { Height = 1000, InnerHeight = 400, InnerWidth = 999 } })
            Assert.ThrowsException<ArgumentException>(() => ConcreteSectionTemplates.Create(bad));
    }
}
