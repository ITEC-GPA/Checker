using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class DoubleAngleCleatColumnWebTests
{
    private static ConnectionRolledISection Section(double h, double b, double tw, double tf, double r, double fy = 275)
        => new(h, b, tw, tf, r, 2*b*tf+(h-2*tf)*tw+4*(1-Math.PI/4)*r*r, fy, 430);
    private static DoubleAngleCleatColumnWebInput Input(double v = 100000, double axial = -200000,
        double fx = 0, double mx = 0, double mz = 0, bool ntc = false, double end = 500,
        ConnectionRolledISection? column = null, double flangeFy = 265,
        PlanarConnectionActions? upper = null, PlanarConnectionActions? lower = null,
        string? source = "One actual FEM combination, same +Y cut convention",
        string? detail = "Actual fabricated geometry; no distributed column load between cuts")
    {
        var col = column ?? Section(400,300,12,20,18);
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EC recommended factors");
        var cleats = new DoubleAngleCleatComponentInput(code, new(90,10,275,430), 11,
            Section(420,200,10,16,15), new(20,22,245,ConnectionBoltGrade.Class8_8,true), 4,
            new(0,-v,0,0,0,0), true, false, "SLE hole clearance checked");
        // Moments at the two actual Y=+/-300mm cuts, not copied origin moments.
        return new(cleats,col,flangeFy,Math.Max(430,flangeFy+40),end,end,
            upper ?? new(fx,axial,0,mx,0,mz+300*fx,0,300,0),
            lower ?? new(fx,axial-v,0,mx,0,mz-300*fx-v*col.WebThickness/2,0,-300,0),detail!,source!);
    }
    private static double Trace(DoubleAngleCleatColumnWebResult r,string name) => r.Support.Trace.Single(x=>x.Name==name).Value;
    private static ConnectionCheck Check(DoubleAngleCleatColumnWebResult r,string name) => r.Support.Checks.Single(x=>x.Id==name);
    private static void Near(double expected,double actual) => Assert.AreEqual(expected,actual,Math.Max(1e-9,Math.Abs(expected)*1e-11));
    private static void Unsupported(DoubleAngleCleatColumnWebResult r,string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Support.Status);
        Assert.IsFalse(r.IsComponentAssessmentComplete);
        CollectionAssert.Contains(r.Support.Diagnostics.ToArray(),diagnostic);
        Assert.AreEqual(0,r.Support.Checks.Count);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SupportedComponentsUseTheSelectedCodeWithoutClaimingWholeJoint(bool ntc)
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(ntc:ntc));
        Assert.IsTrue(r.IsComponentAssessmentComplete,string.Join(";",r.Support.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentStatus);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        CollectionAssert.Contains(r.MissingWholeJointChecks.ToArray(),"SUPPORT_OUT_OF_PLANE_BENDING_AND_PRYING");
        Near(265/(ntc?1.05:1),Check(r,"Column.ElasticNetYield").Resistance);
    }

    [TestMethod]
    public void HandIntegratedNetRectanglesDeductTwoWebHolesAndNoFlangeHoles()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input());
        Near(16320,Trace(r,"Column.GrossRectangularArea"));
        Near(15792,Trace(r,"Column.NetArea"));
        Near(478637504,Trace(r,"Column.NetIx"));
        Near(90045504,Trace(r,"Column.NetIz"));
        Near(-600000,Trace(r,"Column.BeamReactionMoment"));
        Near(300000,Trace(r,"Column.AxialEnvelope"));
        Near(1200000,Trace(r,"Column.WeakMomentEnvelope"));
        Near(300000d/15792+1200000d*150/90045504,Trace(r,"Column.NormalStressBound"));
    }

    [TestMethod]
    public void SciCheckTenIndependentStripArithmeticIsAppliedToEveryPrefixInBothDirections()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input());
        for(int rows=1;rows<=4;rows++) foreach(string edge in new[]{"Top","Bottom"})
        {
            string id="Column."+edge+rows;
            // SCI P358 Check10: e_eff=min(e,5d), extension=min(g/2,5d).
            double gross=12*(100+(rows-1)*70+55),net=gross-rows*22*12;
            Near(gross,Trace(r,id+".GrossStripArea")); Near(net,Trace(r,id+".NetStripArea"));
            Near(12500*rows,Check(r,id+".GrossShear").Demand);
            Near(gross*275/Math.Sqrt(3),Check(r,id+".GrossShear").Resistance);
            Near(net*430/(1.25*Math.Sqrt(3)),Check(r,id+".NetShear").Resistance);
        }
        Near(4380,Trace(r,"Column.Top4.GrossStripArea"));
        Near(3324,Trace(r,"Column.Top4.NetStripArea"));
    }

    [TestMethod]
    public void ColumnWebBearingCarriesEachPhysicalBoltForceExactlyOnce()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input());
        var bolts=r.Support.Checks.Where(x=>x.Id.EndsWith("ColumnWebBearing")).ToArray();
        Assert.AreEqual(8,bolts.Length);
        foreach(var bolt in bolts)
        {
            Near(12500,bolt.Demand);
            // alpha_b for an inner bolt=70/(3*22)-.25; k1=2.5.
            Near(2.5*(70d/66-.25)*430*20*12/1.25,bolt.Resistance);
        }
        Near(100000,bolts.Sum(x=>x.Demand));
    }

    [TestMethod]
    public void ConcomitantNormalStressIsAddedToLocalStripShear()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(mx:10000000));
        double stress=300000d/15792+10000000d*200/478637504+1200000d*150/90045504;
        Near(stress+Math.Sqrt(3)*50000/3324,Check(r,"Column.Top4.ConcomitantNetYield").Demand);
    }

    [DataTestMethod] [DataRow(15000d)] [DataRow(-15000d)]
    public void ActualCutMomentsAreTransportedWithRightHandSigns(double fx)
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(fx:fx,mx:10000000,mz:2000000));
        Assert.IsTrue(r.IsComponentAssessmentComplete,string.Join(";",r.Support.Diagnostics));
        foreach(var residual in r.Support.Trace.Where(x=>x.Name.StartsWith("Equilibrium."))) Near(0,residual.Value);
        Near(1.5*15000/12000,Trace(r,"Column.WeakShearStressBound"));
    }

    [TestMethod]
    public void MissingColumnFaceEccentricityIsRejected()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(lower:new(0,-300000,0,0,0,0,0,-300,0))),"COLUMN_CUT_EQUILIBRIUM:Mz");

    [TestMethod]
    public void OppositeOutwardCutConventionIsRejected()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(lower:new(0,300000,0,0,0,600000,0,-300,0))),"COLUMN_CUT_EQUILIBRIUM:Fy");

    [TestMethod]
    public void OriginMomentsCannotBeMislabelledAsMomentsAtActualCuts()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(fx:15000,
            upper:new(15000,-200000,0,0,0,0,0,300,0),lower:new(15000,-300000,0,0,0,-600000,0,-300,0))),"COLUMN_CUT_EQUILIBRIUM:Mz");

    [DataTestMethod] [DataRow(1e-15)] [DataRow(-1e-15)]
    public void NonzeroPerforatedWebShearIsNeverRoundedAway(double shear)
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(upper:new(0,-200000,shear,0,0,0,0,300,0))),
            "CONCURRENT_STRONG_AXIS_SHEAR_THROUGH_PERFORATED_WEB_NOT_SUPPORTED");

    [TestMethod]
    public void TorsionIsRejectedEvenWithEqualOppositeCuts()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(upper:new(0,-200000,0,0,1e-15,0,0,300,0))),"CONCURRENT_COLUMN_TORSION_NOT_SUPPORTED");

    [TestMethod]
    public void TinyUnbalancedActionHasNoOneNewtonToleranceFloor()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(v:1e-15,axial:0,
            lower:new(0,0,0,0,0,0,0,-300,0))),"COLUMN_CUT_EQUILIBRIUM:Fy");

    [TestMethod]
    public void ZeroLoadStillRequiresRealGeometryAndCutPositions()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(v:0,axial:0));
        Assert.AreEqual(ConnectionStatus.Passed,r.ComponentStatus);
        Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(v:0,axial:0,upper:new(0,0,0,0,0,0,0,100,0))),
            "CUTS_MUST_BRACKET_COMPLETE_COLUMN_CONNECTION_ZONE");
    }

    [TestMethod]
    public void CutsBeyondActualColumnEndAreRejected()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(end:190)),"CUTS_MUST_LIE_WITHIN_ACTUAL_COLUMN_ENDS");

    [TestMethod]
    public void OffCentroidCutsAreNotSilentlyReinterpreted()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(upper:new(0,-200000,0,0,0,0,1,300,0))),"CUTS_MUST_REFERENCE_ACTUAL_COLUMN_CENTROID");

    [TestMethod]
    public void ThinColumnWebFailsApplicabilityBeforeGrossAreaCanQualifyIt()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(column:Section(400,300,6,20,18))),
            "COLUMN_CLASS_THREE_OR_BETTER_IN_UNIFORM_COMPRESSION_REQUIRED");

    [TestMethod]
    public void EachWallUsesItsOwnFyForClassification()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(column:Section(400,600,12,20,18),flangeFy:460)),
            "COLUMN_CLASS_THREE_OR_BETTER_IN_UNIFORM_COMPRESSION_REQUIRED");

    [TestMethod]
    public void FullBeamAndCleatFootprintsMustFitTheSupportingWeb()
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(column:Section(260,300,12,20,18))),
            "BEAM_AND_COMPLETE_CLEATS_MUST_FIT_BETWEEN_COLUMN_ROOTS");

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void TraceabilitySourcesAreRequired(bool missingCase)
        => Unsupported(DoubleAngleCleatColumnWeb.Verify(Input(source:missingCase?null:"Case",detail:missingCase?"Detail":null)),
            missingCase?"ACTUAL_CONCOMITANT_COLUMN_CUTS_REQUIRED":"ACTUAL_COLUMN_WEB_DETAIL_REQUIRED");

    [TestMethod]
    public void ConcurrentColumnOverloadIsAnActualFailure()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(axial:-10000000));
        Assert.IsTrue(r.IsComponentAssessmentComplete); Assert.IsTrue(r.HasFailedComponent);
        Assert.AreEqual(ConnectionStatus.Failed,r.ComponentStatus);
        Assert.IsFalse(Check(r,"Column.ElasticNetYield").Passed);
    }

    [TestMethod]
    public void CleatFailureSurvivesAnUnsupportedColumnComponent()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(v:2000000,source:null));
        Assert.AreEqual(ConnectionStatus.Failed,r.Cleats.ComponentAssessment.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Support.Status);
        Assert.AreEqual(ConnectionStatus.Failed,r.ComponentStatus); Assert.IsTrue(r.HasFailedComponent);
    }

    [TestMethod]
    public void ExtremeFiniteInputCannotProduceAPassedResultOrNonfiniteTrace()
    {
        var r=DoubleAngleCleatColumnWeb.Verify(Input(fx:double.MaxValue/1000));
        Assert.AreNotEqual(ConnectionStatus.Passed,r.ComponentStatus);
        Assert.IsTrue(r.Support.Trace.All(x=>double.IsFinite(x.Value)));
    }
}
