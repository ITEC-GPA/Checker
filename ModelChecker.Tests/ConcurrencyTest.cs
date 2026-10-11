using System.Collections.Concurrent;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Compatibility;
using GPC.Model.Results.Locations;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

/// <summary>
/// Architecture review R7: the concrete verifier locks per instance, not per process. Verifiers working in parallel on preparations of the
/// same model give the sequential results bit for bit.
/// </summary>
[TestClass]
public class ConcurrencyTest
{
    private static string Run(GPC.Model.Models.Model model)
    {
        var options = new ConcreteVerificationOptions { Standard = new StandardNTC2018Concrete(), Criterion = SectionSolver.FailureAnalysisTypes.ConstantEccentricity };
        var preparation = Verification.PrepareBeam(model, 250, Verification.BeamSample(model.BeamElements[250], "synthetic-static", "P+", .5, SectionSide.Unspecified), "concurrency");
        var result = Verification.Run(preparation, CheckMechanism.UlsBiaxialSection, options.CreateVerifier());
        var detail = (SectionResistanceDetails)result.Details;
        return string.Join("|", result.Execution, result.Utilization?.ToString("R"), detail.NRd.ToString("R"), detail.M1Rd.ToString("R"), detail.M2Rd.ToString("R"));
    }

    [TestMethod]
    public void ParallelVerifiersGiveTheSequentialResults()
    {
        string expected = Run(MixedModelFactory.Create());
        StringAssert.StartsWith(expected, nameof(ExecutionStatus.Completed));
        var models = Enumerable.Range(0, 8).Select(_ => MixedModelFactory.Create()).ToArray();
        var results = new ConcurrentBag<string>();
        Parallel.ForEach(models, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, m => results.Add(Run(m)));
        Assert.AreEqual(models.Length, results.Count);
        foreach (var r in results) Assert.AreEqual(expected, r);
    }
}
