using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    public class TripleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new TripleInsulatingGlass GlassProperty => (TripleInsulatingGlass)_glassSurface.GlassProperty;

        protected readonly GlassPanelWrapper _glassPanelWrapperOuter;
        protected readonly GlassPanelWrapper _glassPanelWrapperCenter;
        protected readonly GlassPanelWrapper _glassPanelWrapperInner;
        public TripleInsulatingGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper[] glassPanelWrappers) : base(glassSurface, glassPanelWrappers)
        {
            if (!(glassSurface.GlassProperty is TripleInsulatingGlass))
                throw new ArgumentException("Glass property should be a Triple insulating glass property");

        }

        public TripleInsulatingGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper glassPanelWrapperOuter, GlassPanelWrapper glassPanelWrapperCenter, GlassPanelWrapper glassPanelWrapperInner)
            : this(glassSurface, new GlassPanelWrapper[3] { glassPanelWrapperOuter, glassPanelWrapperCenter, glassPanelWrapperInner })
        {

        }
    }
}
