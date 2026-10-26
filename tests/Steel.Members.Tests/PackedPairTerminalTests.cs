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

internal static class PackedWeldedCaseData
{
    internal static PackedPairImperfectionModel Model(string kind="C",bool ntc=false,double n=1000,bool holes=false)
    {
        var pair=PackedPairCompressionTests.Pair(kind,10);double t=kind=="C"?10:12;
        var i=new SteelVerificationInput(new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S235",210000,235,360)),!holes,holes?pair.Area-2*17*t:null),
            new SteelActions(-n,0,0,0,0,0),ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),new SteelMemberInput(3000,3000,3000));
        return PackedPairImperfectionModel.Evaluate(new SteelPackingLayout(i,Enumerable.Range(0,31).Select(x=>100d*x),"Actual equal terminal and intermediate station layout"));
    }
    internal static ConnectionDesignCode Code(bool ntc=false)=>ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"test");
    internal static PackedPairWeldedLapDetail Lap(PackedPairImperfectionModel m,bool ntc=false,double shift=0,bool holes=false,double throat=3)
    {
        var pair=(SectionBuiltUp)m.Layout.Input.Section.CreateSectionCopy().SectionShape;var p=pair.Parts[0];bool vertical=p.MirrorX!=pair.Parts[1].MirrorX;
        double centre=vertical?p.Section.Centroid.Y:p.Section.Centroid.X;double width=centre<35?30:60;
        return new(Code(ntc),width,60,10,centre+shift,throat,235,360,FilletWeldSteelGrade.S235,FilletWeldSteelGrade.S235,!holes,"Actual full-size welds, sound unperforated roots and compatible consumables");
    }
    internal static SteelPackedPairTerminalDetail Terminal(PackedPairImperfectionModel m,bool ntc=false,double free=20,double overhang=40,
        bool cjp=true,bool fixedRoot=true,double fusion=10,double weldFy=235,double shift=0)
    {
        var lap=Lap(m,ntc,shift);
        return new(lap,new(lap.Width,10,235,360),free,overhang,fusion,lap.Width,weldFy,360,cjp,fixedRoot,
            "Coplanar aligned mid-surfaces, root rotation restraint, full penetration and inspection");
    }
    internal static SteelBoltedPackingResult Bolted(PackedPairImperfectionModel m,string kind="C",bool ntc=false)
        =>SteelBoltedPacking.Verify(m,new(Code(ntc),new(16,17,157,ConnectionBoltGrade.Class8_8,true),60,60,10,
            kind=="C"?100:kind=="L-long"?80:55,235,360,32,true,"Actual filled holes and hardened washers","Normal hole clearance"));
    internal static SteelCheckExecution Member(SteelPackedPairAssessment p)
    {
        var a=SteelMemberAnalysis.PackedCompression(p,"Constant N and physical forks","Explicit C curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"Independently excluded T mode",flexuralTorsionalExclusionSource:"Independently excluded FT mode");
        return SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    }
}

