using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker
{
    public sealed class MultiMaterialCheckJob
    {
        public string Name { get; set; }
        public BeamCheckPlanRequest Plan { get; set; }
        public MaterialCheckerAssignment[] Assignments { get; set; } = new MaterialCheckerAssignment[0];
    }
}
