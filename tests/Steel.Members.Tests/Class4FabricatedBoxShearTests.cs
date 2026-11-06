using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class Class4FabricatedBoxShearTests
{
    private const double H=800,B=400,Fy=355,E=210000;
    private const string Supports="Fabricated box with continuous plate junctions and separately checked end diaphragms/force transfer";
    private static double Rho(double width,double thickness) {
        double l=width/thickness/(56.8*Math.Sqrt(235/Fy));
        return l<=.673?1:(l-.22)/(l*l);
    }
    private static double Area(double tf,double tw) {
        double bc=B-2*tw,hc=H-2*tf;
        return H*B-bc*hc-2*bc*tf*(1-Rho(bc,tf))-2*hc*tw*(1-Rho(hc,tw));
    }
    // EN1-5 Table5.1 plus elastic cap, independently evaluated for BOTH thicknesses.
    private static double LineResistance(double c,double t,bool ntc) {
        double tc=5.34*Math.PI*Math.PI*E/(12*.91)*Math.Pow(t/c,2),gm=ntc?1.05:1;
        double lambda=Math.Sqrt(Fy/(Math.Sqrt(3)*tc));
        return t*Math.Min(tc/gm,Math.Min(1,.83/lambda)*Fy/(Math.Sqrt(3)*gm));
    }
    private static SteelVerificationInput Input(bool ntc,double tf,double tw,double sign=1) {
        double flow=Math.Min(LineResistance(B-2*tw,tf,ntc),LineResistance(H-2*tf,tw,ntc));
        var section=new SteelSectionInput(new SteelSection(new SectionRHS(H,B,tf,tf,tw,tw,"fabricated box"),
            new SteelMaterial("S355",E,Fy,510),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished),true);
        var code=ntc?SteelDesignContext.Ntc2018():new SteelDesignContext(SectionResistanceCode.Eurocode3(1,1.25,"test"),1);
        return new(section,new(-.1*Area(tf,tw)*Fy/(ntc?1.05:1),sign*.2*flow*2*(B-2*tw)/1.5,
            -sign*.6*flow*2*(H-2*tf)/1.5,0,0,0),code.WithConservativeClass4ShearInteraction(),new(6000,6000,6000));
    }
    private static SteelShearBucklingAssessment[] Assess(SteelVerificationInput i)=>new[]{1,2}
        .Select(axis=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(i,axis,i.Code.GammaM1!.Value,Supports)).ToArray();
    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true} from sign in new[]{-1d,1d}
        from pair in new[]{(6d,12d),(20d,6d),(10d,8d)} select new object[]{ntc,pair.Item1,pair.Item2,sign};

    [DataTestMethod,DynamicData(nameof(Cases))]
    public void OppositeEqualWallsUseTheirOwnThicknessAndEffectiveWidth(bool ntc,double tf,double tw,double sign)
    {
        var i=Input(ntc,tf,tw,sign);var a=Assess(i);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),a);
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Checks.Select(c=>c.Message)));
        Assert.AreEqual(.9,r.Checks.First().Utilization!.Value,2e-9);
        Assert.AreEqual(Area(tf,tw),r.Checks.First().Trace.Single(t=>t.Name=="AreaEffective").Value,1e-5);
        foreach(var assessment in a) {
            Assert.AreEqual(tf,assessment.Trace.Single(t=>t.Name=="HorizontalWallThickness").Value);
            Assert.AreEqual(tw,assessment.Trace.Single(t=>t.Name=="VerticalWallThickness").Value);
            Assert.AreEqual(Math.Min(LineResistance(B-2*tw,tf,ntc),LineResistance(H-2*tf,tw,ntc)),
                assessment.Trace.Single(t=>t.Name=="AllWallShearFlowResistance").Value,1e-8);
        }
    }

    [DataTestMethod][DataRow(false,6d,12d)][DataRow(true,6d,12d)][DataRow(false,20d,6d)][DataRow(true,20d,6d)]
    public void FabricatedBoxMemberKeepsBothGrossCriticalModes(bool ntc,double tf,double tw)
    {
        var i=Input(ntc,tf,tw);double nrk=Area(tf,tw)*Fy;
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,false,1,1,1,
            "same-case conservative envelope","prismatic box","torsion excluded","FT excluded",
            lateralTorsionalExclusionSource:"closed box").WithConservativeClass4ShearSuperposition();
        var member=SteelMemberAnalysis.BeamColumn(i,"ULS","same input",p,new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,nrk/.36,SteelStabilityUnit.N,"gross mode1"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,nrk/1.44,SteelStabilityUnit.N,"gross mode2")},4,"uniform compression");
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(member),Assess(i)).Checks.Single();
        Assert.IsTrue(r.IsEvaluated,r.Message);
        double phi=(1+.49+1.44)/2,chi=1/(phi+Math.Sqrt(phi*phi-1.44));
        Assert.AreEqual(.1/(.2*chi),r.Utilization!.Value,2e-9);
        Assert.AreEqual(nrk,r.Trace.Single(t=>t.Name=="NRk").Value,1e-5);
    }

    [TestMethod]
    public void DifferentWallThicknessIsNotSilentlyTreatedAsARolledUniformTube()
    {
        var b=CommonSectionCoverageTests.Input(new SectionRHS(H,B,20,20,6,6,"invalid product"),false,fy:Fy);
        Assert.ThrowsException<NotSupportedException>(()=>SteelShearBucklingAssessment.EvaluateClass4PlateShear(b,1,1,Supports));
    }

    // Independent numerical integration of the closed shear-flow differential equation.
    // Zero twist uses integral(q/t ds)=0: an unweighted mean is incorrect for tf != tw.
    [DataTestMethod][DataRow(6d,12d,1d,0d)][DataRow(20d,6d,0d,1d)]
    [DataRow(6d,12d,-1d,1d)][DataRow(20d,6d,1d,-1d)]
    public void NonuniformClosedCellIntegrationRecoversForcesAndStaysInsideTheFlowBound(double tf,double tw,double vx,double vy)
    {
        double b=B-tw,h=H-tf,ix=tf*b*h*h/2+tw*h*h*h/6,iy=tw*h*b*b/2+tf*b*b*b/6;
        var vertices=new[]{(-b/2,-h/2),(b/2,-h/2),(b/2,h/2),(-b/2,h/2),(-b/2,-h/2)};
        var values=new List<(double Q,double Dx,double Dy,double Ds,double T)>();double q=0;
        const int steps=2000;
        for(int edge=0;edge<4;edge++) {
            var start=vertices[edge];var end=vertices[edge+1];double t=edge%2==0?tf:tw;
            double dx=(end.Item1-start.Item1)/steps,dy=(end.Item2-start.Item2)/steps,ds=Math.Sqrt(dx*dx+dy*dy);
            for(int j=0;j<steps;j++) {
                double x=start.Item1+(j+.5)*dx,y=start.Item2+(j+.5)*dy;
                double dq=-(vx*x/iy+vy*y/ix)*t*ds;
                values.Add((q+dq/2,dx,dy,ds,t));q+=dq;
            }
        }
        double correction=values.Sum(v=>v.Q*v.Ds/v.T)/values.Sum(v=>v.Ds/v.T);
        Assert.AreEqual(vx,values.Sum(v=>(v.Q-correction)*v.Dx),1e-6);
        Assert.AreEqual(vy,values.Sum(v=>(v.Q-correction)*v.Dy),1e-6);
        double bound=1.5*(Math.Abs(vx)/(2*(B-2*tw))+Math.Abs(vy)/(2*(H-2*tf)));
        Assert.IsTrue(values.All(v=>Math.Abs(v.Q-correction)<=bound*(1+1e-9)));
    }
}
