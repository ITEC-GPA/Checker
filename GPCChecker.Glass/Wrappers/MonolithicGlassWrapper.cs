using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GPC.Checker.Glasses.Wrappers
{
    internal class MonolithicGlassWrapper : GlassPanelWrapper
    {
        protected new MonolithicGlass GlassProperty => (MonolithicGlass)_glassSurface.GlassProperty;

        internal MonolithicGlassWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is MonolithicGlass))
                throw new ArgumentException("Glass property should be a Monolithic Glass Property");
        }

        #region Public methods - geometry

        public override double GetDeformationThickness(double loadDuration)
        {
            return GlassProperty.Thickness;
        }

        public override double GetStressThickness(double loadDuration)
        {
            return GlassProperty.Thickness;
        }

        public override double GetTotalThickness()
        {
            return GlassProperty.Thickness;
        }

        public override double GetElasticModulus()
        {
            return GlassProperty.Material.E;
        }

        public override double GetPoissonRatios()
        {
            return GlassProperty.Material.Ni;
        }

        public override double GetSelfWeightPerUnitArea()
        {
            // mm * T/mm3 => T / mm2
            return GlassProperty.Thickness * GlassProperty.Material.Density;
        }

        public override double GetSelfWeightTotal()
        {
            // mm2 * mm * T/mm3 => T
            return _glassSurface.Shape.GetArea() * GlassProperty.Thickness * GlassProperty.Material.Density;
        }

        #endregion

        #region Public methods - Analysis

        public override List<FemModel.FemMesh> GeneratePlateMesh()
        {
            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            Dictionary<Mesh, Dictionary<GeometryBase, int[]>> embeddedGeometriesMapVertex = new Dictionary<Mesh, Dictionary<GeometryBase, int[]>>();

            var restrains = _glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>();
            var pointsRestrain = _glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>();
            
            restrains.AddRange(pointsRestrain);

            embeddedGeometries[_glassSurface.Shape] = restrains.ToArray();

            Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
            Mesh.GenerateMeshOptions.Recombine = true;
            Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
            Mesh.GenerateMeshOptions.Size = 50;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;

            var meshes = Mesh.Generate(shapes, embeddedGeometries, out embeddedGeometriesMapVertex);

            List<FemModel.FemMesh> femMesh = new List<FemModel.FemMesh>();
            foreach(var mesh in meshes)
            {
                var pointRestrainVertexIndex = new Dictionary<int, Restrain>();

                foreach (var kvp in embeddedGeometriesMapVertex[mesh])
                {
                    GeometryBase geometry = kvp.Key;
                    int[] vertex = kvp.Value;

                    if (geometry is Line3d || geometry is Line2d)
                    {
                        Line3d line;
                        if (geometry is Line3d l)
                            line = l;
                        else
                            line = new Line3d((Line2d)geometry);

                        var restrain = _glassSurface.LineRestrain.Where(i => i.Line == line).Select(i => i.Restrain).FirstOrDefault();

                        foreach (int v in vertex)
                            pointRestrainVertexIndex[v] = restrain;
                    }
                    else if (geometry is Point3d || geometry is Point2d)
                    {
                        Point3d point;
                        if (geometry is Point3d p)
                            point = p;
                        else
                            point = new Point3d((Point2d)geometry);

                        var restrain = _glassSurface.PointRestrain.Where(i => i.Point == point).Select(i => i.Restrain).FirstOrDefault();

                        foreach (int v in vertex)
                            pointRestrainVertexIndex[v] = restrain;
                    }
                    else
                    {
                        throw new NotSupportedException($"Geometry of type {geometry.GetType()} is not supported.");
                    }
                }

                femMesh.Add(new FemModel.FemMesh(mesh.Vertices, mesh.Faces, pointRestrainVertexIndex));
            }
            return femMesh;
        }

        #endregion

    }
}
