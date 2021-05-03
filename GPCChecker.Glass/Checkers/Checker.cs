using System;
using System.IO;
using System.Collections.Generic;
using GPC.Checkers.Glasses.FemModel;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Checkers.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Checkers.Glasses.Results;
using GPC.Model.FEM.Properties;
using GPC.Model.Loads;
using System.Linq;
using GPC.Model.Results;
using GPC.Model.FEM;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Combinations;
using GPC.Checkers.Glasses.Extensions;
using GPC.Checkers.Glasses.Models;
using MMLoadCaseBase = GPC.Model.LoadCases.LoadCaseBase;

namespace GPC.Checkers.Glasses.Checkers
{
    public abstract class Checker : Common.Checker
    {
        protected GlassSurface _glassSurface;
        protected FemModelWrapper _femModel;
        protected string _folderPath;
        protected ModelOptions _options;

        /// <summary>
        /// List of global and specific combinations
        /// </summary>
        protected List<Combination> _combinations;

        public Model.FEM.FemModel FemModel => _femModel;


        public Checker(GlassSurface glassSurface, List<Combination> combinations, ModelOptions modelOptions)
        {
            _glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));

            // Creo lista combinazioni sommando la lista delle globali a quelli del prototipo
            _combinations = combinations ?? new List<Combination>();
            _combinations.AddRange(glassSurface.Prototype.Combinations);

            _options = modelOptions;
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


