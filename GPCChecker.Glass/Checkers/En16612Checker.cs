using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;
using GPC.Model.Loads;
using GPC.Model.Elements;
using GPC.Model.FEM.Properties;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Model.Restrains;

namespace GPC.Checker.Glasses.Checkers
{
    public class En16612Checker : Checker
    {

        public En16612Checker(GlassSurface glassSurface)
            : base(glassSurface)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.EN16612)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.EN16612.ToString()} {GetCheckerName()} Checker");



        }

        



        #region Override public methods

        /// <summary>
        /// Metodo responsabile della verifica
        /// 1 - Genera la mesh
        /// 2 - Orchestra il modello fem
        /// 3 - Prende i risultati 
        /// 4 - Fa la verifica
        /// </summary>
        /// 
        public override void SetUpFemModels()
        {

            //femWrapper.SetAnalysisType(_checkParameters.GetAnalysisType());

            
            //foreach (var wrapper in wrappers)
            //{
            //    //FemModelWrapper femWrapper = new FemModelWrapper("fem" + wrappers.IndexOf(wrapper), _checkParameters.GetAnalysisType());
            //    //if (wrapper is MonolithicGlassWrapper mgw)
            //    //{
            //    //    var geometryMesh = mgw.Mesh;
            //    //    var embeddedGeometriesMapVertex = mgw.EmbeddedGeometriesMapVertex;

            //    //    var restrains = mgw.GetRestrains();

            //    //    mgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

            //    //    var monolithicGlassProperty = new MonolithicGlassProperty(mgw.Glass);

            //    //    femWrapper.SetUpMonolithic(geometryMesh, embeddedGeometriesMapVertex, restrains, mgw.Glass, uniformPressureLoads, notUniformPressureLoads);
            //    //}
            //    //else if (wrapper is LaminatedGlassWrapper lgw)
            //    //{
            //    //    var geometryMesh = lgw.Mesh;
            //    //    var embeddedGeometriesMapVertex = lgw.EmbeddedGeometriesMapVertex;

            //    //    var restrains = lgw.GetRestrains();

            //    //    lgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

            //    //    femWrapper.SetUpLaminated(geometryMesh, embeddedGeometriesMapVertex, restrains, lgw, uniformPressureLoads, notUniformPressureLoads, CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);
            //    //}
            //    //else if (wrapper is InsulatedGlassWrapper igw)
            //    //{
            //    //    throw new NotImplementedException();
            //    //}
            //    //else
            //    //    throw new NotSupportedException("Glass wrapper not supported");

            //    //_femModels.Add(femWrapper);
            //}
        }

        protected override string GetCheckerName() => "EN 16612 - 2019";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderPath">Folder where to save the results</param>
        public override void PerformCheck(string folderPath)
        {

            Directory.CreateDirectory(folderPath);


            if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalisys)
            {
                var glass = _glassSurface.Prototype.Glass;

                GlassWrapper glassWrapper;
                if (glass is MonolithicGlass mg)
                {
                    MonolithicGlassWrapper wrapper = new MonolithicGlassWrapper(_glassSurface, mg);

                    List<Mesh> meshes = wrapper.Meshes;
                    Dictionary<Mesh, Dictionary<GeometryRestrain, int[]> > meshGeometryRestrainVertices =  wrapper.MeshGeometryRestrainVertices;

                    if (meshes.Count > 1)
                        throw new NotSupportedException();

                    FemModelWrapper femModelWrapper = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                    MonolithicGlassProperty pp = new MonolithicGlassProperty(mg);


                    femModelWrapper.AddMesh(meshes.First(), pp, null, null, null, null, meshGeometryRestrainVertices.Values.FirstOrDefault());

                    femModelWrapper.SaveToSt7(folderPath);
                }
                else if (glass is LaminatedGlass lg)
                {
                    glassWrapper = new LaminatedGlassWrapper(_glassSurface, lg);
                }
                else if (glass is DoubleInsulatingGlass dgu)
                {
                    glassWrapper = new DoubleInsulatingGlassWrapper(_glassSurface, dgu);
                }
                else if (glass is TripleInsulatingGlass tgu)
                {
                    glassWrapper = new TripleInsulatingGlassWrapper(_glassSurface, tgu);
                }
                else
                {
                    throw new NotSupportedException();
                }



            }
            else if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.NonLinearStaticAnalysis)
            {

            }
            else
                throw new NotSupportedException();

        }

        #endregion

    }
}
