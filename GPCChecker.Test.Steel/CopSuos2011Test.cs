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
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.BeamChecker;
using GPC.Checkers.Steel.Results;

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
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011);
            Cop2011BeamCheckerResults.SectionClass sectionClass =  copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class1); 
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
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011);
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class2);
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
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH (h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class3);
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
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionHTest5()
        {
            // 254x102x22
            double h = 254;         // Steel_CoP_2011_commentary E8.7.2 example 8.2 variazione => anima va in classe 4
            double b = 254;
            double t = 14.2;
            double tw = 8.6;
            double length = 3000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(-100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(length, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class2);
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
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionRHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class4);
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
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionRHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class3);
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
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionRHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class1);
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
            GPC.Model.Sections.Steel.SteelSectionRHS steelSectionRHS = new GPC.Model.Sections.Steel.SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionRHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class2);
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
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionCHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class4);
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
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS =  new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionCHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest3()
        {
            // 254x102x22
            double D = 250;
            double t = 4;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[1] { new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[1] { new GPC.Model.Results.ResultStation(1, 3000, 6000) };
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionCHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class3);
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
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); ;
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionCHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class1);
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
            GPC.Model.Sections.Steel.SteelSectionCHS steelSectionCHS = new GPC.Model.Sections.Steel.SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(6000, Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011); 
            Cop2011BeamCheckerResults.SectionClass sectionClass = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionCHS);

            Assert.AreEqual(sectionClass, Cop2011BeamCheckerResults.SectionClass.Class1);
        }

        [TestMethod]
        public void LateralTorsionalBucklingSectionHTest1()
        {
            // 254x102x22
            double h = 677.9; // Steel_CoP_2011_commentary E8.3.5 example 8.1 
            double b = 253;
            double t = 16.2;
            double tw = 11.7;
            double length = 9000;
            double lengthBuckling = 3000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            GPC.Model.Results.ResultBeamForces[] resultBeamForces = new GPC.Model.Results.ResultBeamForces[3] {
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 467900, 0, 0, 0, CoordinateSystem.Global),
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 931800000, 0, CoordinateSystem.Global), 
                                                    new GPC.Model.Results.ResultBeamForces(0, 0, 0, 0, 465900000, 0, CoordinateSystem.Global) };
            GPC.Model.Results.ResultStation[] resultStations = new GPC.Model.Results.ResultStation[3] { new GPC.Model.Results.ResultStation(1, 3000, 6000),
                                                                                                                new GPC.Model.Results.ResultStation(1, 3000, 6000), 
                                                                                                                new GPC.Model.Results.ResultStation(1, 3000, 6000)};
            GPC.Model.Sections.Steel.SteelSectionH steelSectionH = new GPC.Model.Sections.Steel.SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled) ;
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(length, steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, lengthBuckling / length, 1, 1, 1, 1, 1);
            Cop2011BeamCheckerOptions cop2011BeamChecker = new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options);
            GPC.Model.Standards.StandardCopSuos2011 standardCopSuos2011 = new GPC.Model.Standards.StandardCopSuos2011();

            Cop2011BeamCheckerResults copSuos2011BeamCheckerResults = new Cop2011BeamCheckerResults(cop2011BeamChecker, loadCase, standardCopSuos2011);
            Cop2011BeamCheckerResults.SectionClass sectionClass1 = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[0], steelSectionH);
            Cop2011BeamCheckerResults.SectionClass sectionClass2 = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[1], steelSectionH);
            Cop2011BeamCheckerResults.SectionClass sectionClass3 = copSuos2011BeamCheckerResults.CalculateSectionClass(resultBeamForces[2], steelSectionH);

            double expJx = 1180000000;              // cm^3
            double expWplx = 3994000;               // cm^3

            double shearCapacity = copSuos2011BeamCheckerResults.CalculateShearYCapacity(steelSectionH);
            double SC = shearCapacity / 1000;       //kN
            double expShearCapacity = 1213;         //kN    

            double momentCapacity1 = copSuos2011BeamCheckerResults.CalculateBendingMoment1Capacity(resultBeamForces[1], steelSectionH);     //N*mm
            double momentCapacity2 = copSuos2011BeamCheckerResults.CalculateBendingMoment1Capacity(resultBeamForces[2], steelSectionH);     //N*mm
            double MC1 = momentCapacity1 / 1000000;     //kN*m
            double MC2 = momentCapacity2 / 1000000;     //kN*m
            double expMomentCapacity = 1058;            //kN*m

            double latTorsBucklingCapacity1 = copSuos2011BeamCheckerResults.CalculateLateralTorsionalBucklingMomentCapacity(resultBeamForces[1], steelSectionH);
            double latTorsBucklingCapacity2 = copSuos2011BeamCheckerResults.CalculateLateralTorsionalBucklingMomentCapacity(resultBeamForces[2], steelSectionH);
            double LTBC1 = latTorsBucklingCapacity1 / 1000000;      //kN*m
            double LTBC2 = latTorsBucklingCapacity2 / 1000000;      //kN*m
            double expLatTorsBuckilingCapacity = 851;               //kN*m

            Assert.AreEqual(sectionClass1, Cop2011BeamCheckerResults.SectionClass.Class1);
            Assert.AreEqual(sectionClass2, Cop2011BeamCheckerResults.SectionClass.Class1);
            Assert.AreEqual(sectionClass3, Cop2011BeamCheckerResults.SectionClass.Class1);
            Assert.IsTrue(Math.Abs(expJx / steelSectionH.J11) - 1 < 0.02, $"Jx % Error: {Math.Abs(expJx / steelSectionH.J11 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWplx / steelSectionH.Wpl1) - 1 < 0.02, $"S % Error: {Math.Abs(expWplx / steelSectionH.Wpl1 - 1) * 100}");
            Assert.IsTrue(Math.Abs((SC) / expShearCapacity - 1) <0.01, $"Shear Capacity % Error: {Math.Abs((SC) / expShearCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((MC1) / expMomentCapacity - 1) < 0.02, $"Moment Capacity % Error: {Math.Abs((MC1) / expMomentCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((MC2) / expMomentCapacity - 1) < 0.02, $"Moment Capacity % Error: {Math.Abs((MC2) / expMomentCapacity - 1) * 100}");
            Assert.IsTrue(expMomentCapacity - MC1 > 0);
            Assert.IsTrue(Math.Abs(MC2 - MC1) < 0.001);
            Assert.IsTrue(Math.Abs((LTBC1) / expLatTorsBuckilingCapacity - 1) < 0.05, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((LTBC1) / expLatTorsBuckilingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((LTBC2) / expLatTorsBuckilingCapacity - 1) < 0.05, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((LTBC2) / expLatTorsBuckilingCapacity - 1) * 100}");
            Assert.IsTrue(expLatTorsBuckilingCapacity - LTBC1 > 0);

            Cop2011Checker checker = new Cop2011Checker(new Cop2011BeamCheckerOptions[] { cop2011BeamChecker }, loadCase);
            checker.PerformCheck();
            double beamWR = checker.BeamCheckerResults[0].WorkingRatio;
        }
    }
}
