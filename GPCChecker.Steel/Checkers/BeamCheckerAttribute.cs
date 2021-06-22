using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Checkers
{
    public class BeamCheckerAttribute
    {
        #region Variables

        protected readonly ISteelSection[] _sections;
        protected readonly ResultBeamForces[] _resultBeamForces;
        protected readonly ResultStation[] _resultStations;
        protected readonly Checker.Options _options;

        #endregion


        #region Properties

        public ISteelSection[] Sections => _sections;

        public double BeamLength => Stations[0].ElementLenght;

        public ResultBeamForces[] ResultBeamForces => _resultBeamForces;

        public Checker.Options Options => _options;

        public ResultStation[] Stations  => _resultStations;

        #endregion


        #region Public Constructors

        public BeamCheckerAttribute(ISteelSection[] sections, ResultBeamForces[] resultBeamForces, ResultStation[] resultStations, Checker.Options options)
        {                
            _sections = sections ?? throw new ArgumentException("Input sections can not be null");
            _resultBeamForces = resultBeamForces ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _resultStations = resultStations ?? throw new ArgumentException("Input resultStations can not be null");
            _options = options ?? throw new ArgumentException("Input options can not be null");

            if (_resultStations.Length != _resultBeamForces.Length || _resultStations.Length != _sections.Length)
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
