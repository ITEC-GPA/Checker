using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BeamColumnHighShearTests
{
    private static CodeVerificationTests.Oracle Geometry(string family)=>JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name==family);
    // Rectangle/circle areas and EC3 6.2.6(3); independent of the production shear adapter.
    private static double Av(string family)=>family switch {"H"=>8000,"RHS"=>9600*200d/500,"CHS"=>3800,_=>throw new ArgumentException()};
    private static SteelMemberAnalysis Analysis(string family,bool ntc,double v,bool enabled=true,bool susceptible=false,int cl=3,bool twoShears=false,string? exclusion="Independent shear-buckling assessment")
    {
        var o=Geometry(family);var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc,-.1*o.Area*235,.12*o.Wel1*235,.05*o.Wel2*235);
        var i=new SteelVerificationInput(b.Section,new SteelActions(b.Actions.N,v*Av(family)*235/(Math.Sqrt(3)*(ntc?1.05:1)),twoShears?10000:0,0,b.Actions.M1,b.Actions.M2),b.Code,new SteelMemberInput(3000));
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,susceptible,.8,.9,.7,
            "Independent moment factors","Prismatic nonsway, same state","T excluded","FT excluded",
            lateralTorsionalExclusionSource:susceptible?null:"Twist restrained",shearBucklingExclusionSource:exclusion);
        var values=new List<SteelStabilityValue>{new(SteelStabilityQuantity.Ncr1,o.Area*235/.36,SteelStabilityUnit.N,"lambda1=.6"),
            new(SteelStabilityQuantity.Ncr2,o.Area*235/1.44,SteelStabilityUnit.N,"lambda2=1.2")};
        if(susceptible)values.Add(new(SteelStabilityQuantity.Mcr,o.Wel1*235/.81,SteelStabilityUnit.Nmm,"uniform M, lambdaLT=.9"));
        return SteelMemberAnalysis.BeamColumn(i,"same state","code policy",enabled?p.WithConservativeHighShear():p,values,cl,"Independent resistance isolation");
    }
    private static SteelCheckExecution Run(SteelMemberAnalysis a)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static double Chi(double l,double alpha) {double b=1+alpha*(l-.2)+l*l;return 2/(b+Math.Sqrt(b*b-4*l*l));}

    public static IEnumerable<object[]> Cases=>from f in new[]{"H","RHS","CHS"} from ntc in new[]{false,true}
        from v in new[]{.6,.75,.9} from sign in new[]{-1d,1d} select new object[]{f,ntc,v*sign};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void HighShearMatchesIndependentReducedStrengthAndAnnexBSubstitution(string family,bool ntc,double v)
    {
        // EN1993-1-1 6.2.10(3): at .75Vpl, rho=.25, q=.75.
        // Annex B elastic rows at lambda1=.6, lambda2=1.2; original chi retained.
        double q=1-Math.Pow(2*Math.Abs(v)-1,2),gm=ntc?1.05:1;
        double n1=.1*gm/(q*Chi(.6,.34)),n2=.1*gm/(q*Chi(1.2,.49));
        double m1=.12*gm/q,m2=.05*gm/q,k11=.8*(1+.36*n1),k22=.9*(1+.6*n2);
        if(ntc)k22=Math.Max(k22,.9*(1+.36*n1));
        double expected=Math.Max(n1+k11*m1+k22*m2,n2+.8*k11*m1+k22*m2);
        var r=Run(Analysis(family,ntc,v));Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(q,r.Trace.Single(t=>t.Name=="HighShear.NormalResistanceFactor").Value,1e-10);
        Assert.AreEqual(expected,r.Utilization!.Value,expected*2e-9);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SusceptibleMemberUsesOriginalLtbReductionAndNoFavourableK21(bool ntc)
    {
        var r=Run(Analysis("H",ntc,.75,susceptible:true));Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(1,r.Trace.Single(t=>t.Name=="K21").Value);
        Assert.AreEqual(Chi(.9,ntc?.34:.21),r.Trace.Single(t=>t.Name=="ChiLT").Value,1e-10);
        double q=.75,gm=ntc?1.05:1,n2=.1*gm/(q*Chi(1.2,.49));
        double expected=n2+.12*gm/(q*Chi(.9,ntc?.34:.21))+.9*(1+.6*n2)*.05*gm/q;
        Assert.AreEqual(expected,r.Utilization!.Value,expected*2e-9);
    }

    [DataTestMethod][DataRow(0d)][DataRow(.3)][DataRow(.5)]
    public void LowShearRemainsBitIdentical(double v)
        =>Assert.AreEqual(Run(Analysis("H",false,v,false)).Utilization,Run(Analysis("H",false,v)).Utilization);

    [DataTestMethod][DataRow(1d)][DataRow(1.2)]
    public void ExhaustedNormalResistanceFailsWithoutInventingAnInfiniteUtilization(double v)
    {
        var r=Run(Analysis("H",false,v));Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);
        Assert.AreEqual("Member.HighShearNormalCapacityExhausted",r.DiagnosticCode);Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void SelectionNeverSubstitutesForApplicabilityAndShearBuckling()
    {
        Assert.AreEqual("Member.BeamColumnHighShear",Run(Analysis("H",false,.75,false)).DiagnosticCode);
        Assert.AreEqual("Member.BeamColumnHighShearBiaxial",Run(Analysis("H",false,.75,twoShears:true)).DiagnosticCode);
        Assert.AreEqual("Member.BeamColumnHighShearClass4",Run(Analysis("H",false,.75,cl:4)).DiagnosticCode);
        Assert.IsFalse(Run(Analysis("H",false,.75,exclusion:null)).IsEvaluated);
    }

    [TestMethod]
    public void OptInProducesANewImmutableParameterSet()
    {
        var p=Analysis("H",false,0,false).BeamColumnParameters;var opted=p.WithConservativeHighShear();
        Assert.IsFalse(p.ConservativeHighShear);Assert.IsTrue(opted.ConservativeHighShear);
        Assert.AreEqual(p.MomentFactorSource,opted.MomentFactorSource);Assert.AreEqual(p.Curve1,opted.Curve1);
    }
}
