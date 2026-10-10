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
    public sealed class MaterialCheckerAssignment
    {
        /// <summary>Null matches the job's full selection, still filtered by the checker's material capability.</summary>
        public ElementSelection Selection { get; set; }
        public IMaterialChecker Checker { get; set; }
        /// <summary>Null means all mechanisms. An empty array selects none.</summary>
        public CheckMechanism[] Mechanisms { get; set; }
        /// <summary>Optional exact discriminators. Null also matches legacy mechanism-only tasks.</summary>
        public SectionCheckSpecification[] Checks { get; set; }
        internal bool Matches(CheckMechanism mechanism, SectionCheckSpecification check) => (Mechanisms == null || Mechanisms.Contains(mechanism))
            && (Checks == null || check != null && Checks.Any(c => c.Key == check.Key));
    }
}
