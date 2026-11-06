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
public class RhsWebResistanceTests
{
    private const string Supports="Support stiffeners, attachments and force introduction checked separately for this case";
    private static SectionRHS Shape(bool rounded=true,double h=400,double b=200,double t=6)
        => rounded?new SectionRHSRoundedCorners(h,b,t,2*t,t,"RHS"):new SectionRHS(h,b,t,t,t,t,"box");
    private static SteelVerificationInput Input(Section? shape=null,bool ntc=false,double fy=420,
        double v1=0,double v2=1000,double n=0,double gm1=1,bool? unperforated=true,
        Section.FormedTypes formed=Section.FormedTypes.HotFinished,double torque=0,double e=210000)
    {
        var section=new SteelSection(shape??Shape(),new SteelMaterial("explicit",e,fy,550),Section.SectionTypes.Rolled,formed);
        var code=new SteelDesignContext(ntc?SectionResistanceCode.Ntc2018():SectionResistanceCode.Eurocode3(1,1.25,"explicit factors"),gm1);
        return new(new SteelSectionInput(section,unperforated),new(n,v1,v2,torque,0,0),code);
    }
    private static SteelShearBucklingAssessment Assess(SteelVerificationInput input,int axis=2)
        => SteelShearBucklingAssessment.EvaluateNonRigidRhsWebs(input,axis,input.Code.GammaM1!.Value,Supports);

    // SSAB Domex Tube,2016,Example2.12 p108, equations2.79/2.80 pp104-105.
    // https://www.ssab.com/-/media/files/en/infra/news-and-articles/ssab-domex-tube-structural-hollow-sections-en-1993-handbook.pdf
    // The published cold-formed product is an independent numerical benchmark of
    // the web equation and section geometry, not a cold-formed pipeline certificate.
    [DataTestMethod][DataRow(420d,948500d)][DataRow(355d,871900d)]
    public void SsabPublishedRhs400By200By6MatchesBothSteelGrades(double fy,double published)
    {
        double area=400d*200-(388d*188)-(4-Math.PI)*(144-36);
        double lambda=382d/(86.4*6*Math.Sqrt(235/fy));
        // Example2.12 first rounds lambda to .9851, then chi to .8426;
        // this intermediate rounding explains the ~0.1 kN difference.
        double printedChi=fy==420?Math.Round(.83/Math.Round(lambda,4),4):.83/lambda;
        double reference=6963d*400/600*printedChi*fy/Math.Sqrt(3);
        var i=Input(fy:fy);var r=Assess(i);
        double exact=area*400/600*(.83/lambda)*fy/Math.Sqrt(3);
        Assert.AreEqual(published,reference,50,"Published rounding to 0.1 kN");
        Assert.AreEqual(exact,r.BucklingResistance!.Value,1e-7);
        Assert.AreEqual(published,r.BucklingResistance.Value,50+Math.Abs(exact-reference));
        Assert.AreEqual(area,i.Section.Area,1e-8);
        Assert.AreEqual(382d,r.Trace.Single(v=>v.Name=="BucklingPanelWidth").Value);
        Assert.IsFalse(r.IsBucklingExcluded);Assert.IsTrue(r.RhsWebsOnly);
        Assert.IsFalse(r.ConservativePlateNormalInteraction);
    }

