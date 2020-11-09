using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using System;
using System.Linq;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassPanelWrapper : GlassWrapper, IGlassPanelWrapper
    {
        protected Load _load;

        protected new IGlassPanel GlassProperty => (IGlassPanel)_glassSurface.GlassProperty;

        protected GlassPanelWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is IGlassPanel))
                throw new ArgumentException("Glass property should be a GlassPanel");


        }

        public abstract double GetDeformationThickness();

        public abstract double GetStressThickness();

        public abstract double GetTotalThickness();
    }
}
