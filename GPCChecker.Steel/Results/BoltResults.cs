using GPC.Checkers.Results;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Results
{
	/// <summary>
	/// This class contains the result of a check performed on a bolt grid with a given ILoadCase.
	/// </summary>
	public abstract class BoltResults : CommonResults, ISerializable, IEquatable<BoltResults>
	{
		#region Variables

		protected BoltPosition _boltPosition;
		protected ResultBeamForces _resultBeamForce;

		#endregion

		#region Properties

		public BoltPosition BoltPos { get => _boltPosition; internal set => _boltPosition = value; }

		public ResultBeamForces BeamForces { get => _resultBeamForce; internal set => _resultBeamForce = value; }

		public BoltChecker.BoltOptions BoltCheckerOptions => (BoltChecker.BoltOptions)_options;

		#endregion

		#region Constructor

		public BoltResults(BoltPosition boltPos, ILoadCase loadCase, ResultBeamForces beamForces, Standard standard, BoltChecker.BoltOptions options, string name = "")
			: base(loadCase, standard, options, name)
		{
			BoltPos = boltPos;
			BeamForces = beamForces;
		}

        #endregion

        #region Abstract method

        /// <summary>
        /// Given a result calculate the maximum ratio.
        /// </summary>
        /// <returns>Max ratio.</returns>
        public abstract double CalcMaxRatio();

        #endregion

        #region Comparer

        public override bool Equals(object obj) => Equals(obj as BoltResults);

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + EqualityComparer<BoltPosition>.Default.GetHashCode(BoltPos);
				hashCode = hashCode * -17 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(BeamForces);
				hashCode = hashCode * -17 + EqualityComparer<BoltChecker.BoltOptions>.Default.GetHashCode(BoltCheckerOptions);
				return hashCode;
			}
		}

		public bool Equals(BoltResults other)
		{
			return base.Equals(other) &&
				   EqualityComparer<BoltPosition>.Default.Equals(BoltPos, other.BoltPos) &&
				   EqualityComparer<ResultBeamForces>.Default.Equals(BeamForces, other.BeamForces) &&
				   EqualityComparer<BoltChecker.BoltOptions>.Default.Equals(BoltCheckerOptions, other.BoltCheckerOptions);
		}

		public static bool operator ==(BoltResults left, BoltResults right)
		{
			return EqualityComparer<BoltResults>.Default.Equals(left, right);
		}

		public static bool operator !=(BoltResults left, BoltResults right)
		{
			return !(left == right);
		}

		#endregion
	}
}
