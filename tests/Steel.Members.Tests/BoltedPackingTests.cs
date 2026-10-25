using GPC.Checkers.Steel.Connections;
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

[TestClass]
public class BoltedPackingTests
{
    private static SteelVerificationInput Input(string kind,bool ntc=false,double n=10000,double length=3000,double gap=10,bool holes=true)
    {
        var pair=PackedPairCompressionTests.Pair(kind,gap);double t=kind=="C"?10:12;
        return new(new SteelSectionInput(new SteelSection(pair,new SteelMaterial("S235",210000,235,360)),!holes,holes?pair.Area-2*17*t:null),
            new SteelActions(-n,0,0,0,0,0),ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),
            new SteelMemberInput(length,length,length));
    }
    private static SteelPackingLayout Layout(SteelVerificationInput i,double pitch=100)
        =>new(i,Enumerable.Range(0,(int)(i.Member.Length/pitch)+1).Select(v=>v*pitch),"Actual identical intermediate packings and terminal gussets");
    private static SteelBoltedPackingDetail Detail(string kind,bool ntc=false,double gap=10,bool washers=true,double coordinate=-1,double area=157)
        =>new(ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"test"),
            new ConnectionBolt(16,17,area,ConnectionBoltGrade.Class8_8,true),60,60,gap,coordinate>=0?coordinate:kind=="C"?100:kind=="L-long"?80:55,
            235,360,32,washers,"Ordinary filled round holes, hardened washers, compatible products, no other cuts/holes, no independent transverse load or opening moment","NTC normal clearance");
    private static SteelCheckExecution Member(SteelPackedPairAssessment p,bool torsionExcluded=true)
    {
        var a=SteelMemberAnalysis.PackedCompression(p,"Constant centroidal N; pin ends","Explicit conservative C curves",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:torsionExcluded?"Independent global T exclusion":null,flexuralTorsionalExclusionSource:"Independent global FT exclusion");
        return SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
    }
    [DataTestMethod][DataRow("C",false)][DataRow("C",true)][DataRow("L-long",false)][DataRow("L-long",true)][DataRow("L-short",false)][DataRow("L-short",true)]
    public void CompleteStandardPackingAndBothAngleOrientationsFlowThroughMemberCheck(string kind,bool ntc)
    {
        var i=Input(kind,ntc);var m=PackedPairImperfectionModel.Evaluate(Layout(i));var r=SteelBoltedPacking.Verify(m,Detail(kind,ntc));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.IsTrue(m.ChordCompression>5000);Assert.IsTrue(r.MaximumConnectionDemand>0);Assert.IsFalse(r.CanQualifyWholeMember);
        var p=SteelPackedPairAssessment.FromBoltedPacking(r,10000,200000,"Same-state terminal gusset check including the two endpoint transfer forces","Actual end forks and free warping");
        var result=Member(p);Assert.AreEqual(SteelExecutionStatus.Passed,result.Status,result.Message);
        Assert.AreSame(i,p.Input);Assert.AreSame(r,p.ComputedPacking);Assert.AreEqual(false,p.Input.Section.Unperforated);
        Assert.IsTrue(result.Trace.Any(v=>v.Name=="PackedPair.Computed.Chord0.Wall.ElasticWallAndLap"));
    }
    [TestMethod]
    public void ChannelDemandMatchesIndependentRectangleIntegrationAndBeamColumnEquation()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));
        // C200x75: rectangles 75x12 twice and 10x176 between flanges.
        double a=2*75*12+10*176,x=(2*75*12*37.5+10*176*5)/a;
        double iy=2*(12*Math.Pow(75,3)/12+75*12*Math.Pow(37.5-x,2))+176*Math.Pow(10,3)/12+1760*Math.Pow(5-x,2);
        double d=2*x+10,total=2*iy+a*d*d/2,ncr=Math.PI*Math.PI*210000*total/9e6;
        double moment=10000*20/(1-10000/ncr),delta=moment*a*d/(2*total);
        Assert.AreEqual(ncr,m.CriticalAssemblyPlane,1e-7);Assert.AreEqual(moment,m.MomentAssemblyPlane,1e-7);
        Assert.AreEqual(5000+delta,m.ChordCompression,1e-7);
        Assert.AreEqual(delta*Math.PI/3000,m.ShearFlowPeak,1e-9);
        Assert.AreEqual(2*delta,m.Stations.Sum(s=>s.LongitudinalShear),1e-7);
    }
    [TestMethod]
    public void DiscretizedDifferentialEquationConvergesToSecondOrderMoment()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));
        double n=10000,e0=20,ncr=m.CriticalAssemblyPlane;
        double Error(int panels)
        {
            // Central-difference sine eigenvalue of -d²/dx². Independent numerical ODE oracle.
            double ratio=Math.Pow(2*panels/Math.PI*Math.Sin(Math.PI/(2*panels)),2);
            double numeric=n*e0/(1-n/(ncr*ratio));return Math.Abs(numeric-m.MomentAssemblyPlane);
        }
        Assert.IsTrue(Error(100)<Error(50)/3.9);Assert.IsTrue(Error(200)<.1);
    }
    [TestMethod]
    public void MidspanPackingDoesNotCancelOppositeShearFlows()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));
        var centre=m.Stations.Single(s=>s.Station==1500);
        Assert.IsTrue(centre.LongitudinalShear>0);
        double amplitude=m.ShearFlowPeak*3000/Math.PI;
        Assert.AreEqual(amplitude*(2-2*Math.Sin(Math.PI*1450/3000)),centre.LongitudinalShear,1e-8);
    }
    [TestMethod]
    public void NonuniformPitchUsesBothHalfPanelsAtEachActualStation()
    {
        var i=Input("C",length:300);var m=PackedPairImperfectionModel.Evaluate(new SteelPackingLayout(i,new[]{0d,50d,150d,300d},"Actual positions"));
        Assert.AreEqual(25d,m.Stations[1].TributaryStart);Assert.AreEqual(100d,m.Stations[1].TributaryEnd);
        double q=0,step=75d/10000;
        for(int j=0;j<10000;j++)q+=m.ShearFlowPeak*Math.Abs(Math.Cos(Math.PI*(25+(j+.5)*step)/300))*step;
        Assert.AreEqual(q,m.Stations[1].LongitudinalShear,1e-6);
    }
    [TestMethod]
    public void BoltPackingAndSingleLapBearingUseIndependentNormativeValues()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));var r=SteelBoltedPacking.Verify(m,Detail("C"));
        double beta=9*16d/(8*16+3*10),fv=.6*800*157/1.25*beta;
        Assert.AreEqual(fv,r.Checks.Single(c=>c.Id=="Bolt.Shear").Resistance,1e-8);
        double alpha=30d/(3*17),k=Math.Min(2.5,2.8*30/17-1.7);
        double bearing=Math.Min(k*alpha*360*16*10/1.25,1.5*360*16*10/1.25);
        Assert.AreEqual(bearing,r.Checks.Single(c=>c.Id=="Chord0.Wall.Bearing").Resistance,1e-8);
        Assert.IsTrue(r.Trace.Single(v=>v.Name=="Chord0.Wall.LapMoment").Value>r.MaximumConnectionDemand*20);
    }
    [DataTestMethod][DataRow("washer")][DataRow("edge")][DataRow("holes")][DataRow("pitch")][DataRow("gap")]
    public void UnsupportedDetailsCannotBeConvertedToMemberQualification(string cause)
    {
        var i=Input("C",holes:cause!="holes");var m=PackedPairImperfectionModel.Evaluate(Layout(i,cause=="pitch"?500:100));
        var r=SteelBoltedPacking.Verify(m,Detail("C",gap:cause=="gap"?11:10,washers:cause!="washer",coordinate:cause=="edge"?20:-1));
        Assert.IsFalse(r.IsComplete);
        Assert.ThrowsException<NotSupportedException>(()=>SteelPackedPairAssessment.FromBoltedPacking(r,10000,200000,"End","Fork"));
    }
    [TestMethod]
    public void ComponentFailureAndTerminalFailureBothSurviveAggregation()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));
        // Deliberately inadequate fastener stress area: failure must not be hidden by the passing member.
        var failed=SteelBoltedPacking.Verify(m,Detail("C",area:.01));Assert.IsTrue(failed.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,failed.Status);
        var p=SteelPackedPairAssessment.FromBoltedPacking(failed,10000,200000,"End verified","Fork");
        Assert.AreEqual(SteelExecutionStatus.Failed,Member(p).Status);
        var good=SteelBoltedPacking.Verify(m,Detail("C"));p=SteelPackedPairAssessment.FromBoltedPacking(good,10000,100,"End failed","Fork");
        Assert.AreEqual(SteelExecutionStatus.Failed,Member(p).Status);
    }
    [TestMethod]
    public void GlobalTorsionalModesCannotBeSilentlyReplacedByFlexuralDemandModel()
    {
        var r=SteelBoltedPacking.Verify(PackedPairImperfectionModel.Evaluate(Layout(Input("C"))),Detail("C"));
        var p=SteelPackedPairAssessment.FromBoltedPacking(r,10000,200000,"End","Fork");
        Assert.AreNotEqual(SteelExecutionStatus.Passed,Member(p,false).Status);
    }
    [TestMethod]
    public void EulerLimitIsRejectedBeforeDivisionByZeroOrNegativeAmplification()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C")));double n=Math.Min(m.CriticalAssemblyPlane,m.CriticalOtherPlane);
        Assert.ThrowsException<NotSupportedException>(()=>PackedPairImperfectionModel.Evaluate(Layout(Input("C",n:n))));
        Assert.ThrowsException<NotSupportedException>(()=>PackedPairImperfectionModel.Evaluate(Layout(Input("C",n:2*n))));
    }
    [TestMethod]
    public void LargerDeclaredBowIsPreservedAndDoubleCountingIsRejected()
    {
        var i=Input("C");SteelVerificationInput With(bool included)=>new(i.Section,i.Actions,i.Code,
            new SteelMemberInput(3000,3000,3000,imperfections:new SteelMemberImperfections(included,"Measured",40,-30)));
        var m=PackedPairImperfectionModel.Evaluate(Layout(With(false)));Assert.AreEqual(40d,m.InitialBow);
        Assert.ThrowsException<NotSupportedException>(()=>PackedPairImperfectionModel.Evaluate(Layout(With(true))));
    }
    [DataTestMethod][DataRow("L-long")][DataRow("L-short")]
    public void UnequalAngleDemandMatchesIndependentRectanglesOnTheActualJoinedLeg(string kind)
    {
        // Two disjoint rectangles: horizontal100x12 and vertical12x138 above it.
        double a1=1200,a2=1656,a=a1+a2,x=(a1*50+a2*6)/a,y=(a1*6+a2*81)/a;
        double ix=100*Math.Pow(12,3)/12+a1*Math.Pow(6-y,2)+12*Math.Pow(138,3)/12+a2*Math.Pow(81-y,2);
        double iy=12*Math.Pow(100,3)/12+a1*Math.Pow(50-x,2)+138*Math.Pow(12,3)/12+a2*Math.Pow(6-x,2);
        bool vertical=kind=="L-long";double d=2*(vertical?x:y)+10,ic=vertical?iy:ix;
        double ipair=2*ic+a*d*d/2,ncr=Math.PI*Math.PI*210000*ipair/9e6;
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input(kind)));
        Assert.AreEqual(ncr,m.CriticalAssemblyPlane,1e-7);
        Assert.AreEqual(Math.PI*Math.PI*210000*2*(vertical?ix:iy)/9e6,m.CriticalOtherPlane,1e-7);
        double moment=200000/(1-10000/ncr);
        Assert.AreEqual(5000+moment*a*d/(2*ipair),m.ChordCompression,1e-7);
        Assert.AreEqual(moment*ic/ipair,m.ChordAssemblyMoment,1e-7);
    }
    [TestMethod]
    public void ComputedPackingFailureIsNotHiddenByOtherPassingComponents()
    {
        var m=PackedPairImperfectionModel.Evaluate(Layout(Input("C",n:100000)));
        var r=SteelBoltedPacking.Verify(m,Detail("C"));Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        var stress=r.Checks.Single(c=>c.Id=="Chord0.Wall.ElasticWallAndLap");
        Assert.IsTrue(stress.Demand>r.Trace.Single(v=>v.Name=="Packing.MemberNormalStressBound").Value);
        Assert.AreEqual(r.Checks.Max(c=>c.Utilization),r.Utilization!.Value,1e-12);
    }
    [DataTestMethod][DataRow("source")][DataRow("restraint")][DataRow("demand")]
    public void TerminalLoadIntroductionCannotBeOmitted(string reason)
    {
        var r=SteelBoltedPacking.Verify(PackedPairImperfectionModel.Evaluate(Layout(Input("C"))),Detail("C"));
        Assert.ThrowsException<ArgumentException>(()=>SteelPackedPairAssessment.FromBoltedPacking(r,reason=="demand"?1:10000,200000,
            reason=="source"?"":"Actual terminal check",reason=="restraint"?"":"Actual forks"));
    }
    [TestMethod]
    public void NormalHoleExceptionCannotLeakToOtherPerforatedMembers()
    {
        var i=Input("C");var a=SteelMemberAnalysis.Compression(i,"N","C",SteelBucklingCurve.C,SteelBucklingCurve.C,SteelBucklingCurve.C,
            torsionalExclusionSource:"T",flexuralTorsionalExclusionSource:"FT");
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();
        Assert.AreEqual("Member.PerforatedNotQualified",r.DiagnosticCode);
    }
}
