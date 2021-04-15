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
using GPC.Model.Combinations;
using GPC.Model.Results;

namespace GPC.Checker.Glasses.Checkers
{
    public abstract class Checker : GPC.Checker.Common.Checker
    {
        protected GlassSurface _glassSurface;

        protected FemModelWrapper _femModel;

        protected string _folderPath;

        /// <summary>
        /// List of global and specific combinations
        /// </summary>
        protected List<Combination> _combinations;

        public Checker(GlassSurface glassSurface, List<Combination> globalCombinations)
        {
            this._glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));

            // Creo lista combinazioni sommando la lista delle globali a quelli del prototipo
            this._combinations = globalCombinations == null ? new List<Combination>() : globalCombinations;
            this._combinations.AddRange(glassSurface.Prototype.Combinations);

        }


        #region Public method

        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        }


        /// <summary>
        /// Set up the FemModel class.
        /// </summary>
        /// <param name="folderPath"></param>
        public void FemModelSetup(string folderPath)
        {
            try
            {
                _folderPath = folderPath;
                Directory.CreateDirectory(_folderPath);
            }
            catch
            {
                _folderPath = Path.Combine(Path.GetTempPath(), "gc_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(_folderPath);
            }


            GlassWrapper glassWrapper;
            if (_glassSurface.Prototype.Glass is MonolithicGlass mg)
            {
                MonolithicGlassWrapper wrapper = new MonolithicGlassWrapper(_glassSurface, mg);
                wrapper.AddExternalFaceLoads(_glassSurface.GetLoads());

                // Recupero la mesh - wrapper la genera
                List<Mesh> meshes = wrapper.Meshes;

                if (meshes.Count > 1)
                    throw new NotSupportedException();

                // Creo modello
                _femModel = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                MonolithicGlassProperty pp = new MonolithicGlassProperty(mg, "mg");

                Mesh meshExternal = wrapper.GetExternalGlassMesh(); 
                Mesh meshInternal = wrapper.GetExternalGlassMesh();

                GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              wrapper.MeshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMap, 
                                              out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMap,
                                              out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMap);

                _femModel.AddProperty(pp);
                _femModel.AddMesh(meshes.First(), pp.Name, null, vertexLoadMeshEntityMap, vertexLineLoadMeshEntityMap, faceAreaLoadMeshEntityMap, 
                                  wrapper.MeshGeometryRestrainVertices.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value);

                _femModel.AddCombinations(_combinations);

            }
            else if (_glassSurface.Prototype.Glass is LaminatedGlass lg)
            {
                LaminatedGlassWrapper wrapper = new LaminatedGlassWrapper(_glassSurface, lg);

                wrapper.AddInternalFaceLoads(_glassSurface.GetLoads());

                // Recupero la mesh - wrapper la genera
                List<Mesh> meshes = wrapper.Meshes;

                Mesh meshExternal = wrapper.GetExternalGlassMesh();
                Mesh meshInternal = wrapper.GetInternalGlassMesh();

                GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              wrapper.MeshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMapExternal,
                                              out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMapExternal,
                                              out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMapExternal);

                GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(meshInternal.Guid)).FirstOrDefault().Value,
                                              wrapper.MeshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(meshInternal.Guid)).FirstOrDefault().Value,
                                              out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMapInternal,
                                              out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMapInternal,
                                              out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMapInternal);

                var package = lg.GetGlassPackage();

                _femModel = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                //_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalisys
                
                for (int i = 0; i < package.Length; i++)
                {
                    IGlassPackage layer = package[i];
                    if (layer is MonolithicGlass)
                    {
                        string propertyName = $"Mg {i}";
                        _femModel.AddProperty(new MonolithicGlassProperty((MonolithicGlass)layer, propertyName));

                        if (meshes[i].CompareGuid(meshExternal.Guid))
                        {
                            _femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapExternal, vertexLineLoadMeshEntityMapExternal, faceAreaLoadMeshEntityMapExternal,
                                                wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value);
                        }
                        else if (meshes[i].CompareGuid(meshInternal.Guid))
                        {
                            _femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapInternal, vertexLineLoadMeshEntityMapInternal, faceAreaLoadMeshEntityMapInternal,
                                                wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshInternal.Guid)).FirstOrDefault().Value);
                        }
                        else
                        {
                            _femModel.AddMesh(meshes[i], propertyName, null, null, null, null, null);
                        }
                    }
                    else if (layer is Interlayer)
                    {
                        string propertyName = $"Interlayer {i}";
                        _femModel.AddProperty(new InterlayerBrickProperty((Interlayer)layer, 10, 20, propertyName));

                        _femModel.AddMesh(meshes[i], null, propertyName, null, null, null, null);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }

            }
            else if (_glassSurface.Prototype.Glass is DoubleInsulatingGlass dgu)
            {
                glassWrapper = new DoubleInsulatingGlassWrapper(_glassSurface, dgu);
            }
            else if (_glassSurface.Prototype.Glass is TripleInsulatingGlass tgu)
            {
                glassWrapper = new TripleInsulatingGlassWrapper(_glassSurface, tgu);
            }
            else
            {
                throw new NotSupportedException();
            }


        }


        /// <summary>
        /// 
        /// </summary>
        /// <remarks> <see cref="FemModelSetup(string)"/> must be called before calling this method</remarks>
        public void PerformCheck()
        {
            if (_femModel == null)
                throw new ApplicationException($"FemModel is null. {nameof(FemModelSetup)} should be called before calling this method");

            if (_glassSurface.Prototype.SolverType == Models.Prototype.SolverTypes.Straus7)
            {
                if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalysis)
                {
                    var glass = _glassSurface.Prototype.Glass;

                    GlassWrapper glassWrapper;
                    if (glass is MonolithicGlass mg)
                    {
                        _femModel.SaveToSt7(_folderPath);

                        _femModel.RunSt7Solver(Models.Prototype.AnalysisTypes.LinearStaticAnalysis);

                        _femModel.ReadSt7LinearCombinationResults();

                    }
                    else if (glass is LaminatedGlass lg)
                    {
                        _femModel.SaveToSt7(_folderPath);

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
                    throw new NotImplementedException(_glassSurface.Prototype.AnalysisType.ToString());
                }
                else
                    throw new NotSupportedException(_glassSurface.Prototype.AnalysisType.ToString());
            }
            else
            {
                throw new NotImplementedException(_glassSurface.Prototype.SolverType.ToString());
            }            

        }

        /// <summary>
        /// The mesh is generated by calling <see cref="FemModelSetup(string)"/>
        /// </summary>
        /// <returns>Null if FemModel is not available</returns>
        public Mesh GetMesh()
        {
            if (_femModel is null)
                return null;

            return _femModel.GetMesh();
        }


        #endregion


        #region Public method results

        public List<ResultPlateStress> GetPlateCombinationsResults()
        {
            return _femModel.ResultPlateStresses;
        }


        public List<ResultNodeDisplacement> GetNodeDisplacementCombinationResults()
        {
            return _femModel.ResultNodeDisplacement;
        }


        public void GetWorkinRatio()
        {
            throw new NotImplementedException();
        }

        #endregion


        #region private protected methods

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


        /// <returns>The Load-Vertex/Face Index map differentiated for load type</returns>
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


        #endregion



    }
}
