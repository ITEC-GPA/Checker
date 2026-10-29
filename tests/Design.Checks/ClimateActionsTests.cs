using GPC.Design.Actions.Climate;
using GPC.Model.Actions.Climate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class ClimateActionsTests
{
    private static double P(ClimateResult r,string key)=>r.Parameters.Single(v=>v.Symbol==key).Value;
    private static double V(ClimateCase c,string key)=>c.Values.Single(v=>v.Symbol==key).Value;
    private static void Near(double expected,double actual,double tolerance=1e-10)=>Assert.AreEqual(expected,actual,tolerance);

    [DataTestMethod]
    [DataRow(SnowZone.AlpineI,200,1.5)]
    [DataRow(SnowZone.MediterraneanI,200,1.5)]
    [DataRow(SnowZone.II,200,1.0)]
    [DataRow(SnowZone.III,200,.6)]
    [DataRow(SnowZone.AlpineI,728,2.78)]
    [DataRow(SnowZone.MediterraneanI,602,2.70)]
    [DataRow(SnowZone.II,481,1.70)]
    [DataRow(SnowZone.III,481,1.02)]
    public void SnowGroundNormativeAnchors(SnowZone zone,double altitude,double expected)=>Near(expected,SnowActions.NtcGroundLoad(zone,altitude));

    [DataTestMethod]
    [DataRow(0,false,.8)][DataRow(30,false,.8)][DataRow(45,false,.4)][DataRow(60,false,0.0)][DataRow(75,false,0.0)][DataRow(75,true,.8)]
    public void SnowSlopeBoundaries(double angle,bool guards,double expected)=>Near(expected,SnowActions.Shape(angle,guards));

    [TestMethod]
    public void SnowNtcAndEnExposureRemainDifferentAndCasesAreNotSummed()
    {
        var x=new SnowInput{AssignedGroundLoad=true,GroundLoadKnM2=2,Exposure=SnowExposure.Windswept,PitchLeftDeg=30,PitchRightDeg=45};
        var ntc=SnowActions.Calculate(x);Near(.9,P(ntc,"Ce"));Near(1.44,V(ntc.Cases[0],"s · sinistra"));Near(.72,V(ntc.Cases[0],"s · destra"));Near(.72,V(ntc.Cases[1],"s · sinistra"));Near(.36,V(ntc.Cases[2],"s · destra"));Assert.AreEqual(3,ntc.Cases.Count);
        x.Standard=ClimateStandard.EurocodeFirstGeneration;x.NationalParameters="Prova valori raccomandati EN; sk assegnato";
        Near(.8,P(SnowActions.Calculate(x),"Ce"));x.Exposure=SnowExposure.Sheltered;Near(1.2,P(SnowActions.Calculate(x),"Ce"));
    }
    [TestMethod]
    public void SnowDriftTruncationAndCylinderLimits()
    {
        var x=new SnowInput{Roof=SnowRoof.LowerRoof,AssignedGroundLoad=true,GroundLoadKnM2=1,PitchLeftDeg=0,StepHeightM=3,WidthM=3,UpperWidthM=9};
        var r=SnowActions.Calculate(x);Near(6,P(r,"ls"));Near(2,P(r,"μw"));Near(2,V(r.Cases[1],"s · salto x=0"));Near(1.4,V(r.Cases[1],"s · bordo x=b2"));
        x.Roof=SnowRoof.Cylindrical;x.WidthM=20;x.RiseM=5;r=SnowActions.Calculate(x);Near(2,V(r.Cases[1],"μ · picco destro"));Near(1,V(r.Cases[1],"μ · picco sinistro"));Assert.AreEqual(3,r.Cases.Count);
    }
    [DataTestMethod]
    [DataRow(1,1000,25.0)][DataRow(2,1500,36.25)][DataRow(3,1000,36.99)][DataRow(4,1500,48.16)][DataRow(5,1500,39.2)][DataRow(6,1000,38.08)][DataRow(7,1500,35.56)][DataRow(8,1500,30.0)][DataRow(9,1500,50.84)]
    public void NtcWindZonesAndAltitude(int zone,double altitude,double speed)=>Near(speed,WindClimate.NtcBaseSpeed(zone,altitude));

    [TestMethod]
    public void IndependentWindOracleAndHeightClamp()
    {
        // Independent Python oracle uses ce = kr²*ct*ln(z/z0)*(7+ct*ln(z/z0)), not vm/Iv implementation.
        var data=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/climate-independent.json")))!;
        foreach(var row in data["wind"]!.AsArray())
        {
            var x=new WindSiteInput{NtcZone=3,ExposureCategory=row!["category"]!.GetValue<int>(),ReturnYears=row["years"]!.GetValue<double>(),TopographyFactor=row["ct"]!.GetValue<double>()};
            var r=WindClimate.Calculate(x,row["z"]!.GetValue<double>());Near(row["qp"]!.GetValue<double>(),P(r,"qp"));
        }
        var s=new WindSiteInput{ExposureCategory=5};Near(P(WindClimate.Calculate(s,0),"qp"),P(WindClimate.Calculate(s,12),"qp"));
        s.Standard=ClimateStandard.EurocodeFirstGeneration;s.NationalParameters="EN recommended, Italy example only";s.AssignedBaseSpeed=true;s.ExposureCategory=2;
        Near(P(WindClimate.Calculate(new WindSiteInput{ExposureCategory=2},10),"qp"),P(WindClimate.Calculate(s,10),"qp"));
    }
    [TestMethod]
    public void WindAreaInterpolationAndRoofSignCases()
    {
        Near(-2.5,BuildingWindActions.AreaCoefficient(-1.8,-2.5,.5));Near(-2.15,BuildingWindActions.AreaCoefficient(-1.8,-2.5,Math.Sqrt(10)));Near(-1.8,BuildingWindActions.AreaCoefficient(-1.8,-2.5,20));
        var x=new BuildingWindInput{PitchDeg=15,HeightM=10,WidthM=20,DepthM=30,LoadedAreaM2=10};var r=BuildingWindActions.Calculate(x);Assert.AreEqual(8,r.Cases.Count);
        Near(-.9,V(r.Cases[0],"Copertura F · cpe"));Near(-1,V(r.Cases[0],"Copertura J · cpe"));Near(0,V(r.Cases[2],"Copertura J · cpe"));Near(.2,V(r.Cases[4],"Copertura F · cpe"));
        double qp=P(r,"qp");Near(qp*(-.9+.3),V(r.Cases[0],"Copertura F · p"));Near(qp*(-.9-.2),V(r.Cases[1],"Copertura F · p"));
        x.Roof=WindRoof.Monopitch;x.WindDirectionDeg=90;x.PitchDeg=45;r=BuildingWindActions.Calculate(x);Near(-1.4,V(r.Cases[0],"Copertura G · cpe"));
    }
    [TestMethod]
    public void DominantInternalPressureAndFlatParapet()
    {
        var x=new BuildingWindInput{Roof=WindRoof.Flat,HeightM=10,ParapetM=.5,InternalPressure=InternalPressure.DominantFace,DominantOpeningRatio=2.5,DominantExternalCoefficient=.8,InternalReferenceHeightM=10.5};
        var r=BuildingWindActions.Calculate(x);Assert.AreEqual(2,r.Cases.Count);Near(-1.4,V(r.Cases[0],"Copertura F · cpe"));Near(P(r,"qp")*(-1.4-.66),V(r.Cases[0],"Copertura F · p"));
    }
    [TestMethod]
    public void CnrReferenceWidthMomentAndFourSimultaneousSigns()
    {
        var x=new BridgeWindInput{WidthM=10,TotalHeightM=2,DeckCentreHeightM=19,ApplicabilityConfirmed=true};var r=BridgeWindActions.Calculate(x);double q=P(r,"qp");
        Near(.27,P(r,"cfX"));Near(1.2,P(r,"|cfY|"));Near(20,P(r,"z"));Assert.AreEqual(4,r.Cases.Count);
        Near(2.7*q,V(r.Cases[0],"fX"));Near(-12*q,V(r.Cases[0],"fY"));Near(-20*q,V(r.Cases[0],"mZ"));Near(20*q,V(r.Cases[1],"mZ"));
        x.AdjacentDeck=true;x.SecondWidthM=10;x.GapM=1;r=BridgeWindActions.Calculate(x);Assert.AreEqual(16,r.Cases.Count);
        Near(2.7*q,V(r.Cases[0],"fX"));Near((12.6+68.2/11)*q,V(r.Cases[0],"fY")*-1);
    }
    [TestMethod]
    public void BridgeThermalTableSignsSimultaneityAndJoints()
    {
        var x=new BridgeThermalInput{Site=new ThermalSiteInput{AssignedTemperatures=true,MinimumAirC=-10,MaximumAirC=40},Deck=BridgeDeck.ConcreteBox};
        var r=ThermalActions.Bridge(x);Near(-2,P(r,"Te,min"));Near(42,P(r,"Te,max"));Near(27,P(r,"ΔTN,exp"));Near(17,P(r,"ΔTN,con"));Near(10,P(r,"ΔTM,heat"));Near(5,P(r,"ΔTM,cool"));
        Near(9.45,V(r.Cases[1],"ΔTu"));Near(10,V(r.Cases[1],"ΔTM"));Near(27,V(r.Cases[2],"ΔTu"));Near(7.5,V(r.Cases[2],"ΔTM"));
        Near(47,V(r.Cases.Single(c=>c.Name=="Appoggi/giunti · espansione"),"ΔL"));Near(-37,V(r.Cases.Single(c=>c.Name=="Appoggi/giunti · contrazione"),"ΔL"));
        x.SurfacingMm=150;r=ThermalActions.Bridge(x);Near(5,P(r,"ΔTM,heat"));Near(5,P(r,"ΔTM,cool"));x.BearingSettingTemperatureKnown=true;r=ThermalActions.Bridge(x);Near(37,V(r.Cases.Single(c=>c.Name=="Appoggi/giunti · espansione"),"ΔL"));
    }
    [TestMethod]
    public void HomogeneousThermalTransmissionUsesSurfaceResistances()
    {
        var x=new BuildingThermalInput{Method=BuildingThermalMethod.HomogeneousLayer,ProfileSource="Hand oracle: Rtot=.5",Site=new ThermalSiteInput{AssignedTemperatures=true,MinimumAirC=-10,MaximumAirC=40},SummerInnerAirC=20,WinterInnerAirC=20,SolarIncrementC=30,ThicknessM=.3,ConductivityWmK=1,InnerResistanceM2KW=.16,OuterResistanceM2KW=.04};
        var r=ThermalActions.Building(x);Near(.5,P(r,"Rtot"));Near(66,V(r.Cases[0],"Tse"));Near(36,V(r.Cases[0],"Tsi"));Near(36,V(r.Cases[0],"ΔTu"));Near(30,V(r.Cases[0],"ΔTM"));Near(.001,V(r.Cases[0],"κT"));Near(10.8,V(r.Cases[0],"ΔL"));
    }
    [TestMethod]
    public void InvalidAndOutOfScopeInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>SnowActions.Calculate(new SnowInput{AltitudeM=1501}));
        Assert.ThrowsException<ArgumentException>(()=>SnowActions.Calculate(new SnowInput{Standard=ClimateStandard.EurocodeFirstGeneration}));
        Assert.ThrowsException<ArgumentException>(()=>SnowActions.Calculate(new SnowInput{GroundLoadKnM2=double.NaN,AssignedGroundLoad=true}));
        Assert.ThrowsException<ArgumentException>(()=>WindClimate.Calculate(new WindSiteInput(),201));
        Assert.ThrowsException<ArgumentException>(()=>BuildingWindActions.Calculate(new BuildingWindInput{HeightM=100,DepthM=50}));
        Assert.ThrowsException<ArgumentException>(()=>BridgeWindActions.Calculate(new BridgeWindInput()));
        Assert.ThrowsException<ArgumentException>(()=>BridgeWindActions.Calculate(new BridgeWindInput{ApplicabilityConfirmed=true,BridgeLengthM=201}));
        Assert.ThrowsException<ArgumentException>(()=>ThermalActions.Building(new BuildingThermalInput()));
        Assert.ThrowsException<ArgumentException>(()=>ThermalActions.Bridge(new BridgeThermalInput{SurfacingMm=70}));
    }
    [TestMethod]
    public void ParallelCalculationsDoNotMutateInputOrSharedTables()
    {
        var x=new BuildingWindInput();var reference=System.Text.Json.JsonSerializer.Serialize(BuildingWindActions.Calculate(x));
        Parallel.For(0,20,_=>Assert.AreEqual(reference,System.Text.Json.JsonSerializer.Serialize(BuildingWindActions.Calculate(x))));Near(20,x.PitchDeg);
    }
}
