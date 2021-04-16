using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Models;
using GPC.Checker.Glasses.Wrappers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GlassTests
{
    [TestClass]
    public class GlassWrapperTest : GlassTestBase
    {
        #region Private methods

        private GlassMaterialEn16612 GetGlassMaterialPrEn()
        {
            return new GlassMaterialEn16612("Glass", 70000, 0.23, 25, 
                                        GlassMaterialEn16612.GlassTypes.DrawnSheetGlass, GlassMaterialEn16612.SurfaceTreatments.AsProduced,
                                        GlassMaterialEn16612.PrestressTypes.HeatStrengthened, GlassMaterialEn16612.ManufactoringProcesses.HorizontalToughening, 2700 * 10E-12, 0);
        }

        private InterlayerMaterial GetInterlayerMaterial()
        {
            var it = new InterlayerMaterial("", 1, 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 50 }, new double[] { 0.1, 0.2, 0.30 });
            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });
            return it;
        }

        #endregion 

        [TestMethod]
        public void LaminatedGetDistance()
        {
            // Arrange
            double _tolleranza = 0.0001;
            double[] distancesExpectedGlass = new double[4] { 0, 16, 41.6, 63.6 };
            double[] distancesExpectedInterlayer = new double[3] { 5.5, 26.3, 57.6 };

            Shape s1 = GetRectangularShape(new Point3d(0, 0, 0), new Vector3d(200, 0, 1000));

            MonolithicGlass mg1 = new MonolithicGlass("Mg2", 10, GetGlassMaterialPrEn());
            MonolithicGlass mg2 = new MonolithicGlass("Mg2", 20, GetGlassMaterialPrEn());
            MonolithicGlass mg3 = new MonolithicGlass("Mg3", 30, GetGlassMaterialPrEn());
            MonolithicGlass mg4 = new MonolithicGlass("Mg4", 10, GetGlassMaterialPrEn());

            Interlayer intr1 = new Interlayer("Int1", 1, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr2 = new Interlayer("Int2", 0.6, GetInterlayerMaterial(), Guid.NewGuid());
            Interlayer intr3 = new Interlayer("Int3", 2, GetInterlayerMaterial(), Guid.NewGuid());

            LaminatedGlass lg1 = new LaminatedGlass("Lg1", new MonolithicGlass[] { mg1, mg2, mg3, mg4 }, new Interlayer[] { intr1, intr2, intr3 });

            Prototype p = new Prototype("", lg1, null, null, null, Prototype.Standards.ASTME1300, Prototype.AnalysisTypes.LinearStaticAnalysis,
                              Prototype.CheckMethods.ASTME1300, Prototype.LaminatedEqThicknessMethods.ASTME1300, Prototype.SolverTypes.Straus7, Prototype.LaminatedAnalysisTypes.MultiElement);

            GlassSurface gs = new GlassSurface(p, s1, new Mesh.GenerateOptions());

            LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(gs, lg1);

            // Act
            double[] distancesGlass = lgw.GetMonolithicBarycenterDistances();
            double[] distancesInterlayer = lgw.GetInterlayerBarycenterDistances();

            // Assert
            for (int i = 0; i < distancesExpectedGlass.Length; i++)
            {
                Assert.IsTrue(Math.Abs(distancesExpectedGlass[i] - distancesGlass[i]) < _tolleranza, $"Glass => Indice: {i}, Calcolata: {distancesGlass[i]}, attesa: {distancesExpectedGlass[i]} ");
            }

            for (int i = 0; i < distancesExpectedInterlayer.Length; i++)
            {
                Assert.IsTrue(Math.Abs(distancesExpectedInterlayer[i] - distancesInterlayer[i]) < _tolleranza, $"Interlayer => Indice: {i}, Calcolata: {distancesInterlayer[i]}, attesa: {distancesExpectedInterlayer[i]} ");
            }
        }
    }
}