using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Concrete.Checkers;
using GPC.Geometry;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Slenderness
{
    /// <summary>Stage of a calculation of the model column with several curves.</summary>
    public enum ModelColumnStage
    {
        /// <summary>The actions of the envelope, one step per action.</summary>
        Actions,
        /// <summary>The points of a reduced domain, one step per point.</summary>
        Domain
    }

    /// <summary>Progress of an envelope or of a reduced domain: the stage and the steps completed of the stage. Immutable.</summary>
    public sealed class ModelColumnProgress
    {
        public ModelColumnStage Stage { get; }
        public int Completed { get; }
        public int Total { get; }

        public ModelColumnProgress(ModelColumnStage stage, int completed, int total) { Stage = stage; Completed = completed; Total = total; }
    }

    /// <summary>
    /// Point of the reduced domain at the axial force N (N, compression negative): the capacity M1Rd,max of the first order and the moment M(χu)
    /// of the section at the ultimate curvature (the resistance MRd(N) of the plastic domain in the direction, Nmm), the curvature χ* (1/mm) and
    /// the deflection e2* (mm) of the maximum and the mode; all null with the <see cref="Rejection"/> of the point (transverse moment).
    /// </summary>
    public sealed class ModelColumnDomainPoint
    {
        public double AxialForce { get; internal set; }
        public double? MaximumFirstOrderMoment { get; internal set; }
        public double? UltimateMoment { get; internal set; }
        public double? CurvatureAtMaximum { get; internal set; }
        public double? DeflectionAtMaximum { get; internal set; }
        public ModelColumnMode? Mode { get; internal set; }
        public ModelColumnRejection? Rejection { get; internal set; }
    }

    /// <summary>
    /// Reduced domain N–M1Rd,max of a model column (0.28.0.0, method page ca.colonna-modello §6.9): direction θ (degrees, axes of the forces), l0
    /// (mm), c, φef of every point, resistance in centred compression NRd,c (N, compression negative, with the law of the creep), eccentricities of
    /// the imperfection ei and of the minimum e0 (mm; null without minimum) of the column, points from N = 0 to NRd,c (decreasing N, increasing
    /// compression). An action (N, M0Ed,tot) is inside when M0Ed,tot ≤ M1Rd,max(N); the moment M(χu) is the domain of the section without
    /// second-order effects.
    /// </summary>
    public sealed class ModelColumnDomain
    {
        public double DirectionDegrees { get; internal set; }
        public double EffectiveLength { get; internal set; }
        public double CurvatureFactor { get; internal set; }
        public double EffectiveCreepRatio { get; internal set; }
        public double CompressionResistance { get; internal set; }
        public double ImperfectionEccentricity { get; internal set; }
        public double? MinimumEccentricity { get; internal set; }
        public IReadOnlyList<ModelColumnDomainPoint> Points { get; internal set; }
    }

    /// <summary>
    /// An action of the envelope: name, axial force N (N, compression negative) and first-order moments M1, M2 (Nmm) at the critical section,
    /// without imperfection, in the axes of the forces of the checker (the axes of <see cref="ModelColumnAnalysis.Calculate(ModelColumnRequest, SectionCheckerModelCode2010, ReinforcedConcreteSection, CoordinateSystem, CancellationToken)"/>).
    /// Immutable.
    /// </summary>
    public sealed class ModelColumnAction
    {
        public string Name { get; }
        public double AxialForce { get; }
        public double Moment1 { get; }
        public double Moment2 { get; }

        public ModelColumnAction(string name, double axialForce, double moment1, double moment2)
        {
            if (!ModelColumnAnalysis.Finite(axialForce) || !ModelColumnAnalysis.Finite(moment1) || !ModelColumnAnalysis.Finite(moment2))
                throw new ArgumentException("Model column: finite N, M1 and M2 of the action.");
            Name = name ?? ""; AxialForce = axialForce; Moment1 = moment1; Moment2 = moment2;
        }
    }

    /// <summary>Outcome of an action of the envelope.</summary>
    public enum ModelColumnActionStatus
    {
        /// <summary>M0Ed,tot ≤ M1Rd,max.</summary>
        Verified,
        /// <summary>M0Ed,tot > M1Rd,max: no equilibrium (instability).</summary>
        NotVerified,
        /// <summary>Tensile N: no second-order effect, the model column does not apply (the section is checked by the failure domain).</summary>
        Tension,
        /// <summary>Rejected by the model column (<see cref="ModelColumnActionResult.Rejection"/>); with |N| beyond NRd,c the action is not verified.</summary>
        Rejected
    }

    /// <summary>
    /// Result of an action of the envelope: the moment in the direction θ of the template M = M1 cos θ + M2 sin θ and the transverse moment
    /// M⊥ = −M1 sin θ + M2 cos θ (Nmm); the direction of the column, θ for M ≥ 0 and θ + 180° otherwise, with M0Ed = |M|; whether M⊥ is beyond the
    /// transverse limit of the template times |M| (not checked: EN 1992-1-1 5.8.9); the status, the result of the column (null for tension and
    /// rejections), the rejection with its message and value, the ratio M0Ed,tot/M1Rd,max (+∞ with |N| beyond NRd,c) and the outcome.
    /// </summary>
    public sealed class ModelColumnActionResult
    {
        public int Index { get; internal set; }
        public ModelColumnAction Action { get; internal set; }
        public double Moment { get; internal set; }
        public double TransverseMoment { get; internal set; }
        public bool TransverseNeglected { get; internal set; }
        public double DirectionDegrees { get; internal set; }
        public ModelColumnActionStatus Status { get; internal set; }
        public ModelColumnResult Result { get; internal set; }
        public ModelColumnRejection? Rejection { get; internal set; }
        public string RejectionMessage { get; internal set; }
        public double RejectionValue { get; internal set; } = double.NaN;
        public double? Ratio { get; internal set; }
        public bool? Verified { get; internal set; }
    }

    /// <summary>Warnings of an envelope.</summary>
    public enum ModelColumnEnvelopeWarning
    {
        /// <summary>Some action has a first-order moment normal to θ beyond the transverse limit: it is not checked (biaxial bending, EN 1992-1-1 5.8.9).</summary>
        TransverseFirstOrderMoment,
        /// <summary>No action in compression: nothing to check.</summary>
        NoCompressedAction,
        /// <summary>Some action rejected by the model column for a reason other than |N| beyond NRd,c: the outcome is not complete.</summary>
        RejectedActions
    }

    /// <summary>
    /// Envelope of the model column (0.28.0.0): every action with its own constant N, the outcome (false when an action is not verified or beyond
    /// NRd,c, null when an action is rejected for another reason or none is in compression), the governing action (largest ratio, +∞ beyond NRd,c)
    /// and the governing one among the calculated actions (with a <see cref="ModelColumnActionResult.Result"/>), indices in <see cref="Actions"/>;
    /// the reduced domains of the directions used (θ, and θ + 180° with negative moments), the distinct curves calculated.
    /// </summary>
    public sealed class ModelColumnEnvelopeResult
    {
        public double DirectionDegrees { get; internal set; }
        public IReadOnlyList<ModelColumnActionResult> Actions { get; internal set; }
        public int? Governing { get; internal set; }
        public int? GoverningCalculated { get; internal set; }
        public bool? Verified { get; internal set; }
        public IReadOnlyList<ModelColumnDomain> Domains { get; internal set; }
        public int Curves { get; internal set; }
        public IReadOnlyList<ModelColumnEnvelopeWarning> Warnings { get; internal set; }
        public string Status { get; internal set; }
    }

    /// <summary>
    /// The model column on all the actions of a section (0.28.0.0, method page ca.colonna-modello §6.9): the template gives the direction θ, l0, l,
    /// c, imperfection, minimum eccentricity, creep (with (5.19) φef of each action from its M0Ed and N), rm, steps and the limits; its N and M0Ed
    /// are not used. Each action in compression is a model column at its own constant N with M0Ed = |M1 cos θ + M2 sin θ| in the direction θ or
    /// θ + 180°; actions with the same N, direction and φef share one curve at imposed curvature (cache), and each result is the one of
    /// <see cref="ModelColumnAnalysis.Calculate(ModelColumnRequest, SectionCheckerModelCode2010, ReinforcedConcreteSection, CoordinateSystem, CancellationToken)"/>
    /// on its request, bit for bit. Then the reduced domain of each direction used, at the φef of the governing action of the direction, with
    /// the N of its actions of the same φef.
    /// </summary>
    public static class ModelColumnEnvelope
    {
        /// <summary>
        /// The envelope on the actions. Rejections common to every action (standard, checker, section, intervals) are thrown as
        /// <see cref="ModelColumnException"/>; the others are in the action. Progress: <see cref="ModelColumnStage.Actions"/>, then
        /// <see cref="ModelColumnStage.Domain"/> for each domain.
        /// </summary>
        public static ModelColumnEnvelopeResult Calculate(ModelColumnRequest template, IReadOnlyList<ModelColumnAction> actions, SectionCheckerModelCode2010 checker,
            ReinforcedConcreteSection section, CoordinateSystem axes, int domainIntervals = 40, IProgress<ModelColumnProgress> progress = null,
            CancellationToken token = default(CancellationToken))
        {
            if (template == null || actions == null || checker == null || section == null || axes == null || actions.Any(a => a == null))
                throw new ArgumentNullException();
            ModelColumnAnalysis.ValidateIntervals(domainIntervals, template.AxialForce);
            ModelColumnAnalysis.Resolve(template.AxialForce, checker, section);
            token.ThrowIfCancellationRequested();
            double theta = template.DirectionDegrees, angle = theta * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
            var cache = new ModelColumnCurveCache();
            var results = new List<ModelColumnActionResult>();
            for (int i = 0; i < actions.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var a = actions[i];
                double m = a.Moment1 * cos + a.Moment2 * sin, t = -a.Moment1 * sin + a.Moment2 * cos;
                var r = new ModelColumnActionResult
                {
                    Index = i, Action = a, Moment = m, TransverseMoment = t, DirectionDegrees = m >= 0 ? theta : theta + 180,
                    TransverseNeglected = t != 0 && Math.Abs(t) > template.TransverseMomentLimit * Math.Abs(m)
                };
                if (a.AxialForce > 0) r.Status = ModelColumnActionStatus.Tension;
                else
                {
                    try
                    {
                        r.Result = ModelColumnAnalysis.Calculate(With(template, a.AxialForce, r.DirectionDegrees, Math.Abs(m)), checker, section, axes, token, false, cache);
                        r.Ratio = r.Result.Equilibrium.Ratio; r.Verified = r.Result.Verified;
                        r.Status = r.Verified == true ? ModelColumnActionStatus.Verified : ModelColumnActionStatus.NotVerified;
                    }
                    catch (ModelColumnException rejection)
                    {
                        r.Status = ModelColumnActionStatus.Rejected; r.Rejection = rejection.Reason; r.RejectionMessage = rejection.Message; r.RejectionValue = rejection.Value;
                        if (rejection.Reason == ModelColumnRejection.AxialForceBeyondResistance) { r.Ratio = double.PositiveInfinity; r.Verified = false; }
                    }
                }
                results.Add(r);
                progress?.Report(new ModelColumnProgress(ModelColumnStage.Actions, i + 1, actions.Count));
            }

            int? Worst(IEnumerable<ModelColumnActionResult> rows)
            {
                ModelColumnActionResult worst = null;
                foreach (var r in rows) if (r.Ratio.HasValue && (worst == null || r.Ratio.Value > worst.Ratio.Value)) worst = r;
                return worst?.Index;
            }
            var envelope = new ModelColumnEnvelopeResult
            {
                DirectionDegrees = theta, Actions = results.AsReadOnly(), Governing = Worst(results), GoverningCalculated = Worst(results.Where(r => r.Result != null))
            };
            var warnings = new List<ModelColumnEnvelopeWarning>();
            var applicable = results.Where(r => r.Status != ModelColumnActionStatus.Tension).ToList();
            if (applicable.Any(r => r.TransverseNeglected)) warnings.Add(ModelColumnEnvelopeWarning.TransverseFirstOrderMoment);
            if (applicable.Count == 0) warnings.Add(ModelColumnEnvelopeWarning.NoCompressedAction);
            bool rejected = applicable.Any(r => r.Status == ModelColumnActionStatus.Rejected && r.Rejection != ModelColumnRejection.AxialForceBeyondResistance);
            if (rejected) warnings.Add(ModelColumnEnvelopeWarning.RejectedActions);
            envelope.Verified = applicable.Any(r => r.Verified == false) ? false : rejected || applicable.Count == 0 ? (bool?)null : true;

            // ---- reduced domains: θ, and θ + 180° when some action has a negative moment
            var domains = new List<ModelColumnDomain>();
            var directions = applicable.Select(r => r.DirectionDegrees).Distinct().OrderBy(d => d).ToList();
            if (directions.Count == 0) directions.Add(theta);
            foreach (double direction in directions)
            {
                token.ThrowIfCancellationRequested();
                var calculated = results.Where(r => r.Result != null && r.DirectionDegrees == direction).ToList();
                ModelColumnResult reference = null;
                if (calculated.Count > 0) reference = results[Worst(calculated).Value].Result;
                else if (template.Creep.Kind != ModelColumnCreepKind.FinalCoefficient)
                    try { reference = ModelColumnAnalysis.Calculate(With(template, 0, direction, null), checker, section, axes, token, false, cache); }
                    catch (ModelColumnException) { }
                if (reference == null) continue;
                var extra = calculated.Where(r => r.Result.Creep.EffectiveRatio == reference.Creep.EffectiveRatio).Select(r => r.Action.AxialForce);
                domains.Add(ModelColumnAnalysis.Domain(With(template, reference.AxialForce, direction, null), reference, extra, checker, section, axes, domainIntervals,
                    cache, progress, token));
            }
            envelope.Domains = domains.AsReadOnly();
            envelope.Curves = cache.Count;
            envelope.Warnings = warnings.AsReadOnly();
            int Count(ModelColumnActionStatus status) => results.Count(r => r.Status == status);
            envelope.Status = "Model column envelope (EN 1992-1-1 5.8.6(6), 5.8.8.2), theta = " + theta.ToString("G6", CultureInfo.InvariantCulture) + " deg: "
                + results.Count + " actions, " + Count(ModelColumnActionStatus.Verified) + " verified, " + Count(ModelColumnActionStatus.NotVerified) + " not verified, "
                + Count(ModelColumnActionStatus.Tension) + " in tension, " + Count(ModelColumnActionStatus.Rejected) + " rejected"
                + (envelope.Governing.HasValue ? "; governing " + results[envelope.Governing.Value].Action.Name + ", ratio "
                    + results[envelope.Governing.Value].Ratio.Value.ToString("G4", CultureInfo.InvariantCulture) : "")
                + "; " + envelope.Curves + " curves.";
            return envelope;
        }

        /// <summary>The request of an action: the template with N, the direction and M0Ed (null for capacity only).</summary>
        private static ModelColumnRequest With(ModelColumnRequest t, double axialForce, double direction, double? moment)
            => new ModelColumnRequest(axialForce, direction, t.EffectiveLength, t.MemberLength, moment, t.CurvatureFactor, t.Imperfection, t.MinimumEccentricity,
                t.Creep, t.EndMomentRatio, t.Steps, t.AxialTolerance, t.TransverseMomentLimit, t.RejectTransverseMoment);
    }
}
