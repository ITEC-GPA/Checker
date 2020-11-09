using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    public class LaminatedGlassWrapper : GlassPanelWrapper
    {
        protected new LaminatedGlass GlassProperty => (LaminatedGlass)_glassSurface.GlassProperty;

        public LaminatedGlassWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is LaminatedGlass))
                throw new ArgumentException("Glass property should be a Laminated Glass Property");
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
            throw new NotImplementedException();
        }
    }
}
