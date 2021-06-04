using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections;
using GPC.Model.Results;
using GPC.Checkers.Steel.Results;
using GPC.Model.Standards;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel
{
    public abstract class Checker
    {
        #region Variables

        protected readonly ISteelSection[] _section;
        protected readonly BeamResult[] _result;
        protected readonly Options[] _options;
        protected readonly BeamCheckerResults[] _beamCheckerResults;
        protected readonly Standard _standard;

        #endregion

            
        #region Properties

        public ISteelSection[] Section => _section;

        public double Length => BeamResult.Length;

        public BeamResult[] BeamResult => _result;

        public Options[] CheckerOptions => _options;

        public BeamCheckerResults[] BeamCheckerResults => _beamCheckerResults;

        public Standard Standard { get => _standard; set => Standard = value; }

        #endregion


        #region Constructor

        public Checker(ISteelSection[] section, BeamResult[] beamResult, Options[] options, Standard standard)
        {
            _section = section;
            if (beamResult.Length < 1)
                throw new ArgumentException("BeamResult can not be null");
            _result = beamResult;
            _options = options;
            _standard = standard;
        }

        public Checker(ISteelSection[] section, BeamResult[] beamResult, Options[] options)
        {
            _section = section;
            if (beamResult.Length < 1)
                throw new ArgumentException("BeamResult can not be null");
            _result = beamResult;
            _options = options;
        }

        #endregion



        #region Public abstract method

        public abstract bool PerformCheck();

        #endregion


        public abstract class Options
        {
            protected double _l;
            protected double _kAxialBuckling1;
            protected double _kAxialBuckling2;
            protected double _kLatTorsBuckling;
            protected double _kCriticalMoment1;
            protected double _kCriticalMoment2;
            protected double _mAxialBuckling1;
            protected double _mAxialBuckling2;
            protected double _mLatTorsBuckling;
            protected double _mCriticalMoment1;
            protected double _mCriticalMoment2;


            public double Length => _l;

            /// <summary>
            /// Unbraced length factor for buckling about the frame object 1-axis
            /// </summary>
            public double UnbracedLengthFactorAxialBuck1 => _kAxialBuckling1;

            /// <summary>
            /// Unbraced length factor for buckling about the frame object 1-axis
            /// </summary>
            public double UnbracedLengthFactorAxialBuck2 => _kAxialBuckling2;
            public double UnbracedLengthFactorLatTorsBuck => _kLatTorsBuckling;
            public double UnbracedLengthFactorCriticalMoment1 => _kCriticalMoment1;
            public double UnbracedLengthFactorCriticalMoment2 => _kCriticalMoment2;

            /// <summary>
            /// Effective length factor for buckling about the frame object major axis
            /// </summary>
            public double EffectiveLengthFactorAxialBuck1 => _mAxialBuckling1;
            public double EffectiveLengthFactorAxialBuck2 => _mAxialBuckling2;
            public double EffectiveLengthFactorLatTorsBuck => _mLatTorsBuckling;
            public double EffectiveLengthFactorCriticalMoment1 => _mCriticalMoment1;
            public double EffectiveLengthFactorCriticalMoment2 => _mCriticalMoment2;

            public Options(double length, double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1,
                double unbracedLengthFactorAxialBuck2 = 1, double effectiveLengthFactorAxialBuck2 = 1,
                double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
                double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1,
                double unbracedLengthFactorCriticalMoment2 = 1, double effectiveLengthFactorCriticalMoment2 = 1)
            {
                _l = length;
                _kAxialBuckling1 = unbracedLengthFactorAxialBuck1;
                _kAxialBuckling2 = unbracedLengthFactorAxialBuck2;
                _kLatTorsBuckling = UnbracedLengthFactorLatTorsBuck;
                _kCriticalMoment1 = unbracedLengthFactorCriticalMoment1;
                _kCriticalMoment2 = unbracedLengthFactorCriticalMoment2;
                _mAxialBuckling1 = effectiveLengthFactorAxialBuck1;
                _mAxialBuckling2 = effectiveLengthFactorAxialBuck2;
                _mLatTorsBuckling = effectiveLengthFactorLatTorsBuck;
                _mCriticalMoment1 = effectiveLengthFactorCriticalMoment1;
                _mCriticalMoment2 = effectiveLengthFactorCriticalMoment2;
            }

            public double GetLenghtAxialBuckling1()
            {
                return Length * UnbracedLengthFactorAxialBuck1 * EffectiveLengthFactorAxialBuck1;
            }

            public double GetLenghtAxialBuckling2()
            {
                return Length * UnbracedLengthFactorAxialBuck2 * EffectiveLengthFactorAxialBuck2;
            }

            public double GetLenghtLatTorsBuckling()
            {
                return Length * UnbracedLengthFactorLatTorsBuck * EffectiveLengthFactorLatTorsBuck;
            }

            public double GetLenghtCriticalMoment1()
            {
                return Length * UnbracedLengthFactorCriticalMoment1 * EffectiveLengthFactorCriticalMoment1;
            }

            public double GetLenghtCriticalMoment2()
            {
                return Length * UnbracedLengthFactorCriticalMoment2 * EffectiveLengthFactorCriticalMoment2;
            }
        }


    }


}
