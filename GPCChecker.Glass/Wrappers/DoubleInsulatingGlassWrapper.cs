using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    public class DoubleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new DoubleInsulatingGlass GlassProperty => (DoubleInsulatingGlass)_glassSurface.GlassProperty;

        public DoubleInsulatingGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper[] glassPanelWrappers) 
            : base(glassSurface, glassPanelWrappers)

        {
            if (!(glassSurface.GlassProperty is DoubleInsulatingGlass))
                throw new ArgumentException("Glass property should be a Double insulating Glass Property");

            
        }

        public DoubleInsulatingGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper glassPanelWrapperOuter, GlassPanelWrapper glassPanelWrapperInner) 
            : this(glassSurface, new GlassPanelWrapper[2] { glassPanelWrapperOuter, glassPanelWrapperInner })
        {

        }

    }
}
