using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class EffectiveMemberTests
{
    private static SteelVerificationInput Input(string family,bool ntc,double length=4000,double n=-1e5,double m1=0,double m2=0)
    {
        var i=CommonSectionCoverageTests.Input(EffectiveSectionSolverTests.Shape(family),ntc,n,m1,m2,355);
        return new(i.Section,i.Actions,i.Code,new SteelMemberInput(length,length,length));
    }
    private static SteelCheckExecution Run(SteelMemberAnalysis analysis)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis)).Checks.Single();
    private static SteelMemberAnalysis Compression(SteelVerificationInput input,IEnumerable<SteelStabilityValue>? values=null)=>SteelMemberAnalysis.Compression(input,
        "Prismatic constant centroidal N","assessed compression curves",SteelBucklingCurve.B,SteelBucklingCurve.C,SteelBucklingCurve.C,values,
        values==null?"T excluded by independent restraint assessment":null,values==null?"FT excluded by independent restraint assessment":null);
    private static double Chi(double l,double alpha)
    {
        if(l<=.2)return 1;
        double lo=0,hi=Math.Min(1,1/(l*l));
        for(int j=0;j<100;j++) {double x=(lo+hi)/2;double f=l*l*x*x-(1+alpha*(l-.2)+l*l)*x+1;if(f>0)lo=x;else hi=x;}
        return (lo+hi)/2;
    }
    private static double RhsArea()
    {
        double R(double b) {double l=b/4/(56.8*Math.Sqrt(235d/355));return (l-.22)/(l*l);}
        return 64+8*(392*R(392)+192*R(192));
    }
    [DataTestMethod] [DataRow(false,1000d)] [DataRow(true,1000d)] [DataRow(false,4000d)] [DataRow(true,4000d)] [DataRow(false,10000d)] [DataRow(true,10000d)]
    public void CompressionUsesIndependentEffectiveAreaAndGrossEulerInertias(bool ntc,double length)
    {
        var i=Input("RHS",ntc,length);var r=Run(Compression(i));Assert.IsTrue(r.IsEvaluated,r.Message);
        double a=RhsArea(),iy=(200*Math.Pow(400,3)-192*Math.Pow(392,3))/12,iz=(400*Math.Pow(200,3)-392*Math.Pow(192,3))/12;
        double n1=Math.PI*Math.PI*210000*iy/(length*length),n2=Math.PI*Math.PI*210000*iz/(length*length);
        double expected=a*355/(ntc?1.05:1)*Math.Min(Chi(Math.Sqrt(a*355/n1),.34),Chi(Math.Sqrt(a*355/n2),.49));
        Assert.AreEqual(a,r.Trace.Single(t=>t.Name=="AreaEffective").Value,1e-7);
        Assert.AreEqual(n1,r.Trace.Single(t=>t.Name=="Ncr1").Value,n1*1e-12);Assert.AreEqual(n2,r.Trace.Single(t=>t.Name=="Ncr2").Value,n2*1e-12);
        Assert.AreEqual(expected,r.Resistance!.Value,expected*1e-10);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FourCriticalModesUseSameEffectiveCharacteristicForce(bool ntc)
    {
        var i=Input("RHS",ntc);double nrk=RhsArea()*355;
        var modes=new[]{SteelStabilityQuantity.Ncr1,SteelStabilityQuantity.Ncr2,SteelStabilityQuantity.NcrT,SteelStabilityQuantity.NcrFT};
        foreach(var governing in modes) {
            var values=modes.Select(q=>new SteelStabilityValue(q,q==governing?nrk/4:nrk*4,SteelStabilityUnit.N,"independent eigenvalue"));
            var r=Run(Compression(i,values));Assert.IsTrue(r.IsEvaluated,r.Message);
            double expected=Chi(2,governing==SteelStabilityQuantity.Ncr1?.34:.49)*nrk/(ntc?1.05:1);
            Assert.AreEqual(expected,r.Resistance!.Value,expected*1e-10);
        }
    }
    [DataTestMethod] [DataRow("H",false)] [DataRow("H",true)] [DataRow("RHS",false)] [DataRow("RHS",true)] [DataRow("RoundedRHS",false)] [DataRow("RoundedRHS",true)]
    public void LtbUsesEffectiveModulusAndLeavesExternalMcrUnchanged(string family,bool ntc)
    {
        var i=Input(family,ntc,n:0,m1:1e7);const double mcr=1e9;
        var r=Run(SteelMemberAnalysis.Bending(i,"whole span max M1","general LTB curve",SteelCriticalMomentBasis.UniformMoment,
            new SteelStabilityValue(SteelStabilityQuantity.Mcr,mcr,SteelStabilityUnit.Nmm,"gross elastic eigenvalue")));
        Assert.IsTrue(r.IsEvaluated,r.Message);
        double w=r.Trace.Single(t=>t.Name=="Weff1Min").Value;
        Assert.IsTrue(w<i.Section.CreateSectionCopy().Wel1);
        double alpha=family=="H"?(ntc?.49:.34):.76;
        Assert.AreEqual(Chi(Math.Sqrt(w*355/mcr),alpha)*w*355/(ntc?1.05:1),r.Resistance!.Value,r.Resistance.Value*1e-10);
        Assert.AreEqual(mcr,r.Trace.Single(t=>t.Name=="Mcr").Value);
        var state=SteelEffectiveSectionSolver.Solve(i);Assert.AreEqual(1/state.MaximumAbsoluteStress*1e7,w,w*1e-9);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ExplicitClass4OnStockyHUsesIndependentElasticResistance(bool ntc)
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(300,10,200,20,200,20,"compact H"),ntc,m1:1e6);
        var i=new SteelVerificationInput(b.Section,b.Actions,b.Code,new SteelMemberInput(4000));
        double inertia=2*(200*Math.Pow(20,3)/12+4000*140*140)+10*Math.Pow(260,3)/12,w=inertia/150;
        double mcr=w*235/(.8*.8);
        var r=Run(SteelMemberAnalysis.Bending(i,"M","class4 elastic policy",SteelCriticalMomentBasis.UniformMoment,
            new(SteelStabilityQuantity.Mcr,mcr,SteelStabilityUnit.Nmm,"gross eigenvalue"),4,"independent resistance-contract experiment"));
        Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(w,r.Trace.Single(t=>t.Name=="Weff1Min").Value,w*1e-10);
        Assert.AreEqual(Chi(.8,ntc?.34:.21)*w*235/(ntc?1.05:1),r.Resistance!.Value,r.Resistance.Value*1e-10);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void BeamColumnUsesTableB1ElasticCoefficientsAndDistinctPureActionProperties(bool ntc)
    {
        var i=Input("RHS",ntc,n:-1e5,m1:1e6,m2:-5e5);double nrk=RhsArea()*355;
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.A,SteelBucklingCurve.A,false,.8,.9,1,
            "conservative moment factors","global second order accounted","T assessed","FT assessed",lateralTorsionalExclusionSource:"LTB assessed");
        var r=Run(SteelMemberAnalysis.BeamColumn(i,"same load state","AnnexB / Circular B",p,new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,nrk/.25,SteelStabilityUnit.N,"gross eigenvalue"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,nrk/.64,SteelStabilityUnit.N,"gross eigenvalue")}));
        Assert.IsTrue(r.IsEvaluated,r.Message);double V(string name)=>r.Trace.Single(t=>t.Name==name).Value;
        Assert.AreEqual(nrk,V("NRk"),nrk*1e-10);Assert.AreEqual(.5,V("Ncr1.Slenderness"),1e-10);Assert.AreEqual(.8,V("Ncr2.Slenderness"),1e-10);
        double g=ntc?1.05:1,n1=1e5*g/(Chi(.5,.21)*nrk),n2=1e5*g/(Chi(.8,.21)*nrk);
        double k11=.8*(1+.3*n1),k22=.9*(1+.48*n2);if(ntc)k22=Math.Max(k22,.9*(1+.3*n1));
        double m1=1e6*g/V("M1Rk"),m2=5e5*g/V("M2Rk");
        Assert.AreEqual(Math.Max(n1+k11*m1+k22*m2,n2+.8*k11*m1+k22*m2),r.Utilization!.Value,1e-10);
        Assert.AreEqual(1,V("ElasticInteraction"));Assert.IsTrue(V("Weff1Min")<i.Section.CreateSectionCopy().Wel1);
    }
    [DataTestMethod] [DataRow("C")] [DataRow("T")] [DataRow("L")]
    public void AsymmetricClass4CannotSkipCentroidEccentricityInMemberCheck(string family)
    {
        var r=Run(Compression(Input(family,false)));Assert.IsFalse(r.IsEvaluated);
        Assert.AreEqual("Member.Class4NotQualified",r.DiagnosticCode);Assert.IsNull(r.Resistance);
    }
}
