using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Model.Checker.Configuration;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Results.Locations;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;

namespace GPC.Model.Checker
{
    public interface IPlateCheckSession
    {
        int CreatedCheckers { get; }
        CheckResult Verify(ShellCheckInput input, PlateCheckSpecification check, CancellationToken token);
    }
}
