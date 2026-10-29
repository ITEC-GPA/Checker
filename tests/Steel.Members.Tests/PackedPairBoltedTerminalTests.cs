using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Bolted terminal of packed 2C/2L in compression. Independent oracles are written
/// from rectangle partitions of the sharp-corner Model sections and EN1993-1-8 Table3.4.</summary>
internal static class PackedBoltedTerminalData
{
    internal static ConnectionBolt M16=>new(16,17,157,ConnectionBoltGrade.Class8_8,true);
    internal static ConnectionDesignCode Code(bool ntc=false)=>ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"test");
    internal static PackedPairImperfectionModel Model(string kind="C",bool ntc=false,double n=100000,double gap=10,double panel=125,bool holes=true)
    {
        var pair=PackedPairCompressionTests.Pair(kind,gap);double t=kind=="C"?10:12;
        var i=new SteelVerificationInput(new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S235",210000,235,360)),!holes,holes?pair.Area-2*17*t:null),
            new SteelActions(-n,0,0,0,0,0),ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),new SteelMemberInput(3000,3000,3000));
        int count=(int)Math.Round(3000/panel);
        return PackedPairImperfectionModel.Evaluate(new SteelPackingLayout(i,Enumerable.Range(0,count+1).Select(x=>panel*x),"Actual equal terminal and intermediate station layout"));
    }
    internal static SteelPackedPairBoltedTerminalDetail Terminal(PackedPairImperfectionModel m,bool ntc=false,int bolts=3,double pitch=50,double? line=null,
        double width=-1,double gusset=10,double packing=0,double free=20,double chordEnd=30,double tip=30,bool cjp=true,bool fixedRoot=true,
        double fusion=-1,double collectorWidth=-1,string? execution="Filled normal holes, through bolts, no other holes or cuts")
    {
        var pair=(SectionBuiltUp)m.Layout.Input.Section.CreateSectionCopy().SectionShape;bool channel=pair.Parts[0].Section is SectionC;
        double w=width>0?width:channel?100:120;
        return new(Code(ntc),M16,bolts,pitch,line??(channel?100:80),chordEnd,tip,new ConnectionPlate(w,gusset,235,360),
            packing>0?new ConnectionPlate(60,packing,235,360):null,free,32,25.4,new ConnectionPlate(collectorWidth>0?collectorWidth:w,gusset,235,360),
            fusion>0?fusion:gusset,w,235,360,cjp,fixedRoot,"Coplanar aligned mid-surfaces, root rotation restraint, full penetration and inspection",execution!,"Normal hole clearance");
    }
    internal static SteelBoltedPackingResult Packing(PackedPairImperfectionModel m,string kind="C",bool ntc=false,double thickness=10)
        =>SteelBoltedPacking.Verify(m,new(Code(ntc),M16,60,60,thickness,kind=="C"?100:kind=="L-long"?80:55,235,360,32,true,
            "Actual filled holes and hardened washers","Normal hole clearance"));
    internal static double Value(SteelPackedPairBoltedTerminalResult r,string id)=>r.Trace.Single(x=>x.Name==id).Value;
    internal static ConnectionCheck Check(SteelPackedPairBoltedTerminalResult r,string id)=>r.Checks.Single(x=>x.Id==id);
}

