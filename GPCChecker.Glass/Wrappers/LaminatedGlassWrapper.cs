
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checkers.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Model.Restrains;
using GPC.Model.Loads;
using GPC.Model.FEM.Properties;
using GPC.Model.FEM.Materials;
using System.Threading.Tasks;
using GPC.Model.Results;
using System.Collections.Concurrent;
using GPC.Model.Combinations;
using GPC.Checkers.Glasses.Loads;
using GPC.Checkers.Glasses.LoadCases;

namespace GPC.Checkers.Glasses.Wrappers
{
    public class LaminatedGlassWrapper : GlassPanelWrapper
    {
        protected const int INTERLAYER_DISCRETIZATION = 3;

        /// <summary>
        /// Thickness associates to displacement
        /// </summary>
        protected Dictionary<IGlassLoadCase, double> _thicknessesW;

        /// <summary>
        /// Thickness associates to stress for each glass layer 
        /// </summary>
        protected Dictionary<IGlassLoadCase, double>[] _thicknessesStress;
        

        /// <summary>
        /// List of upper and lower vertices Id of each volume layer
        /// </summary>
        protected (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices)[] _volumeUpperLowerVerticesIds;


        public Dictionary<IGlassLoadCase, double> ThicknessesW => _thicknessesW;

        public Dictionary<IGlassLoadCase, double>[] ThicknessesStress => _thicknessesStress;


        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass) 
            : this(glassSurface, glass, GlassPanelPositions.External)
        {                       

        }

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass, GlassPanelPositions position)
            : base(glassSurface, glass, position)
        {
            _thicknessesW = new Dictionary<IGlassLoadCase, double>();
            _thicknessesStress = new Dictionary<IGlassLoadCase, double>[glass.GlassLayerCount]; 

            _volumeUpperLowerVerticesIds = new (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices)[glass.GlassLayerCount + glass.InterlayerCount];
        }

        #endregion

        #region Geometry

        public override double GetDeformationThickness(IGlassLoad load)
        {
            if (_thicknessesW.Keys.Count == 0)
            {
                CalculateEquivalentThicknesses();
                if (_thicknessesW.Keys.Count == 0)
                    throw new ArgumentException();
            }

            var loadcase = load.GlassLoadCase;

            EquivalentThicknessParameters parameters = 
                new EquivalentThicknessParameters(loadcase.LoadDuration, loadcase.Temperature, load.GetGeometryBase(), loadcase.LoadRestrainCondition);

            if (_thicknessesW.ContainsKey(load.GlassLoadCase))
            {
                return _thicknessesW[load.GlassLoadCase];
            }
            else
            {
                // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                throw new NotSupportedException($"Deformation thickness not found for load duration: {load.GlassLoadCase.LoadDuration}, temperature: {load.GlassLoadCase.Temperature} ");
            }
        }

        public override double[] GetStressThickness(IGlassLoad load)
        {
            if (_thicknessesStress.Length == 0)
            {
                CalculateEquivalentThicknesses();
                if (_thicknessesStress.Length == 0)
                    throw new ArgumentException();
            }

            double[] stressThickness = new double[((LaminatedGlass)Glass).GlassLayerCount];

            var loadcase = load.GlassLoadCase;

            EquivalentThicknessParameters parameters = 
                new EquivalentThicknessParameters(loadcase.LoadDuration, loadcase.Temperature, load.GetGeometryBase(), loadcase.LoadRestrainCondition);

            for (int i = 0; i < _thicknessesStress.Length; i++)
            {
                if (_thicknessesStress[i].ContainsKey(load.GlassLoadCase))
                {
                    stressThickness[i] = _thicknessesStress[i][load.GlassLoadCase];
                }
                else
                {
                    // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                    throw new NotSupportedException($"Deformation thickness not found for load duration: " +
                                                    $"{load.GlassLoadCase.LoadDuration}, temperature: {load.GlassLoadCase.Temperature}");
                }
            }

            return stressThickness;
        }

        public override double GetTotalThickness()
        {
            return ((LaminatedGlass)Glass).TotalThickness;
        }

        /// <returns>The incremental distances of the glass layers center of mass starting from the first one</returns>
        public double[] GetMonolithicBarycenterDistances()
        {
            double[] distances = new double[(Glass as LaminatedGlass).MonolithicGlasses.Length];

            for (int i = 0; i < (Glass as LaminatedGlass).MonolithicGlasses.Length; i++)
            {
                if (i != 0)
                {
                    distances[i] += distances[i - 1];
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i - 1].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i - 1].Thickness;
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i].Thickness / 2.0;
                }
                else
                {
                    distances[i] = 0;
                }
            }
            return distances;
        }

        /// <returns>The incremental distances of the interlayer center of mass starting from the center of mass of the first glass layer</returns>
        public double[] GetInterlayerBarycenterDistances()
        {
            double[] distances = new double[(Glass as LaminatedGlass).Interlayers.Length];

            for (int i = 0; i < (Glass as LaminatedGlass).Interlayers.Length; i++)
            {
                if (i != 0)
                {
                    distances[i] += distances[i - 1];
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i - 1].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i].Thickness;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i].Thickness / 2.0;
                }
                else
                {
                    distances[i] = (Glass as LaminatedGlass).MonolithicGlasses[0].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[0].Thickness / 2.0;
                }
            }
            return distances;
        }

        /// <inheritdoc cref="LaminatedGlass.GetElasticModulus()"/>
        public override double GetElasticModulus()
        {
            return ((LaminatedGlass)Glass).GetElasticModulus();
        }

        /// <inheritdoc cref="LaminatedGlass.GetPoissonRatios()"/>
        public override double GetPoissonRatio()
        {
            return ((LaminatedGlass)Glass).GetPoissonRatios();
        }

        /// <inheritdoc cref="LaminatedGlass.GetSelfWeightPerUnitArea()"/>
        public override double GetSelfWeightPerUnitArea()
        {
            return ((LaminatedGlass)Glass).GetSelfWeightPerUnitArea();
        }

        public override double GetSelfWeightTotal()
        {
            return _glassSurface.Shape.GetArea() * GetSelfWeightPerUnitArea();
        }

        /// <inheritdoc cref="LaminatedGlass.GetDensity()"/>
        public override double GetDensity()
        {
            return ((LaminatedGlass)Glass).GetDensity();
        }

        #endregion

        #region Mesh

        /// <inheritdoc cref="GlassWrapper.GenerateMesh()"/>
        public override bool GenerateMesh()
        {
            bool status = GenerateSingleLayerMesh(out Mesh mesh, out Dictionary<GeometryRestrain, int[]> meshGeometryRestrainVertices,
                                                  out Dictionary<Load, int[]> meshLoadsVertexIndexes, 
                                                  out Dictionary<Load, int[]> meshLoadsFaceIndexes);

            if (!status)
                return false;

            Mesh[] meshes;

            switch (_glassSurface.Prototype.LaminatedAnalysisType)
            {
                case Models.Prototype.LaminatedAnalysisTypes.MultiLayered:
                    meshes = new Mesh[1];
                    meshes[0] = mesh;
                    _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>()
                    { 
                        new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) 
                    };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    for (int n = 0; n < _externalFaceLoads.Count; n++)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes
                                .Where(i => i.Key.CompareGuid(mesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsFaceIndexes[_externalFaceLoads[n]]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes
                                .Where(i => i.Key.CompareGuid(mesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsVertexIndexes[_externalFaceLoads[n]]);
                        }
                    }

                    break;

                case Models.Prototype.LaminatedAnalysisTypes.EquivalentThickness:
                    meshes = new Mesh[1];
                    meshes[0] = mesh;
                    _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>()
                    { 
                        new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) 
                    };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    for (int n = 0; n < _externalFaceLoads.Count; n++)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes
                                .Where(i => i.Key.CompareGuid(mesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsFaceIndexes[_externalFaceLoads[n]]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes
                                .Where(i => i.Key.CompareGuid(mesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsVertexIndexes[_externalFaceLoads[n]]);
                        }
                    }

                    break;

                case Models.Prototype.LaminatedAnalysisTypes.MultiElement:
                    meshes = new Mesh[(Glass as LaminatedGlass).MonolithicGlasses.Count() + (Glass as LaminatedGlass).Interlayers.Count()];

                    var interlayerDistances = GetInterlayerBarycenterDistances();
                    var glassDistances = GetMonolithicBarycenterDistances();

                    Vector3d normal = _glassSurface.Shape.GetNormalVector();

                    // Copia mesh
                    for (int i = 0; i < glassDistances.Length; i++)
                    {
                        Mesh cloned = (Mesh)mesh.Clone(false);

                        cloned.Move(normal * glassDistances[i]);

                        meshes[i * 2] = cloned;

                        var ids = cloned.Faces.SelectMany(k => k.GetNodes()).Distinct().ToList();// faccio cosi cosi prendo gli id degli elementi nell'ordine degli elementi nella lista

                        //var ids = cloned.Vertices.GetElementIdMap().Keys.ToList(); // faccio cosi cosi prendo gli id degli elementi nell'ordine degli elementi nella lista

                        _volumeUpperLowerVerticesIds[i * 2] = (ids, ids);
                    }

                    // Copia mesh e Generazione brick
                    for (int i = 0; i < interlayerDistances.Length; i++)
                    {
                        IEnumerable<int> lowerVertices = null;
                        IEnumerable<int> upperVertices = null;

                        for (int j = 0; j < INTERLAYER_DISCRETIZATION; j++)
                        {
                            Mesh cloned = (Mesh)mesh.Clone(false);
                            Plane plane = _glassSurface.Shape.GetPlane(GeometryBase.GetDefaultTolerance());

                            double thickness = (Glass as LaminatedGlass).Interlayers[i].Thickness;
                            double increment = thickness / INTERLAYER_DISCRETIZATION * j;

                            cloned.Move(normal * (interlayerDistances[i] - thickness / 2.0 + increment));
                            plane.Move(normal * (interlayerDistances[i] - thickness / 2.0 + increment));

                            Mesh volumeMesh = cloned.ExtrudeFaces(normal * thickness / INTERLAYER_DISCRETIZATION);

                            Dictionary<int, int> vertexIdMap = null;

                            if (meshes[i * 2 + 1] == null)
                            {
                                meshes[i * 2 + 1] = volumeMesh;
                            }
                            else
                            {
                                // vertexIdMap: Map between MeshVertex.Id of meshToJoin and id of the same vertex in meshes[i * 2 + 1] (Map old, new)
                                meshes[i * 2 + 1].JoinMesh(volumeMesh, out vertexIdMap, out _, out _);
                            }


                            if (j == 0)
                            {
                                // primo strato di brick
                                if (vertexIdMap == null)
                                {
                                    lowerVertices = volumeMesh.Vertices
                                        .Where(k => plane.SquareDistanceToPlane(k.Point) < Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance()))
                                        .Select(k => k.Id);
                                }
                                else
                                {
                                    throw new NotSupportedException();
                                }
                            }

                            if (j == INTERLAYER_DISCRETIZATION - 1 && vertexIdMap != null)
                            {
                                plane.Move(normal * thickness / INTERLAYER_DISCRETIZATION); // sposto il piano dello spessore per spostarmi nel punto pi� distante dell'interlyaer

                                // primo strato di brick
                                if (vertexIdMap != null)
                                {
                                    // il join mesh ha preso volumeMesh e joinanto dentro meshes, cambiando gli iD, bisogna usare la mappa.
                                    upperVertices = volumeMesh.Vertices
                                        .Where(k => plane.SquareDistanceToPlane(k.Point) < Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance()))
                                        .Select(k => k.Id)
                                        .Select(x => vertexIdMap[x]);
                                }
                                else
                                {
                                    // se � nullo siamo nel caso di INTERLAYER_DISCRETIZATION == 1
                                    upperVertices = volumeMesh.Vertices
                                        .Where(k => plane.SquareDistanceToPlane(k.Point) < Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance()))
                                        .Select(k => k.Id);
                                }
                            }
                        }

                        _volumeUpperLowerVerticesIds[i * 2 + 1] = (lowerVertices, upperVertices);
                    }

                    // Assegno mesh a wrapper
                    _meshes = meshes;

                    // Creo e assegno mappa - meshcarichi,id al wrapper

                    Mesh externalMesh = GetGlassMeshExternal();
                    Mesh internalMesh = GetGlassMeshInternal();

                    _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>() 
                    { 
                        new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(externalMesh, meshGeometryRestrainVertices) 
                    };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    for (int n = 0; n < _externalFaceLoads.Count; n++)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(externalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(externalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes
                                .Where(i => i.Key.CompareGuid(externalMesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsFaceIndexes[_externalFaceLoads[n]]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(_externalFaceLoads[n]))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(externalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(externalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes
                                .Where(i => i.Key.CompareGuid(externalMesh.Guid))
                                .First().Value.Add(_externalFaceLoads[n], meshLoadsVertexIndexes[_externalFaceLoads[n]]);
                        }
                    }

                    for (int n = 0; n < _internalFaceLoads.Count; n++)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(_internalFaceLoads[n]))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(internalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(internalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes
                                .Where(i => i.Key.CompareGuid(internalMesh.Guid))
                                .First().Value.Add(_internalFaceLoads[n], meshLoadsFaceIndexes[_internalFaceLoads[n]]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(_internalFaceLoads[n]))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(internalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(internalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes
                                .Where(i => i.Key.CompareGuid(internalMesh.Guid))
                                .First().Value.Add(_internalFaceLoads[n], meshLoadsVertexIndexes[_internalFaceLoads[n]]);
                        }
                    }

                    _meshComputed = true;

                    break;

                default:
                    throw new NotSupportedException();
            }            
            
            return true;
        }


        /// <param name="layerIndex">The index of the interlayer in a interlayerList. Not in the glassPackage</param>
        /// <returns>upper and lower vertices Id of the interlayer</returns>
        public (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices) GetLayerUpperLowerVerticesIds(int layerIndex)
        {
            return _volumeUpperLowerVerticesIds[layerIndex];
        }

        #endregion

        #region Equivalent thickness


        /// <summary>
        /// Perform calculation of the equivalent thickness based on <see cref="Models.Prototype.LaminatedAnalysisType"/>
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public bool CalculateEquivalentThicknesses()
        {
            var eqThicknessParameters = _glassSurface.Prototype.LaminatedEqThicknessParameter;
            var boundaryCondition = eqThicknessParameters.LaminatedEqThicknessBoundaryCondition;
            var eqThicknessMethod = eqThicknessParameters.LaminatedEqThicknessMethod;

            if (!_glassSurface.IsPlanar())
                throw new NotSupportedException("EET supports only planar surface");

            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{eqThicknessMethod} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{eqThicknessMethod} does support only one interlayer.");

            // merge dei carichi, può contenere duplicati dal punto di vista del calcolo eet
            List<IGlassLoad> loadsToProcess = _externalFaceLoads.Union(_internalFaceLoads).Cast<IGlassLoad>().ToList();

            // converto tutti i carichi in area normale, viene mantenuto l'indice per non perdere associazione con loadsToProcess
            var buffer = new Loads.NormalAreaLoad[loadsToProcess.Count];
            List<Loads.NormalAreaLoad> loadsToProcessNormalAreaLoad = buffer.ToList();

            Plane surfacePlane = _glassSurface.Shape.GetPlane();
            for (int i = 0; i < loadsToProcess.Count; i++)
            {
                if (loadsToProcess[i] is Loads.NormalAreaLoad nal)
                {
                    loadsToProcessNormalAreaLoad[i] = nal;
                    continue;
                }
                else if (loadsToProcess[i] is Loads.AreaLoad al)
                {
                    loadsToProcessNormalAreaLoad[i] = (Loads.NormalAreaLoad)al.ConvertToNormalAreaLoad();
                    continue;
                }
                else if (loadsToProcess[i] is IConvertibleLoad icl)
                {
                    loadsToProcessNormalAreaLoad[i] = (Loads.NormalAreaLoad)icl.ConvertToNormalAreaLoad(surfacePlane, _glassSurface.Checker.Options.LineLoadWidthEqThickness);
                    continue;
                }
                else
                    throw new NotImplementedException(loadsToProcess[i].GetType().FullName);
            }


            if (loadsToProcess.Count() == 0 || loadsToProcessNormalAreaLoad.Count() == 0)
                return false;


            //Load case unici, sono raggrupati in base a durata, temperatura e tipo di vincolo
            List<IGlassLoadCase> loadCaseUnique = loadsToProcess.Select(i => i.GlassLoadCase).Distinct(new LoadCaseDurationTemperatureRestrainEqualityComparer()).ToList();

            /*
                creo lista di carichi di pressione uniforme.
                non vanno nella lista i carichi il cui loadcase è associato ad un altro carico es distribuito
                in quanto pressione + distribuito va risolto con numerico
            */
            List<IGlassLoad> uniformNormalAreaLoad = new List<IGlassLoad>();             
            for (int i = 0; i < loadsToProcess.Count; i++)
            {
                if (loadsToProcess[i] is Loads.NormalAreaLoad nal && nal.GetGeometryBase().Equals(_glassSurface.Shape))
                {
                    List<IGlassLoad> areaLoadsLocal = new List<IGlassLoad>();

                    // è pressione uniforme
                    bool matchLoad = false;
                    for (int j = i + 1; j < loadsToProcess.Count; j++)  // controllo la parte rimanente della lista
                    {
                        if (loadsToProcess[i].GlassLoadCase == loadsToProcess[j].GlassLoadCase) // se esiste un carico con lo stesso loadcase allora il carico i non va nella lista
                        {
                            matchLoad = true;
                            break;
                        }
                    }

                    if (!matchLoad)
                    {
                        uniformNormalAreaLoad.Add(nal);
                    }
                }
            }



            // Calcola lo spessore equivalente per i casi supportati, altrimenti usa eet numerico
            switch (boundaryCondition)
            {
                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.Other:

                    SetEquivalentThicknessEET(GetPsiEETNumerical(loadsToProcessNormalAreaLoad.ToList()));

                    return true; // gi� processati tutti con il numerico, ritorna

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularOneSideClamped:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {

                        if (uniformNormalAreaLoad.Count() > 0)
                        {
                            double psi = 14.0 / 5.0 / Math.Pow(eqThicknessParameters.A, 2.0); // a diverso da zero validato dalla classe LaminatedEqThicknessParameters

                            SetEquivalentThicknessEET(uniformNormalAreaLoad.Select(i => (i.GlassLoadCase, psi)).ToArray());

                            foreach (var load in uniformNormalAreaLoad)
                            {
                                int i = loadsToProcess.IndexOf(load);
                                loadsToProcess.RemoveAt(i);
                                loadsToProcessNormalAreaLoad.RemoveAt(i);
                            }
                        }

                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularTwoSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {

                        if (uniformNormalAreaLoad.Count() > 0)
                        {
                            // FROM THE EFFECTIVE THICKNESS OF LAMINATED GLASS PLATES Laura Galuppi and Gianni Royer-Carfagni

                            double aMm = eqThicknessParameters.A;
                            double bMm = eqThicknessParameters.B;
                            
                            var ni = GetPoissonRatio();

                            double PI5 = Math.Pow(Math.PI, 5);

                            double PIBsu2a = Math.PI * bMm / (2d * aMm);

                            double C = Math.Cosh(PIBsu2a);
                            double S = Math.Sinh(PIBsu2a);

                            double A1 = 4.0 / (PI5) * (ni * (1d + ni) * S - ni * (1d - ni) * PIBsu2a * C) / ((3d + ni) * (1d - ni) * S * C - Math.Pow((1 - ni), 2d) * PIBsu2a);
                            double B1 = 4.0 / (PI5) * (ni * (1d - ni) * S) / ((3d + ni) * (1 - ni) * S * C - Math.Pow((1d - ni), 2d) * PIBsu2a);
        

                            double psiNum = 8d * Math.Pow(Math.PI, 2) * (4d * bMm + B1 * PI5 * bMm * C + 2d * aMm * S * Math.Pow(Math.PI, 4) * (A1 - B1));
                            double psiDen =    aMm * (Math.Pow(B1, 2) * Math.Pow(Math.PI, 11) * Math.Pow(bMm, 2) * S * C + 32.0 * aMm * bMm 
                                             + aMm * bMm * B1 * (-B1 + 4d * A1 * Math.Pow(S, 2)) * Math.Pow(Math.PI, 10)
                                             + 2d * Math.Pow(aMm, 2) * S * C * (2d * Math.Pow(A1, 2) 
                                             + Math.Pow(B1, 2)) * Math.Pow(Math.PI, 9) 
                                             + 16d * B1 * PI5 * aMm * bMm * C 
                                             + 32d * Math.Pow(aMm, 2) * S * Math.Pow(Math.PI, 4) * (A1 - B1));

                            double psi = psiNum / psiDen;

                            SetEquivalentThicknessEET(uniformNormalAreaLoad.Select(i => (i.GlassLoadCase, psi)).ToArray());

                            foreach (var load in uniformNormalAreaLoad)
                            {
                                int i = loadsToProcess.IndexOf(load);
                                loadsToProcess.RemoveAt(i);
                                loadsToProcessNormalAreaLoad.RemoveAt(i);
                            }
                        }
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.ASTME1300)
                    {

                        SetEquivalentThicknessASTM(uniformNormalAreaLoad, eqThicknessParameters.A);

                        foreach (var load in uniformNormalAreaLoad)
                        {
                            int i = loadsToProcess.IndexOf(load);
                            loadsToProcess.RemoveAt(i);
                            loadsToProcessNormalAreaLoad.RemoveAt(i);
                        }

                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.NEN)
                    {
                        throw new NotImplementedException();
                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularThreeSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        // usiamo numerico
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.NEN)
                    {
                        throw new NotImplementedException();
                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        // Processiamo i carichi uniformi 
                        if (uniformNormalAreaLoad.Count() > 0)
                        {
                            // FROM THE EFFECTIVE THICKNESS OF LAMINATED GLASS PLATES Laura Galuppi and Gianni Royer-Carfagni

                            double asquare = Math.Pow(eqThicknessParameters.A, 2);
                            double bsquare = Math.Pow(eqThicknessParameters.B, 2);

                            double psi = Math.Pow(Math.PI, 2) * (asquare + bsquare) / (asquare * bsquare);

                            SetEquivalentThicknessEET(uniformNormalAreaLoad.Select(i => (i.GlassLoadCase, psi)).ToArray());

                            foreach (var load in uniformNormalAreaLoad)
                            {
                                int i = loadsToProcess.IndexOf(load);
                                loadsToProcess.RemoveAt(i);
                                loadsToProcessNormalAreaLoad.RemoveAt(i);
                            }
                        }

                        // PRESSIONE CONCENTRATA
                        
                        /*
                            Devo trovare i carichi normali non uniformemente distribuiti che non hanno loadcase in comune con altri carichi.
                        */
                        List<IGlassLoad> concentratedLoads = new List<IGlassLoad>();

                        //var a = loadsToProcessNormalAreaLoad.GroupBy(i => i.GlassLoadCase);
                        //int b = 1;
                        //var c = loadsToProcessNormalAreaLoad.GroupBy(i => i.GlassLoadCase).Where(g => g.Count() < 1).ToList();
                        //var aa = loadsToProcessNormalAreaLoad.GroupBy(i => i.GlassLoadCase).Where(g => g.Count() < 1).SelectMany(g => g.ToList().Cast<IGlassLoad>());
                        //var bb = loadsToProcessNormalAreaLoad.GroupBy(i => i.GlassLoadCase).Where(g => g.Count() > 1).SelectMany(g => g.ToList().Cast<IGlassLoad>());
                        
                        // Filtro i load con loadcase diversi 
                        concentratedLoads = loadsToProcessNormalAreaLoad.GroupBy(i => i.GlassLoadCase).Where(g => g.Count() < 2)
                                                                        .SelectMany(g => g.ToList().Cast<IGlassLoad>()).ToList();


                        //for (int i = 0; i < loadsToProcessNormalAreaLoad.Count; i++)
                        //{
                        //    if (!loadsToProcessNormalAreaLoad[i].GetGeometryBase().Equals(_glassSurface.Shape)) // Prendo i normali con area diversa da superfice
                        //    {
                        //        List<IGlassLoad> areaLoadsLocal = new List<IGlassLoad>();

                        //        // è pressione uniforme locale

                        //        bool matchLoad = false;
                        //        for (int j = i + 1; j < loadsToProcessNormalAreaLoad.Count; j++)  // controllo la parte rimanente della lista
                        //        {
                        //            if (loadsToProcess[i].GlassLoadCase == loadsToProcess[j].GlassLoadCase) // se esiste un carico con lo stesso loadcase allora il carico i non va nella lista
                        //            {
                        //                matchLoad = true;
                        //                break;
                        //            }
                        //        }

                        //        if (!matchLoad)
                        //        {
                        //            concentratedLoads.Add(loadsToProcessNormalAreaLoad[i]);
                        //        }

                        //    }
                        //}

                        if (concentratedLoads.Count() > 0)
                        {
                            List<Task<(IGlassLoad, double)>> tasks = concentratedLoads.Select(i => GetEETFourSidePsiConcentratedLoadAsync(i, eqThicknessParameters)).ToList();

                            // aspetto tutti i thread prima di processare psi
                            Task.WaitAll(tasks.ToArray());

                            for (int i = 0; i < tasks.Count; i++)
                            {
                                if (tasks[i].Result.Item2 != -1)
                                {
                                    SetEquivalentThicknessEET(new[] { (tasks[i].Result.Item1.GlassLoadCase, tasks[i].Result.Item2) }); // TODO: loadcase doppi 

                                    int j = loadsToProcessNormalAreaLoad.IndexOf((Loads.NormalAreaLoad)tasks[i].Result.Item1);
                                    loadsToProcess.RemoveAt(j);
                                    loadsToProcessNormalAreaLoad.RemoveAt(j);
                                }
                            }

                        }

                        break;
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.ASTME1300)
                    {
                        SetEquivalentThicknessASTM(loadsToProcess);
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.NEN)
                    {
                        throw new NotImplementedException();
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.Omega)
                    {
                        throw new NotImplementedException();
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }


            
            // processo i rimanenti con il numerico
            if (loadsToProcess.Count() > 0)
            {
                SetEquivalentThicknessEET(GetPsiEETNumerical(loadsToProcessNormalAreaLoad));
                return true;
            }

            return false;
        }


        #region Equivalent thickness - protected methods
        
        protected void SetEquivalentThicknessASTM(List<IGlassLoad> loads, double bendingDimension = 0)
        {
            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter.LaminatedEqThicknessMethod} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter.LaminatedEqThicknessMethod} does support only one interlayer.");

            LaminatedGlass glass = (LaminatedGlass)Glass;

            double elasticModulus = glass.GetElasticModulus();

            double h1 = glass.MonolithicGlasses[0].Thickness;
            double h2 = glass.MonolithicGlasses[1].Thickness;
            double hv = glass.Interlayers[0].Thickness;

            double hs = 0.5 * (h1 + h2) + hv;

            double hs1 = hs * h1 / (h1 + h2);
            double hs2 = hs * h2 / (h2 + h1);

            double a = 0;
            if (bendingDimension == 0)
            {
                a = _glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Min(); // in casi regolari funziona,
                                                                                         // in casi irregolari non tanto bene
                                                                                         // es. un poligono di 5 lati con uno dei lati molto piccolo
                                                                                         // andrebbe fatto un metodo per capire qual � "smallest dimension of bending of the laminate plate"
            }
            else
            {
                a = bendingDimension;
            }

            double Is = h1 * Math.Pow(hs2, 2.0) + h2 * Math.Pow(hs1, 2.0);

            double aSquare = Math.Pow(a, 2);
            double hsSquare = Math.Pow(hs, 2);

            double hs1Square = Math.Pow(hs1, 2);
            double hs2Square = Math.Pow(hs2, 2);

            for (int i = 0; i < loads.Count; i++)
            {
                var loadCase = loads[i].GlassLoadCase;

                var shearModule = glass.Interlayers[0].Material.GetShearModule(loadCase.LoadDuration, loadCase.Temperature);

                double lambda = 1.0 / (1.0 + (9.6 * elasticModulus * Is * hv) / (shearModule * hsSquare * aSquare)); // Shear transfer coefficient

                double hw = Math.Pow(hs1Square + hs2Square + 12.0 * lambda * Is, 1.0 / 3.0);

                //EquivalentThicknessParameters parameters = new EquivalentThicknessParameters(loadCase.LoadDuration,
                //                                            loadCase.Temperature, loads[i].GetGeometryBase(), loadCase.LoadRestrainCondition);

                _thicknessesW[loads[i].GlassLoadCase] = hw;
                _thicknessesStress[0][loads[i].GlassLoadCase] = Math.Sqrt(Math.Pow(hw, 3.0) / (h1 + 2.0 * lambda * hs2));
                _thicknessesStress[1][loads[i].GlassLoadCase] = Math.Sqrt(Math.Pow(hw, 3.0) / (h2 + 2.0 * lambda * hs1));
            }

        }

        protected void SetEquivalentThicknessEET((IGlassLoadCase loadCase, double psi)[] loadCasePsiValues)
        {
            var loadCases = loadCasePsiValues.Select(i => i.loadCase).ToArray();
            var psiValues = loadCasePsiValues.Select(i => i.psi).ToArray();


            LaminatedGlass glass = (LaminatedGlass)Glass;

            double h1 = glass.MonolithicGlasses[0].Thickness;
            double h2 = glass.MonolithicGlasses[1].Thickness;
            double hint = glass.Interlayers[0].Thickness;

            double E = GetElasticModulus();
            double ni = GetPoissonRatio();

            double d = 0.5 * (h1 + h2) + hint; // eq. 6.37
            double d1 = d * h2 / (h1 + h2);
            double d2 = d * h1 / (h1 + h2);

            double oneMinusNiSquare = 1.0 - Math.Pow(ni, 2.0);
            double h1Cube = Math.Pow(h1, 3.0);
            double h2Cube = Math.Pow(h2, 3.0);

            // eq. 6.52
            double DAbs = E * h1Cube / (12.0 * oneMinusNiSquare) + E * h2Cube / (12.0 * oneMinusNiSquare);

            // eq. 6.53
            double DFull = DAbs + E / oneMinusNiSquare * h1 * h2 / (h1 + h2) * Math.Pow(d, 2.0);

            // eq 6.46
            double secondDeno = h1Cube + h2Cube;

            // eq 6.46
            double firstDeno = secondDeno + 12 * (h1 * Math.Pow(d1, 2.0) + h2 * Math.Pow(d2, 2.0));


            //_thicknessesW = new Dictionary<IGlassLoadCase, double>();
            if (_thicknessesStress[0] == null)
                _thicknessesStress[0] = new Dictionary<IGlassLoadCase, double>();

            if (_thicknessesStress[1] == null)
                _thicknessesStress[1] = new Dictionary<IGlassLoadCase, double>();


            Action<int> action = new Action<int>((index) =>
            {
                double shearModule = glass.Interlayers[0].Material.GetShearModule(loadCases[index].LoadDuration, loadCases[index].Temperature);

                // eq. 6.55
                double eta2D = 1.0 / (1.0 + hint * E / (shearModule * oneMinusNiSquare) * DAbs / DFull * h1 * h2 / (h1 + h2) * psiValues[index]);

                double tw = Math.Pow(1.0 / (eta2D / firstDeno + (1.0 - eta2D) / secondDeno), 1.0 / 3.0);

                _thicknessesW[loadCases[index]] = tw;
                _thicknessesStress[0][loadCases[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d1) / firstDeno + h1 / Math.Pow(tw, 3.0)));
                _thicknessesStress[1][loadCases[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d2) / firstDeno + h2 / Math.Pow(tw, 3.0)));
            });


            // TODO: valutare se tenere parallel pi� for o solo for a livello prestazionale
            Parallel.ForEach(Enumerable.Range(0, loadCases.Count()), action);

        }


        /// <summary>
        /// Calculate the Psi of EET method with the numerical procedure. Ref. THE EFFECTIVE THICKNESS OF LAMINATED GLASS PLATES
        /// </summary>
        /// <param name="loads"></param>
        /// <returns></returns>
        protected (IGlassLoadCase load, double psi)[] GetPsiEETNumerical(List<Loads.NormalAreaLoad> loads)
        {
            if (loads is null)
                throw new ArgumentNullException(nameof(loads));

            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only one interlayer.");

            var surfacePlane = _glassSurface.Shape.GetPlane();


            // Lista carichi da esportare nel fem
            List<Loads.NormalAreaLoad> normalAreaLoads = loads.ToList() ;


            FemModels.FemModelWrapper femModel = new FemModels.FemModelWrapper("EETNumerical")
            {
                AnalysisType = Model.FEM.FemModel.AnalysisTypes.Linear
            };

            IsotropicFemMaterial material = new IsotropicFemMaterial(GetElasticModulus(), GetPoissonRatio(), 0, GetDensity());

            double plateThickness = 1;
            PlateProperty property = new PlateProperty(material, plateThickness, plateThickness, "p1");

            femModel.AddProperty(property);


            // dimensione mesh di default 2% del massimo lato della bbox. Alla Straus
            var bbboxSize = _glassSurface.Shape.ToLocal().GetBoundingBox().Size;
            femModel.AddShape(_glassSurface.Shape, "p1",
                              new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02, 
                                                           Transfinite = true, 
                                                           Algorithm = Mesh.GenerateOptions.MeshAlgorithm.FrontalDelaunayForQuads, 
                                                           HealShapes = true
                                                         },
                              normalAreaLoads.Cast<Load>().ToList(), _glassSurface.GetRestrains());

            normalAreaLoads.ForEach(i => femModel.AddLoadCase(i.LoadCase));

            var combinations = new List<Combination>();
            for (int i = 0; i < normalAreaLoads.Count(); i++)
            {
                combinations.Add(new Combination($"Load {i}"));
                combinations[i].AddLoadCaseCoefficient(normalAreaLoads[i].LoadCase, 1);
            }

            femModel.AddCombinations(combinations);


            femModel.ExportToSt7(System.IO.Path.GetTempPath(), femModel.Name);
            femModel.SetSolver(Models.Prototype.Solvers.Straus7);   // TODO: rimuovere e passare a solutore interno
            femModel.Solve();


            double flexularRigidity = material.E * Math.Pow(plateThickness, 3.0) / (12.0 * (1.0 - Math.Pow(material.Ni, 2.0)));

            Model.FEM.FiniteElements.FiniteElement[] elements = femModel.GetElements();

            ConcurrentBag<(IGlassLoadCase load, double psi)> psiValues = new ConcurrentBag<(IGlassLoadCase load, double psi)>();

            Action<int> action = new Action<int>((loadIndex) =>
            {
                double num = 0;
                double den = 0;
                for (int e = 0; e < elements.Length; e++)
                {
                    if (elements[e] is Model.FEM.FiniteElements.Plate plate)
                    {
                        var elementArea = plate.GetArea();

                        IEnumerable<ResultDisplacement> resultDisplacement = plate.Nodes.Select(i => i.Results.FirstOrDefault(j =>
                                                        ((Combination)j.Case).ContainsLoadCases(new[] { (Model.LoadCases.LoadCaseBase)normalAreaLoads[loadIndex].LoadCase })).Result)
                                                        .Cast<ResultDisplacement>(); 

                        if (resultDisplacement is null)
                            throw new ArgumentNullException();


                        var mean = ResultDisplacement.GetArithmeticMean(resultDisplacement.ToArray());

                        if (plate.AttributesLoadCase.Where(i => i is Model.FEM.Attributes.PlateNormalPressureAttribute).SingleOrDefault() != null)
                        {
                            num += mean.D3 * flexularRigidity * elementArea * ((Model.FEM.Attributes.PlateNormalPressureAttribute)plate.AttributesLoadCase.FirstOrDefault()).Pressure;
                        }

                        den += (Math.Pow(mean.R1 * flexularRigidity, 2.0) + Math.Pow(mean.R2 * flexularRigidity, 2.0)) * elementArea;
                    }
                }

                if (den == 0)
                    psiValues.Add((loads[loadIndex].GlassLoadCase, double.MaxValue)); // se c'� qualcosa che non va (den == 0) allora diamo un psi grande che corrisponde ad eta 0 cio� layerered limit
                else
                    psiValues.Add((loads[loadIndex].GlassLoadCase, num / den));
            });

            Parallel.ForEach(Enumerable.Range(0, normalAreaLoads.Count()), action);


            return psiValues.ToArray();
        }


        #region EET specific methods

        /// <summary>
        /// This coefficient is used in eet for concentrated loads in case of simply supported 4 side slab 
        /// </summary>
        /// <param name="csi">Coordinate of load shape baricenter parallel to <paramref name="a"/>. Must be less than <paramref name="a"/> - <paramref name="u"/> / 2 </param>
        /// <param name="eta">Coordinate of load shape baricenter parallel to <paramref name="b"/>. Must be less than <paramref name="b"/> - <paramref name="v"/> / 2 </param>
        /// <param name="u">Shape load perimeter lenght parallel to <paramref name="a"/>. Must be less than <paramref name="a"/></param>
        /// <param name="v">Shape load perimeter lenght parallel to <paramref name="b"/>. Must be less than <paramref name="b"/></param>
        /// <param name="a">Greater slab side</param>
        /// <param name="b">Smallest slab side</param>
        /// <param name="m">Series coefficient. Must be > 0 </param>
        /// <param name="n">Series coefficient. Must be > 0 </param>
        protected double GetEETFourSideAmnCoefficient(double csi, double eta, double u, double v, double a, double b, int m, int n)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
            return 1.0 / (m * n * Math.Pow(m * m / a / a + n * n / b / b, 2d)) * 16.0 / Math.Pow(Math.PI, 2d) *
                          Math.Sin(m * Math.PI * csi / a) * Math.Sin(m * Math.PI * u / 2d / a) *
                          Math.Sin(n * Math.PI * eta / b) * Math.Sin(n * Math.PI * v / 2d / b);
        }

        protected async Task<double> GetEETFourSideGxCoefficientAsync(double a, double b, double[,] ACoefficientsSquare)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
            double gx = 0;
            await Task.Run(() =>
            {
                double fixedCoeff = 1.0 / 4.0 * b / (Math.Pow(Math.PI, 6) * a);

                for (int n = 1; n <= 10; n++)
                {
                    for (int m = 1; m <= 10; m++)
                    {
                        gx += fixedCoeff * Math.Pow(m, 2.0) * ACoefficientsSquare[m - 1, n - 1];
                    }
                }
            });

            return gx;
        }

        protected async Task<double> GetEETFourSideGyCoefficientAsync(double a, double b, double[,] ACoefficientsSquare)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
            double gy = 0;
            await Task.Run(() =>
            {
                double fixedCoeff = 1.0 / 4.0 * a / (Math.Pow(Math.PI, 6) * b);
                for (int n = 1; n <= 10; n++)
                {
                    for (int m = 1; m <= 10; m++)
                    {
                        gy += fixedCoeff * Math.Pow(n, 2.0) * ACoefficientsSquare[m - 1, n - 1];
                    }
                }
            });

            return gy;
        }

        protected async Task<double> GetEETFourSideGpCoefficientAsync(double a, double b, double xi, double eta, double u, double v, double[,] ACoefficients)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
            double gp = 0;
            await Task.Run(() =>
            {

                double fixedCoeff1 = 4.0 * a * b / Math.Pow(Math.PI, 6.0);
                double piXiSuA = Math.PI * xi / a;
                double piEtaSuB = Math.PI * eta / b;
                double piUSuA = 0.5 * Math.PI * u / a;
                double piVSuB = 0.5 * Math.PI * v / b;

                for (int n = 1; n <= 10; n++)
                {
                    var sinn = Math.Sin(n * piVSuB) * Math.Sin(n * piEtaSuB);

                    for (int m = 1; m <= 10; m++)
                    {
                        gp += fixedCoeff1 * (ACoefficients[m - 1, n - 1] * Math.Sin(m * piXiSuA) * Math.Sin(m * piUSuA) * sinn) / m / n;
                    }
                }
            });

            return gp;
        }

        /// <summary>
        /// Calculate the Psi coefficient by means of the theoretical solution of a four side supported slab 
        /// </summary>
        /// <param name="load"></param>
        /// <param name="eqThicknessParameters"></param>
        /// <returns></returns>
        protected async Task<(IGlassLoad, double)> GetEETFourSidePsiConcentratedLoadAsync(IGlassLoad load, 
                                                   Models.Prototype.LaminatedEqThicknessParameters eqThicknessParameters)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx

            /*
                Notazione:
                    A: lato 1
                    B: lato 2
                    xi: Coordinata centro impronta del carico parallela ad A
                    eta: Coordinata centro impronta del carico parallela ad B
                    U: Larghezza impronta del carico parallela ad A
                    V: Larghezza impronta del carico parallela ad B
            */


            double psi = -1;

            await Task.Run(() =>
            {
                List<Line3d> glassPerimeter = _glassSurface.Shape.Fill.Explode();

                if (glassPerimeter.Count() != 4)
                    return;

                // setto load geometry
                Polygon3d loadPerimeter = null;
                if (load is Loads.NormalAreaLoad nal)
                {
                    if (nal.Shape.Fill.Explode().Count != 4)
                        return;
                    else
                    {
                        loadPerimeter = nal.Shape.Fill;
                    }
                }
                else if (load is Loads.LineLoad ll)
                {
                    Line3d line = ll.GetGeometry();

                    Vector3d surfaceNormal = _glassSurface.Shape.GetNormalVector();
                    surfaceNormal.Unitize();

                    Vector3d lineVector = new Vector3d(line.Start, line.End);
                    lineVector.Unitize();

                    Vector3d movementVector = surfaceNormal.CrossProduct(lineVector);
                    movementVector *= _glassSurface.Checker.Options.LineLoadWidthEqThickness;
                    movementVector /= 2.0;

                    Point3d p1 = line.Start.CloneAndMove(movementVector);
                    Point3d p2 = line.End.CloneAndMove(movementVector);
                    movementVector.Reverse();
                    Point3d p3 = line.End.CloneAndMove(movementVector);
                    Point3d p4 = line.Start.CloneAndMove(movementVector);

                    loadPerimeter = new Polygon3d()
                        {
                            p1,
                            p2,
                            p3,
                            p4
                        };
                }
                else if (load is Loads.PointLoad pl)
                {
                    Point3d point = pl.GetGeometry();

                    Vector3d v1 = new Vector3d(glassPerimeter[0].Start - glassPerimeter[0].End);
                    v1.Unitize();
                    v1 *= _glassSurface.Checker.Options.PointLoadWidthEqThickness / 2.0;

                    Vector3d v2 = new Vector3d(glassPerimeter[1].Start - glassPerimeter[1].End);
                    v2.Unitize();
                    v2 *= _glassSurface.Checker.Options.PointLoadWidthEqThickness / 2.0;

                    Point3d p1 = point.CloneAndMove(v1);
                    v1.Reverse();
                    Point3d p2 = point.CloneAndMove(v1);

                    p1.Move(v2);
                    p2.Move(v2);

                    v2.Reverse();
                    Point3d p3 = p2.CloneAndMove(v2);
                    Point3d p4 = p1.CloneAndMove(v2);

                    loadPerimeter = new Polygon3d()
                        {
                            p1,
                            p2,
                            p3,
                            p4
                        };
                }
                else
                    return; // carico non supportato
                

                if (loadPerimeter != null)
                {
                    var loadPerimeterLines = loadPerimeter.Explode();

                    // Controllo che le lunghezze siano a coppie uguali
                    if (loadPerimeter[0].DistanceTo(loadPerimeter[1]) != loadPerimeter[2].DistanceTo(loadPerimeter[3]))
                        return;  // Non rettangolo regolare, passiamo al numerico

                    if (loadPerimeter[1].DistanceTo(loadPerimeter[2]) != loadPerimeter[3].DistanceTo(loadPerimeter[0]))
                        return;  // Non rettangolo regolare, passiamo al numerico

                    // Controllo almeno un lato dei loadperimeter sia parallelo al vetro

                    bool isShapeLoadParallel = true;
                    for (int k = 0; k < loadPerimeterLines.Count(); k++)
                    {
                        Vector3d vp = new Vector3d(loadPerimeterLines[k].Start, loadPerimeterLines[k].End);
                        vp.Unitize();

                        bool isParallel = false;

                        for (int j = 0; j < glassPerimeter.Count(); j++)
                        {
                            Vector3d vs = new Vector3d(glassPerimeter[j].Start, glassPerimeter[j].End);
                            vs.Unitize();

                            // un loadPerimeterLine deve essere parallelo ad almeno ad lato di glassPerimeter
                            if (Math.Abs(Math.Abs(vp.DotProduct(vs)) - 1.00) < GeometryBase.GetDefaultTolerance())
                            {
                                // se entra allora il lato � parallelo al glassperimeter[i]
                                isParallel = true;
                                break;
                            }
                        }

                        if (!isParallel)
                        {
                            // se entra qua allora il lato non � parallelo a nessuno
                            isShapeLoadParallel = false;
                            break;
                        }
                    }

                    // se è parallelo procedo al calcolo
                    if (isShapeLoadParallel)
                    {
                        var loadCenter = loadPerimeter.GetCentroid();
                        double xi = 0; // parallelo ad a
                        double eta = 0; // parallelo ad b 
                        double u = loadPerimeter[0].DistanceTo(loadPerimeter[1]);
                        double v = loadPerimeter[1].DistanceTo(loadPerimeter[2]);

                        eta = glassPerimeter[0].DistanceTo(loadCenter);
                        xi = glassPerimeter[1].DistanceTo(loadCenter);

                        //if (glassPerimeter[0].GetLength() > glassPerimeter[1].GetLength())
                        //{
                        //    // primo lato è il più grande
                        //    xi = glassPerimeter[0].DistanceTo(loadCenter);
                        //    eta = glassPerimeter[1].DistanceTo(loadCenter);
                        //}
                        //else
                        //{
                        //    // primo lato è il più piccolo
                        //    eta = glassPerimeter[0].DistanceTo(loadCenter);
                        //    xi = glassPerimeter[1].DistanceTo(loadCenter);
                        //}

                        double[,] ACoefficients = new double[10, 10];
                        double[,] ACoefficientsSquare = new double[10, 10];

                        Action<int> ACoefficientAction = new Action<int>((m) =>
                        {
                            // double[,] non è threadsafe ma ogni thread scrive su un punto diverso.
                            for (int n = 1; n <= 10; n++)
                            {
                                ACoefficients[m - 1, n - 1] = GetEETFourSideAmnCoefficient(xi, eta, u, v, eqThicknessParameters.A, eqThicknessParameters.B, m, n);
                                ACoefficientsSquare[m - 1, n - 1] = Math.Pow(ACoefficients[m - 1, n - 1], 2.0);
                            }
                        });

                        Parallel.For(1, 11, ACoefficientAction);

                        Task<double> gxTask = GetEETFourSideGxCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, ACoefficientsSquare);
                        Task<double> gyTask = GetEETFourSideGyCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, ACoefficientsSquare);
                        Task<double> gpTask = GetEETFourSideGpCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, xi, eta, u, v, ACoefficients);

                        Task.WaitAll(gxTask, gyTask, gpTask);

                        psi =  gpTask.Result / (gxTask.Result + gyTask.Result);
                        return;
                    }
                }

                return;
            });

            return (load, psi);
        }

        #endregion

        #endregion

        #endregion


        public sealed class EquivalentThicknessParameters : IEquatable<EquivalentThicknessParameters>
        {
            public double LoadDuration { get;}
            public double Temperature { get;}
            public GeometryBase LoadGeometry { get;}
            public GlassSurface.LoadRestrainCondition RestrainCondition { get;}


            internal EquivalentThicknessParameters(double loadDuration, double temperature, 
                                                  GeometryBase loadGeometry, GlassSurface.LoadRestrainCondition restrainCondition)
            {
                LoadDuration = loadDuration;
                Temperature = temperature;
                LoadGeometry = loadGeometry ?? throw new ArgumentNullException(nameof(loadGeometry));
                RestrainCondition = restrainCondition;
            }


            public bool EqualsParameters(double loadDuration, double temperature, GeometryBase loadGeometry, GlassSurface.LoadRestrainCondition restrainCondition)
            {

                return     LoadDuration == loadDuration
                        && Temperature == temperature
                        && LoadGeometry == loadGeometry
                        && RestrainCondition == restrainCondition;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as EquivalentThicknessParameters);
            }

            public bool Equals(EquivalentThicknessParameters other)
            {
                if (ReferenceEquals(this, other))
                    return true;

                return other != null &&
                       LoadDuration == other.LoadDuration &&
                       Temperature == other.Temperature &&
                       EqualityComparer<GeometryBase>.Default.Equals(LoadGeometry, other.LoadGeometry) &&
                       RestrainCondition == other.RestrainCondition;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = -23;
                    hashCode = hashCode * -17 + LoadDuration.GetHashCode();
                    hashCode = hashCode * -17 + Temperature.GetHashCode();
                    hashCode = hashCode * -17 + EqualityComparer<GeometryBase>.Default.GetHashCode(LoadGeometry);
                    hashCode = hashCode * -17 + RestrainCondition.GetHashCode();
                    return hashCode; 
                }
            }

            public static bool operator ==(EquivalentThicknessParameters obj1, EquivalentThicknessParameters obj2)
            {
                if (obj1 is null)
                {
                    return obj2 is null;
                }

                if (ReferenceEquals(obj1, obj2))
                    return true;

                return obj1.Equals(obj2);
            }

            public static bool operator !=(EquivalentThicknessParameters obj1, EquivalentThicknessParameters obj2)
            {
                return !(obj1 == obj2);
            }
        }

    }
}
