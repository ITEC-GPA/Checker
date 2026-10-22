using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    public sealed class SteelCompressionCurves
    {
        public SteelBucklingCurve First { get; }
        public SteelBucklingCurve Second { get; }
        public SteelBucklingCurve Torsional { get; }
        public string Source { get; }
        internal SteelCompressionCurves(SteelBucklingCurve first,SteelBucklingCurve second,string source)
        { First=first;Second=second;Torsional=(SteelBucklingCurve)Math.Max((int)first,(int)second);Source=source; }
    }

    /// <summary>Optional preparation, not a member verifier. Gross elastic properties and explicit schemes.
    /// N/mm/MPa/Nmm; normative curve selection and elastic eigenvalues remain separate.</summary>
    public static class SteelAutomaticStability
    {
        public static SteelCompressionCurves CompressionCurves(SteelSectionInput input,SectionResistanceStandard standard)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(standard!=SectionResistanceStandard.Eurocode3_2005&&standard!=SectionResistanceStandard.Ntc2018)
                throw new ArgumentOutOfRangeException(nameof(standard));
            var s=input.CreateSectionCopy();var shape=s.SectionShape;var type=shape.GetType();
            SteelMethodSelector.ValidateGeometry(s);
            if(!new[]{235d,275d,355d,420d,460d}.Contains(input.YieldStrength))
                throw new NotSupportedException("Automatic curves require a steel grade covered by the selected table; use explicit curves otherwise.");
            if(!SteelMethodSelector.KnownGeometry(shape)||SteelMethodSelector.ColdFormedOpen(s)||type==typeof(SectionBuiltUp))
                throw new NotSupportedException("Automatic curves are not qualified for this family/production route.");
            bool high=input.YieldStrength==460;
            SteelBucklingCurve first,second;
            if(type==typeof(SectionH)) {
                var h=(SectionH)shape;
                if(!h.IsDoubleSymmetric)throw new NotSupportedException("Automatic H curves require equal flanges.");
                var hCurves=HCurves(h,s.SectionType,high);
                first=hCurves.First;second=hCurves.Second;
            } else if(shape is SectionRHS rhs&&s.SectionType==Section.SectionTypes.Welded) {
                bool c=type!=typeof(SectionRHS)||rhs.ThicknessTop!=rhs.ThicknessBottom||rhs.ThicknessWebLeft!=rhs.ThicknessWebRight
                    ||Ec3WeldedBucklingPolicy.BoxRequiresCurveC(rhs.Base,rhs.Height,rhs.ThicknessTop,rhs.ThicknessWebLeft,rhs.R==0?(double?)null:rhs.R);
                first=second=c?SteelBucklingCurve.C:SteelBucklingCurve.B;
            } else if(shape is SectionRHS||type==typeof(SectionCHS)) {
                first=second=s.FormedType==Section.FormedTypes.ColdFormed?SteelBucklingCurve.C:high?SteelBucklingCurve.A0:SteelBucklingCurve.A;
            } else if(type==typeof(SectionL)) first=second=SteelBucklingCurve.B;
            else if(type==typeof(SectionC)||type==typeof(SectionT)||type==typeof(SectionRectangular)||type==typeof(SectionCircular)) first=second=SteelBucklingCurve.C;
            else throw new NotSupportedException("Automatic curve selection not qualified.");
            string reference=standard==SectionResistanceStandard.Ntc2018?"NTC 2018 Table 4.2.VIII":"EN 1993-1-1:2005 Table 6.2";
            return new SteelCompressionCurves(first,second,reference+"; geometric-to-principal axis mapping; conservative torsional curve = worse flexural curve.");
        }

        // Shared with the legacy checker: one implementation of the table rows.
        internal static SteelCompressionCurves HCurves(SectionH h,Section.SectionTypes fabrication,bool high)
        {
            SteelBucklingCurve y,z;
            if(fabrication==Section.SectionTypes.Welded) {
                bool thick=h.ThicknessTopFlange>40;y=thick?SteelBucklingCurve.C:SteelBucklingCurve.B;z=thick?SteelBucklingCurve.D:SteelBucklingCurve.C;
            } else if(fabrication==Section.SectionTypes.Rolled) {
                if(h.Height/h.LenghtTopFlange>1.2) {
                    if(h.ThicknessTopFlange<=40) { y=high?SteelBucklingCurve.A0:SteelBucklingCurve.A;z=high?SteelBucklingCurve.A0:SteelBucklingCurve.B; }
                    else if(h.ThicknessTopFlange<=100) { y=high?SteelBucklingCurve.A:SteelBucklingCurve.B;z=high?SteelBucklingCurve.A:SteelBucklingCurve.C; }
                    else throw new NotSupportedException("No rolled I table row for h/b>1.2 and tf>100 mm.");
                } else if(h.ThicknessTopFlange<=100) { y=high?SteelBucklingCurve.A:SteelBucklingCurve.B;z=high?SteelBucklingCurve.A:SteelBucklingCurve.C; }
                else y=z=high?SteelBucklingCurve.C:SteelBucklingCurve.D;
            } else throw new NotSupportedException("H fabrication must be explicitly rolled or welded.");
            return new SteelCompressionCurves(h.Jxx>=h.Jyy?y:z,h.Jxx>=h.Jyy?z:y,"EN 1993-1-1:2005 Table 6.2 / NTC 2018 Table 4.2.VIII");
        }

        /// <summary>Caller declares a complete isolated prismatic member with fork ends, free warping,
        /// same sinusoidal mode/length in both planes and torsion, no intermediate restraints or offsets.
        /// Compression is constant/concentric; Mcr uses uniform major-axis moment, H doubly symmetric only.</summary>
        public static IReadOnlyList<SteelStabilityValue> UniformFork(SteelVerificationInput input,bool bending,string declaration)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(input.Member==null||string.IsNullOrWhiteSpace(declaration))throw new ArgumentException("Explicit full-member length and fork/free-warping declaration required.");
            var a=input.Actions;
            if(a.T!=0||a.V1!=0||a.V2!=0||a.M2!=0||(bending?(a.N!=0||a.M1==0):(a.M1!=0||a.N>=0)))
                throw new NotSupportedException("Uniform fork preparation requires pure constant concentric compression or pure uniform major-axis bending.");
            var s=input.Section.CreateSectionCopy();var shape=s.SectionShape;var type=shape.GetType();
            SteelMethodSelector.ValidateGeometry(s);
            if(SteelMethodSelector.ColdFormedOpen(s)||!(type==typeof(SectionH)||type==typeof(SectionC)||type==typeof(SectionT)||type==typeof(SectionL)))
                throw new NotSupportedException("Automatic torsional eigenvalues require qualified exact open H/C/T/L geometry; other families use explicit values or mode assessments.");
            if(type==typeof(SectionC)&&(((SectionC)shape).LengthTop!=((SectionC)shape).LengthBottom||((SectionC)shape).ThicknessTop!=((SectionC)shape).ThicknessBottom))
                throw new NotSupportedException("Automatic channel torsional properties require equal flanges.");
            double length=input.Member.Length;
            foreach(var effective in new[]{input.Member.BucklingLength1,input.Member.BucklingLength2})
                if(effective.HasValue&&Math.Abs(effective.Value-length)>1e-10*length)throw new ArgumentException("Effective lengths conflict with the declared full-span fork scheme.");
            double e=input.Section.ElasticModulus,g=e/(2*(1+input.Section.PoissonRatio));
            double ix=s.J11,iy=s.J22,j=s.Jt,iw=s.Jw,area=s.Area;
            foreach(var v in new[]{ix,iy,j,area})SteelInputValidation.Positive(v,"gross section property");
            SteelInputValidation.Finite(iw,"Iw");if(iw<0)throw new ArgumentOutOfRangeException("Iw");
            double n1=ElasticStabilityArithmetic.Euler(length,ix,e),n2=ElasticStabilityArithmetic.Euler(length,iy,e);
            double kt=g*j+Math.PI*Math.PI*e*iw/(length*length);
            string source="Gross elastic uniform fork/free-warping sine mode, L="+length.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" mm; "+declaration;
            if(bending) {
                if(type!=typeof(SectionH)||!((SectionH)shape).IsDoubleSymmetric||Math.Abs(shape.AngleX1)>1e-10)
                    throw new NotSupportedException("Automatic uniform Mcr currently requires a doubly symmetric H with horizontal major axis.");
                return Array.AsReadOnly(new[]{new SteelStabilityValue(SteelStabilityQuantity.Mcr,Math.Sqrt(n2)*Math.Sqrt(kt),SteelStabilityUnit.Nmm,source)});
            }
            double dx=s.ShearCenter.X-s.Centroid.X,dy=s.ShearCenter.Y-s.Centroid.Y;
            double c=Math.Cos(shape.AngleX1),sn=Math.Sin(shape.AngleX1),x=c*dx+sn*dy,y=-sn*dx+c*dy;
            double i02=(ix+iy)/area+x*x+y*y;SteelInputValidation.Positive(i02,"polar radius squared");
            double nt=kt/i02,nft=ElasticStabilityArithmetic.FlexuralTorsional(n1,n2,nt,x,y,i02);
            return Array.AsReadOnly(new[]{
                new SteelStabilityValue(SteelStabilityQuantity.Ncr1,n1,SteelStabilityUnit.N,source),
                new SteelStabilityValue(SteelStabilityQuantity.Ncr2,n2,SteelStabilityUnit.N,source),
                new SteelStabilityValue(SteelStabilityQuantity.NcrT,nt,SteelStabilityUnit.N,source),
                new SteelStabilityValue(SteelStabilityQuantity.NcrFT,nft,SteelStabilityUnit.N,source)});
        }
    }
}
