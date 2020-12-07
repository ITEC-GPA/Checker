using GPC.Model.Elements.Glasses;
using System;
using GPC.Geometry;

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
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns>The normal vector unitized</returns>
        public Vector3d GetNormalVector() => _glassSurface.Shape.GetNormalVector();

        public int GetSurfaceId => _glassSurface.Index;
    }
}
