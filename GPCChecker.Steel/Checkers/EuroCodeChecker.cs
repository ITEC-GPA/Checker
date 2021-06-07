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

        public EuroCodeChecker(EN1993p11BeamCheckerOptions[] beamCheckers, ILoadCase loadCase)
            : base(beamCheckers, loadCase)
        {

        }

        public EuroCodeChecker(EN1993p11BeamCheckerOptions[] beamCheckers, ILoadCase loadCase, StandardEN1990 standardEN1990)
            : base(beamCheckers, loadCase, standardEN1990)
        {

        }

        public override void PerformCheck()
        {

        }
    }

}
