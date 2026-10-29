using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleAngleCleatColumnWebStandardTests
{
    // ISO4017 M20: across flats 30, across corners 32.95; dm=(30+32.95)/2 rounded down to 31.5.
    private const double Dm=31.5;
    private static ConnectionRolledISection Section(double h,double b,double tw,double tf,double r,double fy=275)
        =>new(h,b,tw,tf,r,2*b*tf+(h-2*tf)*tw+4*(1-Math.PI/4)*r*r,fy,430);
    private static DoubleAngleCleatComponentInput Cleats(double v,bool ntc,double beamWidth=200,bool notched=false)
        =>new(ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EC recommended factors"),new(90,10,275,430),11,
            Section(420,beamWidth,10,16,15),new(20,22,245,ConnectionBoltGrade.Class8_8,true),4,new(0,-v,0,0,0,0),true,notched,"SLE hole clearance checked");
    private static DoubleAngleCleatColumnWebStandardInput Input(double v=100000,bool ntc=false,ConnectionRolledISection? column=null,double beamWidth=200,
        bool notched=false,string? pinned="Pinned beam ends; column designed for N and V*tw/2",string? tying="Tying resisted by the floor slab ties (separate assessment)",double dm=Dm)
    {
        var col=column??Section(400,300,12,20,18);double axial=-200000;
        return new(Cleats(v,ntc,beamWidth,notched),col,265,305,500,500,new(0,axial,0,0,0,0,0,300,0),
            new(0,axial-v,0,0,0,-v*col.WebThickness/2,0,-300,0),dm,"Actual fabricated geometry; no distributed column load between cuts",
            "One actual FEM combination, same +Y cut convention",pinned!,tying!);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void StandardDetailOnAColumnWebIsACompleteOrdinaryShearVerification(bool ntc)
    {
        var r=DoubleAngleCleatColumnWebStandard.Verify(Input(ntc:ntc));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Assert.IsTrue(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Checks.Any(c=>c.Id.StartsWith("Cleats."))&&r.Checks.Any(c=>c.Id.StartsWith("Support."))&&r.Checks.Any(c=>c.Id.StartsWith("Procedure.")));
        // EN1993-1-8 Table3.4: Ft,Rd=0.9*800*245/1.25; Bp,Rd=0.6*pi*dm*t*fu/1.25 (gammaM2 1.25 also for NTC).
        double ft=.9*800*245/1.25;
        var cleat=r.ProcedureChecks.Single(c=>c.Id=="PunchingHierarchy.CleatLeg");var web=r.ProcedureChecks.Single(c=>c.Id=="PunchingHierarchy.ColumnWeb");
        Assert.AreEqual(141120,ft,1e-9);Assert.AreEqual(ft,cleat.Demand,1e-9);Assert.AreEqual(ft,web.Demand,1e-9);
        Assert.AreEqual(.6*Math.PI*Dm*10*430/1.25,cleat.Resistance,1e-9);Assert.AreEqual(.6*Math.PI*Dm*12*430/1.25,web.Resistance,1e-9);
        Assert.AreEqual(ConnectionCheckRole.Applicability,web.Role);
        Assert.IsTrue(r.ProcedureChecks.Where(c=>c.Id.StartsWith("NominalPin.")||c.Id.StartsWith("Restraint.")).All(c=>c.Role==ConnectionCheckRole.Detailing&&c.Passed));
        // Load utilization: largest resistance utilization of the reused cleat and web components.
        double expected=r.Cleats.ComponentAssessment.Checks.Concat(r.SupportChecks).Where(c=>c.Role==ConnectionCheckRole.Resistance).Max(c=>c.Utilization);
        Assert.AreEqual(expected,r.Utilization!.Value,0);
    }

    [TestMethod]
    public void ReusedComponentChecksAreIdenticalToTheComponentRoutes()
    {
        var input=Input();var r=DoubleAngleCleatColumnWebStandard.Verify(input);
        var cleats=DoubleAngleCleatComponents.Verify(input.Cleats);
        var web=DoubleAngleCleatColumnWeb.Verify(new DoubleAngleCleatColumnWebInput(input.Cleats,input.Column,265,305,500,500,input.UpperCut,input.LowerCut,"a","b"));
        Assert.AreEqual(cleats.ComponentAssessment.Checks.Count+web.Support.Checks.Count+r.ProcedureChecks.Count,r.Checks.Count);
        foreach(var c in cleats.ComponentAssessment.Checks)
        {
            var s=r.Checks.Single(x=>x.Id=="Cleats."+c.Id);Assert.AreEqual(c.Demand,s.Demand,0);Assert.AreEqual(c.Resistance,s.Resistance,0);Assert.AreEqual(c.Role,s.Role);
        }
        foreach(var c in web.Support.Checks)
        {
            var s=r.Checks.Single(x=>x.Id=="Support."+c.Id);Assert.AreEqual(c.Demand,s.Demand,0);Assert.AreEqual(c.Resistance,s.Resistance,0);
        }
    }

    [TestMethod]
    public void ThinColumnWebPunchingHierarchyIsAMissingRequirementNotAFailure()
    {
        // 250x250x6.5x14 r13: clear web 196 > cleats 190 > beam flange 180; Bp,web
        // 0.6*pi*31.5*6.5*430/1.25=132766 N < Ft,Rd 141120 N.
        var r=DoubleAngleCleatColumnWebStandard.Verify(Input(column:Section(250,250,6.5,14,13),beamWidth:180));
        var web=r.ProcedureChecks.Single(c=>c.Id=="PunchingHierarchy.ColumnWeb");
        Assert.AreEqual(.6*Math.PI*Dm*6.5*430/1.25,web.Resistance,1e-9);Assert.IsFalse(web.Passed);
        Assert.IsTrue(r.ProcedureChecks.Single(c=>c.Id=="PunchingHierarchy.CleatLeg").Passed);
        Assert.IsFalse(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsNull(r.Utilization);
        CollectionAssert.Contains(r.MissingRequirements.ToArray(),"PUNCHING_MAY_PRECEDE_BOLT_TENSION_IN_COLUMNWEB:ACTUAL_TENSION_AND_PRYING_MODEL_REQUIRED");
        // At 7.5 mm the hierarchy holds: 0.6*pi*31.5*7.5*430/1.25=153192 N.
        var thicker=DoubleAngleCleatColumnWebStandard.Verify(Input(column:Section(250,250,7.5,14,13),beamWidth:180));
        Assert.IsTrue(thicker.IsStaticResistanceComplete,string.Join(";",thicker.MissingRequirements));
    }

    [DataTestMethod][DataRow("pinned")][DataRow("tying")][DataRow("notched")][DataRow("hardware")]
    public void MissingSourcesOrFieldAreRequirementsNotPasses(string missing)
    {
        var r=DoubleAngleCleatColumnWebStandard.Verify(Input(pinned:missing=="pinned"?" ":"Pinned",tying:missing=="tying"?null:"Separate tying",
            notched:missing=="notched",dm:missing=="hardware"?20:Dm));
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsFalse(r.CanQualifyWholeJoint);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Count>0);
    }

    [TestMethod]
    public void OverloadIsAFailureAndStaysVisible()
    {
        var r=DoubleAngleCleatColumnWebStandard.Verify(Input(v:900000));
        Assert.IsTrue(r.HasFailedComponent);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.Checks.Any(c=>!c.Passed&&c.Role==ConnectionCheckRole.Resistance));
    }

    [TestMethod]
    public void CatalogSeparatesTheStandardShearRouteFromTheComponentFamily()
    {
        var c=ConnectionCapabilityCatalog.Get("DoubleAngleCleatColumnWebShear");
        Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification,c.Level);
        Assert.AreEqual("DoubleAngleCleatColumnWebStandard.Verify",c.RecommendedEntryPoint);
        Assert.IsTrue(c.MissingMechanisms.Any(x=>x.Contains("tying")));
        Assert.AreEqual(ConnectionCapabilityLevel.ComponentsOnly,ConnectionCapabilityCatalog.Get("DoubleAngleCleat").Level);
    }
}
