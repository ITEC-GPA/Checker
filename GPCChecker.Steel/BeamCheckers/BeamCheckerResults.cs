//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using GPC.Model.Results;
//using GPC.Model.Standards;
//using GPC.Model.LoadCases;
//using GPC.Model.Sections.Steel;
//using GPC.Model.Sections;
//using GPC.Checkers.Steel.Checkers;
//using GPC.Checkers.Steel.Results;

//namespace GPC.Checkers.Steel.BeamChecker
//{

//    public abstract class BeamCheckerResults
//    {
//        #region Variables

//        protected BeamStationCheckerResults[] _beamStationCheckerResults;
//        protected readonly Standard _standard;        
//        protected readonly BeamResult _beamResult;
//        protected readonly ISteelSection _section;
//        protected readonly ResultStation _station;
//        protected readonly string _name;
//        protected readonly Checker.Options _options;

//        #endregion


//        #region Properties

//        internal BeamResult BeamResult => _beamResult;

//        internal ResultStation Station => _station;

//        internal ISteelSection Section => _section;

//        public double WorkingRatio => _beamStationCheckerResults.Select(i => i.GetMaxWorkingRatio()).Max();

//        public BeamStationCheckerResults[] BeamStationCheckerResults => _beamStationCheckerResults; 

//        internal double BeamLength => _station.ElementLenght;

//        internal Standard Standard => _standard;

//        internal ILoadCase LoadCase => _beamResult.Case;

//        public string BeamName => _name;

//        internal Checker.Options Options => _options;

//        #endregion


//        #region Constructor

//        internal BeamCheckerResults(BeamResult resultBeamForces, ISteelSection steelSection, ResultStation station, Checker.Options options, Standard standard, string name = "")
//        {
//            _beamResult = resultBeamForces ?? throw new ArgumentNullException(nameof(resultBeamForces));
//            _station = station ?? throw new ArgumentNullException(nameof(station));
//            _section = steelSection ?? throw new ArgumentNullException(nameof(steelSection));            
//            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
//            _name = name;
//            _options = options ?? throw new ArgumentNullException(nameof(options));
//        }

//        #endregion


//        internal abstract void PerformCheck();

        

//    }
//}
