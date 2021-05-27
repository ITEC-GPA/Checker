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

        private readonly ISteelSection _section;  
        private readonly BeamResult[] _result;
        private readonly Options[] _options;
        private readonly BeamCheckerResults[] _beamCheckerResults;
        private readonly Standard _standard;

        #endregion

            
        #region Properties

        public ISteelSection Section => _section;

        public double Length { get => BeamResult.Length; set => Length = value; }

        public BeamResult[] BeamResult => _result;

        public Options[] CheckerOptions => _options;

        public BeamCheckerResults[] BeamCheckerResults => _beamCheckerResults;

        public Standard Standard { get => _standard; set => Standard = value; }

        #endregion


        #region Constructor

        public Checker(ISteelSection section, BeamResult[] beamResult, Options[] options, Standard standard)
        {
            _section = section;
            Length = beamResult.Length < 0 ? throw new ArgumentException($"Length cannot be lower than zero") : Length;
            if (beamResult.Length < 1)
                throw new ArgumentException("BeamResult can not be null");
            _result = beamResult;
            _options = options;
            _standard = standard;
        }

        public Checker(ISteelSection section, BeamResult[] beamResult, Options[] options)
        {
            _section = section;
            Length = beamResult.Length < 0 ? throw new ArgumentException($"Length cannot be lower than zero") : Length;
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

        }


        #region Nested class: SectionProperties

        public abstract class SectionProperties
        {
            #region Variables

            private readonly ISteelSection _section;
            private readonly Options _options;

            #endregion


            #region Properties

            public ISteelSection Section => _section;            

            public Checker.Options Options => _options;

            #endregion


            public SectionProperties(ISteelSection section, Checker.Options standard)
            {
                _section = section;
                _options = standard;
            }




        }

        #endregion
    }


}
