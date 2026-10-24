using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Persistence;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Physical replacement of the five native snapshots, approved 24/10/2026.
    /// Reads only the archived INPUT columns. No archived result is replaced or used
    /// as an independent oracle. The independent stress law and polygon integrals
    /// below never call a constitutive/force integration method of Checker or Model.
    /// </summary>
    [TestClass]
    public class CurvaturePhysicalContractTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static double D(string s) => double.Parse(s, Inv);
        private static void Close(double expected, double actual, double tolerance, string what)
        {
            Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), what);
            Assert.AreEqual(expected, actual, tolerance, what);
        }

        internal static void VerifyHistoricalCase(int caseId)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            var rows = File.ReadAllLines(Path.Combine(folder, "curvature-legacy.csv"))
                .Where(l => !l.StartsWith("#") && l.Length > 0).Skip(1).ToArray();
            Assert.AreEqual(5, rows.Length);
            var c = rows.Single(l => int.Parse(l.Split(';')[0], Inv) == caseId).Split(';');
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "detailing-sections.xml"))) archive = ModelArchive.Load(stream);
            var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
            var standard = ServiceabilityMigrationTests.Standard(c[2]);
            foreach (var pair in c[3].Split(','))
            { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
            double[] Parts(string s) => s.Split(',').Select(D).ToArray();
            var o = Parts(c[4]); var a = Parts(c[5]); var b = Parts(c[6]);
            // All five archived inputs use the physical solver directions -X,-Y.
            // Retain the actual origin (including the round-off-sized C400 offset).
            CollectionAssert.AreEqual(new[] { -1.0, 0, 0 }, a);
            CollectionAssert.AreEqual(new[] { 0.0, -1, 0 }, b);
            var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]),
                new Vector3d(a[0], a[1], a[2]), new Vector3d(b[0], b[1], b[2]));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes,
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            var request = new MomentCurvatureRequest(D(c[8]) * 1000, D(c[9]), int.Parse(c[10], Inv),
                D(c[11]), bool.Parse(c[12]), D(c[13]) * 1000, int.Parse(c[14], Inv));
            var curve = MomentCurvatureAnalysis.Calculate(request, checker, section, axes, D(c[7]));
            string id = "historical input " + caseId + " / " + c[1];
            Assert.IsNull(curve.InterruptedAtStep, id + ": " + curve.Status);
            Assert.IsNull(curve.InterruptionMessage, id);
            Assert.IsTrue(curve.LimitMoment > 0, id);
            Assert.IsTrue(curve.Points.Count >= request.Steps + 1, id);
            Assert.AreEqual(1, curve.Points.Count(p => p.Limit), id);
            Assert.IsTrue(curve.Points.Last().Limit, id);
            Close(curve.Points.Last().Curvature, curve.UltimateCurvature.Value, 0, id + " ultimate chi");
            Close(curve.LimitAxialForce - request.AxialForce, curve.AxialResidual, 0, id + " axial residual");
            Assert.IsTrue(Math.Abs(curve.AxialResidual) <= request.AxialTolerance, id);

            var material = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
            Assert.AreEqual(ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle, material.CompressionStressStrainDiagram);
            Assert.AreEqual(-.002, material.StrainYCompression); Assert.AreEqual(-.0035, material.StrainUCompression);
            double fck = Math.Abs(material.Fck), fcd = standard.AlphaCC * fck / standard.GammaC;
            double fyd = 450 / standard.GammaS, ey = fyd / 200000;
            Close(ey, D(c[7]), 1e-18, id + " archived yield strain and units");
            foreach (var bar in section.Rebars)
            {
                Assert.AreEqual(0, bar.EpsilonP); Assert.AreEqual(200000, bar.RebarMaterial.ElasticModulusTension);
                Assert.AreEqual(450, bar.RebarMaterial.Fyk); Assert.AreEqual(450, bar.RebarMaterial.Fu);
                Assert.AreEqual(.1, bar.RebarMaterial.StrainUTension);
            }
            var outline = section.ConcreteShape.Fill2d.Points.Select(v => new P(v.X, v.Y)).ToArray();
            var polygons = c[1] == "T1200x800"
                ? new[] { Rectangle(-600, 600, 44.230769230769226, 294.2307692307692),
                    Rectangle(-200, 200, -505.7692307692308, 44.230769230769226) }
                : new[] { outline };
            // C400 is the archived 32-sided polygon, not an ideal circle. T is two
            // nonoverlapping rectangles, preventing concave polygon clipping ambiguity.
            if (c[1] == "C400") Assert.AreEqual(32, outline.Length);
            Close(Math.Abs(Area(outline)), polygons.Sum(p => Math.Abs(Area(p))), 1e-7, id + " oracle geometry area");
            double width = outline.Max(v => v.X) - outline.Min(v => v.X);
            double height = outline.Max(v => v.Y) - outline.Min(v => v.Y);
            double scaleN = width * height * fck, scaleM1 = scaleN * height, scaleM2 = scaleN * width;
            // These are numerical discretization DATA. We sum explicit independent
            // sigma(epsilon), never call GaussIntegration.Integration* or solver resultants.
            // Their integration error is measured against exact clipped-polynomial I.
            var quadrature = GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(section.Mesh,
                QuadrangleGaussPoints.GaussPointNumber.Quad400, TriangleGaussPoints.GaussPointNumber.Tri79);
            var nodes = quadrature.SelectMany(g => g).Select(g => new Q(g.GpX, g.GpY, g.EvaluateFunction((x, y) => 1.0))).ToArray();
            Close(polygons.Sum(p => Math.Abs(Area(p))), nodes.Sum(g => g.W), 1e-8 * width * height, id + " quadrature area");
            double cx = Math.Cos(request.DirectionDegrees * Math.PI / 180), sy = Math.Sin(request.DirectionDegrees * Math.PI / 180);
            int firstYield = -1;
            var maxQuadratureError = new double[3]; var maxErrorPoint = new int[3];
            for (int i = 0; i < curve.Points.Count; i++)
            {
                var p = curve.Points[i]; string pointId = id + " point " + i;
                Assert.IsTrue(i == 0 || p.Moment > curve.Points[i - 1].Moment, pointId + " increasing branch");
                Close(p.Moment * cx, p.Mx, 1e-9, pointId + " M1 direction/units Nmm");
                Close(p.Moment * sy, p.My, 1e-9, pointId + " M2 direction/units Nmm");
                Close(Math.Sqrt(p.GradientX * p.GradientX + p.GradientY * p.GradientY), p.Curvature, 1e-18, pointId + " chi 1/mm");
                var plane = new Plane(p.ReferenceStrain, p.GradientX, p.GradientY, o[0], o[1]);
                double maxBar = section.Rebars.Max(v => Math.Abs(plane.At(v.Position.X, v.Position.Y)));
                double compression = Math.Max(0, -outline.Min(v => plane.At(v.X, v.Y)));
                Close(maxBar, p.SteelStrain, 1e-14, pointId + " independent bar strain");
                Close(compression, p.ConcreteCompressionStrain, 1e-14, pointId + " independent concrete strain");
                Assert.AreEqual(maxBar >= ey, p.Yielded, pointId + " first yield classification");
                if (p.Yielded && firstYield < 0) firstYield = i;
                Assert.IsTrue(compression <= .0035 + 1e-12, pointId + " concrete admissibility");
                Assert.IsTrue(maxBar <= .09 + 1e-12, pointId + " steel design strain limit");
                if (p.Limit) Close(.0035, compression, 1e-12, pointId + " active concrete ultimate limit");

                var exact = new F();
                foreach (var polygon in polygons) exact.Add(Integrate(polygon, plane, fcd));
                var numerical = new F();
                foreach (var node in nodes)
                {
                    double force = node.W * Concrete(plane.At(node.X, node.Y), fcd);
                    numerical.Add(force, -force * (node.Y - o[1]), force * (node.X - o[0]));
                }
                foreach (var bar in section.Rebars)
                {
                    double strain = plane.At(bar.Position.X, bar.Position.Y);
                    double stress = Math.Max(-fyd, Math.Min(fyd, 200000 * strain));
                    double force = (stress - Concrete(strain, fcd)) * bar.Area;
                    double m1 = -force * (bar.Position.Y - o[1]), m2 = force * (bar.Position.X - o[0]);
                    exact.Add(force, m1, m2); numerical.Add(force, m1, m2);
                }
                var integrationError = new[] { Math.Abs(exact.N - numerical.N), Math.Abs(exact.M1 - numerical.M1), Math.Abs(exact.M2 - numerical.M2) };
                for (int j = 0; j < 3; j++)
                    if (integrationError[j] > maxQuadratureError[j]) { maxQuadratureError[j] = integrationError[j]; maxErrorPoint[j] = i; }
                // Stopping tolerance of the stress solver is 1e-5 of its force scales.
                // At the limit use the existing public N/angular/domain tolerances;
                // projection onto the prescribed direction loses at most M*sin(angleTol).
                double nTolerance = p.Limit ? Math.Min(request.AxialTolerance,
                    DomainPointAxialTolerance.Calculate(checker.SectionSolver, request.AxialForce)) : 1e-5 * scaleN;
                double mTolerance1 = (p.Limit ? checker.SectionSolver.FailureAnalysisDistanceTolerance : 1e-5) * scaleM1;
                double mTolerance2 = (p.Limit ? checker.SectionSolver.FailureAnalysisDistanceTolerance : 1e-5) * scaleM2;
                if (p.Limit)
                {
                    // Exterior nodes are quadrature data, not physical material points.
                    // Record the finite-support effect explicitly; it is part of |I-Q|.
                    var outsideMaterial = nodes.Where(q => plane.At(q.X, q.Y) < -.0035).ToArray();
                    Console.WriteLine(string.Format(Inv,
                        "{0}; ultimate quadrature nodes beyond ecu={1}; minimum sampled strain={2:R}",
                        id, outsideMaterial.Length, nodes.Min(q => plane.At(q.X, q.Y))));
                    double dot = numerical.M1 * cx + numerical.M2 * sy;
                    double cross = numerical.M2 * cx - numerical.M1 * sy;
                    Assert.IsTrue(dot > 0, pointId + " independent limit direction");
                    Assert.IsTrue(Math.Abs(Math.Atan2(cross, dot)) <= checker.SectionSolver.FailureAnalysisAngularTolerance + 1e-12,
                        pointId + " independent limit angular residual");
                    double angular = Math.Sqrt(numerical.M1 * numerical.M1 + numerical.M2 * numerical.M2)
                        * Math.Sin(checker.SectionSolver.FailureAnalysisAngularTolerance);
                    mTolerance1 += angular; mTolerance2 += angular;
                }
                // Round-off allowance, independent of the response discrepancy, below
                // 1/100000 of the nonlinear stopping tolerance in each scaled component.
                nTolerance += 1e-10 * scaleN; mTolerance1 += 1e-10 * scaleM1; mTolerance2 += 1e-10 * scaleM2;
                Close(request.AxialForce, numerical.N, nTolerance, pointId + " independent discrete equilibrium N");
                Close(p.Mx, numerical.M1, mTolerance1, pointId + " independent discrete equilibrium M1");
                Close(p.My, numerical.M2, mTolerance2, pointId + " independent discrete equilibrium M2");
                // Error budget = numerical stopping + independently evaluated fixed
                // quadrature error. It is NOT fitted from the solver's returned forces.
                Close(request.AxialForce, exact.N, nTolerance + Math.Abs(exact.N - numerical.N), pointId + " exact polygon N");
                Close(p.Mx, exact.M1, mTolerance1 + Math.Abs(exact.M1 - numerical.M1), pointId + " exact polygon M1");
                Close(p.My, exact.M2, mTolerance2 + Math.Abs(exact.M2 - numerical.M2), pointId + " exact polygon M2");
            }
            Console.WriteLine(id + "; max exact-minus-fixed-quadrature errors: N=" + maxQuadratureError[0].ToString("G17", Inv)
                + " N at point " + maxErrorPoint[0] + "; M1=" + maxQuadratureError[1].ToString("G17", Inv)
                + " Nmm at point " + maxErrorPoint[1] + "; M2=" + maxQuadratureError[2].ToString("G17", Inv)
                + " Nmm at point " + maxErrorPoint[2]);
            Console.WriteLine(id + "; native Mlim=" + curve.LimitMoment.ToString("G17", Inv)
                + " Nmm; chiU=" + curve.UltimateCurvature.Value.ToString("G17", Inv)
                + " 1/mm; chiY=" + curve.YieldCurvature.Value.ToString("G17", Inv) + " 1/mm");
            Assert.IsTrue(firstYield > 0, id + " first yield must lie on the branch");
            Close(curve.Points[firstYield].Curvature, curve.YieldCurvature.Value, 0, id + " yield curvature");
            Assert.IsNotNull(curve.YieldRefinement, id);
            Assert.IsTrue(curve.YieldRefinement.Applied, id);
            Close(curve.Points[firstYield].Moment, curve.YieldRefinement.Moment.Value, 0, id + " refined yield moment");
            Assert.IsTrue(curve.UltimateCurvature >= curve.YieldCurvature, id);
        }

        internal sealed class P { internal readonly double X, Y; internal P(double x, double y) { X = x; Y = y; } }
        private sealed class Q { internal readonly double X, Y, W; internal Q(double x, double y, double w) { X = x; Y = y; W = w; } }
        internal sealed class Plane
        {
            internal readonly double E0, Gx, Gy, Ox, Oy;
            internal Plane(double e0, double gx, double gy, double ox = 0, double oy = 0) { E0 = e0; Gx = gx; Gy = gy; Ox = ox; Oy = oy; }
            internal double At(double x, double y) => E0 + Gx * (x - Ox) + Gy * (y - Oy);
        }
        internal sealed class F
        {
            internal double N, M1, M2;
            internal void Add(double n, double m1, double m2) { N += n; M1 += m1; M2 += m2; }
            internal void Add(F f) => Add(f.N, f.M1, f.M2);
        }
        internal static P[] Rectangle(double x0, double x1, double y0, double y1)
            => new[] { new P(x0, y0), new P(x1, y0), new P(x1, y1), new P(x0, y1) };
        internal static double Area(P[] p)
        {
            double sum = 0; for (int i = 0; i < p.Length; i++) { var a = p[i]; var b = p[(i + 1) % p.Length]; sum += a.X * b.Y - b.X * a.Y; }
            return sum / 2;
        }
        internal static double Concrete(double e, double fcd)
        {
            // Same finite material support as the archived input table: epsilon < ecu
            // is beyond the diagram. Tri79 has exterior nodes, so this branch matters
            // even when every physical section vertex satisfies epsilon >= ecu.
            if (e >= 0 || e < -.0035) return 0;
            if (e <= -.002) return -fcd;
            double u = -e / .002 * 8, low = Math.Floor(u), t = u - low;
            double a = low / 8, b = (low + 1) / 8;
            return -fcd * ((1 - t) * (2 * a - a * a) + t * (2 * b - b * b));
        }
        private static P[] Clip(P[] polygon, Plane plane, double level, bool above)
        {
            var kept = new List<P>();
            if (polygon.Length == 0) return kept.ToArray();
            var previous = polygon.Last(); double fp = plane.At(previous.X, previous.Y) - level;
            foreach (var current in polygon)
            {
                double fc = plane.At(current.X, current.Y) - level;
                bool pIn = above ? fp >= 0 : fp <= 0, cIn = above ? fc >= 0 : fc <= 0;
                if (pIn != cIn)
                {
                    double t = fp / (fp - fc);
                    kept.Add(new P(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y)));
                }
                if (cIn) kept.Add(current);
                previous = current; fp = fc;
            }
            return kept.ToArray();
        }
        internal static F Integrate(P[] polygon, Plane plane, double fcd)
        {
            var total = new F();
            // A constant plane exactly on a stress knot belongs to a single band;
            // without this branch both closed half-planes would contain the full area.
            if (plane.Gx == 0 && plane.Gy == 0)
            {
                double area = Area(polygon), sx = 0, sy = 0;
                for (int i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                    double cross = a.X * b.Y - b.X * a.Y;
                    sx += (a.X + b.X) * cross / 6; sy += (a.Y + b.Y) * cross / 6;
                }
                double sign = Math.Sign(area), stress = Concrete(plane.E0, fcd);
                total.Add(stress * sign * area, -stress * sign * (sy - plane.Oy * area),
                    stress * sign * (sx - plane.Ox * area));
                return total;
            }
            // Clip into every affine stress branch including the finite plateau.
            // Beyond ecu the material table defines zero, not a continued plateau.
            for (int band = 0; band < 9; band++)
            {
                double high = -band * .002 / 8;
                var clipped = Clip(polygon, plane, high, false);
                clipped = Clip(clipped, plane, band < 8 ? -(band + 1) * .002 / 8 : -.0035, true);
                if (clipped.Length < 3) continue;
                if (Area(clipped) < 0) Array.Reverse(clipped);
                // Three barycentric points integrate a quadratic exactly: sigma is
                // affine in each clipped strip, sigma*x and sigma*y are quadratic.
                for (int i = 1; i + 1 < clipped.Length; i++)
                {
                    var a = clipped[0]; var b = clipped[i]; var c = clipped[i + 1];
                    double ar = ((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2;
                    var vertices = new[] { a, b, c };
                    for (int j = 0; j < 3; j++)
                    {
                        double x = (a.X + b.X + c.X) / 6 + vertices[j].X / 2;
                        double y = (a.Y + b.Y + c.Y) / 6 + vertices[j].Y / 2;
                        double force = ar / 3 * Concrete(plane.At(x, y), fcd);
                        total.Add(force, -force * (y - plane.Oy), force * (x - plane.Ox));
                    }
                }
            }
            return total;
        }

        [DataTestMethod]
        [DataRow(-.0035, -17.0)]
        [DataRow(-.0035000000001, 0.0)]
        [DataRow(-.004, 0.0)]
        public void IndependentConcreteLawHasFiniteUltimateSupport(double strain, double stress)
        {
            Close(stress, Concrete(strain, 17), 0, "explicit finite constitutive table");
            var result = Integrate(Rectangle(-150, 150, -250, 250), new Plane(strain, 0, 0), 17);
            Close(stress * 150000, result.N, 1e-7, "uniform plane beyond the diagram");
            Close(0, result.M1, 1e-7, "uniform plane M1"); Close(0, result.M2, 1e-7, "uniform plane M2");
        }

        [TestMethod]
        public void IndependentExactIntegralClipsUltimateCutoffRatherThanExtendingPlateau()
        {
            // eps=-.0035+1e-5*y: only y>=0 has nonzero stress on this 2x2mm square.
            // The upper half remains on the plateau: area2mm2, Sy=1mm3.
            var result = Integrate(Rectangle(-1, 1, -1, 1), new Plane(-.0035, 0, 1e-5), 17);
            Close(-34, result.N, 1e-11, "finite plateau area");
            Close(17, result.M1, 1e-11, "finite plateau first moment");
            Close(0, result.M2, 1e-11, "finite plateau symmetry");
        }

        [TestMethod]
        public void IndependentIntegratorMatchesUniformAndAffineClosedForms()
        {
            var rectangle = Rectangle(-150, 150, -250, 250);
            var uniform = Integrate(rectangle, new Plane(-.003, 0, 0), 17);
            Close(-17 * 150000, uniform.N, 1e-7, "uniform N");
            Close(0, uniform.M1, 1e-5, "uniform M1"); Close(0, uniform.M2, 1e-5, "uniform M2");
            // Entire section lies in the FIRST affine segment: slope=fcd*1.875/.002.
            double slope = 17 * 1.875 / .002;
            var affine = Integrate(rectangle, new Plane(-.000125, 1e-7, -2e-7), 17);
            Close(slope * -.000125 * 150000, affine.N, 1e-7, "affine N");
            Close(-slope * -2e-7 * 300 * Math.Pow(500, 3) / 12, affine.M1, 1e-5, "affine M1");
            Close(slope * 1e-7 * 500 * Math.Pow(300, 3) / 12, affine.M2, 1e-5, "affine M2");
        }

        [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(4)] [DataRow(8)]
        public void UniformPlaneAtAStressKnotIsNotCountedTwice(int knot)
        {
            double q = knot / 8.0, sigma = -17 * (2 * q - q * q);
            var result = Integrate(Rectangle(-150, 150, -250, 250), new Plane(-.002 * q, 0, 0), 17);
            Close(sigma * 150000, result.N, 1e-7, "stress knot N");
            Close(0, result.M1, 1e-7, "stress knot M1"); Close(0, result.M2, 1e-7, "stress knot M2");
        }

        [DataTestMethod] [DataRow(false)] [DataRow(true)]
        public void IndependentIntegratorMatchesExactEightSegmentCompressionBlock(bool reverse)
        {
            // Compression depth200mm: plateau=600/7? With epsTop=-.0035,
            // neutral axis x=200, plateau x*(1-.002/.0035)=600/7mm.
            // Exact trapezoids and their first moments are independent 1D statics.
            const double width = 300, height = 500, depth = 200, fcd = 17;
            double n = 0, m = 0, plateau = depth * (1 - .002 / .0035);
            double yTop = height / 2, end = plateau;
            n -= width * fcd * plateau; m += width * fcd * plateau * (yTop - plateau / 2);
            for (int i = 0; i < 8; i++)
            {
                double next = plateau + (i + 1) * (depth - plateau) / 8;
                double qa = 1 - i / 8.0, qb = 1 - (i + 1) / 8.0;
                double sa = fcd * (2 * qa - qa * qa), sb = fcd * (2 * qb - qb * qb);
                double l = next - end, force = width * l * (sa + sb) / 2;
                double first = width * (end * l * (sa + sb) / 2 + l * l * (sa + 2 * sb) / 6);
                n -= force; m += yTop * force - first; end = next;
            }
            var p = Rectangle(-150, 150, -250, 250); if (reverse) Array.Reverse(p);
            var result = Integrate(p, new Plane(-.0035 + .0035 * 250 / depth, 0, -.0035 / depth), fcd);
            Close(n, result.N, 1e-7, "compression block N"); Close(m, result.M1, 1e-5, "compression block M1");
            Close(0, result.M2, 1e-5, "compression block M2");
        }
    }
}
