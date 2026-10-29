using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Bolted terminal of an identical packed 2C/2L in constant compression: ONE
    /// longitudinal line of BoltCount through bolts (normal round holes, category A) through
    /// both connected backs, an optional identical packing on each face and a central
    /// gusset. The stack gusset+2 packings fills the clear gap. Station 0/L of the layout is
    /// the bolt-group centre. TransverseCoordinate is measured on the single Model chord as in
    /// SteelBoltedPackingDetail. ChordEndDistance runs from the node-side bolt to the chord cut
    /// end, GussetTipDistance from the interior bolt to the free gusset tip. Packings are
    /// centred on the bolt line and flush with the chord end. The gusset is centred on the
    /// chord-centroid setting-out line, its root FreeLength before the chord end is restrained
    /// against out-of-plane rotation and continues by a full-penetration butt weld into a
    /// COPLANAR collector, as in the welded standard terminal. mm, N, MPa.</summary>
    public sealed class SteelPackedPairBoltedTerminalDetail
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionBolt Bolt { get; }
        public int BoltCount { get; }
        public double Pitch { get; }
        public double TransverseCoordinate { get; }
        public double ChordEndDistance { get; }
        public double GussetTipDistance { get; }
        public ConnectionPlate Gusset { get; }
        /// <summary>One packing per face, or null when the gusset alone fills the gap.</summary>
        public ConnectionPlate Packing { get; }
        public double FreeLength { get; }
        /// <summary>Actual head/nut/washer envelope, for fit and edge clearance.</summary>
        public double HardwareDiameter { get; }
        /// <summary>Mean of across-points and across-flats of head or nut, whichever smaller (EN1993-1-8 Table3.4 dm).</summary>
        public double MeanHeadNutDiameter { get; }
        public ConnectionPlate Collector { get; }
        public double WeldFusionDepth { get; } public double WeldSeamWidth { get; }
        public double WeldFy { get; } public double WeldFu { get; }
        public bool FullPenetration { get; } public bool RootRotationRestrained { get; }
        public string SupportAndWeldSource { get; }
        public string ExecutionSource { get; }
        public string HoleClearanceSource { get; }
        public SteelPackedPairBoltedTerminalDetail(ConnectionDesignCode code,ConnectionBolt bolt,int boltCount,double pitch,
            double transverseCoordinate,double chordEndDistance,double gussetTipDistance,ConnectionPlate gusset,ConnectionPlate packing,
            double freeLength,double hardwareDiameter,double meanHeadNutDiameter,ConnectionPlate collector,double weldFusionDepth,
            double weldSeamWidth,double weldFy,double weldFu,bool fullPenetration,bool rootRotationRestrained,string supportAndWeldSource,
            string executionSource,string holeClearanceSource)
        {
            Code=code??throw new ArgumentNullException(nameof(code));Bolt=bolt??throw new ArgumentNullException(nameof(bolt));
            Gusset=gusset??throw new ArgumentNullException(nameof(gusset));Collector=collector??throw new ArgumentNullException(nameof(collector));
            if(boltCount<2||boltCount>20)throw new ArgumentOutOfRangeException(nameof(boltCount));
            foreach(double v in new[]{pitch,chordEndDistance,gussetTipDistance,freeLength,hardwareDiameter,meanHeadNutDiameter,
                weldFusionDepth,weldSeamWidth,weldFy,weldFu})ConnectionValidation.Positive(v,"bolted terminal detail");
            ConnectionValidation.Nonnegative(transverseCoordinate,nameof(transverseCoordinate));
            if(weldFu<weldFy)throw new ArgumentException("Weld fu below fy");
            if(hardwareDiameter<=bolt.HoleDiameter||meanHeadNutDiameter<=bolt.HoleDiameter)
                throw new ArgumentException("Actual head/nut/washer dimensions covering the hole required.");
            BoltCount=boltCount;Pitch=pitch;TransverseCoordinate=transverseCoordinate;ChordEndDistance=chordEndDistance;
            GussetTipDistance=gussetTipDistance;Packing=packing;FreeLength=freeLength;HardwareDiameter=hardwareDiameter;
            MeanHeadNutDiameter=meanHeadNutDiameter;WeldFusionDepth=weldFusionDepth;WeldSeamWidth=weldSeamWidth;WeldFy=weldFy;WeldFu=weldFu;
            FullPenetration=fullPenetration;RootRotationRestrained=rootRotationRestrained;SupportAndWeldSource=supportAndWeldSource;
            ExecutionSource=executionSource;HoleClearanceSource=holeClearanceSource;
        }
    }

    public sealed class SteelPackedPairBoltedTerminalResult
    {
        public PackedPairImperfectionModel Demand { get; }
        public SteelPackedPairBoltedTerminalDetail Detail { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete { get; }
        public ConnectionStatus Status { get; }
        public bool CanQualifyWholeTerminal=>IsComplete;
        public bool CanQualifyWholeMember=>false;
        public double ForcePerChord { get; }
        /// <summary>Conditional local force resistance for this fixed end-force pattern and
        /// concomitant member stress; not a global critical load of the member.</summary>
        public double? ForceResistance { get; }
        public double? Utilization=>IsComplete?Checks.Max(x=>x.Utilization):(double?)null;
        /// <summary>DECLARED end condition this terminal provides to the member pipeline: both
        /// transverse translations held at the node by the restrained gusset root/collector,
        /// NO flexural rotational restraint credited (a single bolt line is a hinge for the
        /// member). Twist, warping and axial restraint are not declared here. The member model
        /// must not assume more; SteelPackedPairAssessment.FromBoltedTerminal enforces it.</summary>
        public SteelEndRestraints AssumedMemberEnd { get; }
        public string MemberRestraintAssumption=>"Translations 1/2 held at the gusset root, flexural rotations 1/2 free (pin-ended full-length member scheme); twist and warping not provided by this terminal.";
        internal SteelPackedPairBoltedTerminalResult(PackedPairImperfectionModel demand,SteelPackedPairBoltedTerminalDetail detail,
            List<ConnectionCheck> checks,List<SectionResistanceValue> trace,List<string> issues,double force,double resistance)
        {
            Demand=demand;Detail=detail;Diagnostics=Array.AsReadOnly(issues.Distinct().ToArray());
            var retained=ConnectionOutcome.Retained(checks,Diagnostics.Count);
            Checks=Array.AsReadOnly(retained);Trace=Array.AsReadOnly(trace.ToArray());
            IsComplete=ConnectionOutcome.IsComplete(retained,Diagnostics.Count);Status=ConnectionOutcome.Status(retained,Diagnostics.Count);
            ForcePerChord=force;ForceResistance=IsComplete&&resistance>0&&resistance<double.MaxValue?resistance:(double?)null;
            AssumedMemberEnd=new SteelEndRestraints(SteelRestraintState.Fixed,SteelRestraintState.Fixed,SteelRestraintState.Unknown,
                SteelRestraintState.Free,SteelRestraintState.Free,SteelRestraintState.Unknown,SteelRestraintState.Unknown);
        }
    }

    /// <summary>Explicit transport of each chord force F=|N|/2+Q_end from the chord centroid
    /// to the bolt line: in-plane couple F*e_t to the elastic bolt line (shear components),
    /// out-of-plane couple F*e_n to bolt tension with contact at the node-side bolt row,
    /// rigid-flange prying bound on walls with a free edge, bolt shear/tension interaction with
    /// betaLf and betaP, punching, bearing of every ply, wall bending, chord gross compression
    /// and the gusset as an EN1993-1-1 5.3.2 bow-imperfect elastic beam-column with a stated
    /// cantilever length. No contact/FEM solution; all bounds are conservative statics.</summary>
    public static class SteelPackedPairBoltedTerminal
    {
        public const string MethodId="Steel.Connection.PackedPair.BoltedTerminal";
        private const string EllipseReference="SBE_SS5:2010 AppendixA p5-68, ECCS P126 additional bearing interaction; complementary to EN1993-1-8 Table3.4 / NTC2018 4.2.67";

        public static SteelPackedPairBoltedTerminalResult Verify(PackedPairImperfectionModel demand,SteelPackedPairBoltedTerminalDetail d)
        {
            if(demand==null)throw new ArgumentNullException(nameof(demand));if(d==null)throw new ArgumentNullException(nameof(d));
            var layout=demand.Layout;var input=layout.Input;var property=input.Section.ReadOnlySection;
            var shape=(SectionBuiltUp)property.SectionShape;var chord=shape.Parts[0].Section;var g=ModelBuiltUpGeometry.Create(shape);
            var code=d.Code;var bolt=d.Bolt;double d0=bolt.HoleDiameter;int n=d.BoltCount;double p=d.Pitch;
            var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();var issues=new List<string>();
            double scale=double.MaxValue;
            void Require(bool condition,string message){if(!condition)issues.Add(message);}
            void Value(string name,double value,string unit){if(double.IsNaN(value)||double.IsInfinity(value))issues.Add("NumericRange:"+name);else trace.Add(new SectionResistanceValue(name,value,unit));}
            // Every check scales linearly with the end-force pattern except the bearing
            // ellipse (quadratic) and the gusset beam-column, solved separately below.
            void Check(string id,double demandValue,double capacity,string reference,string unit="N",bool quadratic=false)
            {
                if(!(capacity>0)||double.IsInfinity(capacity)||double.IsNaN(demandValue)||double.IsInfinity(demandValue)||double.IsInfinity(demandValue/capacity)){issues.Add("NumericRange:"+id);return;}
                var check=new ConnectionCheck(id,demandValue,capacity,reference,unit);checks.Add(check);
                if(check.Utilization>0)scale=Math.Min(scale,quadratic?1/Math.Sqrt(check.Utilization):1/check.Utilization);
            }
            void Append<T>(string prefix,ConnectionVerification<T> child) where T:class
            {
                foreach(var issue in child.Diagnostics)issues.Add(prefix+issue);
                foreach(var c in child.Checks)Check(prefix+c.Id,c.Demand,c.Resistance,c.Reference,c.Unit);
            }
            bool vertical=shape.Parts[0].MirrorX!=shape.Parts[1].MirrorX;
            var cs=chord as SectionC;var l=chord as SectionL;
            double t=!(cs is null)?cs.ThicknessWeb:vertical?l.VerticalLegThickness:l.HorizontalLegThickness;
            double gap=Math.Abs(vertical?shape.Parts[0].X-shape.Parts[1].X:shape.Parts[0].Y-shape.Parts[1].Y);
            double minimumThickness=!(cs is null)?Math.Min(t,Math.Min(cs.ThicknessTop,cs.ThicknessBottom)):Math.Min(l.HorizontalLegThickness,l.VerticalLegThickness);
            double low=!(cs is null)?cs.ThicknessBottom+cs.R1:vertical?l.HorizontalLegThickness+l.R:l.VerticalLegThickness+l.R;
            double high=!(cs is null)?cs.Height-cs.ThicknessTop-cs.R1:vertical?l.VerticalLegLength-l.R2:l.HorizontalLegLength-l.R2;
            double centre=d.TransverseCoordinate,half=d.HardwareDiameter/2,minimumEdge=Math.Max(1.2*d0,half);
            double transverseCentroid=vertical?chord.Centroid.Y:chord.Centroid.X;
            // The back faces the gap at local 0, so the centroid coordinate normal to it is the
            // lever between the bolt shear plane and the chord force line.
            double normalLever=vertical?chord.Centroid.X:chord.Centroid.Y;
            double inPlaneLever=centre-transverseCentroid;
            double packing=d.Packing==null?0:d.Packing.Thickness,stack=d.Gusset.Thickness+2*packing;
            double jointLength=(n-1)*p,gussetHalf=d.Gusset.Width/2;
            double gussetLowEdge=gussetHalf+inPlaneLever,gussetHighEdge=gussetHalf-inPlaneLever;
            Require(code.Plates.Standard==input.Code.SectionCode.Standard&&code.Plates.GammaM0==input.Code.SectionCode.GammaM0
                &&code.Plates.GammaM2==input.Code.SectionCode.GammaM2&&code.Plates.ParameterSet==input.Code.SectionCode.ParameterSet,"DesignContextMismatch");
            Require(input.Section.Unperforated==false,"DeclareOrdinaryFilledBoltHoles");
            Require(input.Section.NetArea.HasValue&&Math.Abs(input.Section.NetArea.Value-2*(chord.Area-d0*t))<=1e-8*chord.Area,
                "NetAreaMustMatchOnlyTheDeclaredSingleLineOfFilledHoles");
            Require(property.FormedType==Section.FormedTypes.HotFinished,"HotFinishedChordsRequired");
            Require(Math.Max(Math.Max(input.Section.YieldStrength,d.Gusset.Fy),Math.Max(d.Collector.Fy,d.Packing==null?0:d.Packing.Fy))<=460,"CarbonSteelUpToS460");
            Require(cs is null||vertical&&cs.LengthTop==cs.LengthBottom&&cs.ThicknessTop==cs.ThicknessBottom,"EqualFlangeBackToBackChannelsRequired");
            Require(g.CentroidSpacing>gap,"BacksMustFaceGussetStack");
            Require(Math.Abs(gap-stack)<=1e-8*Math.Max(1,gap)&&gap<=3*minimumThickness,"GussetAndPackingsMustFillCloseGap");
            Require(!string.IsNullOrWhiteSpace(d.ExecutionSource),"ExecutionSourceRequired");
            Require(centre-half>=low&&centre+half<=high&&centre-low>=minimumEdge&&high-centre>=minimumEdge,"BoltLineAndHardwareMustFitStraightBack");
            Require(gussetLowEdge>=minimumEdge&&gussetHighEdge>=minimumEdge,"GussetMustBeCentredOnSettingOutLineAndContainBoltLine");
            if(d.Packing!=null)
                Require(d.Packing.Width/2>=minimumEdge&&centre-d.Packing.Width/2>=low&&centre+d.Packing.Width/2<=high,"PackingMustFitStraightBackWithEdgeDistances");
            Require(d.ChordEndDistance>=minimumEdge&&d.GussetTipDistance>=minimumEdge,"MinimumEndOrHardwareDistance");
            // Bidirectional bolt forces: the transverse minimum governs the line pitch too.
            Require(p>=Math.Max(2.4*d0,d.HardwareDiameter),"InsufficientBidirectionalPitchOrHardwareClearance");
            double thinnest=Math.Min(t,Math.Min(d.Gusset.Thickness,packing>0?packing:double.MaxValue));
            Require(p<=Math.Min(14*thinnest,200),"Table3_3MaximumPitchInCompression");
            Require(layout.PanelLengths.First()>=jointLength/2+d.GussetTipDistance&&layout.PanelLengths.Last()>=jointLength/2+d.GussetTipDistance,
                "TerminalGussetMustEndBeforeNextStation");
            Require(d.Collector.Width>=d.Gusset.Width&&d.Collector.Thickness>=d.Gusset.Thickness,"CoplanarCollectorMustContainFullGussetSection");
            Require(d.FullPenetration&&d.WeldFusionDepth>=d.Gusset.Thickness&&d.WeldSeamWidth>=d.Gusset.Width,"FullPenetrationAcrossEntireGussetRequired");
            Require(d.WeldFy>=Math.Max(d.Gusset.Fy,d.Collector.Fy)&&d.WeldFu>=Math.Max(d.Gusset.Fu,d.Collector.Fu),"CompatibleFullStrengthButtWeldRequired");
            Require(d.RootRotationRestrained&&!string.IsNullOrWhiteSpace(d.SupportAndWeldSource),"ActualRootRestraintMidSurfaceAlignmentAndInspectionRequired");
            // Prying: a free toe beyond the bolt line (angle) allows a contact force at the toe;
            // the rigid-flange statics B=T(m+n)/n, n<=1.25m (EN1993-1-8 Table6.2), bound it.
            // A C web is held by both flanges and has no free edge on either side: no prying,
            // and its lift is carried by a plastic strip with hinges at both roots and the bolt.
            // wallLever is the moment per unit bolt lift in the governing plastic mechanism.
            double wallLever,prying;string wallReference;
            if(!(cs is null))
            {
                double a=centre-cs.ThicknessBottom-.8*cs.R1,b=cs.Height-cs.ThicknessTop-.8*cs.R1-centre;
                wallLever=a*b/(2*(a+b));prying=1;
                wallReference="Plastic web strip between both flange roots (hinges at the roots and the bolt line, T<=2Mpl(a+b)/(ab)); EN1993-1-8 Table6.2 Mpl";
                Require(a>0&&b>0,"BoltLineInsideFlangeRootZone");
            }
            else
            {
                double outstanding=vertical?l.HorizontalLegThickness:l.VerticalLegThickness,toe=(vertical?l.VerticalLegLength:l.HorizontalLegLength)-centre;
                double m=centre-outstanding-.8*l.R,edge=Math.Min(toe,1.25*m);wallLever=m/2;prying=(m+edge)/edge;
                wallReference="EN1993-1-8 Table6.2 mode1 of one T-stub side, T<=2Mpl/m, leg hinged at the heel root and the bolt line";
                Require(m>0&&edge>0,"BoltLineInsideHeelRootZone");
            }
            double transfer=Math.Max(demand.Stations.First().LongitudinalShear,demand.Stations.Last().LongitudinalShear);
            double force=-input.Actions.N/2+transfer;
            Value("BoltedTerminal.EndTransferEnvelope",transfer,"N");Value("BoltedTerminal.ForcePerChord",force,"N");
            Value("BoltedTerminal.InPlaneLever",inPlaneLever,"mm");Value("BoltedTerminal.NormalLever",normalLever,"mm");
            Value("BoltedTerminal.ConnectedWallThickness",t,"mm");Value("BoltedTerminal.ClearGap",gap,"mm");
            Value("BoltedTerminal.PackingThickness",packing,"mm");Value("BoltedTerminal.JointLength",jointLength,"mm");
            Value("BoltedTerminal.WallLever",wallLever,"mm");Value("BoltedTerminal.PryingBoundFactor",prying,"1");
            if(issues.Count!=0)return new SteelPackedPairBoltedTerminalResult(demand,d,checks,trace,issues,force,0);

            // In-plane: the transported chord force F at the bolt-group centre plus Mz=F*e_t,
            // distributed elastically on the single line (rigid plies, equal bolts).
            var points=Enumerable.Range(0,n).Select(i=>new ConnectionPoint2D((i-(n-1)/2.0)*p,0));
            var group=ElasticBoltGroup.Calculate(points,new PlanarConnectionActions(force,0,0,0,0,force*inPlaneLever));
            Require(group.IsSupported,"InPlaneElasticGroupNotSupported");
            // Out-of-plane: the couple F*e_n of EACH chord is closed by bolt tension and contact
            // at the node-side row (row 0): T_i=M*x_i/sum(x_j^2), x_i=i*p. The through bolt is
            // shared by both mirrored chords, so their equal couples do not add in one bolt.
            double couple=force*normalLever,sumSquares=0;for(int i=0;i<n;i++)sumSquares+=(i*p)*(i*p);
            Value("BoltedTerminal.OutOfPlaneCouple",couple,"Nmm");Value("BoltedTerminal.LeverSquares",sumSquares,"mm2");
            if(issues.Count!=0)return new SteelPackedPairBoltedTerminalResult(demand,d,checks,trace,issues,force,0);
            var wall=new ConnectionPlate(high-low,t,input.Section.YieldStrength,input.Section.UltimateStrength);
            string grossReference=code.IsNtc?"NTC2018 4.2.4.1.2.2; EN1993-1-1 6.2.4(3) filled normal holes":"EN1993-1-1:2005 6.2.4, 6.2.4(3) filled normal holes";
            Check("Chord.TerminalGrossCompression",force,chord.Area*input.Section.YieldStrength/code.Plates.GammaM0,grossReference);
            foreach(var f in group.Forces)
            {
                int i=f.Index;string prefix="Bolt"+(i+1)+".";
                double tension=couple*(i*p)/sumSquares,boltTension=prying*tension;
                Value(prefix+"ShearPerPlane",f.Shear,"N");Value(prefix+"CoupleTension",tension,"N");Value(prefix+"TensionWithPrying",boltTension,"N");
                var strength=BoltStrength.Verify(new BoltStrengthInput(code,bolt,f.Shear,boltTension,1,jointLength,packing,d.HoleClearanceSource),out var strengthValues);
                Append(prefix,strength);
                if(strength.IsComplete){Value(prefix+"BetaLongJoint",strengthValues.BetaLongJoint,"1");Value(prefix+"BetaPacking",strengthValues.BetaPacking,"1");}
                var punching=BoltPunching.Verify(new BoltPunchingInput(code,wall,boltTension,d0,d.MeanHeadNutDiameter));
                Append(prefix+"ChordWall.",punching);
                // Lift transfer from the bolt to the rest of the chord: plastic wall strip of the
                // bolt's own non-overlapping tributary length, Mpl=0.25 leff t^2 fy/gammaM0.
                double tributary=i==0?p/2+Math.Min(p/2,d.ChordEndDistance):p;
                Check(prefix+"ChordWall.Bending",tension*wallLever,.25*tributary*t*t*input.Section.YieldStrength/code.Plates.GammaM0,wallReference,"Nmm");
                double chordBearing=Ply("Chord",wall,f.Fx,f.Fy,d.ChordEndDistance+i*p,i==0,Math.Min(centre-low,high-centre),i,prefix+"ChordWall.");
                double gussetBearing=Ply("Gusset",d.Gusset,2*f.Fx,2*f.Fy,d.GussetTipDistance+(n-1-i)*p,i==n-1,Math.Min(gussetLowEdge,gussetHighEdge),i,prefix+"Gusset.");
                double packingBearing=d.Packing==null?double.MaxValue
                    :Ply("Packing",d.Packing,f.Fx,f.Fy,d.ChordEndDistance+i*p,i==0,d.Packing.Width/2,i,prefix+"Packing.");
                if(code.IsNtc&&strength.IsComplete&&punching.IsComplete&&chordBearing>0&&gussetBearing>0&&packingBearing>0)
                {
                    // NTC2018 (4.2.71) uses the assembled minima of bolt/ply shear-bearing and of
                    // bolt tension/punching; one shear plane and its plies, gusset per plane.
                    double shear=Math.Min(strengthValues.ShearResistance,Math.Min(chordBearing,Math.Min(gussetBearing/2,packingBearing)));
                    double tensile=Math.Min(strengthValues.TensionResistance,punching.Checks.Single().Resistance);
                    Check(prefix+"NtcShearTensionInteraction",f.Shear/shear+boltTension/(1.4*tensile),1,
                        "NTC2018 4.2.8.1.1 (4.2.71), minima of bolt shear and ply bearing, of bolt tension and punching","1");
                    Check(prefix+"NtcTensionLimit",boltTension,tensile,"NTC2018 4.2.8.1.1 tension limitation following (4.2.71)");
                }
            }
            // Gusset: the 2F resultant lies on the gusset centre (setting-out line) after the
            // bolt-line couple; its effective band is the 30 degree spread of the bolt line
            // at the node-side row, truncated by the gusset edges and never widened towards
            // the root. Cantilever from the restrained root with free sway at the chord end.
            double spread=jointLength*Math.Tan(Math.PI/6);
            double bandLow=Math.Max(-gussetLowEdge,-spread),bandHigh=Math.Min(gussetHighEdge,spread);
            double band=bandHigh-bandLow,bandOffset=Math.Abs(-inPlaneLever-(bandLow+bandHigh)/2);
            double tg=d.Gusset.Thickness,area=band*tg,length=2*(d.FreeLength+d.ChordEndDistance);
            double inertia=Math.Min(band*tg*tg*tg,tg*band*band*band)/12,modulus=Math.Min(band*tg*tg,tg*band*band)/6,inPlaneModulus=tg*band*band/6;
            double ncr=Math.PI*Math.PI*input.Section.ElasticModulus*inertia/(length*length),total=2*force;
            // Differential chord transfer: chord forces N/2+Q and N/2-Q on the two centroid lines
            // leave Q times the centroid spacing on the gusset mid-surface (their own couples
            // F*e_n are closed by the bolt tension above, enveloped with N/2+Q on both chords).
            double moment=transfer*g.CentroidSpacing;
            double fy=Math.Min(d.Gusset.Fy,d.Collector.Fy),fu=Math.Min(d.Gusset.Fu,d.Collector.Fu);
            double limit=Math.Min(fy/code.Plates.GammaM0,.9*fu/code.Plates.GammaM2);
            Value("Gusset.EffectiveBandWidth",band,"mm");Value("Gusset.BandOffsetFromResultant",bandOffset,"mm");
            Value("Gusset.EffectiveLength",length,"mm");Value("Gusset.Ncr",ncr,"N");Value("Gusset.OutOfPlaneMomentEnvelope",moment,"Nmm");
            Value("Gusset.Area",area,"mm2");Value("Gusset.Modulus",modulus,"mm3");Value("Gusset.InPlaneModulus",inPlaneModulus,"mm3");
            Require(band>0&&area>0,"GussetEffectiveBandOutsideRange");
            if(issues.Count==0)
            {
                double Stress(double s)=>total*s/area+total*s*bandOffset/inPlaneModulus+(moment+total*length/150)*s/(modulus*(1-total*s/ncr));
                string reference="EN1993-1-1:2005 5.3.2 bow Lk/150 (curve d, conservative for solid plates), 6.2.1(7) elastic check; "
                    +"stated cantilever Lk=2(Lf+e1), Whitmore 30-degree band as an upper bound of the effective width; CJP continuity EN1993-1-8 4.7.1";
                if(total>=ncr)Check("Gusset.Euler",total,ncr,"Explicit fixed-root/free-tip isolated band Euler limit");
                else Check("Gusset.AndCollectorElastic",Stress(1),limit,reference,"MPa");
                double lo=0,hi=ncr/total*(1-1e-12);
                for(int i=0;i<80;i++){double mid=(lo+hi)/2;if(Stress(mid)<=limit)lo=mid;else hi=mid;}
                scale=Math.Min(scale,lo);
            }
            double resistance=scale<double.MaxValue?force*scale:0;
            Value("BoltedTerminal.ConditionalForceResistance",resistance,"N");
            return new SteelPackedPairBoltedTerminalResult(demand,d,checks,trace,issues,force,resistance);

            // Bearing of one ply in both bolt-force components and their ellipse. Both senses of
            // the longitudinal force use the actual end distance: no credit is taken for the
            // compression sense pushing the hole towards continuing material.
            double Ply(string name,ConnectionPlate plate,double fx,double fyForce,double end,bool endBolt,double edge,int index,string prefix)
            {
                bool edgeBoltY=index==0||index==n-1;
                var x=BoltBearing.Verify(new BoltBearingInput(code,bolt,plate,Math.Abs(fx),end,edge,p,null,endBolt,true,d.HoleClearanceSource));
                var y=BoltBearing.Verify(new BoltBearingInput(code,bolt,plate,Math.Abs(fyForce),edge,end,null,p,true,edgeBoltY,d.HoleClearanceSource));
                Append(prefix+"X.",x);Append(prefix+"Y.",y);
                if(!x.IsComplete||!y.IsComplete)return 0;
                double rx=x.Checks[0].Resistance,ry=y.Checks[0].Resistance,ux=Math.Abs(fx)/rx,uy=Math.Abs(fyForce)/ry;
                Check(prefix+"BearingEllipse",ux*ux+uy*uy,1,EllipseReference,"1",true);
                return Math.Min(rx,ry);
            }
        }
    }
}