    public static IEnumerable<object[]> AxisCases=>from axis in new[]{1,2} from sign in new[]{-1d,1d}
        from ntc in new[]{false,true} from rounded in new[]{false,true} select new object[]{axis,sign,ntc,rounded};
    [DataTestMethod,DynamicData(nameof(AxisCases))]
    public void BothAxesAndCodesUseActualDemandAndIndependentResistance(int axis,double sign,bool ntc,bool rounded)
    {
        const double h=800,b=600,t=6,fy=355;
        double gm1=ntc?1.10:1.2,gm0=ntc?1.05:1;
        double area=2*t*(h+b-2*t)-(rounded?(4-Math.PI)*3*t*t:0);
        double d=axis==1?b:h,c=d-(rounded?3:2)*t,av=area*d/(b+h);
        double expected=Math.Min(av*fy/(Math.Sqrt(3)*gm0),av*.83*86.4*t*Math.Sqrt(235/fy)/c*fy/(Math.Sqrt(3)*gm1));
        foreach(double ratio in new[]{.7,1.1}) {
            var i=Input(Shape(rounded,h,b,t),ntc,fy,axis==1?sign*ratio*expected:0,axis==2?sign*ratio*expected:0,gm1:gm1);
            var a=Assess(i,axis);var result=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{a});
            var shear=result.Checks.Single(x=>x.Decision.Target==(axis==1?SteelCheckTarget.Shear1:SteelCheckTarget.Shear2));
            Assert.IsTrue(result.IsComplete,shear.Message);
            Assert.AreEqual(expected,shear.Resistance!.Value,1e-7);
            Assert.AreEqual(ratio,shear.Utilization!.Value,1e-12);
            Assert.AreEqual(ratio<1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,shear.Status);
            StringAssert.Contains(shear.Scope,"RHS web-only");
        }
    }

    [TestMethod]
    public void StockyWallsAreCappedByPlasticResistanceAndNotBypassAssessments()
    {
        var i=Input(Shape(true,100,80,10),gm1:.9);
        var r=Assess(i);
        double area=100d*80-80d*60-(4-Math.PI)*(400-100);
        Assert.AreEqual(area*100/180*420/Math.Sqrt(3),r.BucklingResistance!.Value,1e-8);
        Assert.IsFalse(r.IsBucklingExcluded);
    }

    [TestMethod]
    public void SmallerCornerCannotShortenTheActualFlatWeb()
    {
        var i=Input(new SectionRHSRoundedCorners(400,200,6,7,1,"small concentric radius"));
        Assert.AreEqual(386d,Assess(i).Trace.Single(v=>v.Name=="BucklingPanelWidth").Value);
    }

    [DataTestMethod][DataRow(1)][DataRow(2)]
    public void WeldedBoxUsesOnlyTheTwoClearPanelsInTheBucklingArea(int axis)
    {
        var source=Input(Shape(false,800,600));
        var s=new SteelSection(Shape(false,800,600),new SteelMaterial("explicit",210000,420,550),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished);
        var i=new SteelVerificationInput(new SteelSectionInput(s,true),axis==1?new(0,1000,0,0,0,0):source.Actions,source.Code);
        var r=Assess(i,axis);double c=axis==1?588:788;
        double expected=2*.83*86.4*36*Math.Sqrt(235d/420)*420/Math.Sqrt(3);
        Assert.AreEqual(2*c*6,r.Trace.Single(x=>x.Name=="ShearArea").Value);
        Assert.AreEqual(expected,r.BucklingResistance!.Value,1e-7);
    }

    [TestMethod]
    public void CombinedShearsAndCompressedClass4NeverBecomeCompleteFromWebResistances()
    {
        var both=Input(v1:1000);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(both),new[]{Assess(both,1),Assess(both)});
        Assert.IsFalse(r.IsComplete);
        Assert.IsTrue(r.Checks.Any(x=>x.DiagnosticCode=="ShearBuckling.ResistanceMethodRequired"));
        var normal=Input(n:-10000);
        var rn=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(normal),new[]{Assess(normal)});
        Assert.IsFalse(rn.IsComplete);
        Assert.AreEqual(SteelExecutionStatus.NotSupported,rn.Checks.Single(x=>x.Decision.Target==SteelCheckTarget.NormalResistance).Status);
        var plate=new SteelVerificationInput(normal.Section,normal.Actions,normal.Code.WithConservativeClass4ShearInteraction());
        Assert.IsFalse(SteelVerificationPipeline.Execute(SteelMethodSelector.Select(plate),new[]{Assess(plate)}).IsComplete);
    }

    [TestMethod]
    public void GeometryMaterialAndExternalConditionsRemainExplicit()
    {
        foreach(var i in new[]{Input(unperforated:null),Input(unperforated:false),Input(torque:1000),
            Input(new SectionRHSRoundedCorners(400,200,6,9,6,"nonconcentric")),
            Input(new SectionRHS(400,200,8,8,6,6,"unequal walls")),
            Input(formed:Section.FormedTypes.ColdFormed),Input(e:200000)})
            Assert.ThrowsException<NotSupportedException>(()=>Assess(i));
        Assert.ThrowsException<ArgumentException>(()=>Assess(Input(ntc:true,gm1:1)));
        var input=Input();
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateNonRigidRhsWebs(input,2,1.1,Supports));
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateNonRigidRhsWebs(input,2,1," "));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Assess(input,3));
        Assert.ThrowsException<ArgumentNullException>(()=>SteelShearBucklingAssessment.EvaluateNonRigidRhsWebs(null!,2,1,Supports));
        Assert.ThrowsException<ArgumentException>(()=>SteelVerificationPipeline.Execute(SteelMethodSelector.Select(Input()),new[]{Assess(input)}));
    }
}
