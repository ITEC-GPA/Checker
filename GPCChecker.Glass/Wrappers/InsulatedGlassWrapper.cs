using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Model.Glasses;
using System.Collections.Generic;

namespace GPC.Checkers.Glasses.Wrappers
{
    internal abstract class InsulatedGlassWrapper : GlassWrapper
    {

        internal InsulatedGlassWrapper(GlassSurface glassSurface, IInsulatingGlass glass)
            : base(glassSurface, (Glass)glass)
        {

        }


        protected abstract void SetUpWrappers();


        public abstract override bool GenerateMesh();


        public abstract List<IGlassLoad>[] GetLoadSharing(IEnumerable<IGlassLoad> loads, Models.Prototype.Standards standard);

    }
}
