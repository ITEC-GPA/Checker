
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    internal class DoubleInsulatingGlassWrapper : InsulatedGlassWrapper
    {
        protected new DoubleInsulatingGlass Glass => (DoubleInsulatingGlass)_glass;

        internal DoubleInsulatingGlassWrapper(GlassSurface glassSurface, DoubleInsulatingGlass glass) 
            : base(glassSurface, glass)

        {

            
        }

        protected override List<GlassPanelWrapper> GetWrappers()
        {
            List<GlassPanelWrapper> gpw = new List<GlassPanelWrapper>();

            GlassPanelWrapper gpwInner, gpwOuter;

            if (Glass.GlassPanelInner is MonolithicGlass)
            {
                gpwInner = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)Glass.GlassPanelInner);

            }
            else if (Glass.GlassPanelInner is LaminatedGlass)
            {
                gpwInner = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)Glass.GlassPanelInner);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (Glass.GlassPanelOuter is MonolithicGlass)
            {
                gpwOuter = new MonolithicGlassWrapper(_glassSurface, (MonolithicGlass)Glass.GlassPanelInner);

            }
            else if (Glass.GlassPanelOuter is LaminatedGlass)
            {
                gpwOuter = new LaminatedGlassWrapper(_glassSurface, (LaminatedGlass)Glass.GlassPanelInner);
            }
            else
            {
                throw new NotSupportedException();
            }

            return gpw;
        }



    }
}
