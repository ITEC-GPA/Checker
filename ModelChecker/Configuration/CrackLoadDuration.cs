using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{

    /// <summary>Duration of the load for the tension stiffening of the crack width (kt 0.4 long term, 0.6 short term).</summary>
    public enum CrackLoadDuration { LongTerm, ShortTerm }
}
