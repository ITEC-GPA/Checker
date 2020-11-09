using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class InsulatedGlassWrapper : GlassWrapper
    {
        protected GlassPanelWrapper[] _glassPanelWrappers;

        protected new IInsulatingGlass GlassProperty => (IInsulatingGlass)_glassSurface.GlassProperty;


        public InsulatedGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper[] glassPanelWrappers) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is IInsulatingGlass))
                throw new ArgumentException("Glass property should be an insulating Glass Property");

            this._glassPanelWrappers = glassPanelWrappers;
        }

    }
}
