using GPC.Examples;
using GPC.Model.Compatibility;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results.Locations;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

[TestClass]
public class SteelScalarIntegrationTest
{
    private static BeamActionInput Input(bool webCheck=false)
    {
        // Large explicit fillets make c/t class2 while hw/t still requires a
        // shear-buckling check; the two geometric widths must remain distinct.
        var section=webCheck
            ?new SteelSection(new SectionH(300,6,160,10,160,10,"explicit large fillets",60),new SteelMaterial("S460",210000,460,550))
            :new SteelSection(new SectionH(300,12,150,18,150,18,"H300"),SteelMaterialEN1993Data.S355);
        var model=MixedModelFactory.Create(m=>{
            var beam=m.BeamElements[250]; beam.BeamProperty=section; beam.Assignments.Sections.Clear();
            beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=0,End=1,Property=section});
        });
        var sample=Verification.BeamSample(model.BeamElements[250],"synthetic-static","P+",.5,SectionSide.Unspecified);
        var input=BeamActionPreparation.Prepare(model,250,sample,"scalar integration").Input;
        input.Forces.N=input.Forces.V1=input.Forces.V2=input.Forces.T=input.Forces.M1=input.Forces.M2=0;
        return input;
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidUsedEtaCannotProduceAResistanceVerdict(double eta)
    {
        var r=new SteelMaterialChecker(new StandardEN1993p11{NShearBucklingLowGradeOfSteel=eta},"2005")
            .CreateSession().Verify(Input(),CheckMechanism.Shear,default);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Code=="InvalidShearBucklingEta"));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ClearWebHeightGatesShearBucklingEvenWhenFilletsGiveClassTwo(bool bending)
    {
        var input=Input(true); if(bending) input.Forces.M1=100;
        var r=new SteelMaterialChecker(new StandardEN1993p11(),"2005",null,bending).CreateSession()
            .Verify(input,bending?CheckMechanism.UlsBiaxialSection:CheckMechanism.Shear,default);
        // fy460: c/t=160/6=26.667 <=38sqrt(235/460)=27.16;
        // hw/t=280/6=46.667 >72sqrt(235/460)/1.2=42.89.
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);
        Assert.IsTrue(r.Diagnostics.Any(d=>d.Code=="SteelShearBucklingNotQualified"));
        Assert.IsNull(r.Utilization);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ScalarPreparationRetainsTraceAndIgnoresUnusedEta(bool bending)
    {
        var code=new StandardEN1993p11{NShearBucklingHighGradeOfSteel=double.NaN};
        var input=Input(); if(bending) input.Forces.M1=100;
        var r=new SteelMaterialChecker(code,"2005",null,bending).CreateSession()
            .Verify(input,bending?CheckMechanism.UlsBiaxialSection:CheckMechanism.Shear,default);
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome);
        Assert.AreEqual(264d,r.Details.Trace.Single(t=>t.Key=="Web.ClearHeight").Value);
        Assert.AreEqual(12d,r.Details.Trace.Single(t=>t.Key=="Web.Thickness").Value);
        Assert.AreEqual(1.2,r.Details.Trace.Single(t=>t.Key=="Web.Eta").Value);
    }
}
