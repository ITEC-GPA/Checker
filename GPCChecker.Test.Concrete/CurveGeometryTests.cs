using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ConcreteTests
{
    [TestClass]
    public class CurveGeometryTests
    {
        private static ArcCurve3d Circle(double x, double y, double radius) =>
            new ArcCurve3d(new Point3d(x, y, 0), Vector3d.ZAxis, Vector3d.XAxis, radius, 2 * Math.PI);

        private static EllipseCurve3d Ellipse(double x, double y, double a, double b) =>
            new EllipseCurve3d(new Point3d(x, y, 0), Vector3d.ZAxis, Vector3d.XAxis, a, b);

        public static IEnumerable<object[]> GeometryCases()
        {
            // Five topologies at twenty scales/translations: 100 separately discovered cases.
            for (int family = 0; family < 5; family++)
                for (int variant = 0; variant < 20; variant++) yield return new object[] { family, variant };
        }

        [DataTestMethod, DynamicData(nameof(GeometryCases), DynamicDataSourceType.Method)]
        public void CurvesPreserveAreaHolesCoverAndCoordinates(int family, int variant)
        {
            double s = .25 + variant / 4.0, x = 137 * variant - 900, y = 70 - 61 * variant, tolerance = .02 * s;
            bool circular = family == 0 || family == 2, hole = family == 2 || family == 3;
            Curve3d boundary = circular ? (Curve3d)Circle(x, y, 500 * s) : Ellipse(x, y, 500 * s, 300 * s);
            if (family == 4) boundary = new PolylineCurve3d(new[] {
                new Point3d(x - 500*s, y - 300*s, 0), new Point3d(x + 500*s, y - 300*s, 0),
                new Point3d(x + 500*s, y + 300*s, 0), new Point3d(x - 500*s, y + 300*s, 0), new Point3d(x - 500*s, y - 300*s, 0) });
            var holes = !hole ? null : new Curve3d[] { circular ? (Curve3d)Circle(x, y, 300*s) : Ellipse(x, y, 250*s, 150*s) };
            var outline = new SectionCurveOutline(boundary, holes);
            var bar = new CrackBar(x + 440*s, y, 20*s, Math.PI*100*s*s);
            var geometry = CrackSectionGeometry.FromCurves(outline, new[] { bar }, tolerance, 100*s);
            double expected = family == 4 ? 600000*s*s : Math.PI*500*s*(circular ? 500 : 300)*s;
            if (hole) expected -= circular ? Math.PI*300*300*s*s : Math.PI*250*150*s*s;
            double area = CrackSectionGeometry.Area(geometry.Outline) - geometry.Holes.Sum(CrackSectionGeometry.Area);
            Assert.AreEqual(expected, area, expected*0.0003);
            Assert.AreEqual(hole ? 1 : 0, geometry.Holes.Count);
            Assert.AreEqual(circular, geometry.Circular);
            Assert.AreSame(bar, geometry.Bars[0]);
            Assert.AreEqual(50*s, geometry.BarCover(bar), 2*tolerance);
            // Odd segment counts need not produce symmetric polygons. Compare with the analytic
            // half area using a perimeter-times-chord bound, rather than assuming exact symmetry.
            Assert.AreEqual(expected / 2, geometry.Region("half", 1, 0, x, new[] { 0 }).Area, tolerance*2*Math.PI*800*s);
            Assert.AreEqual(1000*s, geometry.Width, 2*tolerance);
            Assert.IsTrue(geometry.Outline.All(p => p.X >= x - 500*s - tolerance && p.X <= x + 500*s + tolerance));
            Assert.IsTrue(geometry.BarCover(new CrackBar(x + 600*s, y, 20*s, 1)) < 0);
            if (hole) Assert.IsTrue(geometry.BarCover(new CrackBar(x, y, 20*s, 1)) < 0);
            for (int i = 0; i < geometry.Outline.Count; i++)
            {
                var a = geometry.Outline[i]; var b = geometry.Outline[(i + 1) % geometry.Outline.Count];
                Assert.IsTrue(Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)) <= 100*s + 1e-7);
            }
            // Sampling never replaces the owned analytic boundary in Model.
            Assert.AreEqual(boundary.GetType(), outline.Boundary.GetType());
        }

        private static CrackBar[] RingBars(double x, double y, double radius, int count) => Enumerable.Range(0, count)
            .Select(i => new CrackBar(x + radius*Math.Cos(.123 + 2*Math.PI*i/count), y + radius*Math.Sin(.123 + 2*Math.PI*i/count), 20, Math.PI*100)).ToArray();

        [TestMethod]
        public void TranslatedCircleUsesAnalyticCentreAndConcentricRingsRemainExplicit()
        {
            var outline = new SectionCurveOutline(Circle(700, -200, 500));
            var bars = RingBars(700, -200, 440, 16);
            var geometry = CrackSectionGeometry.FromCurves(outline, bars, .01);
            Assert.AreEqual(440*Math.PI/8, geometry.MaximumSpacing(Enumerable.Range(0, 16).ToArray()).Value, 1e-8);
            var twoRings = bars.Concat(RingBars(700, -200, 340, 8)).ToArray();
            Assert.IsNull(CrackSectionGeometry.FromCurves(outline, twoRings, .01).MaximumSpacing(new[] { 0, 1 }));
            Assert.AreEqual(440*Math.PI/8, CrackSectionGeometry.FromCurves(outline, twoRings, .01, concentricRings: true)
                .MaximumSpacing(new[] { 0, 1 }).Value, 1e-8);
            Assert.IsTrue(CrackSectionGeometry.FromCurves(new SectionCurveOutline(Ellipse(700, -200, 500, 500)), bars, .01).Circular);
        }

        [DataTestMethod]
        [DataRow(false, 0)] [DataRow(false, 1)] [DataRow(false, 2)]
        [DataRow(true, 0)] [DataRow(true, 1)] [DataRow(true, 2)]
        public void FullCrackCheckIsInvariantUnderTranslation(bool hollow, int strainCase)
        {
            SectionCrackResult Check(double x, double y)
            {
                var bars = RingBars(x, y, 440, 16).Concat(hollow ? RingBars(x, y, 340, 8) : new CrackBar[0]).ToArray();
                var geometry = CrackSectionGeometry.FromCurves(new SectionCurveOutline(Circle(x, y, 500),
                    hollow ? new[] { Circle(x, y, 300) } : null), bars, .05, concentricRings: hollow);
                double chiX = strainCase == 0 ? 0 : .000002, chiY = strainCase == 2 ? .000001 : 0;
                double strain = strainCase == 1 ? 0 : .002;
                var plane = new StrainPlane(chiX, chiY, new Point2d(x, y), strain);
                return SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard("EN 1992-1-1"),
                    ServiceabilityCombination.QuasiPermanent, "XC3", false, null, geometry, plane,
                    bars.Select(b => 200000*plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40,
                    spacingOverride: 300));
            }
            var original = Check(0, 0); var translated = Check(700, -200);
            Assert.IsTrue(original.Width.HasValue, original.Status);
            Assert.AreEqual(original.Outcome, translated.Outcome);
            Assert.AreEqual(original.Width.Value, translated.Width.Value, 1e-9);
            Assert.AreEqual(original.Regions.Count, translated.Regions.Count);
            for (int i = 0; i < original.Regions.Count; i++)
            {
                Assert.AreEqual(original.Regions[i].Key, translated.Regions[i].Key);
                Assert.AreEqual(original.Regions[i].Area, translated.Regions[i].Area, 1e-6);
                CollectionAssert.AreEqual(original.Regions[i].BarIndices.ToArray(), translated.Regions[i].BarIndices.ToArray());
            }
            if (hollow && strainCase == 0) Assert.IsTrue(translated.Regions.Any(r => r.Key == "InnerRing"));
        }

        [TestMethod]
        public void ModelFactoryPreservesBarOrderAndLeavesLegacyShapeUnchanged()
        {
            var material = new ConcreteMaterialEN1992("C25", 25, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
            var steel = new SteelMaterialEN1992("B450", 200000, 450, 540);
            var shape = new SectionCircular(1000);
            var section = new ReinforcedConcreteSection(shape, material);
            section.AddRebar(new ReinforcedConcreteRebar(new RebarSectionCircular(20, steel), new Point2d(900, 500)));
            section.AddRebar(new ReinforcedConcreteRebar(new RebarSectionCircular(16, steel), new Point2d(100, 500)));
            section.AddRebar(new ReinforcedConcreteRebar(new RebarSectionCircular(12, steel), new Point2d(500, 900), sigmaP: 100));
            var tendon = new SteelMaterialEN1992("Tendon", 195000, 1600, 1800, steelType: SteelMaterial.SteelTypes.Tendon);
            section.AddRebar(new ReinforcedConcreteRebar(new RebarSectionCircular(12, tendon), new Point2d(500, 100)));
            var legacy = section.ConcreteShape.Fill2d.Points.Select(p => new Point2d(p.X, p.Y)).ToArray();
            var coarse = CrackSectionGeometry.FromCurves(section, 1);
            var fine = CrackSectionGeometry.FromCurves(section, .01);
            Assert.IsTrue(coarse.Circular);
            Assert.AreEqual(2, fine.Bars.Count);
            Assert.AreEqual(900, fine.Bars[0].X); Assert.AreEqual(100, fine.Bars[1].X);
            Assert.IsTrue(fine.Outline.Count > coarse.Outline.Count);
            Assert.IsTrue(Math.Abs(CrackSectionGeometry.Area(fine.Outline) - Math.PI*250000) <
                Math.Abs(CrackSectionGeometry.Area(coarse.Outline) - Math.PI*250000));
            Assert.AreEqual(legacy.Length, section.ConcreteShape.Fill2d.Points.Count());
            for (int i = 0; i < legacy.Length; i++)
            {
                Assert.AreEqual(legacy[i].X, section.ConcreteShape.Fill2d.Points.ElementAt(i).X);
                Assert.AreEqual(legacy[i].Y, section.ConcreteShape.Fill2d.Points.ElementAt(i).Y);
            }
        }

        [DataTestMethod, DataRow(false), DataRow(true)]
        public void NonConcentricOrEllipticHolesDoNotUseCircularRingFormula(bool elliptic)
        {
            Curve3d hole = elliptic ? (Curve3d)Ellipse(0, 0, 250, 150) : Circle(40, 0, 250);
            var geometry = CrackSectionGeometry.FromCurves(new SectionCurveOutline(Circle(0, 0, 500), new[] { hole }), RingBars(0, 0, 440, 16), .05);
            var plane = new StrainPlane(0, 0, new Point2d(0, 0), .0005);
            var result = SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard("EN 1992-1-1"),
                ServiceabilityCombination.QuasiPermanent, "XC3", false, null, geometry, plane,
                geometry.Bars.Select(b => 100d), true, false, false, 200000, 33000, 2.9, false, true, 40, spacingOverride: 300));
            Assert.IsTrue(result.Status.Contains("implemented for one axis-aligned rectangular hole or a circular ring"), result.Status);
            Assert.IsFalse(result.Regions.Any(r => r.Key == "InnerRing"));
        }

        [TestMethod]
        public void InvalidToleranceIsRejectedAndChildrenAreNeverSilentlyLost()
        {
            // .NET Framework caches default exception messages at their first use. Match the
            // invariant UI culture of the contract tests, even when this test runs before them.
            var thread = Thread.CurrentThread; var uiCulture = thread.CurrentUICulture;
            try
            {
                thread.CurrentUICulture = CultureInfo.InvariantCulture;
                var outline = new SectionCurveOutline(Circle(0, 0, 500));
                foreach (double tolerance in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                    Assert.ThrowsException<ArgumentOutOfRangeException>(() => CrackSectionGeometry.FromCurves(outline, new CrackBar[0], tolerance));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => CrackSectionGeometry.FromCurves(outline, new CrackBar[0], .1, 0));
                Assert.ThrowsException<ArgumentNullException>(() => CrackSectionGeometry.FromCurves((ReinforcedConcreteSection)null, .1));
                Assert.ThrowsException<ArgumentNullException>(() => CrackSectionGeometry.FromCurves((SectionCurveOutline)null, new CrackBar[0], .1));
                Assert.ThrowsException<NotSupportedException>(() => CrackSectionGeometry.FromCurves(new SectionCurveOutline(Circle(0, 0, 500),
                    new[] { Circle(0, 0, 300) }, new[] { new SectionCurveOutline(Circle(0, 0, 100)) }), new CrackBar[0], .1));
                var tilted = new SectionCurveOutline(new ArcCurve3d(new Point3d(0, 0, 0), Vector3d.YAxis, Vector3d.XAxis, 500, 2*Math.PI));
                Assert.ThrowsException<ArgumentException>(() => CrackSectionGeometry.FromCurves(tilted, new CrackBar[0], .1));
            }
            finally { thread.CurrentUICulture = uiCulture; }
        }
    }
}
