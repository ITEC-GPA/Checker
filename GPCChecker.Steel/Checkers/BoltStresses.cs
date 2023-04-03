using GPC.Model.LoadCases;
using GPC.Model.Results;
using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Checkers
{
    public class BoltStresses : IEquatable<BoltStresses>
    {
        /// <summary>
         /// An easy way in BoltStresses to discriminate between ultimate state limit and service state limit.
         /// </summary>
        public enum CombCaseType
        {
            SLU,
            SLS
        }

        public ILoadCase LoadCase;
        public ResultBeamForces ResBeamForces;
        public CombCaseType CombCase;

        public BoltStresses(ILoadCase _loadcase, ResultBeamForces resbeam, CombCaseType combCase)
        {
            LoadCase = _loadcase;
            ResBeamForces = resbeam;
            CombCase = combCase;
        }

        public override bool Equals(object obj) => Equals(obj as BoltStresses);

        public bool Equals(BoltStresses other)
        {
            return EqualityComparer<ILoadCase>.Default.Equals(LoadCase, other.LoadCase) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(ResBeamForces, other.ResBeamForces) &&
                   EqualityComparer<CombCaseType>.Default.Equals(CombCase, other.CombCase);
        }

        public override int GetHashCode()
        {
            int hashCode = -1030903623;
            hashCode = hashCode * -1521134295 + EqualityComparer<ILoadCase>.Default.GetHashCode(LoadCase);
            hashCode = hashCode * -1521134295 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(ResBeamForces);
            hashCode = hashCode * -1521134295 + EqualityComparer<CombCaseType>.Default.GetHashCode(CombCase);
            return hashCode;
        }
    }
}
