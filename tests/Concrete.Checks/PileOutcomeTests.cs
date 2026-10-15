using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class PileOutcomeTests : ConcreteTestBase
    {
        public TestContext TestContext { get; set; }

        private PileRcParameters Parameters()
        {
            var concrete = new ConcreteMaterialEN1992("C35", 35, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450", 200000, 450, 450, .1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            // Reuse the existing section fixture; its cover argument locates the bar centres.
            var section = GetCircularSection(1000, 24, 92, 16, 64, concrete, steel);
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            var standard = new StandardNTC2018Concrete();
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return new PileRcParameters { Solver = checker.SectionSolver, Axes = axes, Section = section, Standard = standard,
                Diameter = 1000, Fck = 35, Fctk05 = concrete.Fctk05, Fcd = Math.Abs(concrete.CalculateFcd(standard)),
                Fyd = Math.Abs(steel.CalculateFyd(standard)), Es = 200000, Cover = 70, LinkDiameter = 10, LinkSpacing = 150,
                ShearModelConfirmed = true, DesignActionsConfirmed = true, GoodBond = true };
        }

        private static PileAction LowDemand(double depth = 2) => new PileAction { Depth = depth, N = 1000, M = 100, V = 100 };

        /// <summary>Large pile of PO-3: D mm as a 32-gon, 24φ26 with centres at D/2 − 93 (cover 70 + link 10 + 13), C35/45 parabola-rectangle, B450C.</summary>
        private PileRcParameters LargePile(double diameter)
        {
            var concrete = new ConcreteMaterialEN1992("C35", 35, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450", 200000, 450, 450, .075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var section = GetCircularSection(diameter, 26, 93, 24, 32, concrete, steel);
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            var standard = new StandardNTC2018Concrete();
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return new PileRcParameters { Solver = checker.SectionSolver, Axes = axes, Section = section, Standard = standard,
                Diameter = diameter, Fck = 35, Fctk05 = concrete.Fctk05, Fcd = Math.Abs(concrete.CalculateFcd(standard)),
                Fyd = Math.Abs(steel.CalculateFyd(standard)), Es = 200000, Cover = 70, LinkDiameter = 10, LinkSpacing = 150,
                ShearModelConfirmed = true, DesignActionsConfirmed = true, GoodBond = true };
        }

        /// <summary>
        /// PO-3 of the ANTHEA bug analysis (15/10/2026): the domain point of a pile is accepted with the tolerance of the section resistance
        /// (DomainPointAxialTolerance, F2-15), so piles of D 1.6 and 2.0 m get MRd at every N; before, |NRd − N| of 1.1-2.0 kN exceeded the 1 kN of the
        /// pile and every depth was "Non verificabile". Expected MRd from an independent fibre integration of the same 32-gon and bars (NTC 2018
        /// 4.1.2.1.2, parabola-rectangle fcd = 0.85 · 35/1.5, εcu = 3.5‰, steel elastic-perfectly plastic fyd = 450/1.15; scratchpad
        /// agent-conc-sle/po3_mrd.py, after the verifier's mrd_circle.py): D 1.6 m, N 0/500/2000 kN: 3293.8, 3581.1, 4367.2 kNm; D 2.0 m: 4307.9, 4691.5,
        /// 5804.1 kNm; tolerance 0.2 % (the integration with or without the concrete displaced by the bars differs by 0.1-0.2 %).
        /// </summary>
        [TestMethod]
        public void LargePilesGetTheirResistanceAtEveryAxialForce()
        {
            foreach (var t in new[] { Tuple.Create(1600d, 0d, 3293.8), Tuple.Create(1600d, 500d, 3581.1), Tuple.Create(1600d, 2000d, 4367.2),
                Tuple.Create(2000d, 0d, 4307.9), Tuple.Create(2000d, 500d, 4691.5), Tuple.Create(2000d, 2000d, 5804.1) })
            {
                string what = "D " + t.Item1 + " N " + t.Item2;
                var result = new PileReinforcement(LargePile(t.Item1)).Check(new PileAction { Depth = 5, N = t.Item2, M = 1079, V = 300 });
                Assert.AreEqual(ExecutionStatus.Completed, result.Execution, what + ": " + result.Message);
                Assert.IsTrue(result.MRdPositive.HasValue && result.MRdNegative.HasValue, what);
                Assert.AreEqual(t.Item3, result.MRdPositive.Value, 2e-3 * t.Item3, what + " MRd+");
                Assert.AreEqual(t.Item3, -result.MRdNegative.Value, 2e-3 * t.Item3, what + " MRd−");
                Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome, what);
            }
        }

        [TestMethod]
        public void AnUnexecutedCheckCannotPassFromItsLabel()
        {
            var result = new PileSectionCheck { Status = "Soddisfatto" };
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
            Assert.AreEqual(ExecutionStatus.NotExecuted, result.Execution);
            Assert.AreEqual(DataStatus.Insufficient, result.Data);
        }

        [TestMethod]
        public void ACompleteNominalCheckHasThreeIndependentStates()
        {
            var result = new PileReinforcement(Parameters()).Check(LowDemand());
            Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
            Assert.AreEqual(ExecutionStatus.Completed, result.Execution);
            Assert.AreEqual(DataStatus.Ready, result.Data);
            Assert.IsNull(result.DevelopmentAvailable, "Nominal N-M-V does not certify construction details");
            result.Status = "Translated by another caller";
            Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        }

        [DataTestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
        public void MissingConfirmationsDoNotPassEvenAtZeroDemand(bool actions, bool shear)
        {
            var p = Parameters(); p.DesignActionsConfirmed = actions; p.ShearModelConfirmed = shear;
            var result = new PileReinforcement(p).Check(new PileAction());
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
            Assert.AreEqual(ExecutionStatus.Completed, result.Execution);
            Assert.AreEqual(DataStatus.Insufficient, result.Data);
        }

        [TestMethod]
        public void AProvenFailureIsRetainedWhenAnotherCheckIsIncomplete()
        {
            var p = Parameters(); p.ShearModelConfirmed = false;
            var result = new PileReinforcement(p).Check(new PileAction { N = 1000, M = 1e6 });
            Assert.IsTrue(result.BendingRatio > 1);
            Assert.AreEqual(EngineeringOutcome.NotSatisfied, result.Outcome);
            Assert.AreEqual(ExecutionStatus.Completed, result.Execution);
            Assert.AreEqual(DataStatus.Insufficient, result.Data);
        }

        [TestMethod]
        public void AnUnsolvedDomainPointIsAnExecutionError()
        {
            var result = new PileReinforcement(Parameters()).Check(new PileAction { N = 1e9, M = 1 });
            Assert.IsNull(result.MRdPositive); Assert.IsNull(result.MRdNegative);
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
            Assert.AreEqual(ExecutionStatus.Error, result.Execution);
            Assert.AreEqual(DataStatus.Ready, result.Data, "The data exists; the resistance could not be solved");
        }

        [TestMethod]
        public void DevelopmentUsesTheTypedOutcomeAndRetainsKnownFailures()
        {
            var service = new PileReinforcement(Parameters());
            var head = service.Check(LowDemand(0)); var middle = service.Check(LowDemand(5));
            var failed = service.Check(new PileAction { Depth = 0, N = 1000, M = 1e6 });
            var run = service.Detail("A", 0, 10, 10, new[] { head, middle });
            head.Status = "Any translated label";
            PileReinforcement.ApplyDevelopment(run, new[] { head, middle, failed });
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, head.Outcome);
            Assert.AreEqual(DataStatus.Insufficient, head.Data);
            Assert.AreEqual("Parziale", head.Status);
            Assert.AreEqual(ExecutionStatus.Completed, head.Execution);
            Assert.IsNull(head.UsableMRdPositive); Assert.IsTrue(head.MRdPositive > 0);
            Assert.AreEqual(EngineeringOutcome.Satisfied, middle.Outcome);
            Assert.AreEqual(DataStatus.Ready, middle.Data);
            Assert.AreEqual(EngineeringOutcome.NotSatisfied, failed.Outcome);
            Assert.AreEqual(DataStatus.Insufficient, failed.Data);
        }

        [TestMethod]
        public void CandidateSelectionRejectsFailedMinimumReinforcement()
        {
            var bad = Parameters(); bad.PileMinimumRequirements = true; bad.LinkSpacing = 500;
            var good = Parameters(); good.PileMinimumRequirements = true;
            var check = new PileReinforcement(bad).Check(new PileAction());
            Assert.AreEqual(false, check.MinimumReinforcementPassed);
            Assert.AreEqual(EngineeringOutcome.NotSatisfied, check.Outcome);
            Assert.AreEqual(1, PileReinforcement.SelectCandidate(new[] { bad, good }, new[] { new PileAction() }));
        }

        [TestMethod]
        public void CancellationDoesNotPublishAResistanceBatch()
        {
            var service = new PileReinforcement(Parameters()); var table = new PileResistanceTable();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsException<OperationCanceledException>(() => service.PrepareResistance(new[] { 0d, 1000 },
                    () => new PileReinforcement(Parameters()), table, cancellation.Token, 4));
                Assert.AreEqual(0, table.Count);
            }
        }

        [TestMethod]
        public void NonfiniteActionsAreRejectedBeforeChecking()
        {
            var service = new PileReinforcement(Parameters());
            Assert.ThrowsException<ArgumentException>(() => service.Check(new PileAction { N = double.NaN }));
            Assert.ThrowsException<ArgumentException>(() => service.Check(new PileAction { V = double.PositiveInfinity }));
        }

        [TestMethod]
        public void CaptureLegacyPileValuesAndLabels()
        {
            var rows = new List<string>();
            string Number(double? value) => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "null";
            void Add(string name, PileSectionCheck r) => rows.Add(string.Join("\t", name,
                Number(r.Action.N), Number(r.Action.V), Number(r.Action.M), Number(r.MRdPositive), Number(r.MRdNegative),
                Number(r.VRd), Number(r.BendingRatio), Number(r.ShearRatio), Number(r.CotTheta), Number(r.GoverningRatio),
                Number(r.UsableMRdPositive), Number(r.UsableMRdNegative), r.MinimumReinforcementPassed, r.DevelopmentAvailable, r.Status, r.Message));
            var parameters = Parameters(); var service = new PileReinforcement(parameters);
            Add("zero", service.Check(new PileAction()));
            Add("low", service.Check(LowDemand()));
            Add("reverse", service.Check(new PileAction { N = 1000, M = -100, V = -100 }));
            Add("bending-fail", service.Check(new PileAction { N = 1000, M = 1e6 }));
            Add("shear-fail", service.Check(new PileAction { N = 1000, V = 1e6 }));
            Add("outside-domain", service.Check(new PileAction { N = 1e9, M = 1 }));
            parameters = Parameters(); parameters.DesignActionsConfirmed = false;
            Add("unconfirmed-actions", new PileReinforcement(parameters).Check(LowDemand()));
            parameters = Parameters(); parameters.ShearModelConfirmed = false;
            Add("unconfirmed-shear", new PileReinforcement(parameters).Check(LowDemand()));
            parameters = Parameters(); parameters.PileMinimumRequirements = true; parameters.LinkSpacing = 500;
            Add("minimum-fail", new PileReinforcement(parameters).Check(new PileAction()));
            var checks = new[] { service.Check(LowDemand(0)), service.Check(LowDemand(5)) };
            var run = service.Detail("A", 0, 10, 10, checks);
            PileReinforcement.ApplyDevelopment(run, checks);
            Add("undeveloped", checks[0]); Add("developed", checks[1]);
            Assert.AreEqual(11, rows.Count);
            string target = Environment.GetEnvironmentVariable("GPC_F4_CAPTURE") ?? Path.Combine(TestContext.TestResultsDirectory, "pile-legacy.tsv");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (var writer = new StreamWriter(new FileStream(target, FileMode.CreateNew, FileAccess.Write)))
                foreach (string row in rows) writer.WriteLine(row);
            TestContext.AddResultFile(target);
        }
    }
}
