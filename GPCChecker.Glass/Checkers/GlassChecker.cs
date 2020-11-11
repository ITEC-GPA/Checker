using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Checker
{
    public abstract class GlassChecker : GPC.Checker.Common.Checker
    {
        public GlassChecker()
        {

        }

        protected abstract override string GetCheckerName();
    }
}
