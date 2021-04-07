using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Loads;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Checkers;
using GPC.Model.Elements;
using System.Collections.Generic;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Models;
using GPC.Geometry.Meshes;
using GPC.TestUtilities;

namespace GlassTests
{
    [TestClass]
    public class GlassWrapperTest : UnitTestBase
    {

        private static string _outputFolder;

        #region Private methods

        private GlassMaterialEn16612 GetGlassMaterialPrEn()
        {
            return new GlassMaterialEn16612("Glass", 70000, 0.23, 25, GlassMaterialEn16612.GlassTypes.DrawnSheetGlass, GlassMaterialEn16612.SurfaceTreatments.AsProduced,
                                        GlassMaterialEn16612.PrestressTypes.HeatStrengthened, GlassMaterialEn16612.ManufactoringProcesses.HorizontalToughening, 2700 * 10E-12, 0);
        }

        private InterlayerMaterial GetInterlayerMaterial()
        {
            var it = new InterlayerMaterial("", 1, 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 50 }, new double[] { 0.1, 0.2, 0.30 });
            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });
            return it;
        }

        private Shape GetRectangularShape(Point3d p, Vector3d vector)
        {
            Polygon3d poly = new Polygon3d()
            {
                new Point3d(p.X, p.Y, p.Z),
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z),
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z + vector.Z),
                new Point3d(p.X, p.Y, p.Z + vector.Z)
            };

            return new Shape(poly, null, null);
        }

        #endregion

        [TestMethod]

        public void LaminatedGetDistance4Glass()
        {
            // Arrange
            double _tolleranza = 0.0001;
            double[] DistancesExpectedGlass = new double[4] {5, 21, 46.6, 68.6 };
            double[] DistancesExpectedInterlayer = new double[3] { 10.5, 31.3, 62.6 };

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));

            MonolithicGlass mg1 = new MonolithicGlass("Mg2", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialPrEn()); 
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 30, GetGlassMaterialPrEn());
            MonolithicGlass mg4 = new MonolithicGlass("Mg4", 10, GetGlassMaterialPrEn());

            Interlayer intr1 = new Interlayer("Int1", 1, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr2 = new Interlayer("Int2", 0.6, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr3 = new Interlayer("Int3", 2, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4 }, new Interlayer[] { intr1, intr2, intr3 });

            Prototype p = new Prototype("", lg1, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalisys, Prototype.CheckMethods.ASTME1300, Prototype.LaminatedEqThicknessMethods.ASTME1300);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;

            GlassSurface gs = new GlassSurface(p, s1, meshOptions);

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

            
            // Act
            //double[] DistancesGlass = lgw.GetMonolithicBarycenterDistances();
            //double[] DistancesInterlayer = lgw.GetInterlayerBarycenterDistances();

            //// Assert

            //for (int i = 0; i < DistancesExpectedGlass.Length; i++)
            //{
            //    Assert.IsTrue(Math.Abs(DistancesExpectedGlass[i] - DistancesGlass[i]) < _tolleranza, $"Glass => Indice: {i}, Calcolata: {DistancesGlass[i]}, attesa: {DistancesExpectedGlass[i]} ");
            //}

            //for (int i = 0; i < DistancesExpectedInterlayer.Length; i++)
            //{
            //    Assert.IsTrue(Math.Abs(DistancesExpectedInterlayer[i] - DistancesInterlayer[i]) < _tolleranza, $"Interlayer => Indice: {i}, Calcolata: {DistancesInterlayer[i]}, attesa: {DistancesExpectedInterlayer[i]} ");
            //}


        }

        [TestMethod]

        public void LaminatedGetDistance3Glass()
        {
            // Arrange
            double _tolleranza = 0.0001;
            double[] DistancesExpectedGlass = new double[3] { 4, 14.76, 25.52, };
            double[] DistancesExpectedInterlayer = new double[2] { 8.38, 21.14, };

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));

            MonolithicGlass mg1 = new MonolithicGlass("Mg2", 8, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 12, GetGlassMaterialPrEn());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 8, GetGlassMaterialPrEn());

            Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr2 = new Interlayer("Int2", 0.76, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, }, new Interlayer[] { intr1, intr2, });

            Prototype p = new Prototype("", lg1, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalisys, Prototype.CheckMethods.ASTME1300, Prototype.LaminatedEqThicknessMethods.ASTME1300);

            var meshOptions = new Mesh.GenerateOptions();
            meshOptions.MeshSize = 20;

            GlassSurface gs = new GlassSurface(p, s1, meshOptions);

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

            //// Act
            //double[] DistancesGlass = lgw.GetMonolithicBarycenterDistances();
            //double[] DistancesInterlayer = lgw.GetInterlayerBarycenterDistances();

            //// Assert

            //for (int i = 0; i < DistancesExpectedGlass.Length; i++)
            //{
            //    Assert.IsTrue(Math.Abs(DistancesExpectedGlass[i] - DistancesGlass[i]) < _tolleranza, $"Glass => Indice: {i}, Calcolata: {DistancesGlass[i]}, attesa: {DistancesExpectedGlass[i]} ");
            //}

            //for (int i = 0; i < DistancesExpectedInterlayer.Length; i++)
            //{
            //    Assert.IsTrue(Math.Abs(DistancesExpectedInterlayer[i] - DistancesInterlayer[i]) < _tolleranza, $"Interlayer => Indice: {i}, Calcolata: {DistancesInterlayer[i]}, attesa: {DistancesExpectedInterlayer[i]} ");
            //}



        }

        [TestMethod]

            public void LaminatedGetDistance2Glass()
            {
                // Arrange
                double _tolleranza = 0.0001;
                double[] DistancesExpectedGlass = new double[2] { 4, 12.76, };
                double[] DistancesExpectedInterlayer = new double[1] { 8.38, };

                Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));

                MonolithicGlass mg1 = new MonolithicGlass("Mg2", 8, GetGlassMaterialPrEn());
                MonolithicGlass mg2 = new MonolithicGlass("Mg2", 8, GetGlassMaterialPrEn());

                Interlayer intr1 = new Interlayer("Int1", 0.76, GetInterlayerMaterial(), Guid.NewGuid());

                LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, }, new Interlayer[] { intr1, });

                Prototype p = new Prototype("", lg1, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalisys, Prototype.CheckMethods.ASTME1300, Prototype.LaminatedEqThicknessMethods.ASTME1300);
                
                var meshOptions = new Mesh.GenerateOptions();
                meshOptions.MeshSize = 20;
                
                GlassSurface gs = new GlassSurface(p, s1, meshOptions);

                LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

                //// Act
                //double[] DistancesGlass = lgw.GetMonolithicBarycenterDistances();
                //double[] DistancesInterlayer = lgw.GetInterlayerBarycenterDistances();

                //// Assert

                //for (int i = 0; i < DistancesExpectedGlass.Length; i++)
                //{
                //    Assert.IsTrue(Math.Abs(DistancesExpectedGlass[i] - DistancesGlass[i]) < _tolleranza, $"Glass => Indice: {i}, Calcolata: {DistancesGlass[i]}, attesa: {DistancesExpectedGlass[i]} ");
                //}

                //for (int i = 0; i < DistancesExpectedInterlayer.Length; i++)
                //{
                //    Assert.IsTrue(Math.Abs(DistancesExpectedInterlayer[i] - DistancesInterlayer[i]) < _tolleranza, $"Interlayer => Indice: {i}, Calcolata: {DistancesInterlayer[i]}, attesa: {DistancesExpectedInterlayer[i]} ");
                //}
            }
    }
}

