using System;
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
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    public partial class BridgeElasticStagesTests
    {
        private static CoordinateSystem Axes(double x, double y, double radians = 0)
        {
            double c = Math.Cos(radians), s = Math.Sin(radians);
            return new CoordinateSystem(new Point2d(x, y), new Vector3d(-c, -s, 0), new Vector3d(s, -c, 0));
        }
        private static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, CoordinateSystem axes, double phi)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, phi, 0, true, 16);
            return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardNTC2018Concrete(), true, -1, new StandardEN1993p11());
        }
        private void RecordPlane(string label, StressAnalysisResult result, double phi)
        {
            Assert.IsNotNull(result.StrainPlane, string.Join("; ", result.GetLog()));
            var p = result.StrainPlane;
            TestContext.WriteLine("{0}: ref=({1:R},{2:R}); raw epsilon0={3:R}; raw gradientX={4:R}; raw gradientY={5:R}; physical factor={6:R}; iteration ID={7}",
                label, p.ReferencePoint.X, p.ReferencePoint.Y, p.StrainReferencePoint, p.ChiX, p.ChiY, 1 + phi, p.Id);
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d)] [DataRow(.5d)] [DataRow(2.2d)] [DataRow(5d)]
        public void NativePlane_PhysicalStrainMatchesAllThreeMaterials(double phi)
        {
            var result = Solve(phi, -200000, 2e9); RecordPlane("G2", result, phi);
            var plane = result.StrainPlane;
            foreach (var v in result.GetStructuralSteelVerticesTension(phi))
                Near(v.tension / Ea, (1 + phi) * plane.GetStrain(v.point), "physical steel strain", 1e-10);
            foreach (var v in result.GetConcreteVerticesTension(phi))
                Near(v.tension / (Ec / (1 + phi)), (1 + phi) * plane.GetStrain(v.point), "physical concrete strain", 1e-10);
            foreach (var v in result.GetRebarsTension(phi))
                Near(v.tension / Es, (1 + phi) * plane.GetStrain(v.rebar.Position), "physical rebar strain", 1e-10);
            var summary = result.CalculateStrainPlaneResult(true, phi, 0);
            Near(summary.EpsilonSSMax, result.GetStructuralSteelVerticesTension(phi).Max(v => v.tension) / Ea, "summary physical strain", 1e-10);
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d)] [DataRow(2.2d)] [DataRow(5d)]
        public void NativeZero_AfterLoadedSolveHasFiniteZeroPlaneAndZeroStress(double phi)
        {
            var axes = Axes(B / 2, 0); var checker = Checker(Section(), axes, phi);
            var first = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(-200000, 0, 0, 0, 2e9, 0, axes), phi, 0);
            Assert.IsNotNull(first.StrainPlane);
            var zero = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(0, 0, 0, 0, 0, 0, axes), phi, 0);
            RecordPlane("Zero after loaded solve", zero, phi);
            Near(zero.StrainPlane.StrainReferencePoint, 0, "zero strain", 1e-12);
            Near(zero.StrainPlane.ChiX, 0, "zero X gradient", 1e-12); Near(zero.StrainPlane.ChiY, 0, "zero Y gradient", 1e-12);
            foreach (double value in Values(zero, phi)) Near(value, 0, "zero stress", 1e-12);
            var summary = zero.CalculateStrainPlaneResult(true, phi, 0);
            Near(summary.SigmaCMin, 0, "zero concrete minimum"); Near(summary.SigmaSSMax, 0, "zero structural steel maximum");
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(-200d)] [DataRow(0d)] [DataRow(200d)]
        public void Constructor_ExistingHPlacementRemainsUnchanged(double eccentricity)
        {
            var section = new ReinforcedConcreteSection(B, Tc, ConcreteMaterialEN1992Data.C35_45, null, 200, 50,
                null, 200, new SectionH(H, Tw, Bt, Tt, Bb, Tb, "H"), SteelMaterialEN1993Data.S355, 40, eccentricity);
            var steel = section.SteelSections.Single();
            Near(steel.PositionToGlobal(steel.Section.Centroid).X, B / 2 + eccentricity, "steel centering");
            Near(steel.Section.Shape.Fill.Max(p => steel.PositionToGlobal(p).Y), 0, "slab interface");
            Assert.IsFalse(steel.IsInsideConcrete);
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d, 0d, 2e9, 0d)] [DataRow(0d, -200000d, 2e9, 5e8)]
        [DataRow(2.2d, -200000d, 2e9, 5e8)] [DataRow(5d, 200000d, -2e9, -5e8)]
        public void LinearUncrackedSolveConvergesInOneNewtonUpdate(double phi, double n, double mx, double my)
        {
            var axes = Axes(B / 2, 0); var checker = Checker(Section(), axes, phi);
            var r = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(n, 0, 0, 0, mx, my, axes), phi, 0);
            RecordPlane("Biaxial", r, phi); Assert.AreEqual(2, r.StrainPlane.Id, "zero trial ID=1 plus one Newton update for a linear system");
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(30d)] [DataRow(90d)] [DataRow(180d)]
        public void RotatedForceDescription_WithFixedSolverAxesPreservesStress(double degrees)
        {
            var original = Axes(B / 2, 0); var rotated = Axes(B / 2, 0, degrees * Math.PI / 180);
            var checker = Checker(Section(), original, 2.2);
            var force = new ResultBeamForces(-200000, 0, 0, 0, 2e9, 5e8, original);
            var a = Values(checker.SectionSolver.GetLinearStressAnalysisResult(force, 2.2, 0), 2.2);
            var b = Values(checker.SectionSolver.GetLinearStressAnalysisResult(force.ToCoordinateSystem(rotated), 2.2, 0), 2.2);
            for (int i = 0; i < a.Length; i++) Near(b[i], a[i], "same physical force with rotated description");
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(30d)] [DataRow(90d)] [DataRow(180d)]
        public void RotatedSolverReference_WithSamePhysicalForceShouldPreserveStress(double degrees)
        {
            var original = Axes(B / 2, 0); var rotated = Axes(B / 2, 0, degrees * Math.PI / 180);
            var force = new ResultBeamForces(-200000, 0, 0, 0, 2e9, 5e8, original);
            var a = Values(Checker(Section(), original, 2.2).SectionSolver.GetLinearStressAnalysisResult(force, 2.2, 0), 2.2);
            var b = Values(Checker(Section(), rotated, 2.2).SectionSolver.GetLinearStressAnalysisResult(force.ToCoordinateSystem(rotated), 2.2, 0), 2.2);
            TestContext.WriteLine("Rotation {0} degrees: same first global vertex, baseline sigma={1:R}, rotated reference sigma={2:R}", degrees, a[0], b[0]);
            for (int i = 0; i < a.Length; i++) Near(b[i], a[i], "solver reference rotation invariance");
        }
        private static ReinforcedConcreteSection EmbeddedH()
        {
            var shape = new SectionH(300, 10, 200, 20, 200, 20, "Embedded H");
            var section = new ReinforcedConcreteSection(500, 500, ConcreteMaterialEN1992Data.C35_45, null, 200, 50,
                null, 200, shape, SteelMaterialEN1993Data.S355, 40);
            section.SteelSections.Clear();
            section.AddSteelSection(new SteelSectionPosition(new SteelSection(shape, SteelMaterialEN1993Data.S355), Point2d.Origin, 0,
                new Point2d(250, 250), InsertionPointType.Centroid) { IsInsideConcrete = true });
            return section;
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d, -1e6)] [DataRow(0d, 1e6)] [DataRow(2.2d, -1e6)] [DataRow(2.2d, 1e6)]
        public void EmbeddedSteel_LinearSolverMustSubtractLinearConcrete(double phi, double n)
        {
            var axes = Axes(250, 250); var section = EmbeddedH(); var checker = Checker(section, axes, phi);
            var r = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(n, 0, 0, 0, 0, 0, axes), phi, 0);
            RecordPlane("Embedded H", r, phi);
            const double steelArea = 10600, grossConcreteArea = 250000;
            double stiffness = Ec * (grossConcreteArea - steelArea) + Ea * (1 + phi) * steelArea;
            double expectedRawStrain = n / stiffness;
            double raw = r.StrainPlane.GetStrain(new Point2d(250, 250));
            double recoveredN = raw * stiffness;
            TestContext.WriteLine("Expected raw strain={0:R}; actual={1:R}; target N={2:R}; independently recovered N={3:R}; relative force error={4:R}", expectedRawStrain, raw, n, recoveredN, recoveredN / n - 1);
            double concreteDesignStress = section.ConcreteMaterial.CalculateDesignStressConcrete(new StandardNTC2018Concrete(), raw);
            double misplacedConcreteForce = (Ec * raw - concreteDesignStress) * steelArea;
            TestContext.WriteLine("Linear CLS stress={0:R}; nonlinear/design CLS stress={1:R}; excess force from inconsistent subtraction={2:R}; recovered N plus excess={3:R}", Ec * raw, concreteDesignStress, misplacedConcreteForce, recoveredN + misplacedConcreteForce);
            // before the correction the solver subtracted the design stress of the nonlinear diagram instead of the linear one: recoveredN + excess = N
            Near(recoveredN, n, "linear equilibrium with concrete displacement");
        }
        [TestMethod, TestCategory("BridgeDeepDive")]
        public void EmbeddedSteel_SmallCompressionScalingAloneDoesNotDetectWrongStiffness()
        {
            var axes = Axes(250, 250); var checker = Checker(EmbeddedH(), axes, 2.2);
            Func<double, double> strain = n => checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(n, 0, 0, 0, 0, 0, axes), 2.2, 0).StrainPlane.GetStrain(new Point2d(250, 250));
            double one = strain(-1e6), two = strain(-2e6);
            TestContext.WriteLine("epsilon(-1 MN)={0:R}; epsilon(-2 MN)={1:R}; ratio={2:R}", one, two, two / one);
            Assert.AreEqual(2 * one, two, Math.Abs(two) * 1e-5, "linearity under doubled load");
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d, 1e-6)] [DataRow(1e-6, 0d)] [DataRow(1e-6, 2e-6)]
        public void GlobalNeutralAxis_MustLieOnZeroStrainPlane(double gx, double gy)
        {
            var p = new StrainPlane(gx, gy, new Point2d(1000, 500), -.001);
            var line = p.GetNeutralAxis(); Assert.IsNotNull(line);
            TestContext.WriteLine("Start=({0:R},{1:R}), End=({2:R},{3:R}), strain at start={4:R}, strain at end={5:R}", line.Start.X, line.Start.Y, line.End.X, line.End.Y, p.GetStrain(line.Start), p.GetStrain(line.End));
            Assert.AreEqual(0, p.GetStrain(line.Start), 1e-12, "global neutral axis start");
            Assert.AreEqual(0, p.GetStrain(line.End), 1e-12, "global neutral axis end");
        }
        [DataTestMethod, TestCategory("BridgeDeepDive")]
        [DataRow(0d, 1e-6)] [DataRow(1e-6, 0d)] [DataRow(1e-6, 2e-6)]
        public void RelativeNeutralAxis_TranslatedByReferenceIsCorrect(double gx, double gy)
        {
            var p = new StrainPlane(gx, gy, new Point2d(1000, 500), -.001);
            var line = p.GetNeutralAxisRespectReferencePoint(); line.Move(1000, 500);
            Assert.AreEqual(0, p.GetStrain(line.Start), 1e-12); Assert.AreEqual(0, p.GetStrain(line.End), 1e-12);
        }
    }
}
