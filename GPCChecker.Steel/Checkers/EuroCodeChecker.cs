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
        // VARIABILI EREDITATE DA CHECKER
        // BeamChecker[]
        // dentro beamchecker c'è : 
        //                          ISteelSection[] 
        //                          ResultBeamForces[]
        //                          ResultStation[] 
        //                          Checker.Options 
        // BeamCheckerResults[]
        // Standard

        public EuroCodeChecker(EN1993p11BeamCheckerAttributes[] beamCheckers, Options options)
            : base(beamCheckers, options)
        {

        }

        public EuroCodeChecker(EN1993p11BeamCheckerAttributes[] beamCheckers, Options options, StandardEN1990 standardEN1990)
            : base(beamCheckers, options, standardEN1990)
        {

        }

        public override void PerformCheck()
        {

        }
    }

}
