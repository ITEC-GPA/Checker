using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using System;
using System.Linq;
using GPC.Geometry;
using System.Collections.Generic;
using GPC.Checker.Glasses.FemModel;
using GPC.Geometry.Meshes;
using GPC.Model.Elements;
using GPC.Model.FEM.Attributes;
using GPC.Checker.Glasses.LoadCases;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
    {
        protected List<Load> _loads;

        internal List<Load> Loads => _loads;

        protected new IGlassPanelProperty GlassProperty => (IGlassPanelProperty)_glassSurface.GlassProperty;


        protected GlassPanelWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            _loads = new List<Load>();
            if (!(glassSurface.GlassProperty is IGlassPanelProperty))
                throw new ArgumentException("Glass property should be a GlassPanel");
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

        public List<IGeometryRestrain> GetRestrains()
        {
            var list = new List<IGeometryRestrain>();
            list.AddRange(_glassSurface.LineRestrain);
            list.AddRange(_glassSurface.PointRestrain);

            return list;
        }

        public void GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads)
        {
            uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

            notUniformPressureLoads = _loads.Except(uniformPressureLoads).ToList();
        }

        public Mesh GenerateGeometryMesh(out Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex)
        {
            List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

            Dictionary<Mesh, Dictionary<GeometryBase, int[]>> _embeddedGeometriesMapVertex;

            // Aggiungo geometria relativa a vincoli
            embeddedGeometriesBuffer.AddRange(_glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>());
            embeddedGeometriesBuffer.AddRange(_glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>());

            // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
            embeddedGeometriesBuffer.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());


            // Meshatura
            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            embeddedGeometries[_glassSurface.Shape] = embeddedGeometriesBuffer.ToArray();

            Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
            Mesh.GenerateMeshOptions.Recombine = true;
            Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
            Mesh.GenerateMeshOptions.Size = 50;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;


            List<Mesh> geometryMeshes = Mesh.Generate(new List<Shape>() { _glassSurface.Shape }, embeddedGeometries, out _embeddedGeometriesMapVertex);

            if (geometryMeshes.Count > 1)
                throw new NotSupportedException("Number of mesh for single glass higher than one");

            embeddedGeometriesMapVertex = _embeddedGeometriesMapVertex[geometryMeshes.First()];

            return geometryMeshes.First();
        }

        #endregion

    }
}
