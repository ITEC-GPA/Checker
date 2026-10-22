using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class EffectiveSectionSolverTests
{
    internal static Section Shape(string family)=>family switch {
        "H"=>new SectionH(600,5,250,8,250,8,"slender H"),
        "C"=>new SectionC(400,4,100,5,100,5,"slender C"),
        "T"=>new SectionT(200,300,4,5,"slender T"),
        "L"=>new SectionL(120,4,180,4,"slender L"),
        "RHS"=>new SectionRHS(400,200,4,4,4,4,"slender RHS"),
        "RoundedRHS"=>new SectionRHSRoundedCorners(400,200,4,6,2,"slender rounded RHS"),
        _=>throw new ArgumentException(family)};
    private static SteelVerificationInput Input(string family,bool ntc=false,double n=-1e5,double m1=0,double m2=0)
        =>CommonSectionCoverageTests.Input(Shape(family),ntc,n,m1,m2,355);
    private static SteelCheckExecution Run(SteelVerificationInput input)=>SteelVerificationPipeline.Execute(
        SteelMethodSelector.Select(input)).Checks.First();

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void UniformRhsCompressionMatchesTable41AndIndependentFourRectangleArea(bool ntc)
    {
        // EN1993-1-5 4.4, Table4.1: psi=1, k=4; direct addition of retained walls
        // and four 4x4 corners, independent of production's gross subtraction.
        double Rho(double b) {double l=b/4/(56.8*Math.Sqrt(235d/355));return Math.Min(1,(l-.22)/(l*l));}
        double expected=4*16+2*4*(Rho(392)*392+Rho(192)*192);
        var input=Input("RHS",ntc);var r=SteelEffectiveSectionSolver.Solve(input);
        Assert.AreEqual(expected,r.State.Area,expected*2e-8);Assert.AreEqual(0,r.State.CentroidShiftX,1e-9);Assert.AreEqual(0,r.State.CentroidShiftY,1e-9);
        Assert.AreEqual(1e5/(expected*355/(ntc?1.05:1)),r.Utilization,1e-8);
        Assert.AreEqual(expected,SteelEffectiveSectionSolver.UniformCompression(input.Section).Area,1e-8);
        var check=Run(input);Assert.AreEqual(SteelMethod.EffectiveNormalResistance,check.Decision.Method);
        Assert.AreEqual(r.Utilization,check.Utilization!.Value,1e-12);Assert.IsTrue(check.IsEvaluated);
        Assert.IsTrue(check.Trace.Any(t=>t.Name=="EffectiveIxy"));StringAssert.Contains(check.Reference,ntc?"NTC 2018":"EN1993");
    }

    [TestMethod]
    public void SymmetricHCompressionMatchesTables41And42()
    {
        double lw=584d/5/(56.8*Math.Sqrt(235d/355)),lf=122.5/8/(28.4*Math.Sqrt(235d/355)*Math.Sqrt(.43));
        double rw=(lw-.22)/(lw*lw),rf=(lf-.188)/(lf*lf);
        double expected=584*5*rw+4*122.5*8*rf+2*5*8;
        var r=SteelEffectiveSectionSolver.Solve(Input("H"));Assert.AreEqual(expected,r.State.Area,expected*2e-8);
        Assert.AreEqual(0,r.State.CentroidShiftX,1e-9);Assert.AreEqual(0,r.State.CentroidShiftY,1e-9);
    }

    [DataTestMethod] [DataRow(275d,90.7)] [DataRow(355d,86.9)]
    public void PublishedSciP363Ub610x178x82EffectiveArea(double fy,double expectedCm2)
    {
        // SCI P363 (2015 reprint), PDF68/B-2 dimensions, PDF132/B-66
        // effective areas. Table gross area is rounded to whole cm2 (104), while
        // effective area has 0.1 cm2 precision. Check the inactive area separately
        // against that printed gross reference, without changing Model geometry.
        var input=CommonSectionCoverageTests.Input(new SectionH(598.6,10,177.9,12.8,177.9,12.8,"UB610x178x82",12.7),false,-1e5,fy:fy);
        var r=SteelEffectiveSectionSolver.Solve(input);
        Assert.AreEqual(10400,input.Section.Area,50);
        Assert.AreEqual(expectedCm2*100,10400-(input.Section.Area-r.State.Area),5);
        Assert.AreEqual(expectedCm2*100,r.State.Area,55); // 50 gross + 5 effective rounding
        Assert.AreEqual(r.State.Area,SteelEffectiveSectionSolver.UniformCompression(input.Section).Area,1e-3);
    }
    [DataTestMethod] [DataRow(120d,120d)] [DataRow(120d,180d)]
    public void SciP363AngleAreaUsesFullLegsAndCommonLongLegSlenderness(double b,double h)
    {
        // SCI P363 A-10/A-11: Aeff=A-(1-rho)*(h+b)*t, with common rho for
        // the unequal angle. EN1993-1-5 4.4(2) uses h in the slenderness of both legs.
        const double t=4;double l=h/t/(28.4*Math.Sqrt(235d/355)*Math.Sqrt(.43)),rho=(l-.188)/(l*l);
        double expected=(h+b)*t-t*t-(1-rho)*(h+b)*t;
        var input=CommonSectionCoverageTests.Input(new SectionL(b,t,h,t,"angle"),false,fy:355);
        var r=SteelEffectiveSectionSolver.UniformCompression(input.Section);
        Assert.AreEqual(expected,r.Area,1e-9);
        Assert.IsTrue(r.Geometry.Plates.All(p=>p.BucklingWidth==h));
    }

    public static IEnumerable<object[]> Cases=>from family in new[]{"H","C","T","L","RHS","RoundedRHS"}
        from ntc in new[]{false,true} from action in new[]{0,1,2,3,4} select new object[]{family,ntc,action};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void EveryQualifiedFamilyConvergesForCompressionBothBendingAxesAndCombinedActions(string family,bool ntc,int action)
    {
        var input=Input(family,ntc,n:action==0?-1e5:action==3?-1e4:action==4?1e4:0,
            m1:action==1||action>=3?1e6:0,m2:action==2||action>=3?-5e5:0);
        var r=SteelEffectiveSectionSolver.Solve(input);Assert.IsTrue(r.Residual<1e-8);
        Assert.IsTrue(r.State.Area<=input.Section.Area*(1+1e-12));Assert.IsTrue(r.State.Area>0);
        for(int i=0;i<r.Plates.Count;i++) {
            var p=r.State.Geometry.Plates[i];var reduction=r.Plates[i];
            double retained=Math.Max(0,p.Length-reduction.CompressedWidth*(1-reduction.Rho));
            Assert.AreEqual(retained,r.State.Widths[2*i]+r.State.Widths[2*i+1],p.Length*2.1e-8);
            Assert.AreEqual(r.State.Stress(p.StartX,p.StartY,input.Actions.N,input.Actions.M1,input.Actions.M2),reduction.StartStress,1e-10);
        }
        var check=Run(input);Assert.IsTrue(check.IsEvaluated,check.DiagnosticCode+": "+check.Message);
        Assert.AreEqual(r.Utilization,check.Utilization!.Value,1e-12);
        // Same physical geometry under the two standards; only gammaM0 changes.
        var ec=SteelEffectiveSectionSolver.Solve(new SteelVerificationInput(input.Section,input.Actions,CommonSectionCoverageTests.Input(Shape(family),false).Code));
        Assert.AreEqual(ec.State.Area,r.State.Area,1e-8);Assert.AreEqual(ec.Utilization*(ntc?1.05:1),r.Utilization,1e-10);
    }

    [DataTestMethod] [DataRow(1e-15)] [DataRow(1e-6)] [DataRow(1e6)]
    public void WidthsAreIndependentOfPositiveLoadMagnitude(double factor)
    {
        var a=SteelEffectiveSectionSolver.Solve(Input("C",n:-1e4,m1:1e6,m2:5e5));
        var b=SteelEffectiveSectionSolver.Solve(Input("C",n:-1e4*factor,m1:1e6*factor,m2:5e5*factor));
        Assert.AreEqual(a.State.Area,b.State.Area,1e-6);Assert.AreEqual(a.Utilization,b.Utilization/factor,1e-9);
    }
    [DataTestMethod] [DataRow("H")] [DataRow("C")] [DataRow("T")] [DataRow("L")] [DataRow("RHS")] [DataRow("RoundedRHS")]
    public void UniformTensionDoesNotDiscardMaterial(string family)
    {
        var input=Input(family,n:1e5);var r=SteelEffectiveSectionSolver.Solve(input);
        Assert.AreEqual(input.Section.Area,r.State.Area,1e-9);Assert.AreEqual(1e5/(input.Section.Area*355),r.Utilization,1e-10);
    }
    [TestMethod]
    public void ReversalMirrorsSymmetricEffectiveBendingSection()
    {
        var a=SteelEffectiveSectionSolver.Solve(Input("RHS",n:0,m1:1e7));
        var b=SteelEffectiveSectionSolver.Solve(Input("RHS",n:0,m1:-1e7));
        Assert.AreEqual(a.State.Area,b.State.Area,1e-7);Assert.AreEqual(a.State.CentroidShiftY,-b.State.CentroidShiftY,1e-7);
        Assert.AreEqual(a.Utilization,b.Utilization,1e-9);
    }
    [TestMethod]
    public void NonConvergenceAndCancellationNeverReturnAResistance()
    {
        Assert.ThrowsException<EffectiveSectionConvergenceException>(()=>SteelEffectiveSectionSolver.Solve(Input("C"),maximumIterations:1));
        Assert.ThrowsException<OperationCanceledException>(()=>SteelEffectiveSectionSolver.Solve(Input("C"),cancellation:new CancellationToken(true)));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>SteelEffectiveSectionSolver.Solve(Input("H"),tolerance:double.NaN));
        var r=Run(Input("RHS",n:-1e9));Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);Assert.IsTrue(r.Utilization>1);
    }
    [TestMethod]
    public void UnqualifiedMaterialShellAndShearAreExplicit()
    {
        var section=new SteelSectionInput(new SteelSection(Shape("H"),new SteelMaterial("nonstandard",200000,355,550)),true);
        Assert.ThrowsException<NotSupportedException>(()=>SteelEffectiveSectionSolver.Qualify(section));
        Assert.ThrowsException<NotSupportedException>(()=>SteelEffectiveSectionSolver.Qualify(CommonSectionCoverageTests.Input(new SectionCHS(500,2,"shell"),false).Section));
        var input=Input("RHS");var shear=new SteelVerificationInput(input.Section,new SteelActions(-1e5,0,1,0,0,0),input.Code);
        var result=Run(shear);Assert.IsFalse(result.IsEvaluated);Assert.AreEqual("Class4.ShearInteractionNotQualified",result.DiagnosticCode);
    }
}
