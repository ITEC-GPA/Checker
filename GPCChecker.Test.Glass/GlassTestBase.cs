using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.TestUtilities;
using GPC.Model.Results;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GlassTests
{
    public abstract class GlassTestBase : UnitTestBase
    {
        protected GlassTestBase()
        {
        }

        #region Shape

        protected Shape GetRectangularShape(double width, double height)
        {
            Polygon3d p = new Polygon3d()
            {
                new Point3d(0,          0,      0),
                new Point3d(width,      0,      0),
                new Point3d(width, height,      0),
                new Point3d(0,     height,      0)
            };

            return new Shape(p);
        }

        protected Shape GetRectangularShape(Point3d p, Vector3d vector)
        {
            Polygon3d poly = new Polygon3d()
            {
                new Point3d(p.X,            p.Y,            p.Z           ),
                new Point3d(p.X + vector.X, p.Y,            p.Z           ),
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z + vector.Z),
                new Point3d(p.X,            p.Y + vector.Y, p.Z + vector.Z)
            };

            return new Shape(poly, null, null);
        }

        #endregion

        #region Material

        protected GlassMaterialEn16612 GetGlassMaterialEn16612(double fgk = 45)
        {
            return new GlassMaterialEn16612("Glass", 70000, 0.23, fgk, GlassMaterialEn16612.GlassTypes.FloatGlass, GlassMaterialEn16612.SurfaceTreatments.AsProduced,
                                        GlassMaterialEn16612.PrestressTypes.HeatStrengthened, GlassMaterialEn16612.ManufactoringProcesses.HorizontalToughening,
                                        GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0);
        }

        protected GlassMaterialEn16612 GetGlassMaterialEn16612(double fgk, GlassMaterialEn16612.GlassTypes glassType, GlassMaterialEn16612.SurfaceTreatments surfaceTreatments,
                                                             GlassMaterialEn16612.PrestressTypes prestress, GlassMaterialEn16612.ManufactoringProcesses manufactoring)
        {
            return new GlassMaterialEn16612("Glass", 70000, 0.23, fgk, glassType, surfaceTreatments,
                                        prestress, manufactoring, GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0);
        }

        protected GlassMaterialAstm GetGlassMaterialAstm(double psiSurface = 1, double nCoeff = 16, double surfaceBaseStress = 23.3, double surfaceBaseEdgeStress = 18.3, double probabiltyOfBreakage = 0.001)
        {
            return new GlassMaterialAstm("Glass", 70000, 0.23, psiSurface, nCoeff, surfaceBaseStress, surfaceBaseEdgeStress, probabiltyOfBreakage,
                                            GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0.1);
        }

        protected InterlayerMaterial GetInterlayerMaterial()
        {
            var it = new InterlayerMaterial("", GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 50 }, new double[] { 0.1, 0.2, 0.30 });
            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });

            it.AddShearModule(100, new double[] { 10, 20, 50 }, new double[] { 0.15, 0.25, 0.35 });
            return it;
        }

        /// <summary>
        /// Set the InterlayerMaterial with the Shear modulus of SentryGlas
        /// </summary>
        protected InterlayerMaterial GetInterlayerMaterialSentryGlas()
        {
            var it = new InterlayerMaterial("SG", GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0, InterlayerMaterial.InterlayerType.SentryGlass);
            it.AddShearModule(3, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 236, 211, 141, 63, 26.4, 8.2, 2.9, 1.3 });
            it.AddShearModule(30, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 228, 206, 119, 36.6, 13.5, 4.3, 2.1, 1.0 });
            it.AddShearModule(60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 225, 195, 110, 30.7, 11.3, 3.7, 1.9, 0.8 });
            it.AddShearModule(5 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 220, 188, 82.8, 19.4, 7.3, 2.6, 1.4, 0.6 });
            it.AddShearModule(30 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 217, 175, 66.1, 11.4, 4.9, 1.9, 1.0, 0.4 });
            it.AddShearModule(60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 206, 169, 60.0, 9.3, 4.2, 1.7, 0.8, 0.3 });
            it.AddShearModule(1 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 190, 146, 49.7, 4.5, 2.8, 1.3, 0.6, 0.3 });
            it.AddShearModule(5 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 178, 130, 24.7, 3.6, 2.4, 1.2, 0.6, 0.2 });
            it.AddShearModule(21 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 172, 115, 12.9, 3.3, 2.2, 1.2, 0.5, 0.2 });
            it.AddShearModule(30 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 171, 112, 11.6, 3.3, 2.2, 1.1, 0.5, 0.2 });
            it.AddShearModule(365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 161, 96.5, 6.8, 3.1, 2.1, 1.0, 0.5, 0.2 });

            it.AddShearModule(50 * 365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            return it;
        }

        /// <summary>
        /// Set the InterlayerMaterial with the Shear modulus of ES Stiff PVB
        /// </summary>
        protected InterlayerMaterial GetInterlayerMaterialPVBStiff()
        {
            var it = new InterlayerMaterial("ES Stiff PVB", GPC.Model.Units.ConvertDensityToDefault(2500, GPC.Model.Units.SI), 0, InterlayerMaterial.InterlayerType.NormalPVB);
            it.AddShearModule(3, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 699, 342, 58, 3.4, 1.7, 1.6, 0, 0 });
            //it.AddShearModule(30,               new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 228, 206, 119, 36.6, 13.5, 4.3, 2.1, 1.0 });
            it.AddShearModule(60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 573, 196, 9.2, 1.8, 1.6, 1.5, 1.9, 0.8 });
            //it.AddShearModule(5 * 60,           new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 220, 188, 82.8, 19.4, 7.3, 2.6, 1.4, 0.6 });
            //it.AddShearModule(30 * 60,          new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 217, 175, 66.1, 11.4, 4.9, 1.9, 1.0, 0.4 });
            it.AddShearModule(60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 388, 37, 2, 1.6, 0, 0, 0, 0 });
            //it.AddShearModule(1 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 190, 146, 49.7, 4.5, 2.8, 1.3, 0.6, 0.3 });
            //it.AddShearModule(5 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 178, 130, 24.7, 3.6, 2.4, 1.2, 0.6, 0.2 });
            //it.AddShearModule(21 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 172, 115, 12.9, 3.3, 2.2, 1.2, 0.5, 0.2 });
            it.AddShearModule(30 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 80, 1.9, 1.5, 1.5, 0, 0, 0, 0 });
            it.AddShearModule(365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 19, 1.6, 1.5, 0, 0, 0, 0, 0 });

            it.AddShearModule(50 * 365 * 24 * 60 * 60, new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            return it;
        }

        #endregion

        #region Export

        protected void ExportMesh(Mesh mesh)
        {
            MeshExport.ExportToMshFormatv2(base.GetFilePathInOutputFolder("Mesh", "msh"), new List<Mesh>() { mesh });
        }

        #endregion

        #region Straus7

        protected void RunApiServer()
        {
            if (Process.GetProcessesByName("St7ApiServer").Length == 0)
            {
                string filePath = System.AppContext.BaseDirectory;
                Console.WriteLine(filePath);

                filePath = Path.GetFullPath(Path.Combine(filePath, @"..\..\..\..\"));
                Console.WriteLine(filePath);

                filePath = Path.GetFullPath(Path.Combine(filePath, @"StrausApi64\St7ApiServer\bin\Debug\St7ApiServer.exe"));

                if (File.Exists(filePath))
                {
                    var version = FileVersionInfo.GetVersionInfo(filePath);

                    Version minimumVersion = new Version(1, 0, 25, 0);

                    if (minimumVersion.CompareTo(new Version(version.FileVersion)) < 0)
                    {
                        throw new ApplicationException($"St7ApiServer version {version.FileVersion} is too lower, you need version: {minimumVersion} or higher");
                    }

                    Process process = new Process();
                    process.StartInfo.FileName = filePath;
                    process.Start();
                }
                else
                {
                    throw new ApplicationException($"Api server not found at this location {filePath}. Start the ApiServer manually");
                }
            }
        }

        #endregion

        #region Standard

        protected class EN16612LoadDurations
        {
            public const double WIND = 3;
            public const double LIVE = 30;
            public const double LIVECROWD = 5 * 60;
            public const double MAINTENANCE = 30 * 60;
            public const double CLIMATEWINTER = 6 * 60 * 60;
            public const double CLIMATESUMMER = 12 * 60 * 60;
            public const double SNOW = 5 * 24 * 60 * 60;
            public const double SELFWEIGHT = 50 * 365 * 24 * 60 * 60;
        }

        #endregion

        #region Results

        protected ResultDisplacement[] GetWorstDisplacementResults(IEnumerable<NodeResult> nodeDisplacements)
        {
            ResultDisplacement[] worstResults = new ResultDisplacement[12];
            // [0] == max D1
            // [1] == max D2
            // [3] == max D3
            // ...
            // [7] == min D1

            for (int i = 0; i < worstResults.Length; i++)
            {
                worstResults[i] = (ResultDisplacement)nodeDisplacements.First().Result;
            }

            foreach (NodeResult result in nodeDisplacements)
            {
                var res = result.Result as ResultDisplacement;


                if (worstResults[0] != null && res.D1 > worstResults[0].D1)
                {
                    worstResults[0] = res;
                }
                else if (worstResults[1] != null && res.D2 > worstResults[1].D2)
                {
                    worstResults[1] = res;
                }
                else if (worstResults[2] != null && res.D3 > worstResults[2].D3)
                {
                    worstResults[2] = res;
                }
                else if (worstResults[3] != null && res.R1 > worstResults[3].R1)
                {
                    worstResults[3] = res;
                }
                else if (worstResults[4] != null && res.R2 > worstResults[4].R2)
                {
                    worstResults[4] = res;
                }
                else if (worstResults[5] != null && res.R3 > worstResults[5].R3)
                {
                    worstResults[5] = res;
                }


                if (worstResults[6] != null && res.D1 < worstResults[6].D1)
                {
                    worstResults[6] = res;
                }
                else if (worstResults[7] != null && res.D2 < worstResults[7].D2)
                {
                    worstResults[7] = res;
                }
                else if (worstResults[8] != null && res.D3 < worstResults[8].D3)
                {
                    worstResults[8] = res;
                }
                else if (worstResults[9] != null && res.R1 < worstResults[9].R1)
                {
                    worstResults[9] = res;
                }
                else if (worstResults[10] != null && res.R2 < worstResults[10].R2)
                {
                    worstResults[10] = res;
                }
                else if (worstResults[11] != null && res.R3 < worstResults[11].R3)
                {
                    worstResults[11] = res;
                }


            }

            return worstResults;
        }


        protected ResultStress[] GetWorstStressResults(IEnumerable<FiniteElementResult> plateStresses)
        {
            ResultStress[] worstResults = new ResultStress[4];
            // [0] == max S11
            // [1] == max S22


            for (int i = 0; i < worstResults.Length; i++)
            {
                worstResults[i] = (ResultStress)plateStresses.First().Results.First();
            }

            foreach (FiniteElementResult results in plateStresses)
            {
                foreach(var result in results.Results)
                {
                    var res = result as ResultStress;
                    
                    if (res != null)
                    {
                        if (worstResults[0] != null && res.S11 > worstResults[0].S11)
                        {
                            worstResults[0] = res;
                        }
                        else if (worstResults[1] != null && res.S22 > worstResults[1].S22)
                        {
                            worstResults[1] = res;
                        }

                        if (worstResults[2] != null && res.S11 < worstResults[2].S11)
                        {
                            worstResults[2] = res;
                        }
                        else if (worstResults[3] != null && res.S22 < worstResults[3].S22)
                        {
                            worstResults[3] = res;
                        }
                    }
                }
            }

            return worstResults;
        }


        #endregion 
    }
}