using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WebPanelAndPatchTests
{
    [TestMethod]
    public void PublishedJrcPanelCriticalStressIsAvailableInSharedSteelKernel()
    {
        var r=Ec3WebPanelShear.Calculate(2720,18,345,210000,1,1.1,1.2,8000);
        Assert.AreEqual(5.8024,r.KTau,1e-12);
        Assert.AreEqual(48.228,r.TauCritical,.01); // JRC/EUR22898EN plate-girder worked example, rounded MPa.
        Assert.AreEqual(.83/r.Slenderness,r.Chi,1e-12);
        Assert.IsTrue(r.Resistance<=r.PlasticResistance);
    }
    [DataTestMethod] [DataRow(0d,5.34)] [DataRow(500d,25.36)] [DataRow(1000d,9.34)] [DataRow(2000d,6.34)]
    public void PanelCoefficientsMatchAnnexA3(double panel,double expected)
        =>Assert.AreEqual(expected,Ec3WebPanelShear.Calculate(1000,10,355,210000,1,1,1.2,panel).KTau,1e-12);

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void QualifiedPanelFlowsThroughCommonClass4ShearAndBending(bool ntc)
    {
        var i=Input(ntc,1e5,1e7);var a=SteelShearBucklingAssessment.EvaluateTransverselyStiffenedHWeb(i,1600,false,ntc?1.05:1,1,"Qualified actual transverse stiffeners, attachments and supports; no longitudinal stiffeners");
        var result=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{a});
        Assert.IsTrue(result.IsComplete,string.Join(";",result.Checks.Select(c=>c.Message)));
        Assert.IsFalse(a.IsBucklingExcluded);Assert.IsTrue(a.BucklingResistance.HasValue);
        var shear=result.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.Shear2);
        Assert.AreEqual(1e5/a.BucklingResistance.Value,shear.Utilization!.Value,1e-12);
        var k=Ec3WebPanelShear.Calculate(1000,8,355,210000,ntc?1.05:1,ntc?1.05:1,1,1600);
        Assert.AreEqual(k.BucklingResistance,a.BucklingResistance.Value,1e-7);
    }

    [TestMethod]
    public void JrcLaunchingBridgePublishedPatchBenchmark()
    {
        // JRC/EUR22898EN(2007),15.1 p173-174/PDF187-188: one inclined web;
        // bf392 is already restricted for the box flange. Not a full bridge check.
        var r=Ec3WebPatchLoading.Calculate(WebPatchLoadType.OneFlangeReactedByShear,2846,22,392,26,420,420,210000,7400,500,1.1);
        Assert.AreEqual(6.30,r.Kf,.005);Assert.AreEqual(4.45e6,r.Fcr,.01e6);
        Assert.AreEqual(17.8,r.M1,.05);Assert.AreEqual(240,r.M2,.5);
        Assert.AreEqual(1380,r.LoadedLength,10);Assert.AreEqual(1.69,r.Slenderness,.01);
        Assert.AreEqual(.296,r.Chi,.002);Assert.AreEqual(3.43e6,r.Resistance,.02e6);
        // Same published case p179: F/FR=.60, M/MR=.985; individual limits matter.
        Assert.AreEqual((.60+.8*.985)/1.4,Ec3WebPatchLoading.Interaction(.60,.985),1e-12);
    }

    [DataTestMethod] [DataRow(WebPatchLoadType.OneFlangeReactedByShear,8d)] [DataRow(WebPatchLoadType.OppositeFlanges,5.5)]
    public void BothLoadTypologiesUsePublishedFigureAndIndependentStockyYieldLimit(WebPatchLoadType type,double kf)
    {
        // Stocky web: m1=10, ly=20+40*(1+sqrt10), chi=1, no m2 branch.
        var r=Ec3WebPatchLoading.Calculate(type,300,30,300,20,235,235,210000,300,20,1.05);
        double ly=60+40*Math.Sqrt(10);
        Assert.AreEqual(kf,r.Kf,1e-12);Assert.AreEqual(0d,r.M2);Assert.AreEqual(1d,r.Chi);
        Assert.AreEqual(ly*30*235/1.05,r.Resistance,1e-7);
    }

    [TestMethod]
    public void LoadedLengthCannotExceedPhysicalPanel()
    {
        var r=Ec3WebPatchLoading.Calculate(WebPatchLoadType.OneFlangeReactedByShear,1000,4,300,20,355,355,210000,100,50,1);
        Assert.AreEqual(100d,r.LoadedLength);Assert.IsTrue(r.EffectiveLength<=100);
    }

    [DataTestMethod] [DataRow(.2,1.2,1.2)] [DataRow(1.1,.1,1.1)] [DataRow(.8,.9,1.0857142857142859)] [DataRow(0d,0d,0d)]
    public void InteractionRetainsBothIndividualLimits(double patch,double normal,double expected)
        =>Assert.AreEqual(expected,Ec3WebPatchLoading.Interaction(patch,normal),1e-12);

    [DataTestMethod] [DataRow(false,false)] [DataRow(true,false)] [DataRow(false,true)] [DataRow(true,true)]
    public void ModelPatchAssessmentRetainsSameCaseSectionAndActualFlange(bool ntc,bool top)
    {
        var input=Input(ntc,0,1e7);var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(input));
        var p=SteelHWebPatchAssessment.Evaluate(section,WebPatchLoadType.OneFlangeReactedByShear,top,1e5,1600,100,ntc?1.05:1,"Same FEM state, vertical patch, laterally restrained flange and panel details independently assessed");
        Assert.AreSame(input,p.Input);Assert.AreSame(section,p.SectionResult);Assert.IsTrue(p.IsComplete);
        Assert.AreEqual(SteelExecutionStatus.Passed,p.Status);Assert.IsFalse(p.CanQualifyWholeMember);
        double normal=section.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.NormalResistance).Utilization!.Value;
        Assert.AreEqual(Ec3WebPatchLoading.Interaction(p.ForceUtilization,normal),p.Utilization!.Value,1e-12);
    }

    [TestMethod]
    public void PurePatchHasNoInventedBendingActionAndFailureIsStillEvaluated()
    {
        var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(Input(false,0,0)));
        var p=SteelHWebPatchAssessment.Evaluate(section,WebPatchLoadType.OneFlangeReactedByShear,false,1e8,1600,100,1,"Pure transverse force, same state");
        Assert.AreEqual(0d,p.Input.Actions.M1);Assert.IsTrue(p.IsComplete);
        Assert.AreEqual(SteelExecutionStatus.Failed,p.Status);Assert.AreEqual(p.ForceUtilization,p.Utilization!.Value);
    }

    [TestMethod]
    public void PatchResistanceHasCorrectLengthAndForceScaling()
    {
        var a=Ec3WebPatchLoading.Calculate(WebPatchLoadType.OneFlangeReactedByShear,1000,8,300,20,355,355,210000,1600,100,1.1);
        var b=Ec3WebPatchLoading.Calculate(WebPatchLoadType.OneFlangeReactedByShear,2000,16,600,40,355,355,210000,3200,200,1.1);
        Assert.AreEqual(a.Slenderness,b.Slenderness,1e-12);Assert.AreEqual(a.Chi,b.Chi,1e-12);
        Assert.AreEqual(2*a.LoadedLength,b.LoadedLength,1e-9);Assert.AreEqual(4*a.Resistance,b.Resistance,1e-7);
    }

    [TestMethod]
    public void RigidEndPostBenefitIsNeverUsedInNonRigidBranch()
    {
        var a=Ec3WebPanelShear.Calculate(1000,4,355,210000,1,1.1,1.2,4000,false);
        var b=Ec3WebPanelShear.Calculate(1000,4,355,210000,1,1.1,1.2,4000,true);
        Assert.IsTrue(a.Slenderness>1.08);Assert.AreEqual(.83/a.Slenderness,a.Chi,1e-12);
        Assert.AreEqual(1.37/(.7+b.Slenderness),b.Chi,1e-12);Assert.IsTrue(b.Resistance>a.Resistance);
    }

    [DataTestMethod] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidInteractionInputsCannotProduceAPassingRatio(double value)
        =>Assert.ThrowsException<ArgumentException>(()=>Ec3WebPatchLoading.Interaction(value,.2));

    [TestMethod]
    public void MissingShearPreparationCannotBeHiddenByPassingPatchResistance()
    {
        var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(Input(false,1e5,1e7)));
        var p=SteelHWebPatchAssessment.Evaluate(section,WebPatchLoadType.OneFlangeReactedByShear,true,1,1600,100,1,"Qualified patch geometry");
        Assert.IsFalse(p.IsComplete);Assert.AreEqual(SteelExecutionStatus.NotSupported,p.Status);Assert.IsNull(p.Utilization);
        var failed=SteelHWebPatchAssessment.Evaluate(section,WebPatchLoadType.OneFlangeReactedByShear,true,1e9,1600,100,1,"Same patch, overloaded");
        Assert.IsFalse(failed.IsComplete);Assert.AreEqual(SteelExecutionStatus.Failed,failed.Status);
    }

    [DataTestMethod] [DataRow("source")] [DataRow("factor")] [DataRow("panel")] [DataRow("bearing")] [DataRow("type")]
    public void UnqualifiedPatchInputsAreRejected(string error)
    {
        var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(Input(true,0,1e7)));
        Assert.ThrowsException<ArgumentException>(()=> {
            try {SteelHWebPatchAssessment.Evaluate(section,error=="type"?(WebPatchLoadType)33:WebPatchLoadType.OneFlangeReactedByShear,true,
                1e5,error=="panel"?0:1600,error=="bearing"?1001:100,error=="factor"?1:1.05,error=="source"?"":"same-case evidence");}
            catch(ArgumentOutOfRangeException ex){throw new ArgumentException(ex.Message);}
        });
    }
    internal static SteelVerificationInput Input(bool ntc,double shear,double moment)
        =>new(new SteelSectionInput(new SteelSection(new SectionH(1040,8,300,20,300,20,"welded panel"),
                new SteelMaterial("S355",210000,355,510),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished),true),
            new SteelActions(0,0,shear,0,moment,0),ntc?SteelDesignContext.Ntc2018():new SteelDesignContext(SectionResistanceCode.Eurocode3(1,1.25,"explicit EC3"),1),
            new SteelMemberInput(6000));
}
