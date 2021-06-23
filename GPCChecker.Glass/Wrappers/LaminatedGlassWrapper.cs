
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.LoadCases;
using GPC.Model.Glasses;
using GPC.Model.Restrains;
using GPC.Model.Loads;
using GPC.Model.FEM.Properties;
using GPC.Model.FEM.Materials;
using GPC.Model.Materials;
using System.Threading.Tasks;
using GPC.Model.Results;
using System.Collections.Concurrent;
using GPC.Model.Combinations;
using GPC.Checkers.Glasses.Loads;

namespace GPC.Checkers.Glasses.Wrappers
{
    public class LaminatedGlassWrapper : GlassPanelWrapper
    {

        protected const int INTERLAYER_DISCRETIZATION = 3;

        /// <summary>
        /// Thickness associates to displacement
        /// </summary>
        protected Dictionary<EquivalentThicknessParameters, double> _thicknessesW;

        /// <summary>
        /// Thickness associates to stress for each glass layer 
        /// </summary>
        protected Dictionary<EquivalentThicknessParameters, double>[] _thicknessesStress;
        

        /// <summary>
        /// List of upper and lower vertices Id of each volume layer
        /// </summary>
        protected (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices)[] _volumeUpperLowerVerticesIds;


        public Dictionary<EquivalentThicknessParameters, double> ThicknessesW => _thicknessesW;

