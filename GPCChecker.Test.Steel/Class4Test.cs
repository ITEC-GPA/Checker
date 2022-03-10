using System;
using System.Collections.Generic;
using System.Windows;
using GPC.Checkers.Steel.EuroCode;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteelTests
{
    [TestClass]
    public class EN1993Class4Test
    {
        [TestMethod]
        public void SingleInnerPlateClass4Test1()
        {
            double fy = 275;
            double h = 510;
            Point2d p0 = new Point2d(0, 0);
            Point2d p1 = new Point2d(0, h);
            double t = 4;
            ECClass4ThinWallSection.ECThinWall inner = new ECClass4ThinWallSection.ECThinWall(t, p0, p1, fy, ECClass4ThinWallSection.ECThinWall.PlateType.Inner, 0, 0);

            double A = inner.Aeff;
            double J = inner.J2EffCentroid;

            double sigmaTop = -1; //-1
            double sigmaInf = 1; //1

            inner.SetSigma(sigmaInf, sigmaTop);
            double[] beff = inner.Beff;

            double Aeff = inner.Aeff;
            Point2d centroidEff = inner.CentroidEff;
            double Jeff = inner.J2EffCentroid;
        }

        [TestMethod]
        public void SingleOuterPlateClass4Test1()
        {
            double fy = 275;
            double L = 500;
            Point2d p0 = new Point2d(0, 0);
            Point2d p1 = new Point2d(L, 0);
            double t = 4;
            ECClass4ThinWallSection.ECThinWall inner = new ECClass4ThinWallSection.ECThinWall(t, p0, p1, fy, ECClass4ThinWallSection.ECThinWall.PlateType.Outer, 0, 0);

            double A = inner.Aeff;
            double J = inner.J2EffCentroid;

            double sigmaL = 1; //-1
            double sigma0 = -1; //1

            inner.SetSigma(sigma0, sigmaL);
            double[] beff = inner.Beff;

            double Aeff = inner.Aeff;
            Point2d centroidEff = inner.CentroidEff;
            double Jeff = inner.J2EffCentroid;
        }

        [TestMethod]
        public void SectionHTest1()
        {
            double tw = 10;
            double tf = 25;
            double h = 1400 + 2 * tf;
            double b = 400;

            SteelSectionH sec = new SteelSectionH(h, tw, b, tf, b, tf, SteelMaterial.S355, string.Empty, 
                Section.SectionTypes.Welded);

            double A = sec.Area;
            double Iy = sec.J22;

            ResultBeamForces resultBeamForces = new ResultBeamForces(0.0, 0.0, 0.0, 0.0, 6.1*1e9, 0.0, CoordinateSystem.Global);
            ECClass4ThinWallSection class4 = new ECClass4ThinWallSection(sec, resultBeamForces);

            double Aeff = class4.AreaEff;
            double Jeff = class4.J22eff;
            Point2d diffCentroid = sec.Centroid - class4.CentroidEff;
        }

        [TestMethod]
        public void SectionHTest2()
        {
            double tw = 12;         // Cordova pagina 52
            double tf = 19;         // IPE600
            double h = 600;
            double b = 220;
            double r = 24;

            SteelSectionH sec = new SteelSectionH(h, tw, b, tf, b, tf, SteelMaterial.S275, string.Empty, 
                Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);

            ResultBeamForces resultBeamForces = new ResultBeamForces(-3800 * 1e3, 0.0, 0.0, 0.0, 600 * 1e6, 0.0, CoordinateSystem.Global);
            ECClass4ThinWallSection class4 = new ECClass4ThinWallSection(sec, resultBeamForces);

            double expAreaEff = 14950;
            double J11eff = 920728000;
            double weff1 = 306640;

            Assert.IsTrue(Math.Abs(class4.AreaEff - expAreaEff)/expAreaEff < 0.01);
            Assert.IsTrue(Math.Abs(class4.J11eff - J11eff) / J11eff < 0.01);
            Assert.IsTrue(Math.Abs(class4.Weff1 - weff1) / weff1 < 0.01);
        }

        [TestMethod]
        public void SectionHTest3()
        {
            double tw = 15;         // Cordova pagina 52
            double tf = 28;         // HEA 800
            double h = 790;
            double b = 300;
            double r = 30;

            SteelSectionH sec = new SteelSectionH(h, tw, b, tf, b, tf, SteelMaterial.S275, string.Empty,
                Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);

            ResultBeamForces resultBeamForces = new ResultBeamForces(-6100 * 1e3, 0.0, 0.0, 0.0, 600 * 1e6, 0.0, CoordinateSystem.Global);
            ECClass4ThinWallSection class4 = new ECClass4ThinWallSection(sec, resultBeamForces);

            double expAreaEff = 27700;

            Assert.IsTrue(Math.Abs(class4.AreaEff - expAreaEff) / expAreaEff < 0.01);
        }

        [TestMethod]
        public void SectionRHSTest1()
        {
            double t = 5;         // Cordova pagina 60         // RHS 600x600x5
            double h = 600;
            double b = 600;
            double r = 30;

            SteelSectionRHS sec = new SteelSectionRHS(h, b, t, t, t, t, SteelMaterial.S275, string.Empty, r, Section.FormedTypes.HotFinished, Section.SectionTypes.Rolled);

            ResultBeamForces resultBeamForces = new ResultBeamForces(-610 * 1e3, 0.0, 0.0, 0.0, 0.0, 0.0, CoordinateSystem.Global);
            ECClass4ThinWallSection class4 = new ECClass4ThinWallSection(sec, resultBeamForces);

            double expAreaEff = 4910;

            Assert.IsTrue(Math.Abs(class4.AreaEff - expAreaEff) / expAreaEff < 0.01);
        }
    }
}
