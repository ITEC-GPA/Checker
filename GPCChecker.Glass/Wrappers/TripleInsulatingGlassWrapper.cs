
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    internal class TripleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new TripleInsulatingGlass Glass => (TripleInsulatingGlass)_glass;

        protected readonly GlassPanelWrapper _glassPanelWrapperOuter;
        protected readonly GlassPanelWrapper _glassPanelWrapperCenter;
        protected readonly GlassPanelWrapper _glassPanelWrapperInner;
        internal TripleInsulatingGlassWrapper(GlassSurface glassSurface, TripleInsulatingGlass glass) 
            : base(glassSurface, glass)
        {

        }

        protected override List<GlassPanelWrapper> GetWrappers()
        {
            throw new NotImplementedException();
        }
    }
}
