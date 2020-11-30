using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using GPC.Model.FEM.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.FemModel;
using GPC.Checker.Glasses.LoadCases;
using GPC.Geometry.Meshes;


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

        public override List<FemMesh> GeneratePlateMesh()
        {
            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

            // Aggiungo geometria relativa a vincoli
            embeddedGeometriesBuffer.AddRange(_glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>());
            embeddedGeometriesBuffer.AddRange(_glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>());

            // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
            embeddedGeometriesBuffer.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());

            List<Load> uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

            // Meshatura
            Dictionary<Mesh, Dictionary<GeometryBase, int[]>> embeddedGeometriesMapVertex = new Dictionary<Mesh, Dictionary<GeometryBase, int[]>>();
            Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
            embeddedGeometries[_glassSurface.Shape] = embeddedGeometriesBuffer.ToArray();

            Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
            Mesh.GenerateMeshOptions.Recombine = true;
            Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
            Mesh.GenerateMeshOptions.Size = 50;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;

            var meshes = Mesh.Generate(shapes, embeddedGeometries, out embeddedGeometriesMapVertex);


            // Set up fem mesh
            List<FemMesh> femMeshes = new List<FemMesh>();
            
            foreach(var mesh in meshes)
            {
                var pointRestrainVertexIndex = new Dictionary<int, Restrain>();
                Dictionary<INodeFemAttribute, int[]> nodeLoadVertexIndex = new Dictionary<INodeFemAttribute, int[]>();
                Dictionary<IPlateFemAttribute, int[]> plateLoadFaceIndex = new Dictionary<IPlateFemAttribute, int[]>();

                foreach (var load in uniformPressureLoads)
                {
                    if (load is NormalAreaLoad nal)
                    {
                        throw new NotImplementedException();
                    }
                    else if (load is GlobalAreaLoad gal)
                    {
                        PlateGlobalPressureAttribute pgpa = new PlateGlobalPressureAttribute((LoadCase)gal.LoadCase, gal.Px, gal.Py, gal.Pz);
                        plateLoadFaceIndex[pgpa] = mesh.Faces.Select(I => I.Id).ToArray();
                    }
                    else
                        throw new NotSupportedException("Load type not supported");
                }


                foreach (var kvp in embeddedGeometriesMapVertex[mesh])
                {
                    GeometryBase geometry = kvp.Key;
                    int[] vertexIndexes = kvp.Value;

                    if (geometry is Line3d || geometry is Line2d)
                    {
                        Line3d line;
                        if (geometry is Line3d l)
                            line = l;
                        else
                            line = new Line3d((Line2d)geometry);

                        var restrain = _glassSurface.LineRestrain.Where(i => i.Line == line).Select(i => i.Restrain).FirstOrDefault();

                        foreach (int v in vertexIndexes)
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

                        if (restrain != null)
                            foreach (int v in vertexIndexes)
                                pointRestrainVertexIndex[v] = restrain;
                                                        
                        var loads = _glassSurface.Loads.Where(i => i.GetGeometry().GetType() == typeof(Point3d)).Where(i => i.GetGeometry() == point);
                        
                        foreach (var load in loads)
                        {
                            if (load is GlobalPointLoad gpl)
                            {
                                NodeGlobalForceAttribute pgfa = new NodeGlobalForceAttribute((LoadCase)gpl.LoadCase, gpl.Fx, gpl.Fy, gpl.Fz, gpl.Mx, gpl.My, gpl.Mz);
                                nodeLoadVertexIndex[pgfa] = vertexIndexes;
                            }
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Geometry of type {geometry.GetType()} is not supported.");
                    }
                }
                
                var femMesh = new FemMesh(mesh.Vertices, mesh.Faces, pointRestrainVertexIndex, nodeLoadVertexIndex, plateLoadFaceIndex);
               
                femMeshes.Add(femMesh);
            }


            return femMeshes;
        }

        #endregion

    }
}
