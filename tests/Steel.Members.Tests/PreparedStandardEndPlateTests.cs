using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Detail prepared once, evaluated for many actual cases: identical to independent Verify calls.</summary>
[TestClass]
public class PreparedStandardEndPlateTests
{
    static IEnumerable<StandardEndPlateInput> Cases(bool ntc, bool flush)
    {
        foreach (var (n, m, v) in new[] { (10000d, 5e6, 20000d), (-2e5, 5e6, 20000d), (0d, -3e7, -40000d), (4e5, 1e8, 1e5), (10000d, 5e6, 20000d) })
            yield return StandardEndPlateCaseData.Input(ntc, flush, n, m, v);
    }

    [DataTestMethod][DataRow(false, false)][DataRow(true, false)][DataRow(false, true)][DataRow(true, true)]
    public void EvaluateOnAPreparedJointEqualsVerifyOfEachCase(bool ntc, bool flush)
    {
        var cases = Cases(ntc, flush).ToArray();
        var prepared = StandardEndPlate.Prepare(cases[0]);
        foreach (var c in cases)
        {
            var direct = StandardEndPlate.Verify(c);
            var shared = StandardEndPlate.Evaluate(prepared, c.Beam.Actions, c.ColumnUpper, c.ColumnLower, c.Beam.ActionCombinationSource);
            Assert.IsTrue(prepared.Owns(shared.Input)); Assert.IsFalse(prepared.Owns(c) && !ReferenceEquals(c, cases[0]));
            Assert.AreEqual(direct.Status, shared.Status); Assert.AreEqual(direct.IsComplete, shared.IsComplete);
            CollectionAssert.AreEqual(direct.Diagnostics.ToArray(), shared.Diagnostics.ToArray());
            CollectionAssert.AreEqual(direct.Checks.Select(x => (x.Id, x.Demand, x.Resistance, x.Reference, x.Unit)).ToArray(),
                shared.Checks.Select(x => (x.Id, x.Demand, x.Resistance, x.Reference, x.Unit)).ToArray());
            CollectionAssert.AreEqual(direct.Report.Results.Select(x => (x.Name, x.Value, x.Unit)).ToArray(), shared.Report.Results.Select(x => (x.Name, x.Value, x.Unit)).ToArray());
            CollectionAssert.AreEqual(direct.Report.Inputs.Select(x => (x.Name, x.Value, x.Unit)).ToArray(), shared.Report.Inputs.Select(x => (x.Name, x.Value, x.Unit)).ToArray());
            Assert.AreEqual(direct.Strength?.AxialTensionResistance, shared.Strength?.AxialTensionResistance);
            Assert.AreEqual(direct.Strength?.NegativeMomentResistance, shared.Strength?.NegativeMomentResistance);
            Assert.AreEqual(direct.Stiffness?.ModelRotation, shared.Stiffness?.ModelRotation); Assert.AreEqual(direct.Stiffness?.AxialOpening, shared.Stiffness?.AxialOpening);
        }
    }

    [TestMethod]
    public void PreparedGeometryProblemsAreReportedForEveryCase()
    {
        var bad = StandardEndPlateCaseData.Input(problem: "missingAlpha");
        var prepared = StandardEndPlate.Prepare(bad);
        var direct = StandardEndPlate.Verify(StandardEndPlateCaseData.Input(problem: "missingAlpha"));
        for (int i = 0; i < 2; i++)
        {
            var r = StandardEndPlate.Evaluate(prepared, bad.Beam.Actions, bad.ColumnUpper, bad.ColumnLower, bad.Beam.ActionCombinationSource);
            CollectionAssert.AreEqual(direct.Diagnostics.ToArray(), r.Diagnostics.ToArray());
            Assert.AreEqual(direct.Status, r.Status);
        }
    }
}
