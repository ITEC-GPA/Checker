using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.Checkers
{
    public abstract class EuroCodeChecker : Checker
    {


        public EuroCodeChecker(BeamCheckerAttributes attributes, Options options, Standard standard)
            : base(attributes, options, standard)
        {

        }

        public override void PerformCheck()
        {

        }

    }
}
