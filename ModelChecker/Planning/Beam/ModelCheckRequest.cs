using GPC.Model.Checking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    public sealed class ModelCheckRequest
    {
        public List<ModelCheckJob> Jobs { get; } = new List<ModelCheckJob>();
    }
}
