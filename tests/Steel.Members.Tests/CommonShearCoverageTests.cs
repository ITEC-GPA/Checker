using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

// EN1993-1-1:2005 6.2.8(5),eq6.30 and NTC2018 eq4.2.31-32.
// Independent integration: uniform flange stress fd, web stress (1-rho)fd.
// Only symmetric H, no N/M2/V1/T, shear buckling independently excluded.
[TestClass]
public class CommonShearCoverageTests
{
    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true} from welded in new[]{false,true}
        from fy in new[]{235d,355d} from sign in new[]{-1d,1d} from ratio in new[]{.5,.500001,.75,1d}
        select new object[]{ntc,welded,fy,sign,ratio};
    private static SteelVerificationInput Input(bool ntc,bool welded,double fy,double v,double m,double n=0,double m2=0)
        => new(new SteelSectionInput(new SteelSection(new SectionH(300,12,150,18,150,18,"H"),new SteelMaterial("explicit",210000,fy,510),
            welded?Section.SectionTypes.Welded:Section.SectionTypes.Rolled,Section.FormedTypes.HotFinished),true),
            new SteelActions(n,0,v,0,m,m2),ntc?SteelDesignContext.Ntc2018():new SteelDesignContext(SectionResistanceCode.Eurocode3(1,1.25,"test")));
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void MajorBendingShearMatchesIndependentReducedStressIntegration(bool ntc,bool welded,double fy,double sign,double ratio)
    {
        double av=welded?3168:3384,gamma=ntc?1.05:1,vpl=av*fy/(Math.Sqrt(3)*gamma);
        var i=Input(ntc,welded,fy,sign*ratio*vpl,-sign*1e7);
        var assessment=ntc?SteelShearBucklingAssessment.EvaluateNtcHWeb(i,"NTC4.2.27"):
            SteelShearBucklingAssessment.EvaluateEc3HWeb(i,1.2,"EC3 eta1.2");
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{assessment});
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Checks.Select(c=>c.Message)));
        double rho=ratio<=.5?0:Math.Pow(2*ratio-1,2),moment=0;
        // Midpoint quadrature is exact for the piecewise linear integrand y*t*stress.
        foreach(var strip in new[]{(Y0:0d,Y1:132d,B:12d,Stress:(1-rho)*fy/gamma),(Y0:132d,Y1:150d,B:150d,Stress:fy/gamma)})
            for(int k=0;k<200;k++) { double dy=(strip.Y1-strip.Y0)/200,y=strip.Y0+(k+.5)*dy;moment+=2*y*strip.B*strip.Stress*dy; }
        Assert.AreEqual(moment,r.Checks[0].Resistance!.Value,moment*1e-11);
        Assert.AreEqual(1e7/moment,r.Checks[0].Utilization!.Value,1e-12);
        if(ratio>.5) {
            Assert.AreEqual(SteelMethod.HMajorBendingShear,r.Checks[0].Decision.Method);
            Assert.AreEqual(rho,r.Checks[0].Trace.Single(t=>t.Name=="Rho").Value,1e-12);
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExceededShearFailsWithoutInventingRemainingMomentResistance(bool ntc)
    {
        var i=Input(ntc,false,235,1e9,1000);
        var a=SteelShearBucklingAssessment.FromExternal(i,2,true,"assessed");
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{a});
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(SteelExecutionStatus.Failed,r.Checks[0].Status);
        Assert.AreEqual("HighShear.PlasticShearExceeded",r.Checks[0].DiagnosticCode);Assert.IsNull(r.Checks[0].Resistance);
    }

    [DataTestMethod] [DataRow("N")] [DataRow("M2")] [DataRow("class3")] [DataRow("buckling")]
    public void HighShearDoesNotEnableOtherInteractionOrBucklingModels(string missing)
    {
        var i=Input(true,false,235,300000,1000,missing=="N"?-1000:0,missing=="M2"?1000:0);
        var plan=SteelMethodSelector.Select(i,classification:missing=="class3"?new SteelSectionClassification(i,3,"explicit class"):null);
        var r=SteelVerificationPipeline.Execute(plan,new[]{SteelShearBucklingAssessment.FromExternal(i,2,missing!="buckling","assessment")});
        Assert.IsFalse(r.IsComplete);Assert.IsFalse(r.Checks[0].IsEvaluated);
    }

    [DataTestMethod] [DataRow(235d)] [DataRow(355d)] [DataRow(460d)]
    public void NtcUnstiffenedWebCriterionHasItsOwnEtaOneAndExactBoundaries(double fy)
    {
        foreach(double side in new[]{-1d,1d}) {
            double hw=5*72*Math.Sqrt(235/fy)*(1+side*1e-8);
            var i=CommonSectionCoverageTests.Input(new SectionH(hw+40,5,200,20,200,20,"web"),true,fy:fy);
            var a=SteelShearBucklingAssessment.EvaluateNtcHWeb(i,"NTC conservative eta");
            Assert.AreEqual(side<0,a.IsBucklingExcluded);Assert.AreEqual(1d,a.Trace.Single(t=>t.Name=="Eta").Value);
            StringAssert.Contains(a.Reference,"4.2.27");
        }
    }

    [DataTestMethod] [DataRow(false,1)] [DataRow(false,2)] [DataRow(true,1)] [DataRow(true,2)]
    public void HotFinishedRoundedRhsShearMatchesIndependentAreaAndSciSample(bool ntc,int axis)
    {
        var original=CommonSectionCoverageTests.Input(new SectionRHSRoundedCorners(50,30,4,6,4,"SCI50x30x4"),ntc,fy:355);
        var i=new SteelVerificationInput(original.Section,new SteelActions(0,axis==1?1000:0,axis==2?1000:0,0,0,0),original.Code);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{SteelShearBucklingAssessment.FromExternal(i,axis,true,"independent short-wall assessment")});
        double area=(50*30-(4-Math.PI)*36)-(42*22-(4-Math.PI)*16);
        double capacity=area*(axis==1?30:50)/80*355/(Math.Sqrt(3)*(ntc?1.05:1));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(capacity,r.Checks.Single(c=>c.IsEvaluated).Resistance!.Value,1e-7);
        if(!ntc&&axis==2)Assert.AreEqual(71600,capacity,50,"SCI P363 D-112 published Vc,Rd");
    }
}
