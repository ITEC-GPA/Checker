using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    /// <summary>Factories describe actual capabilities; a fresh mutable session is used in each Verify call.</summary>
    public interface IMaterialChecker
    {
        string Id { get; }
        string Version { get; }
        string Configuration { get; }
        CheckStandardContext Standard { get; }
        bool Accepts(BeamElement element);
        IMaterialCheckSession CreateSession();
    }
}
