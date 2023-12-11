using GPC.Checkers.Steel.Results;
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
        #region Enums

        /// <summary>
        /// Distribution types for bolt in tension.
        /// </summary>
        public enum TensionDistributionTypes
        {
            Uniform, // Bolt grid tension is distributed by simply dividing by the number of bolts.
            SimpleAssign, // Bolt grid tension is simply assigned to each bolt.
            MethodN1, // Elastic method considering the bolts with linear elastic behavior and the contact area (the whole plate)
                      // with elastic behavior with compressive strength only.
                      // N method, with N=1.
        }

        #endregion

        #region Variables

        protected readonly PlateWithBolts _plateWithBolts;
        protected readonly List<BoltStresses> _boltStresses;
        protected List<BoltResults> _boltResults;
        protected BoltResults _boltResultMax;
        protected List<ENCommonBoltResults.DistanceWarning> _boltDistancesWarning;

        #endregion

        #region Properties

        public List<ENCommonBoltResults.DistanceWarning> BoltDistancesWarning => _boltDistancesWarning;

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

            protected int _numBearingPlate;

            #endregion

            #region Properties

            /// <summary>
            /// Shear plane passes through the threaded portion of the bolt. True if it passes.
            /// </summary>
            public bool ShearPlaneThroughThreadedPortion { get; set; }

            /// <summary>
            /// Number of share planes, one or more.
            /// Coefficient that amplifies shear resistance.
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
            /// Coefficient that amplifies slip resistance.
            /// </summary>
            public int NumFricionPlane
            {
                get => _numFrictionPlane;
                set => _numFrictionPlane = value < 1 ? 1 : value;
            }

            /// <summary>
            /// Number of bearing plates, one or more.
            /// Coefficient that amplifies bearing resistance.
            /// </summary>
            public int NumBearingPlate
            {
                get => _numBearingPlate;
                set => _numBearingPlate = value < 1 ? 1 : value;
            }

            /// <summary>
            /// Distribution of tensile stress between bolts.
            /// </summary>
            public TensionDistributionTypes TensionDistributionType { get; set; }

            /// <summary>
            /// Hole rotation angle, general option for all holes.
            /// Then each hole can override with a particular value.
            /// /// </summary>
            public double HoleAngle { get; set; }

            #endregion

            #region Constructor

            public BoltOptions()
            {
                ShearPlaneThroughThreadedPortion = true;
                NumShearPlane = 1;
                IsCounterSunkBolt = false;
                NumFricionPlane = 1;
                NumBearingPlate = 1;
                TensionDistributionType = TensionDistributionTypes.Uniform;
                HoleAngle = 0.0;
            }

            public BoltOptions(SerializationInfo info, StreamingContext context)
            {
                int BoltOptionsVersion = 1;
                try
                {
                    BoltOptionsVersion = info.GetInt32("BoltOptionsVersion");
                }
                catch { }

                ShearPlaneThroughThreadedPortion = info.GetBoolean("ShearPlaneThroughThreadedPortion");
                _numShearPlane = info.GetInt32("NumShearPlane");
                IsCounterSunkBolt = info.GetBoolean("IsCounterSunkBolt");
                _numFrictionPlane = info.GetInt32("NumFrictionPlane");
                if (BoltOptionsVersion > 1)
                {
                    _numBearingPlate = info.GetInt32("NumBearingPlate");
                }
                else
                {
                    _numBearingPlate = 1;
                }
                if (BoltOptionsVersion > 2)
                {
                    TensionDistributionType = (TensionDistributionTypes)info.GetValue("TensionDistributionType", typeof(TensionDistributionTypes));
                }
                else
                {
                    TensionDistributionType = TensionDistributionTypes.Uniform;
                }
                if (BoltOptionsVersion > 3)
                    HoleAngle = info.GetDouble("HoleAngle");
                else
                    HoleAngle = 0.0;
            }

            #endregion

            #region Methods

            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                int BoltOptionsVersion = 4;
                info.AddValue("BoltOptionsVersion", BoltOptionsVersion);
                info.AddValue("ShearPlaneThroughThreadedPortion", ShearPlaneThroughThreadedPortion);
                info.AddValue("NumShearPlane", _numShearPlane);
                info.AddValue("IsCounterSunkBolt", IsCounterSunkBolt);
                info.AddValue("NumFrictionPlane", _numFrictionPlane);
                info.AddValue("NumBearingPlate", _numBearingPlate);
                info.AddValue("TensionDistributionType", TensionDistributionType);
                info.AddValue("HoleAngle", HoleAngle);
            }

            #endregion
        }

        #endregion
    }
}
