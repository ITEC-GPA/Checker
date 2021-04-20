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
using GPC.Model.Results;
using GPC.Model.FEM;
using GPC.Checker.Glasses.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Extensions;

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
        public bool FemModelSetup(string folderPath)
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

                // GEOMETRIA
                List<Mesh> meshes = wrapper.Meshes;

                if (meshes.Count > 1)
                    throw new NotSupportedException();

                // Creo modello
                _femModel = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                MonolithicGlassProperty pp = new MonolithicGlassProperty(mg, "mg");

                Mesh meshExternal = wrapper.GetExternalGlassMesh();

                GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              wrapper.MeshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMapExternal,
                                              out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMapExternal,
                                              out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMapExternal);

                _femModel.AddProperty(pp);
                _femModel.AddMesh(meshes.First(), pp.Name, null, vertexLoadMeshEntityMapExternal, vertexLineLoadMeshEntityMapExternal, faceAreaLoadMeshEntityMapExternal, 
                                  wrapper.MeshGeometryRestrainVertices.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value);

                // TIPO DI ANALISI

                if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalysis)
                {
                    // COMBINAZIONI LINEARI - NO STAGE
                    // STRAUS: LINEAR LOAD COMBINATION TABLE
                    _femModel.AnalysisType = Model.FEM.FemModel.AnalysisTypes.Linear;
                    _femModel.AddCombinations(_combinations);
                }
                else if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.NonLinearStaticAnalysis)
                {
                    // COMBINAZIONI NON LINEARI - NO STAGE
                    // STRAUS: MONOSTAGE, INCREMENTI COME COMBINAZIONI NON LINEARI

                    _femModel.AnalysisType = Model.FEM.FemModel.AnalysisTypes.NonLinear;

                    _femModel.AddCombinations(_combinations);

                    Stage stage = _femModel.AddStageAsCopyOfModel("Stage1", Model.FEM.FemModel.AnalysisTypes.Linear);
                    
                }
                else
                {
                    throw new NotImplementedException();
                }

                return true;
            }
            else if (_glassSurface.Prototype.Glass is LaminatedGlass lg)
            {
                if (_glassSurface.Prototype.LaminatedAnalysisType == Models.Prototype.LaminatedAnalysisTypes.MultiElement)
                {
                    LaminatedGlassWrapper wrapper = new LaminatedGlassWrapper(_glassSurface, lg);

                    // LOADS
                    List<Load> loads = _glassSurface.GetLoads();
                    if (loads.Count == 0)
                        return false ;
                    wrapper.AddInternalFaceLoads(loads);

                    // GEOMETRIA

                    #region geometria

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


                    _femModel = new FemModelWrapper($"FemName_{_glassSurface.Id}");

                    
                    List<LoadCase> loadCasesUnique = loads.Select(i => i.LoadCase as LoadCase).Where(i => i != null).Distinct().ToList();


                    var glassPackage = lg.GetGlassPackage();

                    
                    int[][] elementIndexes = new int[glassPackage.Count()][]; // Associazione fra l'indice del layer e l'indice degli elementi

                    Dictionary<int, string> glassLayerPropertyNameMap = new Dictionary<int, string>();
                    Dictionary<int, Dictionary<LoadCase, string>> interlayerLoadCasePropertyNameMap = new Dictionary<int, Dictionary<LoadCase, string>>();

                    for (int i = 0; i < glassPackage.Length; i++)
                    {
                        IGlassPackage layer = glassPackage[i];
                        if (layer is MonolithicGlass)
                        {
                            string propertyName = $"Mg {i}";
                            _femModel.AddProperty(new MonolithicGlassProperty((MonolithicGlass)layer, propertyName));

                            glassLayerPropertyNameMap[i] = propertyName;

                            if (meshes[i].CompareGuid(meshExternal.Guid))
                            {
                                var indexes = _femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapExternal, vertexLineLoadMeshEntityMapExternal, faceAreaLoadMeshEntityMapExternal,
                                                    wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value);
                                elementIndexes[i] = indexes;
                            }
                            else if (meshes[i].CompareGuid(meshInternal.Guid))
                            {
                                var indexes = _femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapInternal, vertexLineLoadMeshEntityMapInternal, faceAreaLoadMeshEntityMapInternal,
                                                    wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshInternal.Guid)).FirstOrDefault().Value);
                                elementIndexes[i] = indexes;
                            }
                            else
                            {
                                var indexes = _femModel.AddMesh(meshes[i], propertyName, null, null, null, null, null);
                                elementIndexes[i] = indexes;
                            }
                        }
                        else if (layer is Interlayer il)
                        {
                            var properties = GetInterlayerBrickProperties(loadCasesUnique, il.Material, _femModel.GetBrickPropertyNames());

                            if (loadCasesUnique.Count() != properties.Count())
                                throw new ArgumentException();

                            interlayerLoadCasePropertyNameMap[i] = new Dictionary<LoadCase, string>();
                            for (var j = 0; j < properties.Count; j++)
                            {
                                if (_femModel.AddProperty(properties[j])) // Per ogni layer creo le proprietà dentro al fem
                                {
                                    interlayerLoadCasePropertyNameMap[i][loadCasesUnique[j]] = properties[j].Name;
                                }
                                else
                                    throw new ArgumentException(); // In teoria non è possibile che vada in eccezione perchè i nomi delle proprietà sono uniche e quindi vengono sempre aggiunti
                            }


                            var minProperty = properties.OrderBy(j => j.GetShearModule()).FirstOrDefault(); // Prendo la proprietà con i G minimo per ogni layer e la uso come proprietà iniziale

                            var indexes = _femModel.AddMesh(meshes[i], null, minProperty.Name, null, null, null, null);
                            elementIndexes[i] = indexes;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }


                    #endregion

                    // TIPO DI ANALISI

                    if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalysis)
                    {
                        // LINEARE
                        // Stage lineari per cambiare proprietà all'interlayer
                        // va creato uno stage per ogni loadcase
                        _femModel.AnalysisType = Model.FEM.FemModel.AnalysisTypes.Linear;

                        // O(nlc * n^2)
                        foreach (var loadCase in loads.Select(i => i.LoadCase as GPC.Checker.Glasses.LoadCases.LoadCase).Where(i => i != null).Distinct()) // ciclo su loadcase unici
                        {   
                            Stage stagelc = _femModel.AddStage(loadCase.Name, Model.FEM.FemModel.AnalysisTypes.Linear);

                            for (int i = 0; i < glassPackage.Length; i++)
                            {
                                if (glassPackage[i] is Interlayer)
                                    stagelc.AddFiniteElements(elementIndexes[i], interlayerLoadCasePropertyNameMap[i][loadCase]);

                                else
                                    stagelc.AddFiniteElements(elementIndexes[i], glassLayerPropertyNameMap[i]);
                            }
                        }
                        // TODO: fare combo fittizzie
                    }
                    else if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.NonLinearStaticAnalysis)
                    {
                        // NON LINEARE
                        // Stage lineari per cambiare proprietà all'interlayer
                        // va creato uno stage per ogni loadcase

                        _femModel.AnalysisType = Model.FEM.FemModel.AnalysisTypes.NonLinear;
                        
                        foreach(var combo in _combinations)
                        {

                            var longTermLoadCases = combo.GetLongTermLoadCases((glassPackage[1] as Interlayer).Material, 100);
                            

                        }

                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    // COMBINAZIONI
                    _femModel.AddCombinations(_combinations);


                }
                else
                    throw new NotImplementedException(_glassSurface.Prototype.LaminatedAnalysisType.ToString());

                return true;
            }
            else if (_glassSurface.Prototype.Glass is DoubleInsulatingGlass dgu)
            {
                glassWrapper = new DoubleInsulatingGlassWrapper(_glassSurface, dgu);
                return false;
            }
            else if (_glassSurface.Prototype.Glass is TripleInsulatingGlass tgu)
            {
                glassWrapper = new TripleInsulatingGlassWrapper(_glassSurface, tgu);
                return false;
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
                // STRAUS7

                GlassWrapper glassWrapper;
                if (_glassSurface.Prototype.Glass is MonolithicGlass mg)
                {
                    _femModel.SaveFemModelToSt7(_folderPath);

                    _femModel.RunSt7Solver();

                    _femModel.ReadSt7Result();

                    //_femModel.ReadSt7LinearCombinationResults();

                }
                else if (_glassSurface.Prototype.Glass is LaminatedGlass lg)
                {
                    _femModel.SaveFemModelToSt7(_folderPath);

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


        #region Private Methods

        /// <param name="loadCases"></param>
        /// <param name="material"></param>
        /// <param name="femModelPropertiesNames">A list of property names already inside the femModel</param>
        /// <returns>A list of <see cref="InterlayerBrickProperty"/> with a name that indentifies uniquely the property </returns>
        /// <remarks>This method generate a <see cref="InterlayerBrickProperty"/> for each loadcase in <paramref name="loadCases"/></remarks>
        private List<InterlayerBrickProperty> GetInterlayerBrickProperties(IEnumerable<LoadCase> loadCases, InterlayerMaterial material, List<string> femModelPropertiesNames)
        {
            int index = 0;
            int previousPropertyCount = femModelPropertiesNames.Count;

            List<InterlayerBrickProperty> properties = new List<InterlayerBrickProperty>();

            foreach (var loadCase in loadCases)
            {
                // Il nome è la chiave della collection. Do un nome che indentifica univocamente la proprietà
                properties.Add( new InterlayerBrickProperty(material, loadCase.Temperature, loadCase.LoadDuration, 
                                $"Interlayer_{previousPropertyCount + index} - Material: {material.Name}_{index++} G: {material.GetShearModule(loadCase.LoadDuration, loadCase.Temperature).ToString("F3")} MPa"));
                                

            }

            return properties;
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
                                                   out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMap, 
                                                   out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMap,
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
