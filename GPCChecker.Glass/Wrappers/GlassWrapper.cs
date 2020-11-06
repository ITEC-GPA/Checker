using GPC.Model.Elements.Glasses;

namespace GPC.Checker.Glasses.Wrappers
{
    public abstract class GlassWrapper
    {
        protected Glass _glass;

        protected GlassWrapper()
        {

        }
        

        public abstract double GetDeformationThickness();

        public abstract double GetStressThickness();

        public abstract double GetTotalThickness();
    }
}
