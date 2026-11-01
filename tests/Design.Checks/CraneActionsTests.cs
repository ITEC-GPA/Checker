using GPC.Design.Actions.Cranes;
using GPC.Model.Actions.Cranes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Design.Checks;

[TestClass]
public sealed class CraneActionsTests
{
    private static CraneActionsInput Input()=>new(){NationalParameters="EN prima generazione, valori raccomandati",SupplierSource="Dati nominali della prova",ApplicabilityAssessment="Prova: componenti applicabili assegnate; altre escluse",StaticComponentsConfirmed=true,TrackTolerancesConfirmed=true,CranePositions=new(){new CranePosition()}};
    private static CraneGroupResult Group(CraneActionsResult r,int group)=>r.Groups.First(g=>g.Group==group);
    [DataTestMethod]
    [DataRow(HoistingClass.HC1,1.084)] [DataRow(HoistingClass.HC2,1.168)] [DataRow(HoistingClass.HC3,1.252)] [DataRow(HoistingClass.HC4,1.336)]
    public void HoistingFactorsMatchTable25(HoistingClass hc,double expected)=>Assert.AreEqual(expected,CraneActions.Phi2(hc,.2),1e-12);
    [TestMethod]
    public void FourWheelStaticReactionsAndBothPulsesAreRetained()
    {
        var r=CraneActions.Calculate(Input());var lower=Group(r,1);var upper=r.Groups.Where(g=>g.Group==1).Last();
        // Independently: Qc,left wheel=100/4+20*.95/2=34.5; Qh=200*.95/2=95.
        Assert.AreEqual(142.01,lower.Points[0].VerticalDownKn,1e-10);Assert.AreEqual(28.79,lower.Points[2].VerticalDownKn,1e-10);
        Assert.AreEqual(341.6,lower.Points.Sum(p=>p.VerticalDownKn),1e-10);Assert.AreEqual(365.6,upper.Points.Sum(p=>p.VerticalDownKn),1e-10);
        Assert.AreEqual(2.13015,lower.Points[0].EccentricTorsionKnM,1e-10);
    }
    [TestMethod]
    public void DriveForcesHaveCorrectTotalAndMomentAtMassCentre()
    {
        var i=Input();var g=Group(CraneActions.Calculate(i),4);Assert.AreEqual(15,g.Points.Sum(p=>p.LongitudinalKn),1e-10);Assert.AreEqual(0,g.Points.Sum(p=>p.TransverseKn),1e-10);
        // Static centre y=(100*10+20*1+200*1)/320=3.8125; applied Hx=15.
        Assert.AreEqual(-57.1875,g.Points.Sum(p=>p.XM*p.TransverseKn-p.YM*p.LongitudinalKn),1e-10);
        i.CranePositions[0].TravelDirection=-1;var opposite=Group(CraneActions.Calculate(i),4);Assert.AreEqual(57.1875,opposite.Points.Sum(p=>p.XM*p.TransverseKn-p.YM*p.LongitudinalKn),1e-10);
    }
    [TestMethod]
    public void LoadGroupsExcludeIncompatibleHorizontalComponents()
    {
        var i=Input();i.AutomaticBridgeAcceleration=false;i.IncludeAccidental=true;
        foreach(CraneActionComponent kind in Enum.GetValues(typeof(CraneActionComponent)))i.AuxiliaryActions.Add(new(){Name=kind.ToString(),Component=kind,LongitudinalKn=10,Source="Prova"});
        var r=CraneActions.Calculate(i);
        Assert.AreEqual(25,Group(r,1).Points.Sum(p=>p.LongitudinalKn),1e-12); // 1.5*10 acceleration +10 wind
        Assert.AreEqual(20,Group(r,5).Points.Sum(p=>p.LongitudinalKn),1e-12); // skew + wind
        Assert.AreEqual(10,Group(r,6).Points.Sum(p=>p.LongitudinalKn),1e-12); // crab only
        Assert.AreEqual(0,Group(r,7).Points.Sum(p=>p.LongitudinalKn),1e-12);
        Assert.AreEqual(12.5,Group(r,9).Points.Sum(p=>p.LongitudinalKn),1e-12); // bridge buffer only
        Assert.AreEqual(10,r.Groups.Single(g=>g.Name.Contains("urto carrello")).Points.Sum(p=>p.LongitudinalKn),1e-12);
        Assert.AreEqual(10,Group(r,10).Points.Sum(p=>p.LongitudinalKn),1e-12);
        Assert.IsTrue(r.Groups.Where(g=>g.Group>=9).All(g=>g.Accidental&&!g.Test));
    }
    [TestMethod]
    public void ReleaseRetainsNegativePhiAndWarnsOnLossOfContact()
    {
        var i=Input();i.IncludeRelease=true;i.ReleaseDevice=CraneReleaseDevice.Magnet;i.ReleasedMassFraction=1;
        var r=CraneActions.Calculate(i);var g=Group(r,2);Assert.AreEqual(-1,g.HoistFactor);Assert.AreEqual(-63.95,g.Points[0].VerticalDownKn,1e-12);Assert.IsTrue(r.Summary.Notes.Any(n=>n.Contains("reazione verticale negativa")));
    }
    [TestMethod]
    public void TestsUseQTInsteadOfAddingItToQhAndStaticTestHasNoDrive()
    {
        var r=CraneActions.Calculate(Input());var dynamic=r.Groups.First(g=>g.Name.Contains("prova dinamica"));var stat=r.Groups.First(g=>g.Name.Contains("prova statica"));
        Assert.AreEqual(346.48,dynamic.Points.Sum(p=>p.VerticalDownKn),1e-10); // .9*120+1.1*200*.5*(1+1.168)
        Assert.AreEqual(358,stat.Points.Sum(p=>p.VerticalDownKn),1e-10);Assert.AreEqual(0,stat.Points.Sum(p=>p.LongitudinalKn),1e-10);
    }
    [TestMethod]
    public void EmptyAndResidualGroupsAreDifferent()
    {
        var r=CraneActions.Calculate(Input());Assert.AreEqual(120,Group(r,3).Points.Sum(p=>p.VerticalDownKn),1e-10);Assert.AreEqual(140,Group(r,7).Points.Sum(p=>p.VerticalDownKn),1e-10);
    }
    [TestMethod]
    public void SupplierWheelComponentsPreserveNonUniformWheelPositions()
    {
        var i=Input();i.Distribution=CraneDistribution.SupplierWheelComponents;i.AutomaticBridgeAcceleration=false;
        i.SupplierWheels=new(){new(){Name="A",Rail=1,XM=2,CraneWeightKn=15,HoistLoadKn=90},new(){Name="B",Rail=2,XM=3,CraneWeightKn=25,HoistLoadKn=10}};
        i.CranePositions[0].CraneXM=5;var g=Group(CraneActions.Calculate(i),4);Assert.AreEqual(7,g.Points[0].XM);Assert.AreEqual(8,g.Points[1].XM);Assert.AreEqual(105,g.Points[0].VerticalDownKn);Assert.AreEqual(35,g.Points[1].VerticalDownKn);
    }
    [TestMethod]
    public void SupplierComponentsArePositionSpecificAndKeepSignedForces()
    {
        var i=Input();i.CranePositions.Add(new(){Name="Seconda",CrabYM=10});i.AuxiliaryActions.Add(new(){Name="Guida",Position=2,Component=CraneActionComponent.Skewing,TransverseKn=-20,Source="Costruttore"});
        var r=CraneActions.Calculate(i);Assert.AreEqual(0,Group(r,5).Points.Sum(p=>p.TransverseKn));Assert.AreEqual(-20,r.Groups.Single(g=>g.Group==5&&g.Position==2).Points.Sum(p=>p.TransverseKn));
    }
    [TestMethod]
    public void ValidationRejectsDoubleDynamicsAndUndocumentedAssumptions()
    {
        var i=Input();i.StaticComponentsConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
        i=Input();i.TrackTolerancesConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
        i=Input();i.AuxiliaryActions.Add(new(){Name="Accel",Component=CraneActionComponent.BridgeAcceleration,Source="Costruttore"});Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
        i=Input();i.DynamicTestRatio=1.09;Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
        i=Input();i.CranePositions[0].CrabYM=21;Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
        i=Input();i.Phi4=double.NaN;Assert.ThrowsException<ArgumentException>(()=>CraneActions.Calculate(i));
    }
}
