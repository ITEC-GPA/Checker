using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckerLib.Common.Geometry
{
    public abstract class CheckingSection
    {
        protected abstract Section Section { get; }
        protected CheckingSection()
        {
        }
    }
}
