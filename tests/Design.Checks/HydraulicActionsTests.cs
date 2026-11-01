using GPC.Design.Actions.Hydraulics;
using GPC.Model.Actions.Hydraulics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;

[TestClass]
public sealed class HydraulicActionsTests
{
    private static void Near(double expected, double actual) => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)));
    private static HydraulicActionsInput Wall(double left = 3, double right = 0) => new()
    { UnitWeightKnM3 = 10, WidthM = 2, TopElevationM = 5, WaterCases = new() { new() { LeftLevelM = left, RightLevelM = right } } };
    private static HydraulicCaseResult First(HydraulicActionsInput x) => HydraulicActions.Calculate(x).Cases[0];

    [TestMethod]
    public void WallTriangleHasIndependentAreaAndCentroid()
    {
        // Triangle: base pressure 30 kPa, height 3 m, width 2 m => F=90 kN, zR=1 m.
        var r = First(Wall()); Near(90, r.NetForceKn); Near(90, r.MomentKnM); Near(1, r.ApplicationCoordinateM!.Value);
        Assert.AreEqual(3, r.Diagram.Count); Near(30, r.Diagram[0].NetPressureKpa); Near(0, r.Diagram[1].NetPressureKpa);
    }
    [TestMethod]
    public void WallOpposingWaterIsIntegratedSeparatelyAndDirectionReverses()
    {
        var r = First(Wall(3, 1)); Near(90, r.PositiveForceKn); Near(10, r.OpposingForceKn); Near(80, r.NetForceKn); Near(260.0 / 3, r.MomentKnM);
        var reversed = First(Wall(1, 3)); Near(-r.NetForceKn, reversed.NetForceKn); Near(-r.MomentKnM, reversed.MomentKnM); Near(r.ApplicationCoordinateM!.Value, reversed.ApplicationCoordinateM!.Value);
    }
    [TestMethod]
    public void FullySubmergedWallAndDatumTranslation()
    {
        var x = Wall(10, 8); x.BottomElevationM = 1; x.TopElevationM = 4;
        var r = First(x); Near(120, r.NetForceKn); Near(180, r.MomentKnM); Near(2.5, r.ApplicationCoordinateM!.Value);
        x.BottomElevationM += 100; x.TopElevationM += 100; x.WaterCases[0].LeftLevelM += 100; x.WaterCases[0].RightLevelM += 100;
        var shifted = First(x); Near(r.NetForceKn, shifted.NetForceKn); Near(r.MomentKnM, shifted.MomentKnM); Near(102.5, shifted.ApplicationCoordinateM!.Value);
    }
    [TestMethod]
    public void DryAndBalancedWallsHaveNoInventedResultantCoordinate()
    {
        foreach (var x in new[] { Wall(-2, -1), Wall(3, 3) })
        { var r = First(x); Near(0, r.NetForceKn); Near(0, r.MomentKnM); Assert.IsNull(r.ApplicationCoordinateM); }
    }
    [TestMethod]
    public void SubmergedSlabSatisfiesArchimedesAndDoesNotAddSelfWeight()
    {
        var x = Wall(5, 5); x.Element = HydraulicElement.HorizontalSlab; x.SlabLengthM = 10; x.SlabThicknessM = .5;
        var r = First(x); Near(1000, r.PositiveForceKn); Near(900, r.OpposingForceKn); Near(100, r.NetForceKn); Near(500, r.MomentKnM); Near(5, r.ApplicationCoordinateM!.Value);
        // Independent displaced volume: 10 × 2 × .5 = 10 m³, γ=10 kN/m³ => 100 kN.
        x.WaterCases[0].RightLevelM = 0; Near(1000, First(x).NetForceKn);
    }
    [TestMethod]
    public void AssignedUpliftSubtractsTopWaterAndKeepsTrapezoidFirstMoment()
    {
        var x = Wall(0, 1.5); x.Element = HydraulicElement.HorizontalSlab; x.Uplift = UpliftDistribution.AssignedPressureProfile;
        x.PressureSource = "Independent triangular and rectangular areas";
        x.PressureProfile = new() { new() { PositionM = 0, PressureKpa = 20 }, new() { PositionM = 10, PressureKpa = 40 } };
        var r = First(x); Near(600, r.PositiveForceKn); Near(200, r.OpposingForceKn); Near(400, r.NetForceKn);
        Near(7000.0 / 3, r.MomentKnM); Near(35.0 / 6, r.ApplicationCoordinateM!.Value);
    }
    [TestMethod]
    public void AssignedUpliftCanProducePureCoupleWithoutInvalidDivision()
    {
        var x = Wall(0, 3.5); x.Element = HydraulicElement.HorizontalSlab; x.Uplift = UpliftDistribution.AssignedPressureProfile;
        x.PressureSource = "Independent equilibrium case";
        x.PressureProfile = new() { new() { PositionM = 0, PressureKpa = 20 }, new() { PositionM = 10, PressureKpa = 40 } };
        var r = First(x); Near(0, r.NetForceKn); Near(1000.0 / 3, r.MomentKnM); Assert.IsNull(r.ApplicationCoordinateM);
    }
    private static HydraulicActionsInput Pier() => new()
    { Element = HydraulicElement.RiverPier, DragCoefficient = 1.2, DragSource = "Assigned test coefficient, not a normative default", WidthM = 2,
        WaterCases = new() { new() { LeftLevelM = 3, CurrentVelocityMs = 2 } } };
    [TestMethod]
    public void UniformCurrentAndReverseFlow()
    {
        var x = Pier(); var r = First(x); Near(14.4, r.NetForceKn); Near(21.6, r.MomentKnM); Near(1.5, r.ApplicationCoordinateM!.Value);
        x.WaterCases[0].CurrentVelocityMs = -2; var reverse = First(x); Near(-14.4, reverse.NetForceKn); Near(-21.6, reverse.MomentKnM);
        x.WaterCases[0].LeftLevelM = -1; Near(0, First(x).NetForceKn);
    }
    [TestMethod]
    public void LinearVelocityUsesQuadraticPressureNotLinearPressureInterpolation()
    {
        var x = Pier(); x.UseVelocityProfile = true;
        x.VelocityProfile = new() { new() { ElevationM = 0, VelocityMs = 0 }, new() { ElevationM = 5, VelocityMs = 5 } };
        var r = First(x); Near(10.8, r.NetForceKn); Near(24.3, r.MomentKnM); Near(2.25, r.ApplicationCoordinateM!.Value);
        // ∫0³ 1.2 z² dz = 10.8; ∫0³ 1.2 z³ dz = 24.3, in kN and kN m.
        x.WaterCases[0].ProfileMultiplier = 2; Near(43.2, First(x).NetForceKn);
    }
    [TestMethod]
    public void ReversingVelocityProfileRetainsPureCouple()
    {
        var x = Pier(); x.TopElevationM = 2; x.WaterCases[0].LeftLevelM = 2; x.UseVelocityProfile = true;
        x.VelocityProfile = new() { new() { ElevationM = 0, VelocityMs = -1 }, new() { ElevationM = 2, VelocityMs = 1 } };
        var r = First(x); Near(.4, r.PositiveForceKn); Near(.4, r.OpposingForceKn); Near(0, r.NetForceKn); Near(.6, r.MomentKnM); Assert.IsNull(r.ApplicationCoordinateM);
    }
    [TestMethod]
    public void ScenariosRemainAlternativeAndSerializationRoundTrips()
    {
        var x = Wall(); x.WaterCases.Add(new() { Name = "Piena", LeftLevelM = 5 });
        string json = System.Text.Json.JsonSerializer.Serialize(x);
        var copy = System.Text.Json.JsonSerializer.Deserialize<HydraulicActionsInput>(json)!;
        var r = HydraulicActions.Calculate(copy); Assert.AreEqual(2, r.Cases.Count); Near(90, r.Cases[0].NetForceKn); Near(250, r.Cases[1].NetForceKn);
        Assert.AreEqual(2, r.Summary.Cases.Count); Assert.AreEqual(json, System.Text.Json.JsonSerializer.Serialize(x));
    }
    [TestMethod]
    public void InvalidGeometrySourcesProfilesAndScenariosAreRejected()
    {
        var x = Wall(); x.UnitWeightKnM3 = double.NaN; Reject(x);
        x = Wall(); x.TopElevationM = 0; Reject(x);
        x = Wall(); x.WidthM = -1; Reject(x);
        x = Wall(); x.WaterCases.Add(new() { Name = "Ordinario" }); Reject(x);
        x = Wall(); x.WaterCases = null!; Reject(x);
        x = Pier(); x.DragSource = ""; Reject(x);
        x = Pier(); x.UseVelocityProfile = true; x.VelocityProfile[1].ElevationM = 4; Reject(x);
        x = Wall(); x.Element = HydraulicElement.HorizontalSlab; x.Uplift = UpliftDistribution.AssignedPressureProfile; Reject(x);
        x.PressureSource = "Test"; x.PressureProfile[1].PositionM = 9; Reject(x);
        x.PressureProfile[1].PositionM = 10; x.PressureProfile[0].PressureKpa = -1; Reject(x);
        static void Reject(HydraulicActionsInput input) => Assert.ThrowsException<ArgumentException>(() => HydraulicActions.Calculate(input));
    }
}
