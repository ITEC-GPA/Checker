using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ConcreteTests
{
    /// <summary>
    /// Tests of <see cref="DomainPointAxialTolerance"/> (ANTHEA F2.1, divergence S-1): the tolerance on N of the points of the domain searched at an
    /// assigned axial force depends on the concrete diagram, wider with the stress block. Expected values by hand or from Python (standard library):
    /// closed forms written in the tests, the resistances of attesi.json of the F2.1 bench of ANTHEA (genera_attesi.py) and the numbers of the
    /// script attesi_s1.py of the project of S-1; never outputs of the code. The sections are those of <see cref="DomainPointContractTests"/>,
    /// built as ANTHEA builds them (NTC 2018, αcc 0.85, γc 1.5, γs 1.15, B450C elastic perfectly plastic)
    /// </summary>
    [TestClass]
    public class DomainPointAxialToleranceTests
    {
        private static readonly ConcreteMaterial.CompressionStressStrainDiagrams ParabolaRectangle = ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle;
        private static readonly ConcreteMaterial.CompressionStressStrainDiagrams StressBlock = ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock;

        private static DomainPointContractTests.BenchSection Bench(string name) => DomainPointContractTests.Sections.Single(s => s.Name == name);

        private static SectionSolver Solver(DomainPointContractTests.BenchSection section, ConcreteMaterial.CompressionStressStrainDiagrams diagram,
            SectionSolver.FailureDomainTypes state = SectionSolver.FailureDomainTypes.Plastic) =>
            DomainPointContractTests.BuildChecker(DomainPointContractTests.BuildSection(section, diagram), state).SectionSolver;

        /// <summary>R2 of the bench (400 × 600, 4 + 4Ø20 at 50 mm from the edges) with another concrete class</summary>
        private static DomainPointContractTests.BenchSection R2WithFck(double fck)
        {
            var r2 = Bench("R2");
            return new DomainPointContractTests.BenchSection { Name = "R2-C" + fck, Width = r2.Width, Height = r2.Height, Fck = fck, Bars = r2.Bars, AxialKn = new double[0] };
        }

        /// <summary>A point of the domain with the given forces (N, Nmm)</summary>
        private static FailureDomain.FailureDomainPoint Point(double nrd, double mxrd = 0, double myrd = 0) =>
            new FailureDomain.FailureDomainPoint(new ForceTuple(nrd, mxrd, myrd), SectionSolver.FailureZones.F3A, null, 0);

        // fyd and fcd of NTC 2018 with αcc 0.85, γc 1.5, γs 1.15
        private const double Fyd = 450 / 1.15;
        private static double Fcd(double fck) => 0.85 * fck / 1.5;
        private static double Bar(double diameter) => Math.PI * diameter * diameter / 4;

        // ---------------------------------------------------------------- the rule

        [TestMethod]
        public void OnlyTheStressBlockHasReducedPrecision()
        {
            foreach (ConcreteMaterial.CompressionStressStrainDiagrams diagram in Enum.GetValues(typeof(ConcreteMaterial.CompressionStressStrainDiagrams)))
                Assert.AreEqual(diagram == StressBlock, DomainPointAxialTolerance.HasReducedPrecision(diagram), diagram.ToString());
            Assert.IsTrue(DomainPointAxialTolerance.HasReducedPrecision(Solver(Bench("R1"), StressBlock)));
            Assert.IsFalse(DomainPointAxialTolerance.HasReducedPrecision(Solver(Bench("R1"), ParabolaRectangle)));
            Assert.AreEqual(1000.0, DomainPointAxialTolerance.MinimumTolerance);
            Assert.AreEqual(1e-6, DomainPointAxialTolerance.RelativeTolerance);
            Assert.AreEqual(1e-3, DomainPointAxialTolerance.StressBlockFraction);
        }

        [TestMethod]
        public void ContinuousDiagramsKeepTheToleranceOfAnthea()
        {
            // max(1000 N; 1e-6 |N|): 1000 N up to |N| = 1e9 N, then 1e-6 |N| (3e9 N: 3000 N), exactly as SectionMomentResistance of ANTHEA.
            // The third term, the tolerance of convergence of the search, is 0.25e-4 · 800 · 800 · 40 = 640 N for C2: below 1000 N
            var diagrams = new[] { ParabolaRectangle, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear, ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear };
            var cases = new[] { (0.0, 1000.0), (-1.5e6, 1000.0), (2.5e5, 1000.0), (-3e9, 3000.0), (-1e9, 1000.0), (4e9, 4000.0) };
            foreach (var diagram in diagrams)
                foreach (var state in DomainPointContractTests.States)
                {
                    var solver = Solver(Bench("C2"), diagram, state);
                    foreach (var c in cases)
                        Assert.AreEqual(c.Item2, DomainPointAxialTolerance.Calculate(solver, c.Item1), diagram + " " + state + " N = " + c.Item1);
                }
        }

        [TestMethod]
        public void CentredCompressionResistanceOfARectangle()
        {
            // R2: 400 × 600, 8Ø20, C35/45; at εc2 = 2 ‰ the concrete is at fcd (parabola-rectangle) or η fcd = fcd (stress block, η = 1 up to
            // C50/60) and the bars, beyond εyd = 1.957 ‰, at fyd; the concrete displaced by the bars is not counted.
            // (240 000 - 2513.274123) · 19.833333 + 2513.274123 · 391.304348 = 5 693 608.488122 N (attesi_s1.py)
            double steel = 8 * Bar(20);
            double expected = (400 * 600 - steel) * Fcd(35) + steel * Fyd;
            Assert.AreEqual(5693608.488122, expected, 1e-6);
            foreach (var diagram in new[] { ParabolaRectangle, StressBlock })
                foreach (var state in DomainPointContractTests.States)
                    Assert.AreEqual(expected, DomainPointAxialTolerance.CentredCompressionResistance(Solver(Bench("R2"), diagram, state)), 1e-6 * expected, diagram + " " + state);
        }

        [TestMethod]
        public void CentredCompressionResistanceOfAPolygon()
        {
            // C2: regular polygon of 144 sides inscribed in D 800 (area 72 · 400² · sin(2π/144) = 502 495.342449 mm²), 12Ø16, C40/50:
            // (502 495.342449 - 2412.743158) · 22.666667 + 2412.743158 · 391.304348 = 12 279 322.471819 N (attesi_s1.py)
            double area = 72 * 400.0 * 400.0 * Math.Sin(2 * Math.PI / 144);
            double steel = 12 * Bar(16);
            double expected = (area - steel) * Fcd(40) + steel * Fyd;
            Assert.AreEqual(12279322.471819, expected, 1e-5);
            foreach (var diagram in new[] { ParabolaRectangle, StressBlock })
                Assert.AreEqual(expected, DomainPointAxialTolerance.CentredCompressionResistance(Solver(Bench("C2"), diagram)), 1e-6 * expected, diagram.ToString());
        }

        [TestMethod]
        public void StressBlockUsesEtaAboveC50()
        {
            // R2 with C70/85: εc2 = 2 + 0.085 (70 - 50)^0.53 = 2.4159 ‰ (EN 1992-1-1 table 3.1), beyond εyd: bars at fyd.
            // Parabola-rectangle at εc2: fcd = 39.666667; stress block: η fcd with η = 1 - (70 - 50)/200 = 0.9 (3.1.7(3)), since
            // εc2 > (1 - λ) εcu3 = 0.25 · 2.656 ‰ = 0.664 ‰.
            // (240 000 - 2513.274123) · 0.9 · 39.666667 + 2513.274123 · 391.304348 = 9 461 731.205372 N
            // (240 000 - 2513.274123) · 39.666667 + 2513.274123 · 391.304348 = 10 403 761.884685 N (attesi_s1.py)
            double steel = 8 * Bar(20);
            double stressBlock = (400 * 600 - steel) * 0.9 * Fcd(70) + steel * Fyd;
            double parabolaRectangle = (400 * 600 - steel) * Fcd(70) + steel * Fyd;
            Assert.AreEqual(9461731.205372, stressBlock, 1e-5);
            Assert.AreEqual(10403761.884685, parabolaRectangle, 1e-5);
            Assert.AreEqual(stressBlock, DomainPointAxialTolerance.CentredCompressionResistance(Solver(R2WithFck(70), StressBlock)), 1e-6 * stressBlock);
            Assert.AreEqual(parabolaRectangle, DomainPointAxialTolerance.CentredCompressionResistance(Solver(R2WithFck(70), ParabolaRectangle)), 1e-6 * parabolaRectangle);
            Assert.AreEqual(stressBlock / 1000, DomainPointAxialTolerance.Calculate(Solver(R2WithFck(70), StressBlock), -1.5e6), 1e-6 * stressBlock / 1000);
        }

        [TestMethod]
        public void StressBlockToleranceIsOneThousandthOfTheCentredCompression()
        {
            // NRd,c / 1000 (attesi_s1.py): R2 5693.608488 N, C2 12 279.322472 N, R1 3053.290246 N; the same for the elastic domain
            var cases = new[] { ("R2", 5693.608488), ("C2", 12279.322472), ("R1", 3053.290246) };
            foreach (var c in cases)
                foreach (var state in DomainPointContractTests.States)
                {
                    var solver = Solver(Bench(c.Item1), StressBlock, state);
                    Assert.AreEqual(c.Item2, DomainPointAxialTolerance.Calculate(solver, -1.5e6), 1e-6 * c.Item2, c.Item1 + " " + state);
                    Assert.AreEqual(c.Item2, DomainPointAxialTolerance.Calculate(solver, 0), 1e-6 * c.Item2, c.Item1 + " " + state + " N = 0");
                    Assert.AreEqual(c.Item2, DomainPointAxialTolerance.Calculate(solver, 4e5), 1e-6 * c.Item2, c.Item1 + " " + state + " tension");
                }
            // 1e-6 |N| wins for |N| > 1000 NRd,c: C2, N = -3e10 N → 30 000 N
            Assert.AreEqual(30000.0, DomainPointAxialTolerance.Calculate(Solver(Bench("C2"), StressBlock), -3e10));
            // small section: 200 × 200, 4Ø12 at 60 mm from the axes, C25/30: NRd,c = (40 000 - 452.389342) · 14.166667 + 452.389342 · 391.304348
            // = 737 279.734134 N, NRd,c / 1000 = 737.28 N < 1000 N: the minimum
            var small = new DomainPointContractTests.BenchSection
            {
                Name = "Q", Width = 200, Height = 200, Fck = 25, AxialKn = new double[0],
                Bars = new[] { new double[] { -60, -60, 12 }, new double[] { 60, -60, 12 }, new double[] { 60, 60, 12 }, new double[] { -60, 60, 12 } },
            };
            var smallSolver = Solver(small, StressBlock);
            Assert.AreEqual(737279.734134, DomainPointAxialTolerance.CentredCompressionResistance(smallSolver), 1e-6 * 737279.734134);
            Assert.AreEqual(1000.0, DomainPointAxialTolerance.Calculate(smallSolver, -2e5));
        }

        // ---------------------------------------------------------------- tolerance of convergence of the search (large sections)

        /// <summary>
        /// The pile of the horizontal pile module of ANTHEA (defaults of PaloOrizzontale): circle of 32 sides inscribed in the diameter (vertices at
        /// 2π i / 32, so on both axes), C35/45, 16Ø24 at the radius D/2 - 70 - 10 - 12 (cover 70, stirrups Ø10), as SezioneCA places them; here
        /// with the B450C of the other tests (elastic perfectly plastic)
        /// </summary>
        private static DomainPointContractTests.BenchSection Pile(double diameter)
        {
            double radius = diameter / 2 - 70 - 10 - 12;
            return new DomainPointContractTests.BenchSection
            {
                Name = "P" + diameter, Diameter = diameter, Sides = 32, Fck = 35, AxialKn = new double[0],
                Bars = Enumerable.Range(0, 16).Select(i => new[] { radius * Math.Cos(2 * Math.PI * i / 16), radius * Math.Sin(2 * Math.PI * i / 16), 24.0 }).ToArray(),
            };
        }

        [TestMethod]
        public void ConvergenceToleranceIsTheScaleOfTheSection()
        {
            // 0.25e-4 b h fck, by hand: R1 300 · 500 · 30 → 112.5 N; R2 400 · 600 · 35 → 210 N; R3 300 · 500 · 25 → 93.75 N;
            // C1 600 · 600 · 30 → 270 N; C2 800 · 800 · 40 → 640 N (polygons with vertices on both axes: bounding box D × D).
            // All below 1000 N: for the sections of the bench and of the contract the rule is that of 0.0.17.0
            var cases = new[] { ("R1", 112.5), ("R2", 210.0), ("R3", 93.75), ("C1", 270.0), ("C2", 640.0) };
            foreach (var c in cases)
                Assert.AreEqual(c.Item2, DomainPointAxialTolerance.ConvergenceTolerance(Solver(Bench(c.Item1), ParabolaRectangle)), 1e-9 * c.Item2, c.Item1);
            // it depends on the section only, not on the diagram or on the state
            foreach (var diagram in DomainPointContractTests.Diagrams)
                foreach (var state in DomainPointContractTests.States)
                    Assert.AreEqual(640.0, DomainPointAxialTolerance.ConvergenceTolerance(Solver(Bench("C2"), diagram, state)), 1e-9 * 640, diagram + " " + state);
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.ConvergenceTolerance(null));
        }

        /// <summary>
        /// Pile of D 2000 (bug of the horizontal pile module of ANTHEA, «diameter above 1.6 not calculated any more»): the tolerance of convergence
        /// of the search is 0.25e-4 · 2000 · 2000 · 35 = 3500 N, beyond the 1000 N of the rule of 0.0.17.0, and it is the tolerance of the
        /// continuous diagrams; with the stress block NRd,c / 1000 is larger. Expected values by hand and from attesi_palo_d2000.py (Python, standard
        /// library): polygon area 16 · 1000² · sin(2π/32) = 3 121 445.152258 mm², As = 16 · π · 24² / 4 = 7238.229474 mm²,
        /// NRd,c = (3 121 445.152258 - 7238.229474) · 19.833333 + 7238.229474 · 391.304348 = 64 597 454.632242 N
        /// </summary>
        [TestMethod]
        public void LargeCircularPileHasTheToleranceOfItsSearch()
        {
            const double convergence = 3500.0;
            Assert.AreEqual(convergence, 0.25e-4 * 2000 * 2000 * 35, 1e-9);
            foreach (var diagram in new[] { ParabolaRectangle, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear, ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear })
            {
                var solver = Solver(Pile(2000), diagram);
                Assert.AreEqual(convergence, DomainPointAxialTolerance.ConvergenceTolerance(solver), 1e-9 * convergence, diagram.ToString());
                Assert.AreEqual(convergence, DomainPointAxialTolerance.Calculate(solver, 0), 1e-9 * convergence, diagram + " N = 0");
                Assert.AreEqual(convergence, DomainPointAxialTolerance.Calculate(solver, -1.5e6), 1e-9 * convergence, diagram + " N = -1500 kN");
                Assert.AreEqual(convergence, DomainPointAxialTolerance.Calculate(solver, 3e9), 1e-9 * convergence, diagram + " N = 3e9 N");
                // 1e-6 |N| wins beyond |N| = 3.5e9 N
                Assert.AreEqual(5000.0, DomainPointAxialTolerance.Calculate(solver, -5e9), 1e-9 * 5000, diagram + " N = -5e9 N");
            }
            double area = 16 * 1000.0 * 1000.0 * Math.Sin(2 * Math.PI / 32), steel = 16 * Bar(24);
            double centred = (area - steel) * Fcd(35) + steel * Fyd;
            Assert.AreEqual(64597454.632242, centred, 1e-5);
            var stressBlock = Solver(Pile(2000), StressBlock);
            Assert.AreEqual(convergence, DomainPointAxialTolerance.ConvergenceTolerance(stressBlock), 1e-9 * convergence);
            Assert.AreEqual(centred, DomainPointAxialTolerance.CentredCompressionResistance(stressBlock), 1e-6 * centred);
            Assert.AreEqual(centred / 1000, DomainPointAxialTolerance.Calculate(stressBlock, 0), 1e-6 * centred / 1000);
        }

        /// <summary>
        /// The points of the horizontal pile module of ANTHEA (main 98a21d4, .NET 8, defaults of PaloOrizzontale: C35/45 parabola-rectangle,
        /// 16Ø24, N = 0) for Mx+ and Mx-, NRd in N: rejected by the rule of 0.0.17.0 from D 1600 (|ΔN| from 1011 to 2326 N), within the
        /// tolerance of convergence of the search 0.25e-4 D² 35 (by hand: 1715, 2240, 2528.75, 3500, 5468.75 N), so accepted now.
        /// D 1400 was accepted also before and stays accepted
        /// </summary>
        [TestMethod]
        public void PilePointsOfAntheaAreAcceptedWithinTheirConvergence()
        {
            var cases = new[]
            {
                (1400.0, 1715.0, new[] { -906.711, -929.687 }), (1600.0, 2240.0, new[] { -1289.698, -1238.767 }),
                (1700.0, 2528.75, new[] { -1011.374, -1008.006 }), (2000.0, 3500.0, new[] { -1364.253, -1401.993 }),
                (2500.0, 5468.75, new[] { -2188.310, -2325.790 }),
            };
            foreach (var c in cases)
            {
                var solver = Solver(Pile(c.Item1), ParabolaRectangle);
                Assert.AreEqual(c.Item2, DomainPointAxialTolerance.Calculate(solver, 0), 1e-9 * c.Item2, "D " + c.Item1);
                foreach (double nrd in c.Item3)
                {
                    string at = "D " + c.Item1 + " NRd = " + nrd;
                    Assert.AreEqual(c.Item1 <= 1400, Math.Abs(nrd) <= DomainPointContractTests.ToleranceOf0017(0), at + ": rule of 0.0.17.0");
                    Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(solver, Point(nrd, 1e9), 0), at);
                }
                // the edges: the tolerance of convergence is accepted, beyond it is not
                Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(solver, Point(-c.Item2 * (1 - 1e-9)), 0), "D " + c.Item1 + " edge");
                Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(solver, Point(-c.Item2 * (1 + 1e-9)), 0), "D " + c.Item1 + " beyond");
            }
        }

        /// <summary>
        /// Pile of D 2000, parabola-rectangle, N = 0, Mx+ and Mx-, with the search as ANTHEA runs it: the points are accepted and |MRd| is
        /// 2536.846498 kNm (attesi_palo_d2000.py: neutral axis 185.40 mm, concrete at εcu = 3.5 ‰) within 2e-3. The point of this runtime can be
        /// within 1000 N or not: the verdict of the rule of 0.0.17.0 is printed, not checked
        /// </summary>
        [TestMethod]
        public void LargeCircularPileResistanceMatchesTheClosedForm()
        {
            const double expected = 2536.846498029;
            var section = DomainPointContractTests.BuildSection(Pile(2000), ParabolaRectangle);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            var local = DomainPointContractTests.LocalAxes(section);
            foreach (double direction in new[] { 1.0, -1.0 })
            {
                string at = "Mx" + (direction > 0 ? "+" : "-");
                var point = solver.CalculateDomainPoint(new[] { DomainPointContractTests.Force(local, 0, direction, 0) })[0];
                Assert.IsNotNull(point, at);
                Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(solver, point, 0), at + " |ΔN| = " + Math.Abs(point.NRd) + " N");
                Assert.AreEqual(expected, Math.Abs(point.MxRd / 1e6), 2e-3 * expected, at);
                Assert.AreEqual(Math.Sign(direction), Math.Sign(point.MxRd), at);
                Console.WriteLine(at + ": NRd = " + point.NRd.ToString("R", CultureInfo.InvariantCulture) + " N, MxRd = " +
                    (point.MxRd / 1e6).ToString("R", CultureInfo.InvariantCulture) + " kNm, rule of 0.0.17.0: " +
                    (Math.Abs(point.NRd) <= DomainPointContractTests.ToleranceOf0017(0) ? "accepted" : "rejected"));
            }
        }

        // ---------------------------------------------------------------- S-1

        /// <summary>
        /// The point of S-1 as ANTHEA (.NET 8) gets it from the iterative strategy: C2, stress block, N = -1500 kN, direction Mx+:
        /// NRd = -1 502 050.3995876817 N, MxRd = 707 016 620.8788993 Nmm (bench F2.1, esito.md, and the same section built as in these tests on
        /// .NET 8). |NRd - N| = 2050.3996 N: beyond the 1000 N of the continuous diagrams, within NRd,c / 1000 = 12 279.32 N (attesi_s1.py)
        /// </summary>
        [TestMethod]
        public void S1PointIsAcceptedOnlyWithTheStressBlock()
        {
            const double axial = -1.5e6;
            var s1 = Point(-1502050.3995876817, 707016620.8788993, -415745.1129957719);
            var stressBlock = Solver(Bench("C2"), StressBlock);
            var parabolaRectangle = Solver(Bench("C2"), ParabolaRectangle);
            Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(stressBlock, s1, axial), "stress block");
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, s1, axial), "parabola-rectangle");
            Assert.IsFalse(Math.Abs(s1.NRd - axial) <= DomainPointContractTests.ToleranceOf0017(axial), "rule of 0.0.17.0");

            // edges with the parabola-rectangle (tolerance 1000 N exactly): |ΔN| = 1000 N accepted on both sides, 1000.001 N rejected
            Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, Point(-1501000), axial));
            Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, Point(-1499000), axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, Point(-1501000.001), axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, Point(-1498999.999), axial));
            // edges with the stress block, relative to the tolerance of the library
            double tolerance = DomainPointAxialTolerance.Calculate(stressBlock, axial);
            Assert.AreEqual(12279.322472, tolerance, 1e-6 * 12279.322472);
            Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(stressBlock, Point(axial - tolerance * (1 - 1e-9)), axial));
            Assert.IsTrue(DomainPointAxialTolerance.HasAxialForce(stressBlock, Point(axial + tolerance * (1 - 1e-9)), axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(stressBlock, Point(axial - tolerance * (1 + 1e-9)), axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(stressBlock, Point(axial - tolerance - 1), axial));
            // no point, or a point with a non finite axial force: not the resistance
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(stressBlock, null, axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(stressBlock, Point(double.NaN), axial));
            Assert.IsFalse(DomainPointAxialTolerance.HasAxialForce(parabolaRectangle, Point(double.NegativeInfinity), axial));
        }

        /// <summary>
        /// C2, stress block, N = -1500 kN, iterative strategy as ANTHEA, the four directions: every point accepted by
        /// <see cref="DomainPointAxialTolerance.HasAxialForce"/> has |MRd| = 706.486 kNm (attesi.json, SLU-C2-SB-N1500, rupture on the concrete
        /// side with the stress block on the polygon of 144 sides) within 5e-3, the tolerance of the bench. About y the closed form is the same:
        /// contour and bars are unchanged by a rotation of 90° (vertices every 2.5°, bars at 15° + k 30°).
        /// On .NET Framework (this suite) the iterative strategy stops in direction Mx+ at NRd = -1513.15 kN (|ΔN| = 13.15 kN, 1.07e-3 NRd,c),
        /// beyond the tolerance, and the point is rejected (ANTHEA would reject it also for its transverse moment of 6.7 kNm); on .NET 8 it stops at
        /// -1502.05 kN and the point is accepted (see <see cref="S1PointIsAcceptedOnlyWithTheStressBlock"/>). The other directions go to the
        /// bisection, with N within 0.05 kN. The count of accepted points is a guard of this runtime, not an independent expectation
        /// </summary>
        [TestMethod]
        public void S1ResistanceIsWithinTheClosedForm()
        {
            const double axialKn = -1500, expected = 706.4860203686168;
            var section = DomainPointContractTests.BuildSection(Bench("C2"), StressBlock);
            var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
            var local = DomainPointContractTests.LocalAxes(section);
            int accepted = 0;
            foreach (var direction in DomainPointContractTests.Directions)
            {
                var point = solver.CalculateDomainPoint(new[] { DomainPointContractTests.Force(local, axialKn, direction.Mx, direction.My) })[0];
                Assert.IsNotNull(point, direction.Label);
                if (!DomainPointAxialTolerance.HasAxialForce(solver, point, axialKn * 1000))
                {
                    Assert.IsTrue(Math.Abs(point.NRd - axialKn * 1000) > 12279.322472 * (1 - 1e-6), direction.Label);
                    continue;
                }
                accepted++;
                double moment = (direction.Mx != 0 ? point.MxRd : point.MyRd) / 1e6;
                Assert.AreEqual(expected, Math.Abs(moment), 5e-3 * expected, direction.Label);
                Assert.AreEqual(Math.Sign(direction.Mx + direction.My), Math.Sign(moment), direction.Label);
            }
            Assert.IsTrue(accepted >= 3, "accepted directions: " + accepted);
        }

        /// <summary>
        /// The states of the F2.1 bench with a closed form (attesi.json, NTC 2018, Mx+ compresses the upper edge): with the library's search as
        /// ANTHEA runs it, every point accepted by <see cref="DomainPointAxialTolerance.HasAxialForce"/> has |MxRd| within 5e-3 of the Python value,
        /// and every point accepted with the rule of 0.0.17.0 is still accepted
        /// </summary>
        [TestMethod]
        public void BenchStatesAcceptedByTheRuleMatchTheClosedForm()
        {
            var states = new[]
            {
                ("R1", StressBlock, 0.0, 154.28354038092942, -69.73116839933678), ("R1", StressBlock, -500.0, 233.60394671126926, -168.72054574010082),
                ("R2", StressBlock, 0.0, 255.73169439728562, -255.73169439728562), ("R2", StressBlock, -1000.0, 480.9039858071475, -480.9039858071475),
                ("R3", StressBlock, 0.0, 149.9568421894531, double.NaN), ("C1", StressBlock, 0.0, 226.86152945392652, -226.86152945392664),
                ("C1", StressBlock, -800.0, 344.82992266561433, -344.82992266561445), ("C2", StressBlock, -1500.0, 706.4860203686168, -706.4860203686166),
                ("R2", ParabolaRectangle, 0.0, 255.50613588020252, -255.50613588020252), ("R2", ParabolaRectangle, -1000.0, 479.07129508968126, -479.07129508968126),
                ("R1", ParabolaRectangle, -500.0, 232.20378482662798, -168.2952992873182), ("C1", ParabolaRectangle, -800.0, 341.7987395136041, -341.79873951360435),
            };
            int points = 0, accepted = 0, acceptedBefore = 0;
            foreach (var group in states.GroupBy(s => (s.Item1, s.Item2)))
            {
                var section = DomainPointContractTests.BuildSection(Bench(group.Key.Item1), group.Key.Item2);
                var solver = DomainPointContractTests.BuildChecker(section, SectionSolver.FailureDomainTypes.Plastic).SectionSolver;
                var local = DomainPointContractTests.LocalAxes(section);
                foreach (var state in group)
                    foreach (var (direction, expected) in new[] { (1.0, state.Item4), (-1.0, state.Item5) })
                    {
                        if (double.IsNaN(expected)) continue;
                        string at = state.Item1 + " " + state.Item2 + " N = " + state.Item3 + " Mx" + (direction > 0 ? "+" : "-");
                        var point = solver.CalculateDomainPoint(new[] { DomainPointContractTests.Force(local, state.Item3, direction, 0) })[0];
                        points++;
                        bool before = point != null && Math.Abs(point.NRd - state.Item3 * 1000) <= DomainPointContractTests.ToleranceOf0017(state.Item3 * 1000);
                        bool now = DomainPointAxialTolerance.HasAxialForce(solver, point, state.Item3 * 1000);
                        if (before) { acceptedBefore++; Assert.IsTrue(now, at + ": accepted by the rule of 0.0.17.0, rejected now"); }
                        if (!now) continue;
                        accepted++;
                        Assert.AreEqual(expected, point.MxRd / 1e6, 5e-3 * Math.Abs(expected), at);
                    }
            }
            Assert.AreEqual(23, points);
            Assert.IsTrue(accepted >= acceptedBefore);
            Console.WriteLine("bench states: " + points + " points, accepted " + accepted + " (rule of 0.0.17.0: " + acceptedBefore + ")");
        }

        // ---------------------------------------------------------------- against the contract of 0.0.17.0

        /// <summary>
        /// On the 1024 points of the contract (<see cref="DomainPointContractTests"/>): with the parabola-rectangle, bilinear and non linear diagrams
        /// the verdict and the tolerance are those of the rule of 0.0.17.0 (bit for bit: the tolerance of convergence of the search of the five
        /// sections is at most 640 N, below 1000 N, see <see cref="ConvergenceToleranceIsTheScaleOfTheSection"/>); with the stress block every point
        /// accepted before is still accepted, and the points accepted only now have |ΔN| ≤ NRd,c / 1000
        /// </summary>
        [TestMethod]
        public void ContractVerdictsAreKeptForTheContinuousDiagrams()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", DomainPointContractTests.FileName);
            var rows = File.ReadAllLines(path, new UTF8Encoding(false)).Where(l => l.TrimStart().StartsWith("{\"section\"", StringComparison.Ordinal)).Select(Parse).ToList();
            Assert.AreEqual(1024, rows.Count);
            var solvers = new Dictionary<string, SectionSolver>();
            int same = 0, kept = 0, added = 0, rejected = 0;
            var addedText = new List<string>();
            foreach (var row in rows)
            {
                var diagram = (ConcreteMaterial.CompressionStressStrainDiagrams)Enum.Parse(typeof(ConcreteMaterial.CompressionStressStrainDiagrams), row["diagram"]);
                var state = (SectionSolver.FailureDomainTypes)Enum.Parse(typeof(SectionSolver.FailureDomainTypes), row["state"]);
                string key = row["section"] + " " + diagram + " " + state;
                SectionSolver solver;
                if (!solvers.TryGetValue(key, out solver))
                    solvers[key] = solver = Solver(Bench(row["section"]), diagram, state);
                double axial = Number(row["N"]);
                bool before = row["accepted"] == "true";
                FailureDomain.FailureDomainPoint point = row.ContainsKey("NRd") ? Point(Number(row["NRd"]), Number(row["MxRd"]), Number(row["MyRd"])) : null;
                bool now = DomainPointAxialTolerance.HasAxialForce(solver, point, axial);
                string at = key + " N = " + row["N"] + " " + row["direction"];
                if (diagram != StressBlock)
                {
                    Assert.AreEqual(before, now, at);
                    if (row.ContainsKey("tolerance"))
                        Assert.AreEqual(row["tolerance"], DomainPointContractTests.N(DomainPointAxialTolerance.Calculate(solver, axial)), at);
                    same++;
                    continue;
                }
                if (before) { Assert.IsTrue(now, at); kept++; }
                else if (now)
                {
                    Assert.IsTrue(Math.Abs(point.NRd - axial) <= 1e-3 * DomainPointAxialTolerance.CentredCompressionResistance(solver), at);
                    added++;
                    addedText.Add(at + " |ΔN| = " + (Math.Abs(point.NRd - axial) / 1000).ToString("0.###", CultureInfo.InvariantCulture) + " kN, path " + row["path"]);
                }
                else rejected++;
            }
            Assert.AreEqual(768, same);
            Console.WriteLine("continuous diagrams: " + same + " verdicts unchanged; stress block: " + kept + " kept, " + added + " added, " + rejected + " rejected");
            foreach (var line in addedText) Console.WriteLine("  added: " + line);
        }

        private static readonly Regex Field = new Regex("\"(\\w+)\": (\"[^\"]*\"|[^,}]+)", RegexOptions.Compiled);

        private static Dictionary<string, string> Parse(string line)
        {
            var result = new Dictionary<string, string>();
            foreach (Match m in Field.Matches(line))
                result[m.Groups[1].Value] = m.Groups[2].Value.Trim('"');
            return result;
        }

        private static double Number(string text)
        {
            switch (text)
            {
                case "NaN": return double.NaN;
                case "Infinity": return double.PositiveInfinity;
                case "-Infinity": return double.NegativeInfinity;
                default: return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        // ---------------------------------------------------------------- arguments

        [TestMethod]
        public void ArgumentChecks()
        {
            var solver = Solver(Bench("R1"), StressBlock);
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.HasReducedPrecision((SectionSolver)null));
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.CentredCompressionResistance(null));
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.Calculate(null, 0));
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.HasAxialForce(null, Point(0), 0));
            Assert.ThrowsException<ArgumentNullException>(() => DomainPointAxialTolerance.HasAxialForce(null, null, 0));
            foreach (double axial in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.AreEqual("axialForce", Assert.ThrowsException<ArgumentException>(() => DomainPointAxialTolerance.Calculate(solver, axial)).ParamName);
                Assert.AreEqual("axialForce", Assert.ThrowsException<ArgumentException>(() => DomainPointAxialTolerance.HasAxialForce(solver, Point(0), axial)).ParamName);
            }
        }
    }
}
