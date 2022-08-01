using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class ValidationTestFailureDomainCheckFRC : ConcreteTestBase
    {
        [TestMethod]
        public void ConCribeTest1_1()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest1_2()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest1_3()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest1_4()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest1_5()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest1_6()
        {
            double rebarDiameter = 8;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(117 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest2_1()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, 0.0022, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            Point3d expPoint = new Point3d(28 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint, 10);
        }

        [TestMethod]
        public void ConCribeTest2_2()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, 0.0022, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            Point3d expPoint = new Point3d(28 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest2_3()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, 0.0022, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            Point3d expPoint = new Point3d(28 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint, 8);
        }

        [TestMethod]
        public void ConCribeTest2_4()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, 0.0022, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            Point3d expPoint = new Point3d(28 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest3_1()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(139.56 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest3_2()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(139.56 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest3_3()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(139.56 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest3_4()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(139.56 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest4_1()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(129.45 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest4_2()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(129.45 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest4_3()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(129.45 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest4_4()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFU(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.83 * 0.45, 3.34 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(129.45 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest5_1()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                0.74, 0.42, 0.0001, 0.0022, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(9.62 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }

        [TestMethod]
        public void ConCribeTest5_2()
        {
            double height = 200;
            double width = 1000;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                0.74, 0.42, 0.0001, 0.02, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterial.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial);
            Point3d expPoint = new Point3d(9.62 * 1000000, 0, 0);

            ConCribeCheck(point, expPoint);
        }


        private FailureDomain.FailureDomainPoint GetConCribeTest(double width, double height, double concreteCover, double rebarDiameter, int numbOfRebars,
            ConcreteMaterialModelCode2010 concreteMaterial, SteelMaterial rebarMaterial)
		{
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width / 2.0, 0),
                new Point2d(width, 0),
                new Point2d(width, height / 2.0),
                new Point2d(width, height),
                new Point2d(width / 2.0, height),
                new Point2d(0, height),
                new Point2d(0, height / 2.0)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

            double delta = (width - 2 * concreteCover) / (numbOfRebars - 1);

            List<ReinforcedConcreteRebar> rebars = new List<ReinforcedConcreteRebar>();
            for (int i = 0; i < numbOfRebars; i++)			
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + i * delta, concreteCover)));			

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            
            StandardModelCode2010 standard = new StandardModelCode2010();

            ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomain.FailureDomainPoint point = sectionChecker.CalculatePlasticFailureDomainPoint(force);
            return point;
        }

        private FailureDomain.FailureDomainPoint GetConCribeTest(double width, double height, ConcreteMaterialModelCode2010 concreteMaterial)
        {
            ShapeEx shapeEx = new ShapeEx(GetRectangularShape(width, height), concreteMaterial);
            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            StandardModelCode2010 standard = new StandardModelCode2010();

            ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomain.FailureDomainPoint point = sectionChecker.CalculatePlasticFailureDomainPoint(force);
            return point;
        }

        private void ConCribeCheck(FailureDomain.FailureDomainPoint point, Point3d expPoint, double tolerancePercent = 5)
		{
            Console.WriteLine($"GPC Concrete Checker: " +
                $"N = {Math.Round(point.NRd / 1000, 1)} kN," +
                $"Mx = {Math.Round(point.MxRd / 1000000, 1)} kNm," +
                $"My = {Math.Round(point.MyRd / 1000000, 1)} kNm,");

            Console.WriteLine($"Concribe expected value: " +
                $"N = {Math.Round(expPoint.Z / 1000, 1)} kN," +
                $"Mx = {Math.Round(expPoint.X / 1000000, 1)} kNm," +
                $"My = {Math.Round(expPoint.Y / 1000000, 1)} kNm,");

            Assert.IsTrue(Math.Abs(point.NRd / 1000) < tolerancePercent);
            Assert.IsTrue(Math.Abs(Math.Round(point.MyRd / 1000000, 1)) < tolerancePercent);
            Assert.IsTrue(Math.Abs((point.MxRd - expPoint.X) / expPoint.X * 100) < tolerancePercent);
        }

        private double CalculateEpsilonFU(double height, double concreteCover, double epsilonCU = 0.0035, double epsilonSY = 0.0022)
		{
            return (height * (epsilonCU + epsilonSY)) / (height - concreteCover) - epsilonCU;
        }
    }
}
