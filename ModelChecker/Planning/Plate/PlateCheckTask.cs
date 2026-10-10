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
using GPC.Model.Core.Diagnostics;
using GPC.Model.Results.Queries;

namespace GPC.Model.Checker
{
    public sealed class PlateCheckTask
    {
        internal PreparedElementSample Row;
        internal PlateCheckSpecification Specification;
        internal IPlateChecker Engine;
        internal string EngineKey;
        internal CheckResult Evidence;
        public string Id { get; internal set; }
        public int ElementId => Row.Element.Id;
        public PlateCheckSpecification Check => Specification.Copy();
        public ResultSelection Selection => Row.Selection.Copy();
        public ShellCheckInput Input => Row.Shell?.Input;
        public DataStatus Data => Row.Shell?.Status ?? DataStatus.Insufficient;
        public string CheckerId => Engine?.Id;
    }
}
