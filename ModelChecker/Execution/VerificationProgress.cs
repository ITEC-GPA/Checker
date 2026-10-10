using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Identity;

namespace GPC.Model.Checker
{
    public sealed class VerificationProgress
    {
        public int Completed { get; internal set; }
        public int Total { get; internal set; }
        public string Job { get; internal set; }
        public EntityFamily Family { get; internal set; }
        public int ElementId { get; internal set; }
        public CheckTargetReference Target { get; internal set; }
    }
}
