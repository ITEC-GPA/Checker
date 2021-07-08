using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.Checkers
{
    public class EN1993p11Checker : EuroCodeChecker
    {

        public EN1993p11Checker(BeamCheckerAttributes beamCheckers, Options options, StandardEN1990 standardEN1990) 
            : base(beamCheckers, options, standardEN1990)
        {

        }


    }
}
