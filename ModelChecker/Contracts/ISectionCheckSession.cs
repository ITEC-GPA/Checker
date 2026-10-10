using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;

namespace GPC.Model.Checker
{
    /// <summary>Session able to run tasks with direction, sub-check and combination category.</summary>
    public interface ISectionCheckSession : IMaterialCheckSession
    {
        bool Supports(SectionCheckSpecification check);
        CheckResult Verify(BeamActionInput input, SectionCheckSpecification check, CancellationToken token);
    }
}
