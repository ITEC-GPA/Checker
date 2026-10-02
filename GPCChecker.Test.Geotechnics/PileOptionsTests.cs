using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Data.Sections;
using GPC.Model.Data.Steel;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.PilesFixture;

namespace GeotechnicsTests;

/// <summary>
/// Options added after the migration of the piles: friction angle of Nq reduced by Kishida (Viggiani, Fondazioni, §13.1.2 p. 376), weight of the
/// micropile tube from the density of the Model material (t/mm³) and the tubes of ANTHEA in the ModelData catalogs (Celsius range of Tata Steel).
/// </summary>
[TestClass]
public class PileOptionsTests
{
    private static Soil Sand(double phi) => new("Sand", 18 * KN3, 20 * KN3, phi * Deg, 0, "test");
    private static SoilProfile Profile(double? waterDepth, params (Soil Soil, double Thickness)[] layers)
    {
        double top = 0; var list = new List<SoilLayer>();
        foreach (var (soil, t) in layers) { list.Add(new SoilLayer(soil, -top * M, -(top + t) * M)); top += t; }
        return new SoilProfile("P", list, "test", waterDepth.HasValue ? -waterDepth * M : null);
    }
    private static readonly StandardNTC2018Geotechnics Ntc = new();
    private static AxialCapacityResult<AxialSurveyResistance> Axial(AxialPile pile, params AxialPileSurvey[] surveys)
        => AxialPileCapacity.Calculate(pile, surveys, PileResistanceFactors.FromStandard(Ntc, surveys.Length), PileGroupEfficiency.None());

    // ---------------------------------------------------------------- φ of Nq reduced by Kishida

