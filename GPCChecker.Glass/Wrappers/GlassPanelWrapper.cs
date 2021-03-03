
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
        #region Variables

        protected List<Load> _loads;

        protected List<GeometryRestrain> _geometryRestrain;

        #endregion

        #region Properties

        internal List<Load> Loads => _loads;

        internal List<GeometryRestrain> GeometryRestrains => _geometryRestrain;

        protected new IGlassPanel Glass => (IGlassPanel)_glass;

        //public List<Mesh> Meshes { get { return _meshes; } protected set { _meshes = value; } }

        
        #endregion


        protected GlassPanelWrapper(GlassSurface glassSurface, IGlassPanel glass) 
            : base(glassSurface, (Glass)glass)
        {
            _loads = new List<Load>();
            

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

        public void AddLoad(Load load)
        {
            _loads.Add(load);
        }

        public void AddLoads(List<Load> loads)
        {
            _loads.AddRange(loads);
        }

        //public List<GeometryRestrain> GetRestrains()
        //{
        //    var list = new List<GeometryRestrain>();

        //    return list;
        //}

        public void GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads)
        {
            uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

            notUniformPressureLoads = _loads.Except(uniformPressureLoads).ToList();
        }

        public override void GenerateMesh()
        {
            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            List<GeometryBase> embeddedGeometryRestrains = new List<GeometryBase>();
            List<GeometryBase> embeddedLoads = new List<GeometryBase>();

            List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

            //// Aggiungo geometria relativa a vincoli            
            var restrains = _glassSurface.GetRestrains();
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

            // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
            embeddedLoads.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());

            //List<Load> uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

            // Meshatura
            Dictionary<Mesh, Dictionary<GeometryBase, int[]>> embeddedGeometriesMapVertex = new Dictionary<Mesh, Dictionary<GeometryBase, int[]>>();
            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            
            if (embeddedGeometryRestrains.Count > 0)
                embeddedGeometries[_glassSurface.Shape] = embeddedGeometryRestrains.ToArray();

            if (embeddedLoads.Count > 0)
            {
                if (embeddedGeometries.ContainsKey(_glassSurface.Shape))
                    embeddedGeometries[_glassSurface.Shape].Concat(embeddedLoads.ToArray());
                else
                    embeddedGeometries[_glassSurface.Shape] = embeddedLoads.ToArray();
            }


            SetUpMeshOptions();
            List<Mesh> meshes = Mesh.Generate(shapes, embeddedGeometries, out embeddedGeometriesMapVertex);

            _meshGeometryRestrainVertices = new Dictionary<Mesh, Dictionary<GeometryRestrain, int[]>>();
            // Recupero vertici embedded
            foreach (var geom in embeddedGeometriesMapVertex)
            {
                foreach(var geometry in geom.Value.Keys)
                {
                    var matchingRestrains = restrains.Where(i => i.GetGeometry() == geometry);

                    if (matchingRestrains.Count() > 0)
                    {
                        foreach (var restrain in matchingRestrains)
                        {
                            if (!_meshGeometryRestrainVertices.ContainsKey(geom.Key))
                                _meshGeometryRestrainVertices.Add(geom.Key, new Dictionary<GeometryRestrain, int[]>());

                            _meshGeometryRestrainVertices[geom.Key].Add(restrain, geom.Value[geometry]);
                        }
                    }
                }
            }

            _meshes = meshes;
        }

        //public Mesh GenerateGeometryMesh(out Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex)
        //{
        //    List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

        //    Dictionary<Mesh, Dictionary<GeometryBase, int[]>> _embeddedGeometriesMapVertex;

        //    // Aggiungo geometria relativa a vincoli
        //    //embeddedGeometriesBuffer.AddRange(_glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>());
        //    //embeddedGeometriesBuffer.AddRange(_glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>());

        //    // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
        //    embeddedGeometriesBuffer.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());


        //    // Meshatura
        //    Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
        //    embeddedGeometries[_glassSurface.Shape] = embeddedGeometriesBuffer.ToArray();

        //    /// TODO: togliere le impostazioni hardcoded
        //    Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
        //    Mesh.GenerateMeshOptions.Recombine = true;
        //    Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
        //    Mesh.GenerateMeshOptions.Size = 600;
        //    Mesh.GenerateMeshOptions.UseGlobalProgressID = false;


        //    List<Mesh> geometryMeshes = Mesh.Generate(new List<Shape>() { _glassSurface.Shape }, embeddedGeometries, out _embeddedGeometriesMapVertex);

        //    if (geometryMeshes.Count > 1)
        //        throw new NotSupportedException("Number of mesh for single glass higher than one");

        //    embeddedGeometriesMapVertex = _embeddedGeometriesMapVertex[geometryMeshes.First()];

        //    return geometryMeshes.First();
        //}

        #endregion

    }
}
