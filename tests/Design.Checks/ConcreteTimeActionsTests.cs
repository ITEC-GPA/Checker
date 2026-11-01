using System.Text.Json;
using GPC.Design.Actions.TimeEffects;
using GPC.Model.Actions.Climate;
using GPC.Model.Actions.TimeEffects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cement=GPC.Model.Materials.ConcreteMaterialEuropeanCommon.CementTypes;

namespace Design.Checks;

[TestClass]
public sealed class ConcreteTimeActionsTests
{
    private static TimeEffectsInput Input()=>new(){AreaMm2=200000,DryingPerimeterMm=2000,TimeSteps=60,Observations=new(){new(){Name="28",AgeDays=28},new(){Name="365",AgeDays=365}}};
    [TestMethod]
    public void NtcDryingUsesNegativeTabulatedValuesAndInterpolatedKh()
    {
        // Table 11.2.Va: -.38 per mille at fck40/RH60; kh=.85 at h0=200.
        var law=new ConcreteDeformationLaw(ClimateStandard.Ntc2018,40,Cement.ClassN,60,200,7);
        Assert.AreEqual(.85,law.Kh,1e-15);Assert.AreEqual(-.000323,law.DryingInfinity,1e-15);
        Assert.AreEqual(-.00015154565899002772,law.DryingAt(107),1e-15);
        Assert.AreEqual(-4.89716171909544e-05,law.AutogenousAt(28),1e-15);Assert.AreEqual(0,law.DryingAt(7));
        law=new(ClimateStandard.Ntc2018,30,Cement.ClassN,70,150,7);
        Assert.AreEqual(-.0003525*.925,law.DryingInfinity,1e-15); // bilinear four table vertices
    }
    [TestMethod]
    public void EurocodeAnnexBShrinkageUsesPositiveFcmInTheExponent()
    {
        // Independent Python arithmetic from B.11/B.12, not the historical shrinkage API.
        var law=new ConcreteDeformationLaw(ClimateStandard.EurocodeFirstGeneration,30,Cement.ClassN,70,200,7);
        Assert.AreEqual(-.00030778012447328824,law.DryingInfinity,1e-15);
        Assert.AreEqual(32836.56803133079,law.Ecm28Mpa,1e-8);
        Assert.AreEqual(30,law.FckAt(365));Assert.AreEqual(1,law.EcmAt(28)/law.Ecm28Mpa,1e-14);
        Assert.IsTrue(law.FckAt(7)<30&&law.FckAt(7)>0);Assert.IsTrue(law.EcmAt(7)<law.Ecm28Mpa);
    }
    [DataTestMethod]
    [DataRow(Cement.ClassS)] [DataRow(Cement.ClassN)] [DataRow(Cement.ClassR)]
    public void SaturatedConcreteHasNoDryingButRetainsAutogenousShrinkage(Cement cement)
    {
        var law=new ConcreteDeformationLaw(ClimateStandard.EurocodeFirstGeneration,30,cement,100,500,7);
        Assert.AreEqual(0,law.DryingInfinity);Assert.AreEqual(.7,law.Kh);Assert.IsTrue(law.AutogenousAt(365)<0);
    }
    [TestMethod]
    public void SingleCompressionIncrementMatchesIndependentAnnexBArithmetic()
    {
        var i=Input();i.IncludeShrinkage=false;var r=ConcreteTimeActions.Calculate(i);var p=r.History.Single(p=>p.AgeDays==365);
        Assert.AreEqual(-.00015226926258643362,p.FreeElasticStrain,1e-15);
        Assert.AreEqual(-.00021062934781867153,p.FreeCreepStrain,1e-15);
        Assert.AreEqual(-5,p.StressMpa);Assert.AreEqual(-1000,p.AxialForceKn);Assert.AreEqual(0,p.SupportForceKn);
        Assert.AreEqual(p.FreeStrain,p.ActualStrain);Assert.IsTrue(r.WithinLinearRange&&r.Converged);
        p=r.History.Single(p=>p.AgeDays==28);Assert.AreEqual(0,p.FreeCreepStrain);Assert.AreEqual(-5,p.StressMpa);
    }
    [TestMethod]
    public void LoadingAndUnloadingSuperposeWithoutInventingRecoveryBeforeUnloading()
    {
        var a=Input();a.IncludeShrinkage=false;var first=ConcreteTimeActions.Calculate(a);
        a.StressEvents=new(){new(){AgeDays=90,IncrementMpa=5}};var second=ConcreteTimeActions.Calculate(a);
        a.StressEvents.Add(new(){AgeDays=28,IncrementMpa=-5});var combined=ConcreteTimeActions.Calculate(a);
        var p=combined.History.Single(p=>p.AgeDays==365);
        Assert.AreEqual(first.History.Last().ActualStrain+second.History.Last().ActualStrain,p.ActualStrain,1e-14);Assert.AreEqual(0,p.StressMpa);
        Assert.AreEqual(-5,combined.History.Single(p=>p.AgeDays==28).StressMpa);Assert.IsTrue(p.FreeCreepStrain<0);
    }
    [TestMethod]
    public void FreeShrinkageIsMeasuredFromTheReferenceConfiguration()
    {
        var i=Input();i.StressEvents.Clear();var r=ConcreteTimeActions.Calculate(i);
        Assert.AreEqual(0,r.History.First().RelativeShrinkage);Assert.AreEqual(0,r.History.First().ActualDisplacementMm);
        Assert.IsTrue(r.History.First().AutogenousShrinkage<0);Assert.IsTrue(r.History.Last().ActualDisplacementMm<0);Assert.IsTrue(r.History.All(p=>p.StressMpa==0));
    }
    [TestMethod]
    public void FixedEndsSatisfyCompatibilityAtEveryStepAndCreepRelaxesRestraint()
    {
        var i=Input();i.StressEvents.Clear();i.Restraint=AxialRestraint.FixedDistance;
        var creep=ConcreteTimeActions.Calculate(i);Assert.IsTrue(creep.Converged);
        foreach(var p in creep.History){Assert.AreEqual(0,p.ActualDisplacementMm,1e-9);Assert.AreEqual(p.AxialForceKn,p.SupportForceKn,1e-10);}
        i.IncludeCreep=false;var elastic=ConcreteTimeActions.Calculate(i);
        Assert.IsTrue(creep.History.Last().StressMpa>0&&creep.History.Last().StressMpa<elastic.History.Last().StressMpa);
        Assert.IsFalse(creep.WithinLinearRange);Assert.IsTrue(creep.Summary.Notes[0].Contains("ATTENZIONE"));
    }
    [TestMethod]
    public void EndSpringSatisfiesNodeEquilibriumAndLimitsToTheFreeBar()
    {
        var i=Input();i.Restraint=AxialRestraint.EndSpring;i.Displacements=new(){new(){AgeDays=90,DisplacementMm=.5}};
        var r=ConcreteTimeActions.Calculate(i);
        foreach(var p in r.History)Assert.AreEqual(i.EndSpringKnMm*(p.PrescribedDisplacementMm-p.ActualDisplacementMm),p.SupportForceKn,1e-7);
        i.EndSpringKnMm=1e-7;r=ConcreteTimeActions.Calculate(i);
        Assert.AreEqual(r.History.Last().FreeStrain,r.History.Last().ActualStrain,1e-9);
    }
    [TestMethod]
    public void ImposedDisplacementAndExternalForceHaveDistinctRolesAtFixedEnds()
    {
        var i=Input();i.IncludeShrinkage=false;i.IncludeCreep=false;i.Restraint=AxialRestraint.FixedDistance;i.Displacements=new(){new(){AgeDays=28,DisplacementMm=.1}};
        var r=ConcreteTimeActions.Calculate(i);
        foreach(var p in r.History){Assert.AreEqual(p.PrescribedDisplacementMm,p.ActualDisplacementMm,1e-10);Assert.AreEqual(p.AxialForceKn-p.ExternalStressMpa*i.AreaMm2/1000,p.SupportForceKn,1e-10);}
        Assert.AreEqual(.1,r.History.Last().ActualDisplacementMm,1e-10);
    }
    [TestMethod]
    public void ElasticBarAndSpringMatchTwoSpringsInParallel()
    {
        var i=Input();i.ReferenceAgeDays=28;i.Observations=new(){new(){AgeDays=28,Name="Applicazione"}};i.Restraint=AxialRestraint.EndSpring;i.IncludeShrinkage=false;i.IncludeCreep=false;
        var p=ConcreteTimeActions.Calculate(i).History.Single();
        double barStiffness=32836.56803133079*200000/10000/1000;
        Assert.AreEqual(-1000/(barStiffness+50),p.ActualDisplacementMm,1e-12);
        Assert.AreEqual(-5*barStiffness/(barStiffness+50),p.StressMpa,1e-12);
    }
    [TestMethod]
    public void TimeRefinementConvergesAndInputIsNeverMutated()
    {
        var i=Input();i.Restraint=AxialRestraint.EndSpring;i.StressEvents.Clear();
        string json=JsonSerializer.Serialize(i);var r=ConcreteTimeActions.Calculate(i);Assert.AreEqual(json,JsonSerializer.Serialize(i));
        i.TimeSteps*=2;var refined=ConcreteTimeActions.Calculate(JsonSerializer.Deserialize<TimeEffectsInput>(JsonSerializer.Serialize(i))!);
        Assert.IsTrue(refined.MaxStressDiscretizationMpa<r.MaxStressDiscretizationMpa);Assert.IsTrue(r.Converged&&refined.Converged);
        Assert.AreEqual(r.History.Last().StressMpa,refined.History.Last().StressMpa,.02);
    }
    [TestMethod]
    public void InvalidDomainsAndIncompatibleHistoriesAreRejected()
    {
        var i=Input();i.FckMpa=90;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.AreaMm2=1000;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.RelativeHumidityPercent=39;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.ReferenceAgeDays=3;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.Displacements.Add(new());Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.Observations.Clear();Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.StressEvents[0].IncrementMpa=double.NaN;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
        i=Input();i.Standard=ClimateStandard.EurocodeFirstGeneration;Assert.ThrowsException<ArgumentException>(()=>ConcreteTimeActions.Calculate(i));
    }
}
