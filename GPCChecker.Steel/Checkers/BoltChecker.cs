using GPC.Checkers.Steel.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using static GPC.Checkers.Steel.Results.EN1993BoltResults;

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
        protected List<DistanceWarning> _boltDistancesWarning;

        #endregion

        #region Properties

        public List<DistanceWarning> BoltDistancesWarning => _boltDistancesWarning;

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

        [Serializable]
        public abstract class BoltOptions : Options, ISerializable
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

            public BoltOptions(SerializationInfo info, StreamingContext context)
            {
                ShearPlaneThroughThreadedPortion = info.GetBoolean("ShearPlaneThroughThreadedPortion");
                _numShearPlane = info.GetInt32("NumShearPlane");
                IsCounterSunkBolt = info.GetBoolean("IsCounterSunkBolt");
                _numFrictionPlane = info.GetInt32("NumFrictionPlane");

            }

            #endregion

            #region Methods

            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ShearPlaneThroughThreadedPortion", ShearPlaneThroughThreadedPortion);
                info.AddValue("NumShearPlane", _numShearPlane);
                info.AddValue("IsCounterSunkBolt", IsCounterSunkBolt);
                info.AddValue("NumFrictionPlane", _numFrictionPlane);
            }

            #endregion
        }

        #endregion
    }
}
