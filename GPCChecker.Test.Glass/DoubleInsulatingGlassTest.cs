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
                                        new Prototype.LaminatedEqThicknessParameters());

            // Load
            LoadCase lcPressure = new LoadCase("Wind", EN16612LoadDurations.WIND, 30, GPC.Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            NormalAreaLoad loadWp1 = new NormalAreaLoad(1.0, s1, lcPressure, "wp1", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.External);
            NormalAreaLoad loadWp2 = new NormalAreaLoad(1.5, s1, lcPressure, "wp2", GPC.Checkers.Glasses.Wrappers.GlassPanelWrapper.GlassPanelPositions.Internal);

            // Surface
            GlassSurface gs1 = new GlassSurface(p1, s1);

            GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper dguw = new GPC.Checkers.Glasses.Wrappers.DoubleInsulatingGlassWrapper(gs1, dgu);

            var loadSharing = dguw.GetLoadSharing(new List<IGlassLoad>() { loadWp1, loadWp2 }, Prototype.Standards.EN16612) ;

            Assert.AreEqual(0.827, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.0001);
            Assert.AreEqual(0.173, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp1").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.0001);

            Assert.AreEqual(0.259, loadSharing[0].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.0001);
            Assert.AreEqual(1.241, loadSharing[1].Where(i => ((NormalAreaLoad)i).Name == "wp2").Cast<NormalAreaLoad>().FirstOrDefault().Pressure, 0.0001);


        }
    }
}
