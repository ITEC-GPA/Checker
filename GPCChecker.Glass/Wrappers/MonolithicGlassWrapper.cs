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
            return ((MonolithicGlass)Glass).Thickness;
        }

        public override double GetDeformationThickness(string loadCaseName)
        {
            return ((MonolithicGlass)Glass).Thickness;
        }

        /// <returns>Glass thickness for stress analysis</returns>
        public double GetStressThickness()
        {
            return ((MonolithicGlass)Glass).Thickness;
        }

        public override double[] GetStressThickness(string loadCaseName)
        {
            return new[] { ((MonolithicGlass)Glass).Thickness };
        }

        public override double GetTotalThickness()
        {
            return ((MonolithicGlass)Glass).TotalThickness;
        }

        public override double GetElasticModulus()
        {
            return ((MonolithicGlass)Glass).GetElasticModulus();
        }

        public override double GetPoissonRatio()
        {
            return ((MonolithicGlass)Glass).GetPoissonRatios();
        }
        
        /// <inheritdoc cref="GlassPanelWrapper.GetSelfWeightPerUnitArea()"/>
        public override double GetSelfWeightPerUnitArea()
        {
            return ((MonolithicGlass)Glass).GetSelfWeightPerUnitArea();
        }

        /// <inheritdoc cref="GlassPanelWrapper.GetSelfWeightTotal()"/>
        public override double GetSelfWeightTotal()
        {
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
                out Dictionary<Load, int[]> meshLoadsVertexIndexes, out Dictionary<Load, int[]> meshLoadsFaceIndexes);

            if (!status)
                return false;

            _meshes[0] = mesh;
            _meshGeometryRestrainVertices = new List<KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>>() 
            { 
                new KeyValuePair<Mesh, Dictionary<GeometryRestrain, int[]>>(mesh, meshGeometryRestrainVertices) 
            };
            _meshLoadsFaceIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>() 
            { 
                new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, meshLoadsFaceIndexes) 
            };
            _meshLoadsVertexIndexes = new List<KeyValuePair<Mesh, Dictionary<Load, int[]>>>() 
            { 
                new KeyValuePair<Mesh, Dictionary<Load, int[]>>(mesh, meshLoadsVertexIndexes) 
            };

            _meshComputed = true;
            return true;
        }

        #endregion

    }
}
