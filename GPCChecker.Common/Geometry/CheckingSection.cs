using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Common.Geometry
{
    public abstract class CheckingSection
    {
        protected abstract Section Section { get; }
        protected CheckingSection()
        {
        }
    }
}
