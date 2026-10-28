using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Bound standard LOCAL joint, EN1993-1-8 6.2.2/6.2.7.2 and SCI P3982.5.
    /// Rolled I/H, single-sided, unstiffened, two bolt columns, N=0, fixed tension side.
    /// Old component API qualifications are unchanged. No global member stability,
    /// plastic rotation capacity, seismic ductility or arbitrary plate/fracture model.</summary>
    public sealed class EndPlateStandardJointResult
    {
        public EndPlateLocalCaseAssessmentResult LocalCase { get; }
        public EndPlateInPlaneResult InPlane { get; }
        public EndPlateColumnCaseResult ColumnCase { get; }
        public bool IsComplete { get; }
        public bool HasFailedComponent { get; }
        public ConnectionStatus Status => HasFailedComponent?ConnectionStatus.Failed:IsComplete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => IsComplete;
        public bool CanQualifyWholeMember => false;
        /// <summary>Local joint bound at the retained shear and qualified column stress,
        /// NOT a resistance of the frame or a capacity for another load combination.</summary>
        public double? MomentResistance { get; }
        public EndPlateRotationalStiffnessResult RotationalModel { get; }
        public double? InitialStiffness => RotationalModel?.InitialStiffness;
        public double? SecantStiffness => RotationalModel?.SecantStiffness;
        public double? ModelRotation => RotationalModel?.ModelRotation;
        public double? AppliedGlobalSpring { get; }
        public bool? GlobalSpringMatches { get; }
        public bool RequiresGlobalReanalysis => GlobalSpringMatches==false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> ExternalConditions { get; }
        public ConnectionCheck GoverningCheck=>Checks.Count==0?null:Checks.OrderByDescending(c=>c.Utilization).First();
        internal EndPlateStandardJointResult(EndPlateLocalCaseAssessmentResult local,EndPlateInPlaneResult plane,
            EndPlateColumnCaseResult column,double? appliedSpring)
        {
            LocalCase=local;InPlane=plane;ColumnCase=column;AppliedGlobalSpring=appliedSpring;
            var issues=new List<string>();var checks=new List<ConnectionCheck>();
            if(!local.HasCoherentCase||!ReferenceEquals(plane.Bolts,local.Connection.Bolts)
                ||!ReferenceEquals(column.Input.Parent,local.BeamAndMoment))issues.Add("AllAssessmentsMustBelongToSameImmutableCase");
            if(!plane.IsComplete)issues.AddRange(plane.Diagnostics.Select(x=>"InPlane:"+x));
            if(!column.IsComplete)issues.AddRange(column.Diagnostics.Select(x=>"Column:"+x));
            if(local.HasUnsupportedComponent)issues.AddRange(local.Diagnostics.Concat(new[]{"CompleteLocalComponentsRequired"}));
            if(column.Input.ColumnFlangeFu!=local.Connection.Bolts.BoltAssessment.Input.ColumnFlangeFu)
                issues.Add("ColumnFlangeStrengthMustMatchBoltAssessment");
            // The flange/web weld adapters use full-strength normal envelopes plus the
            // actual V. Therefore their Passed result qualifies every M up to the bound;
            // an isolated weld utilization at the current M would NOT justify this export.
            var m=local.BeamAndMoment.Input;
            if(m.ExtensionGeometry==null&&local.Connection.TopFlushCjpWelds==null)issues.Add("TopFlushRequiresItsActualCjpDetail");
            if(m.ExtensionGeometry!=null&&local.Connection.FilletWelds==null)issues.Add("ExtendedPlateRequiresItsActualPairedFilletDetail");
            checks.AddRange(local.Checks);
            if(local.Checks.Count==0)
            {
                // Preserve adverse child evidence even when another component prevented
                // the legacy local aggregator from producing its combined check list.
                checks.AddRange(local.BeamAndMoment.BeamSection.Checks.Select(c=>c.WithId("BeamSection."+c.Id)));
                if(local.BeamAndMoment.MomentComponents!=null)checks.AddRange(local.BeamAndMoment.MomentComponents.ComponentAssessment.Checks);
                checks.AddRange(local.Connection.Bolts.BoltAssessment.Checks);
                if(local.Connection.FilletWelds!=null)checks.AddRange(local.Connection.FilletWelds.WeldAssessment.Checks);
                if(local.Connection.TopFlushCjpWelds!=null)checks.AddRange(local.Connection.TopFlushCjpWelds.WeldAssessment.Checks);
            }
            checks.AddRange(plane.Checks);checks.AddRange(column.Checks);
            HasFailedComponent=local.HasFailedComponent||plane.Status==ConnectionStatus.Failed||column.Status==ConnectionStatus.Failed;
            IsComplete=issues.Count==0&&column.ContactLimitedMomentResistance.HasValue;
            if(IsComplete)
            {
                MomentResistance=column.ContactLimitedMomentResistance;
                checks.Add(new ConnectionCheck("StandardJoint.Moment",m.Moment,MomentResistance.Value,
                    "EN1993-1-8 6.2.7.2 standard component method; full-strength weld/bolt envelopes at retained V; contact bound","Nmm"));
                HasFailedComponent|=checks.Any(x=>!x.Passed);
                if(!HasFailedComponent)
                {
                    var s=local.InitialStiffnessAssessment.InitialModel.Input;
                    RotationalModel=EndPlateRotationalStiffness.Calculate(new EndPlateRotationalStiffnessInput(s.Code,s.Column,s.CompressionWidth,
                        s.Configuration,s.Beta,s.Rows,0,m.Beam.Area*m.Beam.Fy/m.Policy.Code.Plates.GammaM0,m.Moment,MomentResistance.Value,
                        s.TransformationSource,"Same-case complete standard local joint bound",s.JointGeometrySource));
                    if(!RotationalModel.IsSupported){issues.AddRange(RotationalModel.Diagnostics);IsComplete=false;}
                    else if(appliedSpring.HasValue)
                        GlobalSpringMatches=Math.Abs(appliedSpring.Value-SecantStiffness.Value)<=1e-8*Math.Max(appliedSpring.Value,SecantStiffness.Value);
                }
            }
            Checks=Array.AsReadOnly(checks.ToArray());Diagnostics=Array.AsReadOnly(issues.Distinct().ToArray());
            ExternalConditions=Array.AsReadOnly(new[]{
                "Capacity applies only to this same N=0, fixed tension side, concomitant shear and conservative column-stress context.",
                "The ordinary EN/SCI component model covers out-of-plane plate bending/prying; supplementary in-plane paths are not a general mixed-mode fracture law.",
                "Actual hardware, consumables, full-size weld continuity/root transitions and closed contact execution must match the supplied details.",
                "Both adjoining members require their own stability verification; column cut actions must be projected to the declared common reference.",
                "The secant spring is demand-dependent. Reanalyse the global model when the applied spring differs; no automatic rigid/hinged frame classification is made.",
                "ModelRotation is M/Sj, not available rotation capacity; no plastic redistribution or seismic ductility is qualified."
            });
        }
    }

    public static class EndPlateStandardJoint
    {
        public static EndPlateStandardJointResult Verify(EndPlateLocalCaseAssessmentResult localCase,EndPlateInPlaneResult inPlane,
            EndPlateColumnCaseResult columnCase,double? appliedGlobalSpring=null)
        {
            if(localCase==null)throw new ArgumentNullException(nameof(localCase));
            if(inPlane==null)throw new ArgumentNullException(nameof(inPlane));
            if(columnCase==null)throw new ArgumentNullException(nameof(columnCase));
            if(appliedGlobalSpring.HasValue)ConnectionValidation.Positive(appliedGlobalSpring.Value,nameof(appliedGlobalSpring));
            return new EndPlateStandardJointResult(localCase,inPlane,columnCase,appliedGlobalSpring);
        }
    }
}
