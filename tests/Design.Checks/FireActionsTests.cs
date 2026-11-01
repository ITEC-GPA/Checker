using GPC.Design.Actions.Fire;
using GPC.Model.Actions.Fire;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class FireActionsTests
{
    private static FireActionsInput Input(bool flux=false)=>new(){FireScenarioSource="Scenario dimostrativo",BoundaryParametersSource="Parametri generici EN, da confrontare con la norma del materiale",CalculateBoundaryFlux=flux,PrescribedSurfaceStateConfirmed=flux,RadiationEqualsGasConfirmed=flux,FireFaces=new(){new FireFaceInput{SurfaceSource="Temperatura prescritta per prova"}}};
    [DataTestMethod]
    [DataRow(FireCurve.Standard,30d,841.7958796883296)] [DataRow(FireCurve.Standard,60d,945.340051348972)] [DataRow(FireCurve.Standard,120d,1049.039568745648)]
    [DataRow(FireCurve.External,15d,676.2684680930333)] [DataRow(FireCurve.External,60d,679.9999979200801)]
    [DataRow(FireCurve.Hydrocarbon,15d,1071.331865107162)] [DataRow(FireCurve.Hydrocarbon,60d,1099.9843801662266)]
    public void NominalCurvesMatchIndependentValues(FireCurve curve,double time,double expected)=>Assert.AreEqual(expected,FireActions.NominalTemperature(curve,time),1e-9);
    [TestMethod]
    public void AllNominalCurvesStartAtTwentyDegrees()
    {foreach(var curve in new[]{FireCurve.Standard,FireCurve.External,FireCurve.Hydrocarbon})Assert.AreEqual(20,FireActions.NominalTemperature(curve,0),1e-12);}
    [TestMethod]
    public void GasOnlyDoesNotInventSurfaceTemperatureOrFlux()
    {
        var i=Input();var r=FireActions.Calculate(i);Assert.AreEqual(481,r.GasHistory.Count);Assert.IsTrue(r.Faces[0].History.All(p=>p.SurfaceC is null&&p.NetWm2 is null));Assert.AreEqual(.3,r.Faces[0].AreaM2,1e-12);Assert.AreEqual(25,r.Faces[0].ConvectionWm2K);
    }
    [TestMethod]
    public void PrescribedSurfaceFluxMatchesSeparateConvectionAndRadiation()
    {
        var i=Input(true);i.Curve=FireCurve.AssignedHistory;i.GasTemperatures=new(){new(){TimeMinutes=0,TemperatureC=800},new(){TimeMinutes=120,TemperatureC=800}};i.FireFaces[0].AssignedConvection=true;i.FireFaces[0].ReferenceSurfaceC=200;
        var p=FireActions.Calculate(i).Faces[0].History[0];Assert.AreEqual(15000,p.ConvectionWm2!.Value,1e-9);Assert.AreEqual(57856.852404288,p.RadiationWm2!.Value,1e-7);Assert.AreEqual(72856.852404288,p.NetWm2!.Value,1e-7);
    }
    [TestMethod]
    public void HydrocarbonUsesFiftyAndUnexposedCombinedTransferDoesNotDoubleCountRadiation()
    {
        var i=Input(true);i.Curve=FireCurve.Hydrocarbon;i.FireFaces.Add(new(){Name="Non esposta",Exposure=FireExposure.Unexposed,CombinedUnexposedTransfer=true,ReferenceSurfaceC=120,SurfaceSource="Prova"});
        var r=FireActions.Calculate(i);Assert.AreEqual(50,r.Faces[0].ConvectionWm2K);var p=r.Faces[1].History.Last();Assert.AreEqual(-900,p.NetWm2);Assert.AreEqual(0,p.RadiationWm2);
    }
    [TestMethod]
    public void UnexposedSeparateTransferRadiatesToAmbientAndAdiabaticHasZeroFlux()
    {
        var i=Input(true);i.FireFaces[0].Exposure=FireExposure.Unexposed;i.FireFaces[0].ReferenceSurfaceC=120;
        var p=FireActions.Calculate(i).Faces[0].History.Last();Assert.AreEqual(-400,p.ConvectionWm2);Assert.IsTrue(p.RadiationWm2<0);Assert.AreEqual(20,p.GasC);
        i.FireFaces[0].Exposure=FireExposure.Adiabatic;var r=FireActions.Calculate(i);Assert.IsTrue(r.Faces[0].History.All(v=>v.NetWm2==0&&v.SurfaceC is null));
    }
    [TestMethod]
    public void AssignedHistoriesIncludeKnotsAndCoolingKeepsNegativeFlux()
    {
        var i=Input(true);i.Curve=FireCurve.AssignedHistory;i.TimeStepMinutes=3;i.DurationMinutes=60;
        i.GasTemperatures=new(){new(){TimeMinutes=0,TemperatureC=20},new(){TimeMinutes=17.3,TemperatureC=1000},new(){TimeMinutes=60,TemperatureC=20}};
        i.FireFaces[0].AssignedConvection=true;i.FireFaces[0].SurfaceState=FireSurfaceState.AssignedHistory;
        i.SurfaceTemperatures=new(){new(){Face=1,TimeMinutes=0,TemperatureC=20},new(){Face=1,TimeMinutes=31.1,TemperatureC=500},new(){Face=1,TimeMinutes=60,TemperatureC=500}};
        var r=FireActions.Calculate(i);Assert.AreEqual(1000,r.GasHistory.Single(p=>p.TimeMinutes==17.3).TemperatureC,1e-10);Assert.AreEqual(500,r.Faces[0].History.Single(p=>p.TimeMinutes==31.1).SurfaceC);Assert.IsTrue(r.Faces[0].History.Last().NetWm2<0);
    }
    [TestMethod]
    public void NonGridEndTimeAndObservationAreIncludedExactly()
    {
        var i=Input();i.DurationMinutes=31.7;i.TimeStepMinutes=2.2;var r=FireActions.Calculate(i);Assert.AreEqual(31.7,r.GasHistory.Last().TimeMinutes);Assert.IsTrue(r.GasHistory.Any(p=>p.TimeMinutes==15));Assert.IsTrue(r.GasHistory.Any(p=>p.TimeMinutes==30));
    }
    [TestMethod]
    public void ShadowAndEmissivityScaleRadiationOnly()
    {
        var i=Input(true);var original=FireActions.Calculate(i).Faces[0].History.Last();i.FireFaces[0].ConfigurationFactor=.5;var scaled=FireActions.Calculate(i).Faces[0].History.Last();Assert.AreEqual(original.ConvectionWm2,scaled.ConvectionWm2);Assert.AreEqual(original.RadiationWm2!.Value*.5,scaled.RadiationWm2!.Value,1e-9);
    }
    [TestMethod]
    public void InvalidHistoriesAndUnconfirmedSurfaceStateAreRejected()
    {
        var i=Input(true);i.PrescribedSurfaceStateConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input(true);i.RadiationEqualsGasConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input();i.Curve=FireCurve.AssignedHistory;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i.FireFaces[0].AssignedConvection=true;i.GasTemperatures[1].TimeMinutes=0;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input(true);i.FireFaces[0].SurfaceState=FireSurfaceState.AssignedHistory;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input();i.FireFaces[0].EndXM=0;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input();i.FireFaces[0].CombinedUnexposedTransfer=true;Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
        i=Input();i.SurfaceTemperatures.Add(new(){TimeMinutes=double.NaN});Assert.ThrowsException<ArgumentException>(()=>FireActions.Calculate(i));
    }
}
