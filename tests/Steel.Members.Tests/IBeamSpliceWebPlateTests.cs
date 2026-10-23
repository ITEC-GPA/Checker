using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceWebPlateTests
{
    [DataTestMethod]
    [DataRow("Left.Web.EachCover.GrossShear",425057.087946693)]
    [DataRow("Left.Web.EachCover.NetShear",589632.0840069323)]
    [DataRow("Right.Web.EachCover.GrossCompression",935000d)]
    [DataRow("Right.Web.EachCover.NetTension",919145.4545454545)]
    [DataRow("Left.Web.EachCover.ShearReducedBending",52983333.33333333)]
    [DataRow("Left.Web.EachCover.AxialMomentInteraction",51174970.76023391)]
    [DataRow("Web.Member.GrossShear",649941.5288978085)]
    [DataRow("Web.Member.NetShear",760188.5441607186)]
    public void P398WebSectionResistancesMatchIndependentExactArithmetic(string id,double value)
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input());Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Equal(value,Check(r,id).Resistance);
    }
    [DataTestMethod]
    [DataRow("Web.EachCover.GrossArea",3400d)]
    [DataRow("Web.EachCover.NetArea",2740d)]
    [DataRow("Web.EachCover.ElasticModulus",192666.66666666666)]
    [DataRow("Web.Member.ShearArea",4093.57)]
    [DataRow("Web.Member.NetShearArea",3532.57)]
    [DataRow("Left.Web.EachCover.AxialForce",-31912.280701754386)]
    [DataRow("Left.Web.EachCover.ShearForce",75000d)]
    [DataRow("Left.Web.EachCover.SignedMomentAtBoltCentroid",-27327051.92743764)]
    [DataRow("Right.Web.EachCover.SignedMomentAtBoltCentroid",10452051.92743764)]
    public void PublishedGeometryIsRecomputedWithoutBorrowingItsInconsistentRootRadius(string name,double value)
        =>Equal(value,Trace(IBeamSplicePlates.VerifyWebSections(Input()),name));

    [TestMethod]
    public void EquivalentNmInequalityHasTheSameAcceptanceBoundaryAsTheSciLinearSum()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input());
        Equal(.5339925264534287,Check(r,"Left.Web.EachCover.AxialMomentInteraction").Utilization);
        // |N|/Nrd+|M|/Mrd<=1 is equivalent to |M| <= (1-|N|/Nrd)*Mrd.
        double sum=31912.280701754386/935000+27327051.92743764/52983333.33333333;
        Assert.IsTrue(sum<1);Assert.IsTrue(Check(r,"Left.Web.EachCover.AxialMomentInteraction").Passed);
        StringAssert.Contains(r.Scope,"No web block tearing/local buckling/classification");
    }
    [DataTestMethod][DataRow(.49,0d)][DataRow(.5,0d)][DataRow(.75,.25)][DataRow(1d,1d)][DataRow(1.2,1d)]
    public void ShearReductionStartsAtHalfResistanceAndNeverCreatesNegativeCapacity(double shearRatio,double rho)
    {
        double v=-2*425057.087946693*shearRatio;
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(actions:new(0,v,0,0,0,0))));
        Assert.IsTrue(r.IsComplete);Equal(rho,Trace(r,"Left.Web.EachCover.Rho"));
        Equal((1-rho)*52983333.33333333,Check(r,"Left.Web.EachCover.ShearReducedBending").Resistance);
        Assert.IsTrue(r.Checks.All(c=>c.Resistance>=0&&!double.IsNaN(c.Utilization)));
        if(shearRatio>=1)Assert.AreEqual(ConnectionStatus.Failed,r.Status);
    }
    [TestMethod]
    public void AtZeroShearTheOwnWebMomentIsStillCarriedByTheCovers()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(actions:new(0,0,0,0,0,200e6))));
        Equal(18889551.92743764,Check(r,"Left.Web.EachCover.ShearReducedBending").Demand);
        Equal(Check(r,"Left.Web.EachCover.ShearReducedBending").Demand,Check(r,"Right.Web.EachCover.ShearReducedBending").Demand);
        Equal(0,Trace(r,"Left.Web.EachCover.Rho"));
    }
    [TestMethod]
    public void EveryWebCoverTakesHalfAndTheMemberTakesTheWholeShear()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input());
        Equal(75000,Check(r,"Left.Web.EachCover.GrossShear").Demand);Equal(150000,Check(r,"Web.Member.GrossShear").Demand);
        Equal(31912.280701754386,Check(r,"Right.Web.EachCover.GrossCompression").Demand);
        Equal(0,Check(r,"Right.Web.EachCover.GrossTension").Demand);
    }
    [TestMethod]
    public void TensionReversalUsesNetTensileResistanceInTheInteraction()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(actions:new(150000,150000,0,0,0,200e6))));
        Equal(31912.280701754386,Check(r,"Left.Web.EachCover.NetTension").Demand);
        Equal(0,Check(r,"Left.Web.EachCover.GrossCompression").Demand);
        Equal(31912.280701754386/919145.4545454545,Trace(r,"Left.Web.EachCover.AxialRatio"));
        Equal((1-31912.280701754386/919145.4545454545)*52983333.33333333,Check(r,"Left.Web.EachCover.AxialMomentInteraction").Resistance);
    }
    [DataTestMethod][DataRow(-5000000d)][DataRow(5000000d)]
    public void AxialOverloadExhaustsMomentResistanceWithoutClampingAwayTheFailedAxialCheck(double axial)
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(actions:new(axial,0,0,0,0,1e6))));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.IsComplete);
        Equal(0,Check(r,"Left.Web.EachCover.AxialMomentInteraction").Resistance);
        Assert.IsTrue(double.IsPositiveInfinity(Check(r,"Left.Web.EachCover.AxialMomentInteraction").Utilization));
        Assert.IsFalse(Check(r,"Left.Web.EachCover."+(axial>0?"NetTension":"GrossCompression")).Passed);
    }
    [TestMethod]
    public void NtcChangesFactorsWithoutChangingActionsAndGeometry()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(true));
        Equal(425057.087946693/1.05,Check(r,"Left.Web.EachCover.GrossShear").Resistance);
        Equal(589632.0840069323*1.1/1.25,Check(r,"Left.Web.EachCover.NetShear").Resistance);
        Equal(52983333.33333333/1.05,Check(r,"Left.Web.EachCover.ShearReducedBending").Resistance);
        Equal(150000,Check(r,"Web.Member.GrossShear").Demand);
        Assert.IsTrue(r.Checks.All(c=>c.Reference.Contains("NTC")));
    }
    [TestMethod]
    public void TwoRatherThanThreeTransverseRowsRemoveExactlyOneHoleFromNetAreas()
    {
        // With only two transverse rows the free edge becomes110mm, exceeding
        // the exposed-edge corrosion limit80mm. This variant is unexposed.
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(web:new(2,2,85,120,70)),exposed:false));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        Equal(2960,Trace(r,"Web.EachCover.NetArea"));Equal(3719.57,Trace(r,"Web.Member.NetShearArea"));
    }
    [TestMethod]
    public void NetShearCanGovernInsteadOfGrossShear()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(web:new(340,10,275,275)));
        double net=2740*275/(Math.Sqrt(3)*1.1);
        Assert.IsTrue(net<425057.087946693);
        Equal(75000/net,Trace(r,"Left.Web.EachCover.ShearRatio"));
    }
    [TestMethod]
    public void ZeroActionsAreFiniteAndOutputCollectionsAreImmutable()
    {
        var r=IBeamSplicePlates.VerifyWebSections(Input(forces:IBeamSpliceForceTests.Input(actions:new(0,0,0,0,0,0))));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);Equal(0,r.Utilization!.Value);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
        Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionCheck>)r.Checks).Clear());
        StringAssert.Contains(r.Scope,"COMPONENTS ONLY");
    }
    [DataTestMethod][DynamicData(nameof(OutOfScope),typeof(IBeamSplicePlateTests),DynamicDataSourceType.Method)]
    public void InvalidDetailLeavesNoFavourableWebSectionResult(IBeamSplicePlateInput input,string reason)
        =>Unsupported(IBeamSplicePlates.VerifyWebSections(input),reason);
    [TestMethod]
    public void NullOrMissingCombinationIsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>IBeamSplicePlates.VerifyWebSections(null!));
        Unsupported(IBeamSplicePlates.VerifyWebSections(Input(source:null)),"COMBINATION");
    }
}
