using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedPackingStandardTests
{
    [DataTestMethod][DataRow("C",false)][DataRow("C",true)][DataRow("L-long",false)][DataRow("L-long",true)][DataRow("L-short",false)][DataRow("L-short",true)]
    public void AllWeldedRouteUsesComputedSameCaseWallsAndTerminals(string kind,bool ntc)
    {
        var m=PackedWeldedCaseData.Model(kind,ntc);var packing=SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m,ntc));
        var terminal=SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,ntc));
        Assert.IsTrue(packing.IsComplete,string.Join(";",packing.Diagnostics));Assert.AreEqual(ConnectionStatus.Passed,packing.Status);
        var p=SteelPackedPairAssessment.FromWeldedPacking(packing,terminal,"Actual fork restraints");
        var r=PackedWeldedCaseData.Member(p);Assert.AreEqual(SteelExecutionStatus.Passed,r.Status,r.Message);
        Assert.AreSame(packing,p.ComputedWeldedPacking);Assert.AreSame(terminal,p.ComputedTerminal);
        Assert.IsTrue(r.Trace.Any(x=>x.Name=="PackedPair.Welded.Chord0.ElasticWallAndLap"));
        Assert.IsTrue(packing.CanQualifyWholeStation);Assert.IsFalse(packing.CanQualifyWholeMember);
    }
    [TestMethod]
    public void ActualIntermediateForceIsNotHalfTheStationTransfer()
    {
        var m=PackedWeldedCaseData.Model();var r=SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m));
        double amplitude=m.ShearFlowPeak*3000/Math.PI;
        double q=amplitude*(Math.Sin(Math.PI*150/3000)-Math.Sin(Math.PI*50/3000));
        Assert.AreEqual(q,r.MaximumConnectionDemand,1e-10);
        Assert.AreEqual(q,r.Trace.Single(x=>x.Name=="Lap.ForcePerFace").Value,1e-10);
        Assert.AreEqual(29,r.Trace.Count(x=>x.Name.StartsWith("Packing.Station")));
    }
    [TestMethod]
    public void IsolatedWallBoundHasIndependentAreaModulusAndEccentricity()
    {
        var m=PackedWeldedCaseData.Model();var r=SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m));
        double area=2*75*12+176*10,x=(2*75*12*37.5+176*10*5)/area;
        double lever=10+10+2*x,stripArea=60*10,modulus=60*100/6,q=r.MaximumConnectionDemand;
        double background=r.Trace.Single(v=>v.Name=="Lap.MemberNormalStressBound").Value;
        Assert.AreEqual(q*lever,r.Trace.Single(v=>v.Name=="Chord0.LapMoment").Value,1e-8);
        Assert.AreEqual(background+q*(1+Math.Sqrt(3))/stripArea+q*lever/modulus,
            r.Checks.Single(c=>c.Id=="Chord0.ElasticWallAndLap").Demand,1e-10);
        Assert.IsTrue(r.Checks.Any(c=>c.Id.StartsWith("WallLine")));Assert.IsTrue(r.Checks.Any(c=>c.Id.StartsWith("PlateLine")));
    }
    [DataTestMethod][DataRow("holes")][DataRow("footprint")][DataRow("throat")][DataRow("overlap")]
    public void OutOfFieldWeldDetailsRemainUnsupported(string reason)
    {
        var m=PackedWeldedCaseData.Model(holes:reason=="holes");var d=PackedWeldedCaseData.Lap(m,holes:reason=="footprint",throat:reason=="throat"?2:3);
        if(reason=="overlap")d=new(d.Code,d.Width,100,d.Thickness,d.Centre,d.Throat,d.Fy,d.Fu,d.PlateGrade,d.ChordGrade,true,d.ExecutionSource);
        var r=SteelWeldedPacking.Verify(m,d);Assert.IsFalse(r.IsComplete);Assert.IsNull(r.ConditionalForceResistance);
    }
    [TestMethod]
    public void SameNumbersFromDifferentModelsDoNotQualifyTheMember()
    {
        var a=PackedWeldedCaseData.Model();var b=PackedWeldedCaseData.Model();
        var packing=SteelWeldedPacking.Verify(a,PackedWeldedCaseData.Lap(a));var terminal=SteelPackedPairTerminal.Verify(b,PackedWeldedCaseData.Terminal(b));
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedPairAssessment.FromWeldedPacking(packing,terminal,"Forks"));
    }
    [TestMethod]
    public void TorsionalModeExclusionsRemainMandatory()
    {
        var m=PackedWeldedCaseData.Model();var p=SteelPackedPairAssessment.FromWeldedPacking(SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m)),
            SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m)),"Forks");
        var a=SteelMemberAnalysis.PackedCompression(p,"N","Curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C);
        Assert.AreNotEqual(SteelExecutionStatus.Passed,SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single().Status);
    }
    [TestMethod]
    public void FailedTerminalPreventsPassingTheAllWeldedMember()
    {
        var m=PackedWeldedCaseData.Model();var p=SteelPackedPairAssessment.FromWeldedPacking(SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m)),
            SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,free:5000)),"Forks");
        Assert.AreEqual(SteelExecutionStatus.Failed,PackedWeldedCaseData.Member(p).Status);
    }
    [TestMethod]
    public void AbsentIntermediateStationIsNotACompletePackingSystem()
    {
        var m=PackedWeldedCaseData.Model();var onlyEnds=PackedPairImperfectionModel.Evaluate(new SteelPackingLayout(m.Layout.Input,new[]{0d,3000d},"Ends only"));
        var r=SteelWeldedPacking.Verify(onlyEnds,PackedWeldedCaseData.Lap(onlyEnds));Assert.IsFalse(r.IsComplete);
    }
    [TestMethod]
    public void ResistanceDoesNotEraseHigherMemberNormalStress()
    {
        var low=PackedWeldedCaseData.Model(n:1000);var high=PackedWeldedCaseData.Model(n:10000);
        var a=SteelWeldedPacking.Verify(low,PackedWeldedCaseData.Lap(low));var b=SteelWeldedPacking.Verify(high,PackedWeldedCaseData.Lap(high));
        Assert.IsTrue(a.IsComplete&&b.IsComplete);Assert.IsTrue(b.Utilization>a.Utilization);
        Assert.IsTrue(b.ConditionalForceResistance<a.ConditionalForceResistance);
    }
}
