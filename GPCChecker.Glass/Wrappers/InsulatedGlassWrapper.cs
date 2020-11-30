using GPC.Geometry;
using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry.Meshes;

namespace GPC.Checker.Glasses.Wrappers
{
    internal abstract class InsulatedGlassWrapper : GlassWrapper
    {
        protected GlassPanelWrapper[] _glassPanelWrappers;

        protected List<Mesh> _mesh;

        protected new IInsulatingGlass GlassProperty => (IInsulatingGlass)_glassSurface.GlassProperty;


        internal InsulatedGlassWrapper(GlassSurface glassSurface, GlassPanelWrapper[] glassPanelWrappers) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is IInsulatingGlass))
                throw new ArgumentException("Glass property should be an insulating Glass Property");

            this._glassPanelWrappers = glassPanelWrappers;
        }

    }
}
