
using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Elements;
using GPC.Model.Restrains;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using GPC.Checker.Glasses.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
    {

        protected List<Mesh> _meshes;

        protected List<Load> _externalFaceLoads;
        protected List<Load> _internalFaceLoads;

        protected List<GeometryRestrain> _geometryRestrain;
             
        protected Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> _meshGeometryRestrainVertices;
        protected Dictionary<Mesh, Dictionary<Load, int[]>> _meshLoadsVertexIndexes;
        protected Dictionary<Mesh, Dictionary<Load, int[]>> _meshLoadsFaceIndexes;

        #region Properties

        /// <summary>
        /// The loads applied on the GlassPanelWrapper 
        /// </summary>
        internal List<Load> Loads => _externalFaceLoads;

        internal List<GeometryRestrain> GeometryRestrains => _geometryRestrain;

        /// <summary>
        /// If meshes has not been generated yet, it will call <see cref="GlassWrapper.SetUpMeshOptions()"/> and then <see cref="GlassWrapper.GenerateMesh()"/> />
        /// </summary>
        public List<Mesh> Meshes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    GenerateMesh();
                    return _meshes;
                }
                else
                    return _meshes;
            }
        }

        public Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> MeshGeometryRestrainVertices
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    GenerateMesh();
                    return _meshGeometryRestrainVertices;
                }
                else
                    return _meshGeometryRestrainVertices;
            }
        }

        public Dictionary<Mesh, Dictionary<Load, int[]>> MeshLoadsVertexIndexes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    GenerateMesh();
                    return _meshLoadsVertexIndexes;
                }
                else
                    return _meshLoadsVertexIndexes;
            }
        }

        public Dictionary<Mesh, Dictionary<Load, int[]>> MeshLoadsFaceIndexes
        {
            get
            {
                if (_meshes.Count == 0)
                {
                    GenerateMesh();
                    return _meshLoadsFaceIndexes;
                }
                else
                    return _meshLoadsFaceIndexes;
            }
        }

        #endregion


        protected GlassPanelWrapper(GlassSurface glassSurface, IGlassPanel glass) 
            : base(glassSurface, (Glass)glass)
        {
            this._externalFaceLoads = new List<Load>();
            this._internalFaceLoads = new List<Load>();

            this._meshes = new List<Mesh>();
            this._meshGeometryRestrainVertices = new Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>>();
            this._meshLoadsVertexIndexes = new Dictionary<Mesh, Dictionary<Load, int[]>>();
            this._meshLoadsFaceIndexes = new Dictionary<Mesh, Dictionary<Load, int[]>>();
        }

        #region Public methods - geometry

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loadDuration"></param>
        /// <returns>Glass thickness for deformation analysis</returns>
        public abstract double GetDeformationThickness(double loadDuration);

        /// <summary>
        ///
        /// </summary>
        /// <param name="loadDuration"></param>
        /// <returns>Glass thickness for stress analysis</returns>
        public abstract double GetStressThickness(double loadDuration);

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Total thickness of the glass package included interlayer</returns>
        public abstract double GetTotalThickness();

        public abstract double GetElasticModulus();

        public abstract double GetPoissonRatios();

        public abstract double GetSelfWeightPerUnitArea();

        public abstract double GetSelfWeightTotal();

        #endregion


        #region Public methods - analysis

        public void AddExternalFaceLoad(Load load)
        {
            _externalFaceLoads.Add(load);
        }

        public void AddExternalFaceLoads(List<Load> loads)
        {
            _externalFaceLoads.AddRange(loads);
        }

        public void AddInternalFaceLoad(Load load)
        {
            _internalFaceLoads.Add(load);
        }

        public void AddInternalFaceLoads(List<Load> loads)
        {
            _internalFaceLoads.AddRange(loads);
        }

        //public List<GeometryRestrain> GetRestrains()
        //{
        //    var list = new List<GeometryRestrain>();

        //    return list;
        //}

        public void GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads)
        {
            uniformPressureLoads = _externalFaceLoads.Where(i => i.GetGeometryBase() == _glassSurface.Shape).ToList();
            uniformPressureLoads.AddRange(_internalFaceLoads.Where(i => i.GetGeometryBase() == _glassSurface.Shape).ToList());

            notUniformPressureLoads = _externalFaceLoads.Except(uniformPressureLoads).ToList();
            uniformPressureLoads.AddRange(_internalFaceLoads.Except(uniformPressureLoads).ToList());
        }

        public Mesh GetInternalGlassMesh()
        {
            if (_meshes == null)
                throw new ArgumentNullException();
            return _meshes.Last();
        }

        public Mesh GetExternalGlassMesh()
        {
            if (_meshes == null)
                throw new ArgumentNullException();
            return _meshes.First();
        }


        /// <summary>
        /// Generate the mesh of a single glass layer
        /// </summary>
        protected bool GenerateSinglePanelMesh(out Mesh mesh, out Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>> meshGeometryRestrainVertices, 
                                                              out Dictionary<Mesh, Dictionary<Load, int[]>> meshLoadsVertexIndexes, 
                                                              out Dictionary<Mesh, Dictionary<Load, int[]>> meshLoadsFaceIndexes)
        {

            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            // VINCOLI
            List<GeometryBase> embeddedGeometryRestrains = new List<GeometryBase>();

            // Creo geometria embedded relativa ai vincoli
            List<GeometryRestrain> restrains = _glassSurface.GetRestrains();
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

            // CARICHI
            // Creo geometria embedded relativa ai carichi - se è diversa dalla superficie di partenza.

            List<GeometryBase> nonUniformLoadsGeometry = new List<GeometryBase>();

            List<Load> nonUniformLoads = _internalFaceLoads.Where(i => i.GetGeometryBase() != _glassSurface.Shape).ToList();
            nonUniformLoads.AddRange(_externalFaceLoads.Where(i => i.GetGeometryBase() != _glassSurface.Shape).ToList());

            nonUniformLoadsGeometry.AddRange(nonUniformLoads.Select(i => i.GetGeometryBase()).ToList());

            List<Load> uniformPressureLoads = _internalFaceLoads.Where(i => i.GetGeometryBase() == _glassSurface.Shape).ToList();
            uniformPressureLoads.AddRange(_externalFaceLoads.Where(i => i.GetGeometryBase() != _glassSurface.Shape).ToList());


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


            meshGeometryRestrainVertices = new Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>>();
            meshLoadsVertexIndexes = new Dictionary<Mesh, Dictionary<Load, int[]>>();
            meshLoadsFaceIndexes = new Dictionary<Mesh, Dictionary<Load, int[]>>();
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
                                if (!meshGeometryRestrainVertices.ContainsKey(meshKey))
                                    meshGeometryRestrainVertices.Add(meshKey, new Dictionary<GeometryRestrain, int[]>());

                                meshGeometryRestrainVertices[meshKey].Add(geomRestrain, embeddedGeometriesIndexes[geometry]);
                            }
                        }

                        if (matchingNonUniformLoadsGeometry.Count() > 0)
                        {
                            foreach (var load in matchingNonUniformLoadsGeometry)
                            {
                                if (load is IPointLoad || load is ILineLoad)
                                {
                                    if (!meshLoadsVertexIndexes.ContainsKey(meshKey))
                                        meshLoadsVertexIndexes.Add(meshKey, new Dictionary<Load, int[]>());

                                    meshLoadsVertexIndexes[meshKey].Add(load, embeddedGeometriesIndexes[geometry]);
                                }
                                else if (load is IAreaLoad)
                                {
                                    if (!meshLoadsFaceIndexes.ContainsKey(meshKey))
                                        meshLoadsFaceIndexes.Add(meshKey, new Dictionary<Load, int[]>());

                                    meshLoadsFaceIndexes[meshKey].Add(load, embeddedGeometriesIndexes[geometry]);
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
                        if (!meshLoadsFaceIndexes.ContainsKey(mesh))
                            meshLoadsFaceIndexes.Add(mesh, new Dictionary<Load, int[]>());

                        meshLoadsFaceIndexes[mesh].Add(load, indexes);
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
