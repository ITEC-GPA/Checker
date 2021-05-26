using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Results
{

    public abstract class BeamCheckerResults
    {
        #region Variables

        private ILoadCase _combination;
        private ResultBeamForces[] _resultBeamForces;
        private ResultStation[] _resultStations;
        private double _workingRatio;
        private double _length;
        private ISteelSection _section;
        private BeamStationCheckerResults[] _beamStationCheckerResults;
        private readonly Checker.Options _options;
        private readonly Standard _standard;

        #endregion


        #region Properties

        public ILoadCase Combination { get => _combination; set => _combination = value; }

        public ResultBeamForces[] ResultBeamForces { get => _resultBeamForces; set => _resultBeamForces = value; }

        public ResultStation[] BeamResult { get => _resultStations; set => _resultStations = value; }

        public double WorkingRatio { get => _workingRatio; set => _workingRatio = value; }

        public ISteelSection Section { get => _section; set => _section = value; }

        public BeamStationCheckerResults[] BeamStationCheckerResults { get => _beamStationCheckerResults; set => _beamStationCheckerResults = value; }

        public double Length { get => _length; set => _length = value; }

        public Checker.Options Options { get => _options; }

        public Standard Standard => _standard;

        #endregion


        #region Constructor

        internal BeamCheckerResults(ILoadCase loadCase, ResultBeamForces[] forces, ResultStation[] stations, ISteelSection section, Checker.Options options, Standard standard)
        {
            _section = section;
            if(forces.Length < 1)
                throw new ArgumentException("ResultBeamForces can not be null");
            _resultBeamForces = forces;
            if (stations.Length < 1)
                throw new ArgumentException("ResultStation can not be null");
            _resultStations = stations;
            _combination = loadCase;
            _length = stations.Length < 0 ? throw new ArgumentException($"Length cannot be lower than zero") : Length; 
            _options = (Checker.Options)options;
            _standard = standard;
        }

        #endregion


        internal abstract void PerformCheck();
        // deve settare  le variabili che mancano
        // _beamStationCheckerResults = .....
        // _workingRatio = .....





    }
}
