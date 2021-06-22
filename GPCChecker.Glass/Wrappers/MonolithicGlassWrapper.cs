using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Geometry.Meshes;
using GPC.Model.Glasses;
using GPC.Model.Loads;
using GPC.Model.Restrains;
using System.Collections.Generic;

namespace GPC.Checkers.Glasses.Wrappers
{
    public class MonolithicGlassWrapper : GlassPanelWrapper
    {

        internal MonolithicGlassWrapper(GlassSurface glassSurface, MonolithicGlass glass)
            : base(glassSurface, glass)
        {

        }

        internal MonolithicGlassWrapper(GlassSurface glassSurface, MonolithicGlass glass, GlassPanelPositions position)
            : base(glassSurface, glass, position)
        {

        }

        #region Public methods - geometry

        /// <returns>Glass thickness for deformation analysis</returns>
        public double GetDeformationThickness()
        {
            return (Glass as MonolithicGlass).Thickness;
        }


        public override double GetDeformationThickness(IGlassLoad load)
        {
            return (Glass as MonolithicGlass).Thickness;
        }

        /// <returns>Glass thickness for stress analysis</returns>
        public double GetStressThickness()
        {
            return (Glass as MonolithicGlass).Thickness;
        }

        public override double[] GetStressThickness(IGlassLoad load)
        {
            return new[] { (Glass as MonolithicGlass).Thickness };
        }

        public override double GetTotalThickness()
        {
            return ((MonolithicGlass)Glass).TotalThickness;
        }

        public override double GetElasticModulus()
        {
            return ((MonolithicGlass)Glass).GetElasticModulus();
        }

        public override double GetPoissonRatios()
        {
            return ((MonolithicGlass)Glass).GetPoissonRatios();
        }

        public override double GetSelfWeightPerUnitArea()
        {
            // mm * T/mm3 => T / mm2
            return ((MonolithicGlass)Glass).GetSelfWeightPerUnitArea();
        }

        public override double GetSelfWeightTotal()
        {
            // mm2 * mm * T/mm3 => T
            return _glassSurface.Shape.GetArea() * GetSelfWeightPerUnitArea();
        }

        public override double GetDensity()
        {
            return ((MonolithicGlass)Glass).GetDensity();
        }

        /// <inheritdoc cref="GlassWrapper.GenerateMesh()"/>
        public override bool GenerateMesh()
        {
            bool status = GenerateSingleLayerMesh(out Mesh mesh, out Dictionary<GeometryRestrain, int[]> meshGeometryRestrainVertices,
                                                                   out Dictionary<Load, int[]> meshLoadsVertexIndexes,
                                                                   out Dictionary<Load, int[]> meshLoadsFaceIndexes);

            if (!status)
                return false;

            _meshes[0] = mesh;
            _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>() { new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) };
            _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>() { new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, meshLoadsFaceIndexes) }; ;
            _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>() { new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, meshLoadsVertexIndexes) }; ;

            _meshComputed = true;
            return true;
        }

        #endregion

    }
}
