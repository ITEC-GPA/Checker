using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core.Diagnostics;

namespace GPC.Model.Checker
{
    internal static class NativeResults
    {
        internal static string Version(Type type) => type.Assembly.GetName().Version + "/" + type.Assembly.ManifestModule.ModuleVersionId
            + "/adapter:" + typeof(NativeResults).Assembly.ManifestModule.ModuleVersionId;
        internal static CheckResult Unavailable(string code, string message = null) => new CheckResult { Data = DataStatus.NotSupported,
            Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error(code, message: message) } };
        internal static CheckResult Missing(string code) => new CheckResult { Data = DataStatus.Insufficient, Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error(code) } };
        internal static CheckResult Decision(NativeMethodDetails details)
        {
            var r = new CheckResult { Details = details, Execution = ExecutionStatus.Completed, Data = DataStatus.Ready };
            if (details.Metrics.Count == 0 || details.Metrics.Any(m => !m.Utilization.HasValue && !m.Passed.HasValue)) { r.Data = DataStatus.Insufficient; return r; }
            bool pass = details.Metrics.All(m => m.Passed ?? m.Utilization <= 1);
            if (details.Metrics.All(m => m.Utilization.HasValue)) r.Utilization = details.Metrics.Max(m => m.Utilization);
            r.Outcome = pass ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied; return r;
        }
    }
}
