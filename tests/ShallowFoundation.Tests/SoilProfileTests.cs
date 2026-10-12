using GPC.Checkers.Geotechnics.Foundations;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShallowFoundation.Tests;

[TestClass]
public sealed class SoilProfileTests
{
    static Soil Soil(double gamma=18,double sat=20,double phi=30,double cu=75)=>new("Terreno",gamma*1e-6,sat*1e-6,phi*Math.PI/180,0,"Dati di prova indipendenti",cu*.001);
    static SoilProfile Profile(double origin=0)=>new("Sondaggio",new[]{new SoilLayer(Soil(17,19),origin,origin-500),new SoilLayer(Soil(18,20),origin-500,origin-1000),new SoilLayer(Soil(19,21,35),origin-1000,origin-11000)},"Prova",origin-750);
    [TestMethod] public void UsesCanonicalModelAndIndependentEffectiveCoverIntegral()
    {
        var x=new ShallowFoundationInput{SoilProfile=Profile()};var r=ShallowFoundationBearing.Calculate(x);
        // 0.5*17 + 0.25*18 + 0.25*(20-9.81) = 15.5475 kPa.
        Assert.AreEqual(15.5475,r.Overburden*1000,1e-10);Assert.AreEqual(35,r.AdoptedFrictionAngle*180/Math.PI,1e-10);
        Assert.AreEqual(11.19,r.BearingUnitWeight*1e6,1e-10);
        Assert.AreEqual(Math.PI/6,x.FrictionAngle,1e-15);Assert.IsFalse(x.WaterEnabled); // no mutation
    }
    [TestMethod] public void UndrainedUsesTotalCoverAndCu()
    {
        var r=ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile(),Condition=FoundationSoilCondition.Undrained});
        Assert.AreEqual(18,r.Overburden*1000,1e-10);Assert.AreEqual(.075,r.AdoptedCohesion,1e-12);Assert.AreEqual(0,r.AdoptedFrictionAngle);
    }
    [TestMethod] public void ElevationDatumDoesNotChangeTheResult()
    {
        var a=ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile()});var b=ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile(132500)});
        Assert.AreEqual(a.Resistance,b.Resistance,1e-8);Assert.AreEqual(a.Overburden,b.Overburden,1e-12);
    }
    [TestMethod] public void DifferentPropertiesBelowBaseAreNotAveraged()
    {
        Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile(),Depth=500}));
        Assert.ThrowsException<NotSupportedException>(()=>ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile(),HomogeneousHalfSpace=false}));
    }
    [TestMethod] public void InvalidProfileExtentAndMissingUndrainedStrengthAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(new(){SoilProfile=Profile(),Depth=11000}));
        var soil=new Soil("Drenato",18e-6,20e-6,Math.PI/6,0,"Prova");
        var profile=new SoilProfile("Sondaggio",new[]{new SoilLayer(soil,0,-10000)},"Prova");
        Assert.ThrowsException<ArgumentException>(()=>ShallowFoundationBearing.Calculate(new(){SoilProfile=profile,Condition=FoundationSoilCondition.Undrained}));
    }
    [TestMethod] public void IdenticalBearingLayersCanHaveDifferentNames()
    {
        var soil=Soil();var profile=new SoilProfile("Sondaggio",new[]{new SoilLayer(soil,0,-2000),new SoilLayer(Soil(),-2000,-12000)},"Prova");
        var legacy=ShallowFoundationBearing.Calculate(new());var actual=ShallowFoundationBearing.Calculate(new(){SoilProfile=profile});
        Assert.AreEqual(legacy.Resistance,actual.Resistance,1e-8);
    }
}
