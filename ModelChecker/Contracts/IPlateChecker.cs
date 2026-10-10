using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Model.Checker.Configuration;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Results.Locations;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    public interface IPlateChecker
    {
        string Id { get; }
        string Version { get; }
        string Configuration { get; }
        CheckStandardContext Standard { get; }
        bool Accepts(AreaElement element);
        bool Supports(PlateCheckSpecification check);
        IPlateCheckSession CreateSession();
    }
}
