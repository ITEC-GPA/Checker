using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BeamColumnShearSuperpositionTests
{
    private static CodeVerificationTests.Oracle Geometry(string family) =>
        JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name==family);

    private static SteelMemberAnalysis Analysis(string family,bool ntc,double v1,double v2,int cl=3,
        string? exclusion="Independent panel exclusions for both principal directions")
    {
        var o=Geometry(family);
        // Independently integrated geometry and EN1993-1-1 6.2.6 shear areas.
        double av1=family=="H"?8000:family=="RHS"?.4*o.Area:2*o.Area/Math.PI;
        double av2=family=="H"?2800:family=="RHS"?.6*o.Area:av1;
        double fd=235/(ntc?1.05:1);
        var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc);
        var i=new SteelVerificationInput(b.Section,new(-.03*o.Area*235,v1*av1*fd/Math.Sqrt(3),
            v2*av2*fd/Math.Sqrt(3),0,.04*o.Wel1*235,-.02*o.Wel2*235),b.Code,new SteelMemberInput(3000));
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,false,.8,.9,1,
            "same-state nonsway factors","prismatic model","T excluded","FT excluded",
            lateralTorsionalExclusionSource:"twist restrained",shearBucklingExclusionSource:exclusion)
            .WithConservativeShearSuperposition();
        return SteelMemberAnalysis.BeamColumn(i,"same-case extrema","EC/NTC",p,new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,o.Area*235/.36,SteelStabilityUnit.N,"gross Euler lambda=.6"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,o.Area*235/1.44,SteelStabilityUnit.N,"gross Euler lambda=1.2")},
            cl,"independent class isolation");
    }
    private static SteelCheckExecution Run(SteelMemberAnalysis a,params SteelShearBucklingAssessment[] assessments)
        =>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a),assessments).Checks.Single();
    private static double Chi(double l,double alpha)
    { double phi=(1+alpha*(l-.2)+l*l)/2;return 1/(phi+Math.Sqrt(phi*phi-l*l)); }

    public static IEnumerable<object[]> Cases => from f in new[]{"H","RHS","CHS"}
        from ntc in new[]{false,true} from cl in new[]{1,2,3} from signs in new[]{-1d,1d}
        from pair in new[]{new[]{.2,.3},new[]{.6,.15},new[]{0d,.65}}
        select new object[]{f,ntc,cl,signs*pair[0],-signs*pair[1]};

    [DataTestMethod,DynamicData(nameof(Cases))]
    public void TwoShearsMatchIndependentElasticAnnexBWithLinearCapacityBudget(string family,bool ntc,int cl,double v1,double v2)
    {
        // Independent hand substitution in Annex B / Circular Method B elastic rows.
        // Original lambda/chi stay conservative when uniform yield strength is reduced.
        double q=1-Math.Abs(v1)-Math.Abs(v2),g=ntc?1.05:1;
        double n1=.03*g/(q*Chi(.6,.34)),n2=.03*g/(q*Chi(1.2,.49));
        double k11=.8*(1+.36*n1),k22=.9*(1+.6*n2);
        if(ntc)k22=Math.Max(k22,.9*(1+.36*n1));
        double expected=Math.Max(n1+k11*.04*g/q+k22*.02*g/q,
            n2+.8*k11*.04*g/q+k22*.02*g/q);
        var r=Run(Analysis(family,ntc,v1,v2,cl));
        Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(expected,r.Utilization!.Value,expected*2e-9);
        Assert.AreEqual(q,r.Trace.Single(t=>t.Name=="ShearSuperposition.NormalResistanceFactor").Value,1e-12);
        Assert.AreEqual(1,r.Trace.Single(t=>t.Name=="ElasticInteraction").Value);
        Assert.IsFalse(r.Trace.Any(t=>t.Name=="HighShear.Rho"));
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [DataTestMethod][DataRow(.6,.4)][DataRow(.6,.6)][DataRow(1.01,0d)]
    public void SumExhaustsNormalBudgetEvenWhenIndividualShearsPass(double v1,double v2)
    {
        var r=Run(Analysis("H",false,v1,v2));
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);
        Assert.AreEqual("Member.ShearSuperpositionNormalCapacityExhausted",r.DiagnosticCode);
        Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void EachDirectionNeedsItsOwnPositiveBucklingAssessment()
    {
        var a=Analysis("H",false,.6,.2,exclusion:null);
        var first=SteelShearBucklingAssessment.FromExternal(a.Input,1,true,"flanges");
        Assert.AreEqual(SteelExecutionStatus.RequiresPreparation,Run(a,first).Status);
        var negative=SteelShearBucklingAssessment.FromExternal(a.Input,2,false,"web buckles");
        Assert.AreEqual(SteelExecutionStatus.NotSupported,Run(a,first,negative).Status);
        Assert.IsTrue(Run(a,first,SteelShearBucklingAssessment.FromExternal(a.Input,2,true,"web")).IsEvaluated);
    }

    [TestMethod]
    public void NewOptInDoesNotMutateTheHistoricalMethodOrMakeClass4Applicable()
    {
        var p=Analysis("H",false,0,0).BeamColumnParameters;
        var old=p.WithConservativeHighShear();
        Assert.IsTrue(p.ConservativeShearSuperposition);Assert.IsFalse(p.ConservativeHighShear);
        Assert.IsTrue(old.ConservativeHighShear);Assert.IsFalse(old.ConservativeShearSuperposition);
        Assert.AreEqual("Member.ShearSuperpositionClass4NotQualified",Run(Analysis("H",false,.6,.1,4)).DiagnosticCode);
    }
}
