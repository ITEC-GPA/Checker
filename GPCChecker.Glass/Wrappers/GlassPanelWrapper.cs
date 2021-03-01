
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

        #endregion

        #region Properties

        internal List<Load> Loads => _loads;

        protected new IGlassPanel Glass => (IGlassPanel)_glass;

        public List<Mesh> Meshes { get { return _meshes; } protected set { _meshes = value; } }


        //public Mesh Mesh
        //{
        //    get
        //    {
        //        if (_mesh == null)
        //        {
        //            _mesh = GenerateGeometryMesh(out _embeddedGeometriesMapVertex);
        //            return _mesh;
        //        }
        //        else
        //        {
        //            return _mesh;
        //        }
        //    }
        //}

        //public Dictionary<GeometryBase, int[]> EmbeddedGeometriesMapVertex
        //{
        //    get
        //    {
        //        if (_embeddedGeometriesMapVertex == null)
        //        {
        //            _mesh = GenerateGeometryMesh(out _embeddedGeometriesMapVertex);
        //            return _embeddedGeometriesMapVertex;
        //        }
        //        else
        //        {
        //            return _embeddedGeometriesMapVertex;
        //        }
        //    }
        //}

        #endregion


        protected GlassPanelWrapper(GlassSurface glassSurface, IGlassPanel glass) 
            : base(glassSurface, (Glass)glass)
        {
            _loads = new List<Load>();
            

        }

        #region Public methods - geometry

        public abstract double GetDeformationThickness(double loadDuration);

        public abstract double GetStressThickness(double loadDuration);

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

            List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

            //// Aggiungo geometria relativa a vincoli
            //embeddedGeometriesBuffer.AddRange(_glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>());
            //embeddedGeometriesBuffer.AddRange(_glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>());

            // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
            embeddedGeometriesBuffer.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());

            List<Load> uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

            // Meshatura
            Dictionary<Mesh, Dictionary<GeometryBase, int[]>> embeddedGeometriesMapVertex = new Dictionary<Mesh, Dictionary<GeometryBase, int[]>>();
            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            embeddedGeometries[_glassSurface.Shape] = embeddedGeometriesBuffer.ToArray();


            SetUpMeshOptions();
            List<Mesh> meshes = Mesh.Generate(shapes, embeddedGeometries, out embeddedGeometriesMapVertex);

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
