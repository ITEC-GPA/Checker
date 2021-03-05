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
using GPC.Checker.Glasses.Results;

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
        public override GlassResult PerformCheck(string folderPath)
        {

            GlassResult worstGlassResult = null;

            Directory.CreateDirectory(folderPath);


            if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalisys)
            {
                var glass = _glassSurface.Prototype.Glass;

                GlassWrapper glassWrapper;
                if (glass is MonolithicGlass mg)
                {
                    MonolithicGlassWrapper wrapper = new MonolithicGlassWrapper(_glassSurface, mg);
                    wrapper.AddLoads(_glassSurface.GetLoads());

                    // Recupero la mesh - wrapper la genera
                    List<Mesh> meshes = wrapper.Meshes;

                    if (meshes.Count > 1)
                        throw new NotSupportedException();


                    // Creo modello
                    FemModelWrapper femModelWrapper = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                    MonolithicGlassProperty pp = new MonolithicGlassProperty(mg);

                    GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.ContainsKey(meshes.First()) ? wrapper.MeshLoadsVertexIndexes[meshes.First()] : null,
                                                  wrapper.MeshLoadsFaceIndexes.ContainsKey(meshes.First()) ? wrapper.MeshLoadsFaceIndexes[meshes.First()] : null,
                                                  out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMap, out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMap, 
                                                  out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMap);

                    femModelWrapper.AddMesh(meshes.First(), pp, null, vertexLoadMeshEntityMap, vertexLineLoadMeshEntityMap, faceAreaLoadMeshEntityMap, wrapper.MeshGeometryRestrainVertices.Values.FirstOrDefault());

                    femModelWrapper.SaveToSt7(folderPath);

                    femModelWrapper.RunSt7Solver(Models.Prototype.AnalysisTypes.LinearStaticAnalisys);

                    femModelWrapper.ReadSt7LinearResults();

                    worstGlassResult = femModelWrapper.GetMaxWorkingRatio();

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
                throw new NotImplementedException();
            }
            else
                throw new NotSupportedException();

            return worstGlassResult;
        }


        #endregion


        private void GetLoadTypeVerticesDictionary(Dictionary<Load, int[]> vertexLoadEntityMap, Dictionary<Load, int[]> areaLoadEntityMap, 
                                                   out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMap, out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMap, 
                                                   out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMap)
        {

            vertexLoadMeshEntityMap = new Dictionary<IPointLoad, int[]>();
            vertexLineLoadMeshEntityMap = new Dictionary<ILineLoad, int[]>();
            faceAreaLoadMeshEntityMap = new Dictionary<IAreaLoad, int[]>();

            
            if (vertexLoadEntityMap != null)
            {
                foreach (var kvp in vertexLoadEntityMap)
                {
                    Load load = kvp.Key;
                    int[] indexes = kvp.Value;

                    if (load is IPointLoad ipl)
                    {
                        if (vertexLoadMeshEntityMap.ContainsKey(ipl))
                        {
                            var buffer = vertexLoadMeshEntityMap[ipl].ToList();
                            buffer.AddRange(indexes.ToList());

                            vertexLoadMeshEntityMap[ipl] = buffer.Distinct().ToArray();
                        }
                        vertexLoadMeshEntityMap.Add(ipl, indexes);
                    }
                    else if (load is ILineLoad ill)
                    {
                        if (vertexLineLoadMeshEntityMap.ContainsKey(ill))
                        {
                            var buffer = vertexLineLoadMeshEntityMap[ill].ToList();
                            buffer.AddRange(indexes.ToList());

                            vertexLineLoadMeshEntityMap[ill] = buffer.Distinct().ToArray();
                        }
                        vertexLineLoadMeshEntityMap.Add(ill, indexes);
                    }
                } 
            }

            if (areaLoadEntityMap != null)
            {
                foreach (var kvp in areaLoadEntityMap)
                {
                    Load load = kvp.Key;
                    int[] indexes = kvp.Value;

                    if (load is IAreaLoad ial)
                    {
                        if (faceAreaLoadMeshEntityMap.ContainsKey(ial))
                        {
                            var buffer = faceAreaLoadMeshEntityMap[ial].ToList();
                            buffer.AddRange(indexes.ToList());

                            faceAreaLoadMeshEntityMap[ial] = buffer.Distinct().ToArray();
                        }
                        faceAreaLoadMeshEntityMap.Add(ial, indexes);
                    }

                } 
            }
        }

    }
}
