using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

[TestClass, DoNotParallelize]
public class SectionResponseOpenSeesTests
{
    [DataTestMethod] [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void DeformationControlledCurveMatchesIndependentOpenSees(int caseIndex)
    {
        var reference = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Validation", "opensees-curves-3.8.0.json")))!;
        var c = reference["cases"]![caseIndex]!;
        double D(JsonNode row, string key) => row[key]!.GetValue<double>();
        var fibers = c["fibers"]!.AsArray().Select((f, i) => (Component: f!["component"]!.GetValue<string>(), Fiber: new HistoryFiber("f" + i, 0, D(f, "y"), D(f, "area")))).ToArray();
        var steel = c["plastic"]!.GetValue<bool>() ? (IHistoryMaterialLaw)new HistoryBilinearSteelLaw(200000, 250, 2000) : new HistoryElasticLaw(200000);
        var concrete = new HistoryModelEnvelopeLaw(new Material("Reference concrete", new StressStrainTable([0, -22.5, -30, -30], [0, -.001, -.002, -.0035]), new StressStrainTable([0, 0], [0, .01]), 30000, 30000, .2, 0, 0));
        var section = new HistorySection(fibers.GroupBy(f => f.Component).Select(g => new HistoryComponent(g.Key, g.Key == "steel" ? steel : concrete, g.Select(f => f.Fiber).ToArray(), true)).ToArray());
        bool mc = c["mode"]!.GetValue<string>() == "MC";
        var options = new SectionResponseOptions { Control = mc ? SectionResponseControl.MomentCurvature : SectionResponseControl.AxialForceStrain,
            AxialForce = mc ? D(c, "n") : null, ReferenceY = D(c, "reference"), SubstepsPerTarget = c["substeps"]!.GetValue<int>(), MaximumSubdivisions = 0 };
        var result = BridgeSectionResponseAnalysis.Calculate(section, c["points"]!.AsArray().Select(p => D(p!, "target")), options);
        Assert.IsTrue(result.Completed, result.Message);
        var endpoints = result.Points.Where(p => p.TargetIndex >= 0 && p.ReachedTarget).ToArray();
        double maxStress = 0, maxStrain = 0, maxForce = 0, maxMoment = 0;
        for (int i = 0; i < endpoints.Length; i++)
        {
            var a = endpoints[i]; var e = c["points"]![i]!;
            void Near(double expected, double actual, double abs, string label) => Assert.AreEqual(expected, actual, abs + Math.Abs(expected) * 2e-7, c["name"] + "[" + i + "] " + label);
            Near(D(e, "axial"), a.State.TotalPlane.AxialStrain, 2e-9, "epsilon0"); Near(D(e, "curvature"), a.State.TotalPlane.Curvature, 2e-12, "kappa");
            Near(D(e, "n"), a.State.N, .02, "N"); Near(D(e, "mref"), a.MomentAtReference, 5, "Mref");
            maxForce = Math.Max(maxForce, Math.Abs(D(e, "n") - a.State.N)); maxMoment = Math.Max(maxMoment, Math.Abs(D(e, "mref") - a.MomentAtReference));
            for (int j = 0; j < fibers.Length; j++)
            {
                var f = a.State.Fibers.Single(f => f.Fiber.Id == "f" + j);
                double es = e["stresses"]![j]!.GetValue<double>(), ee = e["strains"]![j]!.GetValue<double>();
                Near(es, f.Stress, .0005, "sigma " + j); Near(ee, f.TotalStrain, 2e-9, "epsilon " + j);
                maxStress = Math.Max(maxStress, Math.Abs(es - f.Stress)); maxStrain = Math.Max(maxStrain, Math.Abs(ee - f.TotalStrain));
            }
        }
        Console.WriteLine($"{c["name"]}: states={endpoints.Length}; maxStress={maxStress:R} MPa; maxStrain={maxStrain:R}; maxN={maxForce:R} N; maxM={maxMoment:R} Nmm");
        var output = Environment.GetEnvironmentVariable("BRIDGE_VALIDATION_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "curve-" + caseIndex + ".json"), System.Text.Json.JsonSerializer.Serialize(new {
                name = c["name"]!.GetValue<string>(), maxStress, maxStrain, maxForce, maxMoment,
                points = endpoints.Select(p => new { axial = p.State.TotalPlane.AxialStrain, curvature = p.State.TotalPlane.Curvature, n = p.State.N, mref = p.MomentAtReference,
                    stresses = fibers.Select(f => p.State.Fibers.Single(a => a.Fiber.Id == f.Fiber.Id).Stress),
                    strains = fibers.Select(f => p.State.Fibers.Single(a => a.Fiber.Id == f.Fiber.Id).TotalStrain) }) }));
        }
    }
}
