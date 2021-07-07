using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Checkers
{
    public class BeamCheckerAttributes
    {
        #region Variables

        protected readonly ISteelSection _section;
        protected readonly BeamResult[] _beamResults;
        protected readonly ResultStation _resultStation;
        protected readonly string _name;

        #endregion


        #region Properties

        public ISteelSection Section => _section;

        public double BeamLength => Station.ElementLenght;

        public BeamResult[] BeamResults => _beamResults;

        public ResultStation Station  => _resultStation;

        public string BeamName => _name;               

        #endregion


        #region Public Constructors

        public BeamCheckerAttributes(ISteelSection sections, BeamResult[] beamResults, ResultStation resultStation, string name = "")
        {       
            for(int i = 0; i < beamResults.Count(); i++)
                for(int j = 0; j < beamResults[i].Results.Count(); j++)
                    if(beamResults[i].Results[j] is ResultBeamForces rbf)  { }
                    else
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");
            _beamResults = beamResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _section = sections ?? throw new ArgumentException("Input sections can not be null");
            _resultStation = resultStation ?? throw new ArgumentException("Input resultStations can not be null");
            _name = name;
        }

        #endregion
    }
}
