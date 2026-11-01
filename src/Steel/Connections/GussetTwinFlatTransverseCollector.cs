using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>A centred gusset T attachment on a rectangular transverse collector.
    /// The collector spans along Z, with full-width simple support lines at +/-Span/2.
    /// Width is along Y, thickness along X, exactly the welded support face dimensions.
    /// Reactions are actual forces ON the collector at those lines in the gusset axes.
    /// No unmodelled axial load, holes, offsets or end moments are admitted.</summary>
    public sealed class GussetTwinFlatTransverseCollectorInput
    {
        public GussetTwinFlatWeldedSupportInput Attachment { get; }
        public ConnectionPlate Collector { get; }
        public double Span { get; }
        public double ElasticModulus { get; }
        public PlanarConnectionActions NegativeEndReaction { get; }
        public PlanarConnectionActions PositiveEndReaction { get; }
        public string SimpleSupportLinesSource { get; }
        public string SameCaseSource { get; }
        public bool UnperforatedConstantSection { get; }
        public GussetTwinFlatTransverseCollectorInput(GussetTwinFlatWeldedSupportInput attachment,
            ConnectionPlate collector,double span,double elasticModulus,
            PlanarConnectionActions negativeEndReaction,PlanarConnectionActions positiveEndReaction,
            string simpleSupportLinesSource,string sameCaseSource,bool unperforatedConstantSection)
        {
            Attachment=attachment??throw new ArgumentNullException(nameof(attachment));
            Collector=collector??throw new ArgumentNullException(nameof(collector));
            NegativeEndReaction=negativeEndReaction??throw new ArgumentNullException(nameof(negativeEndReaction));
            PositiveEndReaction=positiveEndReaction??throw new ArgumentNullException(nameof(positiveEndReaction));
            ConnectionValidation.Positive(span,nameof(span));ConnectionValidation.Positive(elasticModulus,nameof(elasticModulus));
            Span=span;ElasticModulus=elasticModulus;SimpleSupportLinesSource=simpleSupportLinesSource;
            SameCaseSource=sameCaseSource;UnperforatedConstantSection=unperforatedConstantSection;
        }
    }

    public sealed class GussetTwinFlatTransverseCollectorResult
    {
        public GussetTwinFlatWeldedSupportResult Attachment { get; }
        public ConnectionVerification<GussetTwinFlatTransverseCollectorInput> Collector { get; }
        public bool IsStaticResistanceComplete=>Attachment.IsComplete&&Collector.IsComplete;
        public ConnectionStatus Status=>Attachment.Status==ConnectionStatus.Failed||Collector.Status==ConnectionStatus.Failed
            ?ConnectionStatus.Failed:IsStaticResistanceComplete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
        public double? Utilization=>IsStaticResistanceComplete?(double?)Math.Max(Attachment.Utilization.Value,Collector.Utilization.Value):null;
        public string Scope=>"Static tension terminal through bolts, gusset, two fillets and a transverse rectangular collector, bounded at its two declared simple support lines. Supporting structures, their end attachments, frame stability and fatigue remain outside this boundary.";
        internal GussetTwinFlatTransverseCollectorResult(GussetTwinFlatWeldedSupportResult attachment,
            ConnectionVerification<GussetTwinFlatTransverseCollectorInput> collector)
        {Attachment=attachment;Collector=collector;}
    }

    public static class GussetTwinFlatTransverseCollector
    {
        public static GussetTwinFlatTransverseCollectorResult Verify(GussetTwinFlatTransverseCollectorInput p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            var attachment=GussetTwinFlatWeldedSupport.Verify(p.Attachment);
            var issues=new List<string>();var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();
            var a=p.Attachment;var c=p.Collector;var g=a.Transfer.Gusset;var code=a.Transfer.Code;
            Gate(attachment.IsComplete,"ATTACHMENT_OUTSIDE_CENTRED_TENSION_FIELD");
            Gate(c.Width==g.Width&&c.Width==a.SupportFaceWidth&&c.Thickness==a.SupportMaterial.Thickness
                &&c.Fu==a.SupportMaterial.Fu,"MATCHING_FULL_WIDTH_COLLECTOR_AND_WELD_PARENT_REQUIRED");
            Gate(c.Fy<=(int)a.SupportMaterial.Grade&&c.Fy<=460,"COLLECTOR_YIELD_EXCEEDS_DECLARED_GRADE");
            Gate(p.UnperforatedConstantSection,"UNPERFORATED_CONSTANT_RECTANGULAR_COLLECTOR_REQUIRED");
            Gate(p.Span<=a.SupportFaceDepth&&p.Span>g.Thickness+2*Math.Sqrt(2)*a.Throat,"SUPPORT_LINES_MUST_ENCLOSE_THE_ENTIRE_WELD_FOOTPRINT");
            Gate(!string.IsNullOrWhiteSpace(p.SimpleSupportLinesSource)&&!string.IsNullOrWhiteSpace(p.SameCaseSource),"ACTUAL_SUPPORT_LINES_AND_SAME_CASE_EVIDENCE_REQUIRED");
            double force=-attachment.ActionsOnSupport.Fx;
            foreach(var end in new[]{(A:p.NegativeEndReaction,Z:-p.Span/2),(A:p.PositiveEndReaction,Z:p.Span/2)})
            {
                var r=end.A;
                Gate(Close(r.Fx,force/2)&&r.Fy==0&&r.Fz==0&&r.Mx==0&&r.My==0&&r.Mz==0
                    &&r.ApplicationX==a.SupportDistance&&r.ApplicationY==0&&r.ApplicationZ==end.Z,
                    "CONCOMITANT_SIMPLE_SUPPORT_REACTIONS_REQUIRED");
            }
            if(issues.Count==0)
            {
                // Statics plus rectangular elastic section. Replacing the two symmetric
                // fillet lines by a central line load increases M and deflection; no
                // two-way plate spreading, weld reinforcement or membrane action credited.
                double area=c.Width*c.Thickness,inertia=area*c.Thickness*c.Thickness/12;
                double moment=force*p.Span/4,sigma=moment*c.Thickness/(2*inertia),tau=1.5*force/(2*area);
                double equivalent=ElasticBoltGroup.Hypot(sigma,Math.Sqrt(3)*tau);
                double displacement=force*Math.Pow(p.Span,3)/(48*p.ElasticModulus*inertia);
                // Conservative rectangular shear compliance, nu=.3, kappa=5/6.
                displacement+=force*p.Span/(4*(5d/6)*(p.ElasticModulus/2.6)*area);
                double rotation=force*p.Span*p.Span/(16*p.ElasticModulus*inertia);
                Value("Collector.Area",area,"mm2");Value("Collector.Inertia",inertia,"mm4");
                Value("Collector.MaximumMoment",moment,"Nmm");Value("Collector.MaximumShear",force/2,"N");
                Value("Collector.MaximumNormalStress",sigma,"MPa");Value("Collector.MaximumShearStress",tau,"MPa");
                Value("Collector.CentreDisplacement",displacement,"mm");Value("Collector.EndRotation",rotation,"rad");
                Value("Collector.EachEndReaction",force/2,"N");
                // A declared linear model needs a small-deformation domain. This is a
                // library applicability limit, not an EC3 serviceability limit.
                Gate(displacement<=p.Span/200&&rotation<=.01,"COLLECTOR_LINEAR_SMALL_DEFORMATION_DOMAIN_EXCEEDED");
                if(double.IsNaN(equivalent)||double.IsInfinity(equivalent))issues.Add("COLLECTOR_NUMERIC_RANGE");
                else checks.Add(new ConnectionCheck("Collector.ElasticBendingAndShear",equivalent,c.Fy/code.Plates.GammaM0,
                    code.IsNtc?"NTC2018 4.2.4.1.2, elastic stress verification; conservative maxima envelope"
                        :"EN1993-1-1:2005 6.2.1(4),(5); conservative elastic normal/shear maxima envelope","MPa"));
            }
            return new GussetTwinFlatTransverseCollectorResult(attachment,new ConnectionVerification<GussetTwinFlatTransverseCollectorInput>(p,code,
                "Steel.Connection.Gusset.TwinFlat.TransverseCollector","Rectangular transverse collector at two simple support lines, centred static tension only.",
                checks,trace,issues.Distinct(),new[]{
                    "Actual full-width simple supports, equal same-case reactions at Z=+/-Span/2. The support attachment beyond each boundary is not inferred from equilibrium.",
                    "Uniform line force over the gusset/collector common width; no effective width increase, perforations, eccentricity, support moments or other collector loading.",
                    "M=P L/4, V=P/2, rectangular elastic stresses; independent maxima combined conservatively. Load footprint collapsed to centre, no plate/membrane enhancement.",
                    "Displacement includes rectangular shear compliance (nu=.3). L/200 and .01rad are library small-deformation applicability limits, not serviceability certification."}));
            void Gate(bool condition,string issue){if(!condition)issues.Add(issue);}
            void Value(string name,double value,string unit){if(double.IsNaN(value)||double.IsInfinity(value))issues.Add("COLLECTOR_NUMERIC_RANGE");else trace.Add(new SectionResistanceValue(name,value,unit));}
        }
        private static bool Close(double a,double b)=>Math.Abs(a-b)<=1e-9*Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)));
    }
}
