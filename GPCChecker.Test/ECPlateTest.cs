using System;
using System.Collections.Generic;
using System.Windows;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPCChecker.Steel.EuroCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteelTests
{
    [TestClass]
    public class ECPlateTest
    {
        [TestMethod]
        public void SingleInnerPlateClass4Test1()
        {
            double fy = 275;
            double h = 510;
            Point2d p0 = new Point2d(0, 0);
            Point2d p1 = new Point2d(0, h);
            double t = 4;
            ECPlate inner = new ECPlate(t, p0, p1, fy, Plate.TypePlate.inner, 0, 0);

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
            ECPlate inner = new ECPlate(t, p0, p1, fy, Plate.TypePlate.outer, 0, 0);

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

            SectionH sec = new SectionH(h, tw, b, tf, b, tf, false, new SteelMaterial("steel", 200000, 0.3, 355, 510, 7850));

            double A = sec.Area;
            double Iy = sec.J22;

            Class4Section class4 = new Class4Section(sec);
            
            double N = 0;
            double My = 6.1*1e9;
            double Mz = 0*1e6;

            class4.Calc(N, My, Mz);
            
            double Aeff = class4.Aeff;
            double Jeff = class4.J2eff;
            Point2d diffCentroid = sec.Centroid - class4.CentroidEff;
        }
    }
}
