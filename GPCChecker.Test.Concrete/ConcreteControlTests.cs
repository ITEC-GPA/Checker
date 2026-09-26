using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Control tests of the concrete section solver with results computed by hand (September 2026): linear analysis of the uncracked and of
    /// the cracked section, equilibrium of the non linear analysis, ultimate resistances in compression, tension and bending, national annexes
    /// and ACI 318. Conventions: compression negative, M1 positive compresses the top fibres, M2 negative compresses the right side
    /// </summary>
    [TestClass]
    public class ConcreteControlTests : ConcreteTestBase
    {
        private const double B = 300, H = 500;

        private static readonly (double X, double Y, double D)[] FourCorners = { (50, 50, 18), (250, 50, 18), (250, 450, 18), (50, 450, 18) };

        private static readonly (double X, double Y, double D)[] ThreeBottom = { (60, 50, 20), (150, 50, 20), (240, 50, 20) };

        private ReinforcedConcreteSection Rect(double width, double height, IEnumerable<(double X, double Y, double D)> bars,
            ConcreteMaterial concrete = null, SteelMaterial steel = null)
        {
            concrete = concrete ?? ConcreteMaterialEN1992Data.C25_30;
            steel = steel ?? SteelMaterialEN1992Data.B450C;
            var section = new ReinforcedConcreteSection(GetRectangularShape(width, height), concrete);
            section.AddRebars(bars.Select(p => new ReinforcedConcreteRebar(new RebarSectionCircular(p.D, steel), new Point3d(p.X, p.Y, 0))).ToArray());
            return section;
        }

        private SectionCheckerModelCode2010 Checker(IConcreteSection section, StandardModelCode2010 standard, bool tensileConcrete,
            SectionSolver.StressAnalysisTypes stressAnalysis = SectionSolver.StressAnalysisTypes.NonLinear,
            SectionSolver.FailureAnalysisTypes failureAnalysis = SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), failureAnalysis,
                SectionSolver.FailureDomainTypes.Plastic, stressAnalysis, 0, 0, tensileConcrete, 64);
            return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tensileConcrete);
        }

        private ResultBeamForces Force(IConcreteSection section, double nKN, double m1KNm, double m2KNm = 0) =>
            new ResultBeamForces(nKN * 1000, 0, 0, 0, m1KNm * 1e6, m2KNm * 1e6, GetLocalCoordinateSystem(section));

        /// <summary>ψ that gives the homogenization factor n = Es (1 + ψ) / Ec</summary>
        private static double Psi(double n, IConcreteSection section) =>
            n * section.ConcreteMaterial.ElasticModulusCompression / section.GetRebars()[0].RebarMaterial.ElasticModulusTension - 1;

        private static void Rel(double expected, double actual, double tolerance, string message = "") =>
            Assert.AreEqual(expected, actual, Math.Abs(expected) * tolerance + 1e-9, message);

        #region Linear analysis

        [TestMethod]
        public void UncrackedAxialForceIsUniform()
        {
            var section = Rect(B, H, FourCorners);
            double n = 15, psi = Psi(n, section), aBars = 4 * Math.PI * 81;
            StressAnalysisResult r = Checker(section, new StandardEN1992p11(), true).SectionSolver.GetLinearStressAnalysisResult(Force(section, -900, 0), psi, 0);
            double sigmaC = -900e3 / (B * H + (n - 1) * aBars);
            foreach (Point2d p in section.Shape.GetPoints2d())
                Rel(sigmaC, r.GetConcreteTension(psi, p), 1e-6);
            foreach (ReinforcedConcreteRebar bar in section.GetRebars())
                Rel(n * sigmaC, r.GetRebarTension(psi, bar), 1e-6);
        }

        [TestMethod]
        public void CrackedAxialTensionIsCarriedByTheBars()
        {
            var section = Rect(B, H, FourCorners);
            double psi = Psi(15, section), aBars = 4 * Math.PI * 81;
            StressAnalysisResult r = Checker(section, new StandardEN1992p11(), false).SectionSolver.GetLinearStressAnalysisResult(Force(section, 200, 0), psi, 0);
            foreach (ReinforcedConcreteRebar bar in section.GetRebars())
                Rel(200e3 / aBars, r.GetRebarTension(psi, bar), 1e-6);
            foreach (Point2d p in section.Shape.GetPoints2d())
                Assert.AreEqual(0, r.GetConcreteTension(psi, p), 1e-9);
        }

        [TestMethod]
        public void UncrackedBendingIsNavierWithTheHomogenizedInertia()
        {
            var section = Rect(B, H, FourCorners);
            double n = 15, psi = Psi(n, section), a = Math.PI * 81;
            double jxx = B * Math.Pow(H, 3) / 12 + 4 * (n - 1) * a * 200 * 200;
            double jyy = H * Math.Pow(B, 3) / 12 + 4 * (n - 1) * a * 100 * 100;
            StressAnalysisResult r = Checker(section, new StandardEN1992p11(), true).SectionSolver
                .GetLinearStressAnalysisResult(Force(section, 0, 60, -25), psi, 0);
            // σ = -M1 (y - yc) / Jxx + M2 (x - xc) / Jyy
            Func<double, double, double> sigma = (x, y) => -60e6 * (y - H / 2) / jxx + -25e6 * (x - B / 2) / jyy;
            foreach (Point2d p in section.Shape.GetPoints2d())
                Rel(sigma(p.X, p.Y), r.GetConcreteTension(psi, p), 1e-6, $"vertex {p}");
            foreach (ReinforcedConcreteRebar bar in section.GetRebars())
                Rel(n * sigma(bar.Position.X, bar.Position.Y), r.GetRebarTension(psi, bar), 1e-6);
            // top right corner is the most compressed
            Assert.IsTrue(r.GetConcreteTension(psi, new Point2d(B, H)) < 0);
            Assert.IsTrue(r.GetConcreteTension(psi, new Point2d(0, 0)) > 0);
        }

        [TestMethod]
        public void LinearAnalysisIsLinear()
        {
            var section = Rect(B, H, FourCorners);
            double psi = Psi(15, section);
            SectionSolver solver = Checker(section, new StandardEN1992p11(), true).SectionSolver;
            StressAnalysisResult a = solver.GetLinearStressAnalysisResult(Force(section, -300, 40, 10), psi, 0);
            StressAnalysisResult b = solver.GetLinearStressAnalysisResult(Force(section, 100, -15, 20), psi, 0);
            StressAnalysisResult sum = solver.GetLinearStressAnalysisResult(Force(section, -200, 25, 30), psi, 0);
            StressAnalysisResult twice = solver.GetLinearStressAnalysisResult(Force(section, -600, 80, 20), psi, 0);
            foreach (Point2d p in section.Shape.GetPoints2d())
            {
                Rel(a.GetConcreteTension(psi, p) + b.GetConcreteTension(psi, p), sum.GetConcreteTension(psi, p), 1e-6);
                Rel(2 * a.GetConcreteTension(psi, p), twice.GetConcreteTension(psi, p), 1e-6);
            }
        }

        [TestMethod]
        public void CrackedSinglyReinforcedRectangle()
        {
            // b x² / 2 = n As (d - x); J = b x³ / 3 + n As (d - x)²
            var section = Rect(B, H, ThreeBottom);
            double n = 15, psi = Psi(n, section), aS = 3 * Math.PI * 100, d = 450, m = 120e6;
            double x = n * aS / B * (-1 + Math.Sqrt(1 + 2 * B * d / (n * aS)));
            double j = B * x * x * x / 3 + n * aS * (d - x) * (d - x);
            StressAnalysisResult r = Checker(section, new StandardEN1992p11(), false).SectionSolver.GetLinearStressAnalysisResult(Force(section, 0, 120), psi, 0);
            Rel(-m * x / j, r.GetConcreteTension(psi, new Point2d(0, H)), 1e-4, "top concrete");
            Rel(-m * x / j, r.GetConcreteTension(psi, new Point2d(B, H)), 1e-4);
            foreach (ReinforcedConcreteRebar bar in section.GetRebars())
                Rel(n * m * (d - x) / j, r.GetRebarTension(psi, bar), 1e-4, "bars");
            Assert.AreEqual(0, r.GetConcreteTension(psi, new Point2d(0, 0)), 1e-9);
            // strain zero at the neutral axis, H - x from the bottom
            Assert.AreEqual(0, r.StrainPlane.GetStrain(B / 2, H - x), 1e-7);
        }

        [TestMethod]
        public void CrackedDoublyReinforcedRectangle()
        {
            // compressed bars inside the concrete count with n - 1: b x² / 2 + (n - 1) As' (x - d') = n As (d - x)
            var bars = ThreeBottom.Concat(new[] { (60.0, 450.0, 14.0), (240.0, 450.0, 14.0) });
            var section = Rect(B, H, bars);
            double n = 15, psi = Psi(n, section), aS = 3 * Math.PI * 100, aC = 2 * Math.PI * 49, d = 450, dc = 50, m = 150e6;
            // quadratic b/2 x² + ((n - 1) As' + n As) x - ((n - 1) As' d' + n As d) = 0
            double qa = B / 2, qb = (n - 1) * aC + n * aS, qc = -((n - 1) * aC * dc + n * aS * d);
            double x = (-qb + Math.Sqrt(qb * qb - 4 * qa * qc)) / (2 * qa);
            double j = B * x * x * x / 3 + (n - 1) * aC * (x - dc) * (x - dc) + n * aS * (d - x) * (d - x);
            StressAnalysisResult r = Checker(section, new StandardEN1992p11(), false).SectionSolver.GetLinearStressAnalysisResult(Force(section, 0, 150), psi, 0);
            Rel(-m * x / j, r.GetConcreteTension(psi, new Point2d(0, H)), 1e-4);
            Rel(n * m * (d - x) / j, r.GetRebarTension(psi, section.GetRebars().First(bb => bb.Position.Y < 100)), 1e-4);
            Rel(-n * m * (x - dc) / j, r.GetRebarTension(psi, section.GetRebars().First(bb => bb.Position.Y > 400)), 1e-4);
        }

        [TestMethod]
        public void CreepIncreasesTheSteelStressUnderSustainedCompression()
        {
            var section = Rect(B, H, FourCorners);
            SectionSolver solver = Checker(section, new StandardEN1992p11(), true).SectionSolver;
            ReinforcedConcreteRebar bar = section.GetRebars()[0];
            double aBars = 4 * Math.PI * 81;
            double previous = 0;
            foreach (double n in new[] { 6.0, 15.0, 20.0 })
            {
                double psi = Psi(n, section);
                double sigmaS = solver.GetLinearStressAnalysisResult(Force(section, -900, 0), psi, 0).GetRebarTension(psi, bar);
                Rel(n * -900e3 / (B * H + (n - 1) * aBars), sigmaS, 1e-6, $"n = {n}");
                Assert.IsTrue(sigmaS < previous);
                previous = sigmaS;
            }
        }

        #endregion

        #region Non linear analysis

        /// <summary>Resultant (N, M1, M2) of the design stresses of a strain plane by a fine midpoint integration of the rectangle</summary>
        private static (double N, double M1, double M2) Resultant(ReinforcedConcreteSection section, StrainPlane plane, StandardModelCode2010 standard)
        {
            var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
            int nx = 150, ny = 250;
            double dx = B / nx, dy = H / ny, n = 0, m1 = 0, m2 = 0;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < ny; k++)
                {
                    double x = (i + 0.5) * dx, y = (k + 0.5) * dy, e = plane.GetStrain(x, y);
                    double s = e < 0 ? concrete.CalculateDesignStressConcrete(standard, e) : 0;
                    n += s * dx * dy;
                    m1 -= s * dx * dy * (y - H / 2);
                    m2 += s * dx * dy * (x - B / 2);
                }
            foreach (ReinforcedConcreteRebar bar in section.GetRebars())
            {
                double e = plane.GetStrain(bar.Position);
                double sc = e < 0 ? concrete.CalculateDesignStressConcrete(standard, e) : 0;
                double s = bar.RebarMaterial.CalculateDesignStress(standard, e) - sc;
                n += s * bar.Area;
                m1 -= s * bar.Area * (bar.Position.Y - H / 2);
                m2 += s * bar.Area * (bar.Position.X - B / 2);
            }
            return (n, m1, m2);
        }

        [DataTestMethod]
        [DataRow(-800.0, 150.0, 0.0)]
        [DataRow(-1500.0, 60.0, -30.0)]
        [DataRow(0.0, 100.0, 0.0)]
        [DataRow(-300.0, -90.0, 25.0)]
        public void NonLinearAnalysisIsInEquilibrium(double nKN, double m1KNm, double m2KNm)
        {
            var section = Rect(B, H, FourCorners.Concat(new[] { (150.0, 50.0, 18.0), (150.0, 450.0, 18.0) }));
            var standard = new StandardEN1992p11();
            StressAnalysisResult r = Checker(section, standard, false).SectionSolver.GetStressAnalysisResult(Force(section, nKN, m1KNm, m2KNm));
            var (n, m1, m2) = Resultant(section, r.StrainPlane, standard);
            double scaleN = 0.5 * B * H * 25 / 1.5, scaleM = scaleN * H;
            Assert.AreEqual(nKN * 1e3, n, 0.003 * scaleN, "N");
            Assert.AreEqual(m1KNm * 1e6, m1, 0.003 * scaleM, "M1");
            Assert.AreEqual(m2KNm * 1e6, m2, 0.003 * scaleM, "M2");
        }

        [TestMethod]
        public void NonLinearAxialCompressionFollowsTheParabola()
        {
            // uniform strain e with -N = σc(e) (Ac - As) + σs(e) As: solve e by bisection and compare
            var section = Rect(B, H, FourCorners);
            var standard = new StandardEN1992p11();
            var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
            var steel = section.GetRebars()[0].RebarMaterial;
            double aS = 4 * Math.PI * 81, nEd = -1800e3;
            Func<double, double> resultant = e => concrete.CalculateDesignStressConcrete(standard, e) * (B * H - aS) + steel.CalculateDesignStress(standard, e) * aS;
            double lo = -0.002, hi = 0;
            for (int i = 0; i < 100; i++)
            {
                double mid = (lo + hi) / 2;
                if (resultant(mid) < nEd) lo = mid; else hi = mid;
            }
            StressAnalysisResult r = Checker(section, standard, false).SectionSolver.GetStressAnalysisResult(Force(section, -1800, 0));
            foreach (Point2d p in section.Shape.GetPoints2d())
                Rel((lo + hi) / 2, r.StrainPlane.GetStrain(p), 5e-3, $"strain at {p}");
        }

        #endregion

        #region Ultimate resistance

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PureCompressionResistance(bool ntc)
        {
            // uniform εc2 = 2‰: fcd on the net concrete area, the bars (εyd = 1.96‰) at fyd
            var section = Rect(B, H, FourCorners);
            StandardModelCode2010 standard = ntc ? (StandardModelCode2010)new StandardNTC2018Concrete() : new StandardEN1992p11();
            double aS = 4 * Math.PI * 81, fcd = (ntc ? 0.85 : 1.0) * 25 / 1.5, fyd = 450 / 1.15;
            FailureDomain.FailureDomainPoint p = Checker(section, standard, false).CalculateFailureDomainPoint(Force(section, -1000, 0));
            Rel(-(fcd * (B * H - aS) + fyd * aS), p.NRd, 2e-3);
            // centred compression: the moments are only the round-off of the integration on the mesh
            Assert.AreEqual(0, p.MxRd, 2e-3 * Math.Abs(p.NRd) * H);
            Assert.AreEqual(0, p.MyRd, 2e-3 * Math.Abs(p.NRd) * B);
        }

        [TestMethod]
        public void PureTensionResistance()
        {
            var section = Rect(B, H, FourCorners);
            FailureDomain.FailureDomainPoint p = Checker(section, new StandardEN1992p11(), false).CalculateFailureDomainPoint(Force(section, 300, 0));
            Rel(4 * Math.PI * 81 * 450 / 1.15, p.NRd, 1e-3);
        }

        [TestMethod]
        public void BendingResistanceWithTheParabolaRectangle()
        {
            // compression resultant 17/21 fcd b x at 99/238 x from the top; the bars yield
            var section = Rect(B, H, ThreeBottom);
            double aS = 3 * Math.PI * 100, fcd = 25 / 1.5, fyd = 450 / 1.15, d = 450;
            double x = aS * fyd / (17.0 / 21 * B * fcd);
            double mRd = aS * fyd * (d - 99.0 / 238 * x);
            FailureDomain.FailureDomainPoint p = Checker(section, new StandardEN1992p11(), false, failureAnalysis: SectionSolver.FailureAnalysisTypes.ConstantN)
                .CalculateFailureDomainPoint(Force(section, 0, 100), SectionSolver.FailureAnalysisTypes.ConstantN);
            Rel(mRd, p.MxRd, 5e-3, $"x = {x:F1}");
            Assert.AreEqual(0, p.NRd, 1e-3 * aS * fyd);
            // the concrete reaches εcu2 at the top
            Assert.AreEqual(-0.0035, p.StrainPlane.GetStrain(B / 2, H), 1e-5);
        }

        [TestMethod]
        public void BendingResistanceWithTheStressBlock()
        {
            var concrete = new ConcreteMaterialEN1992("C25/30", 25, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
            var section = Rect(B, H, ThreeBottom, concrete);
            double aS = 3 * Math.PI * 100, fcd = 25 / 1.5, fyd = 450 / 1.15, d = 450;
            double x = aS * fyd / (0.8 * B * fcd);
            double mRd = aS * fyd * (d - 0.4 * x);
            FailureDomain.FailureDomainPoint p = Checker(section, new StandardEN1992p11(), false, failureAnalysis: SectionSolver.FailureAnalysisTypes.ConstantN)
                .CalculateFailureDomainPoint(Force(section, 0, 100), SectionSolver.FailureAnalysisTypes.ConstantN);
            // the iterative strategy does not converge with the stress block (zero stress on part of the diagram): the solver uses the intersection one
            Rel(mRd, p.MxRd, 5e-3);
            Assert.AreEqual(0, p.NRd, 1e-3 * aS * fyd);
            FailureDomain.FailureDomainPoint eccentric = Checker(section, new StandardEN1992p11(), false).CalculateFailureDomainPoint(Force(section, -200, 100));
            Assert.IsNotNull(eccentric, "before the fallback a NullReferenceException");
            Rel(100.0 / -200.0 * 1e3, eccentric.MxRd / eccentric.NRd, 1e-2);
        }

        [TestMethod]
        public void SymmetricSectionHasSymmetricResistances()
        {
            // square with 8 bars: M1 and M2 resistances equal and of both signs
            var bars = new[] { (50.0, 50.0, 20.0), (200.0, 50.0, 20.0), (350.0, 50.0, 20.0), (350.0, 200.0, 20.0), (350.0, 350.0, 20.0),
                (200.0, 350.0, 20.0), (50.0, 350.0, 20.0), (50.0, 200.0, 20.0) };
            var section = new ReinforcedConcreteSection(GetRectangularShape(400, 400), ConcreteMaterialEN1992Data.C30_37);
            section.AddRebars(bars.Select(b => new ReinforcedConcreteRebar(new RebarSectionCircular(b.Item3, SteelMaterialEN1992Data.B450C), new Point3d(b.Item1, b.Item2, 0))).ToArray());
            SectionCheckerModelCode2010 checker = Checker(section, new StandardEN1992p11(), false, failureAnalysis: SectionSolver.FailureAnalysisTypes.ConstantN);
            double mx = checker.CalculateFailureDomainPoint(Force(section, -500, 100), SectionSolver.FailureAnalysisTypes.ConstantN).MxRd;
            double mxNegative = checker.CalculateFailureDomainPoint(Force(section, -500, -100), SectionSolver.FailureAnalysisTypes.ConstantN).MxRd;
            double my = checker.CalculateFailureDomainPoint(Force(section, -500, 0, 100), SectionSolver.FailureAnalysisTypes.ConstantN).MyRd;
            Rel(mx, -mxNegative, 5e-3);
            Rel(mx, my, 5e-3);
            Assert.IsTrue(mx > 0);
        }

        [TestMethod]
        public void MoreReinforcementMoreResistance()
        {
            SectionCheckerModelCode2010 Make(double diameter)
            {
                var s = Rect(B, H, ThreeBottom.Select(b => (b.X, b.Y, diameter)));
                return Checker(s, new StandardEN1992p11(), false, failureAnalysis: SectionSolver.FailureAnalysisTypes.ConstantN);
            }
            double previous = 0;
            foreach (double diameter in new[] { 12.0, 16.0, 20.0, 26.0 })
            {
                var checker = Make(diameter);
                var section = (IConcreteSection)checker.SectionCheckerAttribute.Section;
                double m = checker.CalculateFailureDomainPoint(Force(section, 0, 100), SectionSolver.FailureAnalysisTypes.ConstantN).MxRd;
                Assert.IsTrue(m > previous, $"Ø{diameter}: {m / 1e6:F1} kNm");
                previous = m;
            }
        }

        [TestMethod]
        public void WorkingRatioAlongTheEccentricity()
        {
            var section = Rect(B, H, FourCorners);
            SectionCheckerModelCode2010 checker = Checker(section, new StandardEN1992p11(), false);
            ResultBeamForces force = Force(section, -600, 80);
            FailureDomain.FailureDomainPoint p = checker.CalculateFailureDomainPoint(force);
            // the resistance lies on the same ray from the origin
            Rel(force.M1 / force.N, p.MxRd / p.NRd, 2e-3);
            double ratio = new FailureDomain.FailureDomainForce(force, p).CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, 1e6, 1e3);
            Rel(force.N / p.NRd, ratio, 2e-3);
            Assert.IsTrue(ratio < 1);
        }

        [TestMethod]
        public void NationalAnnexChangesOnlyTheConcreteContribution()
        {
            // the difference between EN (αcc = 1) and DIN (αcc = 0.85) in pure compression is 0.15 fck / γc on the net concrete area
            var section = Rect(B, H, FourCorners);
            double en = Checker(section, new StandardEN1992p11(), false).CalculateFailureDomainPoint(Force(section, -1000, 0)).NRd;
            double din = Checker(section, new StandardDINEN1992p11(), false).CalculateFailureDomainPoint(Force(section, -1000, 0)).NRd;
            double ds = Checker(section, new StandardDSEN1992p11(), false).CalculateFailureDomainPoint(Force(section, -1000, 0)).NRd;
            double aS = 4 * Math.PI * 81;
            Rel(-0.15 * 25 / 1.5 * (B * H - aS), en - din, 1e-2);
            // DS: γc = 1.4 and γs = 1.2
            Rel(-(25 / 1.4 * (B * H - aS) + 450 / 1.2 * aS), ds, 2e-3);
        }

        /// <summary>Every non abstract concrete standard of Model (Model Code 2010 family and ACI 318 family)</summary>
        public static IEnumerable<object[]> ConcreteStandards() => typeof(StandardModelCode2010).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && (typeof(StandardModelCode2010).IsAssignableFrom(t) || typeof(StandardACI318).IsAssignableFrom(t)))
            .OrderBy(t => t.Name).Select(t => new object[] { t.Name });

        [DataTestMethod]
        [DynamicData(nameof(ConcreteStandards), DynamicDataSourceType.Method)]
        public void EveryConcreteStandardIsSupported(string standardName)
        {
            Type type = typeof(StandardModelCode2010).Assembly.GetTypes().Single(t => t.Name == standardName);
            var standard = (Standard)Activator.CreateInstance(type);
            double aS = 4 * Math.PI * 81;
            if (standard is StandardModelCode2010 mc)
            {
                // pure compression with the coefficients of the standard: αcc fck / γc on the net concrete, fyd = fyk / γs (yielded at εc2 if εyd < 2‰)
                var section = Rect(B, H, FourCorners);
                SectionCheckerModelCode2010 checker = Checker(section, mc, false);
                double fcd = mc.AlphaCC * 25 / mc.GammaC, fyd = 450 / mc.GammaS, sigmaS = Math.Min(fyd, 200000 * 0.002);
                Rel(-(fcd * (B * H - aS) + sigmaS * aS), checker.CalculateFailureDomainPoint(Force(section, -1000, 0)).NRd, 2e-3, standardName);
                // linear analysis: independent of the standard
                double psi = Psi(15, section);
                double sigma = checker.SectionSolver.GetLinearStressAnalysisResult(Force(section, -900, 0), psi, 0).GetConcreteTension(psi, new Point2d(0, 0));
                Rel(-900e3 / (B * H + 14 * aS), sigma, 1e-6, standardName);
            }
            else
            {
                var aci = (StandardACI318)standard;
                var section = Rect(B, H, FourCorners, ConcreteMaterialACI318Data.Fc4000, SteelMaterialACI318Data.Grade60);
                var options = new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section));
                var checker = new SectionCheckerACI318(new SectionCheckerAttribute(section, null, null), options, aci, false);
                double limit = -aci.PhiCTied * aci.PhiMaximumCompressiveAxialLoadTied * (aci.ConcreteStrengthReductionFactor * 27.579 * (B * H - aS) + 413.685 * aS);
                Rel(limit, checker.CalculateFailureDomainPoint(Force(section, -1000, 0)).NRd, 2e-3, standardName);
                double psi = Psi(8, section);
                double sigma = checker.SectionSolver.GetLinearStressAnalysisResult(Force(section, -900, 0), psi, 0).GetConcreteTension(psi, new Point2d(0, 0));
                Rel(-900e3 / (B * H + 7 * aS), sigma, 1e-6, standardName);
            }
        }

        [TestMethod]
        public void AciMaximumAxialStrength()
        {
            // φ Pn,max = 0.65 · 0.80 · (0.85 f'c (Ag - Ast) + fy Ast), tied column
            var concrete = ConcreteMaterialACI318Data.Fc4000;
            var steel = SteelMaterialACI318Data.Grade60;
            var section = Rect(B, H, FourCorners, concrete, steel);
            double aS = 4 * Math.PI * 81;
            var options = new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section));
            var checker = new SectionCheckerACI318(new SectionCheckerAttribute(section, null, null), options, new StandardACI318p19(), false);
            FailureDomain.FailureDomainPoint p = checker.CalculateFailureDomainPoint(Force(section, -1000, 0));
            Rel(-0.65 * 0.8 * (0.85 * 27.579 * (B * H - aS) + 413.685 * aS), p.NRd, 2e-3);
        }

        [TestMethod]
        public void AciTensionControlledBending()
        {
            // Whitney block: a = As fy / (0.85 f'c b), φ Mn = 0.9 As fy (d - a / 2)
            var concrete = new ConcreteMaterialACI318("4000 psi", 27.579, ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
            var steel = SteelMaterialACI318Data.Grade60;
            var section = Rect(B, H, ThreeBottom, concrete, steel);
            double aS = 3 * Math.PI * 100, fy = 413.685, d = 450;
            double a = aS * fy / (0.85 * 27.579 * B);
            var options = new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN);
            var checker = new SectionCheckerACI318(new SectionCheckerAttribute(section, null, null), options, new StandardACI318p19(), false);
            FailureDomain.FailureDomainPoint p = checker.CalculateFailureDomainPoint(Force(section, 0, 100), SectionSolver.FailureAnalysisTypes.ConstantN);
            Rel(0.9 * aS * fy * (d - a / 2), p.MxRd, 5e-3);
            // with the constant axial force the point has N = 0 (before the fix the iterative strategy returned a point with N = -764 kN)
            Assert.AreEqual(0, p.NRd, 1e-3 * aS * fy);
        }

        #endregion
    }
}
