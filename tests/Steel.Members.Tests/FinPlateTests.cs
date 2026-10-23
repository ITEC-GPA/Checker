using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FinPlateTests
{
    private static ConnectionDesignCode Ec(double gm0 = 1, double gm2p = 1.1)
        => ConnectionDesignCode.Eurocode3(gm0, gm2p, 1.25, "Explicit SCI P358 UK benchmark factors, revised2014");
    private static ConnectionRolledISection Beam() => new(412.8, 179.5, 9.5, 16, 10.2, 9450, 275, 410);
    private static ConnectionRolledISection Section(double h, double b, double tw, double tf, double r)
        => new(h, b, tw, tf, r, 2*b*tf + (h-2*tf)*tw + 4*(1-Math.PI/4)*r*r, 275, 410);
    private static ConnectionRolledISection Support() => Section(466, 300, 13.1, 20, 13);
    private static FinPlateLayout Layout(int rows = 4, double topOffset = 50)
        => new(rows, 1, 80+(rows-1)*70, 70, 0, 40, 40, 50, topOffset, 40, 10);
    private static FinPlateInput Input(ConnectionDesignCode? code = null, double v = 200000, int rows = 4,
        ConnectionRolledISection? beam = null, ConnectionRolledISection? support = null, FinPlateActions? actions = null,
        FinPlateActions? supportActions = null, string? pinned = "Fixture-only frame model qualification; standard SCI detail, restrained beam",
        string? holes = "Fixture-only NTC SLE clearance evidence", string? consumable = "Fixture-only qualified matching consumable",
        bool restrained = true, bool notched = false, bool exposed = false, FinPlateSupportFace face = FinPlateSupportFace.ColumnWeb,
        int sides = 1, double throat = 5.6, FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize,
        FinPlateLayout? layout = null, ConnectionPlate? plate = null, ConnectionBolt? bolt = null)
        => new(code ?? Ec(), plate ?? new(100, 10, 275, 410), bolt ?? new(20, 22, 245, ConnectionBoltGrade.Class8_8, true),
            layout ?? Layout(rows), beam ?? Beam(), support ?? Support(), actions ?? FinPlateActions.DownwardShear(v),
            supportActions ?? FinPlateActions.DownwardShear(0), new(throat, ends, consumable!), restrained, pinned!, holes,
            face, sides, notched, exposed);
    private static ConnectionCheck Check(FinPlateResult r, string id) => r.Checks.Single(c => c.Id == id);
    private static double Trace(FinPlateResult r, string name) => r.Trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual, string reason = "")
        => Assert.AreEqual(expected, actual, Math.Max(1e-6, Math.Abs(expected)*1e-9), reason);
    private static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "fin-plate-independent.json")));
    private static void Unsupported(FinPlateResult result, string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status);
        Assert.IsFalse(result.IsComplete); Assert.AreEqual(0, result.Checks.Count);
        Assert.IsNull(result.Utilization); Assert.IsNull(result.LoadUtilization);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), diagnostic);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CompleteOrdinaryUlsMatchesIndependentAnalyticalFixture(bool ntc)
    {
        // The fixture distinguishes published SCI values from independent arithmetic derived using its procedures.
        using var doc = Fixtures();
        string id = ntc ? "NTC2018-derived-unnotched" : "EC3-UK-derived-unnotched";
        var expected = doc.RootElement.GetProperty("derivedCompleteOrdinaryUlsCases").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == id).GetProperty("independentAnalyticalExpected");
        double Expected(string key) => expected.GetProperty(key).GetDouble();
        var r = FinPlate.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec()));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.IsTrue(r.IsComplete);
        Assert.IsTrue(r.ConformsToSciPinnedDetail); Assert.IsFalse(string.IsNullOrWhiteSpace(r.PinnedModelQualificationSource));
        Equal(Expected("oneBoltShearResistance_N"), Check(r,"Bolt1.Shear").Resistance);
        Equal(Expected("plateBearingVerticalPerBolt_N"), Trace(r,"PlateBearing.Vertical.Resistance"));
        Equal(Expected("plateBearingHorizontalPerBolt_N"), Trace(r,"PlateBearing.Horizontal.Resistance"));
        Equal(Expected("beamWebBearingVerticalPerBolt_N"), Trace(r,"BeamBearing.Vertical.Resistance"));
        Equal(Expected("beamWebBearingHorizontalPerBolt_N"), Trace(r,"BeamBearing.Horizontal.Resistance"));
        Equal(Expected("plateGrossShearResistance_N"), Check(r,"Plate.GrossShear").Resistance);
        Equal(Expected("plateNetShearResistance_N"), Check(r,"Plate.NetShear").Resistance);
        Equal(Expected("plateBlockTearingResistance_N"), Check(r,"Plate.BlockTearing").Resistance);
        Equal(Expected("plateElasticFlexuralEquivalentShearResistance_N"), Check(r,"Plate.ShortPlateFlexure").Resistance/50);
        Equal(Expected("beamAv_mm2"), Trace(r,"Beam.Av")); Equal(Expected("beamAvNet_mm2"), Trace(r,"Beam.AvNet"));
        Equal(Expected("beamGrossShearResistance_N"), Check(r,"Beam.GrossShear").Resistance);
        Equal(Expected("beamNetShearResistance_N"), Check(r,"Beam.NetShear").Resistance);
        Equal(Expected("supportLocalShearResistanceForFullV_N"), Check(r,"Support.LocalShear").Resistance*2);
        Equal(Expected("supportPunchingResistanceForFullV_N"), Check(r,"Support.Punching").Resistance);
        Equal(Expected("supportConservativePlateThicknessLimit_mm"), Trace(r,"Support.ConservativePlateThicknessLimit"));
        Equal(Expected("weldMinimumFullStrengthThroat_mm"), Check(r,"Weld.FullStrengthThroat").Demand);
        Equal(Expected("weldDirectionalUtilization"), r.WeldVerification.Utilization!.Value);
        Equal(Expected("governingUtilization"), r.LoadUtilization!.Value);
        Assert.IsTrue(r.LoadGoverningCheck.Id.EndsWith(".Shear"));
        Assert.IsTrue(r.Checks.All(c => c.Passed));
        // Notched-web block formula is not invented for a continuous unnotched I/H web.
        Assert.IsFalse(r.Checks.Any(c => c.Id == "Beam.BlockTearing"));
    }

    [TestMethod]
    public void UnnotchedConnectionMatchesPublishedSciTableG18WithinItsRounding()
    {
        using var doc = Fixtures();
        var published = doc.RootElement.GetProperty("publishedUnnotchedConnection");
        var r = FinPlate.Verify(Input());
        double resistance = 200000 / r.LoadUtilization!.Value;
        Assert.AreEqual(published.GetProperty("shearResistance_N").GetDouble(), resistance,
            published.GetProperty("absoluteTolerance_N").GetDouble(), "SCI P358 T-82/PDF380, unnotched 406x178x74");
        // Keep precise physics; do not emulate the worked example's prematurely rounded 288 kN.
        Equal(285723.44460210844, resistance);
    }

    [TestMethod]
    public void ComponentFramesRecoverPhysicalForceAndMomentWithCorrectSigns()
    {
        var r = FinPlate.Verify(Input());
        var b = r.BoltDistribution;
        Equal(0, b.Forces.Sum(f => f.Fx)); Equal(-200000, b.Forces.Sum(f => f.Fy)); Equal(0,b.Forces.Sum(f=>f.Fz));
        Equal(10000000,b.Forces.Sum(f=>f.Position.X*f.Fy-f.Position.Y*f.Fx));
        Equal(10000000,b.ActionsAtCentroid.Mz); Equal(0,b.ActionsAtCentroid.Mx);
        Equal(-200000,Trace(r,"WeldGroup.RecoveredFy"));
        Equal(10000000,Trace(r,"WeldGroup.RecoveredMx")); Equal(0,Trace(r,"WeldGroup.RecoveredMz"));
        Assert.AreEqual(2,r.WeldVerification.Input.Segments.Count);
        Equal(-5,r.WeldVerification.Input.Segments[0].Start.X); Equal(5,r.WeldVerification.Input.Segments[1].Start.X);
        // A 10mm plate has two separate roots; zero-offset coincident roots would double-count a weld.
        Equal(3248,Trace(r,"WeldGroup.Area"));
    }

    [DataTestMethod] [DataRow(0.0000001)] [DataRow(.4)] [DataRow(1.0)] [DataRow(200000.0)]
    public void NoSmallVerticalForceIsDroppedAndDistributionIsLinear(double v)
    {
        var r = FinPlate.Verify(Input(v:v));
        Assert.AreEqual(-v,r.BoltDistribution.Forces.Sum(f=>f.Fy),Math.Abs(v)*1e-12);
        Assert.IsTrue(r.BoltDistribution.Forces.All(f=>f.Shear>0));
        Assert.AreEqual(v/285723.44460210844,r.LoadUtilization!.Value, Math.Abs(v/285723.44460210844)*1e-9);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
    }

    [TestMethod]
    public void ZeroActionsRetainDetailAndHierarchyChecksButHaveZeroLoadUtilization()
    {
        var r=FinPlate.Verify(Input(v:0));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status); Equal(0,r.LoadUtilization!.Value);
        Assert.IsTrue(Check(r,"Weld.FullStrengthThroat").Demand>0);
        Equal(0,Check(r,"Support.Punching").Demand);
    }

    [DataTestMethod] [DataRow(0.999999,ConnectionStatus.Passed)] [DataRow(1.000001,ConnectionStatus.Failed)]
    public void PreciseBoltCapacityBoundaryChangesStatus(double factor, ConnectionStatus expected)
    {
        var r=FinPlate.Verify(Input(v:285723.44460210844*factor));
        Assert.AreEqual(expected,r.Status); Equal(factor,r.LoadUtilization!.Value);
    }

    [DataTestMethod] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void EachQualifiedRowCountBalancesActionsAndChecksAllComponents(int rows)
    {
        var beam=rows==2 ? Section(220,110,6,10,8) : rows==3 ? Section(300,150,7,12,8)
            : rows==4 ? Beam() : Section(450,180,9.5,16,10);
        var r=FinPlate.Verify(Input(rows:rows,beam:beam,v:1000));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.AreEqual(rows,r.BoltDistribution.Forces.Count);
        Equal(-1000,r.BoltDistribution.Forces.Sum(f=>f.Fy));
        foreach(string id in new[]{"Plate.GrossShear","Plate.NetShear","Plate.BlockTearing","Plate.ShortPlateFlexure",
            "Beam.GrossShear","Beam.NetShear","Support.LocalShear","Support.Punching","Weld.FullStrengthThroat"})
            Assert.IsTrue(Check(r,id).Passed,id);
    }

    [TestMethod]
    public void WeldFullStrengthHierarchyCannotBeReplacedWithSmallActualDemandCheck()
    {
        var r=FinPlate.Verify(Input(v:1,throat:3));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.WeldVerification.Points.All(p=>p.Verification.Status==ConnectionStatus.Passed));
        Assert.IsFalse(Check(r,"Weld.FullStrengthThroat").Passed);
    }

    [TestMethod]
    public void LocalSupportFailureIsIncludedInTheJointStatus()
    {
        var r=FinPlate.Verify(Input(v:600000,support:Section(230,200,5,15,10)));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsFalse(Check(r,"Support.Punching").Passed);
        Assert.IsFalse(Check(r,"Support.LocalShear").Passed);
    }

    [TestMethod]
    public void PunchingUsesPlateFactorAndChangesIndependentlyFromBoltFactor()
    {
        var a=FinPlate.Verify(Input(code:Ec(gm2p:1.1))); var b=FinPlate.Verify(Input(code:Ec(gm2p:1.25)));
        Equal(Check(a,"Support.Punching").Resistance*1.1/1.25,Check(b,"Support.Punching").Resistance);
        Equal(Check(a,"Bolt1.Shear").Resistance,Check(b,"Bolt1.Shear").Resistance);
        Equal(Check(a,"Weld1.End.DirectionalEquivalent").Resistance,Check(b,"Weld1.End.DirectionalEquivalent").Resistance);
    }

    [TestMethod]
    public void InsufficientPlateHeightFailsDetailEvenWithNegligibleLoad()
    {
        var r=FinPlate.Verify(Input(rows:3,v:1));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status); Assert.IsFalse(r.ConformsToSciPinnedDetail);
        Assert.IsFalse(Check(r,"Detail.MinimumPlateHeight").Passed);
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void ExcessivePitchCannotQualifyPinnedDetailEvenWhenLoadsAreNegligible(bool hasModelEvidence)
    {
        // Independent geometry: cw/tw=184/4=46 < 60sqrt(235/275), so the beam web is
        // within the non-slender scope, but p1=70 mm exceeds 14tw=56 mm.
        var r=FinPlate.Verify(Input(rows:2,beam:Section(220,110,4,10,8),v:1,
            pinned:hasModelEvidence ? "Fixture-only global model evidence" : null));
        Assert.IsFalse(r.ConformsToSciPinnedDetail);
        if(hasModelEvidence)
        {
            Assert.AreEqual(ConnectionStatus.Failed,r.Status);
            Equal(70,Check(r,"Detail.Pitch").Demand); Equal(56,Check(r,"Detail.Pitch").Resistance);
            Assert.IsFalse(Check(r,"Detail.Pitch").Passed);
            Assert.IsTrue(r.LoadUtilization!.Value<1);
        }
        else Unsupported(r,"PINNED_FRAME_MODEL_QUALIFICATION_REQUIRED");
    }

    [DataTestMethod] [DataRow("Fx")] [DataRow("Fy")] [DataRow("Mx")] [DataRow("My")] [DataRow("Mz")] [DataRow("Upward")]
    public void AnyOutOfScopeActionEvenTinyIsNotSilentlyIgnored(string component)
    {
        const double small=1e-100;
        var a=new FinPlateActions(component=="Fx"?small:0,component=="Fy"?small:0,component=="Upward"?small:-200000,
            component=="Mx"?small:0,component=="My"?small:0,component=="Mz"?small:0);
        Unsupported(FinPlate.Verify(Input(actions:a)),"ORDINARY_DOWNWARD_VERTICAL_SHEAR_ONLY");
    }

    [TestMethod]
    public void ExistingSupportActionsRequireAnExplicitInteractionModel()
        => Unsupported(FinPlate.Verify(Input(supportActions:new(1e-100,0,0,0,0,0))),"PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");

    [DataTestMethod] [DataRow(FinPlateSupportFace.ColumnFlange)] [DataRow(FinPlateSupportFace.BeamWeb)] [DataRow(FinPlateSupportFace.HollowWall)]
    public void UnsupportedFacesDoNotBorrowTheColumnWebFormula(FinPlateSupportFace face)
        => Unsupported(FinPlate.Verify(Input(face:face)),"ONLY_IH_COLUMN_WEB_SUPPORT_QUALIFIED");

    [DataTestMethod] [DataRow("Notched")] [DataRow("Unrestrained")] [DataRow("TwoSides")] [DataRow("Exposed")]
    public void MissingScopeExtensionsNeverProducePartialSuccess(string condition)
    {
        var p=Input(notched:condition=="Notched",restrained:condition!="Unrestrained",sides:condition=="TwoSides"?2:1,exposed:condition=="Exposed");
        string expected=condition switch {"Notched"=>"NOTCHED_BEAM_REQUIRES_CHECKS_4_TO_7","Unrestrained"=>"LATERAL_RESTRAINT_REQUIRED_BY_INITIAL_SCOPE",
            "TwoSides"=>"ONLY_ONE_LOADED_SUPPORT_SIDE_QUALIFIED",_=>"EXPOSED_DETAIL_NOT_QUALIFIED"};
        Unsupported(FinPlate.Verify(p),expected);
    }

    [DataTestMethod] [DataRow(null)] [DataRow("")] [DataRow(" ")]
    public void MissingPinnedModelEvidenceCannotBecomeAQualifiedWholeJoint(string? source)
    {
        var r=FinPlate.Verify(Input(pinned:source));
        Unsupported(r,"PINNED_FRAME_MODEL_QUALIFICATION_REQUIRED");
        Assert.IsTrue(r.ConformsToSciPinnedDetail,"Geometric conformity is separate from caller's global-model qualification.");
    }

    [TestMethod]
    public void NtcRequiresClearanceEvidenceWhileEcBenchmarkDoesNot()
    {
        Unsupported(FinPlate.Verify(Input(code:ConnectionDesignCode.Ntc2018(),holes:null)),"NTC_22_MM_HOLE_REQUIRES_SLE_EVIDENCE");
        Assert.AreEqual(ConnectionStatus.Passed,FinPlate.Verify(Input(holes:null)).Status);
    }

    [TestMethod]
    public void WeldConsumableEvidenceIsNotFabricated()
        => Unsupported(FinPlate.Verify(Input(consumable:null)),"WELD_CONSUMABLE_EVIDENCE_REQUIRED");

    [DataTestMethod] [DataRow(FilletWeldEndCondition.Defective)] [DataRow(FilletWeldEndCondition.NominalWithoutFullSizeEvidence)]
    public void FullStrengthDetailRequiresQualifiedWeldEnds(FilletWeldEndCondition condition)
        => Unsupported(FinPlate.Verify(Input(ends:condition)),"FULL_SIZE_WELD_TERMINATIONS_REQUIRED");

    [DataTestMethod] [DataRow(1)] [DataRow(6)]
    public void RowCountsOutsideTheDocumentedShortJointAreRejected(int rows)
        => Unsupported(FinPlate.Verify(Input(rows:rows)),"STANDARD_SINGLE_COLUMN_2_TO_5_ROWS_ONLY");

    [TestMethod]
    public void DoubleColumnIsNotMistakenForTwoIndependentSingleColumns()
    {
        var layout=new FinPlateLayout(4,2,290,70,60,40,40,50,50,40,10);
        Unsupported(FinPlate.Verify(Input(layout:layout,plate:new(160,10,275,410))),"STANDARD_SINGLE_COLUMN_2_TO_5_ROWS_ONLY");
    }

    [TestMethod]
    public void LongPlateDoesNotSilentlySkipLateralTorsionalBuckling()
    {
        var layout=new FinPlateLayout(4,1,290,70,0,40,40,50,50,70,10);
        Unsupported(FinPlate.Verify(Input(layout:layout,plate:new(130,10,275,410))),"LONG_FIN_PLATE_NOT_QUALIFIED");
    }

    [TestMethod]
    public void SupportSlendernessIsAnApplicabilityGate()
        => Unsupported(FinPlate.Verify(Input(support:Section(600,300,8,20,13))),"SUPPORT_WEB_SLENDERNESS_OUTSIDE_SCI_SCOPE");

    [TestMethod]
    public void PlateCannotOverlapBeamRoots()
        => Unsupported(FinPlate.Verify(Input(beam:Section(340,179.5,9.5,16,10.2))),"PLATE_INTERSECTS_BEAM_FLANGE_OR_ROOT");

    [TestMethod]
    public void OverflowingMomentTransportIsExplicitlyUnsupported()
        => Unsupported(FinPlate.Verify(Input(v:double.MaxValue)),"ACTION_TRANSPORT_OUTSIDE_NUMERIC_RANGE");

    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(double.NegativeInfinity)]
    public void NonfiniteActionsAreInvalidInput(double value)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new FinPlateActions(0,0,value,0,0,0));

    [TestMethod]
    public void RolledSectionAcceptsRoundedCatalogueAreaButRejectsInflatedArea()
    {
        // 2*179.5*16+(412.8-32)*9.5+4(1-pi/4)*10.2^2 = 9450.90870 mm2.
        // The independently tabulated 94.5 cm2 is compatible; a larger bounding box is not an area source.
        var catalogue=Beam(); Equal(9450,catalogue.Area);
        Assert.ThrowsException<ArgumentException>(()=>new ConnectionRolledISection(412.8,179.5,9.5,16,10.2,10000,275,410));
        Assert.ThrowsException<ArgumentException>(()=>new ConnectionRolledISection(412.8,179.5,9.5,16,10.2,18900,275,410));
        Assert.ThrowsException<ArgumentException>(()=>new ConnectionRolledISection(412.8,179.5,9.5,16,10.2,9300,275,410));
    }

    [TestMethod]
    public void InconsistentDimensionsAndMissingSupportAreInvalidInput()
    {
        Assert.ThrowsException<ArgumentException>(()=>new FinPlateLayout(4,1,300,70,0,40,40,50,50,40,10));
        Assert.ThrowsException<ArgumentException>(()=>Input(plate:new(120,10,275,410)));
        Assert.ThrowsException<ArgumentException>(()=>new ConnectionRolledISection(412.8,179.5,9.5,16,10.2,100,275,410));
        Assert.ThrowsException<ArgumentNullException>(()=>new FinPlateInput(Ec(),new(100,10,275,410),new(20,22,245,ConnectionBoltGrade.Class8_8,true),
            Layout(),Beam(),null!,FinPlateActions.DownwardShear(1),FinPlateActions.DownwardShear(0),new(5.6,FilletWeldEndCondition.FullSize,"fixture"),true,"fixture"));
    }
}