        public Dictionary<EquivalentThicknessParameters, double>[] ThicknessesStress => _thicknessesStress;


        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass) 
            : this(glassSurface, glass, GlassPanelPositions.External)
        {                       

        }

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass, GlassPanelPositions position)
            : base(glassSurface, glass, position)
        {
            _thicknessesW = new Dictionary<EquivalentThicknessParameters, double>();
            _thicknessesStress = new Dictionary<EquivalentThicknessParameters, double>[glass.GlassLayerCount]; 

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

            EquivalentThicknessParameters parameters = new EquivalentThicknessParameters(loadcase.LoadDuration, loadcase.Temperature, 
                                                            load.GetGeometryBase(), load.LoadRestrainCondition);

            if (_thicknessesW.ContainsKey(parameters))
            {
                return _thicknessesW[parameters];
            }
            else
            {
                // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                throw new NotSupportedException($"Deformation thickness not found for load duration: {parameters.LoadDuration}, temperature: {parameters.Temperature} and geometry: {parameters.LoadGeometry}");
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

            EquivalentThicknessParameters parameters = new EquivalentThicknessParameters(loadcase.LoadDuration, loadcase.Temperature, 
                                                            load.GetGeometryBase(), load.LoadRestrainCondition);

            for (int i = 0; i < _thicknessesStress.Length; i++)
            {
                if (_thicknessesStress[i].ContainsKey(parameters))
                {
                    stressThickness[i] = _thicknessesStress[i][parameters];
                }
                else
                {
                    // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                    throw new NotSupportedException($"Deformation thickness not found for load duration: " +
                        $"{parameters.LoadDuration}, temperature: {parameters.Temperature} and geometry: {parameters.LoadGeometry}");
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
                                                    { new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    foreach (var load in _externalFaceLoads)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(mesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsFaceIndexes[load]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(mesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsVertexIndexes[load]);
                        }
                    }

                    break;

                case Models.Prototype.LaminatedAnalysisTypes.EquivalentThickness:
                    meshes = new Mesh[1];
                    meshes[0] = mesh;
                    _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>()
                                                    { new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    foreach (var load in _externalFaceLoads)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(mesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsFaceIndexes[load]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(mesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(mesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsVertexIndexes[load]);
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
                                meshes[i * 2 + 1] = volumeMesh;
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
                                    lowerVertices = volumeMesh.Vertices.Where(k => plane.SquareDistanceToPlane(k.Point) <
                                                                    Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance())).Select(k => k.Id);
                                }
                                else
                                    throw new NotSupportedException();
                            }

                            if (j == INTERLAYER_DISCRETIZATION - 1 && vertexIdMap != null)
                            {
                                plane.Move(normal * thickness / INTERLAYER_DISCRETIZATION); // sposto il piano dello spessore per spostarmi nel punto più distante dell'interlyaer

                                // primo strato di brick
                                if (vertexIdMap != null)
                                {
                                    // il join mesh ha preso volumeMesh e joinanto dentro meshes, cambiando gli iD, bisogna usare la mappa.
                                    upperVertices = volumeMesh.Vertices.Where(k => plane.SquareDistanceToPlane(k.Point) <
                                                   Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance())).Select(k => k.Id).Select(x => vertexIdMap[x]);

                                }
                                else
                                {
                                    // se è nullo siamo nel caso di INTERLAYER_DISCRETIZATION == 1

                                    upperVertices = volumeMesh.Vertices.Where(k => plane.SquareDistanceToPlane(k.Point) <
                                                                    Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(GeometryBase.GetDefaultTolerance())).Select(k => k.Id);
                                }
                            }
                        }


                        _volumeUpperLowerVerticesIds[i * 2 + 1] = (lowerVertices, upperVertices);

                    }


                    // Assegno mesh a wrapper
                    _meshes = meshes;

                    // Creo e assegno mappa - meshcarichi,id al wrapper

                    Mesh externalMesh = GetExternalGlassMesh();
                    Mesh internalMesh = GetInternalGlassMesh();


                    _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>() 
                                                    { new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(externalMesh, meshGeometryRestrainVertices) };

                    _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
                    _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

                    foreach (var load in _externalFaceLoads)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(externalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(externalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(externalMesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsFaceIndexes[load]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(externalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(externalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(externalMesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsVertexIndexes[load]);
                        }
                    }

                    foreach (var load in _internalFaceLoads)
                    {
                        if (meshLoadsFaceIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsFaceIndexes.Select(i => i.Key.CompareGuid(internalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsFaceIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(internalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsFaceIndexes.Where(i => i.Key.CompareGuid(internalMesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsFaceIndexes[load]);
                        }

                        if (meshLoadsVertexIndexes.ContainsKey(load))
                        {
                            if (_meshLoadsVertexIndexes.Select(i => i.Key.CompareGuid(internalMesh.Guid)).Count() == 0)
                            {
                                _meshLoadsVertexIndexes.Add(new KeyValuePair<Mesh, Dictionary<Load, int[]>>(internalMesh, new Dictionary<Load, int[]>()));
                            }
                            _meshLoadsVertexIndexes.Where(i => i.Key.CompareGuid(internalMesh.Guid)).FirstOrDefault().Value.Add(load, meshLoadsVertexIndexes[load]);
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


            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{eqThicknessMethod} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{eqThicknessMethod} does support only one interlayer.");


            // vengono filtrati in base a loadDuration, temperature e geometria del carico
            List<IGlassLoad> loadsToProcess = _externalFaceLoads.Union(_internalFaceLoads)
                                                .Distinct(new LoadDurationTemperatureAndGeometryEqualityComparer()).Cast<IGlassLoad>().ToList();


            if (loadsToProcess.Count() == 0)
                return false;

            // Calcola lo spessore equivalente per i casi supportati, altrimenti usa eet numerico
            switch (boundaryCondition)
            {
                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.Other:

                    SetEquivalentThicknessEET(GetPsiEETNumerical(loadsToProcess));

                    return true; // già processati tutti, ritorna

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularOneSideClamped:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        List<IGlassLoad> areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.NormalAreaLoad) && i.GetGeometryBase().Equals(_glassSurface.Shape)).ToList();

                        if (areaLoads.Count() > 0)
                        {
                            double psi = 14.0 / 5.0 / Math.Pow(eqThicknessParameters.A, 2.0); // a diverso da zero validato dalla classe LaminatedEqThicknessParameters

                            SetEquivalentThicknessEET(areaLoads.Select(i => (i, psi)).ToArray());
                            areaLoads.ForEach(i => loadsToProcess.Remove(i));
                        }
                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularTwoSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        List<IGlassLoad> areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.NormalAreaLoad) && i.GetGeometryBase().Equals(_glassSurface.Shape)).ToList();

                        if (areaLoads.Count() > 0)
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

                            SetEquivalentThicknessEET(areaLoads.Select(i => (i, psi)).ToArray());
                            areaLoads.ForEach(i => loadsToProcess.Remove(i));
                        }
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.ASTME1300)
                    {
                        List<IGlassLoad> areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.NormalAreaLoad) && i.GetGeometryBase().Equals(_glassSurface.Shape)).ToList();

                        SetEquivalentThicknessASTM(areaLoads, eqThicknessParameters.A);

                        areaLoads.ForEach(i => loadsToProcess.Remove(i));
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
                        // PRESSIONE UNIFORME
                        List<IGlassLoad> areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.NormalAreaLoad) && i.GetGeometryBase().Equals(_glassSurface.Shape)).ToList();
                        if (areaLoads.Count() > 0)
                        {
                            // FROM THE EFFECTIVE THICKNESS OF LAMINATED GLASS PLATES Laura Galuppi and Gianni Royer-Carfagni

                            double asquare = Math.Pow(eqThicknessParameters.A, 2);
                            double bsquare = Math.Pow(eqThicknessParameters.B, 2);

                            double psi = Math.Pow(Math.PI, 2) * (asquare + bsquare) / (asquare * bsquare);

                            SetEquivalentThicknessEET(areaLoads.Select(i => (i, psi)).ToArray());
                            areaLoads.ForEach(i => loadsToProcess.Remove(i));
                        }

                        // PRESSIONE CONCENTRATA
                        areaLoads.Clear();

                        areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.NormalAreaLoad)).ToList();
                        List<IGlassLoad> lineLoads = loadsToProcess.Where(i => i.GetType() == typeof(Loads.LineLoad) && i.GetType() == typeof(Loads.LineLoad)).ToList();

                        if (areaLoads.Count > 0 || lineLoads.Count > 0)
                        {
                            List<Task<(IGlassLoad, double)>> tasksAreaLoadPsi = new List<Task<(IGlassLoad, double)>>();
                            List<Task<(IGlassLoad, double)>> tasksLineLoadPsi = new List<Task<(IGlassLoad, double)>>();

                            if (areaLoads.Count() > 0)
                            {
                                tasksAreaLoadPsi = areaLoads.Select(i => GetEETPsiConcentratedLoadAsync(i, eqThicknessParameters)).ToList();
                            }

                            if (lineLoads.Count() > 0)
                            {
                                tasksLineLoadPsi = lineLoads.Select(i => GetEETPsiConcentratedLoadAsync(i, eqThicknessParameters)).ToList();
                            }

                            // aspetto tutti i thread prima di processare psi
                            Task.WaitAll(tasksAreaLoadPsi.Concat(tasksLineLoadPsi).ToArray());

                            for (int i = 0; i < tasksAreaLoadPsi.Count; i++)
                            {
                                if (tasksAreaLoadPsi[i].Result.Item2 != -1)
                                {
                                    SetEquivalentThicknessEET(new[] { (tasksAreaLoadPsi[i].Result.Item1, tasksAreaLoadPsi[i].Result.Item2) });
                                    loadsToProcess.Remove(tasksAreaLoadPsi[i].Result.Item1);
                                }
                            }

                            for (int i = 0; i < tasksLineLoadPsi.Count; i++)
                            {
                                if (tasksLineLoadPsi[i].Result.Item2 != -1)
                                {
                                    SetEquivalentThicknessEET(new[] { (tasksLineLoadPsi[i].Result.Item1, tasksLineLoadPsi[i].Result.Item2) });
                                    loadsToProcess.Remove(tasksLineLoadPsi[i].Result.Item1);
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

                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.Omega)
                    {

                    }
                    break;
                default:
                    throw new NotSupportedException();
            }


            
            // processo i rimanenti con il numerico
            if (loadsToProcess.Count() > 0)
            {
                SetEquivalentThicknessEET(GetPsiEETNumerical(loadsToProcess));
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
                                                                                         // andrebbe fatto un metodo per capire qual è "smallest dimension of bending of the laminate plate"
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

                EquivalentThicknessParameters parameters = new EquivalentThicknessParameters(loadCase.LoadDuration,
                                                            loadCase.Temperature, loads[i].GetGeometryBase(), loads[i].LoadRestrainCondition);

                _thicknessesW[parameters] = hw;
                _thicknessesStress[0][parameters] = Math.Sqrt(Math.Pow(hw, 3.0) / (h1 + 2.0 * lambda * hs2));
                _thicknessesStress[1][parameters] = Math.Sqrt(Math.Pow(hw, 3.0) / (h2 + 2.0 * lambda * hs1));
            }

        }

        protected void SetEquivalentThicknessEET((IGlassLoad load, double psi)[] loadsToProcessPsiValues)
        {
            var loads = loadsToProcessPsiValues.Select(i => i.load).ToArray();
            var psiValues = loadsToProcessPsiValues.Select(i => i.psi).ToArray();


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

            ConcurrentDictionary<IGlassLoad, double> twBuffer = new ConcurrentDictionary<IGlassLoad, double>();
            ConcurrentDictionary<IGlassLoad, double> tSigma1Buffer = new ConcurrentDictionary<IGlassLoad, double>();
            ConcurrentDictionary<IGlassLoad, double> tSigma2Buffer = new ConcurrentDictionary<IGlassLoad, double>();

            Action<int> action = new Action<int>((index) =>
            {
                double shearModule = glass.Interlayers[0].Material.GetShearModule(loads[index].GlassLoadCase.LoadDuration, loads[index].GlassLoadCase.Temperature);

                // eq. 6.55
                double eta2D = 1.0 / (1.0 + hint * E / (shearModule * oneMinusNiSquare) * DAbs / DFull * h1 * h2 / (h1 + h2) * psiValues[index]);

                double tw = Math.Pow(1.0 / (eta2D / firstDeno + (1.0 - eta2D) / secondDeno), 1.0 / 3.0);

                twBuffer[loads[index]] = tw;
                tSigma1Buffer[loads[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d1) / firstDeno + h1 / Math.Pow(tw, 3.0)));
                tSigma2Buffer[loads[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d2) / firstDeno + h2 / Math.Pow(tw, 3.0)));
            });


            // TODO: valutare se tenere parallel più for o solo for a livello prestazionale
            Parallel.ForEach(Enumerable.Range(0, loads.Count()), action);


            _thicknessesStress[0] = new Dictionary<EquivalentThicknessParameters, double>();
            _thicknessesStress[1] = new Dictionary<EquivalentThicknessParameters, double>();
            for (int i = 0; i < loads.Length; i++)
            {
                EquivalentThicknessParameters parameters = new EquivalentThicknessParameters(loads[i].GlassLoadCase.LoadDuration,
                                                                                             loads[i].GlassLoadCase.Temperature,
                                                                                             loads[i].GetGeometryBase(), loads[i].LoadRestrainCondition);

                _thicknessesW[parameters] = twBuffer[loads[i]];
                _thicknessesStress[0][parameters] = tSigma1Buffer[loads[i]];
                _thicknessesStress[1][parameters] = tSigma2Buffer[loads[i]];
            }

        }

        protected (IGlassLoad load, double psi)[] GetPsiEETNumerical(List<IGlassLoad> loads)
        {
            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only one interlayer.");


            LaminatedGlass glass = (LaminatedGlass)Glass;


            FemModel.FemModelWrapper femModel = new FemModel.FemModelWrapper("EETNumerical")
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
                              new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02 },
                              loads.Cast<Load>().ToList(), _glassSurface.GetRestrains());

            loads.ForEach(i => femModel.AddLoadCase(i.LoadCase));

            var combinations = new List<Combination>();
            for (int i = 0; i < loads.Count(); i++)
            {
                combinations.Add(new Combination($"Load {i}"));
                combinations[i].AddLoadCaseCoefficient(loads[i].LoadCase, 1);
            }

            femModel.AddCombinations(combinations);

            femModel.SaveFemModelToSt7(System.IO.Path.GetTempPath()); // TODO: rimuovere e passare a solutore interno

            femModel.Solve();

            double flexularRigidity = material.E * Math.Pow(plateThickness, 3.0) / (12.0 * (1.0 - Math.Pow(material.Ni, 2.0)));

            Model.FEM.FiniteElements.FiniteElement[] elements = femModel.GetElements();

            ConcurrentBag<(IGlassLoad load, double psi)> psiValues = new ConcurrentBag<(IGlassLoad load, double psi)>();

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
                                                    ((Combination)j.Case).ContainsLoadCases(new[] { (Model.LoadCases.LoadCaseBase)loads[loadIndex].LoadCase })).Result)
                                                        .Cast<ResultDisplacement>(); // TODO: cambiare in containsLoadCase

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
                    psiValues.Add((loads[loadIndex], double.MaxValue)); // se c'è qualcosa che non va (den == 0) allora diamo un psi grande che corrisponde ad eta 0 cioè layerered limit
                else
                    psiValues.Add((loads[loadIndex], num / den));
            });

            Parallel.ForEach(Enumerable.Range(0, loads.Count()), action);


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
        protected double GetEETAmnCoefficient(double csi, double eta, double u, double v, double a, double b, int m, int n)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
            return 1.0 / (m * n * Math.Pow(m * m / a / a + n * n / b / b, 2d)) * 16.0 / Math.Pow(Math.PI, 2d) *
                            Math.Sin(m * Math.PI * csi / a) * Math.Sin(m * Math.PI * u / 2d / a) *
                            Math.Sin(n * Math.PI * eta / b) * Math.Sin(n * Math.PI * v / 2d / b);
        }


        protected async Task<double> GetEETGxCoefficientAsync(double a, double b, double[,] ACoefficientsSquare)
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

        protected async Task<double> GetEETGyCoefficientAsync(double a, double b, double[,] ACoefficientsSquare)
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

        protected async Task<double> GetEETGpCoefficientAsync(double a, double b, double xi, double eta, double u, double v, double[,] ACoefficients)
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

        protected async Task<(IGlassLoad, double)> GetEETPsiConcentratedLoadAsync(IGlassLoad load, Models.Prototype.LaminatedEqThicknessParameters eqThicknessParameters)
        {
            // da foglio galuppi EET_plates_conc_NEW_REV02.xlsx
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

                    Vector3d normal = _glassSurface.Shape.GetNormalVector();
                    Vector3d lineVector = new Vector3d(line.Start, line.End);

                    Vector3d movementVector = normal.CrossProduct(lineVector);
                    movementVector /= 2.0;

                    Point3d p1 = (Point3d)line.Start.Clone();
                    Point3d p2 = (Point3d)line.End.Clone();
                    Point3d p3 = (Point3d)line.Start.Clone();
                    Point3d p4 = (Point3d)line.End.Clone();

                    p1.Move(movementVector);
                    p2.Move(movementVector);

                    movementVector.Reverse();
                    p3.Move(movementVector);
                    p4.Move(movementVector);

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
                                // se entra allora il lato è parallelo al glassperimeter[i]
                                isParallel = true;
                                break;
                            }
                        }

                        if (!isParallel)
                        {
                            // se entra qua allora il lato non è parallelo a nessuno
                            isShapeLoadParallel = false;
                            break;
                        }
                    }


                    if (isShapeLoadParallel)
                    {
                        var loadCenter = loadPerimeter.GetCentroid();
                        double xi = 0; // parallelo ad a
                        double eta = 0; // parallelo ad b 
                        double u = loadPerimeter[0].DistanceTo(loadPerimeter[1]);
                        double v = loadPerimeter[2].DistanceTo(loadPerimeter[3]);

                        if (glassPerimeter[0].GetLength() > glassPerimeter[1].GetLength())
                        {
                            // primo lato è il più grande
                            xi = glassPerimeter[0].DistanceTo(loadCenter);
                            eta = glassPerimeter[1].DistanceTo(loadCenter);
                        }
                        else
                        {
                            // primo lato è il più piccolo
                            eta = glassPerimeter[0].DistanceTo(loadCenter);
                            xi = glassPerimeter[1].DistanceTo(loadCenter);
                        }

                        double[,] ACoefficients = new double[10, 10];
                        double[,] ACoefficientsSquare = new double[10, 10];

                        Action<int> ACoefficientAction = new Action<int>((m) =>
                        {
                            // double[,] non è threadsafe ma ogni thread scrive su un punto diverso.
                            for (int n = 1; n <= 10; n++)
                            {
                                ACoefficients[m - 1, n - 1] = GetEETAmnCoefficient(xi, eta, u, v, eqThicknessParameters.A, eqThicknessParameters.B, m, n);
                                ACoefficientsSquare[m - 1, n - 1] = Math.Pow(ACoefficients[m - 1, n - 1], 2.0);
                            }
                        });

                        Parallel.For(1, 11, ACoefficientAction);

                        Task<double> gxTask = GetEETGxCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, ACoefficientsSquare);
                        Task<double> gyTask = GetEETGyCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, ACoefficientsSquare);
                        Task<double> gpTask = GetEETGpCoefficientAsync(eqThicknessParameters.A, eqThicknessParameters.B, xi, eta, u, v, ACoefficients);

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

            public bool Equals(double loadDuration, double temperature, GeometryBase loadGeometry, GlassSurface.LoadRestrainCondition restrainCondition)
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
