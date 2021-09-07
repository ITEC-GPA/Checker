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


        /// <param name="loads"></param>
        /// <param name="standard"></param>
        /// <param name="compressibleGas"></param>
        /// <param name="cavitySealingPressure">default value is the atmosferic pressure [MPa] </param>
        internal abstract List<NormalAreaLoad>[] GetRedistributionPressures(IEnumerable<IGlassLoad> loads, Models.Prototype.Standards standard, 
                                                                bool compressibleGas, double cavitySealingPressure = 0.1 );


        public abstract double GetMinimumElasticModulus();

        public abstract double GetMinimumPoissonRatio();

        public abstract double GetSelfWeightPerUnitArea();

        public abstract double GetSelfWeightTotal();

        public abstract double GetMaximumDensity();

    }
}
