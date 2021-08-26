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
    public class DoubleInsulatingGlassTest : GlassTestBase
    {
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

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { loadWp1, loadWp2 }, p1.Standard, false);
            

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

            LineLoad lineLoad1 = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)), 
                                    lcLineLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            LineLoad lineLoad2 = new LineLoad(0, 0, 1, 0, 0, 0, new Line3d(new Point3d(0, 500, 0), new Point3d(1000, 500, 0)),
                                    lcLineLoad, GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);


            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);
            Model model = new Model(base.GetOutputFolder());
            model.AddSurface(gs1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetRedistributionPressures(new List<IGlassLoad>() { lineLoad1, lineLoad2 }, p1.Standard, false);

            // confronto con tabelle paper Laura: Pratical design dgus
            double phiL = 0.0072001; // alfa = 0.5, lambda = 0.33
            double phiA = 0.0053523;

            double deltaP1 = (1 / mg1.Thickness * phiL) / ((1 / mg1.Thickness + 1 / mg2.Thickness) * phiA) * lineLoad1.F3 * lineLoad1.Line.GetLength() / s1.GetArea();
            double deltaP2 = (1 / mg1.Thickness * phiL) / ((1 / mg1.Thickness + 1 / mg2.Thickness) * phiA) * lineLoad2.F3 * lineLoad2.Line.GetLength() / s1.GetArea();

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(deltaP1, loadSharing[0].FirstOrDefault().Pressure, 0.01);
            Assert.AreEqual(deltaP1, loadSharing[1].FirstOrDefault().Pressure, 0.01);

            Assert.AreEqual(deltaP2, loadSharing[0].LastOrDefault().Pressure, 0.01);
            Assert.AreEqual(deltaP2, loadSharing[1].LastOrDefault().Pressure, 0.01);


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


            double phiP = 0.00112493; // alfa = 0.5, lambda = 0.33
            double phiA = 0.0053523;

            double deltaP1 = (1 / mg1.Thickness * phiP) / ((1 / mg1.Thickness + 1 / mg2.Thickness) * phiA) * load1.F3 / s1.GetArea();
            double deltaP2 = (1 / mg1.Thickness * phiP) / ((1 / mg1.Thickness + 1 / mg2.Thickness) * phiA) * load2.F3 / s1.GetArea();

            // CARICO SU LASTRA ESTERNA
            Assert.AreEqual(deltaP1, loadSharing[0].FirstOrDefault().Pressure, 0.01);
            Assert.AreEqual(deltaP1, loadSharing[1].FirstOrDefault().Pressure, 0.01);

            Assert.AreEqual(deltaP2, loadSharing[0].LastOrDefault().Pressure, 0.01);
            Assert.AreEqual(deltaP2, loadSharing[1].LastOrDefault().Pressure, 0.01);


        }
    }
}
