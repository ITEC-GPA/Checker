using System.Globalization;
using System.Text.Json;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class UndrainedBasalHeaveTests
{
    private static StandardNTC2018Geotechnics Ntc() => new();
    private static UndrainedBasalHeaveInput Input() => new()
    {
        CaseId = "caso", StageId = "scavo", ExcavationDepth = 5000, ExcavationWidth = 10000,
        TotalUnitWeight = 18 * SoilUnits.KiloNewtonPerCubicMetre, UndrainedStrength = 25 * SoilUnits.KiloPascal,
        BearingCapacityFactor = 6, BearingCapacitySource = "Nc assegnato per il confronto aritmetico, non interpolato",
        VariableSurcharge = 10 * SoilUnits.KiloPascal, IncludeExternalBlockShear = true,
        HomogeneousUndrainedMechanismConfirmed = true, LongExcavationConfirmed = true, CharacteristicValuesConfirmed = true,
        ApplicabilitySource = "Argilla omogenea nel meccanismo, sezione piana", ActionSource = "Azioni caratteristiche del caso"
    };

    [TestMethod]
    public void DecimalPressureBalancesPreparedBeforeTheEngineAgreeInInternalUnits()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "basal-heave-expected.json")));
        foreach (var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var data = row.GetProperty("input");
            double Number(string key) => double.Parse(data.GetProperty(key).GetString()!, CultureInfo.InvariantCulture);
            var input = Input(); input.CaseId = data.GetProperty("id").GetString()!;
            input.ExcavationDepth = Number("h") * SoilUnits.Metre; input.ExcavationWidth = Number("b") * SoilUnits.Metre;
            input.StiffLayerDepth = data.GetProperty("d").ValueKind == JsonValueKind.Null ? null : Number("d") * SoilUnits.Metre;
            input.TotalUnitWeight = Number("gamma") * SoilUnits.KiloNewtonPerCubicMetre;
            input.UndrainedStrength = Number("cu") * SoilUnits.KiloPascal; input.BearingCapacityFactor = Number("nc");
            input.PermanentSurcharge = Number("g2") * SoilUnits.KiloPascal; input.VariableSurcharge = Number("q") * SoilUnits.KiloPascal;
            input.VariableCombinationFactor = Number("psi"); input.IncludeExternalBlockShear = data.GetProperty("shear").GetBoolean();
            var actual = new[] { UndrainedBasalHeave.Characteristic(input) }.Concat(UndrainedBasalHeave.Check(input, Ntc())).ToArray();
            var expected = row.GetProperty("expected").EnumerateArray().ToArray();
            Assert.AreEqual(3, actual.Length);
            for (int n = 0; n < 3; n++)
            {
                var a = actual[n]; var e = expected[n];
                Assert.AreEqual(row.GetProperty("width_m").GetDouble() * SoilUnits.Metre, a.MechanismWidth!.Value, 1e-8);
                Assert.AreEqual(e.GetProperty("gross_kpa").GetDouble() * SoilUnits.KiloPascal, a.GrossPressure!.Value, 1e-12);
                Assert.AreEqual(e.GetProperty("relief_kpa").GetDouble() * SoilUnits.KiloPascal, a.ExternalShearRelief!.Value, 1e-12);
                Assert.AreEqual(e.GetProperty("net_kpa").GetDouble() * SoilUnits.KiloPascal, a.NetPressure!.Value, 1e-12);
                Assert.AreEqual(e.GetProperty("capacity_kpa").GetDouble() * SoilUnits.KiloPascal, a.Capacity!.Value, 1e-12);
                Assert.AreEqual(e.GetProperty("utilization").GetDouble(), a.Utilization!.Value, 1e-11);
                Assert.AreEqual(input.CaseId, a.CaseId); Assert.AreEqual(input.StageId, a.StageId); Assert.AreEqual("MPa", a.Unit);
                Assert.AreEqual(n == 0 ? EngineeringOutcome.NotEvaluated : a.Utilization <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, a.Outcome);
            }
        }
    }

    [TestMethod]
    public void PressureBalanceIsScaleInvariantAndPreviousResultsAreIsolated()
    {
        var input = Input(); var previous = UndrainedBasalHeave.Check(input, Ntc());
        double demand = previous[0].Demand!.Value;
        input.ExcavationDepth *= 10; input.ExcavationWidth *= 10; input.TotalUnitWeight /= 10;
        var scaled = UndrainedBasalHeave.Check(input, Ntc());
        Assert.AreEqual(demand, scaled[0].Demand!.Value, 1e-12);
        input.UndrainedStrength /= 10; input.CaseId = "altro";
        var changed = UndrainedBasalHeave.Check(input, Ntc());
        Assert.IsTrue(changed[0].Utilization > scaled[0].Utilization);
        Assert.AreEqual(demand, previous[0].Demand!.Value, 1e-12); Assert.AreEqual("caso", previous[0].CaseId);
        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<string, double>)previous[0].Coefficients).Add("alterazione", 0));
    }

    [TestMethod]
    public void StiffStratumLimitsTheMechanismAndNoReliefRemainsConservative()
    {
        var input = Input(); var deep = UndrainedBasalHeave.Characteristic(input);
        input.StiffLayerDepth = 4000; var shallow = UndrainedBasalHeave.Characteristic(input);
        Assert.AreEqual(4000, shallow.MechanismWidth); Assert.IsTrue(shallow.Demand < deep.Demand);
        input.IncludeExternalBlockShear = false; var noRelief = UndrainedBasalHeave.Characteristic(input);
        Assert.AreEqual(0, noRelief.ExternalShearRelief); Assert.IsTrue(noRelief.Demand > deep.Demand);
        Assert.AreEqual(deep.Capacity, noRelief.Capacity);
    }

    [TestMethod]
    public void UnsupportedSituationsAndMissingApplicabilityCannotPass()
    {
        foreach (var invalidate in new Action<UndrainedBasalHeaveInput>[]
        {
            i => i.HomogeneousUndrainedMechanismConfirmed = false, i => i.LongExcavationConfirmed = false,
            i => i.CharacteristicValuesConfirmed = false, i => i.ApplicabilitySource = "", i => i.BearingCapacitySource = "",
            i => i.ActionSource = "", i => i.Situation = GeotechnicalSituation.Seismic
        })
        {
            var input = Input(); invalidate(input);
            foreach (var r in UndrainedBasalHeave.Check(input, Ntc()))
            { Assert.AreEqual(EngineeringOutcome.NotEvaluated, r.Outcome); Assert.IsNull(r.Capacity); Assert.IsNull(r.Utilization); }
        }
        var unsupported = UndrainedBasalHeave.Check(Input(), new StandardEN1997p1());
        Assert.AreEqual(1, unsupported.Count); Assert.AreEqual(EngineeringOutcome.NotEvaluated, unsupported[0].Outcome);
    }

    [TestMethod]
    public void InvalidUnitsAndUnrepresentableArithmeticAreExplicit()
    {
        foreach (var invalidate in new Action<UndrainedBasalHeaveInput>[]
        {
            i => i.ExcavationDepth = 0, i => i.ExcavationWidth = -1, i => i.StiffLayerDepth = 0,
            i => i.TotalUnitWeight = double.NaN, i => i.UndrainedStrength = 0, i => i.BearingCapacityFactor = double.PositiveInfinity,
            i => i.PermanentSurcharge = -1, i => i.VariableSurcharge = -1, i => i.VariableCombinationFactor = 1.1
        })
        { var input = Input(); invalidate(input); Assert.ThrowsException<ArgumentOutOfRangeException>(() => UndrainedBasalHeave.Check(input, Ntc())); }
        var overflow = Input(); overflow.TotalUnitWeight = double.MaxValue;
        Assert.IsTrue(UndrainedBasalHeave.Check(overflow, Ntc()).All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Utilization is null));
    }

    [TestMethod]
    public void PartialFactorsAndCharacteristicActionsAreAppliedExactlyOnce()
    {
        var input = Input(); input.IncludeExternalBlockShear = false;
        input.PermanentSurcharge = .02; input.VariableSurcharge = .01; input.VariableCombinationFactor = .5;
        var rows = UndrainedBasalHeave.Check(input, Ntc());
        Assert.AreEqual(.1545, rows[0].Demand!.Value, 1e-12); // 1.3*0.09 + 1.5*0.02 + 1.5*0.5*0.01
        Assert.AreEqual(.1225, rows[1].Demand!.Value, 1e-12); // 0.09 + 1.3*0.02 + 1.3*0.5*0.01
        Assert.AreEqual(.15 / 1.4, rows[1].Capacity!.Value, 1e-12);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, rows[1].Outcome);
    }

    [TestMethod]
    public void ZeroNetPressureDoesNotInventAnInfiniteSafetyFactor()
    {
        var input = Input(); input.StiffLayerDepth = 500;
        var row = UndrainedBasalHeave.Check(input, Ntc())[0];
        Assert.IsTrue(row.NetPressure < 0); Assert.AreEqual(0, row.Demand); Assert.AreEqual(0, row.Utilization);
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome);
        StringAssert.Contains(row.Diagnostic, "Pressione netta non positiva");
    }
}
