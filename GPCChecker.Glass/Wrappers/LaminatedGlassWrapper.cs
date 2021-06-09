
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
            return ((LaminatedGlass)Glass).GetTotalThickness();
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

        public override double GetElasticModulus()
        {
            return ((LaminatedGlass)Glass).GetElasticModulus();
        }

        public override double GetPoissonRatios()
        {
            return ((LaminatedGlass)Glass).GetPoissonRatios();
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return ((LaminatedGlass)Glass).GetSelfWeightPerUnitArea();
        }

        public override double GetSelfWeightTotal()
        {
            return _glassSurface.Shape.GetArea() * GetSelfWeightPerUnitArea();
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
            
            switch (_glassSurface.Prototype.LaminatedEqThicknessMethod)
            {
                case Models.Prototype.LaminatedEqThicknessMethods.EET:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
                    break;
                
                case Models.Prototype.LaminatedEqThicknessMethods.ASTME1300:

                    if (((LaminatedGlass)Glass).GlassLayerCount > 2) 
                        throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessMethod} does support only two layers glass.");

                    if (((LaminatedGlass)Glass).InterlayerCount > 1)
                        throw new NotSupportedException($"{_glassSurface.Prototype.LaminatedEqThicknessMethod} does support only one interlayer.");

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

                    foreach (Load load in _externalFaceLoads.Union(_internalFaceLoads).Distinct())
                    {
                        var shearModule = glass.Interlayers[0].Material.GetShearModule(((LoadCase)load.LoadCase).LoadDuration, ((LoadCase)load.LoadCase).Temperature);

                        double lambda = 1.0 / (1.0 + (9.6 * elasticModulus * Is * hv) / (shearModule * hsSquare * aSquare)); // Shear transfer coefficient

                        double hw = Math.Pow(hs1Square + hs2Square + 12.0 * lambda * Is, 1.0 / 3.0);

                        _thicknessesW[(((LoadCase)load.LoadCase).LoadDuration, ((LoadCase)load.LoadCase).Temperature)] = hw;
                        _thicknessesStress[0][(((LoadCase)load.LoadCase).LoadDuration, ((LoadCase)load.LoadCase).Temperature)] = Math.Sqrt(Math.Pow(hw, 3.0) / (h1 + 2.0 * lambda * hs2));
                        _thicknessesStress[1][(((LoadCase)load.LoadCase).LoadDuration, ((LoadCase)load.LoadCase).Temperature)] = Math.Sqrt(Math.Pow(hw, 3.0) / (h2 + 2.0 * lambda * hs1));
                    }

                    break;
                
                case Models.Prototype.LaminatedEqThicknessMethods.NEN:

                    throw new NotImplementedException();
                    //break;
                
                case Models.Prototype.LaminatedEqThicknessMethods.Omega:
                    throw new NotImplementedException();
                    //break;

                default:
                    throw new NotSupportedException();
            }
        }

        #endregion
    }
}
