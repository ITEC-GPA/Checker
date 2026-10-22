using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

// EC3:2005 6.2.1(4), 6.2.9.2; DM17/01/2018 Tables4.2.III-V pp94-95,
// 4.2.3.2 and 4.2.4.1.2.8. Geometry expected values come from the existing
// independent rectangle/circle integration fixture, never from GPC properties.
[TestClass]
public class CommonSectionCoverageTests
{
    private static CodeVerificationTests.Oracle[] Oracles => JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "section-oracles.json")))!;
    internal static Section Shape(string name) => name switch {
        "H" => new SectionH(300,10,200,20,200,20,"H"),
        "C" => new SectionC(300,10,100,15,100,15,"C"),
        "T" => new SectionT(200,200,10,15,"T"),
        "L" => new SectionL(100,12,150,10,"L"),
        "RHS" => new SectionRHS(300,200,10,10,10,10,"RHS"),
        "CHS" => new SectionCHS(200,10,"CHS"),
        "Rectangular" => new SectionRectangular(200,100,"solid"),
        "Circular" => new SectionCircular(100,"solid"),
        _ => throw new ArgumentException(name) };
    internal static SteelVerificationInput Input(Section shape, bool ntc, double n=0, double m1=0, double m2=0, double fy=235)
        => new(new SteelSectionInput(new SteelSection(shape,new SteelMaterial("explicit",210000,fy,550)),true),
            new SteelActions(n,0,0,0,m1,m2), ntc ? SteelDesignContext.Ntc2018()
                : new SteelDesignContext(SectionResistanceCode.Eurocode3(1,1.25,"explicit EC3 test profile"),1));
    public static IEnumerable<object[]> Cases => from o in Oracles from ntc in new[]{false,true}
        from sign in new[]{-1d,1d} from factor in new[]{.2,1d,1.2}
        select new object[]{o.Name,ntc,sign,factor};

    [DataTestMethod,DynamicData(nameof(Cases))]
    public void AllEightFamiliesElasticInteractionAgainstIndependentSectionIntegrals(string name,bool ntc,double sign,double factor)
    {
        var o=Oracles.Single(o=>o.Name==name); double fd=235/(ntc?1.05:1);
        var input=Input(Shape(name),ntc,sign*.2*factor*o.Area*fd,sign*.3*factor*o.Wel1*fd,-sign*.5*factor*o.Wel2*fd);
        // Explicit class3 selects elastic behavior. Unequal-thickness L requires
        // independently established classification; this test qualifies resistance only.
        var plan=SteelMethodSelector.Select(input,classification:new SteelSectionClassification(input,3,"independent class3 resistance experiment"));
        var result=SteelVerificationPipeline.Execute(plan); var normal=result.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.NormalResistance);
        Assert.IsTrue(result.IsComplete,normal.Message);
        Assert.AreEqual(factor,normal.Utilization!.Value,2e-9);
        Assert.AreEqual(factor<=1+1e-12,normal.Utilization.Value<=1+2e-9);
        StringAssert.Contains(normal.Reference,ntc?"NTC 2018":"EN 1993-1-1");
        StringAssert.Contains(normal.Scope,"Conservative elastic");
    }

    public static IEnumerable<object[]> PlateCases => from kind in new[]{CompressionPartKind.InternalFlat,CompressionPartKind.OutstandFlat,CompressionPartKind.CircularHollow}
        from fy in new[]{235d,355d,460d} from boundary in new[]{1,2,3} from side in new[]{-1d,1d}
        select new object[]{kind,fy,boundary,side};
    [DataTestMethod,DynamicData(nameof(PlateCases))]
    public void NtcTablesBothSidesOfEveryCompressionLimit(CompressionPartKind kind,double fy,int boundary,double side)
    {
        double e=Math.Sqrt(235/fy);
        double[] limits=kind==CompressionPartKind.InternalFlat?new[]{33d,38,42}:kind==CompressionPartKind.OutstandFlat?new[]{9d,10,14}:new[]{50d,70,90};
        double width=10*limits[boundary-1]*(kind==CompressionPartKind.CircularHollow?235/fy:e)*(1+side*1e-8);
        var r=Ntc2018CompressionClassification.EvaluatePart(kind,width,10,fy);
        Assert.IsTrue(r.IsEvaluated); Assert.AreEqual(boundary+(side>0?1:0),r.NominalClass);
        StringAssert.Contains(r.Reference,"NTC 2018");
    }

    [DataTestMethod]
    [DataRow("H",1)] [DataRow("C",1)] [DataRow("T",4)] [DataRow("RHS",1)] [DataRow("CHS",1)]
    [DataRow("Rectangular",3)] [DataRow("Circular",3)]
    public void NtcAutomaticClassUsesItsOwnTablesAndSolidsStayElastic(string family,int expected)
    {
        var input=Input(Shape(family),true,-1000);
        var plan=SteelMethodSelector.Select(input); var normal=plan.Decisions[0];
        Assert.AreEqual(expected,normal.SectionClass); StringAssert.Contains(normal.ClassificationSource.ToUpperInvariant(),"NTC");
        Assert.IsTrue(SteelVerificationPipeline.Execute(plan).IsComplete);
        if(expected==4)Assert.AreEqual(SteelMethod.EffectiveNormalResistance,normal.Method);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RoundedRhsBendingAndInteractionUseIndependentQuarterCircleIntegrals(bool ntc)
    {
        // SCI P363 B-22 geometry HF50x30x4, independent ro=6,ri=4, S355.
        double Area(double h,double b,double r)=>h*b-(4-Math.PI)*r*r;
        double I(double h,double b,double r) {
            double d=h/2-r;
            return b*h*h*h/12-4*(r*r*r*r/3+d*r*r*r+d*d*r*r
                -(Math.PI*Math.Pow(r,4)/16+2*d*Math.Pow(r,3)/3+d*d*Math.PI*r*r/4));
        }
        double area=Area(50,30,6)-Area(42,22,4);
        double w1=(I(50,30,6)-I(42,22,4))/25, w2=(I(30,50,6)-I(22,42,4))/15;
        double fd=355/(ntc?1.05:1);
        foreach(var actions in new[]{(N:0d,M1:.4*w1*fd,M2:0d,U:.4),(N:-.2*area*fd,M1:.3*w1*fd,M2:-.4*w2*fd,U:.9)}) {
            var input=Input(new SectionRHSRoundedCorners(50,30,4,6,4,"SCI"),ntc,actions.N,actions.M1,actions.M2,355);
            var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(input));
            Assert.IsTrue(r.IsComplete); Assert.AreEqual(actions.U,r.Checks[0].Utilization!.Value,2e-9);
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void AnglePrincipalAxesAndSlenderClassRemainExplicit(bool ntc)
    {
        var a=Input(new SectionL(80,10,80,10,"equal angle",0,0),ntc,-1000,10000,-20000);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(a));
        Assert.IsTrue(r.IsComplete); Assert.AreEqual(3,r.Checks[0].Decision.SectionClass);
        var slender=Input(new SectionL(200,5,200,5,"slender angle",0,0),ntc,-1000,10000);
        var effective=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(slender));
        Assert.IsTrue(effective.IsComplete);Assert.AreEqual(SteelMethod.EffectiveNormalResistance,effective.Checks[0].Decision.Method);
        Assert.IsTrue(effective.Checks[0].Trace.Single(v=>v.Name=="AreaEffective").Value<slender.Section.Area);
    }
}
