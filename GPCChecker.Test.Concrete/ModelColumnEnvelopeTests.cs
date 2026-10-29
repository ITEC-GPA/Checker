using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Slenderness;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Envelope of the actions and reduced domain N–M1Rd,max of the model column (GPCChecker.Concrete 0.28.0.0, method page
    /// docs/metodi/ca.colonna-modello.md §6.9). Expected values: the independent reference ReferenceCalculations/model-column (cases
    /// R400-3+3D24-l0-6-domain and R400-3+3D24-l0-6-N1000-phi519 of derive_model_column_oracles.py, net section), the projection of the moments and
    /// φef of (5.19) by hand; the identity with the single model column bit for bit (the cache of the curves).
    /// </summary>
    [TestClass]
    public class ModelColumnEnvelopeTests
    {
        private const double kN = 1e3, kNm = 1e6;

        /// <summary>Reference R400-3+3D24-l0-6-domain: N [kN], M1Rd,max and M(χu) [kNm].</summary>
        private static readonly double[] DomainN = { 0, 500, 1000, 1500, 2000, 2500, 3000, 3200 };
        private static readonly double[] DomainM1 = { 174.198133, 213.920877, 223.656088, 169.406963, 121.874295, 65.607628, 17.828513, 3.896868 };
        private static readonly double[] DomainMu = { 174.198133, 246.263712, 276.511865, 234.963414, 183.950791, 119.788732, 44.827128, 14.178984 };

        /// <summary>The column of the reference at l0 = 6 m: l = 3 m, c = 10, no imperfection, no minimum eccentricity, creep neglected (θ = 0).</summary>
        private static ModelColumnRequest Template(double direction = 0, ModelColumnCreep creep = null, ModelColumnImperfection imperfection = null)
            => new ModelColumnRequest(0, direction, 6000, 3000, null, 10, imperfection ?? ModelColumnImperfection.None, ModelColumnMinimumEccentricity.None, creep);

        private static ModelColumnEnvelopeResult Envelope(ReinforcedConcreteSection section, ModelColumnRequest template, IReadOnlyList<ModelColumnAction> actions,
            int intervals = 10, IProgress<ModelColumnProgress> progress = null, CancellationToken token = default(CancellationToken))
            => ModelColumnEnvelope.Calculate(template, actions, ModelColumnTests.Checker(section), section, DomainPointContractTests.LocalAxes(section), intervals, progress, token);

        private static ModelColumnAction A(string name, double n, double m1, double m2 = 0) => new ModelColumnAction(name, n * kN, m1 * kNm, m2 * kNm);

        private static void Relative(double expected, double actual, double tolerance, string what)
        {
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: expected {1:G9}, actual {2:G9}, deviation {3:+0.0000%;-0.0000%}",
                what, expected, actual, (actual - expected) / expected));
            Assert.AreEqual(expected, actual, Math.Abs(expected) * tolerance, what);
        }

        /// <summary>Synchronous progress (Progress&lt;T&gt; posts to the thread pool).</summary>
        private sealed class Recorder : IProgress<ModelColumnProgress>
        {
            public readonly List<ModelColumnProgress> Steps = new List<ModelColumnProgress>();
            public void Report(ModelColumnProgress value) => Steps.Add(value);
        }

        // ---------------------------------------------------------------- envelope

        /// <summary>
        /// Seven actions on R400 at l0 = 6 m: the governing one is C7 (1500 kN, 160 kNm: 160/169.407 = 0.9445 of the reference), all verified; C3 with a
        /// negative moment in θ + 180° (symmetric section: the same capacity), C4 in tension, C5 with a transverse moment, C6 on the curve of C1.
        /// </summary>
        [TestMethod]
        public void EnvelopeOfTheActions()
        {
            var section = ModelColumnTests.R400();
            var actions = new[] { A("C1", -1000, 180), A("C2", -2000, 100), A("C3", -1000, -150), A("C4", 200, 50), A("C5", -2500, 40, 30), A("C6", -1000, 120),
                A("C7", -1500, 160) };
            var progress = new Recorder();
            var e = Envelope(section, Template(), actions, progress: progress);
            Console.WriteLine(e.Status);
            Assert.AreEqual(7, e.Actions.Count);
            // ratios against the independent reference: M0Ed / M1Rd,max(N)
            double[] capacity = { 223.656088, 121.874295, 223.656088, double.NaN, 65.607628, 223.656088, 169.406963 };
            double[] moment = { 180, 100, 150, double.NaN, 40, 120, 160 };
            for (int i = 0; i < 7; i++)
            {
                var r = e.Actions[i];
                Assert.AreEqual(i, r.Index);
                Assert.AreSame(actions[i], r.Action);
                if (i == 3) continue;
                Relative(capacity[i] * kNm, r.Result.Capacity.MaximumFirstOrderMoment, 2e-3, r.Action.Name + " M1Rd,max");
                Relative(moment[i] / capacity[i], r.Ratio.Value, 2e-3, r.Action.Name + " ratio");
                Assert.AreEqual(moment[i] * kNm, r.Result.FirstOrder.TotalMoment.Value, 1e-6, r.Action.Name + " M0Ed,tot = |M| (no imperfection)");
                Assert.AreEqual(ModelColumnActionStatus.Verified, r.Status);
                Assert.IsTrue(r.Verified == true && r.Result.Verified == true);
            }
            // projection by hand: M = M1 cos 0 + M2 sin 0 = M1, M⊥ = M2; θ + 180° for a negative M
            Assert.AreEqual(-150 * kNm, e.Actions[2].Moment);
            Assert.AreEqual(180, e.Actions[2].DirectionDegrees);
            Assert.AreEqual(180, e.Actions[2].Result.DirectionDegrees);
            Assert.AreEqual(0, e.Actions[0].DirectionDegrees);
            Assert.AreEqual(30 * kNm, e.Actions[4].TransverseMoment);
            Assert.IsTrue(e.Actions[4].TransverseNeglected && e.Actions.Count(a => a.TransverseNeglected) == 1);
            CollectionAssert.Contains(e.Warnings.ToList(), ModelColumnEnvelopeWarning.TransverseFirstOrderMoment);
            // tension: not applicable
            Assert.AreEqual(ModelColumnActionStatus.Tension, e.Actions[3].Status);
            Assert.IsNull(e.Actions[3].Result);
            Assert.IsNull(e.Actions[3].Ratio);
            Assert.IsNull(e.Actions[3].Verified);
            // outcome and governing action
            Assert.AreEqual(true, e.Verified);
            Assert.AreEqual(6, e.Governing);
            Assert.AreEqual(6, e.GoverningCalculated);
            // symmetric section: θ + 180° has the capacity of θ
            Relative(e.Actions[0].Result.Capacity.MaximumFirstOrderMoment, e.Actions[2].Result.Capacity.MaximumFirstOrderMoment, 1e-9, "theta + 180");
            // one curve per (N, direction, φef): 5 for the actions, 11 for each of the two domains (10 intervals; the N of the actions are cached)
            Assert.AreEqual(5 + 11 + 11, e.Curves);
            // each action is the single model column on its request, bit for bit
            foreach (var r in e.Actions.Where(a => a.Result != null))
            {
                var single = ModelColumnTests.Run(section, new ModelColumnRequest(r.Action.AxialForce, r.DirectionDegrees, 6000, 3000, Math.Abs(r.Moment), 10,
                    ModelColumnImperfection.None, ModelColumnMinimumEccentricity.None));
                Assert.AreEqual(single.Capacity.MaximumFirstOrderMoment, r.Result.Capacity.MaximumFirstOrderMoment, 0.0, r.Action.Name);
                Assert.AreEqual(single.Equilibrium.Curvature.Value, r.Result.Equilibrium.Curvature.Value, 0.0, r.Action.Name);
                Assert.AreEqual(single.Points.Count, r.Result.Points.Count, r.Action.Name);
                Assert.AreEqual(single.Status, r.Result.Status, r.Action.Name);
                CollectionAssert.AreEqual(single.Warnings.ToList(), r.Result.Warnings.ToList(), r.Action.Name);
            }
            // the curve of C1 serves C6
            Assert.AreEqual(e.Actions[0].Result.Capacity.MaximumFirstOrderMoment, e.Actions[5].Result.Capacity.MaximumFirstOrderMoment, 0.0);
            // progress: the actions one by one, then the points of each domain
            var stage = progress.Steps.Where(s => s.Stage == ModelColumnStage.Actions).Select(s => s.Completed).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, 7).ToList(), stage);
            Assert.IsTrue(progress.Steps.Where(s => s.Stage == ModelColumnStage.Actions).All(s => s.Total == 7));
            var domain = progress.Steps.Where(s => s.Stage == ModelColumnStage.Domain).ToList();
            Assert.AreEqual(e.Domains.Sum(d => d.Points.Count), domain.Count);
            Assert.IsTrue(domain.Last().Completed == domain.Last().Total);
            StringAssert.Contains(e.Status, "governing C7");
        }

        /// <summary>
        /// The reduced domain of θ: from N = 0 to NRd,c with the N of the actions, against the independent reference (0.2 %); M1Rd,max ≤ M(χu), equal
        /// at N = 0 (no second order), zero at NRd,c (symmetric section); the domain of θ + 180° at the same N and the same values (symmetry).
        /// </summary>
        [TestMethod]
        public void ReducedDomainAgainstTheReference()
        {
            var section = ModelColumnTests.R400();
            var e = Envelope(section, Template(), new[] { A("C1", -1000, 180), A("C2", -2000, 100), A("C3", -1000, -150), A("C5", -2500, 40), A("C7", -1500, 160),
                A("C8", -500, 10), A("C9", -3000, 5), A("C10", -3200, 1) });
            Assert.AreEqual(2, e.Domains.Count);
            var d = e.Domains[0];
            Assert.AreEqual(0, d.DirectionDegrees);
            Assert.AreEqual(180, e.Domains[1].DirectionDegrees);
            Assert.AreEqual(6000, d.EffectiveLength);
            Assert.AreEqual(10, d.CurvatureFactor);
            Assert.AreEqual(0, d.EffectiveCreepRatio);
            Assert.AreEqual(0, d.ImperfectionEccentricity);
            Assert.IsNull(d.MinimumEccentricity);
            Relative(-3290.345 * kN, d.CompressionResistance, 1e-4, "NRd,c of the reference");
            // the grid (11 points) and the N of the actions of θ (7, all different from the grid)
            Assert.AreEqual(11 + 7, d.Points.Count);
            Assert.AreEqual(0, d.Points[0].AxialForce);
            Assert.AreEqual(d.CompressionResistance, d.Points.Last().AxialForce);
            for (int i = 1; i < d.Points.Count; i++) Assert.IsTrue(d.Points[i].AxialForce < d.Points[i - 1].AxialForce, "decreasing N " + i);
            for (int i = 0; i < DomainN.Length; i++)
            {
                var point = d.Points.Single(p => p.AxialForce == -DomainN[i] * kN);
                Relative(DomainM1[i] * kNm, point.MaximumFirstOrderMoment.Value, i == DomainN.Length - 1 ? 5e-3 : 2e-3, "M1Rd,max at N = " + DomainN[i]);
                Relative(DomainMu[i] * kNm, point.UltimateMoment.Value, 2e-3, "M(chi_u) at N = " + DomainN[i]);
            }
            Assert.AreEqual(ModelColumnMode.SectionFailure, d.Points[0].Mode);
            Assert.AreEqual(d.Points[0].UltimateMoment.Value, d.Points[0].MaximumFirstOrderMoment.Value, 0.0, "N = 0: M1Rd,max = M(chi_u)");
            foreach (var p in d.Points)
            {
                Assert.IsNull(p.Rejection);
                Assert.IsTrue(p.MaximumFirstOrderMoment.Value <= p.UltimateMoment.Value * (1 + 1e-12), "inside the section domain at N = " + p.AxialForce);
                Assert.AreEqual(p.CurvatureAtMaximum.Value * 6000.0 * 6000 / 10, p.DeflectionAtMaximum.Value, 1e-9 * Math.Max(1, p.DeflectionAtMaximum.Value));
            }
            Assert.IsTrue(Math.Abs(d.Points.Last().MaximumFirstOrderMoment.Value) < 1e-6 * kNm && Math.Abs(d.Points.Last().UltimateMoment.Value) < 1e-6 * kNm,
                "NRd,c: no moment (symmetric section)");
            // θ + 180°: grid and the N of C3 (on the grid of θ: same values by symmetry)
            var opposite = e.Domains[1];
            Assert.AreEqual(11 + 1, opposite.Points.Count);
            Assert.AreEqual(d.CompressionResistance, opposite.CompressionResistance, 1e-9 * Math.Abs(d.CompressionResistance));
            foreach (var p in opposite.Points)
            {
                // NRd,c integrated on the planes of θ + 180°: the same N of the grid within the round-off
                var same = d.Points.Single(q => Math.Abs(q.AxialForce - p.AxialForce) <= 1e-9 * Math.Abs(d.CompressionResistance));
                Assert.AreEqual(same.MaximumFirstOrderMoment.Value, p.MaximumFirstOrderMoment.Value, 1e-6 * DomainM1[0] * kNm, "symmetry at N = " + p.AxialForce);
                Assert.AreEqual(same.UltimateMoment.Value, p.UltimateMoment.Value, 1e-6 * DomainM1[0] * kNm, "symmetry of M(chi_u) at N = " + p.AxialForce);
            }
        }

        /// <summary>
        /// Creep from (5.19) for each action: φef = φ(∞,t0) (M0Eqp + |NEqp| ei)/(M0Ed + |NEd| ei), ei = 20 mm assigned, by hand; one curve per φef. The
        /// action of 1000 kN and 100 kNm against the reference R400-3+3D24-l0-6-N1000-phi519 (φef = 0.61667); the domain at the φef of the governing.
        /// </summary>
        [TestMethod]
        public void CreepOfEachActionFromTheFinalCoefficient()
        {
            var section = ModelColumnTests.R400();
            var template = Template(creep: ModelColumnCreep.FromFinalCoefficient(2, 30 * kNm, -350 * kN), imperfection: ModelColumnImperfection.Assigned(20));
            var e = Envelope(section, template, new[] { A("A", -1000, 100), A("B", -1000, 60), A("C", -1500, 100) });
            double qp = 30 * kNm + 350 * kN * 20;
            double[] phi = { 2 * qp / (100 * kNm + 1000 * kN * 20), 2 * qp / (60 * kNm + 1000 * kN * 20), 2 * qp / (100 * kNm + 1500 * kN * 20) };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(phi[i], e.Actions[i].Result.Creep.EffectiveRatio, 1e-12, e.Actions[i].Action.Name);
                Assert.AreEqual(e.Actions[i].Action.AxialForce * -20 + Math.Abs(e.Actions[i].Moment), e.Actions[i].Result.FirstOrder.TotalMoment.Value, 1e-6);
            }
            Relative(212.609 * kNm, e.Actions[0].Result.Capacity.MaximumFirstOrderMoment, 2e-3, "N = 1000 kN, phi_ef (5.19) against the reference");
            Assert.IsTrue(e.Actions[1].Result.Capacity.MaximumFirstOrderMoment < e.Actions[0].Result.Capacity.MaximumFirstOrderMoment, "larger phi_ef, smaller capacity");
            int governing = e.Governing.Value;
            Assert.AreEqual(Enumerable.Range(0, 3).OrderByDescending(i => e.Actions[i].Ratio.Value).First(), governing);
            var d = e.Domains.Single();
            Assert.AreEqual(e.Actions[governing].Result.Creep.EffectiveRatio, d.EffectiveCreepRatio);
            Assert.AreEqual(20, d.ImperfectionEccentricity);
            Assert.AreEqual(11 + 1, d.Points.Count, "the grid and the N of the governing action, the only one with its phi_ef");
            Assert.AreEqual(3 + 11, e.Curves);
            Assert.AreEqual(e.Actions[governing].Result.Capacity.MaximumFirstOrderMoment,
                d.Points.Single(p => p.AxialForce == e.Actions[governing].Action.AxialForce).MaximumFirstOrderMoment.Value, 0.0);
        }

        /// <summary>An action beyond the capacity and one beyond NRd,c: not verified; the governing action and the governing calculated one.</summary>
        [TestMethod]
        public void NotVerifiedAndBeyondTheResistance()
        {
            var section = ModelColumnTests.R400();
            var e = Envelope(section, Template(), new[] { A("C1", -1000, 180), A("C8", -3000, 25), A("C9", -3400, 0) });
            Assert.AreEqual(ModelColumnActionStatus.NotVerified, e.Actions[1].Status);
            Assert.IsFalse(e.Actions[1].Result.Equilibrium.Found);
            Relative(25 / 17.828513, e.Actions[1].Ratio.Value, 2e-3, "C8 ratio against the reference");
            Assert.AreEqual(ModelColumnActionStatus.Rejected, e.Actions[2].Status);
            Assert.AreEqual(ModelColumnRejection.AxialForceBeyondResistance, e.Actions[2].Rejection);
            Relative(-3290.345 * kN, e.Actions[2].RejectionValue, 1e-4, "NRd,c of the rejection");
            Assert.IsTrue(double.IsPositiveInfinity(e.Actions[2].Ratio.Value) && e.Actions[2].Verified == false && e.Actions[2].Result == null);
            Assert.AreEqual(false, e.Verified);
            Assert.AreEqual(2, e.Governing);
            Assert.AreEqual(1, e.GoverningCalculated);
            CollectionAssert.DoesNotContain(e.Warnings.ToList(), ModelColumnEnvelopeWarning.RejectedActions);
            // the N beyond NRd,c is not a point of the domain
            Assert.IsTrue(e.Domains.Single().Points.All(p => p.AxialForce >= e.Domains[0].CompressionResistance));
        }

        /// <summary>Rejections of the actions (transverse moment of an asymmetric section): outcome unknown, no domain; only tension: nothing to check.</summary>
        [TestMethod]
        public void RejectedActionsAndNoCompression()
        {
            var asymmetric = ModelColumnTests.Bars(300, 600, 25, new[] { -110.0, -260, 20 }, new[] { 110.0, -260, 20 }, new[] { -110.0, 260, 20 }, new[] { 110.0, 260, 20 });
            var e = Envelope(asymmetric, Template(45), new[] { A("R1", -600, 50), A("R2", -900, 30) });
            Assert.IsTrue(e.Actions.All(a => a.Status == ModelColumnActionStatus.Rejected && a.Rejection == ModelColumnRejection.TransverseMomentBeyondLimit));
            Assert.IsNull(e.Verified);
            Assert.IsNull(e.Governing);
            Assert.IsNull(e.GoverningCalculated);
            CollectionAssert.Contains(e.Warnings.ToList(), ModelColumnEnvelopeWarning.RejectedActions);
            Assert.AreEqual(0, e.Domains.Count, "the reference of the domain is rejected too");
            var section = ModelColumnTests.R400();
            var tension = Envelope(section, Template(), new[] { A("T1", 100, 20), A("T2", 300, 0) });
            Assert.IsTrue(tension.Actions.All(a => a.Status == ModelColumnActionStatus.Tension));
            Assert.IsNull(tension.Verified);
            CollectionAssert.Contains(tension.Warnings.ToList(), ModelColumnEnvelopeWarning.NoCompressedAction);
            Assert.AreEqual(1, tension.Domains.Count, "the domain of θ with the creep of the template");
            Assert.AreEqual(11, tension.Domains[0].Points.Count);
            var empty = Envelope(section, Template(), new ModelColumnAction[0]);
            Assert.IsNull(empty.Verified);
            Assert.AreEqual(0, empty.Actions.Count);
            // rejections common to all the actions are thrown; cancellation
            Assert.AreEqual(ModelColumnRejection.InvalidRequest, Assert.ThrowsException<ModelColumnException>(() => Envelope(section, Template(), new[] { A("C1", -1000, 10) }, 3)).Reason);
            Assert.AreEqual(ModelColumnRejection.UnsupportedStandard, Assert.ThrowsException<ModelColumnException>(() => ModelColumnEnvelope.Calculate(Template(),
                new[] { A("C1", -1000, 10) }, ModelColumnTests.Checker(section, new GPC.Model.Standards.StandardModelCode2010()), section, DomainPointContractTests.LocalAxes(section))).Reason);
            Assert.ThrowsException<ArgumentException>(() => new ModelColumnAction("X", double.NaN, 0, 0));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.ThrowsException<OperationCanceledException>(() => Envelope(section, Template(), new[] { A("C1", -1000, 10) }, token: cancelled.Token));
            }
        }

        /// <summary>The reduced domain of a single request: grid and N of the request, φef of the request; the limits of the intervals.</summary>
        [TestMethod]
        public void DomainOfASingleRequest()
        {
            var section = ModelColumnTests.R400();
            var request = new ModelColumnRequest(-1000 * kN, 0, 6000, 3000, 100 * kNm, imperfection: ModelColumnImperfection.None,
                minimumEccentricity: ModelColumnMinimumEccentricity.Ntc2018, creep: ModelColumnCreep.Effective(1));
            var progress = new Recorder();
            var d = ModelColumnAnalysis.Domain(request, ModelColumnTests.Checker(section), section, DomainPointContractTests.LocalAxes(section), 8, progress);
            var single = ModelColumnTests.Run(section, request);
            Assert.AreEqual(9 + 1, d.Points.Count);
            Assert.AreEqual(1, d.EffectiveCreepRatio);
            Assert.AreEqual(single.Section.CompressionResistance, d.CompressionResistance);
            Assert.AreEqual(single.FirstOrder.MinimumEccentricity.Value, d.MinimumEccentricity.Value);
            Assert.AreEqual(30, d.MinimumEccentricity.Value, 1e-12, "NTC: max(l0/200; 20 mm) = 30 mm");
            Assert.AreEqual(single.Capacity.MaximumFirstOrderMoment, d.Points.Single(p => p.AxialForce == -1000 * kN).MaximumFirstOrderMoment.Value, 0.0);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToList(), progress.Steps.Select(s => s.Completed).ToList());
            Assert.IsTrue(progress.Steps.All(s => s.Stage == ModelColumnStage.Domain && s.Total == 10));
            foreach (int intervals in new[] { 3, 201 })
                Assert.AreEqual(ModelColumnRejection.InvalidRequest, Assert.ThrowsException<ModelColumnException>(() => ModelColumnAnalysis.Domain(request,
                    ModelColumnTests.Checker(section), section, DomainPointContractTests.LocalAxes(section), intervals)).Reason);
            Assert.AreEqual(ModelColumnRejection.TensionAxialForce, Assert.ThrowsException<ModelColumnException>(() => ModelColumnAnalysis.Domain(
                new ModelColumnRequest(100 * kN, 0, 6000, 3000), ModelColumnTests.Checker(section), section, DomainPointContractTests.LocalAxes(section))).Reason);
        }
    }
}
