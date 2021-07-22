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
using GPC.Checkers.Steel.Results;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace SteelTests
{
    [TestClass]
    public class CopSuos2011Test
    {
        #region Generic Test

        [TestMethod]
        public void GenericTest1()
        {
            // 254x102x22
            double h = 304.8; // Steel_CoP_2011_commentary E8.3.5 example 8.1 
            double b = 127;
            double t = 9.652;
            double tw = 6.35;
            double length = 9000;

            LoadCase[] loadCase = new LoadCase[] {new LoadCase ("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[] { new ResultStation(1, 0.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[] { new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled) };
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults) ;

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            int iterations = 10;
            Cop2011Checker[] checkers = new Cop2011Checker[iterations];
            for (int i = 0; i < checkers.Count(); i++)
            {
                checkers[i] = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
                checkers[i].PerformCheck();
            }

            for(int j = 0; j < checkers.Count(); j++)
            {
                Assert.IsTrue(checkers[j].Cop2011BeamStationResults.Count() == 1);
                Assert.IsTrue(checkers[j].Cop2011BeamStationResults.Count() == 1);                
                Assert.IsTrue(checkers[j].Cop2011BeamStationResults[0].Station == resultStations[0]);
                Assert.IsTrue(checkers[j].Cop2011BeamStationResults[0].ResultBeamForce == resultBeamForces[0]);
                Assert.IsTrue(checkers[j].Cop2011BeamStationResults[0].WorkingRatio >= 0.01);
            }    
        }

        [TestMethod]
        public void GenericTest2()
        {
            // 254x102x22
            double h = 304.8; // Steel_CoP_2011_commentary E8.3.5 example 8.1 
            double b = 127;
            double t = 9.652;
            double tw = 6.35;
            double length = 9000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test1", LoadCase.LoadCaseTypes.SelfWeight), 
                                                    new LoadCase("Test2", LoadCase.LoadCaseTypes.SelfWeight), 
                                                    new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global),
                                                    new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global), 
                                                    new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global),
                                                    new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global),
                                                    new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global)};
            ResultStation[] resultStations = new ResultStation[] { new ResultStation(1, 0.0, length), 
                                                                    new ResultStation(1, 0.0, length), 
                                                                    new ResultStation(1, 0.0, length),
                                                                    new ResultStation(1, 0.0, length), 
                                                                    new ResultStation(1, 0.0, length)};
            BeamResult[] beamResults = new BeamResult[] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global),
                                                        new BeamResult(loadCase[1], resultBeamForces, resultStations, CoordinateSystem.Global),
                                                        new BeamResult(loadCase[2], resultBeamForces, resultStations, CoordinateSystem.Global)};
            SteelSectionH[] steelSectionH = new SteelSectionH[] { new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)};
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            checker.PerformCheck();

            Assert.IsTrue(checker.Cop2011BeamStationResults.Count() == 15);            
        }

        #endregion

        #region Section Classification

        [TestMethod]
        public void ClassificationSectionHTest1()
        {
            // 254x102x22
            double h = 254; // Steel_CoP_2011_commentary E7.9 
            double b = 101.6;
            double t = 6.8;
            double tw = 5.8;
            double length = 9000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 10000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH steelSectionH = new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled);
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);

            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH, beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class1); 
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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);

            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH, beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class2);
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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);

            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH, beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100000, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH, beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length/2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 0.85, 1, 0.85, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH, beamResults);

            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1/1000, bucklingCapacity2/1000);     //KN
            double expBucklingCapacity = 2211;     //KN
            double expBuckWR = 0.4523;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionRHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest2()
        {
            // 254x102x22
            double h = 250; 
            double b = 150;
            double t = 6;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions =new BeamCheckerAttributes(steelSectionRHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassA = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            Cop2011Checker.SectionClass sectionClassB = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClassA, Cop2011Checker.SectionClass.Class4);
            Assert.AreEqual(sectionClassB, Cop2011Checker.SectionClass.Class2);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest3()
        {
            // 254x102x22
            double h = 250; 
            double b = 250;
            double t = 12.5;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionRHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassB = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            Cop2011Checker.SectionClass sectionClassA = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;

            Assert.AreEqual(sectionClassB, Cop2011Checker.SectionClass.Class1);
            Assert.AreEqual(sectionClassA, Cop2011Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionRHSTest4()
        {
            // 254x102x22
            double h = 200; 
            double b = 200;
            double t = 8;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 1100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty, 0, Section.FormedTypes.HotFinished, Section.SectionTypes.Rolled) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionRHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class1);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest1()
        {
            // 254x102x22
            double D = 250;
            double t = 4;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-5000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            Cop2011Checker.SectionClass sectionClassBend = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class4);
            Assert.AreEqual(sectionClassBend, Cop2011Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest2()
        {
            // 254x102x22
            double D = 250;
            double t = 4;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class4);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest3()
        {
            // 254x102x22
            double D = 250;
            double t = 4;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest4()
        {
            // 254x102x22
            double D = 273;
            double t = 10;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassBend = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;

            Assert.AreEqual(sectionClassBend, Cop2011Checker.SectionClass.Class1);
            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void ClassificationSectionCHSTest5()
        {
            // 254x102x22
            double D = 406.4;
            double t = 14;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class1);
        }

        #endregion

        #region Buckling

        [TestMethod]
        public void AxialBucklingSectionHTest1()
        {
            // 254x102x22
            double h = 362.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 370.5;       
            double t = 20.7;        
            double tw = 12.3;       
            double length = 7000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-480000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 2.9, 1, 0.96, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 715.4;     //KN
            double expBucklingCapacity2 = 4684;
            double expBuckWR = 0.671;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity -1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2/1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) -1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR -1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionHTest2()
        {
            // 254x102x22
            double h = 549.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 214.0;
            double t = 23.6;
            double tw = 14.7;
            double length = 5000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-480000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 2249.9;     //KN
            double expBucklingCapacity2 = 4581.7;
            double expBuckWR = 0.213;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionHTest3()
        {
            // 254x102x22
            double h = 549.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 214.0;
            double t = 23.6;
            double tw = 14.7;
            double length = 5000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-480000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 2249.9;     //KN
            double expBucklingCapacity2 = 4532.7;
            double expBuckWR = 0.213;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionHTest4()
        {
            // 254x102x22
            double h = 913.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 411.0;
            double t = 57.9;
            double tw = 32.0;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 255, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-10000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Welded)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 11429.7;     //KN
            double expBucklingCapacity2 = 17331.6;
            double expBuckWR = 0.875;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionCHSTest1()
        {
            // 254x102x22
            double D = 549.0;          
            double t = 23.6;
            double length = 5000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-10000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 10113.7;     //KN
            double expBucklingCapacity2 = 10113.7;
            double expBuckWR = 0.9887;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionCHSTest2()
        {
            // 254x102x22
            double D = 139.0;
            double t = 4;
            double length = 3000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 493.8;     //KN
            double expBucklingCapacity2 = 493.8;
            double expBuckWR = 0.202;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionCHSTest3()
        {
            // NOTA: i risultati sono stati presi da SAP usando le BS5950. per questo i valori sono leggermente diversi => 5%
            double D = 400;
            double t = 12;
            double length = 7315;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-10000000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double expBucklingCapacity1 = 4348.646;     //KN
            double expBucklingCapacity2 = 4348.646;
            double expBuckWR = 2.18;

            Assert.AreEqual(sectionClass, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) < 0.05, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) < 0.05, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity2 / (bucklingCapacity2 / 1000) - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.05, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
        }

        [TestMethod]
        public void AxialBucklingSectionRHSTest1()
        {
            // 254x102x22
            double h = 200; 
            double b = 200;
            double t = 8;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 0, 0, 100 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionRHS[] steelSectionRHS = new SteelSectionRHS[] { new SteelSectionRHS(h, b, t, t, t, t, steelMaterial, string.Empty, 0, Section.FormedTypes.HotFinished, Section.SectionTypes.Rolled) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionRHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            Cop2011Checker.SectionClass sectionClassBend = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double shearCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].Shear1Capacity;
            double shearCapacity = shearCapacity1 / 1000;     //KN
            double bendingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;
            double bendingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity;        //N
            double bendingCapacity = Math.Min(bendingCapacity1 / 1000000, bendingCapacity2 / 1000000);     //KN
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1WorkingRatio;        //N
            double bendingWR = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1WorkingRatio;        //N
            double WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N

            double expBucklingCapacity = 1525;         //KN
            double expBendingCapacity = 157.13;          //KN
            double expShearCapacity = 603.4;
            double expBendingWR = 0.636;
            double expBuckWR = 0.001;
            double expWR = 0.745;

            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
            Assert.AreEqual(sectionClassBend, Cop2011Checker.SectionClass.Class1);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity / bendingCapacity - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(bendingCapacity / bucklingCapacity2 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingWR / bendingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBendingWR / bendingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expShearCapacity / shearCapacity - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expShearCapacity / shearCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWR / WR - 1) < 0.01, $"Working Ration % Error: {Math.Abs(expWR / WR - 1) * 100}");
        }

        #endregion

        #region Torsion

        [TestMethod]
        public void TorsionSectionHTest1()
        {
            double h = 362.0;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 370.5;
            double t = 20.7;
            double tw = 12.3;
            double length = 7000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100 * 1000, 0, 300 * 1000, 100 * 1000000, 100 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 2.9, 1, 0.96, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            double shearCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].Shear2Capacity;
            double shearCapacity = shearCapacity2 / 1000;     //KN
            double expShearCapacity = 599;

            Assert.IsTrue(Math.Abs(expShearCapacity / shearCapacity - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expShearCapacity / shearCapacity - 1) * 100}");
        }

        [TestMethod]
        public void TorsionSectionHTest2()
        {
            double h = 500;         // Steel_CoP_2011_commentary E8.9.2 example 8.3 
            double b = 400;
            double t = 25;
            double tw = 15;
            double length = 7000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(0, 0, 500 * 1000, 100 * 1000000, 100 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 2.9, 1, 0.96, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            double shearCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].Shear2Capacity;
            double shearCapacity = shearCapacity2 / 1000;     //KN
            double expShearCapacity = 1078;

            Assert.IsTrue(Math.Abs(expShearCapacity / shearCapacity - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expShearCapacity / shearCapacity - 1) * 100}");
        }

        [TestMethod]
        public void TorsionSectionCHSTest1()
        {
            double D = 600;
            double t = 15;
            double length = 5000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000 * 1000, 1000 * 1000, 0, 100000000, 1000000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            double shearCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].Shear2Capacity;
            double shearCapacity = shearCapacity2 / 1000;     //KN
            double expShearCapacity = 1790;

            Assert.IsTrue(Math.Abs(expShearCapacity / shearCapacity - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expShearCapacity / shearCapacity - 1) * 100}");
        }
        #endregion

        #region Interaction

        [TestMethod]
        public void InteractionSectionCHSTest1()
        {
            // 254x102x22
            double D = 400;
            double t = 12;
            double length = 6000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000 * 1000, 500, 0, 0, 500 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double shearCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].Shear1Capacity;
            double shearCapacity = shearCapacity1 / 1000;     //KN
            double bendingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;
            double bendingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity;        //N
            double bendingCapacity = Math.Min(bendingCapacity1 / 1000000, bendingCapacity2 / 1000000);     //KN
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1WorkingRatio;        //N
            double bendingWR = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1WorkingRatio;        //N
            double WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N

            double expBucklingCapacity = 4796.2;         //KN
            double expBendingCapacity = 586.8;          //KN
            double expShearCapacity = 1800;
            double expBendingWR = 0.852;
            double expBuckWR = 0.208;
            double expWR = 1.23;

            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity / bendingCapacity - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(bendingCapacity / bucklingCapacity2 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingWR / bendingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBendingWR / bendingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expShearCapacity / shearCapacity - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expShearCapacity / shearCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWR / WR - 1) < 0.01, $"Working Ration % Error: {Math.Abs(expWR / WR - 1) * 100}");
        }

        [TestMethod]
        public void InteractionSectionCHSTest2()
        {
            // 254x102x22
            double D = 139.0;
            double t = 4;
            double length = 3000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-100 *1000, 0, 0, 0, 10 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingCapacity = Math.Min(bucklingCapacity1 / 1000, bucklingCapacity2 / 1000);     //KN
            double bendingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;
            double bendingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity;        //N
            double bendingCapacity = Math.Min(bendingCapacity1 / 1000000, bendingCapacity2 / 1000000);     //KN
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1WorkingRatio;        //N
            double bendingWR = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1WorkingRatio;        //N
            double WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N

            double expBucklingCapacity = 493.8;         //KN
            double expBendingCapacity = 23.709;          //KN
            double expBendingWR = 0.4217;
            double expBuckWR = 0.202;
            double expWR = 0.708;

            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity / bendingCapacity - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(bendingCapacity / bucklingCapacity2 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingWR / bendingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBendingWR / bendingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWR / WR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expWR / WR - 1) * 100}");
        }

        [TestMethod]
        public void InteractionSectionCHSTest3()
        {
            double D = 600;
            double t = 15;
            double length = 5000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000 * 1000, 0, 0, 0, 1000 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[1] { (new SteelSectionCHS(D, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            Cop2011Checker.SectionClass sectionClassBend = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity = bucklingCapacity1/1000;     //KN
            double bendingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;
            double bendingCapacity = bendingCapacity1 / 1000000;     //KN
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1WorkingRatio;        //N
            double bendingWR = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1WorkingRatio;        //N
            double WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N

            double expBucklingCapacity = 9593;         //KN
            double expBendingCapacity = 1396.4;          //KN
            double expBendingWR = 0.716;
            double expBuckWR = 0.1042;
            double expWR = 0.820;

            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
            Assert.AreEqual(sectionClassBend, Cop2011Checker.SectionClass.Class3);
            Assert.IsTrue(Math.Abs(expBucklingCapacity / bucklingCapacity - 1) < 0.01, $"Buckling Capacity % Error: {Math.Abs(expBucklingCapacity / bucklingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity / bendingCapacity - 1) < 0.02, $"Buckling Capacity % Error: {Math.Abs(expBendingCapacity / bendingCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingWR / bendingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBendingWR / bendingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWR / WR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expWR / WR - 1) * 100}");
        }

        [TestMethod]
        public void InteractionSectionHTest1()
        {
            double h = 362.0;         // Steel_CoP_2011_commentary E6.12.2 
            double b = 370.5;
            double t = 20.7;
            double tw = 12.3;
            double length = 10000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1000 * 1000, 0, 0, 0, 522.2 * 1000000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClassComp = cop2011Checker.Cop2011BeamStationResults[0].AxialCompressionClass;
            Cop2011Checker.SectionClass sectionClassBend = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            double bucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1Capacity;
            double bucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling2Capacity;        //N
            double bucklingCapacity11 = bucklingCapacity1 / 1000;                                                                       //KN
            double bucklingCapacity22 = bucklingCapacity2 / 1000;                                                                       //KN
            double bendingCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;
            double bendingCapacity2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity;         //N
            double bendingCapacity11 = bendingCapacity1 / 1000000;                                                                      //KN
            double bendingCapacity22 =bendingCapacity2 / 1000000;                                                                       //KN
            double bucklingWR = cop2011Checker.Cop2011BeamStationResults[0].AxialBuckling1WorkingRatio;        //N
            double bendingWR = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1WorkingRatio;        //N
            double WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;        //N

            double expBucklingCapacity1 = 2280;         //KN
            double expBucklingCapacity2 = 4180.8;         //KN
            double expBendingCapacity1 = 810.0;          //KN
            double expBendingCapacity2 = 312.6;          //KN
            double expBendingWR = 0.644;
            double expBuckWR = 0.438;
            double expWR = 1.154;        // LateralTorsionalBuck

            Assert.AreEqual(sectionClassComp, Cop2011Checker.SectionClass.Class3);
            Assert.AreEqual(sectionClassBend, Cop2011Checker.SectionClass.Class1);
            Assert.IsTrue(Math.Abs(expBucklingCapacity2 / bucklingCapacity22 - 1) < 0.01, $"Buckling2 Capacity % Error: {Math.Abs(expBucklingCapacity2 / bucklingCapacity22 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBucklingCapacity1 / bucklingCapacity11 - 1) < 0.01, $"Buckling1 Capacity % Error: {Math.Abs(expBucklingCapacity1 / bucklingCapacity11 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity1 / bendingCapacity11 - 1) < 0.02, $"Bending1 Capacity % Error: {Math.Abs(expBendingCapacity1 / bendingCapacity11 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingCapacity2 / bendingCapacity22 - 1) < 0.02, $"Bending2 Capacity % Error: {Math.Abs(expBendingCapacity2 / bendingCapacity22 - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBendingWR / bendingWR - 1) < 0.01, $"Bending1 Working Ration % Error: {Math.Abs(expBendingWR / bendingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expBuckWR / bucklingWR - 1) < 0.01, $"Buckling Working Ration % Error: {Math.Abs(expBuckWR / bucklingWR - 1) * 100}");
            Assert.IsTrue(Math.Abs(expWR / WR - 1) < 0.01, $"Working Ration % Error: {Math.Abs(expWR / WR - 1) * 100}");
        }

        [TestMethod]
        public void InteractionSectionHTest2()
        {
            double h = 362.0;         // Steel_CoP_2011_commentary E6.12.2 
            double b = 370.5;
            double t = 20.7;
            double tw = 12.3;
            double length = 10000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 275, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(1200 * 1000, 0, 0, 0, 464 * 1000000, -25, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
        }

        [TestMethod]
        public void InteractionSectionHTest3()
        {
            double h = 355.6;         
            double b = 368.6;
            double t = 17.5;
            double tw = 10.4;
            double length = 8100;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(1073 * 1000, 7050, -159750, 0, 460.4 * 1000000, -26.3 * 1000000, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 0.5, 1, 1, 1, 1, 0.5, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
        }

        [TestMethod]
        public void InteractionSectionHTest4()
        {
            double h = 355.6;
            double b = 368.6;
            double t = 17.5;
            double tw = 10.4;
            double length = 8100;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S355", 206000, 0.3, 355, 510, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[1] { new ResultBeamForces(-1073 * 1000, 7050, -159750, 0, -460.4 * 1000000, 26.3 * 1000000, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[1] { new ResultStation(1, length / 2.0, length) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[1] { (new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)) };
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(Cop2011Checker.Cop2011Options.SteelClasses.Class1,
                                                        Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.Default, 0.5, 1, 1, 1, 1, 0.5, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
        }

        #endregion

        #region LateralTorsionalBuckling

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

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[3] { new ResultBeamForces(0, 0, 467900, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 0, 0, 931800000, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 0, 0, 465900000, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[3] { new ResultStation(1, length/2.0, length),
                                                                    new ResultStation(1, length/2.0, length), 
                                                                    new ResultStation(1, length/2.0, length)};
            SteelSectionH[] steelSectionH = new SteelSectionH[3] {  new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)};
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, lengthBuckling / length, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();
            Cop2011Checker.SectionClass sectionClass1 = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            Cop2011Checker.SectionClass sectionClass2 = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;
            Cop2011Checker.SectionClass sectionClass3 = cop2011Checker.Cop2011BeamStationResults[0].BendingCompressionClass;

            double expJx = 1180000000;              // cm^3
            double expWplx = 3994000;               // cm^3

            double shearCapacity = cop2011Checker.Cop2011BeamStationResults[0].Shear2Capacity;
            double SC = shearCapacity / 1000;       //kN
            double expShearCapacity = 1213;         //kN    

            double momentCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity;      //N*mm
            double momentCapacity2 = cop2011Checker.Cop2011BeamStationResults[1].BendingMoment1Capacity;      //N*mm
            double MC1 = momentCapacity1 / 1000000;     //kN*m
            double MC2 = momentCapacity2 / 1000000;     //kN*m
            double expMomentCapacity = 1058;            //kN*m

            double latTorsBucklingCapacity1 = cop2011Checker.Cop2011BeamStationResults[1].LateralTosionalBucklingCapacity;    //N*mm
            double latTorsBucklingCapacity2 = cop2011Checker.Cop2011BeamStationResults[2].LateralTosionalBucklingCapacity;    //N*mm
            double LTBC1 = latTorsBucklingCapacity1 / 1000000;      //kN*m
            double LTBC2 = latTorsBucklingCapacity2 / 1000000;      //kN*m
            double expLatTorsBuckilingCapacity = 851;               //kN*m

            double station1WR = cop2011Checker.Cop2011BeamStationResults[0].WorkingRatio;
            double station2WR = cop2011Checker.Cop2011BeamStationResults[1].WorkingRatio;
            double station3WR = cop2011Checker.Cop2011BeamStationResults[2].WorkingRatio;
            double bendingMomentStation2WR = cop2011Checker.Cop2011BeamStationResults[1].BendingMoment1WorkingRatio;

            double expStation1WR = 0.394;
            double expStation2WR = 1.095;
            double expStation3WR = 0.547;
            double expBendingMomentStation2WR = 0.881;

            Assert.AreEqual(sectionClass1, Cop2011Checker.SectionClass.Class1);
            Assert.AreEqual(sectionClass2, Cop2011Checker.SectionClass.Class1);
            Assert.AreEqual(sectionClass3, Cop2011Checker.SectionClass.Class1);
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

        #endregion

        #region ShearBuckling

        [TestMethod]
        public void ShearBucklingSectionHTest1()
        {
            // 254x102x22
            double h = 677.9; // Steel_CoP_2011_commentary E8.3.5 example 8.1 
            double b = 253;
            double t = 16.2;
            double tw = 11.7;
            double length = 9000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global),
                                                                            new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global)};
            ResultStation[] resultStations = new ResultStation[] { new ResultStation(1, 0.0, length),
                                                                    new ResultStation(1, 0.0, length),          
                                                                    new ResultStation(1, 0.0, length),
                                                                    new ResultStation(1, 0.0, length),
                                                                    new ResultStation(1, 0.0, length),
                                                                    new ResultStation(1, 0.0, length)};
            SteelSectionH[] steelSectionH = new SteelSectionH[] {  new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw-1, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw-2, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw-3, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw-4, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled),
                                                                    new SteelSectionH(h, tw-5, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)};
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes BeamCheckerAttributess = new BeamCheckerAttributes(steelSectionH, beamResults);
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(BeamCheckerAttributess, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();

            double shearCapacity1 = cop2011Checker.Cop2011BeamStationResults[0].Shear2Capacity / 1000;
            double shearCapacity2 = cop2011Checker.Cop2011BeamStationResults[1].Shear2Capacity / 1000;
            double shearCapacity3 = cop2011Checker.Cop2011BeamStationResults[2].Shear2Capacity / 1000;
            double shearCapacity4 = cop2011Checker.Cop2011BeamStationResults[3].Shear2Capacity / 1000;
            double shearCapacity5 = cop2011Checker.Cop2011BeamStationResults[4].Shear2Capacity / 1000;
            double shearCapacity6 = cop2011Checker.Cop2011BeamStationResults[5].Shear2Capacity / 1000;
            double expShearCapacity = 1213;         //kN    
            double expShearCapacity2 = 1109;         //kN    
            double expShearCapacityBuck1 = 1000;         //kN    
            double expShearCapacityBuck2 = 900;         //kN    
            double expShearCapacityBuck3 = 700;         //kN    
            double expShearCapacityBuck4 = 535;         //kN    

            Assert.IsTrue(Math.Abs((shearCapacity1) / expShearCapacity - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity1) / expShearCapacity - 1) * 100}");
            Assert.IsTrue(Math.Abs((shearCapacity2) / expShearCapacity2 - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity2) / expShearCapacity2 - 1) * 100}");
            Assert.IsTrue(Math.Abs((shearCapacity3) / expShearCapacityBuck1 - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity3) / expShearCapacityBuck1 - 1) * 100}");
            Assert.IsTrue(Math.Abs((shearCapacity4) / expShearCapacityBuck2 - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity4) / expShearCapacityBuck2 - 1) * 100}");
            Assert.IsTrue(Math.Abs((shearCapacity5) / expShearCapacityBuck3 - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity5) / expShearCapacityBuck3 - 1) * 100}");
            Assert.IsTrue(Math.Abs((shearCapacity6) / expShearCapacityBuck4 - 1) < 0.01, $"Shear Capacity % Error: {Math.Abs((shearCapacity6) / expShearCapacityBuck4 - 1) * 100}");
        }

        #endregion

        #region Bending Moment

        [TestMethod]
        public void BendingMomentSectionHTest1()
        {
            // 254x102x22
            double h = 304.8; // Steel_CoP_2011_commentary E8.3.5 example 8.1 
            double b = 127;
            double t = 9.652;
            double tw = 6.35;
            double length = 9000;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 500*1000, 0, 0, 0, CoordinateSystem.Global)};
            ResultStation[] resultStations = new ResultStation[] { new ResultStation(1, 0.0, length)};
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionH[] steelSectionH = new SteelSectionH[] {  new SteelSectionH(h, tw, b, t, b, t, steelMaterial, string.Empty, Section.SectionTypes.Rolled)};
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionH[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();

            double bendCap1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity / 1000;
            double bendCap2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity / 1000;  

            Assert.IsTrue(bendCap1 > 0.0);
            Assert.IsTrue(bendCap2 > 0.0);
        }

        [TestMethod]
        public void BendingMomentSectionCHSTest1()
        {
            double t = 12;
            double d = 400;

            LoadCase[] loadCase = new LoadCase[] { new LoadCase("Test", LoadCase.LoadCaseTypes.SelfWeight) };
            SteelMaterial steelMaterial = new SteelMaterial("S275", 206000, 0.3, 265, 430, 7850);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 500 * 1000, 0, 0, 0, CoordinateSystem.Global) };
            ResultStation[] resultStations = new ResultStation[] { new ResultStation(1, 0.0, 5000) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(loadCase[0], resultBeamForces, resultStations, CoordinateSystem.Global) };
            SteelSectionCHS[] steelSectionCHS = new SteelSectionCHS[] { new SteelSectionCHS(d, t, steelMaterial, string.Empty, Section.FormedTypes.HotFinished) };
            Cop2011Checker.Cop2011Options.SteelClasses steelClasses = Cop2011Checker.Cop2011Options.SteelClasses.Class1;
            Cop2011Checker.Cop2011Options options = new Cop2011Checker.Cop2011Options(steelClasses, Cop2011Checker.Cop2011Options.LateralTorsionalBucklingConditions.DestabilizingLoad,
                                                                                    1, 1, 1, 1, 1, 1, 1, 1, 1);
            BeamCheckerAttributes cop2011BeamCheckerOptions = new BeamCheckerAttributes(steelSectionCHS[0], beamResults) ;
            StandardCopSuos2011 standardCopSuos2011 = new StandardCopSuos2011();

            Cop2011Checker cop2011Checker = new Cop2011Checker(cop2011BeamCheckerOptions, options, standardCopSuos2011);
            cop2011Checker.PerformCheck();

            double bendCap1 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment1Capacity / 1000;
            double bendCap2 = cop2011Checker.Cop2011BeamStationResults[0].BendingMoment2Capacity / 1000;

            Assert.IsTrue(bendCap1 > 0.0);
            Assert.IsTrue(bendCap2 > 0.0);
        }

        #endregion
    }
}
