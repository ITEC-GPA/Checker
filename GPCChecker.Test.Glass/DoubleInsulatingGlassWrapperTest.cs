using GlassTests;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Loads;
using GPC.Checkers.Glasses.Models;
using GPC.Geometry;
using GPC.Model.Glasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;

namespace GlassTests
{
    [TestClass]
    public class DoubleInsulatingGlassWrapperTest : GlassTestBase
    {

        private double GetLoadSharingPressureTheoretical(double phiA, double h1, double h2, double E, double ni, double pressure)
        {
            double d1 = E * Math.Pow(h1, 3) / (12.0 * (1 - ni * ni));
            double d2 = E * Math.Pow(h2, 3) / (12.0 * (1 - ni * ni));

            return 1.0 / d1 * phiA / ((1.0 / d1 + 1.0 / d2) * phiA) * pressure;
        }

        private double GetLoadSharingLineLoadTheoretical(double phiA, double phiL, double h1, double h2, double E, double ni, double load, double lineLenght, double area)
        {
            double d1 = E * Math.Pow(h1, 3) / (12.0 * (1 - ni * ni));
            double d2 = E * Math.Pow(h2, 3) / (12.0 * (1 - ni * ni));

            return 1.0 / d1 * phiL / ((1.0 / d1 + 1.0 / d2) * phiA) * load * lineLenght / area;
        }

        private double GetLoadSharingPointLoadTheoretical(double phiA, double phiP, double h1, double h2, double E, double ni, double load, double area)
        {
            double d1 = E * Math.Pow(h1, 3) / (12.0 * (1 - ni * ni));
            double d2 = E * Math.Pow(h2, 3) / (12.0 * (1 - ni * ni));

            return 1.0 / d1 * phiP / ((1.0 / d1 + 1.0 / d2) * phiA) * load / area;
        }

        private double GetPhiATheoretical(double maxSize, double minSize)
        {
            double lamda = minSize / maxSize;

            double a = 0;
            double lambda2 = Math.Pow(lamda, 2);

            for (int m = 0; m < 17; m++)
            {
                if (m % 2 != 0)
                {
                    double m2 = Math.Pow(m, 2);
                    for (int n = 0; n < 17; n++)
                    {
                        if (n % 2 != 0)
                        {
                            a += 1 /
                                (m2 * Math.Pow(n, 2) * Math.Pow(m2 + Math.Pow(n, 2) * lambda2, 2));
                        }
                    }
                }
            }

            return a * 64 * lambda2 / Math.Pow(Math.PI, 8);
        }

        private double GetPhiLTheoretical(double maxSize, double minSize, double loadHeight)
        {
            double lamda = minSize / maxSize;
            double alfa = loadHeight / maxSize;

            double a = 0;
            double lambda2 = Math.Pow(lamda, 2);
            for (int m = 0; m < 17; m++)
            {
                if (m % 2 != 0)
                {
                    double m2 = Math.Pow(m, 2);
                    for (int n = 0; n < 17; n++)
                    {
                        if (n % 2 != 0)
                        {
                            a += Math.Sin(alfa * Math.PI * n) / 
                                (m2 * n * Math.Pow(m2 + Math.Pow(n, 2) * lambda2, 2) );
                        }
                    }
                }                
            }

            return a * 32 * lambda2 / Math.Pow(Math.PI, 7);
        }


