using GPC.Checker.Glasses.Checker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Checkers
{
    public class PrEnGlassChecker : GlassChecker
    {
        public PrEnGlassChecker(Model model) : base(model)
        {

        }

        protected override string GetCheckerName()
        {
            return "prEN 16612 - 2019";
        }
    }
}