[TestClass]
public class PackedPairTerminalTests
{
    [DataTestMethod][DataRow("C",false)][DataRow("C",true)][DataRow("L-long",false)][DataRow("L-long",true)][DataRow("L-short",false)][DataRow("L-short",true)]
    public void WeldedTerminalHasActualChordWallAndCollectorChecks(string kind,bool ntc)
    {
        var m=PackedWeldedCaseData.Model(kind,ntc);var r=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,ntc));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Checks.Where(c=>!c.Passed).Select(c=>c.Id+":"+c.Utilization)));
        Assert.IsTrue(r.CanQualifyWholeTerminal);Assert.IsFalse(r.CanQualifyWholeMember);
        Assert.IsTrue(r.ForcePerChord>500);Assert.IsTrue(r.ForceResistance>r.ForcePerChord);
        Assert.IsTrue(r.Checks.Any(c=>c.Id.StartsWith("WallLine")));Assert.IsTrue(r.Checks.Any(c=>c.Id=="Terminal.GussetAndCollectorElastic"));
    }
    [TestMethod]
    public void EndTransferAndContactSideMomentsAreNotSilentlyCancelled()
    {
        var m=PackedWeldedCaseData.Model();var r=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m));
        double end=m.ShearFlowPeak*3000/Math.PI*Math.Sin(Math.PI*50/3000);
        Assert.AreEqual(500+end,r.ForcePerChord,1e-9);
        Assert.IsTrue(r.Trace.Single(x=>x.Name=="Terminal.CollectorMomentEnvelope").Value>0);
        Assert.IsTrue(r.Trace.Single(x=>x.Name=="Chord0.LapMoment").Value>0);
        Assert.AreEqual(r.Trace.Single(x=>x.Name=="Chord0.LapMoment").Value,r.Trace.Single(x=>x.Name=="Chord1.LapMoment").Value);
    }
    [TestMethod]
    public void CantileverStripNcrAndSecondOrderStressHaveAnIndependentOracle()
    {
        var m=PackedWeldedCaseData.Model();var r=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m));
        double lk=100,a=600,w=1000,ncr=Math.PI*Math.PI*210000*5000/(lk*lk),force=2*r.ForcePerChord;
        double moment=r.Trace.Single(x=>x.Name=="Terminal.CollectorMomentEnvelope").Value;
        Assert.AreEqual(ncr,r.Trace.Single(x=>x.Name=="Terminal.GussetNcr").Value,1e-7);
        double expected=force/a+(moment+force*lk/150)/(w*(1-force/ncr));
        Assert.AreEqual(expected,r.Checks.Single(c=>c.Id=="Terminal.GussetAndCollectorElastic").Demand,1e-10);
    }
    [DataTestMethod][DataRow("overhang")][DataRow("cjp")][DataRow("restraint")][DataRow("fusion")][DataRow("weld")][DataRow("centroid")]
    public void PhysicalDetailRequirementsCannotBeReplacedByLabels(string missing)
    {
        var m=PackedWeldedCaseData.Model();var d=PackedWeldedCaseData.Terminal(m,overhang:missing=="overhang"?1:40,cjp:missing!="cjp",fixedRoot:missing!="restraint",
            fusion:missing=="fusion"?9:10,weldFy:missing=="weld"?200:235,shift:missing=="centroid"?1:0);
        var r=SteelPackedPairTerminal.Verify(m,d);Assert.IsFalse(r.IsComplete);Assert.IsNull(r.ForceResistance);
    }
    [TestMethod]
    public void UnstableGussetIsAFailureWithNoFavourableAmplification()
    {
        var m=PackedWeldedCaseData.Model();var r=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,free:5000));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.Checks.Single(c=>c.Id=="Terminal.GussetEuler").Utilization>1);
    }
    [DataTestMethod][DataRow("C",false)][DataRow("C",true)][DataRow("L-long",false)][DataRow("L-short",true)]
    public void ComputedTerminalFlowsIntoMemberWithoutExternalCapacity(string kind,bool ntc)
    {
        var m=PackedWeldedCaseData.Model(kind,ntc,holes:true);var terminal=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,ntc));
        var p=SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(m,kind,ntc),terminal,"Actual forks");
        var result=PackedWeldedCaseData.Member(p);Assert.AreEqual(SteelExecutionStatus.Passed,result.Status,result.Message);
        Assert.AreSame(terminal,p.ComputedTerminal);Assert.IsTrue(result.Trace.Any(x=>x.Name.StartsWith("PackedPair.Terminal.")));
    }
    [TestMethod]
    public void WrongDemandCannotBeAttachedEvenIfNumbersMatch()
    {
        var a=PackedWeldedCaseData.Model(holes:true);var b=PackedWeldedCaseData.Model(holes:true);
        var terminal=SteelPackedPairTerminal.Verify(b,PackedWeldedCaseData.Terminal(b));
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(a),terminal,"Forks"));
    }
    [TestMethod]
    public void TerminalFailureCannotBeHiddenByAPassingPacking()
    {
        var m=PackedWeldedCaseData.Model(holes:true);var terminal=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,free:5000));
        var p=SteelPackedPairAssessment.FromBoltedPacking(PackedWeldedCaseData.Bolted(m),terminal,"Forks");
        Assert.AreEqual(SteelExecutionStatus.Failed,PackedWeldedCaseData.Member(p).Status);
    }
}
