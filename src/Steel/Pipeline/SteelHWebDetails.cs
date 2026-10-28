using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    public enum SteelWebBoundaryKind { Intermediate, EndSupport }

    /// <summary>One actual boundary of the panel, bound to the same FEM action state.
    /// Two identical full-height flats, continuous double fillets to web and both flanges.
    /// N/mm/MPa. X normal to web, Z along beam. Positive externalCompression includes
    /// the full applied reaction/patch at this boundary; webCompression is the integral
    /// of compressive longitudinal web stresses, enveloping both adjacent panels.
    /// AdjacentShear is the maximum absolute shear of the other panel, not a resultant
    /// subtracted from the shear of this panel. No load is inferred from a source string.</summary>
    public sealed class SteelWebBoundaryDetail
    {
        public SteelVerificationInput Input { get; }
        public SteelWebBoundaryKind Kind { get; }
        public double AdjacentPanelLength { get; }
        public double AdjacentShear { get; }
        public double Width { get; }
        public double Thickness { get; }
        public double WebWeldThroat { get; }
        public double FlangeWeldThroat { get; }
        public double ExternalCompression { get; }
        public double WebCompression { get; }
        public double LoadX { get; }
        public double LoadZ { get; }
        public double BearingWidth { get; }
        public double BearingLength { get; }
        public double EndDistance { get; }
        public double RigidEndPostSpacing { get; }
        public string Source { get; }
        public SteelWebBoundaryDetail(SteelVerificationInput input, SteelWebBoundaryKind kind,
            double adjacentPanelLength, double adjacentShear, double width, double thickness,
            double webWeldThroat, double flangeWeldThroat, double externalCompression, double webCompression,
            double loadX, double loadZ, double bearingWidth, double bearingLength, double endDistance,
            double rigidEndPostSpacing, string source)
        {
            Input=input??throw new ArgumentNullException(nameof(input));
            if(!Enum.IsDefined(typeof(SteelWebBoundaryKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            foreach(double v in new[]{width,thickness,webWeldThroat,flangeWeldThroat})SteelInputValidation.Positive(v,"detail geometry");
            foreach(double v in new[]{adjacentPanelLength,adjacentShear,externalCompression,webCompression,bearingWidth,bearingLength,endDistance,rigidEndPostSpacing})
            {SteelInputValidation.Finite(v,"detail value");if(v<0)throw new ArgumentOutOfRangeException("detail value");}
            SteelInputValidation.Finite(loadX,nameof(loadX));SteelInputValidation.Finite(loadZ,nameof(loadZ));
            if(kind==SteelWebBoundaryKind.Intermediate && (adjacentPanelLength<=0||endDistance!=0||rigidEndPostSpacing!=0))
                throw new ArgumentException("Intermediate boundary requires its real adjacent panel, no end-post geometry.");
            if(kind==SteelWebBoundaryKind.EndSupport && (adjacentPanelLength!=0||adjacentShear!=0||endDistance<=0))
                throw new ArgumentException("End support has one panel and a positive distance from the physical end.");
            if(externalCompression>0&&(bearingWidth<=0||bearingLength<=0))throw new ArgumentException("Loaded boundary requires the actual direct bearing footprint.");
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Same-case FEM forces and actual restraint/execution source required.");
            Kind=kind;AdjacentPanelLength=adjacentPanelLength;AdjacentShear=adjacentShear;Width=width;Thickness=thickness;
            WebWeldThroat=webWeldThroat;FlangeWeldThroat=flangeWeldThroat;ExternalCompression=externalCompression;
            WebCompression=webCompression;LoadX=loadX;LoadZ=loadZ;BearingWidth=bearingWidth;BearingLength=bearingLength;
            EndDistance=endDistance;RigidEndPostSpacing=rigidEndPostSpacing;Source=source;
        }
    }

    public sealed class SteelWebDetailCheck
    {
        public string Name { get; }
        public double Demand { get; }
        public double Resistance { get; }
        public string Unit { get; }
        public double Utilization=>Demand/Resistance;
        internal SteelWebDetailCheck(string name,double demand,double resistance,string unit)
        {
            SteelInputValidation.Finite(demand,name);SteelInputValidation.Positive(resistance,name);
            if(demand<0)throw new ArgumentOutOfRangeException(name);
            SteelInputValidation.Finite(demand/resistance,name);
            Name=name;Demand=demand;Resistance=resistance;Unit=unit;
        }
    }

    /// <summary>Complete local detailing for one standard homogeneous welded H web
    /// panel: two bilateral flat boundaries, their web/end welds and direct bearing.
    /// No longitudinal stiffeners, cut-outs, box diaphragms, bearing device or spreader
    /// plate bending. Those are different details, not implicitly qualified here.</summary>
    public sealed class SteelHWebPanelDetails
    {
        public SteelVerificationInput Input { get; }
        public SteelWebBoundaryDetail Left { get; }
        public SteelWebBoundaryDetail Right { get; }
        public double PanelLength { get; }
        public double GammaM1 { get; }
        public double Eta { get; }
        public bool UsesRigidEndPost { get; }
        public bool Stable { get; }
        public bool Passed=>Stable&&Checks.All(c=>c.Utilization<=1);
        public double? Utilization=>Stable?Checks.Max(c=>c.Utilization):(double?)null;
        public IReadOnlyList<SteelWebDetailCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<WebStiffenerResistance> Stiffeners { get; }
        private SteelHWebPanelDetails(SteelVerificationInput input,double a,double gm1,double eta,double gammaM2Weld,
            SteelWebBoundaryDetail left,SteelWebBoundaryDetail right)
        {
            Input=input;PanelLength=a;GammaM1=gm1;Eta=eta;Left=left;Right=right;
            // The existing assessment validates exact H, axes, material/code and panel length.
            SteelShearBucklingAssessment.EvaluateTransverselyStiffenedHWeb(input,a,false,gm1,eta,"typed detail validation");
            var section=input.Section.ReadOnlySection;var h=(SectionH)section.SectionShape;
            if(section.SectionType!=Section.SectionTypes.Welded||h.R!=0||input.Actions.T!=0||input.Actions.V1!=0||input.Actions.M2!=0)
                throw new NotSupportedException("Homogeneous flat welded H under N/M1/V2, no torsion or skew boundary, required.");
            double hw=h.HeightWeb,tw=h.ThicknessWeb,fy=input.Section.YieldStrength,e=input.Section.ElasticModulus;
            if(fy>460)throw new NotSupportedException("Carbon steel up to S460 required for this weld/detail policy.");
            SteelInputValidation.Positive(gammaM2Weld,nameof(gammaM2Weld));
            if(input.Code.GammaM1.HasValue&&input.Code.GammaM1.Value!=gm1)throw new ArgumentException("Stiffener gammaM1 must match the member design context.");
            if(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018&&gammaM2Weld!=1.25)
                throw new ArgumentException("NTC weld gammaM2=1.25 required.");
            double gm0=input.Code.SectionCode.GammaM0,gm2=gammaM2Weld;
            double span=hw+(h.ThicknessTopFlange+h.ThicknessBottomFlange)/2;
            double available=(Math.Min(h.LenghtTopFlange,h.LenghtBottomFlange)-tw)/2;
            var checks=new List<SteelWebDetailCheck>();var trace=new List<SectionResistanceValue>();var members=new List<WebStiffenerResistance>();
            void Check(string n,double d,double r,string u="1")=>checks.Add(new SteelWebDetailCheck(n,d,r,u));
            void Value(string n,double v,string u) {SteelInputValidation.Finite(v,n);trace.Add(new SectionResistanceValue(n,v,u));}
            bool stable=true;
            void Boundary(SteelWebBoundaryDetail b,string name)
            {
                if(b==null||!ReferenceEquals(b.Input,input))throw new ArgumentException("Both boundaries must belong to this exact state.");
                bool end=b.Kind==SteelWebBoundaryKind.EndSupport;
                double other=end?a:b.AdjacentPanelLength;
                double removed=end?a:a+other;
                double shear=Math.Max(Math.Abs(input.Actions.V2),b.AdjacentShear);
                double tau=Ec3WebPanelShear.Calculate(hw,tw,fy,e,gm0,gm1,eta,removed).TauCritical;
                double nst=Math.Max(0,shear-tau*hw*tw/gm1)+b.ExternalCompression;
                double near=end?b.EndDistance:other/2,far=a/2;
                if(b.RigidEndPostSpacing>0)far=Math.Min(far,b.RigidEndPostSpacing/2);
                WebStiffenerResistance Element(string label,double a1,double a2,double av1,double av2,double force,double ex,double ez)
                {
                    var r=Ec3WebStiffener.Stiffener(hw,tw,span,a1,a2,av1,av2,b.Width,b.Thickness,b.Width,b.Thickness,
                        fy,e,gm1,force,b.WebCompression,ex,ez,1); // no unverified 0.75 restraint benefit
                    members.Add(r);stable&=r.Stable;
                    Value(label+".Nst",force,"N");Value(label+".Area",r.Area,"mm2");Value(label+".Iout",r.InertiaOut,"mm4");
                    Value(label+".NcrOut",r.CriticalOut,"N");Value(label+".NcrIn",r.CriticalIn,"N");
                    Value(label+".MomentOut",r.MomentOut,"Nmm");Value(label+".MomentIn",r.MomentIn,"Nmm");
                    Check(label+".PanelRigidity",r.RigidityRatio,1);
                    Check(label+".LocalPlate",r.LocalRatio,1);Check(label+".TorsionalRigidity",r.TorsionRatio,1);
                    Check(label+".FlangeEnvelope",b.Width,available,"mm");
                    if(r.Stable){Check(label+".SecondOrderStress",r.Stress,fy/gm1,"MPa");Check(label+".Deflection",r.Deflection,hw/300,"mm");}
                    double aw=b.WebWeldThroat,af=b.FlangeWeldThroat,lw=hw-2*aw,lf=b.Width-2*af;
                    if(lw<=0||lf<=0)throw new ArgumentException("Effective weld lengths must be positive.");
                    double fw=Ec3WebStiffener.WeldStrength(input.Section.UltimateStrength,1,gm2);
                    double reduction=Math.Max(.6,Math.Min(1,1.2-.2*lw/(150*aw)));
                    Check(label+".WebWeld",force/(4*aw*lw)+6*(r.MomentOut+r.MomentIn)/(4*aw*lw*lw),fw*reduction,"MPa");
                    double endReduction=Math.Max(.6,Math.Min(1,1.2-.2*lf/(150*af)));
                    if(r.Stable)Check(label+".FlangeWeld",r.Stress*b.Thickness*b.Width/(2*af*lf),fw*endReduction,"MPa");
                    Check(label+".WebThroatMinimum",3,aw,"mm");Check(label+".FlangeThroatMinimum",3,af,"mm");
                    Check(label+".WebWeldLength",Math.Max(30,6*aw),lw,"mm");Check(label+".FlangeWeldLength",Math.Max(30,6*af),lf,"mm");
                    Check(label+".WebLegFitsPlate",Math.Sqrt(2)*aw,b.Thickness,"mm");
                    Check(label+".EndLegFitsPlate",Math.Sqrt(2)*af,b.Thickness,"mm");
                    Check(label+".WebWeldFitsFlange",b.Width+Math.Sqrt(2)*aw,available,"mm");
                    Check(label+".FlangeWeldFitsWebHeight",2*Math.Sqrt(2)*af,hw,"mm");
                    return r;
                }
                double x=nst>0?b.ExternalCompression*b.LoadX/nst:0,z=nst>0?b.ExternalCompression*b.LoadZ/nst:0;
                var st=Element(name,other,a,near,far,nst,x,z);
                Value(name+".RemovedPanelLength",removed,"mm");Value(name+".TauCrRemoved",tau,"MPa");
                if(b.ExternalCompression>0)
                {
                    Check(name+".BearingWidth",tw+2*b.Width+2*Math.Abs(b.LoadX),b.BearingWidth,"mm");
                    Check(name+".BearingLength",b.Thickness+2*Math.Abs(b.LoadZ),b.BearingLength,"mm");
                    Check(name+".FootprintWithinFlange",b.BearingWidth+2*Math.Abs(b.LoadX),Math.Min(h.LenghtTopFlange,h.LenghtBottomFlange),"mm");
                    if(end)Check(name+".BearingWithinEnd",b.BearingLength/2+Math.Abs(b.LoadZ),b.EndDistance,"mm");
                    if(st.Stable)Check(name+".DirectBearing",nst/st.PlateArea+Math.Max(0,st.Stress-nst/st.Area),fy/gm0,"MPa");
                }
                if(b.RigidEndPostSpacing>0)
                {
                    double spacing=b.RigidEndPostSpacing;
                    if(spacing<=.1*hw||spacing>=a)throw new ArgumentException("Rigid end post requires .1 hw < e < a.");
                    double requiredArea=4*hw*tw*tw/spacing;
                    // The second identical bilateral pair carries the full conservative reaction.
                    double tau2=Ec3WebPanelShear.Calculate(hw,tw,fy,e,gm0,gm1,eta,a+spacing).TauCritical;
                    double n2=Math.Max(0,shear-tau2*hw*tw/gm1)+b.ExternalCompression;
                    var second=Element(name+".SecondPair",spacing,a,spacing/2,a/2,n2,n2>0?b.ExternalCompression*b.LoadX/n2:0,0);
                    Check(name+".EndPostArea",requiredArea,st.PlateArea,"mm2");
                    if(st.Stable&&second.Stable)Check(name+".EndPostCombined",requiredArea/st.PlateArea+Math.Max(st.StressRatio.Value,second.StressRatio.Value),1);
                }
            }
            Boundary(left,"Left");Boundary(right,"Right");
            Stable=stable;Checks=Array.AsReadOnly(checks.ToArray());Trace=Array.AsReadOnly(trace.ToArray());Stiffeners=Array.AsReadOnly(members.ToArray());
            // Internal panels need no terminal-post condition; any actual end must have a checked rigid post.
            var ends=new[]{left,right}.Where(b=>b.Kind==SteelWebBoundaryKind.EndSupport).ToArray();
            UsesRigidEndPost=Passed&&ends.Length>0&&ends.All(b=>b.RigidEndPostSpacing>0);
        }
        public static SteelHWebPanelDetails Evaluate(SteelVerificationInput input,double panelLength,double gammaM1,double eta,double gammaM2Weld,
            SteelWebBoundaryDetail left,SteelWebBoundaryDetail right)=>new SteelHWebPanelDetails(input,panelLength,gammaM1,eta,gammaM2Weld,left,right);
    }

    public sealed partial class SteelShearBucklingAssessment
    {
        public SteelHWebPanelDetails PanelDetails { get; private set; }
        public static SteelShearBucklingAssessment FromVerifiedDetails(SteelHWebPanelDetails details)
        {
            if(details==null)throw new ArgumentNullException(nameof(details));
            var r=EvaluateTransverselyStiffenedHWeb(details.Input,details.PanelLength,details.UsesRigidEndPost,
                details.GammaM1,details.Eta,"Computed bilateral stiffeners, supports and continuous web/flange welds; "+details.Left.Source+"; "+details.Right.Source);
            r.PanelDetails=details;return r;
        }
    }
}
