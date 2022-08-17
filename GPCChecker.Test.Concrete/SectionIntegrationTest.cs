using System;
using System.Collections.Generic;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class SectionIntegrationTest : ConcreteTestBase
    {
        [TestMethod]
        public void RectangularSectionIntegration1()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = -0.002;
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
                                                                        new Point2d(300, 0),
                                                                        new Point2d(300, 500),
                                                                        new Point2d(0, 500) }));

            ConcreteMaterial concreteMaterial = ConcreteMaterialEN1992.C25_30;
            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
            ForceTuple expForceTupleNumerics = new ForceTuple(-2500000, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

            Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration2()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = -0.002;
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500) }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
            ForceTuple expForceTupleNumerics = new ForceTuple(-2500000, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

            Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration3()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = -0.000875;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500) }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
            ForceTuple expForceTupleNumerics = new ForceTuple(-1250000, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

            Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration4()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = +0.0001;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500) }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
            ForceTuple expForceTupleNumerics = new ForceTuple(0, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

            Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration5()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = +0.000;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 500),
                new Point2d(0, 500) }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
            ForceTuple expForceTupleNumerics = new ForceTuple(0, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

            Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration6()
        {
            double chiX = 0.0;
            double chiY = 0.0;
            double strainRefPoint = +0.01;

            var section = GetRectangularSection4Rebars(300, 500, 18, 50, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));

            StandardEN1992p11 standard = new StandardEN1992p11();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

            var rebars = section.GetRebars();

            double axialForce = 0;
            for (int i = 0; i < rebars.Length; i++)
                axialForce += solver.CalculateStressRebar(rebars[i], strainRefPoint) * rebars[i].Area;

            ForceTuple expForceTuple = new ForceTuple(axialForce, 0, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
            Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);
        }

        [TestMethod]
        public void RectangularSectionIntegration8()
        {
            double chiX = 0.0;
            double chiY = -1e-3 / 500;
            double strainRefPoint = +0.0;

            var section = GetRectangularSection4Rebars(300, 500, 18, 50);

            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);
            ForceTuple expForceTuple = new ForceTuple(-255 * 1000, 50 * 1000000, 0);

            Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) / Math.Abs(force.N) < 0.1);
            Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) / Math.Abs(force.Mx) < 0.11);
            if (Math.Abs(force.My - expForceTuple.My) > 1000000)
                Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) / Math.Abs(force.My) < 0.1);
        }

        [TestMethod]
        public void BridgeTest()
        {
            double strainRefPointT = +0.01;
            double strainRefPointC = -0.002;

            ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 50,
                10, 14, 13, 14, 10, 14,
                7, 12,
                4, 22, 13, 20, 4, 22,
                ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);

            StrainPlane strainPlaneT = new StrainPlane(section.Centroid, 0, 0, strainRefPointT);
            StrainPlane strainPlaneC = new StrainPlane(section.Centroid, 0, 0, strainRefPointC);

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            ForceTuple forceT = solver.CalculateSectionForceResultant(strainPlaneT);
            ForceTuple forceC = solver.CalculateSectionForceResultant(strainPlaneC);
            ForceTuple expForceTupleT = new ForceTuple(10789 * 1000, 3574.0 * 1000000, 0);
            ForceTuple expForceTupleC = new ForceTuple(-50170 * 1000, -3374.0 * 1000000, 0);

            CheckFailureDomainLimit(forceT, expForceTupleT);
            CheckFailureDomainLimit(forceC, expForceTupleC);
        }

        [TestMethod]
        public void BridgeTopRebarTest()
        {
            double strainRefPoint = +0.01;

            ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 50,
                10, 14, 13, 14, 10, 14,
                0, 12,
                0, 22, 0, 20, 0, 22,
                ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);

            StrainPlane strainPlane = new StrainPlane(section.Centroid, 0, 0, strainRefPoint);
            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);
            ForceTuple expForce = new ForceTuple(3977 * 1000, -2239.0 * 1000000, 0);

            CheckFailureDomainLimit(force, expForce);
        }

        [TestMethod]
        public void BridgeTopRebarTest_2()
        {
            double strainRefPoint = +0.01;

            ReinforcedConcreteSection section = GetBridgeSection(4600, 1800, 3000, 300, 300, 200, 50,
                10, 14, 0, 14, 10, 14,
                0, 12,
                0, 22, 0, 20, 0, 22,
                ConcreteMaterialEN1992.C25_30, SteelMaterial.B450C);

            StrainPlane strainPlane = new StrainPlane(section.Centroid, 0, 0, strainRefPoint);
            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);
            ForceTuple expForce = new ForceTuple(2410 * 1000, -1357 * 1000000, 0);

            CheckFailureDomainLimit(force, expForce);
        }

        [TestMethod]
        public void RectangularSectionIntegration10()
        {
            double rebarDiameter10 = 10;
            double height = 400;
            double width = 400;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C25_30);
            RebarSectionCircular rebarSection16 = new RebarSectionCircular(rebarDiameter10, SteelMaterial.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(50,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(100,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(150,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(200,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(250,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(300,350,0)),
                new ReinforcedConcreteRebar(rebarSection16, new Point3d(350,350,0)),
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            StrainPlane strainPlane = new StrainPlane(section.Centroid, 0, 0, 0.01);
			ForceTuple force = sectionSolverModelCode2010Test.CalculateSectionForceResultant(strainPlane);
            ForceTuple expForce = new ForceTuple(216 * 1000, -32.5 * 1000000, 0);

            CheckFailureDomainLimit(force, expForce);
        }

        [TestMethod]
        public void RectangularSectionIntegration11()
        {
            double rebarDiameter10 = 10;
            double rebarDiameter26 = 26;
            double height = 400;
            double width = 400;

            ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter10, 7, rebarDiameter26, 7, 50);
            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            StrainPlane strainPlane = new StrainPlane(section.Centroid, 0, 0, 0.01);
            ForceTuple force = sectionSolverModelCode2010Test.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForce = new ForceTuple(1671 * 1000, 186 * 1000000, 0);

            if (expForce.N != 0)
                Assert.IsTrue(Math.Abs((force.N - expForce.N) / expForce.N) < 0.1);
            if (expForce.Mx != 0)
                Assert.IsTrue(Math.Abs((force.Mx - expForce.Mx) / expForce.Mx) < 0.1);
            if (expForce.My != 0)
                Assert.IsTrue(Math.Abs((force.My - expForce.My) / expForce.My) < 0.1);
        }

        [TestMethod]
        public void RectangularSectionIntegration12()
        {
            double rebarDiameter10 = 10;
            double rebarDiameter26 = 26;
            double height = 400;
            double width = 400;

            ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter10, 2, rebarDiameter26, 7, 50);
            SectionSolverModelCode2010Test sectionSolverModelCode2010Test = new SectionSolverModelCode2010Test(section, new StandardNTC2018Concrete());

            StrainPlane strainPlane = new StrainPlane(section.Centroid, 0, 0, 0.01);
            ForceTuple force = sectionSolverModelCode2010Test.CalculateSectionForceResultant(strainPlane);

            ForceTuple expForce = new ForceTuple(1516 * 1000, 209 * 1000000, 0);

            if (expForce.N != 0)
                Assert.IsTrue(Math.Abs((force.N - expForce.N) / expForce.N) < 0.1);
            if (expForce.Mx != 0)
                Assert.IsTrue(Math.Abs((force.Mx - expForce.Mx) / expForce.Mx) < 0.1);
            if (expForce.My != 0)
                Assert.IsTrue(Math.Abs((force.My - expForce.My) / expForce.My) < 0.1);
        }

        [TestMethod]
        public void DesignStressSteel()
        {
            var section = GetRectangularSection4Rebars();
            var rebars = section.GetRebars();

            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

            List<double> stresses = new List<double>();

            for (double i = -67.5; i <= 67.5; i++)
                stresses.Add(solver.CalculateStressRebar(rebars[0], i / 1000));


            foreach (double stress in stresses)
                Console.WriteLine(stress);

            for (int i = 0; i < stresses.Count - 1; i++)
            {
                Assert.IsTrue(stresses[i] <= stresses[i + 1]);
                Assert.IsTrue(Math.Abs(stresses[i]) < rebars[0].RebarMaterial.Fu);
            }

        }
    }
}
