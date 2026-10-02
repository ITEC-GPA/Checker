using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Data.Sections;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Five worked examples of the piles and micropiles (report F), each checked against an independent calculation.</summary>
[TestClass]
public class PileExamplesTests
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly StandardNTC2018Geotechnics Ntc = new();
    private static SoilProfile Profile(double? waterDepth, params (Soil Soil, double Thickness)[] layers)
    {
        double top = 0; var list = new List<SoilLayer>();
        foreach (var (soil, t) in layers) { list.Add(new SoilLayer(soil, -top * M, -(top + t) * M)); top += t; }
        return new SoilProfile("Example", list, "example", waterDepth.HasValue ? -waterDepth * M : null);
    }

    /// <summary>
    /// 1. Nq for a pile tip at z = 12.3 m with φ' = 35°: D = 0.6 m (L/D = 20.5, between the curves 20 and 50, geometric weight t = ln(20.5/20)/ln(2.5))
    /// and D = 1.0 m (Nq*, L/D = 12.3, arithmetic between 4 and 32): the second is the value 28.7 shown by the ANTHEA sheet of the same case.
    /// </summary>
    [TestMethod]
    public void Example1_NqOfMediumAndLargePiles()
    {
        var medium = BearingCapacityFactors.Nq(35 * Deg, 12.3 / .6, false); var large = BearingCapacityFactors.Nq(35 * Deg, 12.3, true);
        Close(Math.Log(20.5 / 20) / Math.Log(2.5), medium.Weight, "t", 1e-12);
        Close(Math.Exp(Math.Log(medium.Value1) + medium.Weight * Math.Log(medium.Value2 / medium.Value1)), medium.Nq, "geometric", 1e-12);
        Close(10 * Math.Pow(10, (35 - 25.8) / (37.8 - 25.8)), medium.Value1, "L/D = 20: 10^(1 + (35 − 25.8)/(37.8 − 25.8))", 1e-12);
        Close(28.7, large.Nq, "Nq* of the ANTHEA figure", 2e-3); Close(58.06, medium.Nq, "Nq D 0.6 m", 1e-4);
        TestContext.WriteLine($"Example 1: Nq(D 0.6 m) = {medium.Nq:F2} (curves {medium.Value1:F2}/{medium.Value2:F2}, t {medium.Weight:F4}); Nq*(D 1.0 m) = {large.Nq:F2}");
    }

    /// <summary>
    /// 2. Micropile Ø250 mm, tube CHS 139.7 × 8 of the ModelData catalogue (EN 10210-2), L = 10 m, IGU at 2 MPa, medium sand 3 m (α 1.4) on sandy
    /// gravel (α 1.6): s = 0.20 and 0.20 MPa from the SG2 chart, shaft π α D L s; design (shaft/1.15)/1.70 with one vertical.
    /// </summary>
    [TestMethod]
    public void Example2_MicropileBustamanteDoix()
    {
        var tube = (SectionCHS)SectionMappings.CreateSection("CHS 139.7 x 8");
        var survey = new MicropileSurvey(Profile(null, (new Soil("Sabbia media", 18 * KN3, 20 * KN3, 32 * Deg, 0, "example"), 3), (new Soil("Ghiaia sabbiosa", 19 * KN3, 21 * KN3, 36 * Deg, 0, "example"), 20)),
            new[] { (BustamanteDoixSoil.MediumSand, 1.4, true), (BustamanteDoixSoil.SandyGravel, 1.6, true) });
        var pile = new Micropile(250, 10 * M, 0, MicropileInjection.IGU, 2, 0, null, tube, TubeSteel, compressionAction: 450e3);
        var r = AxialPileCapacity.Calculate(pile, new[] { survey }, PileResistanceFactors.FromStandard(Ntc, 1), PileGroupEfficiency.None());
        var tip = r.Depths.Last().Surveys[0];
        double shaft = Math.PI * 250 * (1.4 * 3 * M * .20 + 1.6 * 7 * M * .20);
        Close(shaft, tip.Shaft, "Σ π α D L s", 1e-12); Close(shaft / 1.15 / 1.7, r.Curves[(AxialCondition.Drained, true)].Design.Last().Value, "design", 1e-12);
        double action = r.CompressionActions.Last().Value;
        Close(2419.0e3, tip.Shaft, "shaft 2419 kN", 1e-4); Close(1237.4e3, r.Curves[(AxialCondition.Drained, true)].Design.Last().Value, "design 1237 kN", 1e-4);
        Assert.IsTrue(action < r.Curves[(AxialCondition.Drained, true)].Design.Last().Value, "verified");
        TestContext.WriteLine($"Example 2: shaft {tip.Shaft / 1e3:F1} kN, design {r.Curves[(AxialCondition.Drained, true)].Design.Last().Value / 1e3:F1} kN, NEd + 1.3 W = {action / 1e3:F1} kN, weight {r.Depths.Last().Weight / 1e3:F2} kN");
    }

    /// <summary>
    /// 3. Free-head long pile in sand (Broms): D = 0.8 m, L = 12 m, φ' = 33°, γ = 19 kN/m³, My = 600 kNm. My = H · (2/3) √(H/(1.5 Kp γ D)) gives H;
    /// Rd = H/ξ/γR with ξ = 1.70 and γR = 1.3.
    /// </summary>
    [TestMethod]
    public void Example3_BromsLongPileInSand()
    {
        var profile = Profile(null, (new Soil("Sand", 19 * KN3, 20 * KN3, 33 * Deg, 0, "example"), 30));
        var r = LateralPileCapacity.Calculate(new LateralPile(800, 12 * M, 0, false, 600e6, "RC section, example", LateralPileMethod.Broms, 150e3, 100),
            new[] { new LateralPileSurvey(profile, new[] { SoilBehaviour.Granular }) }, LateralPileFactors.FromStandard(Ntc, 1), LateralGroupEfficiency.Manual(1));
        double kp = LateralPileCapacity.PassivePressureCoefficient(33 * Deg), h = r.Capacity;
        Close(600e6, h * 2.0 / 3 * Math.Sqrt(h / (1.5 * kp * 19 * KN3 * 800)), "Broms", 1e-9);
        Assert.AreEqual(LateralMechanism.Long, r.Mechanism); Close(h / 1.7 / 1.3, r.DesignResistance, "Rd", 1e-12);
        Close(397.16e3, h, "H 397.16 kN", 1e-4); Close(179.7e3, r.DesignResistance, "design 179.7 kN", 1e-4); Assert.IsTrue(r.Satisfied);
        TestContext.WriteLine($"Example 3: Kp {kp:F3}, H {h / 1e3:F1} kN at zero shear {r.Surveys[0].ZeroShearDepth / M:F2} m, Rd {r.DesignResistance / 1e3:F1} kN, HEd/Rd {r.Utilization:F3}");
    }

    /// <summary>
    /// 4. Restrained head in clay (2 m, cu = 40 kPa) over sand (φ' = 32°), stratified model with distributed reactions, D = 0.8 m, L = 10 m,
    /// My = 1500 kNm: two verticals (the second with sand φ' = 34°), ξ3 = 1.65, ξ4 = 1.55, Reese and Van Impe efficiency.
    /// </summary>
    [TestMethod]
    public void Example4_StratifiedMixedProfile()
    {
        var clay = new Soil("Clay", 18 * KN3, 19 * KN3, 0, 0, "example", undrainedShearStrength: 40 * KPa);
        LateralPileSurvey S(double phi) => new(Profile(3, (clay, 2), (new Soil("Sand", 19 * KN3, 20.5 * KN3, phi * Deg, 0, "example"), 30)), new[] { SoilBehaviour.Cohesive, SoilBehaviour.Granular });
        var eta = LateralGroupEfficiency.ReeseVanImpeEfficiency(800, 3 * M, 2.4 * M, 2 * M, 4.5 * M);
        var r = LateralPileCapacity.Calculate(new LateralPile(800, 10 * M, 0, true, 1500e6, "RC section, example", LateralPileMethod.Stratified, 300e3, 100),
            new[] { S(32), S(34) }, LateralPileFactors.FromStandard(Ntc, 2), eta);
        Assert.AreEqual(1, r.GoverningSurvey); Assert.AreEqual("Diagramma stratificato · terreno multistrato", r.ModelName); Assert.IsTrue(r.Experimental);
        Close(Math.Min(r.MeanCapacity / 1.65, r.Capacity / 1.55) / 1.3 * eta.Eta, r.DesignResistance, "Rd", 1e-12);
        foreach (var s in r.Surveys) Assert.IsTrue(s.DistributedClosure && s.Diagram.All(p => Math.Abs(p.Moment) <= 1500e6 * (1 + 1e-5)));
        Close(1075.5e3, r.Surveys[0].Capacity, "H 1075.5 kN", 1e-4); Close(1091.6e3, r.Surveys[1].Capacity, "H 1091.6 kN", 1e-4); Close(226.0e3, r.DesignResistance, "design 226 kN", 1e-4);
        Assert.IsFalse(r.Satisfied, "HEd = 300 kN above the design resistance: the close spacing of the group governs (η = 0.447)");
        TestContext.WriteLine($"Example 4: H {r.Surveys[0].Capacity / 1e3:F1} / {r.Surveys[1].Capacity / 1e3:F1} kN ({r.Mechanism}), η {eta.Eta:F3}, Rd {r.DesignResistance / 1e3:F1} kN, HEd/Rd {r.Utilization:F3}");
    }

    /// <summary>
    /// 5. Bored pile D = 0.8 m, L = 15 m: silty clay 4 m (cu 50 kPa, c' 5 kPa, φ' 25°) over dense sand (φ' 34°), water at 2 m, buoyancy; NEd = 2000 kN,
    /// NEd,t = 400 kN. Shaft τ = c' + K μ σ'v (clay drained) or α cu (clay below the water table), base A σ'v Nq in the sand.
    /// </summary>
    [TestMethod]
    public void Example5_AxialBoredPile()
    {
        var profile = Profile(2, (new Soil("Silty clay", 19 * KN3, 20 * KN3, 25 * Deg, 5 * KPa, "example", undrainedShearStrength: 50 * KPa), 4), (new Soil("Sand", 19 * KN3, 20.5 * KN3, 34 * Deg, 0, "example"), 30));
        var survey = new AxialPileSurvey(profile, new[] { new AxialPileLayer(SoilBehaviour.Cohesive, SoilDensity.Loose, 9), new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        var r = AxialPileCapacity.Calculate(new AxialPile(PileInstallation.Bored, 800, 15 * M, buoyancy: true, compressionAction: 2000e3, tensionAction: 400e3), new[] { survey },
            PileResistanceFactors.FromStandard(Ntc, 1), PileGroupEfficiency.None());
        var tip = r.Depths.Last(); var s = tip.Surveys[0];
        double sigma = 19 * KN3 * 2 * M + (20 * KN3 - SoilUnits.WaterUnitWeight) * 2 * M + (20.5 * KN3 - SoilUnits.WaterUnitWeight) * 11 * M;
        Close(sigma, s.TipEffectiveStress, "σ'v at the tip", 1e-12);
        Close(Math.PI * 800 * 800 / 4 * sigma * BearingCapacityFactors.Nq(34 * Deg, 15 * M / 800, false).Nq, s.DrainedBase, "base", 1e-12);
        Assert.AreEqual(s.DrainedBase, s.UndrainedBase, "sand at the tip: the undrained base is the drained one");
        Close(AxialPileCapacity.Alpha(PileInstallation.Bored, 50 * KPa), s.Segments[0].Alpha!.Value, "α of the clay", 1e-15);
        double compression = r.Curves[(AxialCondition.Drained, true)].Design.Last().Value, undrained = r.Curves[(AxialCondition.Undrained, true)].Design.Last().Value;
        double tension = r.Curves[(AxialCondition.Drained, false)].Design.Last().Value;
        Assert.IsTrue(r.CompressionActions.Last().Value < Math.Min(compression, undrained) && r.TensionActions.Last().Value < tension, "verified");
        Close(4335e3, s.DrainedBase, "base 4335 kN", 2e-4); Close(2402e3, compression, "drained compression 2402 kN", 3e-4); Close(2425e3, undrained, "undrained compression 2425 kN", 3e-4);
        Close(472e3, tension, "tension 472 kN", 1e-3);
        TestContext.WriteLine($"Example 5: σ'v {s.TipEffectiveStress / KPa:F1} kPa, Nq {s.Nq!.Nq:F2}, base {s.DrainedBase / 1e3:F0} kN, shaft {s.DrainedShaft / 1e3:F0}/{s.UndrainedShaft / 1e3:F0} kN, Rd,c {compression / 1e3:F0}/{undrained / 1e3:F0} kN, Rd,t {tension / 1e3:F0} kN, NEd + 1.3W {r.CompressionActions.Last().Value / 1e3:F0} kN, weight {tip.Weight / 1e3:F1} kN");
    }
}
