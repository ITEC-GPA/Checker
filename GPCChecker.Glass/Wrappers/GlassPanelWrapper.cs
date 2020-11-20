using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using System;
using System.Linq;
using GPC.Geometry;
using System.Collections.Generic;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
    {
        protected Load _load;

        protected List<Mesh> _meshes;

        protected new IGlassPanel GlassProperty => (IGlassPanel)_glassSurface.GlassProperty;

        protected GlassPanelWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is IGlassPanel))
                throw new ArgumentException("Glass property should be a GlassPanel");
        }

        #region Public methods
        public abstract double GetDeformationThickness(double loadDuration);

        public abstract double GetStressThickness(double loadDuration);

        public abstract double GetTotalThickness();

        public abstract double GetElasticModulus();

        public abstract double GetPoissonRatios();

        public abstract double GetSelfWeightPerUnitArea();

        public abstract double GetSelfWeightTotal(); 
        #endregion


        private List<Mesh> GenerateMesh()
        {
            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            return Mesh.Generate(shapes, null, null);
        }

        List<Mesh> IGlassPanelWrapper.GenerateMesh()
        {
            throw new NotImplementedException();
        }
    }
}
