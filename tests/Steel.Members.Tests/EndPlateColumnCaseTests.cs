using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

internal static class EndPlateStandardCaseData
{
    internal static EndPlateMomentWithBeamSectionResult Parent(bool ntc=false,bool flush=false,double moment=100e6,double shear=300000,double stress=200)
    {
        var original=EndPlateInitialCaseTestData.Parent(ntc,flush,moment,shear,beta:1);var m=original.Input;
        var changed=new EndPlateMomentComponentInput(m.Policy,m.Column,m.Beam,m.ColumnFlangeFy,m.EndPlate,m.Bolt,m.InternalGeometry,m.ExtensionGeometry,
            m.ColumnFirstEndContinuation,m.ColumnLastEndContinuation,m.CompressionEndPlateProjection,m.BeamMomentResistance,m.Moment,m.Shear,m.Axial,
            m.Beta,stress,m.PanelConfiguration,m.TransformationSource,"Actual net column stress upper bound",m.ColumnSwayRestraintSource,m.ActionCombinationSource,
            m.TStubMethod,m.ContactWidth,m.HoleClearanceServiceabilitySource);
        return EndPlateMomentWithBeamSection.Verify(changed,original.BeamSection);
    }
    internal static EndPlateColumnCaseInput Column(EndPlateMomentWithBeamSectionResult p,double width=209.3,double height=15.6,double gap=0,
        double axial=-100000,double columnShear=0)
    {
        double signed=p.BeamSection.Input.Actions.V2;
        return new(p,new(axial,0,columnShear,0,-p.Input.Moment/2,0),new(axial-signed,0,columnShear,0,p.Input.Moment/2,0),
            width,height,gap,410,"Same combination, projected cut actions at joint centre","Centred closed flat contact at both interfaces");
    }
}

