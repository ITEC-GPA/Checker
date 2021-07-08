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
        protected readonly string _name;

        #endregion


        #region Properties

        public ISteelSection Section => _section;

        public BeamResult[] BeamResults => _beamResults;

        public ResultStation Station => (ResultStation)_beamResults.FirstOrDefault().Points.FirstOrDefault();

        public double BeamLength => Station.ElementLenght;

        public string BeamName => _name;               

        #endregion


        #region Public Constructors

        public BeamCheckerAttributes(ISteelSection sections, BeamResult[] beamResults, string name = "")
        {
            _beamResults = beamResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _section = sections ?? throw new ArgumentException("Input sections can not be null");
            _name = name;

            for (int i = 0; i < beamResults.Count(); i++)
                for(int j = 0; j < beamResults[i].Results.Count(); j++)
                    if(!(beamResults[i].Results[j] is ResultBeamForces _))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");
            for (int i = 0; i < beamResults.Count(); i++)
                for (int j = 0; j < beamResults[i].Results.Count(); j++)
                    if (((ResultStation)beamResults[i].Points[j]).ElementLenght != BeamLength)
                        throw new ArgumentException("BeamResult.Stations must be the same for each result");
            for (int i = 0; i < beamResults.Count(); i++)
                for (int j = 0; j < beamResults[i].Results.Count(); j++)
                    if (((ResultStation)beamResults[i].Points[j]).DistanceFromStartPoint != Station.DistanceFromStartPoint)
                        throw new ArgumentException("BeamResult.Stations must be the same for each result");
        }

        #endregion
    }
}
