using GPC.Model.Elements.Glasses;
using System;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassWrapper
    {
        protected GlassSurface _glassSurface;

        protected virtual GlassProperty GlassProperty => _glassSurface.GlassProperty;

        protected GlassWrapper(GlassSurface glassSurface)
        {
            this._glassSurface = glassSurface;
        }
    }
}