    [TestMethod]
    public void KishidaAnglesAreTheOnesOfViggiani()
    {
        // driven φ' = (φ'1 + 40°)/2: larger below 40°, equal at 40°, smaller above; bored φ' = φ'1 − 3°, not below zero
        Assert.AreEqual(35, BearingCapacityFactors.BaseFrictionAngleDegrees(30, NqFrictionAngle.KishidaDriven));
        Assert.AreEqual(40, BearingCapacityFactors.BaseFrictionAngleDegrees(40, NqFrictionAngle.KishidaDriven));
        Assert.AreEqual(42, BearingCapacityFactors.BaseFrictionAngleDegrees(44, NqFrictionAngle.KishidaDriven));
        Assert.AreEqual(20, BearingCapacityFactors.BaseFrictionAngleDegrees(0, NqFrictionAngle.KishidaDriven));
        Assert.AreEqual(32, BearingCapacityFactors.BaseFrictionAngleDegrees(35, NqFrictionAngle.KishidaBored));
        Assert.AreEqual(0, BearingCapacityFactors.BaseFrictionAngleDegrees(3, NqFrictionAngle.KishidaBored));
        Assert.AreEqual(0, BearingCapacityFactors.BaseFrictionAngleDegrees(2, NqFrictionAngle.KishidaBored));
        Assert.AreEqual(33.5, BearingCapacityFactors.BaseFrictionAngleDegrees(33.5, NqFrictionAngle.Layer));
        // the layer rule keeps any value (the curves clip it, as ANTHEA); the rules of Kishida need 0 ≤ φ'1 < 90°
        Assert.AreEqual(-1, BearingCapacityFactors.BaseFrictionAngleDegrees(-1, NqFrictionAngle.Layer));
        foreach (double phi in new[] { -1, 90, 95, double.NaN, double.PositiveInfinity })
            foreach (var rule in new[] { NqFrictionAngle.KishidaDriven, NqFrictionAngle.KishidaBored })
                Assert.ThrowsException<ArgumentException>(() => BearingCapacityFactors.BaseFrictionAngleDegrees(phi, rule), phi + " " + rule);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BearingCapacityFactors.BaseFrictionAngleDegrees(30, (NqFrictionAngle)7));
    }

    [TestMethod]
    public void KishidaRuleFollowsTheInstallation()
    {
        Assert.AreEqual(NqFrictionAngle.KishidaBored, BearingCapacityFactors.Kishida(PileInstallation.Bored));
        Assert.AreEqual(NqFrictionAngle.KishidaBored, BearingCapacityFactors.Kishida(PileInstallation.ContinuousFlightAuger), "CFA: bored piles in Viggiani §12.3.4");
        foreach (var driven in new[] { PileInstallation.DrivenSteelSection, PileInstallation.DrivenClosedSteelTube, PileInstallation.DrivenPrecastConcrete, PileInstallation.DrivenCastInPlace })
            Assert.AreEqual(NqFrictionAngle.KishidaDriven, BearingCapacityFactors.Kishida(driven), driven.ToString());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BearingCapacityFactors.Kishida((PileInstallation)99));
    }

    [TestMethod]
    public void NqWithTheReducedAngleIsTheCurveAtThatAngle()
    {
        foreach (bool large in new[] { false, true })
            foreach (double ratio in new[] { 4.0, 7.5, 12.3, 20, 31, 60 })
                foreach (double phi in new[] { 28.0, 31.7, 35, 38.2 })
                {
                    var bored = BearingCapacityFactors.Nq(phi * Deg, ratio, large, NqFrictionAngle.KishidaBored);
                    var reference = BearingCapacityFactors.Nq((phi - 3) * Deg, ratio, large);
                    string id = $"{phi} {ratio} {large}";
                    Assert.AreEqual(NqFrictionAngle.KishidaBored, bored.Rule, id);
                    Close(phi, bored.SoilFrictionAngleDegrees, id + " φ'1", 1e-12);
                    Close(phi - 3, bored.FrictionAngleDegrees, id + " φ'", 1e-12);
                    Close(reference.Nq, bored.Nq, id + " Nq", 1e-12);
                    Assert.AreEqual(reference.FrictionAngleClipped, bored.FrictionAngleClipped, id);
                    var driven = BearingCapacityFactors.Nq(phi * Deg, ratio, large, NqFrictionAngle.KishidaDriven);
                    Close(BearingCapacityFactors.Nq((phi + 40) / 2 * Deg, ratio, large).Nq, driven.Nq, id + " driven", 1e-12);
                    // below 40° the driven rule raises Nq, the bored one lowers it
                    var layer = BearingCapacityFactors.Nq(phi * Deg, ratio, large);
                    Assert.AreEqual(NqFrictionAngle.Layer, layer.Rule);
                    Assert.AreEqual(layer.SoilFrictionAngleDegrees, layer.FrictionAngleDegrees);
                    Assert.IsTrue(driven.Nq >= layer.Nq && bored.Nq <= layer.Nq, id);
                }
        // the reduction can move φ' outside the visible part of a curve: the border is used and flagged (L/D = 5 starts at 23.4°)
        var low = BearingCapacityFactors.Nq(25 * Deg, 5, false, NqFrictionAngle.KishidaBored);
        Assert.IsTrue(low.FrictionAngleClipped);
        Assert.AreEqual(23.4, low.FrictionAngle1);
        Assert.IsFalse(BearingCapacityFactors.Nq(25 * Deg, 5, false).FrictionAngleClipped);
        // check of the screen of ANTHEA (φ = 35°, L/D = 12.3, D > 0.8 m: Nq* = 28.70) and its value with the bored rule (φ' = 32°)
        Close(28.70, BearingCapacityFactors.Nq(35 * Deg, 12.3, true).Nq, "Nq* 35°", 2e-4);
        Close(BearingCapacityFactors.Curve(32, 0, true) + Math.Log(12.3 / 4) / Math.Log(8) * (BearingCapacityFactors.Curve(32, 1, true) - BearingCapacityFactors.Curve(32, 0, true)),
            BearingCapacityFactors.Nq(35 * Deg, 12.3, true, NqFrictionAngle.KishidaBored).Nq, "Nq* bored", 1e-12);
        Assert.ThrowsException<ArgumentException>(() => BearingCapacityFactors.Nq(95 * Deg, 10, false, NqFrictionAngle.KishidaBored));
        Assert.ThrowsException<ArgumentException>(() => BearingCapacityFactors.Nq(30 * Deg, 0, false, NqFrictionAngle.KishidaBored));
    }

    [TestMethod]
    public void AxialPileReducesOnlyTheBaseAngle()
    {
        var survey = new AxialPileSurvey(Profile(4, (Sand(28), 6), (Sand(34), 20)),
            new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Loose), new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        var layer = Axial(new AxialPile(PileInstallation.Bored, 600, 15 * M), survey);
        var bored = Axial(new AxialPile(PileInstallation.Bored, 600, 15 * M, baseFrictionAngle: NqFrictionAngle.KishidaBored), survey);
        Assert.AreEqual(NqFrictionAngle.Layer, new AxialPile(PileInstallation.Bored, 600, 15 * M).BaseFrictionAngle, "default as ANTHEA");
        Assert.AreEqual(layer.Depths.Count, bored.Depths.Count);
        for (int i = 0; i < layer.Depths.Count; i++)
        {
            var a = layer.Depths[i].Surveys[0]; var b = bored.Depths[i].Surveys[0]; double z = layer.Depths[i].Depth;
            Assert.AreEqual(a.DrainedShaft, b.DrainedShaft, "shaft with the φ' of the layer at " + z);
            Assert.AreEqual(a.TipEffectiveStress, b.TipEffectiveStress);
            Assert.AreEqual(a.TipFrictionAngle, b.TipFrictionAngle, "the tip keeps the φ' of the layer");
            if (z == 0) { Assert.IsNull(b.Nq); continue; }
            double nq = BearingCapacityFactors.Nq(a.TipFrictionAngle - 3 * Deg, z / 600, false).Nq;
            Close(Math.PI * 600 * 600 / 4 * a.TipEffectiveStress * nq, b.DrainedBase, "base at " + z, 1e-12);
            Assert.IsTrue(b.DrainedBase < a.DrainedBase, "bored rule lowers the base at " + z);
            Close(a.TipFrictionAngle / Deg - 3, b.Nq!.FrictionAngleDegrees, "φ' at " + z, 1e-9);
        }
        Assert.IsTrue(layer.Warnings.Any(w => w.EndsWith(" φ non ridotto.")));
        Assert.IsTrue(bored.Warnings.Any(w => w.Contains("Kishida (1967) per pali trivellati: φ' = φ'1 − 3°")));
        Assert.IsFalse(bored.Warnings.Any(w => w.Contains("non corrisponde")));

        // driven rule on a driven pile: no warning of mismatch; on a bored pile: warning, the chosen rule is applied anyway
        var driven = Axial(new AxialPile(PileInstallation.DrivenPrecastConcrete, 400, 12 * M, baseFrictionAngle: NqFrictionAngle.KishidaDriven), survey);
        Assert.IsTrue(driven.Warnings.Any(w => w.Contains("per pali battuti: φ' = (φ'1 + 40°)/2")));
        Assert.IsFalse(driven.Warnings.Any(w => w.Contains("non corrisponde")));
        var mismatch = Axial(new AxialPile(PileInstallation.Bored, 600, 15 * M, baseFrictionAngle: NqFrictionAngle.KishidaDriven), survey);
        Assert.IsTrue(mismatch.Warnings.Any(w => w.Contains("regola di Kishida scelta (pali battuti) non corrisponde")));
        var tip = mismatch.Depths.Last().Surveys[0];
        Close(BearingCapacityFactors.Nq((34 + 40) / 2.0 * Deg, 15 * M / 600, false).Nq, tip.Nq!.Nq, "driven rule applied", 1e-12);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new AxialPile(PileInstallation.Bored, 600, 15 * M, baseFrictionAngle: (NqFrictionAngle)5));
    }

    [TestMethod]
    public void UndrainedBaseOfCohesiveTipIsNotAffected()
    {
        var clay = new Soil("Clay", 19 * KN3, 20 * KN3, 24 * Deg, 5 * KPa, "test", undrainedShearStrength: 80 * KPa);
        var survey = new AxialPileSurvey(Profile(2, (clay, 30)), new[] { new AxialPileLayer(SoilBehaviour.Cohesive, SoilDensity.Dense, 9) });
        var a = Axial(new AxialPile(PileInstallation.Bored, 800, 20 * M), survey).Depths.Last().Surveys[0];
        var b = Axial(new AxialPile(PileInstallation.Bored, 800, 20 * M, baseFrictionAngle: NqFrictionAngle.KishidaBored), survey).Depths.Last().Surveys[0];
        Assert.AreEqual(a.UndrainedBase, b.UndrainedBase, "A (Nc cu + σv) below the water table");
        Assert.IsTrue(b.DrainedBase < a.DrainedBase);
    }

    // ---------------------------------------------------------------- weight of the tube from the Model material

    [TestMethod]
    public void TubeWeightUsesTheDensityOfTheModelMaterial()
    {
        var tube = (SectionCHS)SectionMappings.CreateSection("CHS 139.7 x 8");
        var w = MicropileTube.Weight(tube, SteelMaterialEN1993Data.S355, 250, 25 * KN3);
        // ρ 7.85e-9 t/mm³ · g 9810 mm/s² = 7850 kg/m³ · 9.81 m/s² of the legacy calculation
        Close(7850 * 9.81 * 1e-9 * w.SteelArea, w.Steel, "steel", 1e-14);
        Close(SteelMaterialEN1993Data.S355.GetUnitWeight(SoilUnits.Gravity) * w.SteelArea, w.Steel, "material", 1e-15);
        Close(w.Steel, MicropileTube.Weight(tube, TubeSteel, 250, 25 * KN3).Steel, "any Model steel", 1e-15);
        // another density scales only the steel
        var stainless = new SteelMaterial("1.4301", 200000, 210, 520, density: 8e-9);
        var ws = MicropileTube.Weight(tube, stainless, 250, 25 * KN3);
        Close(w.Steel * 8 / 7.85, ws.Steel, "8000 kg/m³", 1e-14);
        Assert.AreEqual(w.Grout, ws.Grout);
        Assert.ThrowsException<ArgumentNullException>(() => MicropileTube.Weight(tube, null!, 250, 25 * KN3));
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.Weight(tube, new SteelMaterial("no mass", 210000, 355, 510, density: 0), 250, 25 * KN3));
        Assert.ThrowsException<ArgumentNullException>(() => new Micropile(250, 10 * M, 0, MicropileInjection.IGU, 2, 0, null, tube, null!));
        var pile = new Micropile(250, 10 * M, 0, MicropileInjection.IGU, 2, 0, null, tube, SteelMaterialEN1993Data.S355);
        Assert.AreEqual(Material.SteelDensity, pile.TubeMaterial.Density);
        // the weight of the micropile along the axis uses the material of the tube
        var survey = new MicropileSurvey(Profile(null, (Sand(32), 20)), new[] { (BustamanteDoixSoil.MediumSand, 1.4, true) });
        var light = new Micropile(250, 10 * M, 0, MicropileInjection.IGU, 2, 0, null, tube, new SteelMaterial("light", 210000, 355, 510, density: 3.925e-9));
        var f = PileResistanceFactors.FromStandard(Ntc, 1);
        double heavyWeight = AxialPileCapacity.Calculate(pile, new[] { survey }, f, PileGroupEfficiency.None()).Depths.Last().Weight;
        double lightWeight = AxialPileCapacity.Calculate(light, new[] { survey }, f, PileGroupEfficiency.None()).Depths.Last().Weight;
        Close((w.Steel + w.Grout) * 10 * M, heavyWeight, "weight S355", 1e-12);
        Close((w.Steel / 2 + w.Grout) * 10 * M, lightWeight, "half density", 1e-12);
    }

    // ---------------------------------------------------------------- tubes of ANTHEA in ModelData

    [TestMethod]
    public void EveryLegacyTubeIsInTheModelDataCatalogs()
    {
        var profiles = new Dictionary<string, (double D, double T, double Mass)>();
        foreach (var line in Lines("piles-chs.jsonl"))
            if (Str(line["kind"]) == "weight" && line["D"] != null && !IsError(line["result"]))
                profiles[Str(line["profile"])] = (Req(line["D"], "D"), Req(line["t"], "t"), Req(line["result"]!["massa_acciaio"], "mass"));
        Assert.AreEqual(64, profiles.Count, "catalog of ANTHEA (Tata Steel Celsius)");
        int standard = 0, celsius = 0;
        foreach (var (name, p) in profiles)
        {
            CatalogProfile profile = SectionCatalogs.Find(name)!;
            Assert.IsNotNull(profile, name);
            Assert.AreEqual(SectionFamily.CircularHollow, profile.Family, name);
            // hot finished tubes: the tables of EN 10210-2 first, then the Celsius range
            if (profile.Catalog == SectionCatalogs.EN10210CircularHollow) standard++;
            else if (profile.Catalog == SectionCatalogs.EN10210CircularHollowCelsius) celsius++;
            else Assert.Fail(name + " found in " + profile.Catalog.Id);
            var chs = (SectionCHS)SectionMappings.CreateSection(profile);
            Assert.AreEqual(p.D, chs.Diameter, name);
            Assert.AreEqual(p.T, chs.Thickness, name);
            // nominal mass of ANTHEA (7850 kg/m³ on the exact area) against the published one (3 significant figures)
            Close(profile["G"], p.Mass, name + " mass", 5e-3);
            var w = MicropileTube.Weight(chs, TubeSteel, 300, 25 * KN3);
            Close(p.Mass * 9.81e-3, w.Steel, name + " weight", 1e-12);
        }
        // 48 in the tables of EN 10210-2; 16 in the Celsius range: the 15 missing before and CHS 76.1 x 6.3, cold formed only in EN 10219-2
        Assert.AreEqual(48, standard);
        Assert.AreEqual(16, celsius);
        Assert.IsNotNull(SectionCatalogs.EN10219CircularHollow.Find("CHS 76.1 x 6.3"));
    }

    // ---------------------------------------------------------------- Model of the transverse capacity before the calculation

    private static Soil Clay(double cu) => new("Clay", 18 * KN3, 20 * KN3, 0, 0, "test", undrainedShearStrength: cu * KPa);
    private static LateralPileSurvey Lateral(double? water, params (Soil Soil, double Thickness, SoilBehaviour Kind)[] layers)
        => new(Profile(water, layers.Select(l => (l.Soil, l.Thickness)).ToArray()), layers.Select(l => l.Kind));

    [TestMethod]
    public void TheModelBeforeTheCalculationIsTheOneOfTheCapacity()
    {
        var sand = (Sand(30), 20.0, SoilBehaviour.Granular); var dense = (Sand(36), 20.0, SoilBehaviour.Granular); var clay = (Clay(50), 20.0, SoilBehaviour.Cohesive);
        var cases = new (LateralPileSurvey[] Surveys, LateralPileMethod Method, string Name)[]
        {
            (new[] { Lateral(null, sand) }, LateralPileMethod.Broms, "Omogeneo"),
            (new[] { Lateral(null, (Sand(30), 4.0, SoilBehaviour.Granular), (Sand(30), 16.0, SoilBehaviour.Granular)) }, LateralPileMethod.Broms, "Omogeneo"),
            (new[] { Lateral(null, (Sand(30), 4.0, SoilBehaviour.Granular), (Sand(36), 16.0, SoilBehaviour.Granular)) }, LateralPileMethod.Broms, "Multistrato sperimentale"),
            // a water table inside the pile changes the effective weight: not uniform
            (new[] { Lateral(3, sand) }, LateralPileMethod.Broms, "Multistrato sperimentale"),
            // one uniform vertical and one layered: the pile is layered
            (new[] { Lateral(null, sand), Lateral(null, (Sand(30), 4.0, SoilBehaviour.Granular), (Sand(36), 16.0, SoilBehaviour.Granular)) }, LateralPileMethod.Broms, "Multistrato sperimentale"),
            (new[] { Lateral(null, clay) }, LateralPileMethod.Broms, "Omogeneo"),
            (new[] { Lateral(null, dense) }, LateralPileMethod.Stratified, "Diagramma stratificato · terreno omogeneo"),
            (new[] { Lateral(null, (Clay(50), 4.0, SoilBehaviour.Cohesive), (Sand(30), 16.0, SoilBehaviour.Granular)) }, LateralPileMethod.Stratified, "Diagramma stratificato · terreno multistrato")
        };
        foreach (var (surveys, method, name) in cases)
        {
            Assert.AreEqual(name, LateralPileCapacity.ModelName(surveys, 10000, 600, method), name);
            var pile = new LateralPile(600, 10000, 0, false, 500e6, "test", method, 100e3);
            Assert.AreEqual(name, LateralPileCapacity.Calculate(pile, surveys, LateralPileFactors.FromStandard(Ntc, surveys.Length), LateralGroupEfficiency.Manual(1)).ModelName, name + " capacity");
        }
    }

    [TestMethod]
    public void TheModelBeforeTheCalculationChecksTheData()
    {
        var mixed = new[] { Lateral(null, (Clay(50), 4.0, SoilBehaviour.Cohesive), (Sand(30), 16.0, SoilBehaviour.Granular)) };
        Throws(() => LateralPileCapacity.ModelName(mixed, 10000, 600, LateralPileMethod.Broms), "mixed sequence with Broms");
        Throws(() => LateralPileCapacity.ModelName(new[] { Lateral(null, (Sand(30), 5.0, SoilBehaviour.Granular)) }, 10000, 600, LateralPileMethod.Broms), "layers shorter than the pile");
        Throws(() => LateralPileCapacity.ModelName(new LateralPileSurvey[0], 10000, 600, LateralPileMethod.Broms), "no vertical");
        Throws(() => LateralPileCapacity.ModelName(mixed, 0, 600, LateralPileMethod.Stratified), "zero length");
        Throws(() => LateralPileCapacity.ModelName(mixed, 10000, double.NaN, LateralPileMethod.Stratified), "NaN diameter");
        Throws(() => LateralPileCapacity.ModelName(mixed, 10000, 600, (LateralPileMethod)7), "unknown method");
        Throws(() => LateralPileCapacity.ModelName(new[] { Lateral(null, (Clay(50), 20.0, SoilBehaviour.Cohesive)) }, 800, 600, LateralPileMethod.Broms), "clay with L ≤ 1.5 D");
        Assert.ThrowsException<ArgumentNullException>(() => LateralPileCapacity.ModelName(null!, 10000, 600, LateralPileMethod.Broms));
    }

    private static void Throws(Action action, string what)
    {
        try { action(); } catch (ArgumentException) { return; }
        Assert.Fail("Accepted: " + what);
    }
}
