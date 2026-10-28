using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class CommonMemberCoverageTests
{
    private static CodeVerificationTests.Oracle[] Oracles => JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!;
    private static SteelVerificationInput Input(string family,bool ntc,double length=4000,double n=-10000,double m=0)
    {
        var i=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc,n,m);
        return new(i.Section,i.Actions,i.Code,new SteelMemberInput(length,length,1.3*length));
    }
    private static SteelStabilityValue Value(SteelStabilityQuantity q,double value)=>new(q,value,SteelStabilityUnit.N,"independent eigenvalue source");
    private static SteelCheckExecution Run(SteelMemberAnalysis a)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    // Independent root solution of the characteristic reduction equation.
    private static double Chi(double lambda,double alpha)
    {
        if(lambda<=.2)return 1;
        double lo=0,hi=Math.Min(1,1/(lambda*lambda));
        for(int k=0;k<100;k++) { double x=(lo+hi)/2; double residual=lambda*lambda*x*x-(1+alpha*(lambda-.2)+lambda*lambda)*x+1; if(residual>0)lo=x;else hi=x; }
        return (lo+hi)/2;
    }
    public static IEnumerable<object[]> CompressionCases=>from o in Oracles from ntc in new[]{false,true}
        from length in new[]{1000d,4000d,12000d} select new object[]{o.Name,ntc,length};
    [DataTestMethod,DynamicData(nameof(CompressionCases))]
    public void CompressionBothAxesAgainstIndependentGeometryAndCharacteristicEquation(string family,bool ntc,double length)
    {
        var o=Oracles.Single(o=>o.Name==family); var i=Input(family,ntc,length);
        var a=SteelMemberAnalysis.Compression(i,"constant centroidal N over whole member","explicit assessed curves",
            SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"Torsional restraint assessment for this test model",
            flexuralTorsionalExclusionSource:"Continuous twist restraint excludes coupling in this test model",
            memberClass:3,classificationSource:"independently prescribed class3 resistance test");
        var r=Run(a); Assert.IsTrue(r.IsEvaluated,r.Message);
        double ncr1=Math.PI*Math.PI*210000*o.I1/(length*length),ncr2=Math.PI*Math.PI*210000*o.I2/Math.Pow(1.3*length,2);
        double expected=o.Area*235/(ntc?1.05:1)*Math.Min(Chi(Math.Sqrt(o.Area*235/ncr1),.34),Chi(Math.Sqrt(o.Area*235/ncr2),.49));
        Assert.AreEqual(expected,r.Resistance!.Value,expected*2e-10);
        Assert.AreEqual(ncr1,r.Trace.Single(t=>t.Name=="Ncr1").Value,ncr1*2e-10);
        Assert.AreEqual(ncr2,r.Trace.Single(t=>t.Name=="Ncr2").Value,ncr2*2e-10);
        StringAssert.Contains(r.Reference,ntc?"NTC 2018":"EN 1993-1-1");
    }

    [DataTestMethod]
    [DataRow(false,SteelStabilityQuantity.Ncr1)] [DataRow(true,SteelStabilityQuantity.Ncr1)]
    [DataRow(false,SteelStabilityQuantity.Ncr2)] [DataRow(true,SteelStabilityQuantity.Ncr2)]
    [DataRow(false,SteelStabilityQuantity.NcrT)] [DataRow(true,SteelStabilityQuantity.NcrT)]
    [DataRow(false,SteelStabilityQuantity.NcrFT)] [DataRow(true,SteelStabilityQuantity.NcrFT)]
    public void AnyOfTheFourModesCanGovernAndNoneIsSilentlyOmitted(bool ntc,SteelStabilityQuantity governing)
    {
        var i=Input("C",ntc); double nrk=5700*235;
        var values=new[]{SteelStabilityQuantity.Ncr1,SteelStabilityQuantity.Ncr2,SteelStabilityQuantity.NcrT,SteelStabilityQuantity.NcrFT}
            .Select(q=>Value(q,q==governing?nrk/4:4*nrk)).ToArray();
        var a=SteelMemberAnalysis.Compression(i,"constant compression","all modes independently analysed",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,values);
        var r=Run(a); Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(Chi(2,.49)*nrk/(ntc?1.05:1),r.Resistance!.Value,nrk*1e-12);
        Assert.AreEqual(r.Resistance.Value,r.Trace.Single(t=>t.Name==governing+".NbRd").Value,1e-7);
        values[0]=Value(SteelStabilityQuantity.Ncr1,nrk*1e8);
        Assert.AreNotEqual(values[0].Value,a.Values[0].Value); // copied collection
    }

    [TestMethod]
    public void MissingTorsionIsIncompleteAndAnExclusionCannotConflictWithAValue()
    {
        var i=Input("H",true);
        var missing=Run(SteelMemberAnalysis.Compression(i,"N","explicit curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C));
        Assert.AreEqual(SteelExecutionStatus.MissingInput,missing.Status); Assert.IsNull(missing.Resistance);
        // Doubly symmetric H (x0=y0=0): det=(N1-N)(N2-N)(NT-N), FT is no distinct mode and is resolved
        // automatically; NcrT stays missing without a torsional length or end twist restraints.
        Assert.AreEqual(1,missing.Decision.MissingInputs.Count); StringAssert.StartsWith(missing.Decision.MissingInputs[0],"NcrT");
        var conflict=Run(SteelMemberAnalysis.Compression(i,"N","explicit curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,
            new[]{Value(SteelStabilityQuantity.NcrT,1e7)},"excluded","excluded"));
        Assert.AreEqual(SteelExecutionStatus.InvalidInput,conflict.Status);
    }

    public static IEnumerable<object[]> BendingCases=>from name in new[]{"H","C","T","RHS","CHS","Rectangular","Circular"}
        from ntc in new[]{false,true} from cl in new[]{1,3} from sign in new[]{-1d,1d} select new object[]{name,ntc,cl,sign};
    [DataTestMethod,DynamicData(nameof(BendingCases))]
    public void LtbUsesCodeSpecificCurvesAndClassDependentModuli(string family,bool ntc,int cl,double sign)
    {
        var o=Oracles.Single(o=>o.Name==family); double w=cl==1?o.Wpl1:o.Wel1;
        double mcr=w*235/(.9*.9); var input=Input(family,ntc,n:0,m:sign*1e7);
        var a=SteelMemberAnalysis.Bending(input,"pure major bending, max M1","recommended code curves",SteelCriticalMomentBasis.UniformMoment,
            new(SteelStabilityQuantity.Mcr,mcr/1e6,SteelStabilityUnit.KNm,"independent eigenvalue calculation"),cl,"independent resistance-only class");
        var r=Run(a); Assert.IsTrue(r.IsEvaluated,r.Message);
        double alpha=family=="H"?(ntc?.34:.21):.76;
        double expected=Chi(.9,alpha)*w*235/(ntc?1.05:1);
        Assert.AreEqual(expected,r.Resistance!.Value,expected*3e-6); // independent polygon plastic integrals round at 2e-6
        Assert.AreEqual(alpha,r.Trace.Single(t=>t.Name=="AlphaLT").Value);
    }

    [TestMethod]
    public void NtcCannotReuseActualDiagramMcrAsUniformMomentMcr()
    {
        var a=SteelMemberAnalysis.Bending(Input("H",true,n:0,m:1e6),"M","profile",SteelCriticalMomentBasis.ActualMomentDiagram,
            new(SteelStabilityQuantity.Mcr,1e8,SteelStabilityUnit.Nmm,"actual triangular diagram"));
        Assert.AreEqual("Member.NtcUniformMcrRequired",Run(a).DiagnosticCode);
    }
    [TestMethod]
    public void TensionAndCombinedActionsAreNeverMislabelledAsCompletedCompressionChecks()
    {
        SteelMemberAnalysis A(SteelVerificationInput i)=>SteelMemberAnalysis.Compression(i,"declaration","curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"assessed",flexuralTorsionalExclusionSource:"assessed");
        Assert.AreEqual(SteelExecutionStatus.NotRequired,Run(A(Input("H",true,n:1000))).Status);
        Assert.AreEqual(SteelExecutionStatus.NotSupported,Run(A(Input("H",true,m:1e5))).Status); // 1 Nmm is below the negligible-action limit
        var i=Input("H",true); var b=SteelMemberAnalysis.Bending(i,"N+M","profile",SteelCriticalMomentBasis.UniformMoment,new(SteelStabilityQuantity.Mcr,1e8,SteelStabilityUnit.Nmm,"source"));
        Assert.AreEqual(SteelExecutionStatus.NotSupported,Run(b).Status);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RoundedRhsSciPublishedCompressionIsRetainedThroughTheNewMemberRoute(bool ntc)
    {
        // SCI P363 B-22,D-22: HF50x30x4 S355, ro6/ri4, L=2m, minor Nb=33.6kN.
        var baseInput=CommonSectionCoverageTests.Input(new SectionRHSRoundedCorners(50,30,4,6,4,"SCI"),ntc,-10000,fy:355);
        var i=new SteelVerificationInput(baseInput.Section,baseInput.Actions,baseInput.Code,new SteelMemberInput(2000,2000,2000));
        var r=Run(SteelMemberAnalysis.Compression(i,"SCI2m, concentric, prismatic, restrained against twist","curve a hot finished",
            SteelBucklingCurve.A,SteelBucklingCurve.A,SteelBucklingCurve.A,torsionalExclusionSource:"closed hollow, torsion restrained",
            flexuralTorsionalExclusionSource:"doubly symmetric closed hollow, twist restrained"));
        Assert.IsTrue(r.IsEvaluated,r.Message);
        // SCI table has rounded A/i and Nb. 0.3kN bounds the independently
        // documented property/last-digit rounding; NTC is a gamma-scaled normative comparison.
        Assert.AreEqual(33600/(ntc?1.05:1),r.Resistance!.Value,300/(ntc?1.05:1));
    }
    [TestMethod]
    public void MissingLengthsClassFourAndRotatedHDoNotReachTheGrossMemberExecutor()
    {
        var original=Input("H",false);
        var noLengths=new SteelVerificationInput(original.Section,original.Actions,original.Code,new SteelMemberInput(4000));
        var r=Run(SteelMemberAnalysis.Compression(noLengths,"constant N","curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"assessed",flexuralTorsionalExclusionSource:"assessed"));
        Assert.AreEqual(SteelExecutionStatus.MissingInput,r.Status);Assert.AreEqual(2,r.Decision.MissingInputs.Count);
        r=Run(SteelMemberAnalysis.Compression(original,"N","curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"assessed",flexuralTorsionalExclusionSource:"assessed",memberClass:4,classificationSource:"class4"));
        Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.IsTrue(r.Trace.Any(t=>t.Name=="AreaEffective")); // explicit class4 selects effective properties even for a stocky test H
        var wide=CommonSectionCoverageTests.Input(new SectionH(100,10,500,20,500,20,"wide H"),false,m1:10000);
        Assert.IsTrue(Math.Abs(wide.Section.PrincipalAxisAngle)>1);
        var i=new SteelVerificationInput(wide.Section,wide.Actions,wide.Code,new SteelMemberInput(3000));
        r=Run(SteelMemberAnalysis.Bending(i,"M1","profile",SteelCriticalMomentBasis.ActualMomentDiagram,
            new(SteelStabilityQuantity.Mcr,1e8,SteelStabilityUnit.Nmm,"source")));
        Assert.AreEqual("Member.LtbGeometry",r.DiagnosticCode);
    }

    [DataTestMethod] [DataRow(300d,false,.21)] [DataRow(300d,true,.34)]
    [DataRow(300.001,false,.34)] [DataRow(300.001,true,.49)]
    public void RolledHLtbTablesUseTheVerifiedDepthWidthBoundary(double height,bool ntc,double alpha)
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(height,12,150,18,150,18,"boundary H"),ntc,m1:1e6);
        var i=new SteelVerificationInput(b.Section,b.Actions,b.Code,new SteelMemberInput(3000));
        var r=Run(SteelMemberAnalysis.Bending(i,"major M1","recommended tables",SteelCriticalMomentBasis.UniformMoment,
            new(SteelStabilityQuantity.Mcr,1e8,SteelStabilityUnit.Nmm,"source")));
        Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(alpha,r.Trace.Single(v=>v.Name=="AlphaLT").Value);
    }

}
