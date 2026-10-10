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
    public interface IMaterialCheckSession
    {
        int CreatedCheckers { get; }
        CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token);
    }
}
