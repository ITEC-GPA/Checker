using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Checkers.Steel.Results;

namespace GPC.Checkers.Steel.EuroCode
{
    public class EN1993_1BeamStationCheckerResults : BeamStationCheckerResults
    {
        public EN1993_1BeamStationCheckerResults(ISteelSection section, ResultBeamForces forces)
            : base(section, forces)
        {

        }

        

        //public bool BeamStationCheck(ISteelSection section, ResultBeamForces forces, Checker.Options standard,
        //    out double maxWorkingRatio, out double axialWR, out double shearWR, out double bending1WR, out double bending2WR, out double torsionWR, out double interactionWR)
        //{
        //    maxWorkingRatio = 0;
        //    axialWR = 0;
        //    shearWR = 0;
        //    bending1WR = 0;
        //    bending2WR = 0;
        //    torsionWR = 0;
        //    interactionWR = 0;
        //    return true;
        //}


    }
}
