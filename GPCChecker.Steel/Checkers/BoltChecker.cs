using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Checkers
{
    /// <summary>
    /// The purpose of this class is to perform a check of a single bolt grid between all the ILoadCases.
    /// </summary>
    [Serializable]
    public abstract class BoltChecker : Model.ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly BoltGrid _boltGrid;
        protected readonly List<BoltStresses> _boltStresses;
        protected readonly Standard _standard;
        protected readonly Options _options;

        protected List<BoltResults> _boltResults;

        protected List<string> _errorLog;

        #endregion

        #region Constructor

        protected BoltChecker(BoltGrid boltGrid, List<BoltStresses> boltStresses, Standard standard, Options options)
        {
            _boltGrid = boltGrid ?? throw new ArgumentNullException(nameof(boltGrid));
            _boltStresses = boltStresses ?? throw new ArgumentNullException(nameof(boltStresses));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        #endregion

        #region Public abstract method

        public abstract void PerformCheck();

        #endregion

        #region Public method

        public List<string> GetErrorLog()
        {
            return _errorLog;
        }

        #endregion

        #region Protected method

        protected static double GetWorkingRatio(double force, double capacity)
        {
            if (Math.Abs(capacity) < 0.01)
                throw new ArgumentException("Capacity can not be null.");

            return Math.Abs(force / capacity);
        }

        #endregion

        #region Nested Class Options

        public abstract class Options
        {
            #region Variables

            protected int _numShearPlane;

            #endregion

            #region Properties

            /// <summary>
            /// Shear plane passes through the threaded portion of the bolt. True if it passes.
            /// </summary>
            public bool ShearPlaneThroughThreadedPortion { get; set; }

            /// <summary>
            /// Number of share planes, one or more.
            /// </summary>
            public int NumShearPlane
            {
                get => _numShearPlane;
                set => _numShearPlane = value < 1 ? 1 : value;
            }

            /// <summary>
            /// True if it is a countersunk bolt (bullone svasato).
            /// </summary>
            public bool IsCounterSunkBolt { get; set; }

            #endregion

            #region Constructor

            public Options()
            {
                ShearPlaneThroughThreadedPortion = true;
                NumShearPlane = 1;
                IsCounterSunkBolt = false;
            }

            #endregion
        }
        #endregion
    }

    public class BoltStresses : IEquatable<BoltStresses>
    {
        public ILoadCase LoadCase;
        public ResultBeamForces ResBeamForces;

        public BoltStresses(ILoadCase _loadcase, ResultBeamForces resbeam)
        {
            LoadCase = _loadcase;
            ResBeamForces = resbeam;
        }

        public override bool Equals(object obj) => Equals(obj as BoltStresses);

        public bool Equals(BoltStresses other)
        {
            return EqualityComparer<ILoadCase>.Default.Equals(LoadCase, other.LoadCase) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(ResBeamForces, other.ResBeamForces);
        }

        public override int GetHashCode()
        {
            int hashCode = -1030903623;
            hashCode = hashCode * -1521134295 + EqualityComparer<ILoadCase>.Default.GetHashCode(LoadCase);
            hashCode = hashCode * -1521134295 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(ResBeamForces);
            return hashCode;
        }
    }
}
