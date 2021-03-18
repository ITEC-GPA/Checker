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
            //this._femModel = new List<FemModelWrapper>();

            // Creo lista combinazioni sommando la lista delle globali a quelli del prototipo
            this._combinations = globalCombinations == null ? new List<Combination>() : globalCombinations;
            this._combinations.AddRange(glassSurface.Prototype.Combinations);

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
                _femModel = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                MonolithicGlassProperty pp = new MonolithicGlassProperty(mg);

                GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.ContainsKey(meshes.First()) ? wrapper.MeshLoadsVertexIndexes[meshes.First()] : null,
                                              wrapper.MeshLoadsFaceIndexes.ContainsKey(meshes.First()) ? wrapper.MeshLoadsFaceIndexes[meshes.First()] : null,
                                              out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMap, out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMap,
                                              out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMap);

                _femModel.AddMesh(meshes.First(), pp, null, vertexLoadMeshEntityMap, vertexLineLoadMeshEntityMap, faceAreaLoadMeshEntityMap, wrapper.MeshGeometryRestrainVertices.Values.FirstOrDefault());

                _femModel.AddCombinations(_combinations);

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

        /// <summary>
        /// 
        /// </summary>
        /// <remarks> <see cref="FemModelSetup(string)"/> must be called before calling this method</remarks>
        public void PerformCheck()
        {
            if (_femModel == null)
                throw new ApplicationException($"FemModel is null. {nameof(FemModelSetup)} should be called before calling this method");
            

            if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalisys)
            {
                var glass = _glassSurface.Prototype.Glass;

                GlassWrapper glassWrapper;
                if (glass is MonolithicGlass mg)
                {                    
                    _femModel.SaveToSt7(_folderPath);
                    
                    _femModel.RunSt7Solver(Models.Prototype.AnalysisTypes.LinearStaticAnalisys);
                    
                    _femModel.ReadSt7LinearCombinationResults();

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

    }
}
