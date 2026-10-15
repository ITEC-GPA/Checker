using GPC.Checkers.Geotechnics.Foundations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShallowFoundation.Tests;

[TestClass]
public sealed class BearingTests
{
    static void Close(double expected, double actual, double relative = 1e-10) => Assert.AreEqual(expected, actual, relative * Math.Max(1, Math.Abs(expected)));
    static ShallowFoundationInput Input(FoundationBearingMethod method = FoundationBearingMethod.Eurocode7) => new() { Method=method, Profile=FoundationDesignProfile.Characteristic };

    [TestMethod]
    public void PublishedBearingFactors_UsaceTable44()
    {
        // Published rounded numbers, independent of this implementation; absolute tolerance covers rounding only.
        foreach (var (phi,nc,nq,m,h,v) in new[] { (10,8.34,2.47,.37,.39,1.22),(20,14.83,6.40,2.87,2.95,5.39),(30,30.14,18.40,15.67,15.07,22.40),(40,75.31,64.19,93.69,79.54,109.41) })
            foreach (var (method,ng) in new[] {(FoundationBearingMethod.Meyerhof,m),(FoundationBearingMethod.Hansen,h),(FoundationBearingMethod.Vesic,v)})
            {
                var x=Input(method); x.FrictionAngle=phi*Math.PI/180;
                var r=ShallowFoundationBearing.Calculate(x);
                Assert.AreEqual(nc,r.Cohesion.N,.011); Assert.AreEqual(nq,r.Surcharge.N,.011); Assert.AreEqual(ng,r.Weight.N,.011);
            }
    }

    [TestMethod]
    public void Ec7IndependentWorkedCase()
    {
        // B=2m L=3m D=1m phi=30°, c=0, gamma=18kN/m³, N=600kN.
        // Independent Python expression: Nq=exp(pi/sqrt(3))*3; Ng=2*(Nq-1)/sqrt(3).
        var x=Input(); var r=ShallowFoundationBearing.Calculate(x);
        Close(18.401122218708668,r.Surcharge.N);
        Close(20.093085194346052,r.Weight.N);
        Close(730.9673600475911,r.UltimatePressure*1000);
        Close(4385.804160285547,r.Resistance/1000);
        x.Profile=FoundationDesignProfile.Ntc2018;
        var ntc=ShallowFoundationBearing.Calculate(x);
        Close(2.3,ntc.ResistanceFactor); Close(r.Resistance/2.3,ntc.Resistance);
        Close(600000/ntc.Resistance,ntc.Utilization!.Value);
    }

    [TestMethod]
    public void UndrainedClosedFormsAndAdditiveHansen()
    {
        var x=Input(); x.Condition=FoundationSoilCondition.Undrained;
        double nc=Math.PI+2, cu=.075, q=.018, s=1+.2*2/3;
        Close(cu*nc*s+q,ShallowFoundationBearing.Calculate(x).UltimatePressure);
        x.Method=FoundationBearingMethod.Hansen;
        var hansen=ShallowFoundationBearing.Calculate(x);
        Close(cu*nc*(1+.2*2/3+.4*.5)+q,hansen.UltimatePressure);
        // Additive corrections in their own fields; the multiplicative ones stay neutral.
        Assert.IsTrue(hansen.Cohesion.Additive);
        Close(.2*2/3,hansen.Cohesion.AdditiveShape);Close(.4*.5,hansen.Cohesion.AdditiveDepth);Close(0,hansen.Cohesion.AdditiveInclination);
        Close(1,hansen.Cohesion.Shape);Close(1,hansen.Cohesion.Depth);Close(1,hansen.Cohesion.Inclination);
        Close(1+.2*2/3+.4*.5,hansen.Cohesion.AdditiveFactor);
        Close(cu*nc*hansen.Cohesion.AdditiveFactor,hansen.Cohesion.Pressure);
        x.Method=FoundationBearingMethod.Vesic;
        var vesic=ShallowFoundationBearing.Calculate(x);
        Close(cu*nc*s*(1+.4*.5)+q,vesic.UltimatePressure);
        Assert.IsFalse(vesic.Cohesion.Additive);Close(1,vesic.Cohesion.AdditiveFactor);
        x.Method=FoundationBearingMethod.Terzaghi;x.Shape=FoundationShape.Strip;
        Close(cu*(1+1.5*Math.PI)+q,ShallowFoundationBearing.Calculate(x).UltimatePressure);
    }

