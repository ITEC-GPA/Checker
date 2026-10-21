using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Status=GPC.Checkers.Steel.Sections.SectionResistanceStatus;
using Check=GPC.Checkers.Steel.Sections.SectionResistanceVerifier;

namespace Steel.Members.Tests;

[TestClass]
public class SectionResistanceContractTests
{
    private static SectionResistanceCode Code(bool ntc) => ntc ? SectionResistanceCode.Ntc2018()
        : SectionResistanceCode.Eurocode3(1, 1.25, "CEN recommended values, explicitly supplied");

    public static IEnumerable<object[]> Decisions =>
        from ntc in new[] { false, true }
        from mechanism in new[] { "tension", "compression", "bending+", "bending-", "shear+", "shear-" }
        from factor in new[] { 0d, .5, .99, 1.1 }
        select new object[] { ntc, mechanism, factor };

    [DataTestMethod,DynamicData(nameof(Decisions))]
    public void IsolatedMechanismHasAnExplicitDecisionAndTrace(bool ntc,string mechanism,double factor)
    {
        // Independent arithmetic using A=1000mm2,Wpl=100000mm3,Av=600mm2,fy235MPa.
        // Existing section-oracle tests continue to qualify geometry/class/modulus mapping.
        double gamma=ntc?1.05:1;
        double resistance=mechanism.StartsWith("bending")?23500000/gamma:
            mechanism.StartsWith("shear")?81406.38795573723/gamma:235000/gamma;
        double action=factor*resistance;
        var c=Code(ntc);
        SectionResistanceResult r=mechanism switch {
            "tension"=>Check.Tension(c,action,1000,235,360,true),
            "compression"=>Check.Compression(c,-action,1000,235,1),
            "bending+"=>Check.Bending(c,action,100000,80000,235,1),
            "bending-"=>Check.Bending(c,-action,100000,80000,235,1),
            "shear+"=>Check.Shear(c,action,600,235),
            _=>Check.Shear(c,-action,600,235)};
        Assert.AreEqual(factor<=1?Status.Passed:Status.Failed,r.Status);
        Assert.IsTrue(r.IsEvaluated);
        Assert.AreEqual(resistance,r.Resistance!.Value,resistance*2e-14);
        Assert.AreEqual(factor,r.Utilization!.Value,2e-14);
        Assert.AreSame(c,r.Code);
        Assert.IsFalse(string.IsNullOrEmpty(r.Reference));
        Assert.IsFalse(string.IsNullOrEmpty(r.Scope));
        Assert.IsNull(r.DiagnosticCode);
        Assert.AreEqual(mechanism.StartsWith("bending")?"Nmm":"N",r.ResistanceUnit);
        Assert.AreEqual(gamma,r.Trace.Single(t=>t.Name=="GammaM0").Value);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ExactNormalAndBendingLimitIsSatisfied(bool ntc)
    {
        var c=Code(ntc);
        double n=235000/(ntc?1.05:1),m=23500000/(ntc?1.05:1);
        Assert.AreEqual(Status.Passed,Check.Tension(c,n,1000,235,360,true).Status);
        Assert.AreEqual(Status.Passed,Check.Compression(c,-n,1000,235,1).Status);
        Assert.AreEqual(Status.Passed,Check.Bending(c,m,100000,null,235,1).Status);
    }

    [DataTestMethod]
    [DataRow(false,1)] [DataRow(false,2)] [DataRow(false,3)]
    [DataRow(true,1)] [DataRow(true,2)] [DataRow(true,3)]
    public void ClassSelectsOnlyItsRequiredModulus(bool ntc,int cl)
    {
        var r=Check.Bending(Code(ntc),1e6,cl==3?null:100000,cl==3?80000:null,235,cl);
        Assert.AreEqual(Status.Passed,r.Status);
        Assert.AreEqual((cl==3?18800000:23500000)/(ntc?1.05:1),r.Resistance!.Value,1e-7);
        Assert.AreEqual(cl==3?"WelMin":"Wpl",r.Trace.Single(t=>t.Unit=="mm3").Name);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void TensionRequiresAnExplicitHoleDecisionAndNetAreaWhenPerforated(bool ntc)
    {
        var c=Code(ntc);
        Unevaluated(Check.Tension(c,10000,1000,235,360,null),Status.MissingInput,"NetSection.DeclarationRequired");
        Unevaluated(Check.Tension(c,10000,1000,235,360,false),Status.MissingInput,"NetArea.Required");
        Unevaluated(Check.Tension(c,10000,1000,235,360,true,900),Status.InvalidInput,"NetSection.ConflictingInputs");
        Unevaluated(Check.Tension(c,10000,1000,235,360,false,1001),Status.InvalidInput,"NetArea.ExceedsGross");
        var r=Check.Tension(c,100000,1000,235,360,false,500);
        Assert.AreEqual(Status.Passed,r.Status);
        Assert.AreEqual(129600,r.Resistance!.Value,1e-9); // .9*500*360/1.25
        Assert.AreEqual(129600,r.Trace.Single(v=>v.Name=="NuRd").Value,1e-9);
        Assert.AreEqual(1.25,r.Trace.Single(v=>v.Name=="GammaM2").Value);
    }

    [TestMethod]
    public void MissingInputsDoNotBecomeZeroActionsOrAvailableCapacities()
    {
        var c=Code(false);
        Unevaluated(Check.Shear(null!,1,100,235),Status.MissingInput,"Code.Required");
        Unevaluated(Check.Shear(c,null,100,235),Status.MissingInput,"DesignAction.Required");
        Unevaluated(Check.Shear(c,1,null,235),Status.MissingInput,"Av.Required");
        Unevaluated(Check.Shear(c,1,100,null),Status.MissingInput,"Fy.Required");
        Unevaluated(Check.Compression(c,-1,1000,235,null),Status.MissingInput,"SectionClass.Required");
    }

    [DataTestMethod]
    [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(0d)] [DataRow(-1d)]
    public void InvalidResistanceInputsAreNotStructuralFailures(double value)
    {
        var r=Check.Shear(Code(false),100,value,235);
        Assert.AreEqual(Status.InvalidInput,r.Status);
        Assert.IsNull(r.Resistance);Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void AxialSignAndMaterialConsistencyAreEnforced()
    {
        var c=Code(false);
        Unevaluated(Check.Tension(c,-1,1000,235,360,true),Status.InvalidInput,"DesignAction.Sign");
        Unevaluated(Check.Compression(c,1,1000,235,1),Status.InvalidInput,"DesignAction.Sign");
        Unevaluated(Check.Tension(c,1,1000,355,235,true),Status.InvalidInput,"Fu.BelowFy");
        Unevaluated(Check.Bending(c,1,1000,800,235,0),Status.InvalidInput,"SectionClass.Invalid");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void Class4IsUnsupportedWithoutFakeCapacity(bool ntc)
    {
        Unevaluated(Check.Bending(Code(ntc),1,1000,800,235,4),Status.NotSupported,"Class4.EffectivePropertiesRequired");
        Unevaluated(Check.Compression(Code(ntc),-1,1000,235,4),Status.NotSupported,"Class4.EffectivePropertiesRequired");
    }

    [DataTestMethod]
    [DataRow(1e300,1e300)] [DataRow(1e-300,1e-300)]
    public void NumericalRangeFailureHasNoNaNRatio(double area,double fy)
        =>Unevaluated(Check.Compression(Code(false),-1,area,fy,1),Status.CalculationError,"NumericalRange");

    [TestMethod]
    public void RatioOverflowIsAlsoAnExecutionError()
        =>Unevaluated(Check.Compression(Code(false),-double.MaxValue,1e-300,1,1),Status.CalculationError,"NumericalRange");

    [TestMethod]
    public void ResultsRemainIndependentAfterAnErrorAndTheirTraceIsReadOnly()
    {
        var first=Check.Shear(Code(false),100,600,235);
        Check.Shear(Code(true),double.NaN,600,235);
        var second=Check.Shear(Code(true),100,600,235);
        Assert.AreEqual(1d,first.Trace.Single(v=>v.Name=="GammaM0").Value);
        Assert.AreEqual(1.05,second.Trace.Single(v=>v.Name=="GammaM0").Value);
        Assert.ThrowsException<NotSupportedException>(()=>((IList<SectionResistanceValue>)first.Trace).Clear());
    }

    [TestMethod]
    public void CodeContextRequiresFiniteFactorsAndRecordsTheDeclaredParameterSet()
    {
        foreach(double bad in new[]{0d,-1d,double.NaN,double.PositiveInfinity})
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(()=>SectionResistanceCode.Eurocode3(bad,1.25,"declared"));
            Assert.ThrowsException<ArgumentOutOfRangeException>(()=>SectionResistanceCode.Eurocode3(1,bad,"declared"));
        }
        Assert.ThrowsException<ArgumentException>(()=>SectionResistanceCode.Eurocode3(1,1.25," "));
        var c=SectionResistanceCode.Eurocode3(1.2,1.3,"Custom supplied factors");
        var r=Check.Compression(c,-1000,1000,235,1);
        Assert.AreEqual(235000/1.2,r.Resistance!.Value,1e-8);
        Assert.AreEqual("Custom supplied factors",r.Code.ParameterSet);
        Assert.AreEqual("2005",c.Edition);
    }

    [TestMethod]
    public void ExistingNtcFacadeAndNewContractShareThePrimitiveButKeepTheirApiSemantics()
    {
        var c=Code(true);
        Assert.AreEqual(Ntc2018SectionResistance.Tension(1000,355,490),Check.Tension(c,1000,1000,355,490,true).Resistance);
        Assert.AreEqual(Ntc2018SectionResistance.Tension(1000,355,490,750),Check.Tension(c,1000,1000,355,490,false,750).Resistance);
        Assert.AreEqual(Ntc2018SectionResistance.Compression(1000,355,2),Check.Compression(c,-1000,1000,355,2).Resistance);
        Assert.AreEqual(Ntc2018SectionResistance.Bending(100000,80000,355,3),Check.Bending(c,-1e6,100000,80000,355,3).Resistance);
        Assert.AreEqual(Ntc2018SectionResistance.Shear(600,355),Check.Shear(c,-1000,600,355).Resistance);
        Assert.ThrowsException<NotSupportedException>(()=>Ntc2018SectionResistance.Compression(1000,355,4));
        Assert.AreEqual(Status.NotSupported,Check.Compression(c,-1000,1000,355,4).Status);
    }

    private static void Unevaluated(SectionResistanceResult r,Status status,string diagnostic)
    {
        Assert.AreEqual(status,r.Status);Assert.AreEqual(diagnostic,r.DiagnosticCode);
        Assert.IsFalse(r.IsEvaluated);Assert.IsNull(r.Resistance);Assert.IsNull(r.Utilization);
        Assert.IsFalse(string.IsNullOrWhiteSpace(r.Message));
    }
}
