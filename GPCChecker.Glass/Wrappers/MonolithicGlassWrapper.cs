using GPC.Geometry;
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

            Mesh.GenerateMeshOptions.Size = 10;
            Mesh.GenerateMeshOptions.UseGlobalProgressID = true;

            var meshes = Mesh.Generate(shapes, null, null);

            List<FemModel.FemMesh> femMesh = new List<FemModel.FemMesh>();
            foreach(var mesh in meshes)
            {
                femMesh.Add(new FemModel.FemMesh(mesh.Vertices, mesh.Faces));
            }
            return femMesh;
        } 

        #endregion

    }
}
