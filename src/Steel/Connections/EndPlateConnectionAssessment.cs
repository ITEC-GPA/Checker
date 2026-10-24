using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Consistent local moment, bolt and weld assessments for one immutable
    /// end-plate case. It neither derives a complete-joint resistance nor certifies
    /// net/block failure, supporting members, rotation or execution details.</summary>
    public sealed class EndPlateConnectionAssessmentResult
    {
        public EndPlateBoltComponentResult Bolts { get; }
        public EndPlateWeldComponentResult FilletWelds { get; }
        public TopFlushCjpEndPlateWeldResult TopFlushCjpWelds { get; }
        public EndPlateMomentComponentResult MomentComponents => Bolts.MomentComponents;
        public EndPlateMomentComponentResult WeldMomentComponents => FilletWelds != null
            ? FilletWelds.MomentComponents : TopFlushCjpWelds.MomentComponents;
        public bool IsSameMomentAssessment { get; }
        public ConnectionStatus CoveredComponentsStatus { get; }
        public bool HasFailedComponent { get; }
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingWholeJointChecks { get; }
        public ConnectionCheck GoverningCheck => Checks.Count == 0 ? null : Checks.OrderByDescending(c => c.Utilization).First();
        public double? CoveredComponentsUtilization => GoverningCheck?.Utilization;
        public string Scope => "Local moment components, prescribed bolt shear with full-tension envelope, and the selected physical weld detail. Complete-joint resistance, net/block, supporting-member and global-model qualification remain open.";

        internal EndPlateConnectionAssessmentResult(EndPlateBoltComponentResult bolts,
            EndPlateWeldComponentResult fillets, TopFlushCjpEndPlateWeldResult cjp)
        {
            Bolts = bolts; FilletWelds = fillets; TopFlushCjpWelds = cjp;
            IsSameMomentAssessment = ReferenceEquals(MomentComponents, WeldMomentComponents);
            ConnectionStatus weldStatus = fillets != null ? fillets.WeldAssessment.Status : cjp.WeldAssessment.Status;
            var states = new[] { MomentComponents.ComponentAssessment.Status, bolts.BoltAssessment.Status,
                WeldMomentComponents.ComponentAssessment.Status, weldStatus };
            HasFailedComponent = states.Contains(ConnectionStatus.Failed);
            CoveredComponentsStatus = !IsSameMomentAssessment || states.Contains(ConnectionStatus.NotSupported)
                ? ConnectionStatus.NotSupported : HasFailedComponent ? ConnectionStatus.Failed : ConnectionStatus.Passed;
            var issues = new List<string>();
            if (!IsSameMomentAssessment) issues.Add("SAME_IMMUTABLE_MOMENT_ASSESSMENT_REQUIRED");
            issues.AddRange(MomentComponents.ComponentAssessment.Diagnostics.Select(d => "Moment:" + d));
            issues.AddRange(bolts.BoltAssessment.Diagnostics.Select(d => "Bolts:" + d));
            issues.AddRange(WeldMomentComponents.ComponentAssessment.Diagnostics.Select(d => "WeldMoment:" + d));
            issues.AddRange((fillets != null ? fillets.WeldAssessment.Diagnostics : cjp.WeldAssessment.Diagnostics)
                .Select(d => "Welds:" + d));
            Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray());
            IEnumerable<ConnectionCheck> weldChecks = fillets != null ? fillets.WeldAssessment.Checks : cjp.WeldAssessment.Checks;
            Checks = Array.AsReadOnly(CoveredComponentsStatus == ConnectionStatus.NotSupported
                ? Array.Empty<ConnectionCheck>()
                : MomentComponents.ComponentAssessment.Checks.Concat(bolts.BoltAssessment.Checks).Concat(weldChecks).ToArray());
            // Keep child qualifications visible. Do not remove a whole pending item merely
            // because one of its named mechanisms has an applicable component calculation.
            var pending = MomentComponents.PendingChecks.Concat(WeldMomentComponents.PendingChecks)
                .Concat(bolts.PendingChecks).Concat(fillets != null ? fillets.PendingChecks : cjp.PendingChecks);
            PendingWholeJointChecks = Array.AsReadOnly(pending.Distinct().ToArray());
        }
    }

    public static class EndPlateConnectionAssessment
    {
        /// <summary>Extended plate with paired fillets. Both adapters must retain the
        /// SAME moment-result instance. Equal numeric actions or source text are not
        /// sufficient evidence that separately constructed assessments form one case.</summary>
        public static EndPlateConnectionAssessmentResult Verify(EndPlateBoltComponentInput bolts,
            EndPlateWeldComponentInput welds)
        {
            if (bolts == null) throw new ArgumentNullException(nameof(bolts));
            if (welds == null) throw new ArgumentNullException(nameof(welds));
            return new EndPlateConnectionAssessmentResult(EndPlateBoltComponents.Verify(bolts),
                EndPlateWeldComponents.Verify(welds), null);
        }

        /// <summary>Top-flush CJP with real inner toe and lower plate projection. This
        /// overload does not reinterpret the fillet model as a complete-penetration weld.</summary>
        public static EndPlateConnectionAssessmentResult Verify(EndPlateBoltComponentInput bolts,
            TopFlushCjpEndPlateWeldInput welds)
        {
            if (bolts == null) throw new ArgumentNullException(nameof(bolts));
            if (welds == null) throw new ArgumentNullException(nameof(welds));
            return new EndPlateConnectionAssessmentResult(EndPlateBoltComponents.Verify(bolts),
                null, TopFlushCjpEndPlateWeldComponents.Verify(welds));
        }
    }
}
