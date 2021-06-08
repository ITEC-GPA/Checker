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
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

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
            double length = 9000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces( 0, 0, 0, 0, 10000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded))};
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class1); 
        }

        [TestMethod]
        public void ClassificationSectionHTest2()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 variazione => flangia in classe 2
            double b = 115;
            double t = 6.8;
            double tw = 5.8;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class2);
        }

        [TestMethod]
        public void ClassificationSectionHTest3()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 variazione => flangia in classe 3
            double b = 120;
            double t = 6.8;
            double tw = 5.8;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionHTest4()
        {
            // 254x102x22
            double h = 500; // Steel_CoP_2011_commentary E7.9 variazione => anima va in classe 4
            double b = 101.6;
            double t = 6.8;
            double tw = 4;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100000, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            try
            {
                cop2011Checker.PerformCheck();
            }
            catch(NotImplementedException)
            {
                
            }
        }

        [TestMethod]
        public void ClassificationSectionHTest5()
        {
            // 254x102x22
            double h = 254;         // Steel_CoP_2011_commentary E8.7.2 example 8.2 
            double b = 254;         // NOTA: l'esempio dice che la sezione è in classe 2 in compressione ma la norma non permette tale classe in compressione.
            double t = 14.2;        // le due classi possibili sono 3 e 4 (la 3 è formalmente uguale alla 2).
            double tw = 8.6;        // le flange sono non slender e l'anima in classe 1
            double length = 3000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 0.85, 1, 0.85, 1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;
            double bucklingCapacity1 = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1/1000, bucklingCapacity2/1000);     //KN
            double expBucklingCapacity = 2211;     //KN
            double expBuckWR = 0.4523;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR) * 100}");
        }

        [TestMethod]
        public void ClassificationSectionHTest6()
        {
            // 254x102x22
            double h = 362.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 370.5;         // NOTA: l'esempio dice che la sezione è in classe 2 in compressione ma la norma non permette tale classe in compressione.
            double t = 14.2;        // le due classi possibili sono 3 e 4 (la 3 è formalmente uguale alla 2).
            double tw = 20.7;        // le flange sono non slender e l'anima in classe 1
            double length = 7000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-480000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 2.9, 1, 2.9, 1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;
            double bucklingCapacity1 = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity = 715.4;     //KN
            double expBuckWR = 0.671;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR) * 100}");
        }

        [TestMethod]
        public void ClassificationSectionRHSTest1()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 150;
            double t = 5;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest2()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 150;
            double t = 6;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest3()
        {
            // 254x102x22
            double h = 250; // Steel_CoP_2011_commentary E7.9
            double b = 250;
            double t = 12.5;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) } ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class1);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest4()
        {
            // 254x102x22
            double h = 200; // Steel_CoP_2011_commentary E7.9
            double b = 200;
            double t = 8;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) } ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionRHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class2);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest1()
        {
            // 254x102x22
            double D = 250; 
            double t = 4;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-5000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest2()
        {
            // 254x102x22
            double D = 250;
            double t = 4;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) } ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest3()
        {
            // 254x102x22
            double D = 250;
            double t = 4;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest4()
        {
            // 254x102x22
            double D = 273;
            double t = 10;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) } ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class1);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest5()
        {
            // 254x102x22
            double D = 406.4;
            double t = 14;
            double length = 6000;

            LoadCase loadCase = new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight);
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) } ;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[] { new Cop2011BeamCheckerOptions(steelSectionCHS, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass = cop2011Checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;

            Assert.AreEqual(sectionClass, Cop2011BeamChecker.SectionClass.Class1);
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
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[3] { new ResultBeamForces(0, 0, 467900, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 0, 0, 931800000, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 0, 0, 465900000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[3] { new ResultStation(1, 3000, length),
                                                                    new ResultStation(1, 4500, length), 
                                                                    new ResultStation(1, 6000, length)};
            SteelSectionH[] steelSectionH = new SteelSectionH[3] {  new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)};
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, lengthBuckling / length, 1, 1, 1, 1, 1);
            Cop2011BeamCheckerOptions[] cop2011BeamCheckerOptions = new Cop2011BeamCheckerOptions[]{
                                                                    new Cop2011BeamCheckerOptions(steelSectionH, resultBeamForces, resultStations, options) };
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker checker = new Cop2011Checker(cop2011BeamCheckerOptions, loadCase, standardCopSuos2011);
            checker.PerformCheck();
            Cop2011BeamChecker.SectionClass sectionClass1 = checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Class;
            Cop2011BeamChecker.SectionClass sectionClass2 = checker.BeamCheckerResults[0].BeamStationCheckerResults[1].Class;
            Cop2011BeamChecker.SectionClass sectionClass3 = checker.BeamCheckerResults[0].BeamStationCheckerResults[2].Class;

            double expJx = 1180000000;              // cm^3
            double expWplx = 3994000;               // cm^3

            double shearCapacity = checker.BeamCheckerResults[0].BeamStationCheckerResults[0].Shear2Capacity;
            double SC = shearCapacity / 1000;       //kN
            double expShearCapacity = 1213;         //kN    

            double momentCapacity1 = checker.BeamCheckerResults[0].BeamStationCheckerResults[0].BendingMoment1Capacity;      //N*mm
            double momentCapacity2 = checker.BeamCheckerResults[0].BeamStationCheckerResults[1].BendingMoment1Capacity;      //N*mm
            double MC1 = momentCapacity1 / 1000000;     //kN*m
            double MC2 = momentCapacity2 / 1000000;     //kN*m
            double expMomentCapacity = 1058;            //kN*m

            double latTorsBucklingCapacity1 = checker.BeamCheckerResults[0].BeamStationCheckerResults[1].LateralTosionalBucklingCapacity;    //N*mm
            double latTorsBucklingCapacity2 = checker.BeamCheckerResults[0].BeamStationCheckerResults[2].LateralTosionalBucklingCapacity;    //N*mm
            double LTBC1 = latTorsBucklingCapacity1 / 1000000;      //kN*m
            double LTBC2 = latTorsBucklingCapacity2 / 1000000;      //kN*m
            double expLatTorsBuckilingCapacity = 851;               //kN*m

            double station1WR = checker.BeamCheckerResults[0].BeamStationCheckerResults[0].WorkingRatio;
            double station2WR = checker.BeamCheckerResults[0].BeamStationCheckerResults[1].WorkingRatio;
            double station3WR = checker.BeamCheckerResults[0].BeamStationCheckerResults[2].WorkingRatio;
            double bendingMomentStation2WR = checker.BeamCheckerResults[0].BeamStationCheckerResults[1].BendingMoment1WorkingRatio;

            double expStation1WR = 0.394;
            double expStation2WR = 1.095;
            double expStation3WR = 0.547;
            double expBendingMomentStation2WR = 0.881;

            Assert.AreEqual(sectionClass1, Cop2011BeamChecker.SectionClass.Class1);
            Assert.AreEqual(sectionClass2, Cop2011BeamChecker.SectionClass.Class1);
            Assert.AreEqual(sectionClass3, Cop2011BeamChecker.SectionClass.Class1);
            Assert.IsTrue(Math.Abs(expJx / steelSectionH[0].J11) - 1 < 0.02, $"Jx % Error: {Math.Abs(expJx / steelSectionH[0].J11 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWplx / steelSectionH[0].Wpl1) - 1 < 0.02, $"S % Error: {Math.Abs(expWplx / steelSectionH[0].Wpl1 - 1) * 100}");
            Assert.IsTrue(Math.Abs((SC) / expShearCapacity - 1) <0.01, $"Shear Capacity % Error: {Math.Abs((SC) / expShearCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((MC1) / expMomentCapacity - 1) < 0.02, $"Moment Capacity % Error: {Math.Abs((MC1) / expMomentCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((MC2) / expMomentCapacity - 1) < 0.02, $"Moment Capacity % Error: {Math.Abs((MC2) / expMomentCapacity - 1) * 100}");
            Assert.IsTrue(expMomentCapacity - MC1 > 0);
            Assert.IsTrue(Math.Abs(MC2 - MC1) < 0.001);
            Assert.IsTrue(Math.Abs((LTBC1) / expLatTorsBuckilingCapacity - 1) < 0.05, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((LTBC1) / expLatTorsBuckilingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((LTBC2) / expLatTorsBuckilingCapacity - 1) < 0.05, $"LateralTorsionalBucklingMoment Capacity % Error: {Math.Abs((LTBC2) / expLatTorsBuckilingCapacity - 1) * 100}");
            Assert.IsTrue(expLatTorsBuckilingCapacity - LTBC1 > 0);

            Assert.IsTrue(Math.Abs(station1WR - expStation1WR) < 0.01, $"Station 1 Working Ration % Error: {Math.Abs(station1WR - expStation1WR) * 100}");
            Assert.IsTrue(Math.Abs(station2WR - expStation2WR) < 0.055, $"Station 2 Working Ration % Error: {Math.Abs(station2WR - expStation2WR) * 100}");
            Assert.IsTrue(Math.Abs(station3WR - expStation3WR) < 0.027, $"Station 3 Working Ration % Error: {Math.Abs(station3WR - expStation3WR) * 100}");
            Assert.IsTrue(Math.Abs(bendingMomentStation2WR - expBendingMomentStation2WR) < 0.015, $"Station 2 Bending Moment Working Ration % Error: {Math.Abs(bendingMomentStation2WR - expBendingMomentStation2WR) * 100}");
        }
    }
}
