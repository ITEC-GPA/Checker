using GPC.Checkers.Glasses.Extensions;
using GPC.Checkers.Glasses.FemModel;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.FEM;
using GPC.Model.FEM.Properties;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using GPC.Model.Materials;
using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public FemModelWrapper FemModel => _femModel;

        public Checker(GlassSurface glassSurface, List<Combination> combinations, ModelOptions modelOptions)
        {
            _glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));

            // Creo lista combinazioni sommando la lista delle globali a quelli del prototipo
            _combinations = combinations ?? new List<Combination>();
            _combinations.AddRange(glassSurface.Prototype.Combinations);

            _options = modelOptions ?? throw new ArgumentNullException(nameof(modelOptions));
        }

        public abstract override string GetCheckerName();


        #region Public method

        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        }


        /// <summary>
        /// Set up the FemModel class.
        /// </summary>
#if DEBUG
        public bool FemModelSetup(string folderPath, string femModelSuffix = "")
#else
        public bool FemModelSetup(string folderPath)
#endif
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

#if DEBUG
            string femModelName = $"{femModelSuffix}_Glass{_glassSurface.Id}_femModel";
#else
            string femModelName = $"Glass{_glassSurface.Id}_femModel";
#endif

            if (_glassSurface.Prototype.Glass is MonolithicGlass mg)
            {
                MonolithicGlassWrapper wrapper = new MonolithicGlassWrapper(_glassSurface, mg);

                _femModel = BuildMonolithicGlass(femModelName, wrapper, mg, _glassSurface.GetLoads(), _glassSurface.Prototype.AnalysisType, _combinations);

                return true;
            }
            else if (_glassSurface.Prototype.Glass is LaminatedGlass lg)
            {
                LaminatedGlassWrapper wrapper = new LaminatedGlassWrapper(_glassSurface, lg);

                _femModel = BuildLaminatedGlass(femModelName, wrapper, lg, _glassSurface.GetLoads(), _glassSurface.Prototype.AnalysisType, _glassSurface.Prototype.LaminatedAnalysisType, _combinations);

                return true;
            }
            else if (_glassSurface.Prototype.Glass is DoubleInsulatingGlass dgu)
            {
                // deve:
                // 1) calcolare la ripartizione 
                // 2) calcolare le due lastre di vetro

                DoubleInsulatingGlassWrapper wrapper = new DoubleInsulatingGlassWrapper(_glassSurface, dgu);

                // 1) Calcolo ripartizione 
                // tre metodi:
                // 1.a) Metodo normativo
                // 1.b) Metodo BAM
                // 1.c) Metodo numerico
                // la ripartizione dipende dal tipo di carico e dalla geometria della lastra


                if (wrapper.InnerGlassPanelWrapper is MonolithicGlassWrapper mgw)
                {
                    MonolithicGlass glass = (MonolithicGlass)dgu.GlassPanelInner;

                    //FemModelWrapper femModel = BuildMonolithicGlass($"{femModelSuffix}_Glass{_glassSurface.Id}_femModel",
                    //                                                 wrapper.InnerGlassPanelWrapper,
                    //                                                 glass, 
                    //                                        )
                }



                return false;
            }
            else if (_glassSurface.Prototype.Glass is TripleInsulatingGlass tgu)
            {
                var glassWrapper = new TripleInsulatingGlassWrapper(_glassSurface, tgu);
                return false;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        

        private FemModelWrapper BuildMonolithicGlass(string femModelName, MonolithicGlassWrapper wrapper, MonolithicGlass mg, IEnumerable<Load> loads, 
                                                                                                          Prototype.AnalysisTypes analysisType,
                                                                                                          IEnumerable<Combination> combinations)
        {

            // Creo modello
            FemModelWrapper femModel = new FemModelWrapper(femModelName);
            

            //MonolithicGlassWrapper wrapper = new MonolithicGlassWrapper(_glassSurface, mg);
            
            wrapper.AddExternalFaceLoads(loads);

            if (loads.OfType<SelfWeightLoad>().Count() > 0)
                wrapper.AddSelfWeightLoad(loads.OfType<SelfWeightLoad>().SingleOrDefault());

            // GEOMETRIA
            Mesh[] meshes = wrapper.Meshes;

            if (meshes.Length > 1)
                throw new NotSupportedException();


            string groupName = $"GlassLayer0";
            femModel.AddGroup(groupName);

            MonolithicGlassProperty pp = new MonolithicGlassProperty(mg, "mg");

            Mesh meshExternal = wrapper.GetExternalGlassMesh();

            GetLoadTypeVerticesDictionary(wrapper.MeshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                          wrapper.MeshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                          out Dictionary<IPointLoad, int[]> vertexLoadMeshEntityMapExternal,
                                          out Dictionary<ILineLoad, int[]> vertexLineLoadMeshEntityMapExternal,
                                          out Dictionary<IAreaLoad, int[]> faceAreaLoadMeshEntityMapExternal);

            femModel.AddProperty(pp);
            femModel.AddMesh(meshes.First(), pp.Name, null, vertexLoadMeshEntityMapExternal, vertexLineLoadMeshEntityMapExternal,
                                                            faceAreaLoadMeshEntityMapExternal,
                                                            wrapper.MeshGeometryRestrainVertices.Where(i => i.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                                            groupName); // Aggiunge i loadcase alla lista dei loadcase

            if (wrapper.ConsiderSelfWeight)
            {
                femModel.AddLoadCase(wrapper.SelfWeightLoad.LoadCase);
                ModelGravitySetUp(femModel, wrapper.SelfWeightLoad); // Aggiungo il modelAccelerationAttribute e creo il loadcase sw se non esiste
            }

            // TIPO DI ANALISI
            if (analysisType == Prototype.AnalysisTypes.LinearStaticAnalysis)
            {
                // COMBINAZIONI LINEARI - NO STAGE
                // STRAUS: LINEAR LOAD COMBINATION TABLE

                femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)analysisType;
                femModel.AddCombinations(combinations);
            }
            else if (analysisType == Prototype.AnalysisTypes.NonLinearStaticAnalysis)
            {
                // COMBINAZIONI NON LINEARI - NO STAGE
                // STRAUS: MONOSTAGE, INCREMENTI COME COMBINAZIONI NON LINEARI

                femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)analysisType;
                femModel.AddCombinations(combinations);

                Stage stage = femModel.AddStageAsCopyOfModel("Stage1", femModel.AnalysisType);
                stage.AddCombinations(combinations);
            }
            else
            {
                throw new NotImplementedException();
            }

            return femModel;
        }


        private FemModelWrapper BuildLaminatedGlass(string femModelName, LaminatedGlassWrapper wrapper, 
                                                    LaminatedGlass lg, IEnumerable<Load> loads, Prototype.AnalysisTypes analysisType, 
                                                    Prototype.LaminatedAnalysisTypes laminatedAnalysisType, IEnumerable<Combination> combinations)
        {


            // Creo modello
            FemModelWrapper femModel = new FemModelWrapper(femModelName);

            if (laminatedAnalysisType == Prototype.LaminatedAnalysisTypes.MultiElement)
            {

                if (loads.Count() == 0)
                    return null;

                wrapper.AddInternalFaceLoads(loads);

                if (loads.OfType<SelfWeightLoad>().Count() > 0)
                    wrapper.AddSelfWeightLoad(loads.OfType<SelfWeightLoad>().SingleOrDefault());

                // GEOMETRIA

                #region Geometria

                // Prendo mesh
                Mesh[] meshes = wrapper.Meshes; // varie mesh, plate e brick una per ogni layer

                Mesh meshExternal = wrapper.GetExternalGlassMesh();
                Mesh meshInternal = wrapper.GetInternalGlassMesh();


                // Associo carichi a indici elementi
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


                List<IGlassLoadCase> loadCasesUnique = loads.Select(i => i.LoadCase as IGlassLoadCase).Where(i => i != null).Distinct().ToList();


                IGlassPackage[] glassPackage = lg.GetGlassPackage();


                // Associazione fra l'indice del layer e l'indice degli elementi plate volumi e nodi
                (int[] nodesId, int[] platesId, int[] volumesId)[] elementIndexes = new (int[] nodesId, int[] platesId, int[] volumesId)[glassPackage.Count()];

                Dictionary<int, string> glassLayerPropertyNameMap = new Dictionary<int, string>();

                // Map between interlayerIndex -> loadcase e nome della proprietà associata a quel loadcase
                Dictionary<int, Dictionary<IGlassLoadCase, string>> interlayerLoadCasePropertyNameMap = new Dictionary<int, Dictionary<IGlassLoadCase, string>>();

                // Aggiunta delle mesh al femModel
                Dictionary<int, int>[] packageNodesNewIndexMap = new Dictionary<int, int>[glassPackage.Count()];
                for (int i = 0; i < glassPackage.Length; i++)
                {
                    IGlassPackage layer = glassPackage[i];
                    if (layer is MonolithicGlass glass)
                    {
                        string propertyName = $"Mg {i} t={glass.Thickness}";
                        string groupName = $"GlassLayer{i}";

                        femModel.AddGroup(groupName);

                        femModel.AddProperty(new MonolithicGlassProperty(glass, propertyName));

                        glassLayerPropertyNameMap[i] = propertyName;

                        if (meshes[i].CompareGuid(meshExternal.Guid))
                        {
                            femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapExternal, vertexLineLoadMeshEntityMapExternal, faceAreaLoadMeshEntityMapExternal,
                                              wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshExternal.Guid)).FirstOrDefault().Value,
                                              out Dictionary<int, int> nodesNewIndexMap,
                                              out Dictionary<int, int> platesNewIndexMap,
                                              out Dictionary<int, int> brickNewIndexMap,
                                              groupName);

                            packageNodesNewIndexMap[i] = nodesNewIndexMap;
                            elementIndexes[i].platesId = platesNewIndexMap.Values.ToArray();
                        }
                        else if (meshes[i].CompareGuid(meshInternal.Guid))
                        {
                            femModel.AddMesh(meshes[i], propertyName, null, vertexLoadMeshEntityMapInternal, vertexLineLoadMeshEntityMapInternal, faceAreaLoadMeshEntityMapInternal,
                                                                        wrapper.MeshGeometryRestrainVertices.Where(j => j.Key.CompareGuid(meshInternal.Guid)).FirstOrDefault().Value,
                                                                        out Dictionary<int, int> nodesNewIndexMap,
                                                                        out Dictionary<int, int> platesNewIndexMap,
                                                                        out Dictionary<int, int> brickNewIndexMap,
                                                                        groupName);

                            packageNodesNewIndexMap[i] = nodesNewIndexMap;
                            elementIndexes[i].platesId = platesNewIndexMap.Values.ToArray();
                        }
                        else
                        {
                            femModel.AddMesh(meshes[i], propertyName, null, null, null, null, null,
                                                                        out Dictionary<int, int> nodesNewIndexMap,
                                                                        out Dictionary<int, int> platesNewIndexMap,
                                                                        out Dictionary<int, int> brickNewIndexMap,
                                                                        groupName);

                            packageNodesNewIndexMap[i] = nodesNewIndexMap;
                            elementIndexes[i].platesId = platesNewIndexMap.Values.ToArray();
                        }
                    }
                    else if (layer is Interlayer il)
                    {
                        string groupName = $"Interlayer{i}";

                        femModel.AddGroup(groupName);

                        List<InterlayerBrickProperty> properties = GetInterlayerBrickProperties(loadCasesUnique, il.Material, femModel.GetBrickPropertyNames());

                        if (loadCasesUnique.Count() != properties.Count())
                            throw new ArgumentException();

                        interlayerLoadCasePropertyNameMap[i] = new Dictionary<IGlassLoadCase, string>();
                        for (var j = 0; j < properties.Count; j++)
                        {
                            if (femModel.AddProperty(properties[j])) // Per ogni layer creo le proprietà dentro al fem
                            {
                                interlayerLoadCasePropertyNameMap[i][loadCasesUnique[j]] = properties[j].Name;
                            }
                            else
                                throw new ArgumentException(); // In teoria non è possibile che vada in eccezione perchè i nomi delle proprietà sono uniche e quindi vengono sempre aggiunti
                        }

                        var minProperty = properties.OrderBy(j => ((Model.FEM.Materials.OrthotropicFemMaterial)j.Material).G12).FirstOrDefault(); // Prendo la proprietà con i G minimo per ogni layer e la uso come proprietà iniziale

                        var indexes = femModel.AddMesh(meshes[i], null, minProperty.Name, null, null, null, null,
                                                                        out Dictionary<int, int> nodesNewIndexMap,
                                                                        out Dictionary<int, int> platesNewIndexMap,
                                                                        out Dictionary<int, int> brickNewIndexMap,
                                                                        groupName);

                        packageNodesNewIndexMap[i] = nodesNewIndexMap;
                        elementIndexes[i].volumesId = brickNewIndexMap.Values.ToArray(); // TODO SISTEMARE
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }

                }

                for (int i = 0; i < glassPackage.Length; i++)
                {
                    // Generazione links
                    if (i == 0)
                    {
                        femModel.GenerateRigidLinks(wrapper.GetLayerUpperLowerVerticesIds(i).lowerVertices.Select(k => packageNodesNewIndexMap[i][k]),
                                                    wrapper.GetLayerUpperLowerVerticesIds(i + 1).lowerVertices.Select(k => packageNodesNewIndexMap[i + 1][k]));
                    }
                    else if (i % 2 == 0 && i < glassPackage.Length - 1)
                    {
                        femModel.GenerateRigidLinks(wrapper.GetLayerUpperLowerVerticesIds(i - 1).upperVertices.Select(k => packageNodesNewIndexMap[i - 1][k]),
                                                    wrapper.GetLayerUpperLowerVerticesIds(i).lowerVertices.Select(k => packageNodesNewIndexMap[i][k]));

                        femModel.GenerateRigidLinks(wrapper.GetLayerUpperLowerVerticesIds(i).upperVertices.Select(k => packageNodesNewIndexMap[i][k]),
                                                    wrapper.GetLayerUpperLowerVerticesIds(i + 1).lowerVertices.Select(k => packageNodesNewIndexMap[i + 1][k]));
                    }
                    else if (i == glassPackage.Length - 1)
                    {
                        femModel.GenerateRigidLinks(wrapper.GetLayerUpperLowerVerticesIds(i - 1).upperVertices.Select(k => packageNodesNewIndexMap[i - 1][k]),
                                                    wrapper.GetLayerUpperLowerVerticesIds(i).lowerVertices.Select(k => packageNodesNewIndexMap[i][k]));
                    }
                }


                #endregion Geometria

                if (wrapper.ConsiderSelfWeight)
                {
                    femModel.AddLoadCase(wrapper.SelfWeightLoad.LoadCase);
                    ModelGravitySetUp(femModel, wrapper.SelfWeightLoad); // Aggiungo il modelAccelerationAttribute e creo il loadcase sw se non esiste
                }

                // TIPO DI ANALISI

                if (analysisType == Prototype.AnalysisTypes.LinearStaticAnalysis)
                {
                    // LINEARE
                    // Stage lineari per cambiare proprietà all'interlayer
                    // va creato uno stage per ogni loadcase
                    femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)analysisType;

                    // O(nlc * n^2)
                    // Ciclo i loadcase unici

                    Dictionary<Combination, (List<int> stageId, List<Combination> comboFictituous)> comboStageIdMap = new Dictionary<Combination, (List<int>, List<Combination>)>();

                    foreach (var loadCase in combinations.SelectMany(i => i.GetLoadCases()).Select(i => i as MMLoadCaseBase).Where(i => i != null).Distinct()) // ciclo su loadcase unici
                    {
                        Stage stagelc = femModel.AddStage(loadCase.Name, (Model.FEM.FemModel.AnalysisTypes)analysisType);

                        // aggiunge gli elementi allo stage e cambia le proprietà dell'interlayer
                        for (int i = 0; i < glassPackage.Length; i++)
                        {
                            if (glassPackage[i] is Interlayer)
                            {
                                stagelc.AddFiniteElements(elementIndexes[i].volumesId, interlayerLoadCasePropertyNameMap[i][(IGlassLoadCase)loadCase]);
                            }
                            else
                                stagelc.AddFiniteElements(elementIndexes[i].platesId, glassLayerPropertyNameMap[i]);
                        }

                        // cerco tutti i coefficienti associati al loadcase che sto guardando fra tutte le combinazioni
                        foreach (Combination combo in combinations.Select(i => i).Where(i => i.GetLoadCaseCoefficient(loadCase) != 0).ToList())
                        {
                            if (!comboStageIdMap.ContainsKey(combo))
                            {
                                comboStageIdMap[combo] = (new List<int>(), new List<Combination>());
                            }

                            Combination comboFict = new Combination($"{loadCase.Name} {combo[loadCase]}");
                            comboFict.AddLoadCaseCoefficient(loadCase, combo[loadCase]);

                            comboStageIdMap[combo].stageId.Add(stagelc.Id);
                            comboStageIdMap[combo].comboFictituous.Add(comboFict);

                            stagelc.AddCombination(comboFict);

                            femModel.AddStageCombinationSplittedMap(combo.Name, new int[] { stagelc.Id }, new string[] { comboFict.Name });
                        }

                    }

                }
                else if (analysisType == Prototype.AnalysisTypes.NonLinearStaticAnalysis)
                {
                    // NON LINEARE
                    // Stage lineari per cambiare proprietà all'interlayer
                    // va creato uno stage per ogni loadcase

                    femModel.AnalysisType = (Model.FEM.FemModel.AnalysisTypes)analysisType;

                    List<Combination> combinationsToProcess = combinations.ToList();

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

                        // filtro la lista dei loadcase LT togliendo i loadcase che non esistono nel modello
                        IEnumerable<IGlassLoadCase> longTermLoadCasesFiltered = longTermLoadCases.Intersect(femModel.GetLoadCases().Cast<IGlassLoadCase>().ToList());
                        IEnumerable<IGlassLoadCase> missingLoadCases = longTermLoadCases.Except(femModel.GetLoadCases().Cast<IGlassLoadCase>().ToList());

                        if (longTermLoadCasesFiltered.Count() > 0)
                        {
                            var longTermLoadCaseCoefficients = firstCombo.GetLoadCaseCoefficientsTuple(longTermLoadCasesFiltered).Select(i => ((MMLoadCaseBase loadCase, double coefficient))i).ToArray();

                            List<Combination> matchedCombinations = combinationsToProcess.Where(i => i.ContainsLoadCaseCoefficients(longTermLoadCaseCoefficients)).ToList(); // contiene la prima combo

                            Stage stage1 = femModel.AddStage($"Stage {index++} LT", Model.FEM.FemModel.AnalysisTypes.Linear);

                            var ltCombination = (Combination)firstCombo.CloneEmpty();
                            ltCombination.AddLoadCaseCoefficients(longTermLoadCaseCoefficients);

                            stage1.AddCombination(ltCombination);

                            var stCombinations = new List<Combination>();
                            foreach (var combo in matchedCombinations)
                            {
                                var c = (Combination)combo.Clone();
                                c.RemoveLoadCaseCoefficients(firstCombo.GetLoadCaseCoefficientsTuple(missingLoadCases).Select(i => ((MMLoadCaseBase loadCase, double coefficient))i).ToArray());
                                c.RemoveLoadCaseCoefficients(longTermLoadCaseCoefficients);

                                if (c.LoadCaseCount > 0) // TODO: cambiare in LoadCaseCount
                                {
                                    stCombinations.Add(c);
                                }
                            }

                            if (stCombinations.Count > 0)
                            {
                                // se combo ST è vuota, vuol dire che tutte i loadcase della combo in esame sono LT
                                // non creo stage 2.

                                Stage stage2 = femModel.AddStage($"Stage {index++} ST", Model.FEM.FemModel.AnalysisTypes.Linear, true);
                                stage2.AddCombinations(stCombinations);

                                var lcLTLowerG = longTermLoadCasesFiltered.GetLowerGvalueLoadCase(intMat);
                                var lcSTLowerG = stCombinations.SelectMany(i => i.GetIGlassLoadCase()).Distinct().GetLowerGvalueLoadCase(intMat);

                                for (int i = 0; i < glassPackage.Length; i++)
                                {
                                    if (glassPackage[i] is Interlayer)
                                    {
                                        stage1.AddFiniteElements(elementIndexes[i].volumesId, interlayerLoadCasePropertyNameMap[i][lcLTLowerG]);
                                        stage2.AddFiniteElements(elementIndexes[i].volumesId, interlayerLoadCasePropertyNameMap[i][lcSTLowerG]);
                                    }
                                    else
                                    {
                                        stage1.AddFiniteElements(elementIndexes[i].platesId, glassLayerPropertyNameMap[i]);
                                        stage2.AddFiniteElements(elementIndexes[i].platesId, glassLayerPropertyNameMap[i]);
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

                            Stage stage2 = femModel.AddStage($"Stage {index++} ST", Model.FEM.FemModel.AnalysisTypes.Linear, false);

                            var stCombinations = new List<Combination>();
                            foreach (var combo in matchedCombinations)
                            {
                                var clone = (Combination)combo.Clone();
                                clone.RemoveLoadCaseCoefficients(longTermLoadCaseCoefficients);
                                if (clone.LoadCaseCount > 0)
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
                                    stage2.AddFiniteElements(elementIndexes[i].volumesId, interlayerLoadCasePropertyNameMap[i][lcSTLowerG]);
                                }
                                else
                                {
                                    stage2.AddFiniteElements(elementIndexes[i].volumesId, glassLayerPropertyNameMap[i]);
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
                // uso le combo a livello di modello per salvare le combo di riferimento da girare.
                // negli stage ci sono o quelle complete o quelle splittate da ricostruire
                femModel.AddCombinations(combinations);
            }
            else
                throw new NotImplementedException(laminatedAnalysisType.ToString());

            return femModel;
        }






#if !DEBUG
        /// <remarks> <see cref="FemModelSetup(string)"/> must be called before calling this method</remarks>
#endif
        /// <summary>
        /// Run the femModel solver and fill the results arrays
        /// </summary>
        public void PerformCheck()
        {
            if (_femModel == null)
                throw new ApplicationException($"FemModel is null. {nameof(FemModelSetup)} should be called before calling this method");

            if (_glassSurface.Prototype.SolverType == Prototype.SolverTypes.Straus7)
            {
                // STRAUS7
                GlassWrapper glassWrapper;
                if (_glassSurface.Prototype.Glass is MonolithicGlass mg)
                {
                    _femModel.SaveFemModelToSt7(_folderPath); // Esporta modello in st7
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

            _femModel.Solve(); // Lancia solver e legge risultati
        }


#if (!DEBUG)
        /// <summary>
        /// The mesh is generated by calling <see cref="FemModelSetup(string)"/>
        /// </summary>
        /// <returns>Null if FemModel is not available</returns>
#endif
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
                var mat = material.GetOrthotropicFemMaterial(loadCase.LoadDuration, loadCase.Temperature,
                                                            ModelAnalysisOptions.Instance.InterlayerBrickElasticModulus,
                                                            ModelAnalysisOptions.Instance.InterlayerBrickElasticModulus,
                                                            ModelAnalysisOptions.Instance.InterlayerBrickElasticModulus,
                                                            FemOptions.Instance.InterlayerPoissonValue,
                                                            FemOptions.Instance.InterlayerPoissonValue,
                                                            FemOptions.Instance.InterlayerPoissonValue);

                properties.Add(new InterlayerBrickProperty(mat, loadCase.Temperature, loadCase.LoadDuration,
                                $"Interlayer_{previousPropertyCount + index} - Material: {material.Name}_{index++}, G12: {mat.G12:F3} MPa, G23: {mat.G23:F3} MPa, G31: {mat.G31:F3} MPa"));
            }

            return properties;
        }

        /// <summary>
        /// Add a <see cref="Model.FEM.Attributes.ModelAccelerationAttribute"/> for each loadcase of type <see cref="Model.LoadCases.LoadCase.LoadCaseTypes.SelfWeight"/> in the Combinations list
        /// </summary>
        /// <param name="femModel"></param>
        /// <param name="load"></param>
        /// <inheritdoc cref="Model.FEM.FemModel.AddModelAcceleration(string)"/>
        private void ModelGravitySetUp(FemModelWrapper femModel, SelfWeightLoad load)
        {
            var accelerationModel = femModel.AddModelAcceleration(load.LoadCase.Name);

            accelerationModel.CoordinateSystem = Geometry.CoordinateSystem.Global;

            int gravityDirection = Math.Sign(load.GravityVector * Geometry.CoordinateSystem.Global.V1);

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



        #endregion

        #region Protected methods

        //protected abstract void GetDGUUniformPressureLoadSharing();


        //protected abstract void GetDGUUniformPressureLoadSharing();

        #endregion

        #region Results


        /// <inheritdoc cref="Model.FEM.FemModel.GetCombinationNodeDisplacementResults(Combination, string)"/>
        internal IEnumerable<NodeResult> GetCombinationNodeDisplacementResult(Combination combination, string combinationName = "")
        {
            return FemModel.GetCombinationNodeDisplacementResults(combination, combinationName);
        }


        /// <inheritdoc cref="Model.FEM.FemModel.GetCombinationElementStressResults(Combination, string)"/>
        internal IEnumerable<FiniteElementResult> GetCombinationPlateStressResult(Combination combination, string combinationName = "")
        {
            return FemModel.GetCombinationElementStressResults(combination, combinationName);
        }


        public void GetWorkinRatio()
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}