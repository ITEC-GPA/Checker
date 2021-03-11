using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    public class MonolithicGlassWrapper : GlassPanelWrapper
    {
        public new MonolithicGlass Glass => (MonolithicGlass)_glass;

        internal MonolithicGlassWrapper(GlassSurface glassSurface, MonolithicGlass glass)
            : base(glassSurface, glass)
        {

        }

        #region Public methods - geometry

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loadDuration"></param>
        /// <returns>Glass thickness for deformation analysis</returns>
        public double GetDeformationThickness()
        {
            return Glass.Thickness;
        }

        /// <inheritdoc/>
        public override double GetDeformationThickness(double loadDuration)
        {
            return Glass.Thickness;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loadDuration"></param>
        /// <returns>Glass thickness for stress analysis</returns>
        public double GetStressThickness()
        {
            return Glass.Thickness;
        }

        /// <inheritdoc/>
        public override double GetStressThickness(double loadDuration)
        {
            return Glass.Thickness;
        }

        /// <inheritdoc/>
        public override double GetTotalThickness()
        {
            return Glass.Thickness;
        }

        public override double GetElasticModulus()
        {
            return Glass.Material.E;
        }

        public override double GetPoissonRatios()
        {
            return Glass.Material.Ni;
        }

        public override double GetSelfWeightPerUnitArea()
        {
            // mm * T/mm3 => T / mm2
            return Glass.Thickness * Glass.Material.Density;
        }

        public override double GetSelfWeightTotal()
        {
            // mm2 * mm * T/mm3 => T
            return _glassSurface.Shape.GetArea() * Glass.Thickness * Glass.Material.Density;
        }

        #endregion

        #region Public methods - Analysis

        //public override List<FemMesh> GeneratePlateMesh()
        //{
        //    var shapes = new List<Shape>();
        //    shapes.Add(_glassSurface.Shape);

        //    List<GeometryBase> embeddedGeometriesBuffer = new List<GeometryBase>();

        //    // Aggiungo geometria relativa a vincoli
        //    embeddedGeometriesBuffer.AddRange(_glassSurface.LineRestrain.Select(i => i.Line).ToList<GeometryBase>());
        //    embeddedGeometriesBuffer.AddRange(_glassSurface.PointRestrain.Select(i => i.Point).ToList<GeometryBase>());

        //    // Aggiungo geometria relativa a carichi - se è diversa dalla superficie di partenza.
        //    embeddedGeometriesBuffer.AddRange(_loads.Where(i => i.GetGeometry() != _glassSurface.Shape).Select(i => i.GetGeometry()).ToList());

        //    List<Load> uniformPressureLoads = _loads.Where(i => i.GetGeometry() == _glassSurface.Shape).ToList();

        //    // Meshatura
        //    Dictionary<Mesh, Dictionary<GeometryBase, int[]>> embeddedGeometriesMapVertex = new Dictionary<Mesh, Dictionary<GeometryBase, int[]>>();
        //    Dictionary<Shape, GeometryBase[]> embeddedGeometries = new Dictionary<Shape, GeometryBase[]>();
        //    embeddedGeometries[_glassSurface.Shape] = embeddedGeometriesBuffer.ToArray();

        //    Mesh.GenerateMeshOptions.Algorithm = Mesh.GenerateMeshOptions.MeshAlgorithm.PackingOfParallelograms;
        //    Mesh.GenerateMeshOptions.Recombine = true;
        //    Mesh.GenerateMeshOptions.RecombinationAlgorithm = Mesh.GenerateMeshOptions.RecombinationMeshAlgorithm.BlossomFullQuad;
        //    Mesh.GenerateMeshOptions.Size = 50;
        //    Mesh.GenerateMeshOptions.UseGlobalProgressID = true;

        //    List<Mesh> meshes = Mesh.Generate(shapes, embeddedGeometries, out embeddedGeometriesMapVertex);

        //    // Set up fem mesh
        //    List<FemMesh> femMeshes = new List<FemMesh>();

        //    foreach(var mesh in meshes)
        //    {
        //        var pointRestrainVertexIndex = new Dictionary<int, Restrain>();
        //        Dictionary<INodeFemAttribute, int[]> nodeAttributeVertexIndex = new Dictionary<INodeFemAttribute, int[]>();
        //        Dictionary<IPlateFemAttribute, int[]> plateAttributeFaceIndex = new Dictionary<IPlateFemAttribute, int[]>();
        //        List<IGlassPanelProperty> plateProperties = new List<IGlassPanelProperty>();

        //        // proprietà
        //        foreach (var face in mesh.Faces)
        //        {
        //            plateProperties.Add(this.GlassProperty);
        //        }

        //        // loads
        //        foreach (var load in uniformPressureLoads)
        //        {
        //            if (load is NormalAreaLoad nal)
        //            {
        //                throw new NotImplementedException();
        //            }
        //            else if (load is GlobalAreaLoad gal)
        //            {
        //                PlateGlobalPressureAttribute pgpa = new PlateGlobalPressureAttribute((LoadCase)gal.LoadCase, gal.Px, gal.Py, gal.Pz);
        //                plateAttributeFaceIndex[pgpa] = mesh.Faces.Select(I => I.Id).ToArray();
        //            }
        //            else
        //                throw new NotSupportedException("Load type not supported");
        //        }

        //        // geometria embedded
        //        foreach (var kvp in embeddedGeometriesMapVertex[mesh])
        //        {
        //            GeometryBase geometry = kvp.Key;
        //            int[] vertexIndexes = kvp.Value;

        //            if (geometry is Line3d || geometry is Line2d)
        //            {
        //                Line3d line;
        //                if (geometry is Line3d l)
        //                    line = l;
        //                else
        //                    line = new Line3d((Line2d)geometry);

        //                var restrain = _glassSurface.LineRestrain.Where(i => i.Line == line).Select(i => i.Restrain).FirstOrDefault();

        //                if (restrain != null)
        //                    foreach (int v in vertexIndexes)
        //                        pointRestrainVertexIndex[v] = restrain;
        //            }
        //            else if (geometry is Point3d || geometry is Point2d)
        //            {
        //                Point3d point;
        //                if (geometry is Point3d p)
        //                    point = p;
        //                else
        //                    point = new Point3d((Point2d)geometry);

        //                var restrain = _glassSurface.PointRestrain.Where(i => i.Point == point).Select(i => i.Restrain).FirstOrDefault();

        //                if (restrain != null)
        //                    foreach (int v in vertexIndexes)
        //                        pointRestrainVertexIndex[v] = restrain;

        //                var loads = _glassSurface.Loads.Where(i => i.GetGeometry().GetType() == typeof(Point3d)).Where(i => (Point3d)i.GetGeometry() == point);
        //                foreach (var load in loads)
        //                {
        //                    if (load is GlobalPointLoad gpl)
        //                    {
        //                        NodeGlobalForceAttribute pgfa = new NodeGlobalForceAttribute((LoadCase)gpl.LoadCase, gpl.Fx, gpl.Fy, gpl.Fz, gpl.Mx, gpl.My, gpl.Mz);
        //                        nodeAttributeVertexIndex[pgfa] = vertexIndexes;
        //                    }
        //                    else
        //                    {
        //                        throw new NotSupportedException("Load type not supported");
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                throw new NotSupportedException($"Geometry of type {geometry.GetType()} is not supported.");
        //            }
        //        }

        //        var femMesh = new FemMesh(mesh.Vertices, mesh.Faces, plateProperties, pointRestrainVertexIndex, nodeAttributeVertexIndex, plateAttributeFaceIndex);

        //        femMeshes.Add(femMesh);
        //    }

        //    return femMeshes;
        //}

        #endregion
    }
}