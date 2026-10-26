using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateStandardJointTests
{
    static (EndPlateLocalCaseAssessmentResult local,EndPlateInPlaneResult plane,EndPlateColumnCaseResult column) Case(
        bool ntc=false,bool flush=false,double moment=100e6,double shear=300000,double contactWidth=209.3,bool overloadedBolt=false)
    {
        var p=EndPlateStandardCaseData.Parent(ntc,flush,moment,shear);
        var connection=EndPlateInitialCaseTestData.Connection(p.MomentComponents,overloadedBolt:overloadedBolt);
        var hardware=Enumerable.Range(0,p.MomentComponents.EffectiveRows.Count).Select(i=>new EndPlateBoltElongationDetail(i,70.5,"25+20.5+4+4+(15+19)/2"));
        var initial=EndPlateInitialStiffnessFromMoment.Calculate(EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(p,hardware));
        return (EndPlateLocalCaseAssessment.Assess(p,connection,initial),EndPlateInPlaneChecks.Verify(connection.Bolts),
            EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,width:contactWidth)));
    }
    [DataTestMethod]
    [DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void OrdinaryLocalJointAggregatesAllChecksWithoutPromotingTheOldApis(bool ntc,bool flush)
    {
        var c=Case(ntc,flush);var r=EndPlateStandardJoint.Verify(c.local,c.plane,c.column);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));Assert.IsTrue(r.IsComplete);Assert.IsTrue(r.CanQualifyWholeJoint);
        Assert.IsFalse(r.CanQualifyWholeMember);Assert.IsFalse(c.local.CanQualifyWholeJoint);Assert.IsFalse(c.plane.CanQualifyWholeJoint);
        Assert.IsTrue(r.Checks.Any(x=>x.Id.Contains("OuterBlock")));Assert.IsTrue(r.Checks.Any(x=>x.Id.Contains("ElasticNetYield")));
        Assert.IsTrue(r.Checks.Any(x=>x.Id=="CompressionContact"));Assert.IsNull(r.GlobalSpringMatches);
        double expected=flush?69580519870.4866:90338344812.44681;
        Assert.AreEqual(expected,r.InitialStiffness!.Value,expected*1e-10);
        double ratio=c.local.Actions.M1/r.MomentResistance!.Value;
        double mu=ratio<=2d/3?1:Math.Pow(1.5*ratio,2.7);
        Assert.AreEqual(expected/mu,r.SecantStiffness!.Value,expected*1e-10);
        Assert.AreEqual(c.local.Actions.M1/r.SecantStiffness.Value,r.ModelRotation!.Value,1e-12);
    }

    [TestMethod]
    public void IdenticalNumbersFromAnotherCaseAreRejected()
    {
        var a=Case();var b=Case();var r=EndPlateStandardJoint.Verify(a.local,b.plane,a.column);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsNull(r.MomentResistance);Assert.IsNull(r.SecantStiffness);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),"AllAssessmentsMustBelongToSameImmutableCase");
    }

    [TestMethod]
    public void AnotherColumnCaseCannotBeAttached()
    {
        var a=Case();var b=Case();var r=EndPlateStandardJoint.Verify(a.local,a.plane,b.column);
        Assert.IsFalse(r.IsComplete);Assert.IsNull(r.MomentResistance);
    }

    [TestMethod]
    public void KnownBoltFailureSurvivesAMismatchedColumn()
    {
        var a=Case(overloadedBolt:true);var b=Case();var r=EndPlateStandardJoint.Verify(a.local,a.plane,b.column);
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.HasFailedComponent);Assert.IsFalse(r.IsComplete);
    }

    [TestMethod]
    public void ReportRetainsFailedBoltEvidenceWhenTheStiffnessCaseIsMismatched()
    {
        var a=Case(overloadedBolt:true);var b=Case();
        var incomplete=EndPlateLocalCaseAssessment.Assess(a.local.BeamAndMoment,a.local.Connection,b.local.InitialStiffnessAssessment);
        Assert.AreEqual(0,incomplete.Checks.Count);
        var r=EndPlateStandardJoint.Verify(incomplete,a.plane,a.column);
        var report=StandardConnectionCaseReport.Create(r);
        Assert.AreEqual(ConnectionStatus.Failed,report.Status);Assert.IsFalse(report.IsComplete);
        Assert.IsTrue(report.Checks.Any(x=>!x.Passed));
        Assert.IsFalse(report.Results.Any(x=>x.Name=="Joint.MomentResistanceAtRetainedShear"));
    }

    [TestMethod]
    public void ContactCanGovernTheCompleteMomentResistance()
    {
        var c=Case(moment:20e6,contactWidth:40);var r=EndPlateStandardJoint.Verify(c.local,c.plane,c.column);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        double force=c.local.BeamAndMoment.MomentComponents.EffectiveRows.Sum(x=>x.Resistance);
        double raw=c.local.BeamAndMoment.MomentComponents.MomentResistance!.Value;
        Assert.AreEqual(raw*40*15.6*265/force,r.MomentResistance!.Value,1e-7);
        Assert.IsTrue(r.MomentResistance<raw);
    }

    [TestMethod]
    public void ContactFailureDoesNotExportAnAcceptableSpring()
    {
        var c=Case(contactWidth:10);var r=EndPlateStandardJoint.Verify(c.local,c.plane,c.column);
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.IsComplete);Assert.IsNull(r.SecantStiffness);
        Assert.IsTrue(r.Checks.Any(x=>x.Id=="StandardJoint.Moment"&&!x.Passed));
    }

    [TestMethod]
    public void MatchingSpringAndChangedSpringAreDistinguishedFromStrength()
    {
        var c=Case();var raw=EndPlateStandardJoint.Verify(c.local,c.plane,c.column);
        var match=EndPlateStandardJoint.Verify(c.local,c.plane,c.column,raw.SecantStiffness);
        var different=EndPlateStandardJoint.Verify(c.local,c.plane,c.column,raw.SecantStiffness*2);
        Assert.AreEqual(true,match.GlobalSpringMatches);Assert.IsFalse(match.RequiresGlobalReanalysis);
        Assert.AreEqual(false,different.GlobalSpringMatches);Assert.IsTrue(different.RequiresGlobalReanalysis);
        Assert.AreEqual(ConnectionStatus.Passed,different.Status);Assert.IsFalse(different.CanQualifyWholeMember);
    }

    [DataTestMethod][DataRow(0.0)][DataRow(-1.0)][DataRow(double.NaN)][DataRow(double.PositiveInfinity)]
    public void InvalidGlobalSpringRejected(double value)
    {var c=Case();Assert.ThrowsException<ArgumentOutOfRangeException>(()=>EndPlateStandardJoint.Verify(c.local,c.plane,c.column,value));}

    [TestMethod]
    public void ZeroLoadPreservesFiniteStiffnessAndZeroRotation()
    {
        var c=Case(moment:0,shear:0);var r=EndPlateStandardJoint.Verify(c.local,c.plane,c.column);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));Assert.AreEqual(0,r.ModelRotation!.Value,1e-15);
        Assert.AreEqual(r.InitialStiffness,r.SecantStiffness);
    }
}
