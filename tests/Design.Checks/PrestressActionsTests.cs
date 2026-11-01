using System.Text.Json;
using GPC.Design.Actions.Prestressing;
using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Prestressing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class PrestressActionsTests
{
    private static PrestressActionsInput Input()=>new(){SystemSource="Parametri dell’esempio di prova, non ETA",ConcreteStressSource="Tensioni uniformi assegnate del caso analitico",ProfileIntervals=100,Observations=new(){new(){Name="Trasferimento",AgeDays=28},new(){Name="Un anno",AgeDays=365}}};
    private static PrestressActionsInput Straight()
    {var i=Input();i.Profile=TendonProfile.Straight;i.FrictionCoefficient=0;i.WobbleRadM=0;i.IncludeDeferredLosses=false;return i;}
    [TestMethod]
    public void NoFrictionAnchorageLossIsEpSlipOverLength()
    {
        var i=Straight();var r=PrestressActions.Calculate(i);
        // Δσ=195000*6/30000=39 MPa, no elastic shortening for one post-tensioned tendon.
        Assert.IsTrue(r.Stages.All(s=>s.Points.All(p=>Math.Abs(p.InitialStressMpa-1311)<1e-8)));
        Assert.AreEqual(207.6923076923077,r.JackingElongationMm,1e-9);Assert.AreEqual(6,r.AnchorageRecoveryMm,1e-9);Assert.IsTrue(r.Converged&&r.WithinStressLimits);
    }
    [TestMethod]
    public void StraightFrictionUsesMuTimesWobbleInsideTheExponent()
    {
        var i=Straight();i.FrictionCoefficient=.2;i.WobbleRadM=.01;i.AnchorageSlipMm=0;
        var r=PrestressActions.Calculate(i);
        Assert.AreEqual(1271.3821203387358,r.Stages[0].Points.Last().AfterFrictionMpa,1e-10);
        Assert.AreEqual(201.5843068237544,r.JackingElongationMm,5e-5);Assert.AreEqual(0,r.AnchorageRecoveryMm);
    }
    [TestMethod]
    public void PartialAnchorageInfluenceMatchesTheAnalyticExponentialProfile()
    {
        var i=Straight();i.FrictionCoefficient=.2;i.WobbleRadM=.01;
        var r=PrestressActions.Calculate(i);var points=r.Stages[0].Points;
        // δ=σj/(Ep λ) * (1-exp(-λ a))², λ=.002/m, δ=6 mm => a=21.26240924565 m.
        Assert.AreEqual(1239.930036028829,points.First().AfterAnchoringMpa,.002);
        Assert.AreEqual(points.Last().AfterFrictionMpa,points.Last().AfterAnchoringMpa,1e-10);
        Assert.IsTrue(points.Where(p=>p.XM>=21.3).All(p=>p.AfterAnchoringMpa==p.AfterFrictionMpa));Assert.AreEqual(6,r.AnchorageRecoveryMm,1e-9);
    }
    [TestMethod]
    public void AnchorageSlipMayReachTheWholeTendonWithoutAFalseUnchangedFarEnd()
    {
        var i=Straight();i.FrictionCoefficient=.2;i.WobbleRadM=.01;i.AnchorageSlipMm=20;
        var r=PrestressActions.Calculate(i);var points=r.Stages[0].Points;
        Assert.AreEqual(1145.2431226785345,points.First().AfterAnchoringMpa,.002);
        Assert.IsTrue(points.Last().AfterAnchoringMpa<points.Last().AfterFrictionMpa);Assert.AreEqual(20,r.AnchorageRecoveryMm,1e-9);
    }
    [TestMethod]
    public void TwoSymmetricActiveEndsEachContributeTheirSpecifiedSlip()
    {
        var i=Straight();i.Jacking=JackingEnd.BothSymmetric;var r=PrestressActions.Calculate(i);
        Assert.AreEqual(12,r.AnchorageRecoveryMm,1e-9);Assert.IsTrue(r.Stages[0].Points.All(p=>Math.Abs(p.InitialStressMpa-1272)<1e-8));
    }
    [TestMethod]
    public void RightJackingMirrorsLeftJackingOnASymmetricParabola()
    {
        var i=Input();var left=PrestressActions.Calculate(i);i.Jacking=JackingEnd.Right;var right=PrestressActions.Calculate(i);
        for(int n=0;n<left.Stages[1].Points.Count;n++)Assert.AreEqual(left.Stages[1].Points[n].EffectiveStressMpa,right.Stages[1].Points[right.Stages[1].Points.Count-1-n].EffectiveStressMpa,1e-8);
        Assert.IsTrue(left.Stages[0].Points.Last().ArcLengthM>i.LengthM);Assert.AreEqual(-.4,left.Stages[0].Points[i.ProfileIntervals].EccentricityM,1e-12);
    }
    [DataTestMethod]
    [DataRow(PrestressRelaxationClass.OrdinaryWireStrand,8.0,61.112613455110996)]
    [DataRow(PrestressRelaxationClass.LowRelaxationWireStrand,2.5,12.547314338071677)]
    [DataRow(PrestressRelaxationClass.HotRolledBar,4.0,27.885938451532972)]
    public void RelaxationClassesMatchIndependentEquationArithmetic(PrestressRelaxationClass kind,double rho,double expected)
    {
        Assert.AreEqual(expected,PrestressActions.RelaxationLoss(kind,rho,1302,1860,1000),1e-10);
        Assert.AreEqual(0,PrestressActions.RelaxationLoss(kind,rho,1302,1860,0));
        Assert.AreEqual(PrestressActions.RelaxationLoss(kind,rho,1302,1860,500000),PrestressActions.RelaxationLoss(kind,rho,1302,1860,1e8));
    }
    [TestMethod]
    public void EquivalentTimeContinuesAnUnchangedRelaxationHistoryExactly()
    {
        var kind=PrestressRelaxationClass.LowRelaxationWireStrand;
        double first=PrestressActions.RelaxationLoss(kind,2.5,1350,1860,168);
        double next=PrestressActions.SubsequentRelaxationLoss(kind,2.5,1350-first,first,1860,8760-168);
        Assert.AreEqual(PrestressActions.RelaxationLoss(kind,2.5,1350,1860,8760),first+next,1e-10);
        double lower=PrestressActions.SubsequentRelaxationLoss(kind,2.5,1250-first,first,1860,8760-168);Assert.IsTrue(lower<next&&lower>0);
    }
    [TestMethod]
    public void CoupledDeferredLossesMatchIndependentEc2Equation546()
    {
        var i=Straight();i.Standard=ClimateStandard.EurocodeFirstGeneration;i.NationalParameters="Valori raccomandati dell’esempio";i.IncludeDeferredLosses=true;i.AnchorageSlipMm=0;i.FckMpa=30;i.ConcreteAreaMm2=200000;i.DryingPerimeterMm=2000;i.TendonAreaMm2=1000;
        var r=PrestressActions.Calculate(i);var p=r.Stages[1].Points[0];
        // Independent Python: Ecm=32836.5680313, phi=1.452432430899, shrink=0.0002019430154243,
        // relaxation=25.2904092040 MPa, coupled denominator=1.064193593377.
        Assert.AreEqual(37.00350035257088,p.ShrinkageContributionMpa,1e-9);Assert.AreEqual(48.62990256765392,p.CreepContributionMpa,1e-9);Assert.AreEqual(19.011886078934847,p.RelaxationContributionMpa,1e-9);
        Assert.AreEqual(1245.3547110008403,p.EffectiveStressMpa,1e-9);Assert.AreEqual(p.EffectiveStressMpa/i.TendonModulusMpa,p.EffectivePrestrain,1e-15);
        Assert.AreEqual(0,r.Stages[0].Points[0].DeferredLossMpa);Assert.AreEqual(-p.EffectiveForceKn,p.ConcreteAxialForceKn,1e-12);
    }
    [TestMethod]
    public void PretensionSeparatesBedRelaxationElasticShorteningAndDeferredState()
    {
        var i=Straight();i.Method=PrestressingMethod.PreTensioned;i.JackingAgeDays=0;i.TransferAgeDays=7;i.Observations[0].AgeDays=7;i.IncludeDeferredLosses=true;
        var r=PrestressActions.Calculate(i);var p=r.Stages[0].Points[0];
        Assert.IsTrue(p.ShortRelaxationLossMpa>0&&p.ElasticLossMpa>0);Assert.AreEqual(p.AfterAnchoringMpa-p.ShortRelaxationLossMpa-p.ElasticLossMpa,p.InitialStressMpa,1e-10);
        Assert.AreEqual(0,p.DeferredLossMpa);Assert.IsTrue(r.Stages[1].Points[0].EffectiveStressMpa<p.EffectiveStressMpa);Assert.AreEqual(1311,p.AfterAnchoringMpa,1e-8);
    }
    [TestMethod]
    public void SequentialPostTensioningUsesTheMeanIdenticalTendonFactor()
    {
        var i=Straight();i.SuccessiveTendons=4;var r=PrestressActions.Calculate(i);double ecm=22000*Math.Pow(4.8,.3);
        Assert.AreEqual(195000/ecm*.375*8,r.Stages[0].Points[0].ElasticLossMpa,1e-9);
    }
    [TestMethod]
    public void UnbondedDeferredLossIsCompatibleWithAUniformLengthwiseStressReduction()
    {
        var i=Input();i.Method=PrestressingMethod.PostTensionedUnbonded;var r=PrestressActions.Calculate(i);var losses=r.Stages[1].Points.Select(p=>p.DeferredLossMpa).ToArray();
        Assert.AreEqual(losses.Min(),losses.Max(),1e-10);Assert.IsTrue(r.Stages[1].Points.First().InitialStressMpa!=r.Stages[1].Points.Last().InitialStressMpa);
    }
    [TestMethod]
    public void InputRoundTripAndRefinementPreserveStateAndSources()
    {
        var i=Input();string before=JsonSerializer.Serialize(i);var r=PrestressActions.Calculate(i);Assert.AreEqual(before,JsonSerializer.Serialize(i));
        var copy=JsonSerializer.Deserialize<PrestressActionsInput>(before)!;copy.ProfileIntervals*=2;var refined=PrestressActions.Calculate(copy);
        Assert.IsTrue(r.Converged&&refined.Converged);Assert.AreEqual(r.Stages[1].Points[0].EffectiveStressMpa,refined.Stages[1].Points[0].EffectiveStressMpa,.03);
    }
    [TestMethod]
    public void NormativeStressExcessIsVisibleAndInvalidModelsAreRejected()
    {
        var i=Input();i.JackingStressMpa=1450;var r=PrestressActions.Calculate(i);Assert.IsFalse(r.WithinStressLimits);Assert.IsTrue(r.Summary.Notes[0].Contains("ATTENZIONE"));
        i=Input();i.SystemSource="";Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.Jacking=JackingEnd.BothSymmetric;i.EndEccentricityM=.1;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.Method=PrestressingMethod.PreTensioned;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.AnchorageSlipMm=1000;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.ProfileIntervals=21;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.ConcreteQpStressMpa=double.NaN;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
        i=Input();i.LengthM=1;Assert.ThrowsException<ArgumentException>(()=>PrestressActions.Calculate(i));
    }
}
