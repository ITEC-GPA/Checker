using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheckerLib.Common;

namespace CheckerLib.RC.Checker
{
    // classe astratta. da fare verificatori per ogni normativa
    public abstract class RCChecker : CheckerLib.Common.Checker.Checker
    {
        public RCChecker() : base()
        {

        }

        protected override string GetCheckerName() => "Reinforced concrete checker";
    }
}
