using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;

namespace GPC.Model.Checker
{
    public sealed class ModelCheckJob
    {
        public string Name { get; set; }
        public PreparationRequest Preparation { get; set; }
        public BeamCheckPlanRequest BeamPlan { get; set; }
        public ConcreteVerificationOptions Options { get; set; }
    }
}
