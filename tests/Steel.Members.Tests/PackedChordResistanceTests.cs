using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class PackedChordResistanceTests
{
    public static IEnumerable<object[]> Cases()
    {foreach(var f in new[]{"C","L-long","L-short"})foreach(bool ntc in new[]{false,true})foreach(double pitch in new[]{100d,200d})yield return new object[]{f,ntc,pitch};}
    private static SteelPackedPairAssessment Packing(SteelVerificationInput i,double pitch,double compression=60000,double connection=50000)=>
        SteelPackedPairAssessment.FromUniformForkChords(i,pitch,10,10,compression,1000,10000,connection,
            "Pitch includes end panels, verified packing and gussets","Complete local envelopes, centroidal chord forces",
            "Complete connection/plate resistance independently verified","Each panel: constant centroidal N, no local M/V, fork/free warping over pitch; same state");
    private static SteelCheckExecution Run(SteelPackedPairAssessment p)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(
        SteelMemberAnalysis.PackedCompression(p,"global concentric N","C curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"global torsion assessed",flexuralTorsionalExclusionSource:"global FT assessed"))).Checks.Single();

    [DataTestMethod] [DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void ChordEulerValuesUsePrincipalInertiasOfOneProfile(string family,bool ntc,double pitch)
    {
        var p=Packing(PackedPairCompressionTests.Input(family,ntc),pitch);var local=p.ChordCalculation.Compression;
        // Independent rectangle integration. No packing area or parallel-axis pair term.
        var rs=family=="C"?new[]{(0d,0d,75d,12d),(0d,12d,10d,176d),(0d,188d,75d,12d)}:
            new[]{(0d,0d,100d,12d),(0d,12d,12d,138d)};
        double area=rs.Sum(r=>r.Item3*r.Item4),cx=rs.Sum(r=>r.Item3*r.Item4*(r.Item1+r.Item3/2))/area,
            cy=rs.Sum(r=>r.Item3*r.Item4*(r.Item2+r.Item4/2))/area;
        double ix=0,iy=0,xy=0;
        foreach(var (x,y,b,h) in rs) {double dx=x+b/2-cx,dy=y+h/2-cy;ix+=b*h*h*h/12+b*h*dy*dy;iy+=h*b*b*b/12+b*h*dx*dx;xy+=b*h*dx*dy;}
        double d=Math.Sqrt(Math.Pow((ix-iy)/2,2)+xy*xy),major=(ix+iy)/2+d,minor=(ix+iy)/2-d;
        double Value(string key)=>local.Trace.Single(v=>v.Name==key).Value;
        Assert.AreEqual(Math.PI*Math.PI*210000*major/(pitch*pitch),Value("Ncr1"),Value("Ncr1")*1e-10);
        Assert.AreEqual(Math.PI*Math.PI*210000*minor/(pitch*pitch),Value("Ncr2"),Value("Ncr2")*1e-10);
        Assert.AreEqual(area*235/(ntc?1.05:1),p.ChordTensionResistance,1e-7);
        Assert.IsTrue(Value("NcrT")>0);Assert.IsTrue(Value("NcrFT")>0);
        Assert.AreEqual(local.Resistance,p.ChordCompressionResistance);
        var global=Run(p);Assert.IsTrue(global.IsEvaluated,global.Message);
        Assert.IsTrue(global.Trace.Any(v=>v.Name=="PackedPair.Chord.NcrFT"));
        Assert.IsTrue(global.Utilization>=local.Utilization);
    }
    [TestMethod]
    public void UnequalAngleJoinedLegChangesPairButNotTheSingleChordResistance()
    {
        var longLeg=Packing(PackedPairCompressionTests.Input("L-long"),100);
        var shortLeg=Packing(PackedPairCompressionTests.Input("L-short"),100);
        Assert.AreEqual(longLeg.ChordCompressionResistance,shortLeg.ChordCompressionResistance,1e-8);
        Assert.AreNotEqual(Run(longLeg).Trace.Single(t=>t.Name=="Ncr1").Value,Run(shortLeg).Trace.Single(t=>t.Name=="Ncr1").Value);
    }
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void ComputedChordOrExternalConnectionFailureCannotBeHidden(bool chord)
    {
        var p=Packing(PackedPairCompressionTests.Input("C"),100,chord?1e8:60000,chord?50000:5000);
        Assert.AreEqual(SteelExecutionStatus.Failed,Run(p).Status);
    }
    [TestMethod]
    public void LocalModelAndEnvelopeAreMandatory()
    {
        var i=PackedPairCompressionTests.Input("C");
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedChordResistance.UniformFork(i,100,60000," "));
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedChordResistance.UniformFork(i,100,40000,"model"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>SteelPackedChordResistance.UniformFork(i,0,60000,"model"));
        Assert.AreEqual(SteelExecutionStatus.NotSupported,Run(Packing(i,1000)).Status);
    }
}
