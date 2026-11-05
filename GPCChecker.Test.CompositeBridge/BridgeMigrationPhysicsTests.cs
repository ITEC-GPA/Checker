using GPC.Checkers.CompositeBridge;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cement=GPC.Model.Materials.ConcreteMaterialEuropeanCommon.CementTypes;

namespace GPCChecker.Test.CompositeBridge;

[TestClass]
public class BridgeMigrationPhysicsTests
{
    [DataTestMethod,DataRow(Cement.ClassS,-.00026577029797417693),DataRow(Cement.ClassN,-.00031895281715993315),DataRow(Cement.ClassR,-.0004224929218062434)]
    public void ShrinkagePortMatchesFrozenBridgheReferences(Cement cement,double expected)
    {
        var result=ConcreteShrinkage.Calculate(30,cement,80,100000,2000,double.PositiveInfinity,7);
        Assert.AreEqual(expected,result.Total,1e-15);Assert.AreEqual(100,result.NotionalSize);Assert.AreEqual(1,result.Kh);
    }
    [TestMethod]
    public void ShrinkageIncrementsAndDomainLimitsAreExplicit()
    {
        double At(double age,double start)=>ConcreteShrinkage.Calculate(32,Cement.ClassN,70,750000,5000,age,7,start).Increment;
        Assert.AreEqual(-.000198939016744924,At(365,7),1e-15);
        Assert.AreEqual(At(365,7),At(28,7)+At(365,28),1e-15);Assert.AreEqual(0,At(28,28),1e-18);
        Assert.ThrowsException<ArgumentException>(()=>At(7,28));
        Assert.ThrowsException<ArgumentException>(()=>ConcreteShrinkage.Calculate(30,Cement.ClassN,101,100000,2000,28,7));
        Assert.AreEqual(0,BridgeCreepCoefficient.FromModularRatio(7,210000,30000),1e-14);
        Assert.AreEqual(2,BridgeCreepCoefficient.FromModularRatio(21,210000,30000),1e-14);
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>BridgeCreepCoefficient.FromModularRatio(6,210000,30000));
    }
    [TestMethod]
    public void CompositeEigenActionsMatchTwoRectangleEquilibriumWithoutMutatingTheSection()
    {
        var concrete=new ConcreteMaterialEN1992("C30",30,ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel=new SteelMaterialEN1993("S355",210000,355,510);
        var section=ReinforcedConcreteSection.CreateBridgeSection(400,200,concrete,null,0,40,null,0,new SectionRectangular(300,200),steel);
        var definition=new CompositeSectionDefinition {PhysicalSection=section,LongTermRatio=10,ShortTermRatio=7,IncludeReinforcement=false};
        string before=ModelValues.Fingerprint(new object[]{definition});double strain=-.0003;
        var result=CompositeShrinkageActions.Calculate(definition,strain);
        double area=60000+80000d/10,centroid=(-150*60000+100*80000d/10)/area;
        double inertia=200*Math.Pow(300,3)/12+60000*Math.Pow(-150-centroid,2)+(400*Math.Pow(200,3)/12+80000*Math.Pow(100-centroid,2))/10;
        double force=210000d/10*80000*strain,moment=force*(100-centroid);
        Assert.AreEqual(centroid,result.Centroid,1e-9);Assert.AreEqual(force,result.EigenForce,1e-6);Assert.AreEqual(moment,result.EigenMoment,1e-5);
        Assert.AreEqual(force/(210000*area),result.FreeStrain,1e-14);Assert.AreEqual(moment/(210000*inertia),result.Curvature,1e-14);
        Assert.AreEqual(-result.FreeStrain,result.PreStrain);Assert.AreEqual(result.Curvature/steel.AlfaThermalExpansion,result.ThermalGradient,1e-14);
        Assert.AreEqual(before,ModelValues.Fingerprint(new object[]{definition}));
        definition.IncludeConcrete=false;Assert.ThrowsException<ArgumentException>(()=>CompositeShrinkageActions.Calculate(definition,strain));
    }
}
