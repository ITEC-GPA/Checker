using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class Class4PlateShearSuperpositionTests
{
    private const double Fy=355, E=210000, H=800, B=300, T=6;
    private const string Supports="Continuous plate junctions; end diaphragms, force transfer and attachments separately verified for this state";
    private static double Rho(double width) {
        double l=width/T/(56.8*Math.Sqrt(235/Fy));
        return Math.Min(1,(l-.22)/(l*l));
    }
    // Independent EN1993-1-5 Table4.1 uniform-compression widths; corner squares retained.
    private static double Area()=>2*T*(H+B-2*T)-2*T*((H-2*T)*(1-Rho(H-2*T))+(B-2*T)*(1-Rho(B-2*T)));
    private static double Tau(bool ntc,double gm1) {
        double b=Math.Max(H-2*T,B-2*T);
        double tc=5.34*Math.PI*Math.PI*E/(12*(1-.3*.3))*Math.Pow(T/b,2);
        double l=Math.Sqrt(Fy/(Math.Sqrt(3)*tc));
        return Math.Min(Math.Min(Fy/(Math.Sqrt(3)*(ntc?1.05:1)),Math.Min(1,.83/l)*Fy/(Math.Sqrt(3)*gm1)),tc/gm1);
    }
    private static double R(int axis,bool ntc,double gm1)=>Tau(ntc,gm1)*2*(axis==1?B-2*T:H-2*T)*T/1.5;
    private static SteelVerificationInput Input(bool ntc,double r1,double r2,double nRatio=.1,double m1=0,double m2=0,double? gm1=null)
    {
        double gm=gm1??(ntc?1.05:1);
        var b=CommonSectionCoverageTests.Input(new SectionRHS(H,B,T,T,T,T,"class4 sharp box"),ntc,fy:Fy);
        var code=new SteelDesignContext(b.Code.SectionCode,gm).WithConservativeClass4ShearInteraction();
        return new(b.Section,new(-nRatio*Area()*Fy/(ntc?1.05:1),r1*R(1,ntc,gm),r2*R(2,ntc,gm),0,m1,m2),code,new(6000,6000,6000));
    }
    private static SteelShearBucklingAssessment[] Assess(SteelVerificationInput i)=>new[]{1,2}
        .Where(a=>(a==1?i.EffectiveActions.V1:i.EffectiveActions.V2)!=0)
        .Select(a=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,a,i.Code.GammaM1!.Value,Supports)).ToArray();

    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true}
        from r in new[]{.2,.6,.85,1.1} from sign1 in new[]{-1d,1d} from sign2 in new[]{-1d,1d}
        select new object[]{ntc,r,sign1,sign2};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void BoxSectionMatchesIndependentPlateAndShearFlowBounds(bool ntc,double r2,double sign1,double sign2)
    {
        var i=Input(ntc,.1*sign1,r2*sign2);var assessments=Assess(i);
        var result=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),assessments);
        Assert.IsTrue(result.IsComplete,string.Join(";",result.Checks.Select(c=>c.Message)));
        var normal=result.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.NormalResistance);
        Assert.AreEqual(.2+r2,normal.Utilization!.Value,2e-9);
        Assert.AreEqual(Area(),normal.Trace.Single(t=>t.Name=="AreaEffective").Value,1e-5);
        foreach(var a in assessments) {
            Assert.AreEqual(R(a.Axis,ntc,ntc?1.05:1),a.BucklingResistance!.Value,1e-7);
            Assert.AreEqual(4d,a.Trace.Single(t=>t.Name=="CheckedWalls").Value);
        }
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void MemberPreservesOriginalEffectiveAreaCriticalForcesAndBucklingCurve(bool ntc)
    {
        var i=Input(ntc,.2,.5,nRatio:.03);double nrk=Area()*Fy;
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,false,1,1,1,
            "Conservative same-state maxima","Prismatic member","torsion excluded","FT excluded",
            lateralTorsionalExclusionSource:"Closed box and load through shear centre").WithConservativeClass4ShearSuperposition();
        var member=SteelMemberAnalysis.BeamColumn(i,"one case","same member",p,new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,nrk/.36,SteelStabilityUnit.N,"gross critical force"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,nrk/1.44,SteelStabilityUnit.N,"gross critical force")},4,"uniform class4");
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(member),Assess(i)).Checks.Single();
        Assert.IsTrue(r.IsEvaluated,r.Message);
        double phi=(1+.49*(1.2-.2)+1.44)/2,chi=1/(phi+Math.Sqrt(phi*phi-1.44));
        Assert.AreEqual(.03/(.3*chi),r.Utilization!.Value,2e-9);
        Assert.AreEqual(nrk,r.Trace.Single(t=>t.Name=="NRk").Value,1e-5);
        Assert.AreEqual(.3,r.Trace.Single(t=>t.Name=="Class4PlateInteraction.NormalResistanceFactor").Value,1e-12);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void BiaxialBendingRemainsConcomitantAndSignsRespectDoubleSymmetry(bool ntc)
    {
        double? reference=null;
        foreach(int sign1 in new[]{-1,1})foreach(int sign2 in new[]{-1,1}) {
            var i=Input(ntc,.1,.3,nRatio:.3,m1:sign1*1e6,m2:sign2*1e6);
            var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),Assess(i)).Checks.First();
            Assert.IsTrue(r.IsEvaluated,r.Message);
            Assert.IsTrue(r.Utilization>.7);
            if(reference.HasValue)Assert.AreEqual(reference.Value,r.Utilization!.Value,1e-8);
            reference=r.Utilization;
        }
    }

    [TestMethod]
    public void BridgePartialFactorChangesOnlyThePlateResistanceAndRemainsExplicit()
    {
        var i=Input(true,.2,.5,gm1:1.10);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),Assess(i)).Checks.First();
        Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(.8,r.Utilization!.Value,2e-9);
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,1,1.05,Supports));
    }

    [TestMethod]
    public void ExternalFlagsMissingAxesAndDifferentSnapshotsCannotQualifyTheInteraction()
    {
        var i=Input(false,.2,.5);var plan=SteelMethodSelector.Select(i);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan).IsComplete);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan,Assess(i).Take(1)).IsComplete);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan,new[]{1,2}.Select(a=>SteelShearBucklingAssessment.FromExternal(i,a,true,"External exclusion"))).IsComplete);
        Assert.ThrowsException<ArgumentException>(()=>SteelVerificationPipeline.Execute(plan,Assess(Input(false,.2,.5))));
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,1,1,""));
    }

    [TestMethod]
    public void UnequalWallsAppliedTorqueAndRoundedCornersRemainOutsideThisModel()
    {
        foreach(var shape in new Section[]{new SectionRHS(H,B,T,2*T,T,T,"unequal walls"),
            new SectionRHSRoundedCorners(H,B,T,2*T,T,"rounded")}) {
            var b=CommonSectionCoverageTests.Input(shape,false,fy:Fy);
            var i=new SteelVerificationInput(b.Section,new(-1000,1000,1000,0,0,0),b.Code.WithConservativeClass4ShearInteraction());
            Assert.ThrowsException<NotSupportedException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,1,1,Supports));
        }
        var ordinary=Input(false,.2,.5);
        var torque=new SteelVerificationInput(ordinary.Section,new(-1000,1000,1000,100000,0,0),ordinary.Code);
        Assert.ThrowsException<NotSupportedException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(torque,1,1,Supports));
    }

    [TestMethod]
    public void HUsesPeakShearInBothFlangesAndWebAndKeepsTheModeOptIn()
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(840,6,300,20,300,20,"fabricated H"),false,fy:Fy);
        var i=new SteelVerificationInput(b.Section,new(-100000,10000,30000,0,1e6,1e6),b.Code.WithConservativeClass4ShearInteraction());
        var a=Assess(i);double j22=2*20*Math.Pow(300,3)/12+800*Math.Pow(6,3)/12;
        Assert.AreEqual(Fy/Math.Sqrt(3)*8*j22/(300*300),a[0].BucklingResistance!.Value,1e-7);
        double j11=2*(300*Math.Pow(20,3)/12+300*20*410*410)+6*Math.Pow(800,3)/12;
        double qmax=300*20*410+6*400*200;
        double tc=5.34*Math.PI*Math.PI*E/(12*(1-.3*.3))*Math.Pow(6d/800,2);
        // This slender web is governed by the elastic bifurcation cap, with
        // Jourawski's peak stress rather than a uniform average stress.
        Assert.AreEqual(tc*6*j11/qmax,a[1].BucklingResistance!.Value,1e-7);
        Assert.AreSame(a[1],a[1].WithConservativeBiaxialNormalInteraction());
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),a);
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Checks.Select(c=>c.Message)));
        Assert.IsFalse(b.Code.ConservativeClass4ShearInteraction);
        Assert.IsTrue(i.Code.WithNegligibleActions(SteelNegligibleActionPolicy.Default).ConservativeClass4ShearInteraction);
    }

    [TestMethod]
    public void AsymmetricHMinorShearCannotSuppressItsShearCentreEccentricity()
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(840,6,300,20,250,20,"unequal flanges"),false,fy:Fy);
        var i=new SteelVerificationInput(b.Section,new(-100000,10000,30000,0,1e6,1e6),b.Code.WithConservativeClass4ShearInteraction());
        Assert.ThrowsException<NotSupportedException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,1,1,Supports));
        var aligned=new SteelVerificationInput(b.Section,new(-100000,0,30000,0,1e6,1e6),i.Code);
        Assert.IsTrue(SteelVerificationPipeline.Execute(SteelMethodSelector.Select(aligned),Assess(aligned)).IsComplete);
    }

    [DataTestMethod][DataRow(.8)][DataRow(1d)][DataRow(1.1)]
    public void ExhaustedCombinedShearBudgetFailsWithoutNonFiniteResults(double ratio)
    {
        var i=Input(false,.2,ratio);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),Assess(i));
        Assert.IsTrue(r.IsComplete);
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Checks.First().Status);
        Assert.IsTrue(r.Checks.SelectMany(c=>c.Trace).All(t=>double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void CompactSectionsOptingIntoPlateBoundsCannotFallBackToHigherPlasticResistance()
    {
        var b=CommonSectionCoverageTests.Input(new SectionRHS(200,100,10,10,10,10,"compact box"),false,fy:Fy);
        var i=new SteelVerificationInput(b.Section,new(-10000,5000,10000,0,0,0),b.Code.WithConservativeClass4ShearInteraction());
        var a=Assess(i);var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),a).Checks.First();
        Assert.IsTrue(r.IsEvaluated,r.Message);
        Assert.AreEqual(SteelMethod.EffectiveNormalResistance,r.Decision.Method);
        double normal=10000/(5600*Fy);
        Assert.AreEqual(normal+5000/a[0].BucklingResistance!.Value+10000/a[1].BucklingResistance!.Value,r.Utilization!.Value,1e-10);
    }

    // Independent closed-cell beam solution: integrate dq/ds=-(Vx*x/Iy+Vy*y/Ix)t
    // around the median rectangle, then impose integral(q/t ds)=0. The library
    // uses neither this integration nor the elastic inertias in its RHS bound.
    [DataTestMethod][DataRow(300d,800d,1d,0d)][DataRow(300d,800d,0d,1d)]
    [DataRow(800d,300d,1d,1d)][DataRow(100d,2000d,-1d,1d)][DataRow(1000d,1000d,1d,-1d)]
    public void IndependentClosedCellIntegrationBoundsEveryWallAndRecoversBothForces(double b,double h,double vx,double vy)
    {
        const double t=2;double bm=b-t,hm=h-t;
        double ix=t*(bm*hm*hm/2+hm*hm*hm/6),iy=t*(hm*bm*bm/2+bm*bm*bm/6);
        var vertices=new[]{(-bm/2,-hm/2),(bm/2,-hm/2),(bm/2,hm/2),(-bm/2,hm/2),(-bm/2,-hm/2)};
        var values=new List<(double Q,double Dx,double Dy,double Ds)>();double q=0;
        const int steps=1000;
        for(int edge=0;edge<4;edge++) {
            var start=vertices[edge];var end=vertices[edge+1];
            double dx=(end.Item1-start.Item1)/steps,dy=(end.Item2-start.Item2)/steps,ds=Math.Sqrt(dx*dx+dy*dy);
            for(int j=0;j<steps;j++) {
                double x=start.Item1+(j+.5)*dx,y=start.Item2+(j+.5)*dy;
                double dq=-(vx*x/iy+vy*y/ix)*t*ds;
                values.Add((q+dq/2,dx,dy,ds));q+=dq;
            }
        }
        double correction=values.Sum(v=>v.Q*v.Ds)/values.Sum(v=>v.Ds);
        Assert.AreEqual(vx,values.Sum(v=>(v.Q-correction)*v.Dx),2e-6);
        Assert.AreEqual(vy,values.Sum(v=>(v.Q-correction)*v.Dy),2e-6);
        double bound=1.5*(Math.Abs(vx)/(2*(b-2*t)*t)+Math.Abs(vy)/(2*(h-2*t)*t));
        Assert.IsTrue(values.All(v=>Math.Abs(v.Q-correction)/t<=bound*(1+1e-9)));
    }
}
