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
    public abstract class BoltChecker : Checker, ISerializable
    {
        #region Variables

        protected readonly PlateWithBolts _plateWithBolts;
        protected readonly List<BoltStresses> _boltStresses;
        protected List<BoltResults> _boltResults;
        protected BoltResults _boltResultMax;

        #endregion

        #region Constructor

        protected BoltChecker(PlateWithBolts plateWithBolts, List<BoltStresses> boltStresses, Standard standard, BoltOptions options, int id, string name = "")
            : base(options, standard, id, name)
        {
            _plateWithBolts = plateWithBolts ?? throw new ArgumentNullException(nameof(plateWithBolts));
            _boltStresses = boltStresses ?? throw new ArgumentNullException(nameof(boltStresses));
        }

        #endregion

        #region Nested Class Options

        public abstract class BoltOptions : Options
        {
            #region Variables

            protected int _numShearPlane;

            protected int _numFrictionPlane;

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

            /// <summary>
            /// Number of share planes, one or more.
            /// </summary>
            public int NumFricionPlane
            {
                get => _numFrictionPlane;
                set => _numFrictionPlane = value < 1 ? 1 : value;
            }

            #endregion

            #region Constructor

            public BoltOptions()
            {
                ShearPlaneThroughThreadedPortion = true;
                NumShearPlane = 1;
                IsCounterSunkBolt = false;
                NumFricionPlane = 1;
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