        private double GetPhiPTheoretical(double maxSize, double minSize, double loadHeight)
        {
            double lamda = minSize / maxSize;
            double alfa = loadHeight / maxSize;

            double a = 0;
            double lambda2 = Math.Pow(lamda, 2);
            for (int m = 0; m < 17; m++)
            {
                if (m % 2 != 0)
                {
                    double m2 = Math.Pow(m, 2);
                    for (int n = 0; n < 17; n++)
                    {
                        if (n % 2 != 0)
                        {
                            a += Math.Sin(alfa * Math.PI * n) * Math.Pow(-1, m - 1) /
                                (m * n * Math.Pow(m2 + Math.Pow(n, 2) * lambda2, 2));
                        }
                    }
                }
            }

            return a * 16 * lambda2 / Math.Pow(Math.PI, 6);
        }


        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("EN16612")]
        [TestCategory("Monolithic")]
        [TestCategory("Pressure")]
        public void LoadSharing1()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp1 = new NormalAreaLoad(1.0, s1, lcPressure, "wp1", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            NormalAreaLoad loadWp2 = new NormalAreaLoad(1.5, s1, lcPressure, "wp2", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { loadWp1, loadWp2 }, p1.Standard, false) ;

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(0.827 - 1, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.01);
            Assert.AreEqual(0.827, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure
                                     + loadWp1.Pressure, 0.01);
            Assert.AreEqual(0.173, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.01);


            // CARICO SU LASTRA INTERNA
            Assert.AreEqual(1.199, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.05);
            Assert.AreEqual(-1.199, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.05);
            Assert.AreEqual(0.301, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure
                                     + loadWp2.Pressure, 0.05);
        }


        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("ASTME1300")]
        [TestCategory("Monolithic")]
        [TestCategory("Pressure")]
        public void LoadSharing2()
        {
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp1 = new NormalAreaLoad(1.0, s1, lcPressure, "wp1", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            NormalAreaLoad loadWp2 = new NormalAreaLoad(1.5, s1, lcPressure, "wp2", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { loadWp1, loadWp2 }, p1.Standard, false);

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(0.827 - 1, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.01);
            Assert.AreEqual(0.827, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure
                                        + loadWp1.Pressure, 0.01);
            Assert.AreEqual(0.173, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.01);


            // CARICO SU LASTRA INTERNA
            Assert.AreEqual(1.199,  loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.05);
            Assert.AreEqual(-1.199, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.05);
            Assert.AreEqual(0.301,  loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure
                                         + loadWp2.Pressure, 0.05);

        }


        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("Pressure")]
        public void LoadSharing3()
        {
            RunApiServer();

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));
            s1.Fill[0].Move(new Vector3d(1, 0, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase lcPressure3 = new LoadCase("Wind2", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp1 = new NormalAreaLoad(1.0, s1, lcPressure, "wp1", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            NormalAreaLoad loadWp2 = new NormalAreaLoad(1.5, s1, lcPressure, "wp2", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);
            NormalAreaLoad loadWp3 = new NormalAreaLoad(1.5, s1, lcPressure3, "wp3", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            List<NormalAreaLoad>[] loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { loadWp1, loadWp2, loadWp3 }, p1.Standard, false);


            Assert.IsTrue(loadSharing.Count() == 2, loadSharing.Length.ToString());
            Assert.IsTrue(loadSharing[0].Count() == 2, loadSharing[0].Count().ToString());
            Assert.IsTrue(loadSharing[1].Count() == 2, loadSharing[1].Count().ToString());

            Console.WriteLine(loadSharing[0][0].Pressure);
            Console.WriteLine(loadSharing[1][0].Pressure);

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(+0.25, loadSharing[0][0].Pressure, 0.001);
            Assert.AreEqual(+0.75, loadSharing[0][1].Pressure, 0.001);

            // CARICO SU LASTRA INTERNA
            Assert.AreEqual(-0.25, loadSharing[1][0].Pressure, 0.001);
            Assert.AreEqual(-0.75, loadSharing[1][1].Pressure, 0.001);

        }



        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("LineLoad")]
        public void LoadSharing4()
        {
            RunApiServer();

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcLineLoad = new LoadCase("Live", EN16612LoadDurations.LIVE, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            LineLoad lineLoadExt = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)), 
                                    lcLineLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            LineLoad lineLoadInt = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)),
                                    lcLineLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            Model model = new Model(base.GetOutputFolder());
            model.AddSurface(gs1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { lineLoadExt, lineLoadInt }, p1.Standard, false);

            Assert.IsTrue(loadSharing.Count() == 2, loadSharing.Length.ToString());
            Assert.IsTrue(loadSharing[0].Count() == 1, loadSharing[0].Count().ToString());
            Assert.IsTrue(loadSharing[1].Count() == 1, loadSharing[1].Count().ToString());

            // confronto con tabelle paper Laura: Pratical design dgus
            double phiL = GetPhiLTheoretical(3000, 1000, 500);
            double phiA = GetPhiATheoretical(3000, 1000);

            var deltaPExpectedExt = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg1.Thickness, mg2.Thickness,
                                                                      mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                      lineLoadExt.F3, lineLoadExt.Line.GetLength(), gs1.GetArea());

            var deltaPExpectedInt = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg2.Thickness, mg1.Thickness,
                                                                      mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                      lineLoadInt.F3, lineLoadInt.Line.GetLength(), gs1.GetArea());

            Console.WriteLine($"PhiL: {phiL} ");
            Console.WriteLine($"PhiA: {phiA} ");

            Console.WriteLine($"Numerical DeltaP: {loadSharing[0].FirstOrDefault().Pressure} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpectedExt} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpectedInt} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpectedExt - deltaPExpectedInt} ");

            double expectedExt = +deltaPExpectedInt - deltaPExpectedExt;
            double expectedInt = -deltaPExpectedInt + deltaPExpectedExt;

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(expectedExt, loadSharing[0][0].Pressure, Math.Abs(expectedExt * 0.001));
            // CARICO SU LASTRA INTERNA                     
            Assert.AreEqual(expectedInt, loadSharing[1][0].Pressure, Math.Abs(expectedInt * 0.001));

        }


        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("PointLoad")]
        public void LoadSharing5()
        {
            RunApiServer();

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 10, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcLoad = new LoadCase("Live", EN16612LoadDurations.LIVE, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            PointLoad load1 = new PointLoad(0, 0, 1, 0, 0, 0, new Point3d(500, 500, 0),
                                    lcLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            PointLoad load2 = new PointLoad(0, 0, 1, 0, 0, 0, new Point3d(500, 500, 0),
                                    lcLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            Model model = new Model(base.GetOutputFolder());
            model.AddSurface(gs1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { load1, load2 }, p1.Standard, false);


            // confronto con tabelle paper Laura: Pratical design dgus
            double phiP = GetPhiPTheoretical(3000, 1000, 500);
            double phiA = GetPhiATheoretical(3000, 1000);


            var deltaPExpected1 = GetLoadSharingPointLoadTheoretical(phiA, phiP, mg1.Thickness, mg2.Thickness,
                                                                    mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                    load1.F3, gs1.GetArea());

            var deltaPExpected2 = GetLoadSharingPointLoadTheoretical(phiA, phiP, mg2.Thickness, mg1.Thickness,
                                                                    mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                    load2.F3, gs1.GetArea());


            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(-deltaPExpected1, loadSharing[0].FirstOrDefault().Pressure, 0.000001);
            Assert.AreEqual(+deltaPExpected1, loadSharing[1].FirstOrDefault().Pressure, 0.000001);

            Assert.AreEqual(deltaPExpected2, loadSharing[0].LastOrDefault().Pressure, 0.000001);
            Assert.AreEqual(-deltaPExpected2, loadSharing[1].LastOrDefault().Pressure, 0.000001);


        }




        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("LineLoad")]
        public void LoadSharing6()
        {
            RunApiServer();

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(1000, 3000, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialAstm());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 6, GetGlassMaterialAstm());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.EN16612, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                        Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement,
                                        new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase lcLineLoad = new LoadCase("Live", EN16612LoadDurations.LIVE, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.LiveLoad);

            LineLoad lineLoad1 = new LineLoad(0, 0, 10, 0, 0, 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)),
                                    lcLineLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            Model model = new Model(base.GetOutputFolder());
            model.AddSurface(gs1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { lineLoad1 }, p1.Standard, false);

            // confronto con tabelle paper Laura: Pratical design dgus
            double phiL = GetPhiLTheoretical(3000, 1000, 500);
            double phiA = GetPhiATheoretical(3000, 1000);

            var deltaPExpected = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg1.Thickness, mg2.Thickness, 
                                                                    mg1.GetElasticModulus(), mg1.GetPoissonRatios(), 
                                                                    lineLoad1.F3, lineLoad1.Line.GetLength(), gs1.GetArea());

            Console.WriteLine($"PhiL: {phiL} ");
            Console.WriteLine($"PhiA: {phiA} ");

            Console.WriteLine($"Numerical DeltaP: {loadSharing[0].FirstOrDefault().Pressure} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpected} ");


            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(-deltaPExpected, loadSharing[0].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));
            Assert.AreEqual(deltaPExpected, loadSharing[1].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));


        }


        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("LineLoad")]
        public void LoadSharing7()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double minorSide = 1000;
            double majorSide = 2500;
            double loadHeight = 500;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                                                      Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7, 
                                                                      Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                      new Prototype.LaminatedEqThicknessParameters(), null);

            // Load
            LoadCase loadCase1 = new LoadCase("Wind1", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            LoadCase loadCase2 = new LoadCase("Wind2", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            LineLoad load1 = new LineLoad(0, 0, 2.0, 0, 0, 0, new Line3d(new Point3d(0, loadHeight, 0), new Point3d(minorSide, loadHeight, 0)), loadCase1,
                                                            GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            LineLoad load2 = new LineLoad(0, 0, 1.5, 0, 0, 0, new Line3d(new Point3d(0, loadHeight, 0), new Point3d(minorSide, loadHeight, 0)), loadCase1,
                                                            GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            LineLoad load3 = new LineLoad(0, 0, 1.5, 0, 0, 0, new Line3d(new Point3d(0, loadHeight, 0), new Point3d(minorSide, loadHeight, 0)), loadCase2,
                                                            GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            model.AddSurface(gs1);


            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { load1, load2, load3 }, p1.Standard, false);


            // confronto con tabelle paper Laura: Pratical design dgus

            // CARICO 1
            // viene rimpiazzato dal due in quanto stesso lc
            double phiL = GetPhiLTheoretical(majorSide, minorSide, loadHeight);
            double phiA = GetPhiATheoretical(majorSide, minorSide);

            var deltaPExpected = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg1.Thickness, mg2.Thickness, mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                               load2.F3, load2.Line.GetLength(), gs1.GetArea());


            Console.WriteLine($"PhiL: {phiL} ");
            Console.WriteLine($"PhiA: {phiA} ");

            Console.WriteLine($"Numerical DeltaP: {loadSharing[0].FirstOrDefault().Pressure} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpected} ");


            Assert.AreEqual(-deltaPExpected, loadSharing[0].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));
            Assert.AreEqual(+deltaPExpected, loadSharing[1].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));

            // CARICO 2
            deltaPExpected = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg1.Thickness, mg2.Thickness, mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                           load3.F3, load3.Line.GetLength(), gs1.GetArea());

            Console.WriteLine($"Numerical DeltaP: {loadSharing[0].LastOrDefault().Pressure} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpected} ");

            Assert.AreEqual(-deltaPExpected, loadSharing[0].LastOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));
            Assert.AreEqual(+deltaPExpected, loadSharing[1].LastOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));

        }



        [TestMethod]
        [TestCategory("BAM")]
        [TestCategory("Numerical")]
        [TestCategory("Monolithic")]
        [TestCategory("LineLoad")]
        public void LoadSharing8()
        {
            RunApiServer();

            Model model = new Model(base.GetOutputFolder());

            double minorSide = 1000;
            double majorSide = 2500;
            double loadHeight = 2500 / 2.0 - 50;
            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(minorSide, majorSide, 0));

            MonolithicGlass mg1 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            MonolithicGlass mg2 = new MonolithicGlass("Mg1", 6, GetGlassMaterialEn16612());
            AirChamber airChamber = new AirChamber("Ac", 15);

            DoubleInsulatingGlass dgu = new DoubleInsulatingGlass("Dgu", mg1, mg2, airChamber);

            // Prototype
            Prototype p1 = new Prototype("p1", dgu, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                                                                      Prototype.CheckMethods.DominantLoad, Prototype.Solvers.Straus7,
                                                                      Prototype.LaminatedAnalysisTypes.MultiElement,
                                                                      new Prototype.LaminatedEqThicknessParameters(), null);
            p1.MeshOptions.MeshSize = 150;

            // Load
            LoadCase loadCase1 = new LoadCase("Wind1", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            LineLoad load1 = new LineLoad(0, 0, 1.5, 0, 0, 0, new Line3d(new Point3d(0, loadHeight, 0), new Point3d(minorSide, loadHeight, 0)), loadCase1,
                                                            GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            LineLoad load2 = new LineLoad(0, 0, 1.5, 0, 0, 0, new Line3d(new Point3d(0, majorSide - loadHeight, 0), new Point3d(minorSide, majorSide - loadHeight, 0)), loadCase1,
                                                            GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            model.AddSurface(gs1);


            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { load1, load2 }, p1.Standard, false);

            Assert.IsTrue(loadSharing.Count() == 2);


            // confronto con tabelle paper Laura: Pratical design dgus
            // CARICO 1
            // viene rimpiazzato dal due in quanto stesso lc
            double phiL = GetPhiLTheoretical(majorSide, minorSide, loadHeight);
            double phiA = GetPhiATheoretical(majorSide, minorSide);

            var deltaPExpected = GetLoadSharingLineLoadTheoretical(phiA, phiL, mg1.Thickness, mg2.Thickness, mg1.GetElasticModulus(), mg1.GetPoissonRatios(),
                                                                               load1.F3, load1.Line.GetLength(), gs1.GetArea());


            Console.WriteLine($"PhiL: {phiL} ");
            Console.WriteLine($"PhiA: {phiA} ");

            Console.WriteLine($"Numerical DeltaP: {loadSharing[0].FirstOrDefault().Pressure} ");
            Console.WriteLine($"Expected DeltaP: {deltaPExpected} ");


            Assert.AreEqual(-deltaPExpected, loadSharing[0].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));
            Assert.AreEqual(+deltaPExpected, loadSharing[1].FirstOrDefault().Pressure, Math.Abs(deltaPExpected * 0.001));

        }
    }
}
