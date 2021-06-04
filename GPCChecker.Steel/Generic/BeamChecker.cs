using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Steel;
using GPC.Model.LoadCases;

namespace GPCCheckers.Steel.Generic
{
    public class BeamChecker
    {
        #region Variables

        protected readonly ISteelSection _section;
        protected readonly ResultBeamForces[] _resultBeamForces;
        protected readonly ResultStation[] _resultStations;
        protected readonly Checker.Options _options;


        #endregion


        #region Properties

        public ISteelSection Section => _section;

        public double BeamLength => Stations[0].ElementLenght;

        public ResultBeamForces[] ResultBeamForces => _resultBeamForces;

        public Checker.Options Options => _options;

        public ResultStation[] Stations  => _resultStations;



        #endregion


        #region Public Constructors

        public BeamChecker(ISteelSection sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, Checker.Options options)
        {
            _section = sections;
            if (resultBeamForces.Length < 1)
                throw new ArgumentException("Input resultBeamForces can not be null");
            _resultBeamForces = resultBeamForces;
            if (resultStations.Length < 1)
                throw new ArgumentException("Input resultStations can not be null");
            _resultStations = resultStations;
            if (options.Length < 1)
                throw new ArgumentException("Input options can not be null");
            _options = options;

            if (_resultStations.Length != _resultBeamForces.Length)
                throw new ArgumentException("The input array must have the same length");
        }

        #endregion
    }
}
