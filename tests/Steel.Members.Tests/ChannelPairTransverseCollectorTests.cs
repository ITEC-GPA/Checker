using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ChannelPairTransverseCollectorTests
{
    private static ChannelPairTransverseCollectorInput Input(bool ntc=false,double total=50000,string change="")
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EN recommended");
        var bolt=new ConnectionBolt(20,22,245,ConnectionBoltGrade.Class8_8,true);
        var channel=new SectionC(200,8,75,12,75,12,"C200");
        Section shape=change=="single"?channel:SectionBuiltUp.DoubleChannel(channel,change=="gap"?12:16,change!="toes","2C200");
        if(change=="offset")shape=new SectionBuiltUp(new[]{new SectionBuiltUp.Part(channel,8,0),new SectionBuiltUp.Part(channel,-8,10,mirrorX:true)},"offset");
        var section=new SteelSectionInput(new SteelSection(shape,new SteelMaterial("S275",210000,275,430),formedType:Section.FormedTypes.HotFinished));
        var end=new ChannelWebTensionInput(code,section,total/2,
            change=="aisc"?ChannelWebTensionMethod.AiscShearLagAlternative:ChannelWebTensionMethod.ConnectedWebOnly,
            bolt,2,80,3,70,40,37,"Actual channel and hardware","SLS clearance accepted","Standalone alternative only");
        var transfer=new GussetTwinFlatTensionInput(code,new(240,16,275,430),
            new(change=="strip-width"?201:200,change=="strip-thickness"?9:8,275,430),
            change=="bolt"?new(24,26,353,ConnectionBoltGrade.Class8_8,true):bolt,
            new(change=="share"?total+1:total,0,0,0,0,change=="eccentric"?1:0),
            change=="rows"?2:3,70,80,40,40,holeClearanceServiceabilitySource:"SLS clearance accepted");
        var attachment=new GussetTwinFlatWeldedSupportInput(transfer,150,240,160,6,FilletWeldSteelGrade.S275,
            new(FilletWeldSteelGrade.S275,430,20),FilletWeldMethod.Directional,FilletWeldEndCondition.FullSize,
            "Matching consumable","Symmetric full-width load distribution","Continuous double fillets");
        var collector=new GussetTwinFlatTransverseCollectorInput(attachment,new(240,20,275,430),160,210000,
            new(total/2,0,0,0,0,0,150,0,-80),new(change=="reaction"?-total/2:total/2,0,0,0,0,0,150,0,80),
            change=="source"?"":"Actual simple support lines", "Same LC1 reactions",true);
        return new(end,collector);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PhysicalChannelTerminalMatchesIndependentStripAndCollectorCalculations(bool ntc)
    {
        var r=ChannelPairTransverseCollector.Verify(Input(ntc));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        // Each channel web: (h-2d0)tw=(200-44)*8=1248mm2, N=25kN.
        var net=r.Checks.Single(c=>c.Id=="Channel.Web.NetRupture");
        Assert.AreEqual(25000,net.Demand);Assert.AreEqual(.9*1248*430/1.25,net.Resistance,1e-8);
        // Collector B240,t20,L160,P50kN: sigma=PL/4 /(Bt^2/6)=125,
        // tau=1.5 P/2 /(Bt)=7.8125. Independent elastic stress envelope.
        var stress=r.Checks.Single(c=>c.Id=="Collector.Collector.ElasticBendingAndShear");
        Assert.AreEqual(Math.Sqrt(125*125+3*7.8125*7.8125),stress.Demand,1e-10);
        Assert.AreEqual(275/(ntc?1.05:1),stress.Resistance,1e-10);
        var s=SteelAssessmentSummary.From(r);Assert.AreEqual(EngineeringOutcome.Satisfied,s.Outcome);
        Assert.IsTrue(s.ExternalConditions.Any(x=>x.Contains("supporting structures")));
        Assert.AreEqual(r.Checks.Count,r.Checks.Select(c=>c.Id).Distinct().Count());
        Assert.AreEqual(r.Trace.Count,r.Trace.Select(c=>c.Name).Distinct().Count());
    }

    [DataTestMethod]
    [DataRow("single")][DataRow("gap")][DataRow("toes")][DataRow("offset")][DataRow("aisc")]
    [DataRow("strip-width")][DataRow("strip-thickness")][DataRow("bolt")][DataRow("share")][DataRow("rows")]
    [DataRow("eccentric")][DataRow("reaction")][DataRow("source")]
    public void InconsistentGeometryAndBoundaryActionsCannotQualifyTheTerminal(string change)
    {
        var r=ChannelPairTransverseCollector.Verify(Input(change:change));
        Assert.IsFalse(r.IsComplete);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsNull(r.Utilization);Assert.AreEqual(0,r.Checks.Count);
    }

    [TestMethod]
    public void FailureIsRetainedWhenTheCollectorBoundaryIsUnknown()
    {
        var r=ChannelPairTransverseCollector.Verify(Input(total:3e6,change:"source"));
        var s=SteelAssessmentSummary.From(r);
        Assert.IsFalse(s.IsComplete);Assert.AreEqual(EngineeringOutcome.NotSatisfied,s.Outcome);
        Assert.IsTrue(r.Checks.Any(c=>c.Id.StartsWith("Channel.")&&!c.Passed));
    }

    [TestMethod]
    public void EmptyLoadCaseAndCatalogRetainTheActualScope()
    {
        var r=ChannelPairTransverseCollector.Verify(Input(total:0));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(0,r.Utilization);
        Assert.AreEqual("ChannelPairTransverseCollector.Verify",ConnectionCapabilityCatalog.Get("ChannelPairTransverseCollector").RecommendedEntryPoint);
        Assert.IsTrue(ConnectionCapabilityCatalog.Get("ChannelPairTransverseCollector").MissingMechanisms.Any(x=>x.Contains("Single C")));
    }
}
