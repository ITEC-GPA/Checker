
using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Elements;
using GPC.Model.Restrains;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;

namespace GPC.Checkers.Glasses.Wrappers
{
    public abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
    {

        public enum GlassPanelPositions
        {
            Internal,
            Central,
            External
        }


        protected Mesh[] _meshes;

        protected List<Load> _externalFaceLoads;
        protected List<Load> _internalFaceLoads;
        protected Loads.SelfWeightLoad _selfWeightLoad;

        protected List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>> _meshGeometryRestrainVertices;
        protected List<KeyValuePair<Mesh, Dictionary<Load, int[]>>> _meshLoadsVertexIndexes;
        protected List<KeyValuePair<Mesh, Dictionary<Load, int[]>>> _meshLoadsFaceIndexes;

        protected bool _meshComputed = false;

        protected GlassPanelPositions _glassPanelPositions;


        #region Properties

        public bool ConsiderSelfWeight => _selfWeightLoad != null;

        internal List<GeometryRestrain> GeometryRestrains => _glassSurface.GetRestrains();

        /// <summary>
        /// If meshes has not been generated yet, it will call <see cref="GlassWrapper.GenerateMesh()"/> 
        /// </summary>
        public Mesh[] Meshes
        {
            get
            {
                if (!_meshComputed)
                    GenerateMesh();
                return _meshes;
            }
        }

        public List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>> MeshGeometryRestrainVertices
        {
            get
            {
                if (!_meshComputed)
                    GenerateMesh();
                return _meshGeometryRestrainVertices;
            }
        }

        public List<KeyValuePair<Mesh, Dictionary<Load, int[]>>> MeshLoadsVertexIndexes
        {
            get
            {
                if (!_meshComputed)
                    GenerateMesh();
                return _meshLoadsVertexIndexes;
            }
        }

        public List<KeyValuePair<Mesh, Dictionary<Load, int[]>>> MeshLoadsFaceIndexes
        {
            get
            {
                if (!_meshComputed)
                    GenerateMesh();
                return _meshLoadsFaceIndexes;
            }
        }

        public Loads.SelfWeightLoad SelfWeightLoad => _selfWeightLoad;

        #endregion


        protected GlassPanelWrapper(GlassSurface glassSurface, IGlassPanel glass) 
            : this(glassSurface, glass, GlassPanelPositions.External)
        {

        }

        protected GlassPanelWrapper(GlassSurface glassSurface, IGlassPanel glass, GlassPanelPositions glassPanelPosition)
            : base(glassSurface, (Glass)glass)
        {
            _externalFaceLoads = new List<Load>();
            _internalFaceLoads = new List<Load>();

            _meshes = new Mesh[glass.GetGlassPackage().Length];

            _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>();
            _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();
            _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>();

            _glassPanelPositions = glassPanelPosition;
        }

        #region Public methods - geometry


        /// <returns>Glass thickness for deformation analysis</returns>
        public abstract double GetDeformationThickness(string loadCaseName);

        /// <returns>Glass thickness for stress analysis</returns>
        public abstract double[] GetStressThickness(string loadCaseName);


        /// <returns>Total thickness of the glass package included interlayer</returns>
        public abstract double GetTotalThickness();

        public abstract double GetElasticModulus();

        public abstract double GetPoissonRatio();

        /// <returns>Weight divided per area. [L * M / L^3]  T / mm2</returns>
        public abstract double GetSelfWeightPerUnitArea();

        /// <returns>Total weight. [M]</returns>
        public abstract double GetSelfWeightTotal();

        public abstract double GetDensity();

        #endregion


        #region Public methods - analysis

        /// <remarks>The load will be added only if it is different from <see cref="SelfWeightLoad"/></remarks>
        public void AddExternalFaceLoad(IGlassLoad load)
        {
            if (load is Loads.SelfWeightLoad)
                return;
            else
                _externalFaceLoads.Add((Load)load);
        }

        /// <remarks>The load will be added only if it is different from <see cref="SelfWeightLoad"/></remarks>
        public void AddExternalFaceLoads(IEnumerable<IGlassLoad> loads)
        {
            var loadCasted = loads.Cast<Load>().ToList();
            _externalFaceLoads.AddRange(loadCasted.Where(i => !(i is Loads.SelfWeightLoad)).ToList());
        }

        /// <remarks>The load will be added only if it is different from <see cref="SelfWeightLoad"/></remarks>
        public void AddInternalFaceLoad(IGlassLoad load)
        {
            if (load is Loads.SelfWeightLoad)
                return;
            else
                _internalFaceLoads.Add((Load)load);
        }

        /// <remarks>The load will be added only if it is different from <see cref="SelfWeightLoad"/></remarks>
        public void AddInternalFaceLoads(IEnumerable<IGlassLoad> loads)
        {
            var loadCasted = loads.Cast<Load>().ToList();
            _internalFaceLoads.AddRange(loadCasted.Where(i => !(i is Loads.SelfWeightLoad)).ToList());
        }

        public void AddSelfWeightLoad(Loads.SelfWeightLoad load)
        {
            load.GlassPanelPosition = _glassPanelPositions;

            _selfWeightLoad = load;
        }


        /// <summary>Split the InteralFaceLoads and ExternalFaceLoads in three list based on their geometry and type </summary>
        /// <param name="uniformPressureLoads">List of load acting as uniform pressure</param>
        /// <param name="notUniformPressureLoads">List of load acting as not uniform pressure</param>
        /// <param name="nonUniformLoadsGeometry">List of geometries associated to the not uniform loads</param>
        public void GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads, out List<GeometryBase> nonUniformLoadsGeometry)
        {
            uniformPressureLoads = _externalFaceLoads.Where(i => i.GetGeometryBase() == _glassSurface.Shape).ToList();
            uniformPressureLoads.AddRange(_internalFaceLoads.Where(i => i.GetGeometryBase() == _glassSurface.Shape).ToList());

            notUniformPressureLoads = _externalFaceLoads.Except(uniformPressureLoads).ToList();
            notUniformPressureLoads.AddRange(_internalFaceLoads.Except(uniformPressureLoads).ToList());

            nonUniformLoadsGeometry = notUniformPressureLoads.Select(i => i.GetGeometryBase()).ToList();
        }


