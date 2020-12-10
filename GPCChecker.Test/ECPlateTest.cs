using System;
using GPC.Geometry;
using GPCChecker.Steel.EuroCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ECPlateTest
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
            ECPlate inner = new ECPlate(t, p0, p1, fy, ECPlate.TypePlate.inner, 0, 0);

            double A = inner.Aeff;
            double J = inner.J2EffCentroid;

            double sigmaTop = -1; //-1
            double sigmaInf = 1; //1

            inner.SetSigma(sigmaInf, sigmaTop);
            double[] beff = inner.Beff;

            double Aeff = inner.Aeff;
            Point2d centroidEff = inner.CentroidEff;
            double Jeff = inner.J2EffCentroid;

            double x = 1;
        }

        [TestMethod]
        public void SingleOuterPlateClass4Test1()
        {
            double fy = 275;
            double L = 500;
            Point2d p0 = new Point2d(0, 0);
            Point2d p1 = new Point2d(L, 0);
            double t = 4;
            ECPlate inner = new ECPlate(t, p0, p1, fy, ECPlate.TypePlate.outer, 0, 0);

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
    }
}
