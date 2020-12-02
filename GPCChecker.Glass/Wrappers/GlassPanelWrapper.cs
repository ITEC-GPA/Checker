using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using System;
using System.Linq;
using GPC.Geometry;
using System.Collections.Generic;

namespace GPC.Checker.Glasses.Wrappers
{
    internal abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
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

        public abstract List<FemModel.FemMesh> GeneratePlateMesh();

        #endregion

    }
}
