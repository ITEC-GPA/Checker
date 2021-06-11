
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

namespace GPC.Checkers.Glasses.Wrappers
{
    public class LaminatedGlassWrapper : GlassPanelWrapper
    {

        protected const int INTERLAYER_DISCRETIZATION = 3;

        /// <summary>
        /// Thickness associates to displacement
        /// </summary>
        protected Dictionary<(double loadDuration, double temperature), double> _thicknessesW;

        /// <summary>
        /// Thickness associates to stress for each glass layer 
        /// </summary>
        protected Dictionary<(double loadDuration, double temperature), double>[] _thicknessesStress;
        
        /// <summary>
        /// List of upper and lower vertices Id of each volume layer
        /// </summary>
        protected (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices)[] _volumeUpperLowerVerticesIds;

        
        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass) 
            : this(glassSurface, glass, GlassPanelPositions.External)
        {                       

        }

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass, GlassPanelPositions position)
            : base(glassSurface, glass, position)
        {
            _thicknessesW = new Dictionary<(double loadDuration, double temperature), double>();
            _thicknessesStress = new Dictionary<(double loadDuration, double temperature), double>[glass.GlassLayerCount]; 

            _volumeUpperLowerVerticesIds = new (IEnumerable<int> lowerVertices, IEnumerable<int> upperVertices)[glass.GlassLayerCount + glass.InterlayerCount];
        }

        #endregion

        #region Geometry

        public override double GetDeformationThickness(double loadDuration, double temperature)
        {
            if (_thicknessesW.Keys.Count == 0)
            {
                CalculateEquivalentThicknesses();
                if (_thicknessesW.Keys.Count == 0)
                    throw new ArgumentException();
            }


            if (_thicknessesW.ContainsKey((loadDuration, temperature)))
            {
                return _thicknessesW[(loadDuration, temperature)];
            }
            else
            {
                // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                throw new NotSupportedException($"Deformation thickness not found for load duration: {loadDuration} and temperature: {temperature}");
            }

        }


        public override double[] GetStressThickness(double loadDuration, double temperature)
        {
            if (_thicknessesStress.Length == 0)
            {
                CalculateEquivalentThicknesses();
                if (_thicknessesStress.Length == 0)
                    throw new ArgumentException();
            }

            double[] stressThickness = new double[((LaminatedGlass)Glass).GlassLayerCount];

            for (int i = 0; i < _thicknessesStress.Length; i++)
            {
                if (_thicknessesStress[i].ContainsKey((loadDuration, temperature)))
                {
                    stressThickness[i] = _thicknessesStress[i][(loadDuration, temperature)];
                }
                else
                {
                    // da valutare se inserire interpolazione valori. Ocio che il valore dello spessore non varia linearmente
                    throw new NotSupportedException($"Stress thickness not found for layer: {i} " +
                                                    $"load duration: {loadDuration} and temperature: {temperature}");
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
        public override double GetPoissonRatios()
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
        public void CalculateEquivalentThicknesses()
        {
            var eqThicknessParameters = _glassSurface.Prototype.LaminatedEqThicknessParameter;
            var boundaryCondition = eqThicknessParameters.LaminatedEqThicknessBoundaryCondition;
            var eqThicknessMethod = eqThicknessParameters.LaminatedEqThicknessMethod;


            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{eqThicknessMethod} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{eqThicknessMethod} does support only one interlayer.");


            // vengono filtrati in base a loadDuration, temperature e stessa geometria
            List<Load> loadsToProcess = _externalFaceLoads.Union(_internalFaceLoads).Distinct(new Loads.LoadDurationAndTemperatureEqualityComparer()).ToList();


            
            // Calcola lo spessore equivalente per i casi supportati, altrimenti usa eet numerico
            switch (boundaryCondition)
            {
                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.Other:

                    SetEquivalentThicknessEET(loadsToProcess, GetPsiEETNumerical(loadsToProcess).ToList());

                    return; // già processati tutti, ritorna

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularOneSideClamped:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        List<Load> areaLoads = loadsToProcess.Where(i => i.GetType() == typeof(NormalAreaLoad)).ToList();

                        if (areaLoads.Count() > 0)
                        {
                            areaLoads.ForEach(i => loadsToProcess.Remove(i));

                            double psi = 14.0 * 5.0 / eqThicknessParameters.A;

                            SetEquivalentThicknessEET(areaLoads, areaLoads.Select(i => psi).ToList());
                        }
                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularTwoSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {
                        
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.ASTME1300)
                    {
                        SetEquivalentThicknessASTM(loadsToProcess);
                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.NEN)
                    {
                        
                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularThreeSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {

                    }
                    else if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.NEN)
                    {

                    }
                    break;

                case Models.Prototype.LaminatedEqThicknessBoundaryConditions.RectangularFourSidesSimplySupported:

                    if (eqThicknessMethod == Models.Prototype.LaminatedEqThicknessMethods.EET)
                    {

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
                SetEquivalentThicknessEET(loadsToProcess, GetPsiEETNumerical(loadsToProcess).ToList());
            }
        }


        protected void SetEquivalentThicknessASTM(List<Load> loads)
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

            double a = _glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Min(); // in casi regolari funziona,
                                                                                            // in casi irregolari non tanto bene
                                                                                            // es. un poligono di 5 lati con uno dei lati molto piccolo
                                                                                            // andrebbe fatto un metodo per capire qual è "smallest dimension of bending of the laminate plate"
            
            double Is = h1 * Math.Pow(hs2, 2.0) + h2 * Math.Pow(hs1, 2.0);

            double aSquare = Math.Pow(a, 2);
            double hsSquare = Math.Pow(hs, 2);

            double hs1Square = Math.Pow(hs1, 2);
            double hs2Square = Math.Pow(hs2, 2);

            for (int i = 0; i < loads.Count; i++)
            {
                var shearModule = glass.Interlayers[0].Material.GetShearModule(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature);

                double lambda = 1.0 / (1.0 + (9.6 * elasticModulus * Is * hv) / (shearModule * hsSquare * aSquare)); // Shear transfer coefficient

                double hw = Math.Pow(hs1Square + hs2Square + 12.0 * lambda * Is, 1.0 / 3.0);

                _thicknessesW[(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = hw;
                _thicknessesStress[0][(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = Math.Sqrt(Math.Pow(hw, 3.0) / (h1 + 2.0 * lambda * hs2));
                _thicknessesStress[1][(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = Math.Sqrt(Math.Pow(hw, 3.0) / (h2 + 2.0 * lambda * hs1));
            }

        }


        protected void SetEquivalentThicknessEET(List<Load> loads, List<double> psiFactors)
        {

            if (loads.Count() != psiFactors.Count())
                throw new ArgumentOutOfRangeException();


            LaminatedGlass glass = (LaminatedGlass)Glass;

            double h1 = glass.MonolithicGlasses[0].Thickness;
            double h2 = glass.MonolithicGlasses[1].Thickness;
            double hint = glass.Interlayers[0].Thickness;

            double E = GetElasticModulus();
            double ni = GetPoissonRatios();

            double d = 0.5 * (h1 + h2) + hint; // eq. 6.37
            double d1 = d * h2 / (h1 + h2);
            double d2 = d * h1 / (h1 + h2);

            double oneMinusNiSquare = 1.0 - Math.Pow(ni, 2.0);
            double h1Cube = Math.Pow(h1, 3.0);
            double h2Cube = Math.Pow(h2, 3.0);

            // eq. 6.52
            double DAbs = E * h1Cube / (12.0 * (1.0 - oneMinusNiSquare)) + E * h2Cube / (12.0 * (1.0 - oneMinusNiSquare));

            // eq. 6.53
            double DFull = DAbs + E / oneMinusNiSquare * h1 * h2 / (h1 + h2) * Math.Pow(d, 2.0);

            // eq 6.46
            double secondDeno = h1Cube + h2Cube;

            // eq 6.46
            double firstDeno = secondDeno + 12 * (h1 * Math.Pow(d1, 2.0) + h2 * Math.Pow(d2, 2.0));

            ConcurrentDictionary<Load, double> twBuffer = new ConcurrentDictionary<Load, double>();
            ConcurrentDictionary<Load, double> tSigma1Buffer = new ConcurrentDictionary<Load, double>();
            ConcurrentDictionary<Load, double> tSigma2Buffer = new ConcurrentDictionary<Load, double>();


            Action<int> action = new Action<int>((index) =>
            {                
                double shearModule = glass.Interlayers[0].Material.GetShearModule(((LoadCase)loads[index].LoadCase).LoadDuration, ((LoadCase)loads[index].LoadCase).Temperature);

                // eq. 6.55
                double eta2D = 1.0 / (1.0 + hint * E / (shearModule * oneMinusNiSquare)) * DAbs / DFull * h1 * h2 / (h1 + h2) * psiFactors[index];

                double tw = Math.Pow(1 / (eta2D / firstDeno + (1.0 - eta2D) / secondDeno), 1.0 / 3.0);

                twBuffer[loads[index]] = tw;
                tSigma1Buffer[loads[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d1) / firstDeno + h1 / Math.Pow(tw, 3.0)));
                tSigma2Buffer[loads[index]] = Math.Sqrt(1.0 / (2.0 * eta2D * Math.Abs(d2) / firstDeno + h2 / Math.Pow(tw, 3.0)));
            });


            Parallel.ForEach(Enumerable.Range(0, loads.Count()), action);


            // TODO: valutare se tenere parallel più for o solo for a livello prestazionale
            for (int i = 0; i < loads.Count; i++)
            {
                _thicknessesW[(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = twBuffer[loads[i]];
                _thicknessesStress[0][(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = tSigma1Buffer[loads[i]];
                _thicknessesStress[1][(((LoadCase)loads[i].LoadCase).LoadDuration, ((LoadCase)loads[i].LoadCase).Temperature)] = tSigma2Buffer[loads[i]];
            }

        }


        protected double[] GetPsiEETNumerical(List<Load> loads)
        {
            if (((LaminatedGlass)Glass).GlassLayerCount > 2)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only two layers glass.");

            if (((LaminatedGlass)Glass).InterlayerCount > 1)
                throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessParameter} does support only one interlayer.");


            LaminatedGlass glass = (LaminatedGlass)Glass;

            FemModel.FemModelWrapper femModel = new FemModel.FemModelWrapper("EETNumerical");

            IsotropicFemMaterial material = new IsotropicFemMaterial(GetElasticModulus(), GetPoissonRatios(), 0, GetDensity());

            double plateThickness = 1;
            PlateProperty property = new PlateProperty(material, plateThickness, plateThickness, "p1");
            femModel.AddProperty(property);

            var bbboxSize = _glassSurface.Shape.ToLocal().GetBoundingBox().Size;
            
            // dimensione mesh di default 2% del massimo lato della bbox. Alla Straus
            femModel.AddShape(_glassSurface.Shape, "p1", 
                              new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02 }, 
                              loads, _glassSurface.GetRestrains());

            femModel.SaveFemModelToSt7(System.IO.Path.GetTempPath()); // TODO: rimuovere e passare a solutore interno

            femModel.Solve();

            double flexularRigidity = material.E * Math.Pow(plateThickness, 3.0) / (12.0 * (1.0 - Math.Pow(material.Ni, 2.0)));

            Model.FEM.FiniteElements.FiniteElement[] elements = femModel.GetElements();

            double num = 0;
            double den = 0;

            for (int e = 0; e < elements.Length; e++)
            {
                if (elements[e] is Model.FEM.FiniteElements.Plate plate)
                {
                    var elementArea = plate.GetArea();

                    IEnumerable<ResultDisplacement> resultDisplacement = plate.Nodes.Select(i => i.Results.FirstOrDefault().Result).Cast<ResultDisplacement>();

                    var mean = ResultDisplacement.GetArithmeticMean(resultDisplacement.ToArray());

                    if (plate.AttributesLoadCase.Where(i => i is Model.FEM.Attributes.PlateNormalPressureAttribute).SingleOrDefault() != null)
                    {
                        num += mean.D3 * flexularRigidity * elementArea * ((Model.FEM.Attributes.PlateNormalPressureAttribute)plate.AttributesLoadCase.FirstOrDefault()).Pressure;
                    }

                    den += (Math.Pow(mean.R1 * flexularRigidity, 2.0) + Math.Pow(mean.R2 * flexularRigidity, 2.0)) * elementArea;
                }
            }

            double psi = Math.Abs(num / den); // * Math.Pow(10, 6));


            return new[] { psi };
        }





        #endregion
    }
}
