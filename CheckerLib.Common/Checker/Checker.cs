using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckerLib.Common.Checker
{
    public abstract class Checker
    {
        protected Checker()
        {

        }

        protected abstract string GetCheckerName();
    }
}