    [TestMethod]
    public void AxisSwapAndMomentSignInvariance()
    {
        var x=Input();x.HorizontalX=40000;x.HorizontalY=20000;x.MomentX=40e6;x.MomentY=120e6;
        var r=ShallowFoundationBearing.Calculate(x);
        (x.Width,x.Length)=(x.Length,x.Width);(x.MomentX,x.MomentY)=(x.MomentY,x.MomentX);(x.HorizontalX,x.HorizontalY)=(x.HorizontalY,x.HorizontalX);
        Close(r.Resistance,ShallowFoundationBearing.Calculate(x).Resistance);
        x.MomentX=-x.MomentX;x.HorizontalX=-x.HorizontalX;
        Close(r.Resistance,ShallowFoundationBearing.Calculate(x).Resistance);
    }

    [TestMethod]
    public void WaterPressureAndNoSilentClipping()
    {
        var x=Input();x.WaterEnabled=true;x.WaterDepth=0;
        var r=ShallowFoundationBearing.Calculate(x);
        Close(.01019,r.Overburden);Close(10.19e-6,r.BearingUnitWeight);
        x.Condition=FoundationSoilCondition.Undrained;Close(.020,ShallowFoundationBearing.Calculate(x).Overburden);
        x.Condition=FoundationSoilCondition.Drained;
        x.WaterDepth=x.Depth+x.Width;var dry=ShallowFoundationBearing.Calculate(x);
        Close(.018,dry.Overburden);Close(18e-6,dry.BearingUnitWeight);
        // Beyond the domain: explicit null capacity with flag and reason, never a clipped positive value.
        x.MomentY=x.Normal*x.Width/2;
        Exhausted(ShallowFoundationBearing.Calculate(x));
        x=Input();x.Condition=FoundationSoilCondition.Undrained;x.HorizontalX=x.Width*x.Length*x.UndrainedStrength+1;
        Exhausted(ShallowFoundationBearing.Calculate(x));
    }