[TestClass]
public class PackedPairBoltedTerminalTests
{
    // Sharp C200x75x10/12: web 200x10 at x 0..10, flanges 65x12 at x 10..75.
    private const double ChannelArea=3560, ChannelNormalLever=76300.0/3560;
    // Sharp L 100x12 (horizontal) + 150x12 (vertical): vertical leg 12x150, horizontal 88x12.
    private const double AngleNormalLever=69936.0/2856, AngleTransverseCentroid=141336.0/2856;

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ChannelTerminalMatchesIndependentStatics(bool ntc)
    {
        var m=PackedBoltedTerminalData.Model("C",ntc);var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m,ntc));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Checks.Where(c=>!c.Passed).Select(c=>c.Id+":"+c.Utilization)));
        double q=Math.Max(m.Stations[0].LongitudinalShear,m.Stations.Last().LongitudinalShear),f=50000+q;
        Assert.AreEqual(f,r.ForcePerChord,1e-9);
        Assert.AreEqual(ChannelNormalLever,PackedBoltedTerminalData.Value(r,"BoltedTerminal.NormalLever"),1e-12);
        Assert.AreEqual(0,PackedBoltedTerminalData.Value(r,"BoltedTerminal.InPlaneLever"),1e-12);
        Assert.AreEqual(1,PackedBoltedTerminalData.Value(r,"BoltedTerminal.PryingBoundFactor"));
        // Rows at 0, 50, 100 mm from the node-side row: sum of squares 12500 mm2.
        double couple=f*ChannelNormalLever;
        Assert.AreEqual(0,PackedBoltedTerminalData.Value(r,"Bolt1.CoupleTension"),1e-12);
        Assert.AreEqual(couple*50/12500,PackedBoltedTerminalData.Value(r,"Bolt2.CoupleTension"),1e-9);
        double tension=couple*100/12500;
        Assert.AreEqual(tension,PackedBoltedTerminalData.Value(r,"Bolt3.TensionWithPrying"),1e-9);
        foreach(int i in new[]{1,2,3})Assert.AreEqual(f/3,PackedBoltedTerminalData.Value(r,"Bolt"+i+".ShearPerPlane"),1e-9);
        // EN1993-1-8 Table3.4, M16 8.8 threaded: Fv=0.6*800*157/1.25, Ft=0.9*800*157/1.25.
        var shear=PackedBoltedTerminalData.Check(r,"Bolt3.Bolt.Shear");Assert.AreEqual(60288,shear.Resistance,1e-9);
        Assert.AreEqual(90432,PackedBoltedTerminalData.Check(r,"Bolt3.Bolt.Tension").Resistance,1e-9);
        Assert.AreEqual(f/3/60288+tension/(1.4*90432),PackedBoltedTerminalData.Check(r,"Bolt3.Bolt.ShearTensionInteraction").Demand,1e-12);
        // Punching under head/nut on the 10 mm web: 0.6*pi*25.4*10*360/1.25.
        Assert.AreEqual(.6*Math.PI*25.4*10*360/1.25,PackedBoltedTerminalData.Check(r,"Bolt3.ChordWall.Plate.BoltHeadPunching").Resistance,1e-9);
        // Web strip between flange faces 12..188 (no radius), bolt at 100: a=b=88,
        // lever ab/(2(a+b))=22; tributary 50 mm, Mpl=0.25*50*10^2*235.
        var bending=PackedBoltedTerminalData.Check(r,"Bolt3.ChordWall.Bending");
        double gm0=ntc?1.05:1;
        Assert.AreEqual(22*tension,bending.Demand,1e-7);Assert.AreEqual(293750/gm0,bending.Resistance,1e-9);
        Assert.AreEqual(293750/gm0,PackedBoltedTerminalData.Check(r,"Bolt1.ChordWall.Bending").Resistance,1e-9);
        Assert.AreEqual(ChannelArea*235/gm0,PackedBoltedTerminalData.Check(r,"Chord.TerminalGrossCompression").Resistance,1e-7);
        // Gusset 100x10: 30-degree band 100*tan30=57.7 per side exceeds the half width 50.
        // Lk=2(20+30)=100, Ncr=pi^2*210000*(100*10^3/12)/100^2; moment Q*(10+2*e_n).
        Assert.AreEqual(100,PackedBoltedTerminalData.Value(r,"Gusset.EffectiveBandWidth"),1e-12);
        double ncr=Math.PI*Math.PI*210000*(100*1000/12.0)/(100*100);
        Assert.AreEqual(ncr,PackedBoltedTerminalData.Value(r,"Gusset.Ncr"),1e-6);
        double moment=q*(10+2*ChannelNormalLever),stress=2*f/1000+(moment+2*f*100/150)/(100*100/6.0*(1-2*f/ncr));
        var gusset=PackedBoltedTerminalData.Check(r,"Gusset.AndCollectorElastic");
        Assert.AreEqual(stress,gusset.Demand,1e-9);Assert.AreEqual(Math.Min(235/gm0,.9*360/1.25),gusset.Resistance,1e-12);
        Assert.AreEqual(ntc,r.Checks.Any(c=>c.Id=="Bolt3.NtcShearTensionInteraction"));
        Assert.IsTrue(r.ForceResistance>r.ForcePerChord);Assert.IsTrue(r.ForceResistance<=r.ForcePerChord/r.Utilization*(1+1e-9));
    }

    [TestMethod]
    public void NtcInteractionUsesAssembledBoltBearingAndPunchingMinima()
    {
        var m=PackedBoltedTerminalData.Model("C",true);var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m,true));
        double f=r.ForcePerChord,tension=f*ChannelNormalLever*100/12500;
        // Bolt3 (interior row): chord ply inner bolt alpha=50/51-0.25, k1=2.5 at edge 88;
        // gusset ply end bolt alpha=30/51, per shear plane half of the gusset bearing.
        double chord=2.5*(50/51.0-.25)*360*16*10/1.25,gusset=2.5*(30/51.0)*360*16*10/1.25;
        double transverse=2.5*Math.Min(1,88/51.0)*360*16*10/1.25;
        double shear=Math.Min(60288,Math.Min(Math.Min(chord,transverse),Math.Min(gusset,2.5*Math.Min(1,50/51.0)*360*16*10/1.25)/2));
        double punching=.6*Math.PI*25.4*10*360/1.25,tensile=Math.Min(90432,punching);
        Assert.AreEqual(f/3/shear+tension/(1.4*tensile),PackedBoltedTerminalData.Check(r,"Bolt3.NtcShearTensionInteraction").Demand,1e-12);
        Assert.AreEqual(tensile,PackedBoltedTerminalData.Check(r,"Bolt3.NtcTensionLimit").Resistance,1e-9);
    }

    [TestMethod]
    public void AngleTerminalRetainsInPlaneCoupleAndRigidFlangePryingBound()
    {
        var m=PackedBoltedTerminalData.Model("L-long",n:50000);var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Checks.Where(c=>!c.Passed).Select(c=>c.Id+":"+c.Utilization)));
        double f=r.ForcePerChord,et=80-AngleTransverseCentroid;
        Assert.AreEqual(AngleNormalLever,PackedBoltedTerminalData.Value(r,"BoltedTerminal.NormalLever"),1e-12);
        Assert.AreEqual(et,PackedBoltedTerminalData.Value(r,"BoltedTerminal.InPlaneLever"),1e-12);
        // Heel: outstanding leg 12 mm, no radius: m=80-12=68; toe 150-80=70<1.25m.
        Assert.AreEqual(138/70.0,PackedBoltedTerminalData.Value(r,"BoltedTerminal.PryingBoundFactor"),1e-14);
        // Elastic line at -50/0/+50: transverse F*e_t*50/5000 on the end bolts only.
        Assert.AreEqual(Math.Sqrt(f*f/9+Math.Pow(f*et*50/5000,2)),PackedBoltedTerminalData.Value(r,"Bolt1.ShearPerPlane"),1e-9);
        Assert.AreEqual(f/3,PackedBoltedTerminalData.Value(r,"Bolt2.ShearPerPlane"),1e-9);
        double tension=f*AngleNormalLever*100/12500;
        Assert.AreEqual(138/70.0*tension,PackedBoltedTerminalData.Value(r,"Bolt3.TensionWithPrying"),1e-9);
        Assert.AreEqual(34*tension,PackedBoltedTerminalData.Check(r,"Bolt3.ChordWall.Bending").Demand,1e-7);
        Assert.AreEqual(.25*50*144*235,PackedBoltedTerminalData.Check(r,"Bolt3.ChordWall.Bending").Resistance,1e-9);
        // Gusset 120 wide centred on the centroid line: edges 60+e_t below, 60-e_t above the
        // bolt line; band from -100*tan30 to 60-e_t, resultant at -e_t.
        double spread=100*Math.Tan(Math.PI/6),bandLow=-spread,bandHigh=60-et,band=bandHigh-bandLow;
        Assert.AreEqual(band,PackedBoltedTerminalData.Value(r,"Gusset.EffectiveBandWidth"),1e-9);
        Assert.AreEqual(Math.Abs(-et-(bandLow+bandHigh)/2),PackedBoltedTerminalData.Value(r,"Gusset.BandOffsetFromResultant"),1e-9);
    }

    [TestMethod]
    public void PackingAndLongJointFactorsReachEveryBolt()
    {
        // Gap 20 = gusset 6 + two 7 mm packings; seven bolts at 50: Lj=300>15d=240.
        var m=PackedBoltedTerminalData.Model("C",n:20000,gap:20,panel:250);
        var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m,bolts:7,gusset:6,packing:7));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        double betaL=1-(300-240)/(200*16.0),betaP=9*16/(8*16+3*7.0);
        foreach(int i in Enumerable.Range(1,7))
        {
            Assert.AreEqual(betaL,PackedBoltedTerminalData.Value(r,"Bolt"+i+".BetaLongJoint"),1e-15);
            Assert.AreEqual(betaP,PackedBoltedTerminalData.Value(r,"Bolt"+i+".BetaPacking"),1e-15);
            Assert.AreEqual(60288*betaL*betaP,PackedBoltedTerminalData.Check(r,"Bolt"+i+".Bolt.Shear").Resistance,1e-9);
        }
        Assert.IsTrue(r.Checks.Any(c=>c.Id=="Bolt1.Packing.BearingEllipse"));
        // Sum of squares 2500*(1+4+9+16+25+36)=227500 mm2.
        Assert.AreEqual(r.ForcePerChord*ChannelNormalLever*300/227500,PackedBoltedTerminalData.Value(r,"Bolt7.CoupleTension"),1e-9);
    }

    [DataTestMethod]
    [DataRow("cjp")][DataRow("restraint")][DataRow("fusion")][DataRow("collector")][DataRow("execution")][DataRow("gap")]
    [DataRow("pitch")][DataRow("maxpitch")][DataRow("edge")][DataRow("gusset")][DataRow("station")][DataRow("holes")]
    public void PhysicalDetailRequirementsCannotBeReplacedByLabels(string missing)
    {
        var m=PackedBoltedTerminalData.Model(holes:missing!="holes");
        var d=PackedBoltedTerminalData.Terminal(m,cjp:missing!="cjp",fixedRoot:missing!="restraint",fusion:missing=="fusion"?9:-1,
            collectorWidth:missing=="collector"?90:-1,execution:missing=="execution"?" ":"Filled holes",gusset:missing=="gap"?8:10,
            pitch:missing=="pitch"?40:missing=="maxpitch"?150:50,line:missing=="edge"?25:null,width:missing=="gusset"?40:-1,
            tip:missing=="station"?80:30,bolts:missing=="maxpitch"?2:3);
        var r=SteelPackedPairBoltedTerminal.Verify(m,d);
        Assert.IsFalse(r.IsComplete);Assert.IsNull(r.ForceResistance);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.AreEqual(0,r.Checks.Count);
    }

    [TestMethod]
    public void DeclaredMemberEndIsAPinWithHeldTranslations()
    {
        var m=PackedBoltedTerminalData.Model();var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m));
        var e=r.AssumedMemberEnd;
        Assert.AreEqual(SteelRestraintState.Fixed,e.Translation1);Assert.AreEqual(SteelRestraintState.Fixed,e.Translation2);
        Assert.AreEqual(SteelRestraintState.Free,e.Rotation1);Assert.AreEqual(SteelRestraintState.Free,e.Rotation2);
        Assert.AreEqual(SteelRestraintState.Unknown,e.Rotation3);Assert.IsFalse(r.CanQualifyWholeMember);
    }

    [DataTestMethod][DataRow("C",false)][DataRow("C",true)][DataRow("L-long",false)]
    public void ComputedBoltedTerminalFlowsIntoMemberAndReport(string kind,bool ntc)
    {
        var m=PackedBoltedTerminalData.Model(kind,ntc,40000);var terminal=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m,ntc));
        var p=SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(m,kind,ntc),terminal,"Actual forks");
        Assert.AreSame(terminal,p.ComputedBoltedTerminal);Assert.IsNull(p.ComputedTerminal);
        Assert.AreEqual(terminal.ForcePerChord,p.ConnectionDemand);Assert.AreEqual(terminal.ForceResistance!.Value,p.ConnectionResistance);
        var result=PackedWeldedCaseData.Member(p);Assert.AreEqual(SteelExecutionStatus.Passed,result.Status,result.Message);
        Assert.IsTrue(result.Trace.Any(x=>x.Name.StartsWith("PackedPair.BoltedTerminal.")));
        StringAssert.Contains(result.Scope,"Computed bolted terminal");
        var report=StandardConnectionCaseReport.Create(p);
        Assert.AreEqual("Steel.Connection.PackedPair.BoltedPackingBoltedTerminal",report.MethodId);
        Assert.IsTrue(report.IsComplete);Assert.AreEqual(ConnectionStatus.Passed,report.Status);
        Assert.IsTrue(report.Checks.Any(c=>c.Id.StartsWith("Terminal.Bolt"))&&report.Checks.Any(c=>c.Id.StartsWith("Packing.")));
    }

    [TestMethod]
    public void WrongDemandIncompleteTerminalAndOverlappingFootprintsAreRefused()
    {
        var a=PackedBoltedTerminalData.Model();var b=PackedBoltedTerminalData.Model();
        var other=SteelPackedPairBoltedTerminal.Verify(b,PackedBoltedTerminalData.Terminal(b));
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(a),other,"Forks"));
        var incomplete=SteelPackedPairBoltedTerminal.Verify(a,PackedBoltedTerminalData.Terminal(a,cjp:false));
        Assert.ThrowsException<NotSupportedException>(()=>SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(a),incomplete,"Forks"));
        // 100 mm panels: gusset tip 50+30 plus half packing 30 exceeds the first panel.
        var tight=PackedBoltedTerminalData.Model(panel:100);var t=SteelPackedPairBoltedTerminal.Verify(tight,PackedBoltedTerminalData.Terminal(tight));
        Assert.IsTrue(t.IsComplete,string.Join(";",t.Diagnostics));
        Assert.ThrowsException<NotSupportedException>(()=>SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(tight),t,"Forks"));
        Assert.ThrowsException<ArgumentNullException>(()=>SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(a),null!,"Forks"));
    }

    [TestMethod]
    public void TerminalFailureIsKeptAndCannotBeHiddenByAPassingPacking()
    {
        var m=PackedBoltedTerminalData.Model();var r=SteelPackedPairBoltedTerminal.Verify(m,PackedBoltedTerminalData.Terminal(m,free:400));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.ForceResistance<r.ForcePerChord);
        var p=SteelPackedPairAssessment.FromBoltedTerminal(PackedBoltedTerminalData.Packing(m),r,"Forks");
        Assert.AreEqual(SteelExecutionStatus.Failed,PackedWeldedCaseData.Member(p).Status);
    }

    [TestMethod]
    public void CatalogListsTheBoltedTerminalAsABoundedStandardRoute()
    {
        var c=ConnectionCapabilityCatalog.Get("StandardBoltedPackedTerminal");
        Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification,c.Level);
        Assert.AreEqual("SteelPackedPairBoltedTerminal.Verify",c.RecommendedEntryPoint);
        Assert.IsTrue(c.MissingMechanisms.Any(x=>x.Contains("T attachment"))&&c.ExternalRequirements.Any(x=>x.Contains("T/FT")));
    }

    [TestMethod]
    public void ExistingWeldedTerminalRouteIsUnchangedByTheBoltedFactory()
    {
        var m=PackedWeldedCaseData.Model(holes:true);var terminal=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m));
        var p=SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(m),terminal,"Actual forks");
        Assert.IsNull(p.ComputedBoltedTerminal);
        var result=PackedWeldedCaseData.Member(p);Assert.IsFalse(result.Scope.Contains("bolted terminal"));
        Assert.AreEqual("Steel.Connection.PackedPair.BoltedPackingWeldedTerminal",StandardConnectionCaseReport.Create(p).MethodId);
    }
}