                if (_glassSurface.GetLoads().OfType<SelfWeightLoad> ().Count() > 0)
                    wrapper.AddSelfWeightLoad(_glassSurface.GetLoads().OfType<SelfWeightLoad>().SingleOrDefault());


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
                                  wrapper.MeshGeometryRestrainVertices.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value); // Aggiunge i loadcase alla lista dei loadcase

                if (wrapper.ConsiderSelfWeight)
                {
                    _femModel.AddLoadCase(wrapper.SelfWeightLoad.LoadCase);
                    ModelGravitySetUp(_femModel, wrapper.SelfWeightLoad); // Aggiungo il modelAccelerationAttribute e creo il loadcase sw se non esiste
                }


                // TIPO DI ANALISI
                if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.LinearStaticAnalysis)
                {
                    // COMBINAZIONI LINEARI - NO STAGE
                    // STRAUS: LINEAR LOAD COMBINATION TABLE
                    _femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)_glassSurface.Prototype.AnalysisType;
                    _femModel.AddCombinations(_combinations);
                }
                else if (_glassSurface.Prototype.AnalysisType == Models.Prototype.AnalysisTypes.NonLinearStaticAnalysis)
                {
                    // COMBINAZIONI NON LINEARI - NO STAGE
                    // STRAUS: MONOSTAGE, INCREMENTI COME COMBINAZIONI NON LINEARI

                    _femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)_glassSurface.Prototype.AnalysisType;
                    _femModel.AddCombinations(_combinations);
                    
                    Stage stage = _femModel.AddStageAsCopyOfModel("Stage1", _femModel.AnalysisType);
                    stage.AddCombinations(_combinations);

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

                    if (_glassSurface.GetLoads().OfType<SelfWeightLoad>().Count() > 0)
                        wrapper.AddSelfWeightLoad(_glassSurface.GetLoads().OfType<SelfWeightLoad>().SingleOrDefault());

                    // GEOMETRIA

                    #region Geometria

                    List<Mesh> meshes = wrapper.Meshes; // varie mesh, plate e brick una per ogni layer

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

                    List<IGlassLoadCase> loadCasesUnique = loads.Select(i => i.LoadCase as IGlassLoadCase).Where(i => i != null).Distinct().ToList();

                    var glassPackage = lg.GetGlassPackage();

                    int[][] elementIndexes = new int[glassPackage.Count()][]; // Associazione fra l'indice del layer e l'indice degli elementi

                    Dictionary<int, string> glassLayerPropertyNameMap = new Dictionary<int, string>();
                    
                    // Map between interlayerIndex -> loadcase e nome della proprietà associata a quel loadcase
                    Dictionary<int, Dictionary<IGlassLoadCase, string>> interlayerLoadCasePropertyNameMap = new Dictionary<int, Dictionary<IGlassLoadCase, string>>();

                    for (int i = 0; i < glassPackage.Length; i++)
                    {
                        IGlassPackage layer = glassPackage[i];
                        if (layer is MonolithicGlass glass)
                        {
                            string propertyName = $"Mg {i} t={glass.Thickness}";
                            _femModel.AddProperty(new MonolithicGlassProperty(glass, propertyName));

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
                            List<InterlayerBrickProperty> properties = GetInterlayerBrickProperties(loadCasesUnique, il.Material, _femModel.GetBrickPropertyNames());

                            if (loadCasesUnique.Count() != properties.Count())
                                throw new ArgumentException();

                            interlayerLoadCasePropertyNameMap[i] = new Dictionary<IGlassLoadCase, string>();
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

                    if (wrapper.ConsiderSelfWeight)
                    {
                        _femModel.AddLoadCase(wrapper.SelfWeightLoad.LoadCase);
                        ModelGravitySetUp(_femModel, wrapper.SelfWeightLoad); // Aggiungo il modelAccelerationAttribute e creo il loadcase sw se non esiste
                    }


                    // TIPO DI ANALISI

                    if (_glassSurface.Prototype.AnalysisType == Prototype.AnalysisTypes.LinearStaticAnalysis)
                    {
                        // LINEARE
                        // Stage lineari per cambiare proprietà all'interlayer
                        // va creato uno stage per ogni loadcase
                        _femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)_glassSurface.Prototype.AnalysisType;

                        // O(nlc * n^2)
                        Dictionary<Combination, List<int>> comboStageIdMap = new Dictionary<Combination, List<int>>();
                        foreach (var loadCase in _combinations.SelectMany(i => i.GetLoadCases()).Select(i => i as MMLoadCaseBase).Where(i => i != null).Distinct()) // ciclo su loadcase unici
                        {   
                            Stage stagelc = _femModel.AddStage(loadCase.Name, (Model.FEM.FemModel.AnalysisTypes)_glassSurface.Prototype.AnalysisType);

                            for (int i = 0; i < glassPackage.Length; i++)
                            {
                                if (glassPackage[i] is Interlayer)
                                {
                                    stagelc.AddFiniteElements(elementIndexes[i], interlayerLoadCasePropertyNameMap[i][(IGlassLoadCase)loadCase]);
                                }
                                else
                                    stagelc.AddFiniteElements(elementIndexes[i], glassLayerPropertyNameMap[i]);
                            }

                            foreach (Combination combo in _combinations.Select(i => i).Where(i => i.GetLoadCaseCoefficient(loadCase) != 0).ToList())
                            {
                                if (!comboStageIdMap.ContainsKey(combo))
                                    comboStageIdMap[combo] = new List<int>();
                                comboStageIdMap[combo].Add(stagelc.Id);

                                Combination comboFict;
                                comboFict = new Combination($"{loadCase.Name} {combo[loadCase]}");
                                comboFict.AddLoadCaseCoefficient(loadCase, combo[loadCase]);

                                stagelc.AddCombination(comboFict);
                            }

                        }

                    }
                    else if (_glassSurface.Prototype.AnalysisType == Prototype.AnalysisTypes.NonLinearStaticAnalysis)
                    {
                        // NON LINEARE
                        // Stage lineari per cambiare proprietà all'interlayer
                        // va creato uno stage per ogni loadcase

                        _femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)_glassSurface.Prototype.AnalysisType;

                        List<Combination> combinationsToProcess = _combinations.ToList();

                        int index = 0;
                        while (combinationsToProcess.Count > 0)
                        {
                            // algoritmo che crea stages LT e ST
                            // 1) prendo la prima combo, divido i suoi loadcase in LT e ST
                            // 1.a) per dividere la combo (1) devo avere un materiale di riferimento e un valore di G soglia
                            // 1.b) il materiale di riferimento può essere diverso all'interno dello stesso pacchetto di vetro
                            // 1.c) faccio un ciclo su tutti i materiali, per ognuno cerco i loadcase LT della prima combo e sommo il risultato.
                            // 2) cerco le altre combo con stessi LT e stessi coefficienti
                            // 3) creo uno stage LT e ci aggiungo una combo fatta solo dai loadcase LT e loro coefficienti
                            // 4) creo uno stage ST con morph e ci aggiungo tutte le combo del punto (2) ma solo la parte ST
                            // 5) imposto che la proprietà dell'interlayer vari tra gli stage LT e ST prendendo sempre quella con il G minimo all'interno del gruppo.


                            Combination firstCombo = combinationsToProcess.First();
                            
                            InterlayerMaterial intMat = (glassPackage[1] as Interlayer).Material;

                            List<IGlassLoadCase> longTermLoadCases = new List<IGlassLoadCase>();
                            foreach (var layer in glassPackage)
                            {
                                if (layer is Interlayer i)
                                {
                                    longTermLoadCases.AddRange(firstCombo.GetIGlassLoadCase().GetLongTermLoadCases(i.Material, 70));
                                }
                            }
                            longTermLoadCases = longTermLoadCases.Distinct().ToList();

                            IEnumerable<IGlassLoadCase> longTermLoadCasesFiltered = longTermLoadCases.Intersect(_femModel.loadCases.Cast<IGlassLoadCase>().ToList()); // filtro la lista dei loadcase LT togliendo i loadcase che non esistono nel modello

                            if (longTermLoadCasesFiltered.Count() > 0)
                            {
                                //(IGlassLoadCase loadCase, double coefficient)[] longTermLoadCaseCoefficients = firstCombo.GetLoadCaseCoefficientsTuple(longTermLoadCasesFiltered);
                                var longTermLoadCaseCoefficients = firstCombo.GetLoadCaseCoefficientsTuple(longTermLoadCasesFiltered).Select(i => ((MMLoadCaseBase loadCase, double coefficient))i).ToArray();
                                 
                                List<Combination> matchedCombinations = combinationsToProcess.Where(i => i.ContainsLoadCaseCoefficients(longTermLoadCaseCoefficients)).ToList(); // contiene la prima combo

                                Stage stage1 = _femModel.AddStage($"Stage {index++} LT", Model.FEM.FemModel.AnalysisTypes.Linear);

                                var ltCombination = (Combination)firstCombo.CloneEmpty();
                                ltCombination.AddLoadCaseCoefficients(longTermLoadCaseCoefficients);

                                stage1.AddCombination(ltCombination);

                                var stCombinations = new List<Combination>();
                                foreach (var combo in matchedCombinations)
                                {
                                    var c = (Combination)combo.Clone();
                                    c.RemoveLoadCaseCoefficients(longTermLoadCaseCoefficients);

                                    if (c.GetLoadCases().Count() > 0) // TODO: cambiare in LoadCaseCount
                                    {
                                        stCombinations.Add(c);
                                    }
                                }

                                if (stCombinations.Count > 0)
                                {
                                    // se combo ST è vuota, vuol dire che tutte i loadcase della combo in esame sono LT
                                    // non creo stage 2.

                                    Stage stage2 = _femModel.AddStage($"Stage {index++} ST", Model.FEM.FemModel.AnalysisTypes.Linear, true);
                                    stage2.AddCombinations(stCombinations);

                                    var lcLTLowerG = longTermLoadCases.GetLowerGvalueLoadCase(intMat);
                                    var lcSTLowerG = stCombinations.SelectMany(i => i.GetIGlassLoadCase()).Distinct().GetLowerGvalueLoadCase(intMat);

                                    for (int i = 0; i < glassPackage.Length; i++)
                                    {
                                        if (glassPackage[i] is Interlayer)
                                        {
                                            stage1.AddFiniteElements(elementIndexes[i], interlayerLoadCasePropertyNameMap[i][lcLTLowerG]);
                                            stage2.AddFiniteElements(elementIndexes[i], interlayerLoadCasePropertyNameMap[i][lcSTLowerG]);
                                        }
                                        else
                                        {
                                            stage1.AddFiniteElements(elementIndexes[i], glassLayerPropertyNameMap[i]);
                                            stage2.AddFiniteElements(elementIndexes[i], glassLayerPropertyNameMap[i]);
                                        }
                                    }
                                }

                                combinationsToProcess = combinationsToProcess.Except(matchedCombinations).ToList();
                            }
                            else
                            {
                                // In questo caso longTermLoadCasesFiltered è vuota
                                // vuol dire che i load case LT non ci sono nel modello
                                // si crea solo lo stage per i loadcase ST

                                var longTermLoadCaseCoefficients = firstCombo.GetLoadCaseCoefficientsTuple(longTermLoadCasesFiltered).Cast<(MMLoadCaseBase loadCase, double coefficient)>().ToArray();

                                List<Combination> matchedCombinations = combinationsToProcess.Where(i => i.ContainsLoadCaseCoefficients(longTermLoadCaseCoefficients)).ToList(); // contiene la prima combo

                                Stage stage2 = _femModel.AddStage($"Stage {index++} ST", Model.FEM.FemModel.AnalysisTypes.Linear, false);

                                var stCombinations = new List<Combination>();
                                foreach (var combo in matchedCombinations)
                                {
                                    var clone = (Combination)combo.Clone();
                                    clone.RemoveLoadCaseCoefficients(longTermLoadCaseCoefficients);
                                    if (clone.GetLoadCases().Count() > 0) // TODO: cambiare in LoadCaseCount
                                    {
                                        stCombinations.Add(clone);
                                    }
                                }

                                stage2.AddCombinations(stCombinations);

                                combinationsToProcess = combinationsToProcess.Except(matchedCombinations).ToList();

                                var lcSTLowerG = stCombinations.SelectMany(i => i.GetLoadCases()).Distinct().Cast<LoadCase>().ToList().GetLowerGvalueLoadCase(intMat);

                                for (int i = 0; i < glassPackage.Length; i++)
                                {
                                    if (glassPackage[i] is Interlayer)
                                    {
                                        stage2.AddFiniteElements(elementIndexes[i], interlayerLoadCasePropertyNameMap[i][lcSTLowerG]);
                                    }
                                    else
                                    {
                                        stage2.AddFiniteElements(elementIndexes[i], glassLayerPropertyNameMap[i]);
                                    }
                                }
                            }

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
        private List<InterlayerBrickProperty> GetInterlayerBrickProperties(IEnumerable<IGlassLoadCase> loadCases, InterlayerMaterial material, List<string> femModelPropertiesNames)
        {
            int index = 0;
            int previousPropertyCount = femModelPropertiesNames.Count;

            List<InterlayerBrickProperty> properties = new List<InterlayerBrickProperty>();

            foreach (var loadCase in loadCases)
            {
                // Il nome è la chiave della collection. Do un nome che indentifica univocamente la proprietà
                properties.Add( new InterlayerBrickProperty(material, loadCase.Temperature, loadCase.LoadDuration, 
                                $"Interlayer_{previousPropertyCount + index} - Material: {material.Name}_{index++} G: {material.GetShearModule(loadCase.LoadDuration, loadCase.Temperature):F3} MPa"));
                                

            }

            return properties;
        }

        /// <summary>
        /// Add a <see cref="GPC.Model.FEM.Attributes.ModelAccelerationAttribute"/> for each loadcase of type <see cref="Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight"/> in the Combinations list
        /// </summary>
        /// <param name="femModel"></param>
        /// <param name="load"></param>
        /// <inheritdoc cref="GPC.Model.FEM.FemModel.AddModelAcceleration(string)"/>
        private void ModelGravitySetUp(FemModelWrapper femModel, SelfWeightLoad load)
        {

            var accelerationModel = femModel.AddModelAcceleration(load.LoadCase.Name);

            accelerationModel.CoordinateSystem = GPC.Geometry.CoordinateSystem.Global;

            int gravityDirection = Math.Sign(load.GravityVector * GPC.Geometry.CoordinateSystem.Global.V1);

            switch (_options.GravityAxis)
            {
                case ModelOptions.GravityAxes.X:
                    accelerationModel.A1 = gravityDirection * load.Acceleration;
                    break;
                case ModelOptions.GravityAxes.Y:
                    accelerationModel.A2 = gravityDirection * load.Acceleration;
                    break;                                    
                case ModelOptions.GravityAxes.Z:              
                    accelerationModel.A3 = gravityDirection * load.Acceleration;
                    break;
                default:
                    throw new ArgumentException();
            }


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
