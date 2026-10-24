using GPC.Design.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Design.Checks;

[TestClass]
public sealed class SpectraTests
{
    [TestMethod]public void IndependentExcelCapture()=>SpectraEquivalence.Verify(Path.Combine(AppContext.BaseDirectory,"Fixtures/spettri-cslp103-excel.json"));
    [TestMethod]public void IsolationExampleAndAnalyticalCorrection(){
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/spettri-isolamento-excel.json")));var d=doc.RootElement;
        var h=d.GetProperty("hazard").EnumerateArray().Select(r=>new HazardParameters(r[0].GetDouble(),r[1].GetDouble(),r[2].GetDouble())).ToArray();
        var states=NtcSpectra.Calculate(NtcSpectra.Site(12.4818,41.9109,SiteInterpolation.RuledSurface),new(Soil:"B"),h);
        var result=IsolationSpectra.Calculate(states,new(412.6,5,10,1.5,1,[new("1",8,1.01),new("2",0,1.81)]));
        void Eq(double actual,double expected)=>Assert.AreEqual(expected,actual,1e-10*Math.Max(1,Math.Abs(expected)));
        foreach(var s in states){int i=Array.IndexOf(new[]{"SLO","SLD","SLV","SLC"},s.State);var r=result[i];
            var example=IsolationSpectra.ExampleSamples(s,r.ThresholdSeconds,1,Math.Sqrt(2d/3));var expected=d.GetProperty("spectra").GetProperty(s.State);for(int j=0;j<45;j++){Eq(example[j].PeriodSeconds,expected[j][0].GetDouble());Eq(example[j].AccelerationG,expected[j][1].GetDouble());}
            Eq(r.ForceKn,r.BearingForces.Sum(g=>g.Count*g.ForcePerBearingKn));Eq(r.DisplacementMm,r.ForceKn/r.StiffnessKnPerMm);
        }
        var iso=d.GetProperty("isolation");Eq(result[3].PeriodSeconds,iso.GetProperty("H11").GetDouble());Eq(result[0].PeriodSeconds,iso.GetProperty("H33").GetDouble());Eq(result[3].ExampleSampleAccelerationG,iso.GetProperty("H26").GetDouble());
        Eq(result[3].ExampleSampleAccelerationG*9.81*412.6,iso.GetProperty("H27").GetDouble());
        // Independent closed form: at Tis the example is on the constant-velocity branch, with eta=sqrt(2/3).
        var slc=states[3];double exact=h[3].AgG*slc.Ss*h[3].F0*Math.Sqrt(2d/3)*slc.Tc/result[3].PeriodSeconds;
        Eq(result[3].AccelerationG,exact);Assert.IsTrue(Math.Abs(exact-result[3].ExampleSampleAccelerationG)>1e-4,"The tabulated lookup must remain distinguishable from the exact ordinate.");
        var p=d.GetProperty("pendulum");var pendulum=IsolationSpectra.Pendulum(p.GetProperty("H5").GetDouble(),p.GetProperty("H4").GetDouble(),p.GetProperty("H8").GetDouble(),p.GetProperty("H9").GetDouble());
        Eq(pendulum.FrictionForceKn,p.GetProperty("H6").GetDouble());Eq(pendulum.EffectiveStiffnessKnPerMm,p.GetProperty("H11").GetDouble());Eq(pendulum.DampingPercent,p.GetProperty("H13").GetDouble()*100);Eq(pendulum.PeriodSeconds,p.GetProperty("H17").GetDouble());
    }
    [TestMethod]public void AnalyticalLimitsAndInvalidInputs(){
        var h=new HazardParameters(.2,2.5,.4);Assert.AreEqual(.2,NtcSpectra.ElasticHorizontal(h,1,1,1,1,0),1e-15);Assert.AreEqual(.5,NtcSpectra.ElasticHorizontal(h,1,1,1,1,.4),1e-15);Assert.AreEqual(.2,NtcSpectra.ElasticHorizontal(h,1,1,1,1,1),1e-15);Assert.AreEqual(.03,NtcSpectra.ElasticHorizontal(h,1,1,1,1,4),1e-15);
        Assert.AreEqual(.55,NtcSpectra.Damping(100),1e-15);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>NtcSpectra.Site(double.NaN,42,SiteInterpolation.RuledSurface));
        Assert.ThrowsException<ArgumentException>(()=>NtcSpectra.Site(2.35,48.85,SiteInterpolation.RuledSurface));
        Assert.ThrowsException<ArgumentException>(()=>NtcSpectra.Validate(new(Soil:"S1")));
        Assert.ThrowsException<ArgumentException>(()=>IsolationSpectra.Validate(new(1,5,10,1,1,[new("empty",0,1)])));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>IsolationSpectra.Pendulum(1,.05,0,1000));
    }
    [TestMethod]public void IsolationConventionalSpectrumStopsAtFourSeconds(){
        var states=NtcSpectra.Calculate(NtcSpectra.Site(12.4818,41.9109,SiteInterpolation.RuledSurface),new());
        // Independent M = 4000/pi² kN*s²/m for K = 1 kN/mm gives exactly Tis = 4 s.
        var boundary=new IsolationOptions(405.2847345693511,5,10,1,1,[new("single",1,1)]);
        var atLimit=IsolationSpectra.Calculate(states,boundary);
        foreach(var result in atLimit){
            Assert.AreEqual(4,result.PeriodSeconds,1e-14);
            Assert.IsTrue(result.Spectrum.All(point=>point.PeriodSeconds>=0&&point.PeriodSeconds<=4));
            Assert.IsTrue(RuntimeCompatibility.IsFinite(result.ExampleSampleAccelerationG));
        }
        var below=IsolationSpectra.Calculate(states,boundary with { MassKnS2PerM=400 });
        Assert.IsTrue(below.All(result=>result.PeriodSeconds<4));
        var above=Assert.ThrowsException<ArgumentException>(()=>IsolationSpectra.Calculate(states,boundary with { MassKnS2PerM=406 }));
        StringAssert.Contains(above.Message,"NTC 2018 §3.2.3.2");
        // A different stiffness factor can make only the ultimate states leave the domain.
        Assert.ThrowsException<ArgumentException>(()=>IsolationSpectra.Calculate(states,boundary with { UltimateStiffnessFactor=.99 }));
    }
    [TestMethod]public void ParallelSitesAreIndependent(){
        var first=NtcSpectra.Site(7.768,45.368,SiteInterpolation.RuledSurface);var expected=first.Periods[6].Parameters;
        Parallel.For(0,24,i=>{var s=NtcSpectra.Site(i%2==0?7.768:12.4818,i%2==0?45.368:41.9109,SiteInterpolation.RuledSurface);if(i%2==0)Assert.AreEqual(expected,s.Periods[6].Parameters);});
        Assert.AreEqual(8100,Cslp103Data.Towns.Count);
    }
}
