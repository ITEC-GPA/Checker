using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Steel;
using GPC.Model.LoadCases;

namespace GPC.Checkers.Steel.Checkers
{
    public class BeamCheckerOptions
    {
        #region Variables

        protected readonly ISteelSection[] _section;
        protected readonly ResultBeamForces[] _resultBeamForces;
        protected readonly ResultStation[] _resultStations;
        protected readonly Checker.Options _options;


        #endregion


        #region Properties

        public ISteelSection[] Section => _section;

        public double BeamLength => Stations[0].ElementLenght;

        public ResultBeamForces[] ResultBeamForces => _resultBeamForces;

        public Checker.Options Options => _options;

        public ResultStation[] Stations  => _resultStations;



        #endregion


        #region Public Constructors

        public BeamCheckerOptions(ISteelSection[] sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, Checker.Options options)
        {
            _section = sections;
            if (resultBeamForces.Length < 1)
                throw new ArgumentException("Input resultBeamForces can not be null");
            _resultBeamForces = resultBeamForces;
            if (resultStations.Length < 1)
                throw new ArgumentException("Input resultStations can not be null");
            _resultStations = resultStations;
            if (options== null)
                throw new ArgumentException("Input options can not be null");
            _options = options;

            if (_resultStations.Length != _resultBeamForces.Length)
                throw new ArgumentException("The input array must have the same length");
        }


        public double GetLenghtAxialBuckling1()
        {
            return BeamLength * Options.UnbracedLengthFactorAxialBuck1 * Options.EffectiveLengthFactorAxialBuck1;
        }

        public double GetLenghtAxialBuckling2()
        {
            return BeamLength * Options.UnbracedLengthFactorAxialBuck2 * Options.EffectiveLengthFactorAxialBuck2;
        }

        public double GetLenghtLatTorsBuckling()
        {
            return BeamLength * Options.UnbracedLengthFactorLatTorsBuck * Options.EffectiveLengthFactorLatTorsBuck;
        }

        public double GetLenghtCriticalMoment1()
        {
            return BeamLength * Options.UnbracedLengthFactorCriticalMoment1 * Options.EffectiveLengthFactorCriticalMoment1;
        }

        public double GetLenghtCriticalMoment2()
        {
            return BeamLength * Options.UnbracedLengthFactorCriticalMoment2 * Options.EffectiveLengthFactorCriticalMoment2;
        }

        #endregion
    }
}
