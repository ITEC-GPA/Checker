using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Slenderness;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Model column of GPCChecker.Concrete 0.27.0.0 (ModelColumnAnalysis, method page docs/metodi/ca.colonna-modello.md). Expected values:
    /// <list type="bullet">
    /// <item>the independent reference ReferenceCalculations/model-column/derive_model_column_oracles.py (Python standard library, no GPC code; values in
    /// model-column-independent-oracles.json), with the declared model of the library (net section, plastic ultimate strains);</item>
    /// <item>two external examples, see the README of that folder for their source: case R400-3+3D24-l0-12 (400 × 400, 3+3Ø24, NEd = 500 kN,
    /// l0 = 12 m, fyk = 450) and case R500-6+6D24-N1200 (500 × 500, 6+6Ø24, NEd = 1200 kN, l0 = 8, 13, 18 m, graphical reading);</item>
    /// <item>closed forms of EN 1992-1-1 and NTC 2018 by hand.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class ModelColumnTests
    {
        private const double kN = 1e3, kNm = 1e6;

        // ---------------------------------------------------------------- sections

        /// <summary>Rectangle b × h centred in the origin with layers (y, number, diameter) of bars spread on b − 2·cover (x), NTC 2018 checker.</summary>
        internal static ReinforcedConcreteSection Rectangle(double b, double h, double fck, double es, double fyk, double cover, params (double Y, int Count, double Diameter)[] layers)
        {
            var concrete = new ConcreteMaterialEN1992("C" + fck, fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450C", es, fyk, fyk, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var outline = new List<Point2d> { new Point2d(-b / 2, -h / 2), new Point2d(b / 2, -h / 2), new Point2d(b / 2, h / 2), new Point2d(-b / 2, h / 2) };
            var section = new ReinforcedConcreteSection(new Shape2d(new Polygon2d(outline), new Polygon2d[0]), concrete);
            foreach (var layer in layers)
                for (int i = 0; i < layer.Count; i++)
                {
                    double x = layer.Count == 1 ? 0 : -b / 2 + cover + (b - 2 * cover) * i / (layer.Count - 1);
                    section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(layer.Diameter, steel), new Point2d(x, layer.Y)) });
                }
            return section;
        }

        private static ReinforcedConcreteSection Bars(double b, double h, double fck, params double[][] bars)
        {
            var concrete = new ConcreteMaterialEN1992("C" + fck, fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450C", 200000, 450, 450, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var outline = new List<Point2d> { new Point2d(-b / 2, -h / 2), new Point2d(b / 2, -h / 2), new Point2d(b / 2, h / 2), new Point2d(-b / 2, h / 2) };
            var section = new ReinforcedConcreteSection(new Shape2d(new Polygon2d(outline), new Polygon2d[0]), concrete);
            foreach (var bar in bars)
                section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(bar[2], steel), new Point2d(bar[0], bar[1])) });
            return section;
        }

        private static ReinforcedConcreteSection Circle(double diameter, int sides, int bars, double barRadius, double barDiameter)
        {
            var concrete = new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450C", 200000, 450, 450, 0.075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var outline = Enumerable.Range(0, sides).Select(i => new Point2d(diameter / 2 * Math.Cos(2 * Math.PI * i / sides), diameter / 2 * Math.Sin(2 * Math.PI * i / sides))).ToList();
            var section = new ReinforcedConcreteSection(new Shape2d(new Polygon2d(outline), new Polygon2d[0]), concrete);
            for (int i = 0; i < bars; i++)
                section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(barDiameter, steel),
                    new Point2d(barRadius * Math.Cos(2 * Math.PI * i / bars), barRadius * Math.Sin(2 * Math.PI * i / bars))) });
            return section;
        }

        internal static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, StandardModelCode2010 standard = null)
        {
            standard = standard ?? new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section), SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        /// <summary>Case R400-3+3D24: 400 × 400, 3Ø24 at y = ±160 (40 mm from the faces to the axis), C25/30, fyk 450, Es 200000.</summary>
        internal static ReinforcedConcreteSection R400() => Rectangle(400, 400, 25, 200000, 450, 40, (160, 3, 24), (-160, 3, 24));

        /// <summary>Case R500-6+6D24: 500 × 500, 6Ø24 at y = ±210, C35/45, fyk 430, Es 206000.</summary>
        internal static ReinforcedConcreteSection R500() => Rectangle(500, 500, 35, 206000, 430, 40, (210, 6, 24), (-210, 6, 24));

        internal static ModelColumnResult Run(ReinforcedConcreteSection section, ModelColumnRequest request, StandardModelCode2010 standard = null)
            => ModelColumnAnalysis.Calculate(request, Checker(section, standard), section, DomainPointContractTests.LocalAxes(section));

        /// <summary>The request of the external examples: no imperfection, no minimum eccentricity, creep neglected (as the sources).</summary>
        private static ModelColumnRequest Plain(double nKn, double l0, double? m0Knm = null, double c = 10, ModelColumnCreep creep = null, double direction = 0)
            => new ModelColumnRequest(-nKn * kN, direction, l0, l0 / 2, m0Knm * kNm, c, ModelColumnImperfection.None, ModelColumnMinimumEccentricity.None, creep);

        private static void Relative(double expected, double actual, double tolerance, string what)
        {
            Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), what + ": not finite");
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: expected {1:G9}, actual {2:G9}, deviation {3:+0.0000%;-0.0000%}",
                what, expected, actual, (actual - expected) / expected));
            Assert.AreEqual(expected, actual, Math.Abs(expected) * tolerance, what + string.Format(": expected {0:G9}, actual {1:G9}, deviation {2:P3}",
                expected, actual, (actual - expected) / expected));
        }

        private static double MomentAt(ModelColumnResult r, double chi)
        {
            var p = r.Points;
            int i = Enumerable.Range(1, p.Count - 1).First(j => p[j].Curvature >= chi);
            double t = (chi - p[i - 1].Curvature) / (p[i].Curvature - p[i - 1].Curvature);
            return p[i - 1].Moment + t * (p[i].Moment - p[i - 1].Moment);
        }

        // ---------------------------------------------------------------- external example R400-3+3D24-l0-12 and the independent reference

        /// <summary>
        /// External source (README): M at 11 curvatures χ·H [‰] of the table (gross section) and max M1Rd = 157.25 kNm, e2 = 153 mm, M0Ed = 137.5 kNm
        /// verified. The library uses the net section (declared): the independent reference gives 156.083 kNm net and 157.388 kNm gross. Tolerances of
        /// the project: 0.2 % on M against the reference, 2 % on χ* and e2* (flat maximum), 1.5 % against the source.
        /// </summary>
        [TestMethod]
        public void R400CaseAgainstTheReferenceAndTheSource()
        {
            var r = Run(R400(), Plain(500, 12000, 137.5));
            Console.WriteLine(r.Status);
            // independent reference, net section (model-column-independent-oracles.json, case R400-3+3D24-l0-12)
            double[] chiH = { 0.612, 1.224, 1.836, 2.448, 3.060, 4.246, 4.284, 4.895, 6.119, 7.955, 11.015 };
            double[] reference = { 57.171, 92.546, 123.121, 152.122, 180.101, 231.706, 233.137, 235.831, 239.666, 243.329, 246.092 };
            for (int i = 0; i < chiH.Length; i++)
                Relative(reference[i] * kNm, MomentAt(r, chiH[i] / 1000 / 400), 2e-3, "M at chi H = " + chiH[i]);
            Relative(156.083 * kNm, r.Capacity.MaximumFirstOrderMoment, 2e-3, "M1Rd,max against the reference");
            Relative(1.069899e-5, r.Capacity.CurvatureAtMaximum, 0.02, "chi* against the reference");
            Relative(154.07, r.Capacity.DeflectionAtMaximum, 0.02, "e2* against the reference");
            Relative(3.092206e-5, r.Capacity.UltimateCurvature, 2e-3, "chi_u against the reference");
            Relative(246.264 * kNm, r.Capacity.UltimateMoment, 2e-3, "M(chi_u) against the reference");
            Assert.AreEqual(ModelColumnMode.Instability, r.Capacity.Mode);
            // external source: max M1Rd 157.25 kNm (gross section), e2 = 153 mm
            Relative(157.25 * kNm, r.Capacity.MaximumFirstOrderMoment, 0.015, "M1Rd,max against the source");
            Relative(153, r.Capacity.DeflectionAtMaximum, 0.02, "e2* against the source");
            // equilibrium at M0Ed = 137.5 kNm: verified (source), chi_eq < chi*, residual of the equilibrium
            var e = r.Equilibrium;
            Assert.IsTrue(e.Found && e.Verified && r.Verified == true);
            Assert.IsTrue(e.Curvature.Value < r.Capacity.CurvatureAtMaximum);
            Assert.AreEqual(e.DesignMoment.Value, e.SectionMoment.Value, 1e-6 * e.DesignMoment.Value, "residual of the equilibrium");
            Relative(8.831816e-6, e.Curvature.Value, 2e-3, "chi_eq against the reference");
            Relative(201.0891 * kNm, e.DesignMoment.Value, 2e-3, "MEd against the reference");
            Relative(137.5 / 156.083, e.Ratio, 2e-3, "ratio");
            // all the points within the admissible strains, increasing curvature, the last one at the crushing of the concrete
            for (int i = 0; i < r.Points.Count; i++)
            {
                Assert.IsTrue(r.Points[i].ConcreteCompressionStrain <= 0.0035 * (1 + 1e-12), "concrete strain " + i);
                Assert.IsTrue(i == 0 || r.Points[i].Curvature > r.Points[i - 1].Curvature, "curvature " + i);
                Assert.AreEqual(r.Points[i].Moment - r.Points[i].SecondOrderMoment, r.Points[i].FirstOrderCapacity, 1e-6, "M - M2 " + i);
            }
            Assert.IsTrue(r.Points.Last().Ultimate && r.Points.Count(p => p.Ultimate) == 1);
            Assert.AreEqual(0.0035, r.Points.Last().ConcreteCompressionStrain, 1e-9, "the ultimate point is at the crushing of the concrete");
            Assert.IsTrue(r.Capacity.YieldCurvature.Value < r.Capacity.UltimateCurvature);
            var atMaximum = r.Points.Single(p => p.Maximum);
            Assert.AreEqual(r.Capacity.CurvatureAtMaximum, atMaximum.Curvature);
            Assert.AreEqual(r.Capacity.MaximumFirstOrderMoment, atMaximum.FirstOrderCapacity);
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "maximum: M = {0:G9}, M2 = {1:G9}, ec = {2:G6}, es = {3:G6}, yielded {4}, chi_y = {5:G9}",
                atMaximum.Moment, atMaximum.SecondOrderMoment, atMaximum.ConcreteCompressionStrain, atMaximum.SteelStrain, atMaximum.Yielded, r.Capacity.YieldCurvature));
            Assert.AreEqual(0, r.TransverseMomentRatio, 1e-9);
        }

        /// <summary>Nominal curvature of EN 1992-1-1 5.8.8 on the same case, by hand (README): Kr = 1, 1/r0 = 0.012077 1/m, e2 = 173.9 mm, M2 = 86.96 kNm.</summary>
        [TestMethod]
        public void R400NominalCurvatureAndSlenderness()
        {
            var r = Run(R400(), Plain(500, 12000, 137.5));
            var n = r.NominalCurvature;
            Assert.AreEqual(1, n.Kr);
            Relative(0.22058823529411767, n.RelativeAxialForce, 1e-6, "n");
            Relative(0.46858742597789454, n.MechanicalRatio, 1e-6, "omega");
            Relative(360, n.EffectiveDepth, 1e-9, "d = h/2 + is");
            Relative(1.207729468599034e-5, n.BasicCurvature, 1e-6, "1/r0");
            Relative(-0.21782032302755083, n.Beta, 1e-6, "beta");
            Assert.AreEqual(1, n.Kphi);
            Relative(173.91304347826087, n.Deflection, 1e-6, "e2");
            Relative(86.95652173913044 * kNm, n.SecondOrderMoment, 1e-6, "M2");
            Relative(246.264 * kNm, n.ResistingMoment.Value, 3e-3, "MRd(N) of the domain against the reference M(chi_u)");
            Relative(137.5 * kNm + n.SecondOrderMoment, n.DesignMoment.Value, 1e-12, "MEd = M0Ed + M2");
            // slenderness (NTC profile): i = h/√12, λ, ν, λlim = 25/√ν
            var s = r.Slenderness;
            Relative(115.47005383792516, r.Section.RadiusOfGyration, 1e-9, "i");
            Relative(103.92304845413263, s.Slenderness, 1e-9, "lambda");
            Relative(53.2290647422377, s.LimitNtc2018, 1e-6, "lambda lim NTC");
            Relative(41.48788291125234, s.LimitEn1992, 1e-6, "lambda lim EN (A = 1, C = 0.7)");
            Assert.AreEqual(s.LimitNtc2018, s.Limit);
            Assert.IsFalse(s.SecondOrderEffectsNegligible);
            Relative(160000 * 0.85 * 25 / 1.5 + 6 * Math.PI * 144 * 450 / 1.15, r.Section.AxialResistance, 1e-9, "NRd = Ac fcd + As fyd");
            // trace: the codes of the method in order, the value of the capacity
            var codes = r.Trace.Select(t => t.Code).ToList();
            Assert.AreEqual(ModelColumnTraceCodes.Method, codes[0]);
            Assert.IsTrue(codes.IndexOf(ModelColumnTraceCodes.Slenderness) < codes.IndexOf(ModelColumnTraceCodes.LimitSlenderness));
            Assert.IsTrue(codes.IndexOf(ModelColumnTraceCodes.LimitSlenderness) < codes.IndexOf(ModelColumnTraceCodes.ImperfectionEccentricity));
            Assert.AreEqual(r.Capacity.MaximumFirstOrderMoment, r.Trace.Single(t => t.Code == ModelColumnTraceCodes.MaximumFirstOrderMoment).Value);
            Assert.AreEqual(r.Equilibrium.DesignMoment, r.Trace.Single(t => t.Code == ModelColumnTraceCodes.DesignMoment).Value);
            Assert.IsTrue(r.Trace.All(t => t.Description.Length > 0));
            foreach (var t in r.Trace) Console.WriteLine(t);
        }

        /// <summary>
        /// External source (README), graphical reading: 500 × 500, NEd = 1200 kN, M1,max = 552 / 450 / 300 kNm at l0 = 8 / 13 / 18 m, tolerance 5 %; the
        /// independent reference, net section: 554.124 / 454.845 / 308.561 kNm (0.2 %).
        /// </summary>
        [TestMethod]
        public void R500CaseAgainstTheReferenceAndTheSource()
        {
            var section = R500();
            foreach (var (l0, source, reference) in new[] { (8000.0, 552.0, 554.124), (13000.0, 450.0, 454.845), (18000.0, 300.0, 308.561) })
            {
                var r = Run(section, Plain(1200, l0));
                Relative(reference * kNm, r.Capacity.MaximumFirstOrderMoment, 2e-3, "l0 = " + l0 + " against the reference");
                Relative(source * kNm, r.Capacity.MaximumFirstOrderMoment, 0.05, "l0 = " + l0 + " against the source");
                Relative(646.962 * kNm, r.Capacity.UltimateMoment, 2e-3, "M(chi_u)");
                Assert.AreEqual(ModelColumnMode.Instability, r.Capacity.Mode);
                Assert.IsNull(r.Equilibrium);
                Assert.IsNull(r.Verified);
            }
        }

        // ---------------------------------------------------------------- limit cases

        [TestMethod]
        public void ShortColumnFailsAtTheSection()
        {
            var r = Run(R400(), Plain(500, 500));
            Assert.AreEqual(ModelColumnMode.SectionFailure, r.Capacity.Mode);
            Assert.AreEqual(r.Capacity.UltimateCurvature, r.Capacity.CurvatureAtMaximum);
            Relative(245.877 * kNm, r.Capacity.MaximumFirstOrderMoment, 2e-3, "against the reference");
            Relative(r.NominalCurvature.ResistingMoment.Value, r.Capacity.MaximumFirstOrderMoment, 2e-3, "M1Rd,max -> MRd(N) of the domain");
            // l0 = 1 m: the maximum is just before chi_u (M(chi) almost flat there), as the reference
            Relative(244.724 * kNm, Run(R400(), Plain(500, 1000)).Capacity.MaximumFirstOrderMoment, 2e-3, "l0 = 1 m");
        }

        [TestMethod]
        public void NoAxialForce()
        {
            var r = Run(R400(), Plain(0, 12000, 100));
            CollectionAssert.Contains(r.Warnings.ToList(), ModelColumnWarning.NoAxialForce);
            Assert.AreEqual(0, r.SecondOrderStiffness);
            Assert.IsTrue(r.Points.All(p => p.SecondOrderMoment == 0));
            Assert.AreEqual(ModelColumnMode.SectionFailure, r.Capacity.Mode);
            Relative(174.198 * kNm, r.Capacity.MaximumFirstOrderMoment, 2e-3, "against the reference");
            Relative(r.NominalCurvature.ResistingMoment.Value, r.Capacity.MaximumFirstOrderMoment, 2e-3, "MRd(0) of the domain");
            Assert.AreEqual(100 * kNm, r.Equilibrium.DesignMoment.Value, 1e-9);
            Assert.AreEqual(100 * kNm, r.Equilibrium.SectionMoment.Value, 1e-6 * 100 * kNm);
            Assert.IsTrue(double.IsPositiveInfinity(r.Slenderness.Limit));
        }

        [TestMethod]
        public void HighAxialForces()
        {
            var section = R400();
            var r = Run(section, Plain(2000, 6000));
            Relative(121.874 * kNm, r.Capacity.MaximumFirstOrderMoment, 2e-3, "N = 2000 kN, l0 = 6 m");
            Relative(1.035710e-5, r.Capacity.UltimateCurvature, 2e-3, "chi_u at N = 2000 kN");
            r = Run(section, Plain(3200, 3000));
            Relative(10.796 * kNm, r.Capacity.MaximumFirstOrderMoment, 5e-3, "N = 3200 kN (0.97 NRd,c), l0 = 3 m");
            Assert.AreEqual(ModelColumnMode.Instability, r.Capacity.Mode);
            // the maximum between two samples is a point of the curve, between its neighbours
            int m = r.Points.ToList().FindIndex(p => p.Maximum);
            Assert.IsTrue(m > 0 && r.Points[m - 1].Curvature < r.Points[m].Curvature && r.Points[m].Curvature < r.Points[m + 1].Curvature);
            Assert.IsTrue(r.Points.All(p => p.FirstOrderCapacity <= r.Capacity.MaximumFirstOrderMoment));
            Relative(-3290.345 * kN, r.Section.CompressionResistance, 1e-4, "NRd,c of the reference");
            var x = Assert.ThrowsException<ModelColumnException>(() => Run(section, Plain(3400, 3000)));
            Assert.AreEqual(ModelColumnRejection.AxialForceBeyondResistance, x.Reason);
            Assert.AreEqual(ModelColumnRejection.AxialForceBeyondResistance, x.Data[ModelColumnAnalysis.RejectionKey]);
            Relative(-3290.345 * kN, x.Value, 1e-4, "NRd,c of the rejection");
        }

        [TestMethod]
        public void CurvatureFactorEight()
        {
            var r8 = Run(R400(), Plain(500, 12000, c: 8));
            Relative(136.824 * kNm, r8.Capacity.MaximumFirstOrderMoment, 2e-3, "c = 8 against the reference");
            Assert.IsTrue(r8.Capacity.MaximumFirstOrderMoment < Run(R400(), Plain(500, 12000)).Capacity.MaximumFirstOrderMoment);
            Assert.AreEqual(8, r8.CurvatureFactor);
            Relative(500 * kN * 12000.0 * 12000 / 8, r8.SecondOrderStiffness, 1e-12, "k");
        }

        [TestMethod]
        public void CreepStretchesTheConcreteLaw()
        {
            var r1 = Run(R400(), Plain(500, 12000, creep: ModelColumnCreep.Effective(1)));
            var r2 = Run(R400(), Plain(500, 12000, creep: ModelColumnCreep.Effective(2)));
            Relative(147.532 * kNm, r1.Capacity.MaximumFirstOrderMoment, 2e-3, "phi_ef = 1 against the reference");
            Relative(132.482 * kNm, r2.Capacity.MaximumFirstOrderMoment, 2e-3, "phi_ef = 2 against the reference");
            Relative(6.184412e-5, r1.Capacity.UltimateCurvature, 2e-3, "chi_u at phi_ef = 1");
            // the ultimate moment of the stretched law is the one without creep (all strains scaled)
            Relative(246.264 * kNm, r1.Capacity.UltimateMoment, 3e-3, "M(chi_u) at phi_ef = 1");
            Assert.AreEqual(0.0035 * 2, r1.Points.Last().ConcreteCompressionStrain, 1e-9, "ecu (1 + phi_ef)");
            // the nominal curvature ignores the creep here (beta < 0)
            Assert.AreEqual(1, r1.NominalCurvature.Kphi);
        }

        [TestMethod]
        public void CreepPathAtZeroIsBitIdentical()
        {
            var section = R400();
            var request = Plain(500, 12000, 137.5);
            var plain = ModelColumnAnalysis.Calculate(request, Checker(section), section, DomainPointContractTests.LocalAxes(section));
            var creep = ModelColumnAnalysis.Calculate(request, Checker(section), section, DomainPointContractTests.LocalAxes(section), default(System.Threading.CancellationToken), true);
            Assert.AreEqual(plain.Points.Count, creep.Points.Count);
            for (int i = 0; i < plain.Points.Count; i++)
            {
                Assert.AreEqual(plain.Points[i].Curvature, creep.Points[i].Curvature, 0.0, "chi " + i);
                Assert.AreEqual(plain.Points[i].Moment, creep.Points[i].Moment, 0.0, "M " + i);
                Assert.AreEqual(plain.Points[i].ReferenceStrain, creep.Points[i].ReferenceStrain, 0.0, "e0 " + i);
            }
            Assert.AreEqual(plain.Capacity.MaximumFirstOrderMoment, creep.Capacity.MaximumFirstOrderMoment, 0.0);
            Assert.AreEqual(plain.Equilibrium.Curvature.Value, creep.Equilibrium.Curvature.Value, 0.0);
        }

        // ---------------------------------------------------------------- imperfections, minimum eccentricity, creep (5.19)

        [TestMethod]
        public void ImperfectionsAndMinimumEccentricity()
        {
            var section = R400();
            ModelColumnResult R(ModelColumnImperfection i, ModelColumnMinimumEccentricity m, double? m0 = 10)
                => Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 6000, m0 * kNm, imperfection: i, minimumEccentricity: m));
            // EN 5.2(7)a with l = 6 m: αh = 2/√6, θi = αh/200, ei = θi l0/2 (by hand: 24.4949 mm); the default of the request
            var en = R(null, ModelColumnMinimumEccentricity.None);
            Assert.AreEqual(ModelColumnImperfectionKind.En1992Inclination, en.Imperfection.Kind);
            Assert.AreEqual(2 / Math.Sqrt(6), en.Imperfection.HeightFactor.Value, 1e-12);
            Assert.AreEqual(1, en.Imperfection.MemberFactor.Value, 1e-12);
            Assert.AreEqual(1.0 / 200 * 2 / Math.Sqrt(6), en.Imperfection.Inclination.Value, 1e-12);
            Assert.AreEqual(24.494897427831784, en.Imperfection.Eccentricity, 1e-12);
            Assert.AreEqual(500 * kN * 24.494897427831784, en.Imperfection.Moment, 1e-6);
            Assert.AreEqual(10 * kNm + 500 * kN * 24.494897427831784, en.FirstOrder.TotalMoment.Value, 1e-6);
            // the slenderness does not change the curve: the capacity is the one without imperfection, the allowed external moment is reduced by N ei
            Assert.AreEqual(en.Capacity.MaximumFirstOrderMoment - en.Imperfection.Moment, en.Capacity.MaximumAppliedMoment.Value, 1e-6);
            Assert.AreEqual(20, R(ModelColumnImperfection.Ntc2018Straightness, ModelColumnMinimumEccentricity.None).Imperfection.Eccentricity, 1e-12);
            Assert.AreEqual(30, R(ModelColumnImperfection.En1992Simplified, ModelColumnMinimumEccentricity.None).Imperfection.Eccentricity, 1e-12);
            Assert.AreEqual(12.5, R(ModelColumnImperfection.Assigned(12.5), ModelColumnMinimumEccentricity.None).Imperfection.Eccentricity, 1e-12);
            Assert.AreEqual(0, R(ModelColumnImperfection.None, ModelColumnMinimumEccentricity.None).Imperfection.Moment);
            // αh limits and m: l = 1 m -> αh = 1; l = 16 m -> 2/4 below 2/3 -> 2/3; m = 2 -> αm = √0.75
            var limits = Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 1000, 0, imperfection: ModelColumnImperfection.En1992Inclination(1.0 / 300, 2)));
            Assert.AreEqual(1, limits.Imperfection.HeightFactor.Value, 1e-12);
            Assert.AreEqual(Math.Sqrt(0.75), limits.Imperfection.MemberFactor.Value, 1e-12);
            Assert.AreEqual(1.0 / 300 * Math.Sqrt(0.75) * 6000, limits.Imperfection.Eccentricity, 1e-12);
            Assert.AreEqual(2.0 / 3, Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 16000, 0)).Imperfection.HeightFactor.Value, 1e-12);
            // minimum eccentricity as a minimum of the total: NTC (default of the profile) l0/200 = 60 mm, EN h/30 → 20 mm
            var ntc = R(ModelColumnImperfection.En1992Inclination(), ModelColumnMinimumEccentricity.Standard, 0);
            Assert.AreEqual(ModelColumnMinimumEccentricity.Ntc2018, ntc.FirstOrder.MinimumEccentricityRule);
            Assert.AreEqual(60, ntc.FirstOrder.MinimumEccentricity.Value, 1e-12);
            Assert.IsTrue(ntc.FirstOrder.MinimumGoverns);
            Assert.AreEqual(500 * kN * 60, ntc.FirstOrder.TotalMoment.Value, 1e-6);
            var enMinimum = R(ModelColumnImperfection.None, ModelColumnMinimumEccentricity.En1992, 0);
            Assert.AreEqual(20, enMinimum.FirstOrder.MinimumEccentricity.Value, 1e-12);
            Assert.AreEqual(500 * kN * 20, enMinimum.FirstOrder.TotalMoment.Value, 1e-6);
            Assert.IsFalse(R(ModelColumnImperfection.En1992Inclination(), ModelColumnMinimumEccentricity.Ntc2018, 100).FirstOrder.MinimumGoverns);
            // EN profile: minimum h/30 by default, λlim of (5.13N)
            var ec2 = Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 6000, 0), new StandardEN1992p11 { AlphaCC = 0.85 });
            Assert.AreEqual(ModelColumnProfile.EN1992p11, ec2.Profile);
            Assert.AreEqual(ModelColumnMinimumEccentricity.En1992, ec2.FirstOrder.MinimumEccentricityRule);
            Assert.AreEqual(ec2.Slenderness.LimitEn1992, ec2.Slenderness.Limit);
        }

        [TestMethod]
        public void CreepFromTheFinalCoefficient()
        {
            var section = R400();
            // (5.19) with the imperfection in both moments: φef = φ∞ (M0Eqp + |NEqp| ei)/(M0Ed + |NEd| ei)
            var creep = ModelColumnCreep.FromFinalCoefficient(2, 30 * kNm, -350 * kN);
            var r = Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 6000, 47.5 * kNm, imperfection: ModelColumnImperfection.Assigned(20), creep: creep));
            double ratio = (30 * kNm + 350 * kN * 20) / (47.5 * kNm + 500 * kN * 20);
            Assert.AreEqual(ratio, r.Creep.MomentRatio.Value, 1e-12);
            Assert.AreEqual(2 * ratio, r.Creep.EffectiveRatio, 1e-12);
            Relative(1 / (1 + 0.2 * 2 * ratio), r.Slenderness.FactorA, 1e-12, "A");
            Assert.IsTrue(r.Capacity.MaximumFirstOrderMoment < Run(section, Plain(500, 12000)).Capacity.MaximumFirstOrderMoment);
            // 5.8.4(4): φ = 0 with λ = 104 > 75 is not admissible: warning; with λ ≤ 75, φ∞ ≤ 2 and M0Ed/NEd ≥ h no warning
            var neglected = Run(section, new ModelColumnRequest(-500 * kN, 0, 12000, 6000, 47.5 * kNm, creep: ModelColumnCreep.Neglected(1.5)));
            Assert.IsFalse(neglected.Creep.SlendernessCondition);
            CollectionAssert.Contains(neglected.Warnings.ToList(), ModelColumnWarning.CreepNeglectedWithoutConditions);
            var admissible = Run(section, new ModelColumnRequest(-500 * kN, 0, 8000, 4000, 250 * kNm, creep: ModelColumnCreep.Neglected(1.5)));
            Assert.IsTrue(admissible.Creep.NeglectAdmissible, "λ = 69.3, φ∞ = 1.5, M0Ed/NEd = 500 mm + ei ≥ 400 mm");
            CollectionAssert.DoesNotContain(admissible.Warnings.ToList(), ModelColumnWarning.CreepNeglectedWithoutConditions);
            var unknown = Run(section, new ModelColumnRequest(-500 * kN, 0, 8000, 4000, 250 * kNm));
            Assert.IsNull(unknown.Creep.FinalCoefficientCondition);
            CollectionAssert.Contains(unknown.Warnings.ToList(), ModelColumnWarning.CreepNeglectedWithoutConditions);
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Assert.ThrowsException<ModelColumnException>(() => Run(section,
                new ModelColumnRequest(-500 * kN, 0, 12000, 6000, null, creep: creep))).Reason, "(5.19) needs M0Ed");
        }

        // ---------------------------------------------------------------- properties

        [TestMethod]
        public void MonotoneInTheEffectiveLengthAndInTheCreep()
        {
            var section = R400();
            double previous = double.PositiveInfinity;
            foreach (double l0 in new[] { 2000.0, 4000, 6000, 9000, 12000, 16000 })
            {
                double m = Run(section, Plain(500, l0)).Capacity.MaximumFirstOrderMoment;
                Assert.IsTrue(m < previous, "l0 = " + l0);
                previous = m;
            }
            previous = double.PositiveInfinity;
            foreach (double phi in new[] { 0.0, 0.5, 1, 1.5, 2.5 })
            {
                double m = Run(section, Plain(500, 9000, creep: ModelColumnCreep.Effective(phi))).Capacity.MaximumFirstOrderMoment;
                Assert.IsTrue(m < previous, "phi_ef = " + phi);
                previous = m;
            }
        }

        [TestMethod]
        public void SymmetricSectionsAndDirections()
        {
            // square with 8 bars symmetric about the axes and the diagonals: θ = 0, 90, 180, 270 give the same column
            var square = Bars(400, 400, 25, new[] { -160.0, -160, 20 }, new[] { 0.0, -160, 20 }, new[] { 160.0, -160, 20 }, new[] { 160.0, 0, 20 },
                new[] { 160.0, 160, 20 }, new[] { 0.0, 160, 20 }, new[] { -160.0, 160, 20 }, new[] { -160.0, 0, 20 });
            var reference = Run(square, Plain(800, 8000, 60));
            foreach (double theta in new[] { 90.0, 180, 270 })
            {
                var r = Run(square, Plain(800, 8000, 60, direction: theta));
                Relative(reference.Capacity.MaximumFirstOrderMoment, r.Capacity.MaximumFirstOrderMoment, 1e-9, "theta = " + theta);
                Relative(reference.Equilibrium.Curvature.Value, r.Equilibrium.Curvature.Value, 1e-8, "chi_eq theta = " + theta);
                Assert.IsTrue(r.TransverseMomentRatio < 1e-9, "transverse theta = " + theta);
            }
            // 72-gon with 12 bars at 30°: θ = 30° is a plane of symmetry, as θ = 0°
            var circle = Circle(500, 72, 12, 200, 20);
            var c0 = Run(circle, Plain(1000, 7000));
            var c30 = Run(circle, Plain(1000, 7000, direction: 30));
            Relative(c0.Capacity.MaximumFirstOrderMoment, c30.Capacity.MaximumFirstOrderMoment, 1e-8, "circle theta = 30");
            Assert.IsTrue(c30.TransverseMomentRatio < 1e-8, "circle transverse " + c30.TransverseMomentRatio);
        }

        [TestMethod]
        public void AsymmetricSectionTransverseMoment()
        {
            // bars only at one corner region: bending at 45° about a non-principal direction gives a transverse moment
            var section = Bars(300, 600, 25, new[] { -110.0, -260, 20 }, new[] { 110.0, -260, 20 }, new[] { -110.0, 260, 20 }, new[] { 110.0, 260, 20 });
            var x = Assert.ThrowsException<ModelColumnException>(() => Run(section, Plain(600, 6000, direction: 45)));
            Assert.AreEqual(ModelColumnRejection.TransverseMomentBeyondLimit, x.Reason);
            Assert.IsTrue(x.Value > 0.01);
            var accepted = Run(section, new ModelColumnRequest(-600 * kN, 45, 6000, 3000, null, imperfection: ModelColumnImperfection.None,
                minimumEccentricity: ModelColumnMinimumEccentricity.None, rejectTransverseMoment: false));
            CollectionAssert.Contains(accepted.Warnings.ToList(), ModelColumnWarning.TransverseMoment);
            Assert.AreEqual(x.Value, accepted.TransverseMomentRatio, 1e-12);
            // the principal directions of the same section have no transverse moment
            Assert.IsTrue(Run(section, Plain(600, 6000, direction: 90)).TransverseMomentRatio < 1e-9);
        }

        [TestMethod]
        public void Rejections()
        {
            var section = R400();
            ModelColumnRejection Reason(Action action) => Assert.ThrowsException<ModelColumnException>(action).Reason;
            Assert.AreEqual(ModelColumnRejection.TensionAxialForce, Reason(() => Run(section, Plain(-10, 6000))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, Plain(500, 6000, c: 7.9))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, Plain(500, 6000, c: 10.1))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, Plain(500, 0))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, Plain(500, 6000, -1))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, new ModelColumnRequest(-500 * kN, 0, 6000, 3000, steps: 5))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, new ModelColumnRequest(-500 * kN, 0, 6000, 3000,
                imperfection: ModelColumnImperfection.Assigned(-1)))));
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => Run(section, new ModelColumnRequest(-500 * kN, 0, 6000, 3000,
                creep: ModelColumnCreep.Effective(-0.1)))));
            Assert.AreEqual(ModelColumnRejection.UnsupportedStandard, Reason(() => Run(section, Plain(500, 6000), new StandardModelCode2010())));
            var other = R400();
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Reason(() => ModelColumnAnalysis.Calculate(Plain(500, 6000), Checker(section), other,
                DomainPointContractTests.LocalAxes(other))));
            var plain = Rectangle(400, 400, 25, 200000, 450, 40);
            Assert.AreEqual(ModelColumnRejection.UnsupportedSection, Reason(() => Run(plain, Plain(500, 6000))));
        }

        // ---------------------------------------------------------------- biaxial bending (EN 1992-1-1 5.8.9)

        [TestMethod]
        public void BiaxialBending()
        {
            var square = Bars(400, 400, 25, new[] { -160.0, -160, 20 }, new[] { 0.0, -160, 20 }, new[] { 160.0, -160, 20 }, new[] { 160.0, 0, 20 },
                new[] { 160.0, 160, 20 }, new[] { 0.0, 160, 20 }, new[] { -160.0, 160, 20 }, new[] { -160.0, 0, 20 });
            var x = Run(square, Plain(800, 6000, 80));
            var y = Run(square, Plain(800, 6000, 10, direction: 90));
            var separate = ModelColumnBiaxialCheck.Calculate(x, y);
            Assert.AreEqual(1, separate.SlendernessRatio, 1e-12);
            Assert.IsTrue(separate.SlendernessCondition);
            Assert.AreEqual(x.Equilibrium.DesignMoment.Value / y.Equilibrium.DesignMoment.Value, separate.EccentricityRatio.Value, 1e-9);
            Assert.IsTrue(separate.SeparateChecksSufficient && separate.Verified);
            Assert.IsNull(separate.Utilization);
            var y2 = Run(square, Plain(800, 6000, 60, direction: 90));
            var both = ModelColumnBiaxialCheck.Calculate(x, y2);
            Assert.IsFalse(both.SeparateChecksSufficient);
            double nu = 800 * kN / x.Section.AxialResistance;
            Assert.AreEqual(nu, both.RelativeAxialForce, 1e-12);
            Assert.AreEqual(nu <= 0.1 ? 1 : 1 + 0.5 * (nu - 0.1) / 0.6, both.Exponent, 1e-12);
            double expected = Math.Pow(x.Equilibrium.DesignMoment.Value / x.NominalCurvature.ResistingMoment.Value, both.Exponent)
                + Math.Pow(y2.Equilibrium.DesignMoment.Value / y2.NominalCurvature.ResistingMoment.Value, both.Exponent);
            Assert.AreEqual(expected, both.Utilization.Value, 1e-12);
            Assert.AreEqual(expected <= 1, both.Verified);
            Assert.AreEqual(2, ModelColumnBiaxialCheck.Calculate(x, y2, true).Exponent);
            Assert.AreEqual(1, ModelColumnBiaxialCheck.Exponent(0.05));
            Assert.AreEqual(1.5, ModelColumnBiaxialCheck.Exponent(0.7), 1e-12);
            Assert.AreEqual(1.75, ModelColumnBiaxialCheck.Exponent(0.85), 1e-12);
            Assert.AreEqual(2, ModelColumnBiaxialCheck.Exponent(1.2));
        }
    }
}
