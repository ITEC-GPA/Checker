using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedIBeamSplicePlateTests
{
    internal static WeldedIBeamSpliceForceInput Detail(PlanarConnectionActions? actions=null, double innerWidth=140,
        double outerThickness=20, double webThickness=12, double first=70)
        => new(new(new(400,30,355,510),new(320,16,355,510),new(400,30,355,510),8,8),
            new(new(360,outerThickness,355,510),new(innerWidth,20,355,510),40,60,new(3,2,70,180,first)),
            new(new(360,20,355,510),new(140,20,355,510),40,60,new(3,2,70,180,first)),
            new(280,webThickness,355,510),new(3,3,70,90,first),60,10,actions??new(1e6,0,0,0,0,0));
    internal static WeldedIBeamSplicePlateInput Input(bool ntc=false,WeldedIBeamSpliceForceInput? detail=null)
    {
        var b=WeldedIBeamSpliceTests.BoltInput(ntc,detail??Detail());
        return new(ntc?IBeamSplicePlatePolicy.Ntc2018():IBeamSplicePlatePolicy.Eurocode3(b.Code,1),b);
    }
    static void Close(double e,double a)=>WeldedIBeamSpliceTests.Close(e,a,1e-10);
    static SpliceCoverPlateAssessment Cover(WeldedIBeamSplicePlateResult r,WeldedSpliceCover cover)=>r.Covers.Single(c=>c.Cover==cover);
    static double Trace(ConnectionVerification<WeldedIBeamSplicePlateInput> r,string name)=>r.Trace.Single(t=>t.Name==name).Value;

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void AllEightActualCoversAreAssessedAndNoComponentClaimsACompleteJoint(bool ntc)
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(ntc));
        Assert.AreEqual(0,r.Diagnostics.Count,string.Join(";",r.Diagnostics));Assert.AreEqual(8,r.Covers.Count);
        foreach(var c in r.Covers)
        {
            Assert.IsTrue(c.Sections.IsComplete,string.Join(";",c.Sections.Diagnostics));
            Assert.IsTrue(c.LongitudinalCompression.IsComplete,string.Join(";",c.LongitudinalCompression.Diagnostics));
            Assert.IsTrue(c.AxialBlockTearing.IsComplete,string.Join(";",c.AxialBlockTearing.Diagnostics));
        }
        Assert.IsFalse(r.CanQualifyWholeJoint);Assert.IsTrue(r.PendingChecks.Contains("GENERAL_NVM_BLOCK_TEARING"));
    }
    [DataTestMethod]
    [DataRow(false,WeldedSpliceCover.TopOuter,7200d,6160d)]
    [DataRow(false,WeldedSpliceCover.TopInnerPositive,2800d,2280d)]
    [DataRow(false,WeldedSpliceCover.BottomInnerNegative,2800d,2280d)]
    [DataRow(false,WeldedSpliceCover.WebPositive,3360d,2424d)]
    [DataRow(true,WeldedSpliceCover.TopOuter,7200d,6160d)]
    [DataRow(true,WeldedSpliceCover.TopInnerPositive,2800d,2280d)]
    [DataRow(true,WeldedSpliceCover.BottomInnerNegative,2800d,2280d)]
    [DataRow(true,WeldedSpliceCover.WebPositive,3360d,2424d)]
    public void IndependentRectangularAreasAndStrengths(bool ntc,WeldedSpliceCover cover,double area,double net)
    {
        var c=Cover(WeldedIBeamSplicePlates.Verify(Input(ntc)),cover);double gamma0=ntc?1.05:1;
        Close(area,Trace(c.Sections,"GrossArea"));Close(net,Trace(c.Sections,"NetArea"));
        Close(area*355/gamma0,c.Sections.Checks.First(x=>x.Id.EndsWith(".GrossTension")).Resistance);
        Close(.9*net*510/1.25,c.Sections.Checks.First(x=>x.Id.EndsWith(".NetAxialTension")).Resistance);
        Close(area*355/(1.27*Math.Sqrt(3)*gamma0),c.Sections.Checks.First(x=>x.Id.EndsWith(".GrossShear")).Resistance);
        Close(net*510/(Math.Sqrt(3)*1.25),c.Sections.Checks.First(x=>x.Id.EndsWith(".NetShear")).Resistance);
    }
    [TestMethod]
    public void AxialCoverCutsHaveKnownStaircaseAndUnloadedEnds()
    {
        var c=Cover(WeldedIBeamSplicePlates.Verify(Input()),WeldedSpliceCover.TopOuter);
        // Parent A=29120, each flange A=12000. Equal face offsets split the flange force in half.
        double n=1e6*12000/29120/2;
        Close(0,c.Cuts.First().N);Close(0,c.Cuts.Last().N);
        foreach(var item in new[]{(-210d,0,0d),(-210d,1,n/3),(-140d,0,n/3),(-140d,1,2*n/3),(-70d,1,n),(0d,-1,n),(70d,1,2*n/3),(210d,1,0d)})
        {
            var cut=c.Cuts.Single(x=>x.X==item.Item1&&x.ColumnLimit==item.Item2);Close(item.Item3,cut.N);Close(0,cut.V);Close(0,cut.M);
        }
    }
    [TestMethod]
    public void RectangularBlockPathsMatchIndependentFirstAndFullLigaments()
    {
        var r=Cover(WeldedIBeamSplicePlates.Verify(Input()),WeldedSpliceCover.TopOuter).AxialBlockTearing;
        // First longitudinal prefix, lower-edge L: Ant=(90-26/2)*20=1540;
        // Anv=(60-26/2)*20=940. EC eccentric expression is used even for centred layouts.
        var first=r.Checks.Single(c=>c.Id=="Left.Path1.LowerEdge");
        Close(1540,Trace(r,"Left.Path1.LowerEdge.Ant"));Close(940,Trace(r,"Left.Path1.LowerEdge.Anv"));
        Close(.5*1540*510/1.25+940*355/Math.Sqrt(3),first.Resistance);
        Close(1e6*12000/29120/2/6,first.Demand);
        Assert.IsTrue(r.Trace.Any(t=>t.Name.EndsWith(".Anv")&&Math.Abs(t.Value-2*(60+140-2.5*26)*20)<1e-9));
        Assert.IsTrue(r.Checks.Count>12); // Every prefix and edge, not just the longest two paths.
    }
    [DataTestMethod][DataRow(1d)][DataRow(-1d)]
    public void CompressionHasNoFictitiousTensionAndUsesTheSamePhysicalSignOnBothEnds(double sign)
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(sign*1e6,0,0,0,0,0))));
        var c=Cover(r,WeldedSpliceCover.TopOuter);
        Close(sign<0?1e6*12000/29120/2/7200:0,Trace(c.LongitudinalCompression,"MaximumCompression"));
        if(sign<0)Assert.IsTrue(c.AxialBlockTearing.Checks.All(x=>x.Demand==0));
        var left=c.AxialBlockTearing.Checks.Where(x=>x.Id.StartsWith("Left.")).Select(x=>x.Demand).ToArray();
        var right=c.AxialBlockTearing.Checks.Where(x=>x.Id.StartsWith("Right.")).Select(x=>x.Demand).ToArray();
        CollectionAssert.AreEqual(left,right);
    }
    [TestMethod]
    public void ThinCoverCompressionMatchesIndependentEulerSlendernessAndCurveC()
    {
        var c=Cover(WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(-1e6,0,0,0,0,0),outerThickness:10))),WeldedSpliceCover.TopOuter);
        double lambda=.6*140/(Math.PI*10/Math.Sqrt(12))*Math.Sqrt(355d/210000);
        double phi=.5*(1+.49*(lambda-.2)+lambda*lambda),chi=1/(phi+Math.Sqrt(phi*phi-lambda*lambda));
        Close(lambda,Trace(c.LongitudinalCompression,"Slenderness"));Close(chi,Trace(c.LongitudinalCompression,"Chi"));
        Close(chi*355,c.LongitudinalCompression.Checks.Single(x=>x.Id.EndsWith("InterFastenerBuckling")).Resistance);
    }
    [DataTestMethod][DataRow(1d)][DataRow(1e-12)]
    public void EccentricInnerStripsRetainLocalBendingAndDoNotAcquireAxialBlockQualification(double scale)
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(scale*1e6,0,0,0,0,0),innerWidth:160)));
        var c=Cover(r,WeldedSpliceCover.TopInnerPositive);
        Assert.IsTrue(c.Cuts.Any(x=>Math.Abs(x.M)>0));Assert.IsTrue(c.Cuts.Any(x=>Math.Abs(x.V)>0));
        Assert.AreEqual(ConnectionStatus.NotSupported,c.AxialBlockTearing.Status);
        Assert.IsTrue(c.Sections.IsComplete);
    }
    [DataTestMethod][DataRow(1d)][DataRow(-1d)][DataRow(1e-12)]
    public void WebNvmScopeRemainsExplicitAndZeroLoadsAreNotInvented(double scale)
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(scale*1e6,scale*2e5,0,0,0,scale*200e6))));
        var c=Cover(r,WeldedSpliceCover.WebPositive);
        Assert.IsTrue(c.Sections.IsComplete);Assert.AreEqual(ConnectionStatus.NotSupported,c.AxialBlockTearing.Status);
        Assert.IsTrue(c.Cuts.Any(x=>x.M!=0));Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    [TestMethod]
    public void HighShearExhaustsBendingWithoutNanOrResidualCapacity()
    {
        var c=Cover(WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(0,1e8,0,0,0,100e6)))),WeldedSpliceCover.WebPositive);
        Assert.AreEqual(ConnectionStatus.Failed,c.Sections.Status);
        Assert.IsTrue(c.Sections.Checks.Any(x=>x.Id.EndsWith("AxialShearBending")&&x.Demand>0&&x.Resistance==0));
        Assert.IsTrue(c.Sections.Trace.All(t=>double.IsFinite(t.Value)));
    }
    [TestMethod]
    public void AFailedSectionIsRetainedEvenWhenItsBlockMechanismIsUnsupported()
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(1e9,1e8,0,0,0,1e10))));
        Assert.IsTrue(r.HasFailedComponent);Assert.IsTrue(r.Covers.Any(c=>c.AxialBlockTearing.Status==ConnectionStatus.NotSupported));
    }
    [TestMethod]
    public void InvalidGeometryCodeAndNullDoNotProduceFavourableResults()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamSplicePlates.Verify(null!));
        var b=Input().Bolts;var r=WeldedIBeamSplicePlates.Verify(new(IBeamSplicePlatePolicy.Ntc2018(),b));
        Assert.AreEqual(0,r.Covers.Count);Assert.IsTrue(r.Diagnostics.Contains("PLATE_BOLT_POLICY_MISMATCH"));
        var invalid=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(first:5)));Assert.AreEqual(0,invalid.Covers.Count);
    }
    [TestMethod]
    public void P398GrossElasticInteractionRuleHasHalfShearThreshold()
    {
        // Evaluate the actual cut against independent SCI P398 p59 arithmetic.
        var p=Input(detail:Detail(new(2e5,7e5,0,0,0,120e6)));
        var c=Cover(WeldedIBeamSplicePlates.Verify(p),WeldedSpliceCover.WebPositive);
        double a=280*12,an=(280-3*26)*12,vr=Math.Min(a*355/(1.27*Math.Sqrt(3)),an*510/(Math.Sqrt(3)*1.25));
        double nr=Math.Min(a*355,.9*an*510/1.25),mr=12*280*280/6d*355;
        var cut=c.Cuts.Single(x=>x.X==0);double rho=Math.Pow(2*Math.Abs(cut.V)/vr-1,2);
        Assert.IsTrue(Math.Abs(cut.V)/vr>.5&&Math.Abs(cut.V)/vr<1);
        int index=c.Cuts.ToList().IndexOf(cut);
        Close((1-rho)*mr*Math.Max(0,1-Math.Abs(cut.N)/nr),c.Sections.Checks.Single(x=>x.Id==$"Cut{index}.AxialShearBending").Resistance);
    }
    [TestMethod]
    public void EveryCutBalancesAnIndependentFreeBodyFromTheRationalForceOracle()
    {
        var r=WeldedIBeamSplicePlates.Verify(Input(detail:WeldedIBeamSpliceTests.Input()));
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","welded-ibeam-splice-independent.json")));
        foreach(var c in r.Covers)
        {
            double centre=Trace(c.Sections,"TransverseCentroid");
            var forces=json.RootElement.GetProperty("groups").EnumerateArray().Where(g=>g.GetProperty("cover").GetString()==c.Cover.ToString())
                .SelectMany(g=>g.GetProperty("forces").EnumerateArray()).Select(f=>f.EnumerateArray().Select(v=>v.GetDouble()).ToArray()).ToArray();
            foreach(var cut in c.Cuts)
            {
                var freeBody=forces.Where(f=>f[0]<cut.X || (f[0]==cut.X&&cut.ColumnLimit==1)).ToArray();
                // Direct statics at the cut; no cumulative algorithm or calls to the production distributor.
                Assert.AreEqual(freeBody.Sum(f=>f[2]),cut.N,1e-7);
                Assert.AreEqual(freeBody.Sum(f=>f[3]),cut.V,1e-7);
                Assert.AreEqual(freeBody.Sum(f=>(f[0]-cut.X)*f[3]-(f[1]-centre)*f[2]),cut.M,1e-5);
            }
        }
    }
    [DataTestMethod][DataRow(1e-12)][DataRow(1e6)]
    public void ForcesAndCutMomentsScaleWithoutAnAbsoluteLoadThreshold(double scale)
    {
        var baseline=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(1e6,2e5,0,0,0,200e6))));
        var scaled=WeldedIBeamSplicePlates.Verify(Input(detail:Detail(new(scale*1e6,scale*2e5,0,0,0,scale*200e6))));
        for(int i=0;i<8;i++)for(int j=0;j<baseline.Covers[i].Cuts.Count;j++)
        {
            var a=baseline.Covers[i].Cuts[j];var b=scaled.Covers[i].Cuts[j];
            Assert.AreEqual(a.N*scale,b.N,Math.Abs(scale)*1e-7);
            Assert.AreEqual(a.V*scale,b.V,Math.Abs(scale)*1e-7);
            Assert.AreEqual(a.M*scale,b.M,Math.Abs(scale)*1e-5);
        }
    }
}
