using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Model.Checker.Configuration;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Results.Locations;
using GPC.Model.Checking.Preparation;

namespace GPC.Model.Checker
{
    public sealed class PlateCheckPlan
    {
        internal PlateCheckJob Job;
        internal ModelPreparation Preparation;
        internal Func<bool> Current;
        public string Name => Job.Name;
        public string ScopeFingerprint => Preparation.ScopeFingerprint;
        public IReadOnlyList<PlateCheckTask> WorkItems { get; internal set; }
        public bool IsCurrent => Current();
    }
}
