using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Checkers.Steel.EuroCode
{
    public abstract class EuroCodeChecker : Checker
    {

        public EuroCodeChecker(ISteelSection section, BeamResult[] beamResult, EN1993p11Checker.EN1993_1Options options)
            : base(section, beamResult, options)
        {

        }

        public EuroCodeChecker(ISteelSection section, BeamResult[] beamResult, EN1993p11Checker.EN1993_1Options options, StandardEN1990 standardEN1990)
            : base(section, beamResult, options, standardEN1990)
        {

        }

        public override bool PerformCheck()
        {
                return true;
        }
    }

}
