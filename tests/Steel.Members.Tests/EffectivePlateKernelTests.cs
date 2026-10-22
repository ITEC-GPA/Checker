using GPC.Checkers.Steel.EffectiveSections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

[TestClass]
public class EffectivePlateKernelTests
{
    [DataTestMethod]
    [DataRow(1d,4d)] [DataRow(.5,5.290322580645161)] [DataRow(0d,7.81)]
    [DataRow(-.5,13.4)] [DataRow(-1d,23.9)] [DataRow(-2d,53.82)] [DataRow(-3d,95.68)]
    public void InternalBucklingFactorsFromJrcTable41(double psi,double k)
    {
        var r=EffectivePlateReduction.InternalPlate(1000,10,-100,-100*psi,355);
        Assert.AreEqual(k,r.KSigma,k*1e-12);Assert.AreEqual(psi,r.Psi,1e-12);
        Assert.IsTrue(r.EffectiveAtStart+r.EffectiveAtEnd<=1000+1e-9);
    }
    [DataTestMethod]
    [DataRow(1d,true,.43)] [DataRow(.5,true,.688095238095238)] [DataRow(0d,true,1.7)]
    [DataRow(-.5,true,8.475)] [DataRow(-1d,true,23.8)]
    [DataRow(1d,false,.43)] [DataRow(.5,false,.4825)] [DataRow(0d,false,.57)]
    [DataRow(-.5,false,.6925)] [DataRow(-1d,false,.85)] [DataRow(-2d,false,1.27)] [DataRow(-3d,false,1.83)]
    public void OutstandFactorsDistinguishMaximumCompressionAtSupportAndFreeEdge(double psi,bool support,double k)
    {
        // JRC EUR22898EN p50, EN1993-1-5 Table4.2. The two orientations
        // have very different k and cannot be merged by swapping stresses.
        var r=EffectivePlateReduction.Outstand(300,6,support?-100:-100*psi,support?-100*psi:-100,355);
        Assert.AreEqual(k,r.KSigma,k*1e-12);Assert.AreEqual(psi,r.Psi,1e-12);
        double bc=psi<0?300/(1-psi):300;
        Assert.AreEqual(bc,r.CompressedWidth,1e-10);
        Assert.AreEqual(r.Rho*bc+300-bc,r.EffectiveAtStart+r.EffectiveAtEnd,1e-10);
        Assert.AreEqual(support?300-bc:0,r.EffectiveAtEnd,1e-10);
    }
    [TestMethod]
    public void IndependentPublishedPlateAndStressScaling()
    {
        // JRC/bridge validation plate 492x8, S235, psi=.406. Independently
        // evaluated values: k=8.2/1.456, lambda=.917..., rho=.867... .
        var a=EffectivePlateReduction.InternalPlate(492,8,-100,-40.6,235);
        double k=8.2/1.456,lambda=61.5/(28.4*Math.Sqrt(k)),rho=(lambda-.055*3.406)/(lambda*lambda);
        Assert.AreEqual(rho,a.Rho,1e-12);
        var b=EffectivePlateReduction.InternalPlate(492,8,-350,-142.1,235);
        Assert.AreEqual(a.Rho,b.Rho,1e-12);
        Assert.AreEqual(a.EffectiveAtStart,EffectivePlateReduction.InternalPlate(492,8,-40.6,-100,235).EffectiveAtEnd,1e-12);
    }
    [TestMethod]
    public void StrongGradientReportsConservativeTableBoundaryWithoutChangingNeutralAxis()
    {
        var r=EffectivePlateReduction.Outstand(600,2,-10,100,355);
        Assert.IsTrue(r.StressRatioBounded);Assert.AreEqual(23.8,r.KSigma,1e-10);
        Assert.AreEqual(600/11d,r.CompressedWidth,1e-10);
        Assert.AreEqual(600-600/11d,r.EffectiveAtEnd,1e-10);
    }
    [DataTestMethod] [DataRow(0d)] [DataRow(100d)]
    public void TensilePlateHasNoRemovedMaterial(double tension)
    {
        var a=EffectivePlateReduction.InternalPlate(800,3,tension,2*tension,355);
        var b=EffectivePlateReduction.Outstand(800,3,tension,2*tension,355);
        Assert.AreEqual(800,a.EffectiveAtStart+a.EffectiveAtEnd);Assert.AreEqual(800,b.EffectiveAtStart+b.EffectiveAtEnd);
    }
    [TestMethod]
    public void BadPlateInputsCannotCreateFiniteLookingResults()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>EffectivePlateReduction.InternalPlate(100,0,-100,-100,355));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>EffectivePlateReduction.Outstand(100,3,double.NaN,355));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>EffectivePlateReduction.InternalPlate(-1,3,-100,-100,355));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>EffectivePlateReduction.InternalPlate(100,3,-100,-100,double.PositiveInfinity));
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FixedPointReturnsTheStateUsedForTheResponse(bool aitken)
    {
        var result=EffectiveSectionIteration.Solve(0d,x=>new[]{x,x+2},r=>r[1]/2,
            (a,b)=>Math.Abs(a-b),(a,b,w)=>(1-w)*a+w*b,tolerance:1e-10,
            coordinates:aitken?x=>new[]{x}:null);
        Assert.AreEqual(2,result.State,4e-10);Assert.AreEqual(result.State,result.Response[0]);
        Assert.IsTrue(result.Residual<1e-10);
    }
    [TestMethod]
    public void NonconvergenceInvalidResidualAndCancellationProduceNoResult()
    {
        Assert.ThrowsException<EffectiveSectionConvergenceException>(()=>EffectiveSectionIteration.Solve(1d,x=>x,x=>-x,
            (a,b)=>Math.Abs(a-b),(a,b,w)=>b,maximumIterations:3));
        Assert.ThrowsException<InvalidOperationException>(()=>EffectiveSectionIteration.Solve(1d,x=>x,x=>x,(a,b)=>double.NaN,(a,b,w)=>b));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(()=>EffectiveSectionIteration.Solve(1d,x=>x,x=>x,(a,b)=>0,(a,b,w)=>b,cancellation:cancellation.Token));
    }
}
