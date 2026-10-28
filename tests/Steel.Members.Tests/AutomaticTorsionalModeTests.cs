using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Automatic NcrT/NcrFT of the compression route when the caller supplies neither value nor exclusion.</summary>
[TestClass]
public class AutomaticTorsionalModeTests
{
    // IPE300 with the rolled fillet r=15 (ArcelorMittal: A=53.81cm2, Iy=8356cm4, Iz=603.8cm4, It=20.12cm4, Iw=125.9e3cm6).
    private static SectionH Ipe300() => new(300,7.1,150,10.7,150,10.7,"IPE300",15);
    private static SteelVerificationInput Input(Section shape,bool ntc,SteelMemberInput member,double n=-1e5,double m1=0)
    {
        var i=CommonSectionCoverageTests.Input(shape,ntc,n,m1);
        return new(i.Section,i.Actions,i.Code,member);
    }
    private static SteelCheckExecution Run(SteelMemberAnalysis a)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    private static SteelMemberAnalysis Compression(SteelVerificationInput i,IEnumerable<SteelStabilityValue>? values=null,string? excludeT=null,string? excludeFT=null)
        =>SteelMemberAnalysis.Compression(i,"constant centroidal N","explicit curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,values,excludeT,excludeFT);
    private static double V(SteelCheckExecution r,string name)=>r.Trace.Single(t=>t.Name==name).Value;
    // Independent root of the characteristic reduction equation lambda^2 chi^2-(1+alpha(lambda-.2)+lambda^2)chi+1=0.
    private static double Chi(double lambda,double alpha)
    {
        if(lambda<=.2)return 1;
        double lo=0,hi=Math.Min(1,1/(lambda*lambda));
        for(int k=0;k<100;k++){double x=(lo+hi)/2;if(lambda*lambda*x*x-(1+alpha*(lambda-.2)+lambda*lambda)*x+1>0)lo=x;else hi=x;}
        return (lo+hi)/2;
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void Ipe300TorsionalModeMatchesTheClosedFormAndGoverns(bool ntc)
    {
        // L1=LT=6000, L2=2000 (minor axis braced): with catalogue values G=E/2.6=80769MPa,
        // i0^2=(8356+603.8)e4/5381=16651mm2, GIt=1.625e10, pi^2*E*Iw/LT^2=7.248e9 Nmm2,
        // NcrT=(1.625e10+7.248e9)/16651=1.411e6N < Ncr2=pi^2*E*Iz/2000^2=3.13e6N: T governs.
        var i=Input(Ipe300(),ntc,new SteelMemberInput(6000,6000,2000,6000));
        var s=i.Section.CreateSectionCopy();
        // The Model section reproduces the catalogue torsion constants (rolled-I fillet formula: It=19.92e4, -1%).
        Assert.AreEqual(20.12e4,s.Jt,20.12e4*.02);Assert.AreEqual(125.9e9,s.Jw,125.9e9*.01);
        double g=210000/(2*(1+i.Section.PoissonRatio)),i02=(s.J11+s.J22)/s.Area;
        double ncrT=(g*s.Jt+Math.PI*Math.PI*210000*s.Jw/(6000d*6000))/i02;
        Assert.AreEqual(1.411e6,ncrT,1.411e6*.02); // hand value above
        var r=Run(Compression(i));Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(ncrT,V(r,"NcrT"),ncrT*1e-12);Assert.AreEqual(6000,V(r,"NcrT.Length"));Assert.AreEqual(1,V(r,"NcrT.Automatic"));
        Assert.AreEqual(1,V(r,"NcrFT.AutomaticExclusion"));Assert.AreEqual(1,V(r,"NcrFT.Excluded"));
        double nrk=s.Area*235,expected=Chi(Math.Sqrt(nrk/ncrT),.49)*nrk/(ntc?1.05:1);
        Assert.AreEqual(expected,r.Resistance!.Value,expected*1e-10);Assert.AreEqual(r.Resistance.Value,V(r,"NcrT.NbRd"),1e-6);
        StringAssert.Contains(r.Reference,ntc?"NTC 2018 4.2.4.1.3.1":"EN 1993-1-1:2005 6.3.1.4");StringAssert.Contains(r.Reference,"LcrT=6000");
    }

    [TestMethod]
    public void EndTwistRestraintsGiveTheMemberLengthAndExplicitInputAlwaysWins()
    {
        var fork=new SteelEndRestraints(rotation3:SteelRestraintState.Fixed);
        var i=Input(Ipe300(),false,new SteelMemberInput(5000,5000,5000,startRestraints:fork,endRestraints:fork));
        var r=Run(Compression(i));Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(5000,V(r,"NcrT.Length"));Assert.AreEqual(1,V(r,"NcrT.LengthFromEndTwistRestraints"));
        var oneEnd=Input(Ipe300(),false,new SteelMemberInput(5000,5000,5000,startRestraints:fork));
        Assert.AreEqual(SteelExecutionStatus.MissingInput,Run(Compression(oneEnd)).Status); // a free/unknown end has no default length
        // Explicit value: used as given, no automatic trace.
        var supplied=Run(Compression(i,new[]{new SteelStabilityValue(SteelStabilityQuantity.NcrT,2e6,SteelStabilityUnit.N,"independent eigenvalue")}));
        Assert.AreEqual(2e6,V(supplied,"NcrT"));Assert.IsFalse(supplied.Trace.Any(t=>t.Name=="NcrT.Automatic"));
        // Explicit exclusions: both modes excluded by the caller, nothing automatic.
        var excluded=Run(Compression(i,excludeT:"continuous twist restraint",excludeFT:"assessed"));
        Assert.AreEqual(1,V(excluded,"NcrT.Excluded"));Assert.IsFalse(excluded.Trace.Any(t=>t.Name.StartsWith("NcrT.Automatic")||t.Name.StartsWith("NcrFT.Automatic")));
        Assert.IsFalse(excluded.Reference!.Contains("automatic torsional modes"));
    }

    [DataTestMethod] [DataRow("RHS",false)] [DataRow("RHS",true)] [DataRow("CHS",false)] [DataRow("CHS",true)]
    public void ClosedHollowSectionsExcludeTorsionalModesWithTheLowerBoundSlenderness(string family,bool ntc)
    {
        // CHS200x10: It=2I, i0^2=2I/A, so G*It/i0^2=G*A and lambda_T=sqrt(fy/G)=sqrt(235/80769)=0.054.
        // RHS300x200x10 (Bredt It=4*Am^2*t/p=4*(290*190)^2*10/960=1.265e8, i0^2=(1.207e8+0.639e8)/9600=19230):
        // G*It/i0^2=5.31e8N, lambda_T=sqrt(9600*235/5.31e8)=0.065. Both < 0.2: T and FT excluded at any length.
        var i=Input(CommonSectionCoverageTests.Shape(family),ntc,new SteelMemberInput(8000,8000,8000));
        var s=i.Section.CreateSectionCopy();double g=210000/(2*(1+i.Section.PoissonRatio)),i02=(s.J11+s.J22)/s.Area;
        double lambda=Math.Sqrt(s.Area*235*i02/(g*s.Jt));Assert.IsTrue(lambda<(family=="CHS"?.06:.08),lambda.ToString());
        var r=Run(Compression(i));Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(lambda,V(r,"NcrT.LowerBoundSlenderness"),lambda*1e-12);
        Assert.AreEqual(1,V(r,"NcrT.AutomaticExclusion"));Assert.AreEqual(1,V(r,"NcrT.Excluded"));Assert.AreEqual(1,V(r,"NcrFT.Excluded"));
        double n1=Math.PI*Math.PI*210000*s.J11/64e6,n2=Math.PI*Math.PI*210000*s.J22/64e6,nrk=s.Area*235;
        double expected=nrk/(ntc?1.05:1)*Math.Min(Chi(Math.Sqrt(nrk/n1),.34),Chi(Math.Sqrt(nrk/n2),.49));
        Assert.AreEqual(expected,r.Resistance!.Value,expected*1e-10); // flexural modes only
        StringAssert.Contains(r.Reference,ntc?"NTC 2018 4.2.4.1.3.1: closed":"6.3.1.4(1) requires torsional modes for open cross-sections");
    }

    [DataTestMethod] [DataRow("C")] [DataRow("T")] [DataRow("L")]
    public void OtherFamiliesKeepRequiringExplicitTorsionalInput(string family)
    {
        var r=Run(SteelMemberAnalysis.Compression(Input(CommonSectionCoverageTests.Shape(family),false,new SteelMemberInput(3000,3000,3000,3000)),
            "N","curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,memberClass:3,classificationSource:"independent class3 test"));
        Assert.AreEqual(SteelExecutionStatus.MissingInput,r.Status);
        CollectionAssert.AreEquivalent(new[]{"NcrT or explicit mode exclusion","NcrFT or explicit mode exclusion"},r.Decision.MissingInputs.ToArray());
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnResolvesTorsionalModesLikeTheCompressionRoute(bool ntc)
    {
        // Same member as the IPE300 case; Cm/LT explicit, NcrT left to the automatic route.
        var i=Input(Ipe300(),ntc,new SteelMemberInput(6000,6000,2000,6000),n:-2e5,m1:5e6);
        SteelBeamColumnParameters P(string? t,string? ft)=>new(SteelBucklingCurve.A,SteelBucklingCurve.B,false,1,1,1,"explicit Cm=1","first order nonsway",t,ft,
            lateralTorsionalExclusionSource:"continuously braced compression flange");
        var automatic=Run(SteelMemberAnalysis.BeamColumn(i,"one state","Annex B",P(null,null)));
        Assert.IsTrue(automatic.IsEvaluated,automatic.Message);
        var s=i.Section.CreateSectionCopy();double g=210000/(2*(1+i.Section.PoissonRatio));
        double ncrT=(g*s.Jt+Math.PI*Math.PI*210000*s.Jw/36e6)/((s.J11+s.J22)/s.Area);
        Assert.AreEqual(ncrT,V(automatic,"NcrT"),ncrT*1e-12);Assert.AreEqual(1,V(automatic,"NcrFT.AutomaticExclusion"));
        // Independent supply of the same closed-form value and the FT identity gives the same verdict.
        var supplied=Run(SteelMemberAnalysis.BeamColumn(i,"one state","Annex B",P(null,"doubly symmetric: no distinct FT mode"),
            new[]{new SteelStabilityValue(SteelStabilityQuantity.NcrT,ncrT,SteelStabilityUnit.N,"closed form")}));
        Assert.AreEqual(supplied.Utilization!.Value,automatic.Utilization!.Value,1e-12);
        var missing=Run(SteelMemberAnalysis.BeamColumn(Input(Ipe300(),ntc,new SteelMemberInput(6000,6000,2000),n:-2e5,m1:5e6),"one state","Annex B",P(null,null)));
        Assert.AreEqual(SteelExecutionStatus.MissingInput,missing.Status);StringAssert.StartsWith(missing.Decision.MissingInputs.Single(),"NcrT");
        Assert.ThrowsException<ArgumentException>(()=>P(" ",null));
    }
}
