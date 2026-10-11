using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Architecture review R7: the concrete engine needs no lock outside the library. Engines built at the same time on the same section
    /// instance (whose integration mesh is created once under the section lock) and used in parallel give the sequential results bit for
    /// bit, and the solver log accepts the writes of its own parallel loops.
    /// </summary>
    [TestClass]
    public class ConcurrencyTests
    {
        private static ReinforcedConcreteSection Section()
        {
            using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "stress-sections.xml"));
            return (ReinforcedConcreteSection)ModelArchive.Load(file).BeamProperties["R300x500"];
        }

        private static SectionCheckerModelCode2010 Engine(ReinforcedConcreteSection section, SectionSolver.FailureAnalysisTypes criterion)
        {
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, criterion, SectionSolver.FailureDomainTypes.Plastic,
                SectionSolver.StressAnalysisTypes.NonLinear, 2, 0, false, 32);
            return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardNTC2018Concrete(), false);
        }

        private static ResultBeamForces[] Forces(ReinforcedConcreteSection section)
        {
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            return new[] { -900e3, -300e3, 0, 150e3 }.SelectMany(n => new[] { (60e6, 0.0), (90e6, 30e6), (-40e6, 70e6) }
                .Select(m => new ResultBeamForces(n, 0, 0, 0, m.Item1, m.Item2, axes))).ToArray();
        }

        private static string Solve(SectionCheckerModelCode2010 engine, ResultBeamForces force)
        {
            var r = new LegacySectionCalculation(engine).SolveResistance(new SectionAnalysisInput(force));
            return string.Join("|", r.Diagnostics.Status, R(r.N), R(r.M1), R(r.M2), R(r.Utilization));
        }

        private static string R(double? v) => v?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

        [TestMethod]
        public void EnginesBuiltConcurrentlyOnTheSameSectionGiveTheSequentialResults()
        {
            var reference = Section(); var forces = Forces(reference);
            var expected = forces.Select(f => Solve(Engine(reference, SectionSolver.FailureAnalysisTypes.ConstantN), f)).ToArray();
            for (int round = 0; round < 4; round++)
            {
                // A fresh section per round: its integration mesh is built by the first engine, while the others wait on the section lock.
                var shared = Section(); var results = new ConcurrentDictionary<int, string>();
                Parallel.For(0, forces.Length * 2, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                {
                    int k = i % forces.Length;
                    var force = Forces(shared)[k];
                    results[i] = Solve(Engine(shared, SectionSolver.FailureAnalysisTypes.ConstantN), force);
                });
                for (int i = 0; i < forces.Length * 2; i++)
                    Assert.AreEqual(expected[i % forces.Length], results[i], $"round {round}, force {i % forces.Length}");
            }
        }

        [TestMethod]
        public void ParallelSearchesOfOneSolverKeepEveryLogEntry()
        {
            // The raw vector overload runs its searches in parallel on one solver; each search can write the log.
            var sequential = Engine(Section(), SectionSolver.FailureAnalysisTypes.ConstantMxMy);
            var parallel = Engine(Section(), SectionSolver.FailureAnalysisTypes.ConstantMxMy);
            var forces = Enumerable.Range(0, 256).Select(i => new ResultBeamForces(0, 0, 0, 0, (i + 1) * 10e6, 0,
                new CoordinateSystem(sequential.SectionSolver.ConcreteSection.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)))).ToArray();
#pragma warning disable CS0618 // raw parallel path exercised on purpose
            foreach (var force in forces) sequential.SectionSolver.CalculateDomainPoint(new[] { force });
            for (int round = 0; round < 4; round++) parallel.SectionSolver.CalculateDomainPoint(forces);
#pragma warning restore CS0618
            int expected = sequential.SectionSolver.GetLog().Count * 4;
            var log = parallel.SectionSolver.GetLog();
            Assert.IsTrue(expected >= forces.Length, $"the fallbacks did not write the log ({expected} entries): the probe does not exercise concurrent writes");
            Assert.AreEqual(expected, log.Count, "concurrent writes lost log entries");
            Assert.IsTrue(log.All(entry => entry != null), "a concurrent write corrupted a log entry");
        }
    }
}
