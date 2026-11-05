using System;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Twist fixed at BOTH physical ends. The labels specify warping only;
    /// they are not flexural boundary conditions or effective lengths for Mcr.</summary>
    public enum SteelIdealWarpingBoundary { FreeBothEnds, FixedOneEnd, FixedBothEnds }

    public static partial class SteelMemberSchemePreparation
    {
        /// <summary>Exact uniform-compression torsional eigenvalue of a doubly symmetric
        /// prismatic H: EIomega theta''''+(N i0²-GIt)theta''=0. Twist zero at both ends;
        /// warping either free (bimoment zero) or fixed (theta'=0). Gross properties.
        /// No spring, distortion, local plate restraint or coupled asymmetric mode is inferred.</summary>
        public static SteelStabilityValue TorsionalMode(SteelSectionInput section,double length,
            SteelIdealWarpingBoundary boundary,string source)
        {
            if(section==null)throw new ArgumentNullException(nameof(section));
            SteelInputValidation.Positive(length,nameof(length));
            if(!Enum.IsDefined(typeof(SteelIdealWarpingBoundary),boundary))throw new ArgumentOutOfRangeException(nameof(boundary));
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Physical twist/warping restraint declaration required.",nameof(source));
            var s=section.ReadOnlySection;SteelMethodSelector.ValidateGeometry(s);
            if(section.Unperforated!=true || s.SectionShape.GetType()!=typeof(SectionH)
                || !s.SectionShape.IsDoubleSymmetric || SteelMethodSelector.ColdFormedOpen(s))
                throw new NotSupportedException("Unperforated doubly symmetric prismatic H required for the uncoupled torsional eigenmode.");
            foreach(double v in new[]{s.Area,s.J11,s.J22,s.Jt})SteelInputValidation.Positive(v,"gross section property");
            SteelInputValidation.Finite(s.Jw,"gross warping constant");
            if(s.Jw<0)throw new ArgumentOutOfRangeException("Iw");
            double k=boundary==SteelIdealWarpingBoundary.FixedBothEnds?.5
                :boundary==SteelIdealWarpingBoundary.FixedOneEnd?Math.PI/4.493409457909064:1;
            double value=ElasticStabilityArithmetic.Torsional(section.ElasticModulus,section.PoissonRatio,
                Math.Sqrt(s.J11/s.Area),Math.Sqrt(s.J22/s.Area),0,0,s.Jt,s.Jw,k*length);
            return new SteelStabilityValue(SteelStabilityQuantity.NcrT,value,SteelStabilityUnit.N,
                "Vlasov uniform-compression torsional eigenproblem; twist fixed at both ends; warping="+boundary
                +"; L="+F(length)+"mm; kw="+F(k)+"; NcrT=(GIt+E Iw (pi/(kw L))^2)/i0^2; gross properties; "+source);
        }

        /// <summary>Conservative Mcr bound for an H with end lateral translations and
        /// twist fixed, including additional rotational/warping restraints. Release
        /// the additional restraints and replace the actual same-case diagram by its
        /// uniform absolute maximum: C1=1. Loading through shear centre only.
        /// Does not credit restraint stiffness or claim the exact restrained eigenvalue.</summary>
        public static SteelPreparedCriticalMoment EndRestrainedBendingLowerBound(SteelSectionInput section,
            SteelMemberInput member,SteelForkMomentDiagram diagram,string source)
        {
            if(section==null||member==null||diagram==null)throw new ArgumentNullException("preparation input");
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("End restraint/load-model source required.",nameof(source));
            if(diagram.LoadHeight!=0)throw new NotSupportedException("The restrained-span lower bound requires loads through the shear centre.");
            foreach(var end in new[]{member.StartRestraints,member.EndRestraints})
                if(end==null || end.Translation1!=SteelRestraintState.Fixed || end.Translation2!=SteelRestraintState.Fixed
                    || end.Rotation3!=SteelRestraintState.Fixed)
                    throw new NotSupportedException("Both end lateral translations and twist must be explicitly fixed. Unknown or free cannot establish the bound.");
            if(diagram.Shape==SteelForkMomentShape.GenericEnvelope && diagram.Stations.Last().Position!=member.Length)
                throw new ArgumentException("Moment stations belong to another physical span.");
            var uniformDiagram=SteelForkMomentDiagram.EndMoments(diagram.MaximumMoment,diagram.MaximumMoment,
                "Uniform absolute envelope of the same-case diagram: "+diagram.Source);
            var relaxed=ForkBending(section,new SteelMemberInput(member.Length),uniformDiagram,
                "Released end bending rotations and warping; physical lateral translations/twist retained; "+source);
            string reference=relaxed.UniformMoment.Source+"; LOWER BOUND ONLY: additional end restraints released; actual same-case diagram="+diagram.Source;
            var value=new SteelStabilityValue(SteelStabilityQuantity.Mcr,relaxed.UniformMoment.Value,SteelStabilityUnit.Nmm,reference);
            return new SteelPreparedCriticalMoment(section,member,diagram,value,value,
                relaxed.Trace.Concat(new[]{V("AdditionalEndRestraintsReleased",1,"1"),V("ConservativeCriticalMomentLowerBound",1,"1")}),true);
        }
    }
}
