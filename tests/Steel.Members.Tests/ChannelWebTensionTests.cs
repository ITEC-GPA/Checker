using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Channel connected by the web only, in tension. Benchmark geometry of
/// PHASE_3C_3E_CHANNEL_TENSION_REVIEW.txt section4: sharp C200x75x8/12, A=3208 mm2,
/// xbar=73132/3208 mm from the web back; S275 fu430; M20 8.8 in 22 mm holes.</summary>
[TestClass]
public class ChannelWebTensionTests
{
    private const double Area=3208, Xbar=73132.0/3208, Fv=.6*800*245/1.25;
    private static ConnectionDesignCode Code(bool ntc)=>ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EC recommended");
    private static SteelSectionInput Single(Section? shape=null,Section.FormedTypes formed=Section.FormedTypes.HotFinished)
        =>new(new SteelSection(shape??new SectionC(200,8,75,12,75,12,"C200"),new SteelMaterial("S275",210000,275,430),formedType:formed));
    private static ChannelWebTensionInput Input(double n=200000,ChannelWebTensionMethod method=ChannelWebTensionMethod.ConnectedWebOnly,bool ntc=false,
        SteelSectionInput? section=null,int lines=1,double spacing=0,int bolts=3,double pitch=70,double end=40,string? alternative=null)
        =>new(Code(ntc),section??Single(),n,method,new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true),lines,spacing,bolts,pitch,end,37,
            "Actual drawing; no other holes","SLS hole clearance accepted",alternative);
    private static ConnectionCheck Check(ConnectionVerification<ChannelWebTensionInput> r,string id)=>r.Checks.Single(c=>c.Id==id);
    private static double Value(ConnectionVerification<ChannelWebTensionInput> r,string id)=>r.Trace.Single(c=>c.Name==id).Value;

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void WebOnlyFieldUsesTheWebStripAndItsBlocksOnly(bool ntc)
    {
        var r=ChannelWebTension.Verify(Input(ntc:ntc));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        double gm0=ntc?1.05:1;
        Assert.AreEqual(3208,Value(r,"Channel.GrossArea"),1e-9);Assert.AreEqual(Xbar,Value(r,"Channel.CentroidFromWebBack"),1e-12);
        // Web strip 200x8=1600, one 22 mm hole: 1424 mm2.
        Assert.AreEqual(1600*275/gm0,Check(r,"Web.GrossYielding").Resistance,1e-9);
        Assert.AreEqual(.9*1424*430/1.25,Check(r,"Web.NetRupture").Resistance,1e-9);
        Assert.AreEqual(440870.4,Check(r,"Web.NetRupture").Resistance,1e-6);
        // Shear plane (40+140-2.5*22)*8=1000 mm2; edge block (100-11)*8=712 mm2, eccentric.
        Assert.AreEqual(1000,Value(r,"Block.ShearPlaneArea"),1e-12);
        double edge=.5*712*430/1.25+1000*275/(Math.Sqrt(3)*gm0);
        Assert.AreEqual(edge,Check(r,"Block.EdgeTop").Resistance,1e-9);Assert.AreEqual(200000,Check(r,"Block.EdgeTop").Demand);
        Assert.IsFalse(r.Checks.Any(c=>c.Id.StartsWith("Channel.")));
        // Bolts: N/3 per plane, Lj=140<15d so betaLf=1; bearing alpha 40/66 and 70/66-1/4, k1=2.5.
        Assert.AreEqual(Fv,Check(r,"Bolt.Shear").Resistance,1e-9);Assert.AreEqual(200000/3.0,Check(r,"Bolt.Shear").Demand,1e-9);
        Assert.AreEqual(2.5*(40/66.0)*430*20*8/1.25,Check(r,"Web.Bearing.EndBolt.OuterLine").Resistance,1e-9);
        Assert.AreEqual(2.5*(70/66.0-.25)*430*20*8/1.25,Check(r,"Web.Bearing.InnerBolt.OuterLine").Resistance,1e-9);
    }

    [TestMethod]
    public void TwoLinesUseInteriorAndOuterLineBlocks()
    {
        var r=ChannelWebTension.Verify(Input(lines:2,spacing:80));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Assert.AreEqual(60,Value(r,"Bolts.OuterLineEdge"),1e-12);
        Assert.AreEqual(.9*1248*430/1.25,Check(r,"Web.NetRupture").Resistance,1e-9);
        Assert.AreEqual(464*430/1.25+2000*275/Math.Sqrt(3),Check(r,"Block.Interior").Resistance,1e-9);
        Assert.AreEqual(.5*392*430/1.25+1000*275/Math.Sqrt(3),Check(r,"Block.EdgeBottom").Resistance,1e-9);
        Assert.AreEqual(100000,Check(r,"Block.EdgeBottom").Demand,1e-9);
        Assert.AreEqual(200000/6.0,Check(r,"Bolt.Shear").Demand,1e-9);
        // EC edge bolt with a transverse neighbour: k1=min(2.5,2.8*60/22-1.7,1.4*80/22-1.7)=3.39 -> 2.5.
        Assert.AreEqual(2.5*(40/66.0)*430*160/1.25,Check(r,"Web.Bearing.EndBolt.OuterLine").Resistance,1e-9);
    }

    [TestMethod]
    public void DeclaredAiscAlternativeUsesShearLagWithoutTheConnectedElementRelaxation()
    {
        var r=ChannelWebTension.Verify(Input(method:ChannelWebTensionMethod.AiscShearLagAlternative,alternative:"Project acceptance of AISC 360-16 D3 as alternative"));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        double u=1-Xbar/140;
        Assert.AreEqual(u,Value(r,"Aisc.U"),1e-15);Assert.AreEqual(1600/3208.0,Value(r,"Aisc.ConnectedWebRatioNotUsed"),1e-15);
        Assert.AreEqual(.9*u*3032*430/1.25,Check(r,"Channel.EffectiveNetRupture").Resistance,1e-9);
        Assert.AreEqual(Area*275,Check(r,"Channel.GrossYielding").Resistance,1e-9);
        Assert.IsFalse(r.Checks.Any(c=>c.Id.StartsWith("Web.Gross")||c.Id=="Web.NetRupture"));
        // The web-only field is the more conservative of the two for this detail.
        var web=ChannelWebTension.Verify(Input());
        Assert.IsTrue(Check(web,"Web.NetRupture").Resistance<Check(r,"Channel.EffectiveNetRupture").Resistance);
        // Wide flanges and the shortest joint: 1-xbar/l falls below Aweb/A; the D3 relaxation to
        var wide=new SectionC(200,6,100,12,100,12,"C200x100");
        var f=ChannelWebTension.Verify(Input(method:ChannelWebTensionMethod.AiscShearLagAlternative,alternative:"Accepted",
            section:Single(wide),bolts:2,pitch:49));
        Assert.IsTrue(f.IsComplete,string.Join(";",f.Diagnostics));
        double xbar=(1200*3+2*94*12*(6+47))/(1200+2*94*12.0),floor=1200/(1200+2*94*12.0);
        Assert.AreEqual(xbar,Value(f,"Channel.CentroidFromWebBack"),1e-12);
        Assert.IsTrue(1-xbar/49<floor);Assert.AreEqual(1-xbar/49,Value(f,"Aisc.U"),1e-15);
    }

    [TestMethod]
    public void BackToBackPairUsesTheShareOfOneChannel()
    {
        var pair=SectionBuiltUp.DoubleChannel(new SectionC(200,8,75,12,75,12,"C200"),10,true,"2C200");
        var single=ChannelWebTension.Verify(Input());var r=ChannelWebTension.Verify(Input(section:Single(pair)));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Assert.AreEqual(single.Checks.Count,r.Checks.Count);
        foreach(var c in single.Checks){var d=Check(r,c.Id);Assert.AreEqual(c.Demand,d.Demand,0);Assert.AreEqual(c.Resistance,d.Resistance,0);}
    }

    [DataTestMethod]
    [DataRow("unspecified")][DataRow("aisc-source")][DataRow("taper")][DataRow("cold")][DataRow("unequal")][DataRow("onebolt")]
    [DataRow("flange")][DataRow("pitch")][DataRow("end")][DataRow("geometry")]
    public void UnjustifiedFieldsAreRefused(string field)
    {
        Section shape=field=="taper"?new SectionCTaperFlange(200,8,75,12,.08,10,5,12,"UPN"):field=="unequal"?new SectionC(200,8,75,12,60,12,"C"):new SectionC(200,8,75,12,75,12,"C");
        var input=new ChannelWebTensionInput(Code(false),Single(shape,field=="cold"?Section.FormedTypes.ColdFormed:Section.FormedTypes.HotFinished),200000,
            field=="unspecified"?ChannelWebTensionMethod.Unspecified:field=="aisc-source"?ChannelWebTensionMethod.AiscShearLagAlternative:ChannelWebTensionMethod.ConnectedWebOnly,
            new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true),field=="flange"?2:1,field=="flange"?170:0,field=="onebolt"?1:3,
            field=="pitch"?120:70,field=="end"?20:40,37,field=="geometry"?" ":"Actual drawing");
        var r=ChannelWebTension.Verify(input);
        Assert.IsFalse(r.IsComplete);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.AreEqual(0,r.Checks.Count);
    }

    [TestMethod]
    public void OverloadIsAKnownFailureWithinTheDeclaredField()
    {
        var r=ChannelWebTension.Verify(Input(n:500000));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(Check(r,"Web.GrossYielding").Utilization>1);
    }

    [TestMethod]
    public void CatalogRegistersTheChannelEndAsComponentsOnly()
    {
        var c=ConnectionCapabilityCatalog.Get("ChannelWebOnlyTensionEnd");
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,c.Level);Assert.AreEqual("ChannelWebTension.Verify",c.RecommendedEntryPoint);
        Assert.IsTrue(c.MissingMechanisms.Any(x=>x.Contains("collector")));
    }
}
