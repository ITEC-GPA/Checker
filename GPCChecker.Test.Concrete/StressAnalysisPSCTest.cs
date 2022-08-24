using System;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
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

namespace ConcreteTests
{
    [TestClass]
    public class StressAnalysisPSCTest : ConcreteTestBase
    {
        [TestMethod]
        public void SquareSectionPrestressed1()
        {
            double rebarDiameter = 20;
            double rebarDiameterPrestress = 20;


            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(300, 0),
                new Point2d(300, 300),
                new Point2d(0, 300) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C25_30);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, SteelMaterialEN1993Data.Y1620CHardening);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(150, 150, 0), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            StandardEN1992p11 standard = new StandardEN1992p11();

            ResultBeamForces[] forces = new ResultBeamForces[] {
                new ResultBeamForces(-1000 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-500 * 1000, 0, 0, 0, 40 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-200 * 1000, 0, 0, 0, -40 * 1000000, -20 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0 * 1000, 0, 0, 0, -30 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)) };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

            for (int i = 0; i < slsResult.Length; i++)
                Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
        }

        [TestMethod]
        public void SquareSectionPrestressed2()
        {
            double rebarDiameter = 26;
            double rebarDiameterPrestress = 26;

            // SquareSectionPrestressed2
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(500, 0),
                new Point2d(500, 500),
                new Point2d(0, 500) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, SteelMaterialEN1993Data.Y1620CHardening);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(450, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 450,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 450,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 450,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 450,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(450, 450,0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(250, 250, 0), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            StandardEN1992p11 standard = new StandardEN1992p11();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 120 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-400 * 1000, 0, 0, 0, 150 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-800 * 1000, 0, 0, 0, 80 * 1000000, 150 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-900 * 1000, 0, 0, 0, 100 * 1000000, 100 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-2000 * 1000, 0, 0, 0, 20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

            for (int i = 0; i < slsResult.Length; i++)
                Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
        }

        [TestMethod]
        public void SquareSectionPrestressed3()
        {
            double rebarDiameter = 14;
            double rebarDiameterPrestress = 26;
            double phi = 1.55;

            // SquareSectionPrestressed2
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(400, 0),
                new Point2d(400, 400),
                new Point2d(0, 400) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C35_45);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, SteelMaterialEN1993Data.Y1620C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebar, new Point3d(40,40,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(40, 360, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(360, 40, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(360, 360, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 40,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 360,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 40,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 360,0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(200, 100, 0), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0 * 1000, 0, 0, 0, 200 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

            for (int i = 0; i < slsResult.Length; i++)
                Assert.IsTrue(LinearAnalysisCommonAssertModelCode(phi, slsResult[i]));
        }

        [TestMethod]
        public void SquareSectionPrestressed4()
        {
            double rebarDiameterPrestress = 26;
            double phi = 1.287;

            // SquareSectionPrestressed2
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(400, 0),
                new Point2d(400, 400),
                new Point2d(0, 400) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C25_30);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, SteelMaterialEN1993Data.Y1620C);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebarP, new Point2d(200, 200), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0 * 1000, 0, 0, 0, 0 * 1000000, 0 * 1000000, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] slsResult = sectionChecker.GetLinearStressAnalysisResult(phi);

            for (int i = 0; i < slsResult.Length; i++)
                Assert.IsTrue(LinearAnalysisCommonAssertModelCode(phi, slsResult[i]));
        }

        [TestMethod]
        public void RectangularSectionPrestressed1()
        {
            double rebarDiameter = 20;
            double rebarDiameterPrestress = 26;

            // SquareSectionPrestressed2
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(400, 0),
                new Point2d(400, 700),
                new Point2d(0, 700) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, SteelMaterialEN1993Data.Y1620CHardening);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 650,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(150, 650,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(250, 650,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 650,0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(200, 350, 0), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(-1000 * 1000, 0, 0, 0, 50 * 1000000, 80 * 1000000,GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-500 * 1000, 0, 0, 0, 20 * 1000000, 50 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-200 * 1000, 0, 0, 0, 80 * 1000000, 120 * 1000000,GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-400 * 1000, 0, 0, 0, 150 * 1000000, 20 * 1000000,GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-800 * 1000, 0, 0, 0, 80 * 1000000, 150 * 1000000,GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-900 * 1000, 0, 0, 0, 100 * 1000000, 100 * 1000000,GetLocalCoordinateSystem(section)),
                new ResultBeamForces(-2000 * 1000, 0, 0, 0, 20 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
                new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, GetLocalCoordinateSystem(section)),
            };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();

            for (int i = 0; i < slsResult.Length; i++)
                Assert.IsTrue(TensionAnalysisCommonAssertModelCode(slsResult[i], section, forces[i], standard));
        }

        [TestMethod]
        public void RectangularSectionPrestressed2()
        {
            double rebarDiameter = 18;
            double rebarDiameterPrestress = 26;
            double psi = 1.72;

            // SquareSectionPrestressed2
            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
                new Point2d(0, 0),
                new Point2d(400, 0),
                new Point2d(400, 700),
                new Point2d(0, 700) }));

            ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992Data.C45_55);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, SteelMaterialEN1993Data.B450C);
            RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new SteelMaterial("", 200000, 1500, 1500));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
                new ReinforcedConcreteRebar(rebar, new Point3d(50,40,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 40, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(50, 660,0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(350, 660,0)),
                new ReinforcedConcreteRebar(rebarP, new Point3d(200, 100, 0), 1400) };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);
            StandardEN1992p11 standard = new StandardEN1992p11();

            ResultBeamForces[] forces = new ResultBeamForces[]
            {
                new ResultBeamForces(0 * 1000, 0, 0, 0, 500 * 1000000, 0 * 1000000,GetLocalCoordinateSystem(section)),
            };

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, forces, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

            StressAnalysisResult[] results = sectionChecker.GetLinearStressAnalysisResult(psi, psi);
            StressAnalysisResult result = results[0];

            if (result.StrainPlane != null)
            {
                (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension(psi);
                (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension(psi, psi);

                Console.WriteLine($"Tensions associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} ");

                for (int i = 0; i < rebarTensions.Length; i++)
                    Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
                        $"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

                for (int i = 0; i < concreteTensions.Length; i++)
                    Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");
            }
            else
            {
                Console.WriteLine($"Result {result.Id} associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} don't find strain plane." +
                    $"Point is external");
            }
        }
    }
}
