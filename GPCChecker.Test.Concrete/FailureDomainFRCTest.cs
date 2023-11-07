using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
    [TestClass]
    public class FailureDomainFRCTest : ConcreteTestBase
    {
        [TestMethod]
        public void RectangularSectionTest1()
        {
            double rebarDiameter = 18;
            double height = 500;
            double width = 300;
            double concreteCover = 50;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 25,
                ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
                1.0, 1.00, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC,
                0, 0, 0, ConcreteMaterialModelCode2010.CementTypes.ClassN);

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, concreteMaterial);
            StandardEN1992p11 standard = new StandardEN1992p11();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
            //var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain.Domain);

            //ExportToGmsh(plasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain);			

            Point3d maxTraction = new Point3d(0, 0, 464000);
            Point3d minCompression = new Point3d(0, 0, -2850000);

            BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

            ForceTuple maxFT = CalculateAdimensionalForces(section,
                new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
            ForceTuple minFT = CalculateAdimensionalForces(section,
                new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

            Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
            Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

            Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

            FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
                GetLocalCoordinateSystem(section), 1));

            Point3d expDomainPoint = new Point3d(106 * 1000000, 0, 0);

            Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 2.5);
        }

        [TestMethod]
        public void RectangularSectionTest2()
        {
            double h = 500;
            Shape2d shape = GetRectangularShape(h, h);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 25,
                ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear, 1.0, 1.00, 5E-5, 0.01,
                ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC,
                0, 0, 0, ConcreteMaterialModelCode2010.CementTypes.ClassN);

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();

            Point3d maxTraction = new Point3d(0, 0, 166666);
            Point3d minCompression = new Point3d(0, 0, -3537000);

            BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

            ForceTuple maxFT = CalculateAdimensionalForces(section, new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
            ForceTuple minFT = CalculateAdimensionalForces(section, new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

            Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
            Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

            Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

            FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0, GetLocalCoordinateSystem(section), 1));

            Point3d expDomainPoint = new Point3d(40 * 1000000, 0, 0);

            Console.WriteLine(domainPoint.Point.ToString());
            Console.WriteLine(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100);

            Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 5, domainPoint.Point.ToString());
        }

        [TestMethod]
        public void RectangularSectionTest3()
        {
            double height = 1000;
            double width = 300;

            Shape2d shape = GetRectangularShape(width, height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 40, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear,
                 0.6750, 0.50, 0.00195, 0.02, ConcreteMaterial.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC,
                 0, 0, 0, ConcreteMaterial.CementTypes.ClassN);

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);
            StandardEN1992p11 standard = new StandardEN1992p11();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
            //var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain.Domain);

            //ExportToGmsh(plasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain);			

            Point3d maxTraction = new Point3d(0, 0, 100000);
            Point3d minCompression = new Point3d(0, 0, -8000000);

            BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

            ForceTuple maxFT = CalculateAdimensionalForces(section, new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
            ForceTuple minFT = CalculateAdimensionalForces(section, new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

            Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
            Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

            Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

            FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 30 * 1000000, 0,
                GetLocalCoordinateSystem(section), 1));

            Point3d expDomainPoint = new Point3d(54 * 1000000, 0, 0);

            Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 5);
        }

        [TestMethod]
        public void RectangularSectionTest4()
        {
            double height = 1000;
            double width = 300;

            Shape2d shape = GetRectangularShape(width, height);

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 40,
                ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
                 1.0, 1.0, 0.00195, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear);

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, new StandardEN1992p11(), true);
            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain.Domain);

            Point3d maxTraction = new Point3d(0, 0, 200000);
            Point3d minCompression = new Point3d(0, 0, -8000000);

            BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

            ForceTuple maxFT = CalculateAdimensionalForces(section,
                            new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
            ForceTuple minFT = CalculateAdimensionalForces(section,
                new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

            Assert.IsTrue(Math.Abs(maxFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

            Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

            FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
                GetLocalCoordinateSystem(section), 1));

            Point3d expDomainPoint = new Point3d(97 * 1000000, 0, 0);

            Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 2.7);
        }

        [TestMethod]
        public void RectangularSectionTest5()
        {
            double height = 600;
            double width = 300;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ConcreteMaterialModelCode2010 concreteMaterial = ConcreteMaterialModelCode2010Data.C30_37_10;

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, concreteMaterial);
            StandardEN1992p11 standard = new StandardEN1992p11();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
            //var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain.Domain);

            //ExportToGmsh(plasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain);			
        }

        [TestMethod]
        public void RectangularSectionTest6()
        {
            double height = 400;
            double width = 400;
            double rebarDiameter = 20;
            double concreteCover = 50;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(-width / 2.0, -height / 2.0),
                new Point2d(width / 2.0, -height / 2.0),
                new Point2d(width / 2.0, height / 2.0),
                new Point2d(-width / 2.0, height / 2.0)
            }));

            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1992Data.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width / 2.0, -height / 2.0 + concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + height / 2.0)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + height - concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width / 2.0, -height / 2.0 + height - concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + height - concreteCover)),
                new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + height / 2.0))
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, ConcreteMaterialModelCode2010Data.C30_37_15);
            section.AddRebars(rebars);

            StandardModelCode2010 standard = new StandardModelCode2010();
            //ExportToGmsh(section);

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();
            ShowDomainPoints(elasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain.GetMesh());

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResultAsync().Result;
            ShowDomainPoints(plasticFailureDomain.Domain);
            //ExportToGmsh(plasticFailureDomain.Domain.GetMesh());
        }

        [TestMethod]
        public void RectangularSectionTest10()
        {
            double height = 500;
            double width = 300;

            ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 30, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle,
                1.55, 1.80, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, ConcreteMaterial.ConcreteTypes.FRC);

            var section = new ReinforcedConcreteSection(new SectionRectangular(height, width), concreteMaterial);

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, new StandardModelCode2010(), true);

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
            var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain.Domain);
            ShowDomainPoints(elasticFailureDomain.Domain);
            plasticFailureDomain.Domain.GetMesh(out _);
            elasticFailureDomain.Domain.GetMesh(out _);
        }

        [TestMethod]
        public void RectangularSectionTest11()
        {
            double height = 400;
            double width = 400;

            var section = new ReinforcedConcreteSection(new SectionRectangular(width, height), ConcreteMaterialModelCode2010Data.C30_37_5);
            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, new StandardModelCode2010(), true);

            var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();
            ShowDomainPoints(elasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain.GetMesh());

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResultAsync().Result;
            ShowDomainPoints(plasticFailureDomain.Domain);
            //ExportToGmsh(plasticFailureDomain.Domain.GetMesh());
        }

        [TestMethod]
        public void RectangularSectionTest12()
        {
            double height = 400;
            double width = 400;

            StressStrainTable UHCP1SSTComp = new StressStrainTable(
                new double[] { 0, -107, -107 }, new double[] { 0, -0.002378, -0.003709 });
            StressStrainTable UHCP1SSTTens_1 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2, 0.0 }, new double[] { 0, 0.000089, 0.002392, 0.011, 0.028 });
            StressStrainTable UHCP1SSTTens_2 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2, 1.6 }, new double[] { 0, 0.000089, 0.002392, 0.011, 0.0195 });
            StressStrainTable UHCP1SSTTens_3 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2 }, new double[] { 0, 0.000089, 0.002392, 0.011 });
            StressStrainTable UHCP1SSTTens_4 = new StressStrainTable(
                new double[] { 0, 4, 4 }, new double[] { 0, 0.000089, 0.002392 });
            StressStrainTable UHCP1SSTTens_5 = new StressStrainTable(
                new double[] { 0, 4 }, new double[] { 0, 0.000089 });
            ConcreteMaterialModelCode2010 mat1_1 = new ConcreteMaterialModelCode2010("UHPC 107_1", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_1, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_2 = new ConcreteMaterialModelCode2010("UHPC 107_2", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_2, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_3 = new ConcreteMaterialModelCode2010("UHPC 107_3", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_3, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_4 = new ConcreteMaterialModelCode2010("UHPC 107_4", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_4, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_5 = new ConcreteMaterialModelCode2010("UHPC 107_4", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_5, ConcreteMaterial.ConcreteTypes.FRC);

            var section1 = new ReinforcedConcreteSection(new SectionRectangular(width, height), mat1_1);
            SectionCheckerAttribute sectionCheckerAttribute1 = new SectionCheckerAttribute(section1, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions1 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section1), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker1 = new SectionCheckerModelCode2010(sectionCheckerAttribute1, sectionOptions1, new StandardModelCode2010(), true);

            var section2 = new ReinforcedConcreteSection(new SectionRectangular(width, height), mat1_2);
            SectionCheckerAttribute sectionCheckerAttribute2 = new SectionCheckerAttribute(section2, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions2 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section2), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker2 = new SectionCheckerModelCode2010(sectionCheckerAttribute2, sectionOptions2, new StandardModelCode2010(), true);

            var section3 = new ReinforcedConcreteSection(new SectionRectangular(width, height), mat1_3);
            SectionCheckerAttribute sectionCheckerAttribute3 = new SectionCheckerAttribute(section3, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions3 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section3), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker3 = new SectionCheckerModelCode2010(sectionCheckerAttribute3, sectionOptions3, new StandardModelCode2010(), true);

            var section4 = new ReinforcedConcreteSection(new SectionRectangular(width, height), mat1_4);
            SectionCheckerAttribute sectionCheckerAttribute4 = new SectionCheckerAttribute(section4, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions4 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section4), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker4 = new SectionCheckerModelCode2010(sectionCheckerAttribute4, sectionOptions4, new StandardModelCode2010(), true);

            var section5 = new ReinforcedConcreteSection(new SectionRectangular(width, height), mat1_5);
            SectionCheckerAttribute sectionCheckerAttribute5 = new SectionCheckerAttribute(section5, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions5 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section4), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker5 = new SectionCheckerModelCode2010(sectionCheckerAttribute5, sectionOptions5, new StandardModelCode2010(), true);

            var plasticFailureDomain1 = sectionChecker1.GetPlasticFailureDomainResult();
            var plasticFailureDomain2 = sectionChecker2.GetPlasticFailureDomainResult();
            var plasticFailureDomain3 = sectionChecker3.GetPlasticFailureDomainResult();
            var plasticFailureDomain4 = sectionChecker4.GetPlasticFailureDomainResult();
            var plasticFailureDomain5 = sectionChecker5.GetPlasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain1.Domain);
            ShowDomainPoints(plasticFailureDomain2.Domain);
            ShowDomainPoints(plasticFailureDomain3.Domain);
            ShowDomainPoints(plasticFailureDomain4.Domain);
            ShowDomainPoints(plasticFailureDomain5.Domain);

            //ExportToGmsh(new FailureDomain[] { plasticFailureDomain1.Domain, plasticFailureDomain2.Domain, plasticFailureDomain3.Domain, plasticFailureDomain4.Domain, plasticFailureDomain5.Domain });
        }

        [TestMethod]
        public void RectangularSectionTest13()
        {
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 200),
                new Point2d(200, 200),
                new Point2d(200, 400),
                new Point2d(100, 400),
                new Point2d(100, 200),
                new Point2d(0, 200),
            }));

            RebarSectionCircular rebar = new RebarSectionCircular(12, SteelMaterialEN1992Data.B450C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
{
                new ReinforcedConcreteRebar(rebar, new Point2d(120, 350)),
                new ReinforcedConcreteRebar(rebar, new Point2d(180, 350)),
                new ReinforcedConcreteRebar(rebar, new Point2d(120, 50)),
                new ReinforcedConcreteRebar(rebar, new Point2d(180, 50)),
};

            StressStrainTable UHCP1SSTComp = new StressStrainTable(
                new double[] { 0, -107, -107 }, new double[] { 0, -0.002378, -0.003709 });
            StressStrainTable UHCP1SSTTens_1 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2, 0.0 }, new double[] { 0, 0.000089, 0.002392, 0.011, 0.028 });
            StressStrainTable UHCP1SSTTens_2 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2, 1.6 }, new double[] { 0, 0.000089, 0.002392, 0.011, 0.0195 });
            StressStrainTable UHCP1SSTTens_3 = new StressStrainTable(
                new double[] { 0, 4, 4, 3.2 }, new double[] { 0, 0.000089, 0.002392, 0.011 });
            StressStrainTable UHCP1SSTTens_4 = new StressStrainTable(
                new double[] { 0, 4, 4 }, new double[] { 0, 0.000089, 0.002392 });
            StressStrainTable UHCP1SSTTens_5 = new StressStrainTable(
                new double[] { 0, 4 }, new double[] { 0, 0.000089 });
            ConcreteMaterialModelCode2010 mat1_1 = new ConcreteMaterialModelCode2010("UHPC 107_1", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_1, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_2 = new ConcreteMaterialModelCode2010("UHPC 107_2", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_2, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_3 = new ConcreteMaterialModelCode2010("UHPC 107_3", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_3, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_4 = new ConcreteMaterialModelCode2010("UHPC 107_4", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_4, ConcreteMaterial.ConcreteTypes.FRC);
            ConcreteMaterialModelCode2010 mat1_5 = new ConcreteMaterialModelCode2010("UHPC 107_5", -0.002378, 0.000089, UHCP1SSTComp, UHCP1SSTTens_5, ConcreteMaterial.ConcreteTypes.Concrete);

            ReinforcedConcreteSection section1 = new ReinforcedConcreteSection(shape, mat1_1);
            section1.AddRebars(rebars);

            SectionCheckerAttribute sectionCheckerAttribute1 = new SectionCheckerAttribute(section1, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions1 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section1), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker1 = new SectionCheckerModelCode2010(sectionCheckerAttribute1, sectionOptions1, new StandardModelCode2010(), true);

            ReinforcedConcreteSection section2 = new ReinforcedConcreteSection(shape, mat1_2);
            section2.AddRebars(rebars);

            SectionCheckerAttribute sectionCheckerAttribute2 = new SectionCheckerAttribute(section2, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions2 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section2), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker2 = new SectionCheckerModelCode2010(sectionCheckerAttribute2, sectionOptions2, new StandardModelCode2010(), true);

            ReinforcedConcreteSection section3 = new ReinforcedConcreteSection(shape, mat1_3);
            section3.AddRebars(rebars);

            SectionCheckerAttribute sectionCheckerAttribute3 = new SectionCheckerAttribute(section3, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions3 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section3), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker3 = new SectionCheckerModelCode2010(sectionCheckerAttribute3, sectionOptions3, new StandardModelCode2010(), true);

            ReinforcedConcreteSection section4 = new ReinforcedConcreteSection(shape, mat1_4);
            section4.AddRebars(rebars);

            SectionCheckerAttribute sectionCheckerAttribute4 = new SectionCheckerAttribute(section4, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions4 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section4), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker4 = new SectionCheckerModelCode2010(sectionCheckerAttribute4, sectionOptions4, new StandardModelCode2010(), true);

            ReinforcedConcreteSection section5 = new ReinforcedConcreteSection(shape, mat1_5);
            section5.AddRebars(rebars);

            SectionCheckerAttribute sectionCheckerAttribute5 = new SectionCheckerAttribute(section5, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions5 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section5), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker5 = new SectionCheckerModelCode2010(sectionCheckerAttribute5, sectionOptions5, new StandardModelCode2010(), true);

            var plasticFailureDomain1 = sectionChecker1.GetPlasticFailureDomainResult();
            var plasticFailureDomain2 = sectionChecker2.GetPlasticFailureDomainResult();
            var plasticFailureDomain3 = sectionChecker3.GetPlasticFailureDomainResult();
            var plasticFailureDomain4 = sectionChecker4.GetPlasticFailureDomainResult();
            var plasticFailureDomain5 = sectionChecker5.GetPlasticFailureDomainResult();

            ShowDomainPoints(plasticFailureDomain1.Domain);
            ShowDomainPoints(plasticFailureDomain2.Domain);
            ShowDomainPoints(plasticFailureDomain3.Domain);
            ShowDomainPoints(plasticFailureDomain4.Domain);
            ShowDomainPoints(plasticFailureDomain5.Domain);
        }

        [TestMethod]
        public void RectangularSectionTest14()
        {
            double height = 400;
            double width = 400;

            var section = new ReinforcedConcreteSection(new SectionRectangular(width, height), ConcreteMaterialModelCode2010Data.C30_37_25);
            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, true, 64);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, new StandardModelCode2010(), true);

            var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();
            ShowDomainPoints(elasticFailureDomain.Domain);
            //ExportToGmsh(elasticFailureDomain.Domain.GetMesh());

            var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResultAsync().Result;
            ShowDomainPoints(plasticFailureDomain.Domain);
            //ExportToGmsh(plasticFailureDomain.Domain.GetMesh());
        }
    }
}
