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

public partial class WeldedIBeamSpliceStandardTests
{
    internal static SteelVerificationResult Section(WeldedIBeamSpliceDetailInput d,SteelActions? actions=null,bool prepare=true,double fy=355)
    {
        var s=d.Plates.Bolts.Detail.Section;
        var model=new SteelSection(new SectionH(s.Height,s.Web.Thickness,s.TopFlange.Width,s.TopFlange.Thickness,
            s.BottomFlange.Width,s.BottomFlange.Thickness,"Standard splice parent",0),new SteelMaterial("S355 test",210000,fy,510),
            GPC.Model.Sections.Section.SectionTypes.Welded,GPC.Model.Sections.Section.FormedTypes.HotFinished);
        var input=new SteelVerificationInput(new(model,true),actions??WeldedIBeamSpliceParents.ToSteelActions(d.Plates.Bolts.Detail),new(d.Plates.Policy.Code.Plates));
        var plan=SteelMethodSelector.Select(input);
        var shear=d.Plates.Policy.Code.IsNtc?SteelShearBucklingAssessment.EvaluateNtcHWeb(input,"NTC compact parent web")
            :SteelShearBucklingAssessment.EvaluateEc3HWeb(input,1.2,"EC compact parent web eta1.2");
        return SteelVerificationPipeline.Execute(plan,prepare?new[]{shear}:Array.Empty<SteelShearBucklingAssessment>());
    }
    internal static SpliceLongitudinalFillets Welds(double throat=5,string? source="Continuous double fillets; qualified consumable and inspection; no transverse concentrated loads")
        =>new(throat,throat,300,FilletWeldSteelGrade.S355,FilletWeldSteelGrade.S355,FilletWeldSteelGrade.S355,source!);
    internal static WeldedIBeamSpliceParentInput Parents(WeldedIBeamSpliceDetailInput? detail=null,SteelVerificationResult? left=null,
        SteelVerificationResult? right=null,SpliceLongitudinalFillets? welds=null,string? source="Elastic global analysis; lateral restraint at splice; second-order applicability checked")
    {
        detail??=Input();left??=Section(detail);right??=Section(detail);
        return new(detail,left,right,welds??Welds(),source!);
    }
    [DataTestMethod][DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void ParentsReuseTheActualSteelResultsAndBindWelds(bool ntc,bool unequal)
    {
        var p=Parents(Input(ntc,Detail(unequalFlanges:unequal)));var r=WeldedIBeamSpliceParents.Verify(p);
        Assert.IsTrue(r.IsBound,string.Join(";",r.BindingDiagnostics));
        Assert.IsTrue(r.IsSectionAndWeldComplete,string.Join(";",p.LeftSection.Checks.Select(c=>c.Status+":"+c.DiagnosticCode)));
        Assert.IsFalse(r.HasFailedSectionOrWeld);Assert.AreSame(p.LeftSection,r.Input.LeftSection);
        Assert.AreEqual(ConnectionStatus.Passed,r.LongitudinalWelds.Status);Assert.IsFalse(r.HasBothMemberAssessments);
    }
    [TestMethod]
    public void ThePoleIsTransportedBeforeThePhysicalMomentSignIsConverted()
    {
        var d=Detail(new(2e5,1e5,0,0,0,8e7,applicationX:30,applicationY:20));
        var a=WeldedIBeamSpliceParents.ToSteelActions(d);
        Assert.AreEqual(-79e6,a.M1);Assert.AreEqual(1e5,a.V2);Assert.AreEqual(0,a.M2);Assert.AreEqual(0,a.V1);
    }
    [DataTestMethod][DataRow("moment")][DataRow("shear")][DataRow("normal")][DataRow("material")][DataRow("code")][DataRow("geometry")]
    public void AnUnrelatedPassingSectionCannotBeBorrowed(string flaw)
    {
        var d=Input();var a=WeldedIBeamSpliceParents.ToSteelActions(d.Plates.Bolts.Detail);
        var left=flaw=="code"?Section(Input(true)):flaw=="geometry"?Section(Input(detail:Detail(unequalFlanges:true))):
            Section(d,new(flaw=="normal"?-a.N:a.N,0,flaw=="shear"?-a.V2:a.V2,0,flaw=="moment"?-a.M1:a.M1,0),fy:flaw=="material"?275:355);
        var r=WeldedIBeamSpliceParents.Verify(Parents(d,left:left));Assert.IsFalse(r.IsBound);Assert.IsFalse(r.IsSectionAndWeldComplete);
    }
    [TestMethod]
    public void MissingShearPreparationAndARealFailureRemainVisible()
    {
        var d=Input(detail:Detail(new(3e7,1e5,0,0,0,50e6)));
        var r=WeldedIBeamSpliceParents.Verify(Parents(d,right:Section(d,prepare:false)));
        Assert.IsTrue(r.IsBound);Assert.IsFalse(r.IsSectionAndWeldComplete);Assert.IsTrue(r.HasFailedSectionOrWeld);
    }
    [TestMethod]
    public void ContinuousWeldShearFlowMatchesAnIndependentRectangleIntegral()
    {
        var r=WeldedIBeamSpliceParents.Verify(Parents());
        double inertia=2*(400*Math.Pow(30,3)/12+400*30*175*175)+16*Math.Pow(320,3)/12;
        double q=1e5*400*30*175/inertia;
        WeldedIBeamSpliceTests.Close(q,r.LongitudinalWelds.Trace.Single(t=>t.Name=="Top.ShearFlow").Value);
        WeldedIBeamSpliceTests.Close(Math.Sqrt(3)*q/10,r.LongitudinalWelds.Checks.Single(c=>c.Id=="Top.DirectionalEquivalent").Demand);
        WeldedIBeamSpliceTests.Close(510/(.9*1.25),r.LongitudinalWelds.Checks.Single(c=>c.Id=="Top.DirectionalEquivalent").Resistance);
    }
    [DataTestMethod][DataRow("throat")][DataRow("length")][DataRow("source")][DataRow("analysis")]
    public void WeldAndAnalysisPrerequisitesAreNotInferred(string flaw)
    {
        var w=flaw=="length"?new SpliceLongitudinalFillets(5,5,100,FilletWeldSteelGrade.S355,FilletWeldSteelGrade.S355,FilletWeldSteelGrade.S355,"WPS"):
            Welds(flaw=="throat"?6:5,flaw=="source"?null:"WPS");
        var r=WeldedIBeamSpliceParents.Verify(Parents(welds:w,source:flaw=="analysis"?null:"Analysis"));
        Assert.IsFalse(r.IsSectionAndWeldComplete);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ActualMemberResultsRemainSeparateFromTheLocalJointResistance(bool ntc)
    {
        var d=Input(ntc,Detail(new(-2e5,0,0,0,0,0)));var section=Section(d);var original=section.Plan.Input;
        var i=new SteelVerificationInput(original.Section,original.Actions,new(original.Code.SectionCode,d.Plates.Policy.GammaM1),new(4000,4000,4000));
        var a=SteelMemberAnalysis.Compression(i,"Same constant compression case on the whole member","Explicit curves for test",
            SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"Continuous torsional restraint in test",flexuralTorsionalExclusionSource:"Same restraint",memberClass:1,classificationSource:"Compact test geometry");
        var member=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a));
        Assert.IsTrue(member.IsComplete);
        var p=new WeldedIBeamSpliceParentInput(d,section,section,Welds(),"Member analysis with explicit restraint",member,member);
        var r=WeldedIBeamSpliceParents.Verify(p);
        Assert.IsTrue(r.HasBothMemberAssessments,string.Join(";",r.BindingDiagnostics));
        Assert.AreSame(member,r.Input.LeftMember);Assert.IsFalse(r.HasFailedMember);
    }
    [TestMethod]
    public void Class4GrossParentDoesNotQualifyThePerforatedSpliceByItself()
    {
        var detail=Detail(new(-1e5,0,0,0,0,0));var s=detail.Section;
        var slender=new WeldedIBeamSpliceSection(s.TopFlange,new(320,3,355,510),s.BottomFlange,8,8);
        detail=new(slender,detail.Top,detail.Bottom,detail.EachWebCover,detail.WebBolts,detail.WebCoverEnd,detail.ButtGap,detail.Actions);
        var r=WeldedIBeamSpliceParents.Verify(Parents(Input(detail:detail)));
        Assert.IsTrue(r.BindingDiagnostics.Any(x=>x.Contains("CLASS4_PERFORATED_PARENT")));
    }
}
