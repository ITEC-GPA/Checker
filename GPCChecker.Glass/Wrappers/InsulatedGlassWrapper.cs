using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Model.Glasses;
using GPC.Checker.Glasses.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    internal abstract class InsulatedGlassWrapper : GlassWrapper
    {
        protected new IInsulatingGlass Glass => (IInsulatingGlass)_glass;


        internal InsulatedGlassWrapper(GlassSurface glassSurface, IInsulatingGlass glass) 
            : base(glassSurface, (Glass)glass)
        {

        }

        protected abstract void SetUpWrappers();


        public abstract override void GenerateMesh();

    }
}
