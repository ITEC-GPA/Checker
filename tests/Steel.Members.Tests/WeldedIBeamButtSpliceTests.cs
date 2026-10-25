using GPC.Checkers.Steel.Connections;
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
public class WeldedIBeamButtSpliceTests
{
    static ConnectionDesignCode Code(bool ntc=false)=>ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EN test profile");
    static SteelVerificationResult AssessSection(ConnectionDesignCode code,SteelActions? actions=null,double fy=355,double fu=510,
        bool prepareShear=true,double height=380,bool holes=false,Section.SectionTypes production=Section.SectionTypes.Welded)
    {
        var model=new SteelSection(new SectionH(height,16,400,30,400,30,"Compact welded I",0),
            new SteelMaterial("Actual plate steel",210000,fy,fu),production,Section.FormedTypes.HotFinished);
        var input=new SteelVerificationInput(new(model,!holes),actions??new(1e6,0,0,0,0,0),new(code.Plates));
        // This fixture: web320/16=20; outstands192/30=6.4. Both Class1 at fy<=355 even in uniform compression.
        var classification=new SteelSectionClassification(input,1,"Independent Table5.2 uniform-compression limits for this compact fixture");
        var plan=SteelMethodSelector.Select(input,SteelCheckScope.Section,classification);
        var shear=code.IsNtc?SteelShearBucklingAssessment.EvaluateNtcHWeb(input,"NTC compact web")
            :SteelShearBucklingAssessment.EvaluateEc3HWeb(input,1,"Explicit eta1, compact web");
        return SteelVerificationPipeline.Execute(plan,prepareShear?new[]{shear}:Array.Empty<SteelShearBucklingAssessment>());
    }
    static SpliceButtWeldDetail Weld(double thickness,double length,SpliceWeldPenetration penetration=SpliceWeldPenetration.Full,
        double fy=355,double fu=510,string? procedure="Qualified WPS/WPQR reference",string? inspection="Preparation and inspection plan reference")
        =>new(penetration,thickness,length,fy,fu,procedure!,inspection!);
    static WeldedIBeamButtSpliceInput Input(bool ntc=false,SteelVerificationResult? left=null,SteelVerificationResult? right=null,
        SpliceButtWeldDetail? top=null,SpliceButtWeldDetail? web=null,SpliceButtWeldDetail? bottom=null,string? source="One ULS",
        string? alignment="Identical aligned sections; complete fusion through seam intersections")
    {
        var c=Code(ntc);left??=AssessSection(c);right??=AssessSection(c);
        return new(c,left,right,top??Weld(30,400),web??Weld(16,320),bottom??Weld(30,400),source!,alignment!);
    }
    static void Close(double expected,double actual)=>WeldedIBeamSpliceTests.Close(expected,actual);

