using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Wrappers;

namespace GPC.Checker.Glasses.Checkers
{
    public class PrEnGlassChecker : GlassChecker
    {



        public PrEnGlassChecker(Model model, LaminatedAnalysisType laminatedAnalysisType) : base(model, laminatedAnalysisType)
        {

        }

        public override void Run()
        {
            List<GlassWrapper> wrappers = GetWrappers();

            foreach (var wrapper in wrappers)
            {
                if (wrapper is GlassPanelWrapper gpw)
                {
                    gpw.GeneratePlateMesh();
                }
                else if (wrapper is InsulatedGlassWrapper igw)
                {

                }
                else
                    throw new NotSupportedException("Glass wrapper not supported");
            }

        }

        protected override string GetCheckerName()
        {
            return "prEN 16612 - 2019";
        }
    }
}
