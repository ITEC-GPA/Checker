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
using GPC.Checkers.Steel.Cop2011;

namespace GPC.Checkers.Steel.Results
{
    public class CopSuos2011BeamStationCheckerResults : BeamStationCheckerResults
    {

        public CopSuos2011BeamStationCheckerResults(ISteelSection section, ILoadCase loadCase, ResultBeamForces forces, ResultStation station)
            : base(section, loadCase, forces, station)
        {

        }
               
    }
}
