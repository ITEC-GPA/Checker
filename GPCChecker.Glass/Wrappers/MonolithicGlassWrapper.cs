using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    public class MonolithicGlassWrapper : GlassPanelWrapper
    {
        protected new MonolithicGlass GlassProperty => (MonolithicGlass)_glassSurface.GlassProperty;

        public MonolithicGlassWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is MonolithicGlass))
                throw new ArgumentException("Glass property should be a Monolithic Glass Property");
        }

        public override double GetDeformationThickness()
        {
            throw new NotImplementedException();
        }

        public override double GetStressThickness()
        {
            throw new NotImplementedException();
        }

        public override double GetTotalThickness()
        {
            return this.GlassProperty.Thickness;
        }
    }
}
