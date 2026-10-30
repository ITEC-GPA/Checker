using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using GPC.Design.Actions.General;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class GeneralActionTests
{
    private static double Value(ClimateResult r,string name,string symbol)=>r.Cases.Single(c=>c.Name==name).Values.Single(v=>v.Symbol==symbol).Value;
    private static double Parameter(ClimateResult r,string symbol)=>r.Parameters.Single(v=>v.Symbol==symbol).Value;
    [TestMethod] public void BuildingLayersAndPartitionsHaveIndependentHandCalculation()
    {
        var x=new BuildingLoadsInput{PartitionLineKnM=2.1,PartitionDistributionConfirmed=true};
        var r=BuildingLoads.Calculate(x);Assert.AreEqual(6.25,Value(r,"Distribuito globale","G1"),1e-12);
        Assert.AreEqual(2.84,Value(r,"Distribuito globale","G2"),1e-12);Assert.AreEqual(10,Value(r,"Distribuito globale","Qk·b"),1e-12);
        Assert.AreEqual(800,Value(r,"Concentrato locale","p"),1e-10);
    }
    [DataTestMethod][DataRow(1,.4)][DataRow(1.00001,.8)][DataRow(2,.8)][DataRow(3,1.2)][DataRow(5,2)]
    public void NtcPartitionThresholds(double line,double expected)
    {var r=BuildingLoads.Calculate(new(){PartitionLineKnM=line,PartitionDistributionConfirmed=true});Assert.AreEqual(expected,Parameter(r,"partizioni"),1e-12);}
    [TestMethod] public void EnMovablePartitionsBelongToQAndNeedAssignedNationalLoads()
    {
        var x=new BuildingLoadsInput{Standard=ClimateStandard.EurocodeFirstGeneration,NationalParameters="Caso di prova EN, parametri assegnati",PartitionLineKnM=1,PartitionDistributionConfirmed=true};
        Assert.ThrowsException<ArgumentException>(()=>BuildingLoads.Calculate(x));x.AssignedImposedLoads=true;var r=BuildingLoads.Calculate(x);
        Assert.AreEqual(1.64,Value(r,"Distribuito globale","G2"),1e-12);Assert.AreEqual(2.5,Value(r,"Distribuito globale","Qk"),1e-12);
        x.PartitionLineKnM=3.1;Assert.ThrowsException<ArgumentException>(()=>BuildingLoads.Calculate(x));
    }
    [TestMethod] public void ReductionsAreExclusiveAndRespectCategoryFloor()
    {
        var x=new BuildingLoadsInput{Reduction=ImposedReduction.LoadedArea,LoadedAreaM2=100};
        Assert.AreEqual(.6,Parameter(BuildingLoads.Calculate(x),"α"),1e-12);
        x.Occupancy=OccupancyCategory.GatheringC3;x.LoadedAreaM2=1000;Assert.AreEqual(.6,Parameter(BuildingLoads.Calculate(x),"α"),1e-12);
        x.Reduction=ImposedReduction.Storeys;x.Storeys=10;Assert.AreEqual(.76,Parameter(BuildingLoads.Calculate(x),"α"),1e-12);
        x.Storeys=2;Assert.ThrowsException<ArgumentException>(()=>BuildingLoads.Calculate(x));
    }
    [TestMethod] public void ParkingReturnsTwoWheelLoadsWithoutHalvingAgain()
    {var r=BuildingLoads.Calculate(new(){Occupancy=OccupancyCategory.VehiclesF});Assert.AreEqual(10,Value(r,"Concentrato locale","Qk"));Assert.AreEqual(2,Value(r,"Concentrato locale","n"));Assert.AreEqual(.1,Value(r,"Concentrato locale","a"));}
    [DataTestMethod][DataRow(5.39,1,3,.0)][DataRow(5.4,2,2.7,0)][DataRow(6,2,3,0)][DataRow(10,3,3,1)]
    public void TrafficLaneBoundaries(double width,int n,double lane,double remaining)
    {var r=RoadTrafficActions.Calculate(new(){CarriagewayWidthM=width});Assert.AreEqual(n,Parameter(r,"n"));Assert.AreEqual(lane,Parameter(r,"w1"),1e-12);if(width>=5.4)Assert.AreEqual(remaining,Parameter(r,"wr"),1e-12);}
    [TestMethod] public void TrafficActionsMatchHandTotalsAndBraking()
    {
        var r=RoadTrafficActions.Calculate(new());Assert.AreEqual(1200,Value(r,"LM1 / schema 1 · totale assegnato","ΣTS"),1e-12);
        Assert.AreEqual(1112.5,Value(r,"LM1 / schema 1 · totale assegnato","ΣUDL"),1e-12);Assert.AreEqual(427.5,Value(r,"Frenatura +","Qlong"),1e-12);
        Assert.AreEqual(-427.5,Value(r,"Frenatura − / accelerazione","Qlong"),1e-12);Assert.AreEqual(0,Value(r,"Centrifuga","Qcentr"));
        Assert.AreEqual(.76,Value(r,"LM2 / schema 2 · locale","ax"),1e-12);Assert.AreEqual(1.01,Value(r,"LM2 / schema 2 · locale","ay"),1e-12);
    }
    [DataTestMethod][DataRow(199,240)][DataRow(200,240)][DataRow(1000,48)][DataRow(1500,32)][DataRow(1501,0)]
    public void CentrifugalPiecewise(double radius,double expected)=>Assert.AreEqual(expected,Value(RoadTrafficActions.Calculate(new(){StraightBridge=false,RadiusM=radius}),"Centrifuga","Qcentr"),1e-12);
    [TestMethod] public void EnFactorsAndJointAreNotSilentlyAppliedToOtherCases()
    {
        var x=new RoadTrafficInput{Standard=ClimateStandard.EurocodeFirstGeneration,NationalParameters="EN di prova",AlphaQ1=.8,Alphaq1=.7,BetaQ=.8,AddJointAmplification=true,JointDistanceM=0};
        var r=RoadTrafficActions.Calculate(x);Assert.AreEqual(240,Value(r,"Corsia 1","Qasse"),1e-12);Assert.AreEqual(416,Value(r,"LM2 / schema 2 · locale","Qasse"),1e-12);
        Assert.AreEqual(335.25,Value(r,"Frenatura +","Qlong"),1e-12);Assert.AreEqual(3,Value(r,"Marciapiedi e folla","qconcom"),1e-12);
        x.LoadedLengthM=300;Assert.IsTrue(Value(RoadTrafficActions.Calculate(x),"Frenatura +","Qlong")<=900);
    }
    [TestMethod] public void ImpactNtcAndEnHaveDistinctSuperstructureForces()
    {
        var x=new VehicleImpactInput{Target=ImpactTarget.Superstructure,ClearanceM=5.5};var n=VehicleImpact.Calculate(x);
        Assert.AreEqual(500,Value(n,"Prospetto verticale","Fx"),1e-12);Assert.AreEqual(86.82408883346517,Value(n,"Intradosso · 10° verso l’alto","Fz"),1e-10);
        x.Standard=ClimateStandard.EurocodeFirstGeneration;x.NationalParameters="EN raccomandato";Assert.AreEqual(250,Value(VehicleImpact.Calculate(x),"Prospetto verticale","Fx"),1e-12);
        x.ClearanceM=6;Assert.AreEqual(0,Value(VehicleImpact.Calculate(x),"Prospetto verticale","Fx"));
    }
    [TestMethod] public void ImpactDirectionsAreAlternativeAndHeightDependsOnStandard()
    {
        var x=new VehicleImpactInput();var r=VehicleImpact.Calculate(x);Assert.AreEqual(2,r.Cases.Count);Assert.AreEqual(1250,Value(r,"Parallelo alla marcia","M"));Assert.AreEqual(500,Value(r,"Ortogonale alla marcia","F"));
        x.Standard=ClimateStandard.EurocodeFirstGeneration;x.NationalParameters="EN di prova";Assert.AreEqual(1500,Value(VehicleImpact.Calculate(x),"Parallelo alla marcia","M"));
        x.Target=ImpactTarget.Forklift;Assert.AreEqual(250,Value(VehicleImpact.Calculate(x),"Carrello elevatore","F"));
    }
    [TestMethod] public void NtcCombinationExtremaUseBuildingTableNotGeotechnicalG2()
    {
        var r=ActionCombinations.Calculate(new());Assert.AreEqual(94,Parameter(r,"Emin"),1e-12);Assert.AreEqual(250,Parameter(r,"Emax"),1e-12);
        Assert.IsTrue(r.Cases.Any(c=>c.Values.Single(v=>v.Symbol=="G2").Value==.8));
    }
    [TestMethod] public void En610And610abHaveIndependentHandExtrema()
    {
        var x=new ActionCombinationsInput{Standard=ClimateStandard.EurocodeFirstGeneration,NationalParameters="EN recommended",RecommendedEnFactorsConfirmed=true};
        Assert.AreEqual(250.5,Parameter(ActionCombinations.Calculate(x),"Emax"),1e-12);
        x.EnExpression=EnUltimateExpression.Equations610a610b;var r=ActionCombinations.Calculate(x);Assert.AreEqual(228,Parameter(r,"Emax"),1e-12);
        Assert.IsTrue(r.Cases.Any(c=>c.Name.StartsWith("EN 6.10a")));Assert.IsTrue(r.Cases.Any(c=>c.Name.StartsWith("EN 6.10b")));
    }
    [TestMethod] public void CorrelationKeepsCommonSourceTogetherAndExclusionSurvivesServiceCases()
    {
        var x=new ActionCombinationsInput();x.Actions[0].Correlation=x.Actions[1].Correlation="Peso";
        var r=ActionCombinations.Calculate(x);Assert.IsTrue(r.Cases.All(c=>{double g1=c.Values[0].Value,g2=c.Values[1].Value;return g1==1&&g2==.8||g1==1.3&&g2==1.5;}));
        x.Family=CombinationFamily.Characteristic;x.Actions[2].ExclusiveGroup=x.Actions[3].ExclusiveGroup="Alternativa";
        r=ActionCombinations.Calculate(x);Assert.IsTrue(r.Cases.All(c=>c.Values[2].Value==0||c.Values[3].Value==0));Assert.IsTrue(r.Cases.Any(c=>c.Values[3].Value==1));
    }
    [TestMethod] public void AccidentalEnLeadingPsi1DiffersFromNtcPsi2()
    {
        var x=new ActionCombinationsInput{Family=CombinationFamily.Accidental};x.Actions.Add(new(){Name="Urto",Nature=ActionNature.Accidental,Effect=200});
        Assert.AreEqual(345,Parameter(ActionCombinations.Calculate(x),"Emax"),1e-12);
        x.Standard=ClimateStandard.EurocodeFirstGeneration;x.NationalParameters="EN test";x.RecommendedEnFactorsConfirmed=true;
        Assert.AreEqual(355,Parameter(ActionCombinations.Calculate(x),"Emax"),1e-12);x.EnAccidentalLeadingPsi1=false;Assert.AreEqual(345,Parameter(ActionCombinations.Calculate(x),"Emax"),1e-12);
    }
    [TestMethod] public void InvalidInputsAndCancellationAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>BuildingLoads.Calculate(new(){TributaryWidthM=double.NaN}));
        Assert.ThrowsException<ArgumentException>(()=>RoadTrafficActions.Calculate(new(){CarriagewayWidthM=2}));
        Assert.ThrowsException<ArgumentException>(()=>VehicleImpact.Calculate(new(){AssignedImpactForce=true}));
        var x=new ActionCombinationsInput();x.Actions[1].Name="G1";Assert.ThrowsException<ArgumentException>(()=>ActionCombinations.Calculate(x));
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsException<OperationCanceledException>(()=>ActionCombinations.Calculate(new(),cancel.Token));
    }
}
