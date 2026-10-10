using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Model.Elements;
using GPC.Model.Core;

namespace GPC.Model.Checker
{
    public sealed class MultiMaterialCheckRequest
    {
        public List<MultiMaterialCheckJob> Jobs { get; } = new List<MultiMaterialCheckJob>();
    }
}