    static void Exhausted(ShallowFoundationResult r)
    {
        Assert.IsTrue(r.DomainExhausted);
        Assert.AreEqual(0,r.Resistance);Assert.AreEqual(0,r.UltimatePressure);Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Warnings.Any(w=>w.Contains("Capacità portante assunta nulla")));
    }

    [TestMethod]
    public void ActionsBeyondTheDomainAreNotVerified()
    {
        // Califfo §3.4.1: no inclination factor may be negative, otherwise the bearing capacity is null.
        var x=Input();x.Profile=FoundationDesignProfile.Ntc2018;x.MomentY=x.Normal*x.Width/2;
        var r=ShallowFoundationBearing.Calculate(x);
        Exhausted(r);Assert.AreEqual("Non verificata",r.Status);Assert.AreEqual(0,r.EffectiveArea);Assert.IsNull(r.DemandPressure);
        x=Input();x.Profile=FoundationDesignProfile.Ntc2018;x.HorizontalX=x.Normal;
        r=ShallowFoundationBearing.Calculate(x);
        Exhausted(r);Assert.AreEqual("Non verificata",r.Status);
        Assert.AreEqual(0,r.Surcharge.Inclination);Assert.AreEqual(0,r.Weight.Inclination);Assert.AreEqual(0,r.Cohesion.Inclination);
        // phi=30°, c=0, m=(2+2/3)/(1+2/3)=1.6: H/N=0.9 gives iq=0.1^m < 1/Nq, so ic < 0 while the fraction is still below one.
        x=Input();x.HorizontalX=.9*x.Normal;
        r=ShallowFoundationBearing.Calculate(x);
        Exhausted(r);Assert.AreEqual("Confronto caratteristico",r.Status);Close(Math.Pow(.1,1.6),r.Surcharge.Inclination);
        x.HorizontalX=.8*x.Normal;
        r=ShallowFoundationBearing.Calculate(x);
        Assert.IsFalse(r.DomainExhausted);Assert.IsTrue(r.Resistance>0);Assert.IsTrue(r.Cohesion.Inclination>0);
        // Vesić undrained: m H / (A cu Nc) > 1 with m = 1.6.
        x=Input(FoundationBearingMethod.Vesic);x.Condition=FoundationSoilCondition.Undrained;x.HorizontalX=1.5e6;
        Exhausted(ShallowFoundationBearing.Calculate(x));
    }

    [TestMethod]
    public void NearZeroAngleUsesStableLimit()
    {
        var x=Input();x.Cohesion=.02;
        foreach(double phi in new[]{1e-12,1e-10,1e-8})
        { x.FrictionAngle=phi;var r=ShallowFoundationBearing.Calculate(x);Close(Math.PI+2,r.Cohesion.N,1e-7);Assert.IsTrue(double.IsFinite(r.Resistance)); }
        x.FrictionAngle=0;
        Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
    }

    [TestMethod]
    public void TerzaghiTableAndMethodRestrictions()
    {
        Close(19.7,ShallowFoundationBearing.TerzaghiGamma(30));Close(23.8,ShallowFoundationBearing.TerzaghiGamma(31));
        Close(780.1,ShallowFoundationBearing.TerzaghiGamma(48));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>ShallowFoundationBearing.TerzaghiGamma(51));
        var x=Input(FoundationBearingMethod.Hansen);x.HorizontalX=1;
        Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input();x.Shape=FoundationShape.Circle;x.MomentY=1;
        Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input();x.RoughBase=false;
        Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(x));
    }

    [TestMethod]
    public void ResultantOutsideTheBaseGivesASerializableResult()
    {
        // Resultant on and beyond the edge: null capacity, and the contact graphics must not leak non-finite numbers.
        foreach (double factor in new[]{.5,.6})
        {
            var x=Input();x.Profile=FoundationDesignProfile.Ntc2018;x.MomentY=factor*x.Normal*x.Width;
            var r=ShallowFoundationBearing.Calculate(x);
            Exhausted(r);Assert.AreEqual("Non verificata",r.Status);
            System.Text.Json.JsonSerializer.Serialize(r);
            // No compressive contact can balance a resultant on or beyond the edge: no graphics, reason in the warnings.
            Assert.IsNull(r.Graphics);Assert.IsTrue(r.Warnings.Any(w=>w.StartsWith("Distribuzione delle pressioni non convergente")));
        }
    }

    [TestMethod]
    public void DensityReductionDomainAndContinuity()
    {
        var x=Input();var r=ShallowFoundationBearing.Calculate(x);
        x.Compression=FoundationCompression.VesicDensity;x.RelativeDensity=2d/3;
        Close(r.Resistance,ShallowFoundationBearing.Calculate(x).Resistance);
        x.RelativeDensity=0;Assert.IsTrue(ShallowFoundationBearing.Calculate(x).Resistance<r.Resistance);
        x.RelativeDensity=.7;Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
    }

    [TestMethod]
    public void InclinedEc7IntermediateFactorsAndEffectiveArea()
    {
        var x=Input();x.MomentY=120e6;x.HorizontalX=60000;x.Cohesion=.01;
        var r=ShallowFoundationBearing.Calculate(x);
        Close(1600,r.EffectiveWidth);Close(4800000,r.EffectiveArea);
        // Independent expression in SI units: cot30=sqrt3, m=(2+B'/L')/(1+B'/L').
        double m=38d/23, a=1-60/(600+4.8*10*Math.Sqrt(3));
        double iq=Math.Pow(a,m),nq=3*Math.Exp(Math.PI/Math.Sqrt(3));
        Close(iq,r.Surcharge.Inclination);Close(Math.Pow(a,m+1),r.Weight.Inclination);
        Close(iq-(1-iq)/(nq-1),r.Cohesion.Inclination);
        x.Condition=FoundationSoilCondition.Undrained;x.HorizontalX=4.8e6*.075;
        Close(.5,ShallowFoundationBearing.Calculate(x).Cohesion.Inclination);
    }

    [TestMethod]
    public void MeyerhofBelowTenDegreesIsInterpolatedAndDeclared()
    {
        // USACE EM 1110-1-1905 Table 4-3: for 0 < phi <= 10° linear interpolation between phi = 0 (factor 1) and phi = 10°.
        var x=Input(FoundationBearingMethod.Meyerhof);x.FrictionAngle=5*Math.PI/180;
        var r=ShallowFoundationBearing.Calculate(x);
        double kp10=Math.Pow(Math.Tan(50*Math.PI/180),2);
        Close(1+.5*.1*kp10*2/3,r.Surcharge.Shape);Close(r.Surcharge.Shape,r.Weight.Shape);
        Close(1+.5*.1*Math.Sqrt(kp10)*.5,r.Surcharge.Depth);Close(r.Surcharge.Depth,r.Weight.Depth);
        Assert.IsTrue(r.Warnings.Any(w=>w.StartsWith("Meyerhof con φ′ < 10°")));
        x.FrictionAngle=10*Math.PI/180;
        Assert.IsFalse(ShallowFoundationBearing.Calculate(x).Warnings.Any(w=>w.StartsWith("Meyerhof con φ′ < 10°")));
    }

    [TestMethod]
    public void MeyerhofUndrainedAppliesIqToTheSurcharge()
    {
        // GEO-1, user decision of 15/10/2026 (Meyerhof 1963 / USACE EM 1110-1-1905 Tab. 4-3 over the Califfo manual (3.16)):
        // ic = iq = (1 − θ/90°)² for any φ; with φ = 0 only Nq = sq = dq = 1. Strip B = 2 m, D = 3 m, q = 18·3 = 54 kPa, cu = 75 kPa,
        // N = 300 kN, H = 150 kN: θ = atan(0.5), i = 0.49679; qult = 75·(π + 2)·1.3·i + 54·i = 249.04 + 26.83 = 275.87 kPa
        // (independent: scratchpad\bug\geo-meyerhof-iq\indip.py, 275.870; before + q: 303.04 kPa).
        var x=Input(FoundationBearingMethod.Meyerhof);x.Shape=FoundationShape.Strip;x.Condition=FoundationSoilCondition.Undrained;
        x.Depth=3000;x.Normal=300000;x.HorizontalX=150000;
        var r=ShallowFoundationBearing.Calculate(x);
        double i=Math.Pow(1-Math.Atan(.5)/(Math.PI/2),2);
        Close(.4967892261934240,i,1e-12);Close(i,r.Cohesion.Inclination);Close(i,r.Surcharge.Inclination);
        Close(54e-3*i,r.Surcharge.Pressure);Close((75*(Math.PI+2)*1.3+54)*i/1000,r.UltimatePressure);
        Assert.AreEqual(275.870,r.UltimatePressure*1000,.001);
        // Continuity with the drained branch at φ′ → 0 and c′ = cu (the verifier: 275.871 kPa).
        var d=Input(FoundationBearingMethod.Meyerhof);d.Shape=FoundationShape.Strip;d.Depth=3000;d.Normal=300000;d.HorizontalX=150000;
        d.FrictionAngle=1e-4*Math.PI/180;d.Cohesion=.075;
        Close(r.UltimatePressure,ShallowFoundationBearing.Calculate(d).UltimatePressure,1e-4);
        // Normal load: i = 1, the surcharge term stays q; the other methods keep + q also with H.
        x.HorizontalX=0;var n=ShallowFoundationBearing.Calculate(x);Close(1,n.Surcharge.Inclination);Close(54e-3,n.Surcharge.Pressure);
        var v=Input(FoundationBearingMethod.Vesic);v.Shape=FoundationShape.Strip;v.Condition=FoundationSoilCondition.Undrained;v.Depth=3000;v.Normal=300000;v.HorizontalX=40000;
        var vr=ShallowFoundationBearing.Calculate(v);Close(1,vr.Surcharge.Inclination);Close(54e-3,vr.Surcharge.Pressure);
    }

    [TestMethod]
    public void RejectNonFiniteAndOutOfDomainInputs()
    {
        var x=Input();x.Width=double.NaN;Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input();x.Normal=0;Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input();x.Width=1e200;x.Length=1e200;Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input();x.Shape=FoundationShape.Strip;x.HorizontalY=1;Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(x));
        x=Input(FoundationBearingMethod.Meyerhof);x.HorizontalX=1;Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(x));
    }
}
