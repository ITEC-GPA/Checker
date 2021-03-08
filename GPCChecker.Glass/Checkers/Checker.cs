using System;
using System.IO;
using System.Collections.Generic;
using GPC.Checker.Glasses.FemModel;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Checker.Glasses.Results;
using GPC.Model.FEM.Properties;
using GPC.Model.Loads;
using System.Linq;

namespace GPC.Checker.Glasses.Checkers
{
    public abstract class Checker : GPC.Checker.Common.Checker
    {
        protected GlassSurface _glassSurface;

        protected List<FemModelWrapper> _femModels;

        public Checker(GlassSurface glassSurface)
        {
            this._glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));
            this._femModels = new List<FemModelWrapper>();
        }

        protected GlassWrapper GetWrapper()
        {
            var glass = _glassSurface.Prototype.Glass;

            if (glass is MonolithicGlass mg)
            {
                return new MonolithicGlassWrapper(_glassSurface, mg);
            }
            else if (glass is LaminatedGlass lg)
            {
                return new LaminatedGlassWrapper(_glassSurface, lg);
            }
            else if (glass is DoubleInsulatingGlass dgu)
            {
                return new DoubleInsulatingGlassWrapper(_glassSurface, dgu);
            }
            else if (glass is TripleInsulatingGlass tgu)
            {
                return new TripleInsulatingGlassWrapper(_glassSurface, tgu);
            }
            else
            {
                throw new NotSupportedException();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderPath">Folder where to save the results</param>
        public GlassResult PerformCheck(string folderPath)
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

        protected abstract override string GetCheckerName();

        public abstract void SetUpFemModels();

        public void RunSt7Solver()
        {
            //foreach (FemModelWrapper femModel in _femModels)
            //{
            //    femModel.RunSt7Solver(Path.Combine(_model.OutputFolder, Path.ChangeExtension(femModel.Name, "st7")));
            //}
        }


    }
}
