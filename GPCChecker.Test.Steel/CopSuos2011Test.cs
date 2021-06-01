using GPC.Checkers.Steel.Cop2011;
using GPC.Geometry;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteelTests
{
    [TestClass]
    public class CopSuos2011Test
    {
        [TestMethod]
        public void ClassificationSectionHTest1()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 
            double b = 101.6;
            double t = 6.8;
            double tw = 5.8;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces ( 0, 0, 0, 0, 10000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionH, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass =  copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class1); 
        }

        [TestMethod]
        public void ClassificationSectionHTest2()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 variazione => flangia in classe 2
            double b = 115;
            double t = 6.8;
            double tw = 5.8;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-100, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionH, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class2);
        }

        [TestMethod]
        public void ClassificationSectionHTest3()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 variazione => flangia in classe 3
            double b = 120;
            double t = 6.8;
            double tw = 5.8;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionH, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionHTest4()
        {
            // 254x102x22
            double h = 500; // Steel_CoP_2011_commentary E7.9 variazione => anima va in classe 4
            double b = 101.6;
            double t = 6.8;
            double tw = 4;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-100000, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionH, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest1()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 150;
            double t = 5;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionRHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionRHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest2()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 150;
            double t = 6;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionRHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionRHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest3()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 250;
            double t = 12.5;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionRHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionRHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class1);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest4()
        {
            // 254x102x22
            double h = 200; // Steel_CoP_2011_commentary E7.9
            double b = 200;
            double t = 8;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionRHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionRHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class2);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest1()
        {
            // 254x102x22
            double D = 250; 
            double t = 4;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-5000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionCHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionCHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest2()
        {
            // 254x102x22
            double D = 250;
            double t = 4;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-1000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionCHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionCHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest3()
        {
            // 254x102x22
            double D = 250;
            double t = 4;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionCHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionCHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest4()
        {
            // 254x102x22
            double D = 273;
            double t = 10;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionCHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionCHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class1);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest5()
        {
            // 254x102x22
            double D = 406.4;
            double t = 14;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionCHS, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionCHS, resultBeamForces[0]);

            Assert.AreEqual(sectionClass, CopSuos2011BeamCheckerResults.SectionClass.Class1);
        }

        [TestMethod]
        public void LateralTorsionalBucklingSectionHTest1()
        {
            // 254x102x22
            double h = 677.9; // Steel_CoP_2011_commentary E7.9 variazione => anima va in classe 4
            double b = 253;
            double t = 16.2;
            double tw = 11.7;
            double length = 9000;
            double lengthBuckling = 3000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[3] {
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 467900, 0, 0, 0, CoordinateSystem.Global),
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 931800, 0, CoordinateSystem.Global), 
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 465900, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled);
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(length, steelClasses);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            CopSuos2011BeamCheckerResults copSuos2011BeamCheckerResults = new CopSuos2011BeamCheckerResults(loadCase, resultBeamForces, resultStations, steelSectionH, options, standardCopSuos2011);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass1 = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[0]);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass2 = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[1]);
            CopSuos2011BeamCheckerResults.SectionClass sectionClass3 = copSuos2011BeamCheckerResults.CalculateSectionClass(steelSectionH, resultBeamForces[2]);

            double shearCapacity = copSuos2011BeamCheckerResults.CalculateShearYCapacity(steelSectionH);
            double expShearCapacity = 1213;         //kN    

            double momentCapacity1 = copSuos2011BeamCheckerResults.CalculateBendingMoment1Capacity(resultBeamForces[1], steelSectionH);     //N*mm
            double momentCapacity2 = copSuos2011BeamCheckerResults.CalculateBendingMoment1Capacity(resultBeamForces[2], steelSectionH);     //N*mm
            double expMomentCapacity = 1058;        //kN*m

            double latTorsBucklingCapacity1 = copSuos2011BeamCheckerResults.CalculateLateralTorsionalBucklingMomentCapacity(resultBeamForces[1], steelSectionH);
            double latTorsBucklingCapacity2 = copSuos2011BeamCheckerResults.CalculateLateralTorsionalBucklingMomentCapacity(resultBeamForces[2], steelSectionH);
            double expLatTorsBuckilingCapacity = 851;

            Assert.AreEqual(sectionClass1, CopSuos2011BeamCheckerResults.SectionClass.Class1);
            Assert.AreEqual(sectionClass2, CopSuos2011BeamCheckerResults.SectionClass.Class1);
            Assert.AreEqual(sectionClass3, CopSuos2011BeamCheckerResults.SectionClass.Class1);
            Assert.IsTrue(Math.Abs((shearCapacity / 1000) / expShearCapacity - 1) < 0.1, $"Shear Capacity % Error: {Math.Abs((shearCapacity / 1000) / expShearCapacity - 1)}");
            Assert.IsTrue(Math.Abs((momentCapacity1 / 1000000) / expMomentCapacity - 1) < 0.1, $"Moment Capacity % Error: {Math.Abs((momentCapacity1 / 1000000) / expMomentCapacity - 1)}");
            Assert.IsTrue(Math.Abs((momentCapacity2 / 1000000) / expMomentCapacity - 1) < 0.1, $"Moment Capacity % Error: {Math.Abs((momentCapacity2 / 1000000) / expMomentCapacity - 1)}");
            Assert.IsTrue(Math.Abs((latTorsBucklingCapacity1 / 1000000) / expLatTorsBuckilingCapacity - 1) < 0.1, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((latTorsBucklingCapacity1 / 1000000) / expLatTorsBuckilingCapacity - 1)}");
            Assert.IsTrue(Math.Abs((latTorsBucklingCapacity2 / 1000000) / expLatTorsBuckilingCapacity - 1) < 0.1, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((latTorsBucklingCapacity2 / 1000000) / expLatTorsBuckilingCapacity - 1)}");

        }
    }
}
