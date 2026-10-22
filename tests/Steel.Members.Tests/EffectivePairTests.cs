using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class EffectivePairTests
{
    internal static SteelVerificationInput Input(string family,bool ntc,SteelActions actions)
    {
        var pair=family=="C"?SectionBuiltUp.DoubleChannel(new SectionC(300,3,120,3,120,3,"C"),8,true,"2C"):
            SectionBuiltUp.DoubleAngle(new SectionL(100,4,150,4,"L"),8,family=="L-long"?SectionBuiltUp.AnglePairJoinedLeg.Long:SectionBuiltUp.AnglePairJoinedLeg.Short,"2L");
        return new(new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S355",210000,355,510)),true),actions,
            ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),new SteelMemberInput(3000,3000,3000));
    }
    public static IEnumerable<object[]> Cases()
    {foreach(var f in new[]{"C","L-long","L-short"})foreach(bool ntc in new[]{false,true})foreach(int load in Enumerable.Range(0,5))yield return new object[]{f,ntc,load};}
    [DataTestMethod] [DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void EffectivePairSolvesAllNormalStatesInBothCodes(string family,bool ntc,int load)
    {
        var actions=new[]{new SteelActions(-10000,0,0,0,0,0),new SteelActions(0,0,0,0,1e5,0),new SteelActions(0,0,0,0,0,1e5),
            new SteelActions(-10000,0,0,0,1e5,-1e5),new SteelActions(10000,0,0,0,-1e5,1e5)};
        var input=Input(family,ntc,actions[load]);var solved=SteelEffectiveSectionSolver.Solve(input);
        Assert.IsTrue(solved.Residual<1e-8);Assert.IsTrue(solved.State.Area>0&&solved.State.Area<=input.Section.Area);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(input)).Checks.Single(c=>c.Decision.Target==SteelCheckTarget.NormalResistance);
        Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(solved.Utilization,r.Utilization!.Value,1e-10);
        Assert.AreEqual(SteelMethod.EffectiveNormalResistance,r.Decision.Method);
    }

    [DataTestMethod] [DataRow("C")] [DataRow("L-long")] [DataRow("L-short")]
    public void UniformEffectiveIntegralsMatchIndependentRetainedRectangles(string family)
    {
        var input=Input(family,false,new SteelActions(-10000,0,0,0,0,0));
        var state=SteelEffectiveSectionSolver.UniformCompression(input.Section);
        double eps=Math.Sqrt(235d/355);
        double Out(double b,double t) {double l=b/(28.4*t*eps*Math.Sqrt(.43));return l<=.748?1:(l-.188)/(l*l);}
        var rectangles=new List<(double x,double y,double b,double h)>();
        if(family=="C") {
            double l=294/(28.4*3*eps*2),rho=(l-.22)/(l*l),half=294*rho/2,width=3+117*Out(117,3);
            rectangles.Add((0,0,width,3));rectangles.Add((0,297,width,3));
            rectangles.Add((0,3,3,half));rectangles.Add((0,297-half,3,half));
        } else {
            double rho=Out(150,4);
            rectangles.Add((0,0,4,4));rectangles.Add((4,0,100*rho-4,4));rectangles.Add((0,4,4,150*rho-4));
        }
        var pair=(SectionBuiltUp)input.Section.CreateSectionCopy().SectionShape;
        var placed=new List<(double x,double y,double b,double h)>();
        foreach(var p in pair.Parts)foreach(var r in rectangles) {
            // Independent affine transform of rectangle centres; axis reflection leaves local inertias unchanged.
            double x=p.X+(p.MirrorX?-1:1)*(r.x+r.b/2),y=p.Y+(p.MirrorY?-1:1)*(r.y+r.h/2);
            placed.Add((x,y,r.b,r.h));
        }
        double a=placed.Sum(r=>r.b*r.h),xg=placed.Sum(r=>r.b*r.h*r.x)/a,yg=placed.Sum(r=>r.b*r.h*r.y)/a;
        double ix=placed.Sum(r=>r.b*Math.Pow(r.h,3)/12+r.b*r.h*Math.Pow(r.y-yg,2));
        double iy=placed.Sum(r=>r.h*Math.Pow(r.b,3)/12+r.b*r.h*Math.Pow(r.x-xg,2));
        double xy=placed.Sum(r=>r.b*r.h*(r.x-xg)*(r.y-yg));
        Assert.AreEqual(a,state.Area,a*1e-11);Assert.AreEqual(xg,state.X,1e-10);Assert.AreEqual(yg,state.Y,1e-10);
        Assert.AreEqual(ix,state.Ixx,ix*1e-10);Assert.AreEqual(iy,state.Iyy,iy*1e-10);Assert.AreEqual(xy,state.Ixy,1e-7);
    }
    [TestMethod]
    public void EffectivePairSectionDoesNotImplyQualifiedClass4Member()
    {
        var i=Input("C",false,new SteelActions(-10000,0,0,0,0,0));
        var packing=new SteelPackedPairAssessment(i,100,8,8,6000,1e5,0,1e5,1000,1e5,"detail","local model","external resistances");
        var a=SteelMemberAnalysis.PackedCompression(packing,"N","curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"T",flexuralTorsionalExclusionSource:"FT");
        Assert.AreEqual(SteelExecutionStatus.NotSupported,SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single().Status);
    }
}
