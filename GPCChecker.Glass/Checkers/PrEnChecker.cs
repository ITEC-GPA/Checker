using GPC.Checker.Common;
using GPC.Checker.Glasses.Checker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Checkers
{
    public class PrEnChecker : GlassChecker
    {
        public PrEnChecker() : base()
        {

        }

        protected override string GetCheckerName()
        {
            return "prEN 16612 - 2019";
        }
    }
}
