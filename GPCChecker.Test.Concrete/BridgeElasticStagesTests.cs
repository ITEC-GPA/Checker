using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>Independent rectangle/point-bar oracles, not values copied from the implementation.
    /// N/mm/MPa; positive N tension, positive M compresses the upper fibre; y=0 at slab interface.</summary>
    [TestClass, TestCategory("BridgeStages"), DoNotParallelize]
    public partial class BridgeElasticStagesTests
    {
        private const double B = 2000, Tc = 240, H = 1260, Hw = 1200, Tw = 12, Bt = 400, Tt = 25, Bb = 600, Tb = 35, Diameter = 16;
        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void RecordLoadedLibraries()
        {
            foreach (var assembly in new[] { typeof(SectionSolver).Assembly, typeof(ReinforcedConcreteSection).Assembly, typeof(ConcreteMaterialEN1992Data).Assembly })
            {
                using (var stream = System.IO.File.OpenRead(assembly.Location))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    TestContext.WriteLine(assembly.Location + " | SHA256=" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""));
            }
        }
        private static double Ea { get { return SteelMaterialEN1993Data.S355.ElasticModulusTension; } }
        private static double Es { get { return SteelMaterialEN1992Data.B450C.ElasticModulusTension; } }
        private static double Ec { get { return ConcreteMaterialEN1992Data.C35_45.ElasticModulusCompression; } }
        private static ReinforcedConcreteSection Section(bool top = true, bool bottom = true)
        {
            var bar = new RebarSectionCircular(Diameter, SteelMaterialEN1992Data.B450C);
            return new ReinforcedConcreteSection(B, Tc, ConcreteMaterialEN1992Data.C35_45, top ? bar : null, 200, 50,
                bottom ? bar : null, 200, new SectionH(H, Tw, Bt, Tt, Bb, Tb, "Bridge audit"), SteelMaterialEN1993Data.S355, 40);
        }
        private static StressAnalysisResult Solve(double phi, double n, double m, bool top = true, bool bottom = true, double yRef = 0)
        {
            var section = Section(top, bottom);
            var axes = new CoordinateSystem(new Point2d(B / 2, yRef), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, phi, 0, true, 16);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardNTC2018Concrete(), true, -1, new StandardEN1993p11());
            return checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(n, 0, 0, 0, m, 0, axes), phi, 0);
        }
        private static void Near(double actual, double expected, string label, double relative = 1e-5)
        {
            Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), label + " is not finite");
            Assert.AreEqual(expected, actual, 1e-7 + relative * Math.Max(1, Math.Abs(expected)), label);
        }
        private static (double Area, double Y, double I) Oracle(double phi, bool top, bool bottom, bool line)
        {
            double ratio = Ea / Ec * (1 + phi);
            var parts = new List<(double Area, double Y, double I)> {
                (Bt * Tt, -Tt / 2, line ? 0 : Bt * Math.Pow(Tt, 3) / 12),
                (Tw * Hw, -Tt - Hw / 2, Tw * Math.Pow(Hw, 3) / 12),
                (Bb * Tb, -H + Tb / 2, line ? 0 : Bb * Math.Pow(Tb, 3) / 12),
                (B * Tc / ratio, Tc / 2, B * Math.Pow(Tc, 3) / 12 / ratio)
            };
            foreach (double y in new[] { top ? Tc - 50 : double.NaN, bottom ? 40 : double.NaN }.Where(y => !double.IsNaN(y)))
                parts.Add((10 * Math.PI * Diameter * Diameter / 4 * (Es / Ea - 1 / ratio), y,
                    line ? 0 : 10 * Math.PI * Math.Pow(Diameter, 4) / 64 * (Es / Ea - 1 / ratio)));
            double a = parts.Sum(p => p.Area), cy = parts.Sum(p => p.Area * p.Y) / a;
            return (a, cy, parts.Sum(p => p.I + p.Area * Math.Pow(p.Y - cy, 2)));
        }
        private static double[] Values(StressAnalysisResult result, double phi)
        {
            return result.GetStructuralSteelVerticesTension(phi).Select(p => p.tension)
                .Concat(result.GetConcreteVerticesTension(phi).Select(p => p.tension))
                .Concat(result.GetRebarsTension(phi).Select(p => p.tension)).ToArray();
        }
        [DataTestMethod]
        [DataRow(false, false, 0)] [DataRow(true, false, 10)] [DataRow(false, true, 10)] [DataRow(true, true, 20)]
        public void Geometry_OptionalRowsAndFaceToAxisCovers(bool top, bool bottom, int count)
        {
            var section = Section(top, bottom); Assert.AreEqual(count, section.Rebars.Count());
            Assert.IsTrue(section.Rebars.All(b => Math.Abs(b.Position.Y - 190) < 1e-9 || Math.Abs(b.Position.Y - 40) < 1e-9));
            foreach (var row in section.Rebars.GroupBy(b => b.Position.Y)) { Near(row.Min(b => b.Position.X), 100, "first bar", 1e-12); Near(row.Max(b => b.Position.X), 1900, "last bar", 1e-12); }
        }
        [DataTestMethod]
        [DataRow(0d, false, false)] [DataRow(0d, true, false)] [DataRow(0d, false, true)] [DataRow(0d, true, true)]
        [DataRow(2.2d, false, false)] [DataRow(2.2d, true, false)] [DataRow(2.2d, false, true)] [DataRow(2.2d, true, true)]
        [DataRow(5d, false, false)] [DataRow(5d, true, false)] [DataRow(5d, false, true)] [DataRow(5d, true, true)]
        public void G2Q_HomogenizedGeometryAgainstIndependentRectangles(double phi, bool top, bool bottom)
        {
            var p = Section(top, bottom).GetHomogeneizedMechanicalProperties(phi); var expected = Oracle(phi, top, bottom, false);
            double ratio = Ea / Ec * (1 + phi);
            Near(p.areaH / ratio, expected.Area, "area", 1e-10); Near(p.centroidH.Y, expected.Y, "centroid", 1e-10); Near(p.JxxH / ratio, expected.I, "inertia", 1e-10);
        }
        [DataTestMethod]
        [DataRow(0d, 0d, 2e9)] [DataRow(0d, -400000d, 2e9)] [DataRow(0d, 250000d, -1e9)] [DataRow(0d, -300000d, 0d)]
        [DataRow(2.2d, 0d, 2e9)] [DataRow(2.2d, -400000d, 2e9)] [DataRow(2.2d, 250000d, -1e9)] [DataRow(2.2d, -300000d, 0d)]
        [DataRow(5d, 0d, 2e9)] [DataRow(5d, -400000d, 2e9)] [DataRow(5d, 250000d, -1e9)] [DataRow(5d, -300000d, 0d)]
        public void G2Q_StressesAndEquilibriumAgainstIndependentLineSection(double phi, double n, double m)
        {
            var p = Oracle(phi, true, true, true); var result = Solve(phi, n, m);
            Func<double, double> sigma = y => n / p.Area - (m + n * p.Y) / p.I * (y - p.Y);
            foreach (var point in result.GetStructuralSteelVerticesTension(phi)) Near(point.tension, sigma(point.point.Y), "structural steel");
            foreach (var point in result.GetConcreteVerticesTension(phi)) Near(point.tension, sigma(point.point.Y) / (Ea / Ec * (1 + phi)), "concrete");
            // Rebar E differs from structural steel E and must not be silently identified with it.
            var bars = Section().Rebars.ToArray(); var tensions = result.GetRebarsTension(phi).ToArray();
            for (int i = 0; i < bars.Length; i++) Near(tensions[i].tension, sigma(bars[i].Position.Y) * Es / Ea, "rebar");
            var vertices = result.GetStructuralSteelVerticesTension(phi).OrderBy(v => v.point.Y).ToArray();
            double slope = (vertices.Last().tension - vertices.First().tension) / (vertices.Last().point.Y - vertices.First().point.Y);
            double uniform = vertices.First().tension + slope * (p.Y - vertices.First().point.Y);
            Near(uniform * p.Area, n, "reconstructed N", 2e-5); Near(-slope * p.I - uniform * p.Area * p.Y, m, "reconstructed M", 2e-5);
        }
        [DataTestMethod]
        [DataRow(0d)] [DataRow(1d)] [DataRow(2.2d)] [DataRow(5d)]
        public void G2Q_Superposition(double phi)
        {
            double[] a = Values(Solve(phi, -200000, 8e8), phi), b = Values(Solve(phi, 100000, 12e8), phi), sum = Values(Solve(phi, -100000, 2e9), phi);
            for (int i = 0; i < sum.Length; i++) Near(sum[i], a[i] + b[i], "superposition");
        }
        [DataTestMethod]
        [DataRow(0d, -2d)] [DataRow(0d, .01d)] [DataRow(0d, 3d)] [DataRow(2.2d, -2d)] [DataRow(2.2d, .01d)] [DataRow(2.2d, 3d)]
        public void G2Q_ScalingAndLoadReversal(double phi, double factor)
        {
            var baseline = Values(Solve(phi, -150000, 2e9), phi); var scaled = Values(Solve(phi, -150000 * factor, 2e9 * factor), phi);
            for (int i = 0; i < baseline.Length; i++) Near(scaled[i], factor * baseline[i], "linear scaling");
        }
        [DataTestMethod]
        [DataRow(0d)] [DataRow(2.2d)] [DataRow(5d)]
        public void G2Q_ZeroActionsAndReferenceTransport(double phi)
        {
            foreach (double value in Values(Solve(phi, 0, 0), phi)) Near(value, 0, "zero action", 1e-10);
            var original = Values(Solve(phi, -200000, 2e9), phi);
            var shifted = Values(Solve(phi, -200000, 1.9e9, yRef: 500), phi);
            for (int i = 0; i < original.Length; i++) Near(shifted[i], original[i], "moment transport");
        }
        [DataTestMethod]
        [DataRow(0d)] [DataRow(.5d)] [DataRow(2.2d)] [DataRow(5d)]
        public void Homogenization_PhiNInverse(double phi)
        {
            double ratio = Ea / Ec * (1 + phi);
            Near(ReinforcedConcreteSection.CalculateHomogenizedFactorPhi(ratio, SteelMaterialEN1993Data.S355, ConcreteMaterialEN1992Data.C35_45), phi, "inverse", 1e-12);
        }
        [DataTestMethod]
        [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)]
        public void G2_OptionalRebarRowsRemainSolvable(bool top, bool bottom)
        {
            var p = Oracle(2.2, top, bottom, true); var result = Solve(2.2, 0, 2e9, top, bottom);
            foreach (var point in result.GetStructuralSteelVerticesTension(2.2)) Near(point.tension, -2e9 / p.I * (point.point.Y - p.Y), "optional row stress");
        }
        [DataTestMethod, TestCategory("ApiCharacterization")]
        [DataRow(.5d)] [DataRow(2.2d)] [DataRow(5d)]
        public void LinearResult_DefaultSteelGettersDoNotReuseStoredPhi(double phi)
        {
            // Characterizes the overload contract; it does NOT endorse default getters for linear analysis.
            var result = Solve(phi, 0, 2e8);
            Assert.IsTrue(result.LinearElasticAnalysis); Assert.IsTrue(result.PsiRebar.HasValue); Near(result.PsiRebar.GetValueOrDefault(), phi, "stored phi");
            var explicitSteel = result.GetStructuralSteelVerticesTension(phi); var defaultSteel = result.GetStructuralSteelVerticesTension();
            var explicitBars = result.GetRebarsTension(phi); var defaultBars = result.GetRebarsTension();
            for (int i = 0; i < explicitSteel.Length; i++) Near(defaultSteel[i].tension * (1 + phi), explicitSteel[i].tension, "default steel ratio");
            for (int i = 0; i < explicitBars.Length; i++) Near(defaultBars[i].tension * (1 + phi), explicitBars[i].tension, "default rebar ratio");
            TestContext.WriteLine("Steel: explicit={0:R}, default={1:R}; rebar: explicit={2:R}, default={3:R}",
                explicitSteel[0].tension, defaultSteel[0].tension, explicitBars[0].tension, defaultBars[0].tension);
        }
        [DataTestMethod, TestCategory("ConstructorRegression")]
        [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(true, true)]
        public void Constructor_NullSteelShouldHonorDocumentedContract(bool top, bool bottom)
        {
            // Fixed in Model source. The intentionally retained ANTHEA DLL snapshot predates the fix.
            var bar = new RebarSectionCircular(Diameter, SteelMaterialEN1992Data.B450C);
            var section = new ReinforcedConcreteSection(B, Tc, ConcreteMaterialEN1992Data.C35_45, top ? bar : null, 200, 50,
                bottom ? bar : null, 200, null, SteelMaterialEN1993Data.S355, 40);
            Assert.AreEqual(0, section.SteelSections.Count());
            Assert.AreEqual((top ? 10 : 0) + (bottom ? 10 : 0), section.Rebars.Count());
            Near(section.Area, B * Tc, "concrete-only area");
        }
    }
}
