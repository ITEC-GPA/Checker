using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Bolt;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static GPC.Model.Sections.Bolt.BoltGrid;

namespace GPC.Checkers.Steel.Results
{
    /// <summary>
    /// This class contains the result of a check performed on a bolt grid with a given ILoadCase.
    /// </summary>
    public abstract class BoltResults : Model.ModelObject, ISerializable, IEquatable<BoltResults>
    {
        #region Properties

        public BoltPosition BoltPos { get; internal set; }

        public ILoadCase Case { get; internal set; }

        public ResultBeamForces BeamForces { get; internal set; }

        public Standard Standard { get; internal set; }

        public BoltChecker.Options Options { get; internal set; }

        #endregion

        #region Constructor

        public BoltResults(BoltPosition boltPos, ILoadCase @case, ResultBeamForces beamForces, Standard standard, BoltChecker.Options options)
        {
            BoltPos = boltPos;
            Case = @case;
            BeamForces = beamForces;
            Standard = standard;
            Options = options;
        }

        #endregion

        #region Comparer

        public override bool Equals(object obj) => Equals(obj as BoltResults);

        public override int GetHashCode()
        {
            int hashCode = 337272910;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<BoltPosition>.Default.GetHashCode(BoltPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<ILoadCase>.Default.GetHashCode(Case);
            hashCode = hashCode * -1521134295 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(BeamForces);
            hashCode = hashCode * -1521134295 + EqualityComparer<Standard>.Default.GetHashCode(Standard);
            hashCode = hashCode * -1521134295 + EqualityComparer<BoltChecker.Options>.Default.GetHashCode(Options);
            return hashCode;
        }

        public bool Equals(BoltResults other)
        {
            return base.Equals(other) &&
                   EqualityComparer<BoltPosition>.Default.Equals(BoltPos, other.BoltPos) &&
                   EqualityComparer<ILoadCase>.Default.Equals(Case, other.Case) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(BeamForces, other.BeamForces) &&
                   EqualityComparer<Standard>.Default.Equals(Standard, other.Standard) &&
                   EqualityComparer<BoltChecker.Options>.Default.Equals(Options, other.Options);
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
