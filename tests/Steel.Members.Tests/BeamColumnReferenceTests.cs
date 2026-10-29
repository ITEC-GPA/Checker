using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BeamColumnReferenceTests
{
    [DataTestMethod] [DataRow(1)] [DataRow(2)]
    public void SciP364Example11BiaxialInteractionFromPublishedDesignResistances(int sectionClass)
    {
        // SCI P364 Ex11 pp120-121, PDF128-129, UKC203x203x46 S275.
        // Printed N=590kN, My=30kNm, Mz=1kNm, NRk=1614.3kN,
        // MyRk=136.7kNm, MzRk=63.5kNm, chiY=.8,chiZ=.47,chiLT,mod=.94.
        // This isolates the interaction arithmetic: it does NOT replace the native
        // general LTB curve with the example's UK modified curve.
        var r=BeamColumnInteraction.Evaluate(590/(.8*1614.3),590/(.47*1614.3),30/(.94*136.7),1/63.5,
            .66,1.13,sectionClass,true,.6,.6,.6);
        Assert.AreEqual(.73,r.K11,.006);Assert.AreEqual(.75,r.K12,.006);
        Assert.AreEqual(.78,r.K21,.006);Assert.AreEqual(1.25,r.K22,.006);
        Assert.AreEqual(.64,r.Utilization1,.006);Assert.AreEqual(.98,r.Utilization2,.006);
        // 0.006 covers the publication's two-decimal factors and ratios, not a
        // tolerance against a value produced by this implementation.
    }

    [TestMethod]
    public void SciP364Example12ClassThreeMajorBendingAndCompression()
    {
        // SCI P364 Ex12 pp134-135, PDF142-143, UKB457x191x67 S355.
        // Published section is class3 for the actual N-M pattern; classification
        // and chiLT=1 are external published inputs to this scalar comparison.
        var r=BeamColumnInteraction.Evaluate(1100/(.94*3035.3),1100/(.62*3035.3),146/461.5,0,
            .43,.96,3,true,.9,1,.63);
        Assert.AreEqual(.99,r.K11,.006);Assert.AreEqual(.93,r.K21,.006);
        Assert.AreEqual(.70,r.Utilization1,.006);Assert.AreEqual(.88,r.Utilization2,.006);
        // Printed kzy=.93; the final substitution prints .92. We test the equation
        // and its rounded result, without copying that inconsistent substitution.
    }

    [DataTestMethod] [DataRow(false,1,.864,.8424,.911111111111111,1.404,.54344,.813733333333333)]
    [DataRow(true,1,.864,.8424,.911111111111111,1.404,.54344,.813733333333333)]
    [DataRow(false,3,.8576,1.116,.955555555555556,1.116,.56888,.798266666666667)]
    [DataRow(true,3,.8576,1.116,.955555555555556,1.116,.56888,.798266666666667)]
    public void Ec3B2AndCircularC42VAgainstIndependentHandSubstitution(bool ntc,int cl,double k11,double k12,double k21,double k22,double u1,double u2)
    {
        // NTC Circolare2019 p107 Table C4.2.V / EC3 AnnexB TableB.2.
        // n1=.2,n2=.4,m1=.3,m2=.1,lambda1=.6,lambda2=1.2,Cm=.8/.9/.7.
        var r=BeamColumnInteraction.Evaluate(.2,.4,.3,.1,.6,1.2,cl,true,.8,.9,.7,ntc);
        Assert.AreEqual(k11,r.K11,1e-12);Assert.AreEqual(k12,r.K12,1e-12);Assert.AreEqual(k21,r.K21,1e-12);Assert.AreEqual(k22,r.K22,1e-12);
        Assert.AreEqual(u1,r.Utilization1,1e-12);Assert.AreEqual(u2,r.Utilization2,1e-12);
    }

    [DataTestMethod] [DataRow(.1,.7)] [DataRow(.399,.9544)] [DataRow(.4,.954285714285714)]
    [DataRow(.401,.954171428571429)] [DataRow(1d,.885714285714286)] [DataRow(1.001,.885714285714286)]
    public void AnnexB2PlasticWeakAxisBranchesHaveTheSpecifiedBounds(double lambda,double k21)
    {
        var r=BeamColumnInteraction.Evaluate(.2,.4,.3,.1,.6,lambda,1,true,.8,.9,.6);
        Assert.AreEqual(k21,r.K21,1e-12);
    }

    [DataTestMethod] [DataRow(1)] [DataRow(3)]
    public void NtcRestrainedMinorCoefficientExplicitlyBoundsBothPrintedIndexInterpretations(int cl)
    {
        var r=BeamColumnInteraction.Evaluate(.8,.2,.3,.1,1.5,.5,cl,false,.8,.9,1,true);
        // Literal Circular TableIV y indices govern this chosen case. Values from
        // direct printed substitution: class1=.9*(1+1.4*.8)=1.908;
        // class3=.9*(1+.6*.8)=1.332. EC3's z terms are smaller.
        Assert.AreEqual(cl==1?1.908:1.332,r.K22,1e-12);
        var ec=BeamColumnInteraction.Evaluate(.8,.2,.3,.1,1.5,.5,cl,false,.8,.9,1);
        Assert.IsTrue(r.K22>=ec.K22);Assert.IsTrue(r.Utilization>=ec.Utilization);
        StringAssert.Contains(r.Reference,"conservative index envelope");
    }

    private static CodeVerificationTests.Oracle H=>JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name=="H");
    private static double RootChi(double lambda,double alpha)
    {
        double lo=0,hi=Math.Min(1,1/(lambda*lambda));
        for(int k=0;k<100;k++) { double q=(lo+hi)/2;double f=lambda*lambda*q*q-(1+alpha*(lambda-.2)+lambda*lambda)*q+1;if(f>0)lo=q;else hi=q; }
        return (lo+hi)/2;
    }
    private static SteelBeamColumnParameters Parameters(bool susceptible=true,SteelCriticalMomentBasis basis=SteelCriticalMomentBasis.UniformMoment,string? shear=null)
        =>new(SteelBucklingCurve.B,SteelBucklingCurve.C,susceptible,.8,.9,.7,"Independently declared equivalent moment factors",
            "Prismatic non-sway member, global effects accounted for; first-order local diagrams", "Independent torsional compression-mode screen",
            "Independent coupled compression-mode screen",basis,susceptible?null:"Full twist restraint of this test model",shear);
    private static SteelVerificationInput Input(bool ntc,int cl,int pattern,double sign=1,double nFactor=.2)
    {
        var o=H;double w1=cl<=2?o.Wpl1:o.Wel1,w2=cl<=2?o.Wpl2:o.Wel2;
        var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("H"),ntc,-nFactor*o.Area*235,
            pattern==2?0:sign*.1*w1*235,pattern==1?0:-sign*.06*w2*235);
        return new(b.Section,b.Actions,b.Code,new SteelMemberInput(4000));
    }
    private static SteelMemberAnalysis Analysis(SteelVerificationInput i,int cl,bool susceptible=true,SteelCriticalMomentBasis basis=SteelCriticalMomentBasis.UniformMoment,string? shear=null)
    {
        var o=H;double w1=cl<=2?o.Wpl1:o.Wel1;
        var values=new List<SteelStabilityValue>{new(SteelStabilityQuantity.Ncr1,o.Area*235/(.6*.6)/1000,SteelStabilityUnit.KN,"Independent critical force, lambda1=.6"),
            new(SteelStabilityQuantity.Ncr2,o.Area*235/(1.2*1.2),SteelStabilityUnit.N,"Independent critical force, lambda2=1.2")};
        if(susceptible&&i.Actions.M1!=0)values.Add(new(SteelStabilityQuantity.Mcr,w1*235/(.9*.9)/1e6,SteelStabilityUnit.KNm,"Independent uniform-moment bifurcation, lambdaLT=.9"));
        return SteelMemberAnalysis.BeamColumn(i,"Single whole-member concomitant state","Qualified code policy",Parameters(susceptible,basis,shear),values,cl,"Independent classification for resistance isolation");
    }
    private static SteelCheckExecution Run(SteelMemberAnalysis a)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    [TestMethod]
    public void WholeMemberMaximaCannotBeMisrepresentedAsConcomitantSectionActions()
        =>Assert.ThrowsException<ArgumentException>(()=>SteelMethodSelector.SelectMember(Analysis(Input(false,1,3),1),SteelCheckScope.SectionAndMember));
    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true} from cl in new[]{1,3} from susceptible in new[]{false,true}
        from pattern in new[]{1,2,3} from sign in new[]{-1d,1d}select new object[]{ntc,cl,susceptible,pattern,sign};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void HNativeRouteUsesIndependentSectionIntegralsCriticalValuesAndReductionRoots(bool ntc,int cl,bool susceptible,int pattern,double sign)
    {
        var r=Run(Analysis(Input(ntc,cl,pattern,sign),cl,susceptible));Assert.IsTrue(r.IsEvaluated,r.Message);
        double gamma=ntc?1.05:1,chi1=RootChi(.6,.34),chi2=RootChi(1.2,.49),chiLT=susceptible&&pattern!=2?RootChi(.9,ntc?.34:.21):1;
        double n1=.2*gamma/chi1,n2=.2*gamma/chi2,m1=pattern==2?0:.1*gamma/chiLT,m2=pattern==1?0:.06*gamma;
        // Fixed-lambda direct table substitutions independent of the generic production algorithm.
        double k11=.8*(1+(cl==1?.4:.36)*n1),k22=.9*(1+(cl==1?1.4:.6)*n2);
        double k12=(cl==1?.6:1)*k22;
        double k21=susceptible?1-(cl==1?.1:.05)*n2/.45:(cl==1?.6:.8)*k11;
        double expected=Math.Max(n1+k11*m1+k12*m2,n2+k21*m1+k22*m2);
        Assert.AreEqual(expected,r.Utilization!.Value,expected*4e-6); // polygon oracle plastic integrals are rounded to 2e-6
        Assert.AreEqual(chi1,r.Trace.Single(t=>t.Name=="Ncr1.Chi").Value,1e-12);
        Assert.AreEqual(chi2,r.Trace.Single(t=>t.Name=="Ncr2.Chi").Value,1e-12);
        Assert.AreEqual(chiLT,r.Trace.Single(t=>t.Name=="ChiLT").Value,4e-6);
        StringAssert.Contains(r.Reference,ntc?"Circolare 2019":"Annex B");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExceededAxialCapacityCannotBeCancelledByInteractionCoefficients(bool ntc)
    {
        var r=Run(Analysis(Input(ntc,1,3,nFactor:2),1));
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);Assert.IsTrue(r.Utilization>1);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }
    [DataTestMethod] [DataRow("missing-mcr")] [DataRow("class4")] [DataRow("torque")] [DataRow("tension")]
    [DataRow("high-shear")] [DataRow("shear-assessment")] [DataRow("ntc-mcr-basis")]
    public void BeamColumnQualificationRetainsUnsupportedCasesAndSelectsEffectiveClass4(string problem)
    {
        var i=Input(true,3,3);
        if(problem=="torque"||problem=="tension"||problem.Contains("shear"))i=new(i.Section,
            new SteelActions(problem=="tension"?100:i.Actions.N,0,problem.Contains("shear")?1e8:0,problem=="torque"?1e5:0,i.Actions.M1,i.Actions.M2),i.Code,i.Member); // 1 Nmm of torque is below the negligible-action limit
        var original=Analysis(i,problem=="class4"?4:3,basis:problem=="ntc-mcr-basis"?SteelCriticalMomentBasis.ActualMomentDiagram:SteelCriticalMomentBasis.UniformMoment,
            shear:problem=="high-shear"?"Independent buckling screen":null);
        var a=problem=="missing-mcr"?SteelMemberAnalysis.BeamColumn(i,"state","policy",Parameters(),original.Values.Where(v=>v.Quantity!=SteelStabilityQuantity.Mcr),3,"class3"):original;
        var r=Run(a);
        if(problem=="class4") {
            Assert.IsTrue(r.IsEvaluated,r.Message);Assert.IsTrue(r.Trace.Any(t=>t.Name=="AreaEffective"));
            Assert.IsTrue(r.Trace.Any(t=>t.Name=="Weff1Min"));return;
        }
        if(problem=="tension") {
            // Was NotSupported (compression required). Tension is neglected as favourable: n1=n2=0, so with class3 NTC,
            // Cm=.8/.9/.7 and susceptibility k11=.8, k12=k22=.9, k21=max(1-.05*lambda2*0/.45,1-.05*0/.45)=1 (Table C4.2.V);
            // m1=1.05*.1/chiLT(.9, curve b), m2=1.05*.06; governing row 2: m1+.9 m2.
            Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(100,r.Trace.Single(t=>t.Name=="TensionNeglectedAsFavourable").Value);
            double m1=1.05*.1/RootChi(.9,.34),m2=1.05*.06;
            Assert.AreEqual(m1+.9*m2,r.Utilization!.Value,(m1+.9*m2)*4e-6);Assert.AreEqual(1,r.Trace.Single(t=>t.Name=="K21").Value,1e-12);
            return;
        }
        Assert.IsFalse(r.IsEvaluated);Assert.IsNull(r.Utilization);
    }
    [DataTestMethod] [DataRow("C")] [DataRow("T")] [DataRow("L")] [DataRow("RHS")]
    public void OtherFamiliesDoNotSilentlyUseTheSymmetricHCoefficients(string family)
    {
        var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),false,-1000,1000,1000);
        var i=new SteelVerificationInput(b.Section,b.Actions,b.Code,new SteelMemberInput(3000,3000,3000));
        Assert.AreEqual("Member.BeamColumnGeometry",Run(SteelMemberAnalysis.BeamColumn(i,"state","policy",Parameters())).DiagnosticCode);
    }
    [DataTestMethod] [DataRow(.399)] [DataRow(1.001)] [DataRow(double.NaN)]
    public void EquivalentMomentFactorsOutsideTheirQualifiedRangeAreRejected(double cm)
        =>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,true,cm,1,1,"source","model","T","FT"));
}
