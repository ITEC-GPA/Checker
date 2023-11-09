using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace ConcreteTests
{
    [TestClass]
    public class ValidationTestFailureDomainCheckFRC : ConcreteTestBase
    {
        public Point3d ExcelSolution_1 = new Point3d(115.0 * 1000000, 0, 0);
        public Point3d ExcelSolution_2 = new Point3d(28.5 * 1000000, 0, 0);
        public Point3d ExcelSolution_3 = new Point3d(141.0 * 1000000, 0, 0);
        public Point3d ExcelSolution_4 = new Point3d(128.9 * 1000000, 0, 0);
        public Point3d ExcelSolution_5 = new Point3d(8.1 * 1000000, 0, 0);
        public Point3d ExcelSolution_6 = new Point3d(9.8 * 1000000, 0, 0);

        public Point3d ConcribeSolution_1 = new Point3d(117.0 * 1000000, 0, 0);
        public Point3d ConcribeSolution_2 = new Point3d(28.0 * 1000000, 0, 0);
        public Point3d ConcribeSolution_3 = new Point3d(139.56 * 1000000, 0, 0);
        public Point3d ConcribeSolution_4 = new Point3d(129.45 * 1000000, 0, 0);
        public Point3d ConcribeSolution_5 = new Point3d(9.62 * 1000000, 0, 0);
        public Point3d ConcribeSolution_6 = new Point3d(11.04 * 1000000, 0, 0);

        #region Slab Test 1

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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1);
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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1);
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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1);
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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1);
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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1, 7);
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
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_1, ExcelSolution_1);
        }

        #endregion

        #region Slab Test 2

        [TestMethod]
        public void ConCribeTest2_1()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2, 9);
        }

        [TestMethod]
        public void ConCribeTest2_2()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2);
        }

        [TestMethod]
        public void ConCribeTest2_3()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2);
        }

        [TestMethod]
        public void ConCribeTest2_4()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2); ;
        }

        [TestMethod]
        [Ignore("The search for the domain point does not converge.")]
        public void ConCribeTest2_5()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2);
        }

        [TestMethod]
        [Ignore("The search for the domain point does not converge.")]
        public void ConCribeTest2_6()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFU(height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C30/37_15kg/m3", 30, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                3.07, 2.65, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteMaterial);
            ConCribeCheck(point, ConcribeSolution_2, ExcelSolution_2);
        }

        #endregion

        #region Slab Test 3

        [TestMethod]
        public void ConCribeTest3_1()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3);
        }

        [TestMethod]
        public void ConCribeTest3_2()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3);
        }

        [TestMethod]
        public void ConCribeTest3_3()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3);
        }

        [TestMethod]
        public void ConCribeTest3_4()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3);
        }

        [TestMethod]
        public void ConCribeTest3_5()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3, 8.5);
        }

        [TestMethod]
        [Ignore("With ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock, it does not converge.")]
        public void ConCribeTest3_6()
        {
            double rebarDiameter = 12;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 10, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_3, ExcelSolution_3, 12);
        }

        #endregion

        #region Slab Test 4

        [TestMethod]
        public void ConCribeTest4_1()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4);
        }

        [TestMethod]
        public void ConCribeTest4_2()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4);
        }

        [TestMethod]
        public void ConCribeTest4_3()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4);
        }

        [TestMethod]
        public void ConCribeTest4_4()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4);
        }

        [TestMethod]
        public void ConCribeTest4_5()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4, 7.5);
        }

        [TestMethod]
        public void ConCribeTest4_6()
        {
            double rebarDiameter = 16;
            double height = 300;
            double width = 1000;
            double concreteCover = 40;
            double epsfU = CalculateEpsilonFUFib(height, concreteCover);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C40/50_20kg/m3", 40, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.StressBlock,
                2.75 * 0.45, 3.25 * 0.33, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.RigidPlastic, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, concreteCover, rebarDiameter, 5, concreteMaterial, rebarMaterial);
            ConCribeCheck(point, ConcribeSolution_4, ExcelSolution_4, 6);
        }

        #endregion

        #region Ground Slab

        [TestMethod]
        public void ConCribeTest5_1()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFUCSTR();
            double sigmaR1 = 0.74;
            double sigmaR4 = 0.42;
            double sigmaR5 = CalculateSigmaR5(epsfU, sigmaR1, sigmaR4);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                sigmaR1, sigmaR5, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            StandardModelCode2010 standardModelCode2010 = GetConcribeStandard();

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial, standardModelCode2010);
            ConCribeCheck(point, ConcribeSolution_5, ExcelSolution_5);
        }

        [TestMethod]
        public void ConCribeTest5_2()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFUCSTR();
            double sigmaR1 = 0.74;
            double sigmaR4 = 0.42;
            double sigmaR5 = CalculateSigmaR5(epsfU, sigmaR1, sigmaR4);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                sigmaR1, sigmaR5, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            StandardModelCode2010 standardModelCode2010 = GetConcribeStandard();

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial, standardModelCode2010);
            ConCribeCheck(point, ConcribeSolution_5, ExcelSolution_5);
        }

        [TestMethod]
        public void ConCribeTest6_1()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFUCSTR();
            double sigmaR1 = 0.81;
            double sigmaR4 = 0.51;
            double sigmaR5 = CalculateSigmaR5(epsfU, sigmaR1, sigmaR4);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.ParabolaRectangle,
                sigmaR1, sigmaR5, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            StandardModelCode2010 standardModelCode2010 = GetConcribeStandard();

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial, standardModelCode2010);
            ConCribeCheck(point, ConcribeSolution_6, ExcelSolution_6);
        }

        [TestMethod]
        public void ConCribeTest6_2()
        {
            double height = 200;
            double width = 1000;
            double epsfU = CalculateEpsilonFUCSTR();
            double sigmaR1 = 0.81;
            double sigmaR4 = 0.51;
            double sigmaR5 = CalculateSigmaR5(epsfU, sigmaR1, sigmaR4);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("C35/45_4kg/m3", 35, ConcreteMaterialEuropeanCommon.CompressionStressStrainDiagrams.Bilinear,
                sigmaR1, sigmaR5, 0.0001, epsfU, ConcreteMaterialEuropeanCommon.TensionStressStrainDiagrams.Bilinear, ConcreteMaterialEuropeanCommon.ConcreteTypes.FRC);
            SteelMaterial rebarMaterial = SteelMaterialEN1992Data.B500C;

            StandardModelCode2010 standardModelCode2010 = GetConcribeStandard();

            FailureDomain.FailureDomainPoint point = GetConCribeTest(width, height, 0, 8, 0, concreteMaterial, rebarMaterial, standardModelCode2010);
            ConCribeCheck(point, ConcribeSolution_6, ExcelSolution_6);
        }

        #endregion

        #region Private Methods

        private FailureDomain.FailureDomainPoint GetConCribeTest(double width, double height, double concreteCover, double rebarDiameter, int numbOfRebars,
            ConcreteMaterialModelCode2010 concreteMaterial, SteelMaterial rebarMaterial, StandardModelCode2010 standard = null)
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
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

            double delta = (width - 2 * concreteCover) / (numbOfRebars - 1);

            List<ReinforcedConcreteRebar> rebars = new List<ReinforcedConcreteRebar>();
            for (int i = 0; i < numbOfRebars; i++)
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + i * delta, concreteCover)));

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);
            section.AddRebars(rebars);

            if (standard == null)
                standard = new StandardModelCode2010();

            ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomain.FailureDomainPoint point = sectionChecker.CalculatePlasticFailureDomainPoint(force);
            return point;
        }

        private FailureDomain.FailureDomainPoint GetConCribeTest(double width, double height, ConcreteMaterialModelCode2010 concreteMaterial)
        {
            ReinforcedConcreteSection section = new ReinforcedConcreteSection(GetRectangularShape(width, height), concreteMaterial);
            StandardModelCode2010 standard = new StandardModelCode2010();

            ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 100 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section));

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomain.FailureDomainPoint point = sectionChecker.CalculatePlasticFailureDomainPoint(force);
            return point;
        }

        private void ConCribeCheck(FailureDomain.FailureDomainPoint point, Point3d expPoint, Point3d expPointExcel, double tolerancePercent = 5)
        {
            Console.WriteLine($"GPC Concrete Checker: " +
                $"N = {Math.Round(point.NRd / 1000, 2)} kN," +
                $"Mx = {Math.Round(point.MxRd / 1000000, 2)} kNm," +
                $"My = {Math.Round(point.MyRd / 1000000, 2)} kNm,");

            Console.WriteLine($"Concribe expected value: " +
                $"N = {Math.Round(expPoint.Z / 1000, 1)} kN," +
                $"Mx = {Math.Round(expPoint.X / 1000000, 2)} kNm," +
                $"My = {Math.Round(expPoint.Y / 1000000, 2)} kNm,");

            Console.WriteLine($"Excel expected value: " +
                $"N = {Math.Round(expPointExcel.Z / 1000, 2)} kN," +
                $"Mx = {Math.Round(expPointExcel.X / 1000000, 2)} kNm," +
                $"My = {Math.Round(expPointExcel.Y / 1000000, 2)} kNm,");

            Console.WriteLine($"Error: {Math.Round((point.MxRd - expPoint.X) / expPoint.X * 100, 2)} %");

            Assert.IsTrue(Math.Abs(point.NRd / 1000) < tolerancePercent);
            Assert.IsTrue(Math.Abs(Math.Round(point.MyRd / 1000000, 1)) < tolerancePercent);
            Assert.IsTrue(Math.Abs((point.MxRd - expPoint.X) / expPoint.X * 100) < tolerancePercent);
        }

        private double CalculateEpsilonFUFib(double height, double concreteCover, double epsilonCU = 0.0035, double epsilonSY = 0.0022)
        {
            //return 0.00222;
            return 0.0032;
            //return (height * (epsilonCU + epsilonSY)) / (height - concreteCover) - epsilonCU;
        }

        private double CalculateEpsilonFU(double height, double concreteCover, double epsilonCU = 0.0035, double epsilonSY = 0.0022)
        {
            // valore rottura delle fibre nel caso di sezioni armate second sTruc
            return 0.0032;
            //return 0.02;
            //return (height * (epsilonCU + epsilonSY)) / (height - concreteCover) - epsilonCU;
        }

        private double CalculateEpsilonFUCSTR()
        {
            return 0.025;
        }

        private double CalculateEpsilonFU(double height)
        {
            // valore rottura delle fibre nel caso di sezioni non armate second sTruc
            return 0.0022;
        }

        private double CalculateSigmaR5(double epsilonFU, double sigmaR1, double sigmaR4)
        {
            return sigmaR1 - ((epsilonFU / 0.025) * (sigmaR1 - sigmaR4));
        }

        private StandardModelCode2010 GetConcribeStandard()
        {
            StandardModelCode2010 standardModelCode2010 = new StandardModelCode2010();
            standardModelCode2010.GammaF = 1.0;
            standardModelCode2010.AlphaCC = 0.85;
            standardModelCode2010.AlphaCT = 0.85;
            return standardModelCode2010;

        }

        #endregion
    }
}