[TestClass]
public class EndPlateColumnCaseTests
{
    [DataTestMethod]
    [DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void CoherentColumnAndContactAreCalculated(bool ntc,bool flush)
    {
        var p=EndPlateStandardCaseData.Parent(ntc,flush);var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));Assert.IsTrue(r.IsComplete);
        Assert.IsFalse(r.CanQualifyWholeMember);Assert.IsTrue(r.ContactLimitedMomentResistance>0);
    }

    [TestMethod]
    public void NetGeometryAgreesWithIndependentDisjointRectangles()
    {
        var p=EndPlateStandardCaseData.Parent();var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p));
        // Positive z towards connected flange; omit roots on purpose.
        var rectangles=new[]{(258.8,20.5,-123.1),(12.8,225.7,0.0),(258.8-52,20.5,123.1)};
        double area=rectangles.Sum(x=>x.Item1*x.Item2),cy=rectangles.Sum(x=>x.Item1*x.Item2*x.Item3)/area;
        double inertia=rectangles.Sum(x=>x.Item1*Math.Pow(x.Item2,3)/12+x.Item1*x.Item2*Math.Pow(x.Item3-cy,2));
        Assert.AreEqual(area,r.Trace.Single(x=>x.Name=="Column.NetArea").Value,1e-7);
        Assert.AreEqual(cy,r.Trace.Single(x=>x.Name=="Column.NetCentroidShift").Value,1e-10);
        Assert.AreEqual(inertia,r.Trace.Single(x=>x.Name=="Column.NetInertia").Value,1e-6);
        double stress=400000/area+(50e6+400000*Math.Abs(cy))*(266.7/2+Math.Abs(cy))/inertia;
        Assert.AreEqual(stress,r.Trace.Single(x=>x.Name=="Lower.NormalStressBound").Value,1e-8);
    }

    [DataTestMethod][DataRow(-400000.0)][DataRow(400000.0)]
    public void AxialForceAtGrossCentroidIncludesNetSectionEccentricity(double axial)
    {
        var p=EndPlateStandardCaseData.Parent(moment:0,shear:0);
        var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,axial:axial));
        // Independent three-rectangle section, coordinates measured from gross centroid.
        var parts=new[]{(258.8,20.5,-123.1),(12.8,225.7,0.0),(206.8,20.5,123.1)};
        double area=parts.Sum(x=>x.Item1*x.Item2);
        double centroid=parts.Sum(x=>x.Item1*x.Item2*x.Item3)/area;
        double inertia=parts.Sum(x=>x.Item1*Math.Pow(x.Item2,3)/12+x.Item1*x.Item2*Math.Pow(x.Item3-centroid,2));
        double momentAtNetCentroid=-axial*centroid;
        double[] edgeStresses=new[]{-133.35,133.35}.Select(z=>axial/area+momentAtNetCentroid*(z-centroid)/inertia).ToArray();
        double expected=edgeStresses.Max(x=>Math.Abs(x));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        Assert.AreEqual(expected,r.Trace.Single(x=>x.Name=="Upper.NormalStressBound").Value,1e-8);
        Assert.AreEqual(expected,r.Trace.Single(x=>x.Name=="Lower.NormalStressBound").Value,1e-8);
        Assert.IsTrue(expected>Math.Abs(axial)/area);
        Assert.AreEqual(Math.Max(0,-edgeStresses.Min()),r.Trace.Single(x=>x.Name=="Column.RequiredLongitudinalCompressionStress").Value,1e-8);
    }

    [TestMethod]
    public void ActualStressCannotBeReplacedByAZeroSourceLabel()
    {
        var p=EndPlateStandardCaseData.Parent(stress:0);var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p));
        Assert.IsFalse(r.IsComplete);Assert.IsNull(r.ContactLimitedMomentResistance);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),"ComponentColumnStressBelowActualNetSectionBound");
    }

    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void ThreeEquilibriumConditionsAreIndependent(int which)
    {
        var p=EndPlateStandardCaseData.Parent();var d=EndPlateStandardCaseData.Column(p);var lo=d.Lower;
        var wrong=new SteelActions(lo.N+(which==0?1000:0),0,which==2?1000:0,0,lo.M1+(which==1?1e6:0),0);
        var r=EndPlateColumnCase.Verify(new(p,d.Upper,wrong,209.3,15.6,0,410,d.CombinationSource,d.ContactExecutionSource));
        Assert.IsFalse(r.IsComplete);Assert.IsTrue(r.Diagnostics.Any(x=>x.Contains("Jump")));
    }

    [TestMethod]
    public void OpenOrOversizedContactIsUnsupported()
    {
        var p=EndPlateStandardCaseData.Parent();
        Assert.IsFalse(EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,gap:.01)).IsComplete);
        Assert.IsFalse(EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,width:210)).IsComplete);
    }

    [TestMethod]
    public void SmallPhysicalContactFailsAndReducesTheMomentBound()
    {
        var p=EndPlateStandardCaseData.Parent();var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,width:20,height:5));
        Assert.IsTrue(r.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        var c=r.Checks.Single(x=>x.Id=="CompressionContact");Assert.AreEqual(26500,c.Resistance,1e-8);
        double sum=p.MomentComponents.EffectiveRows.Sum(x=>x.Resistance);
        Assert.AreEqual(p.MomentComponents.MomentResistance!.Value*26500/sum,r.ContactLimitedMomentResistance!.Value,1e-7);
    }

    [TestMethod]
    public void AdverseColumnShearCannotHideBehindBetaOne()
    {
        var p=EndPlateStandardCaseData.Parent();var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,columnShear:-1000));
        Assert.IsFalse(r.IsComplete);CollectionAssert.Contains(r.Diagnostics.ToArray(),"ActualPanelShearExceedsBetaOneEnvelope");
    }

    [TestMethod]
    public void ReversingBeamShearReversesTheColumnAxialJump()
    {
        var p=EndPlateStandardCaseData.Parent(shear:-300000);
        var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,axial:-400000));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
    }

    [TestMethod]
    public void LocalColumnOverloadIsPreservedEvenIfStressContextIsIncomplete()
    {
        var p=EndPlateStandardCaseData.Parent();var r=EndPlateColumnCase.Verify(EndPlateStandardCaseData.Column(p,axial:-4e6));
        Assert.IsFalse(r.IsComplete);Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.Checks.Any(x=>!x.Passed));
    }
}