        public Mesh GetGlassMeshInternal()
        {
            if (_meshes == null)
                throw new ArgumentNullException();
            return _meshes.Last();
        }

        /// <summary>
        /// return the mesh at index 
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public Mesh GetGlassMesh(int index)
        {
            if (_meshes == null)
                throw new ArgumentNullException();
            return _meshes[index];
        }


        public Mesh GetGlassMeshExternal()
        {
            if (_meshes == null)
                throw new ArgumentNullException();
            return _meshes.First();
        }


        /// <summary>
        /// Generate the mesh of a single glass layer
        /// </summary>
        protected bool GenerateSingleLayerMesh(out Mesh mesh, out Dictionary<GeometryRestrain, int[]> meshGeometryRestrainVertices, 
                                                              out Dictionary<Load, int[]> meshLoadsVertexIndexes, 
                                                              out Dictionary<Load, int[]> meshLoadsFaceIndexes)
        {

            var shapes = new List<Shape>
            {
                _glassSurface.Shape
            };

            // VINCOLI
            List<GeometryBase> embeddedGeometryRestrains = new List<GeometryBase>();

            // Creo geometria embedded relativa ai vincoli
            List<GeometryRestrain> restrains = this.GeometryRestrains;
            if (restrains != null)
            {
                foreach (var restrain in restrains)
                {
                    if (restrain is PointRestrain pr)
                    {
                        embeddedGeometryRestrains.Add(pr.Point);
                    }
                    else if (restrain is LineRestrain lr)
                    {
                        embeddedGeometryRestrains.Add(lr.Line);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            // CARICHI - Sia su faccia interna che esterna
            GetLoads(out List<Load> uniformPressureLoads, out List<Load> nonUniformLoads, out List<GeometryBase> nonUniformLoadsGeometry);

            // MESH
            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            
            if (embeddedGeometryRestrains.Count > 0)
                embeddedGeometries[_glassSurface.Shape] = embeddedGeometryRestrains.ToArray();

            if (nonUniformLoadsGeometry.Count > 0)
            {
                if (embeddedGeometries.ContainsKey(_glassSurface.Shape))
                {
                    var buffer = embeddedGeometries[_glassSurface.Shape].ToList();
                    buffer.AddRange(nonUniformLoadsGeometry);

                    embeddedGeometries[_glassSurface.Shape] = buffer.ToArray();
                }
                else
                    embeddedGeometries[_glassSurface.Shape] = nonUniformLoadsGeometry.ToArray();
            }


            meshGeometryRestrainVertices = new Dictionary<GeometryRestrain, int[]>();
            meshLoadsVertexIndexes = new Dictionary<Load, int[]>();
            meshLoadsFaceIndexes = new Dictionary<Load, int[]>();
            mesh = null;
            if (Mesh.Generate(shapes, embeddedGeometries, _glassSurface.MeshOptions, out List<Mesh> meshesBuffer, out Mesh.GenerateMeshStatus generateMeshStatus))
            {
                mesh = meshesBuffer.First();

                // Recupero vertici embedded
                foreach (var geom in generateMeshStatus.EmbeddedGeometriesVertexMap)
                {
                    Mesh meshKey = geom.Key;
                    Dictionary<GeometryBase, int[]> embeddedGeometriesIndexes = geom.Value;

                    foreach (var geometry in embeddedGeometriesIndexes.Keys)
                    {
                        var matchingRestrains = restrains.Where(i => i.GetGeometry() == geometry);
                        var matchingNonUniformLoadsGeometry = nonUniformLoads.Where(i => i.GetGeometryBase() == geometry);

                        if (matchingRestrains.Count() > 0)
                        {
                            foreach (var geomRestrain in matchingRestrains)
                            {
                                meshGeometryRestrainVertices.Add(geomRestrain, embeddedGeometriesIndexes[geometry]);
                            }
                        }

                        if (matchingNonUniformLoadsGeometry.Count() > 0)
                        {
                            foreach (var load in matchingNonUniformLoadsGeometry)
                            {
                                if (load is IPointLoad || load is ILineLoad)
                                {
                                    meshLoadsVertexIndexes.Add(load, embeddedGeometriesIndexes[geometry]);
                                }
                                else if (load is IAreaLoad)
                                {
                                    meshLoadsFaceIndexes.Add(load, embeddedGeometriesIndexes[geometry]);
                                }
                                else
                                    throw new NotSupportedException();
                            }
                        }
                    }

                }

                if (uniformPressureLoads.Count > 0)
                {
                    var indexes = mesh.Faces.Select(i => i.Id).ToArray();

                    foreach (var load in uniformPressureLoads)
                    {
                        meshLoadsFaceIndexes.Add(load, indexes);
                    }
                }

                return true;
            }
            else
            {

                if (generateMeshStatus.GetLastException() != null)
                    throw generateMeshStatus.GetLastException();

                return false;
            }
            

        }

        #endregion

    }
}
