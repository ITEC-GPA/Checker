using System;
using System.Collections.Generic;
using System.Windows;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
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
            SectionH sec = new SectionH(500, 10, 400, 10, 400, 10, new SteelMaterial("steel", 200000, 0.3, 355, 510, 7850));
            List<ECPlate> plateSec = new List<ECPlate>(5);
            for (int i = 0; i < sec.Plates.Length; i++)
            {
                plateSec.Add(new ECPlate(sec.Plates[i]));
            }

            Class4Section class4 = new Class4Section(plateSec);
            
            double N = -1000;
            double My = 0*1e6;
            double Mz = 0*1e6;

            double Aeffk = sec.Area;
            double Aeffkp1 = 0;
            int iter = 0;
            while (Math.Abs(Aeffkp1-Aeffk) > 0.01*Aeffk)
            {
                iter++;

                Aeffk = class4.Aeff;
                for (int i = 0; i < class4.Plates.Count; i++)
                {
                    //calculation sigma in initial point always active
                    Class4Point2d p = class4.Plates[i].InitialPoint;
                    double sigmaInitialPoint = N / class4.Aeff + My / class4.J2eff * (p.Y - class4.Centroid.Y) + Mz / class4.J1eff * (p.X - class4.Centroid.X);

                    //calculation of sigma in the active point
                    p = class4.Plates[i].LastPointActive;
                    double sigmaLastPointActive = N / class4.Aeff + My / class4.J2eff * (p.Y - class4.Centroid.Y) + Mz / class4.J1eff * (p.X - class4.Centroid.X);
                    class4.Plates[i].SetSigma(sigmaInitialPoint, sigmaLastPointActive);
                }
                Aeffkp1 = class4.Aeff; 
            }
            MessageBox.Show(iter.ToString());
            double x = 0;
        }
    }
}