    [DataTestMethod][DataRow(false,1d)][DataRow(true,1.05)]
    public void FullPenetrationReusesTheConnectedSectionTensionResistance(bool ntc,double gamma)
    {
        var r=WeldedIBeamButtSplice.Verify(Input(ntc));Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.SeamPrerequisites.Diagnostics));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.IsFalse(r.CertifiesWeldExecution);Assert.IsFalse(r.CoversFatigue);Assert.IsFalse(r.CoversMemberStability);
        // Gross area=2*400*30+320*16=29120 mm2. No throat factor or weld reinforcement.
        var n=r.LeftSection.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.NormalResistance);
        Close(29120*355/gamma,n.Resistance!.Value);
        Assert.AreSame(r.Input.LeftSection,r.LeftSection);Assert.AreSame(r.Input.RightSection,r.RightSection);
        Assert.IsTrue(r.SeamPrerequisites.Checks.All(c=>c.Reference.Contains(ntc?"NTC2018":"EN1993")));
    }
    [DataTestMethod][DataRow(false,1d)][DataRow(true,1.05)]
    public void PureMomentUsesTheExistingSectionRuleAndIndependentPlasticModulus(bool ntc,double gamma)
    {
        var c=Code(ntc);var section=AssessSection(c,new(0,0,0,0,300e6,0));
        var r=WeldedIBeamButtSplice.Verify(Input(ntc,left:section,right:section));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        // Rectangle integrals about PNA: 2*400*30*175 + 16*320^2/4 = 4609600 mm3.
        Close(4609600*355/gamma,section.Checks.Single(x=>x.Decision.Target==SteelCheckTarget.NormalResistance).Resistance!.Value);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void WeakerConnectedMaterialGovernsWithoutAveragingOrWeldStrengthBonus(bool ntc)
    {
        var c=Code(ntc);var a=new SteelActions(8.5e6,0,0,0,0,0);
        var left=AssessSection(c,a);var right=AssessSection(c,a,275,430);
        var r=WeldedIBeamButtSplice.Verify(Input(ntc,left,right));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.IsStaticResistanceComplete);Assert.IsTrue(r.HasFailedSection);
        Assert.AreEqual(SteelExecutionStatus.Passed,left.Checks.Single(x=>x.Decision.Target==SteelCheckTarget.NormalResistance).Status);
        Assert.AreEqual(SteelExecutionStatus.Failed,right.Checks.Single(x=>x.Decision.Target==SteelCheckTarget.NormalResistance).Status);
    }
    [DataTestMethod][DataRow(false,1d)][DataRow(false,-1d)][DataRow(true,1d)][DataRow(true,-1d)]
    public void SameCaseCombinedActionsAndMomentReversalRemainThoseOfTheCore(bool ntc,double sign)
    {
        var c=Code(ntc);var a=new SteelActions(-1e6,0,sign*100000,0,sign*200e6,0);var section=AssessSection(c,a);
        var r=WeldedIBeamButtSplice.Verify(Input(ntc,section,section));
        Assert.IsTrue(section.IsComplete,string.Join(";",section.Checks.Select(x=>x.DiagnosticCode)));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Assert.AreSame(a,r.LeftSection.Plan.Input.Actions);
        Assert.AreSame(section.Checks,r.RightSection.Checks);
    }
    [TestMethod]
    public void MissingShearPreparationNeverBecomesAFullStrengthJointPass()
    {
        var c=Code();var a=new SteelActions(0,0,100000,0,200e6,0);var section=AssessSection(c,a,prepareShear:false);
        var r=WeldedIBeamButtSplice.Verify(Input(left:section,right:section));
        Assert.IsFalse(section.IsComplete);Assert.IsFalse(r.IsStaticResistanceComplete);Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
    [TestMethod]
    public void KnownSectionFailureRemainsVisibleWhenTheOtherSideNeedsPreparation()
    {
        var c=Code();var a=new SteelActions(50e6,0,50000,0,10e6,0);
        var r=WeldedIBeamButtSplice.Verify(Input(left:AssessSection(c,a),right:AssessSection(c,a,prepareShear:false)));
        Assert.IsTrue(r.HasFailedSection);Assert.IsFalse(r.IsStaticResistanceComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
    [DataTestMethod][DataRow("partial")][DataRow("depth")][DataRow("length")][DataRow("procedure")][DataRow("inspection")]
    public void UnqualifiedOrIncompleteSeamDoesNotPassEvenIfBothSectionsPass(string flaw)
    {
        var seam=Weld(flaw=="depth"?15:16,flaw=="length"?319:320,
            flaw=="partial"?SpliceWeldPenetration.Partial:SpliceWeldPenetration.Full,
            procedure:flaw=="procedure"?null:"WPS",inspection:flaw=="inspection"?null:"NDT plan");
        var r=WeldedIBeamButtSplice.Verify(Input(web:seam));Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsTrue(r.LeftSection.IsComplete);
    }
    [DataTestMethod][DataRow(350d,510d)][DataRow(355d,500d)]
    public void UndermatchingConsumableFailsRatherThanIncreasingTheParentResistance(double fy,double fu)
    {
        var r=WeldedIBeamButtSplice.Verify(Input(top:Weld(30,400,fy:fy,fu:fu)));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.IsStaticResistanceComplete);
        Assert.IsTrue(r.SeamPrerequisites.Checks.Any(c=>!c.Passed));
    }
    [DataTestMethod][DataRow("actions")][DataRow("geometry")][DataRow("code")][DataRow("holes")][DataRow("rolled")]
    public void BindingCannotMixDifferentCasesSectionsOrPolicies(string flaw)
    {
        var c=Code();var other=AssessSection(flaw=="code"?Code(true):c,
            actions:flaw=="actions"?new(2e6,0,0,0,0,0):null,height:flaw=="geometry"?382:380,
            holes:flaw=="holes",production:flaw=="rolled"?Section.SectionTypes.Rolled:Section.SectionTypes.Welded);
        var r=WeldedIBeamButtSplice.Verify(Input(right:other));Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsFalse(r.IsStaticResistanceComplete);
    }
    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void OutOfPlaneActionsAreNotSilentlyDropped(int component)
    {
        var a=new SteelActions(1e6,component==0?1:0,0,component==1?1:0,0,component==2?1:0);
        var s=AssessSection(Code(),a);var r=WeldedIBeamButtSplice.Verify(Input(left:s,right:s));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsStaticResistanceComplete);
    }
    [TestMethod]
    public void SourceAndNullContractsRemainExplicit()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamButtSplice.Verify(null!));
        Assert.ThrowsException<ArgumentException>(()=>Weld(30,400,fy:510,fu:355));
        Assert.AreEqual(ConnectionStatus.NotSupported,WeldedIBeamButtSplice.Verify(Input(source:null)).Status);
        Assert.AreEqual(ConnectionStatus.NotSupported,WeldedIBeamButtSplice.Verify(Input(alignment:null)).Status);
    }
}
