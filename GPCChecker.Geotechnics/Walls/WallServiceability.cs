using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>
    /// Settlement of the base of the wall: layers below the foundation with Eoed (Model profile through <see cref="SettlementLayer.FromProfile"/>
    /// or assigned), pressure removed by the excavation (MPa), limits of the settlement (mm) and of the rotation (rad), rigid substratum at the bottom
    /// of the layers (no check of the residual stress).
    /// </summary>
    public sealed class WallSettlementOptions
    {
        public IReadOnlyList<SettlementLayer> Layers { get; }
        public double RemovedPressure { get; }
        public double SettlementLimit { get; }
        public double RotationLimit { get; }
        public bool RigidBase { get; }
        public WallSettlementOptions(IEnumerable<SettlementLayer> layers, double removedPressure, double settlementLimit = 25, double rotationLimit = .002, bool rigidBase = false)
        {
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray();
            if (!(removedPressure >= 0 && removedPressure <= 5)) throw new ArgumentException("removed_pressure: inserire un valore finito fra 0 e 5000 kPa.");
            if (!(settlementLimit >= .01 && settlementLimit <= 1000)) throw new ArgumentException("settlement_limit: inserire un valore finito fra 0.01 e 1000 mm.");
            if (!(rotationLimit >= 1e-6 && rotationLimit <= .1)) throw new ArgumentException("rotation_limit: inserire un valore finito fra 1E-06 e 0.1.");
            RemovedPressure = removedPressure; SettlementLimit = settlementLimit; RotationLimit = rotationLimit; RigidBase = rigidBase;
        }
    }

    /// <summary>
    /// Serviceability of a combination: settlements at the toe, at the centre and at the heel (mm), rotation of the foundation (rad, positive
    /// towards the fill), elastic displacement of the stem fixed at the base and decoupled estimate of the head displacement (mm), slices of the
    /// settlement at the centre, deformed stem and the message of what could not be calculated.
    /// </summary>
    public sealed class WallServiceCase
    {
        public string Combination { get; internal set; } = "";
        public double? ToeSettlement { get; internal set; }
        public double? CentreSettlement { get; internal set; }
        public double? HeelSettlement { get; internal set; }
        public double? Rotation { get; internal set; }
        public double? StemDisplacement { get; internal set; }
        public double? HeadDisplacement { get; internal set; }
        public string Status { get; internal set; } = "";
        public IReadOnlyList<SettlementSlice> Slices { get; internal set; } = new SettlementSlice[0];
        public IReadOnlyList<WallDisplacementPoint> Shape { get; internal set; } = new WallDisplacementPoint[0];
    }

    /// <summary>
    /// Accelerogram for the permanent sliding of the wall (Newmark): name, limit state (SLD or SLV), confirmation of its compatibility with the site,
    /// yield acceleration (g), scale, limit of the displacement (mm) and the samples.
    /// </summary>
    public sealed class WallAccelerogram
    {
        public string Name { get; }
        public string State { get; }
        public bool Compatible { get; }
        public double YieldAcceleration { get; }
        public double Scale { get; }
        public double Limit { get; }
        public IReadOnlyList<AccelerogramSample> Samples { get; }
        public WallAccelerogram(string name, string state, bool compatible, double yieldAcceleration, double scale, double limit, IEnumerable<AccelerogramSample> samples)
        {
            Name = name ?? ""; State = state ?? ""; Compatible = compatible; YieldAcceleration = yieldAcceleration; Scale = scale; Limit = limit;
            Samples = (samples ?? Enumerable.Empty<AccelerogramSample>()).ToArray();
        }
    }

    /// <summary>Permanent sliding of an accelerogram: the record, the history (null when not calculated) and the outcome.</summary>
    public sealed class WallSlidingCase
    {
        public WallAccelerogram Record { get; internal set; } = null!;
        public SlidingResult? History { get; internal set; }
        public WallCheck Check { get; internal set; } = null!;
    }

    /// <summary>Options of the serviceability: settlements (null = not calculated), limit of the head displacement (mm) and horizontal stiffness of the foundation (MPa = N/mm per mm) for the displacements (null = not calculated), accelerograms.</summary>
    public sealed class WallServiceOptions
    {
        public WallSettlementOptions? Settlement { get; }
        public (double HeadLimit, double HorizontalStiffness)? Displacement { get; }
        public IReadOnlyList<WallAccelerogram> Accelerograms { get; }
        public WallServiceOptions(WallSettlementOptions? settlement = null, (double HeadLimit, double HorizontalStiffness)? displacement = null, IEnumerable<WallAccelerogram>? accelerograms = null)
        {
            Settlement = settlement; Displacement = displacement; Accelerograms = (accelerograms ?? Enumerable.Empty<WallAccelerogram>()).ToArray();
        }
    }

    /// <summary>Serviceability results: one case per SLS combination (when settlements or displacements are requested), sliding of every accelerogram and the checks.</summary>
    public sealed class WallServiceResult
    {
        public IReadOnlyList<WallServiceCase> Cases { get; internal set; } = null!;
        public IReadOnlyList<WallSlidingCase> Sliding { get; internal set; } = null!;
        public IReadOnlyList<WallCheck> Checks { get; internal set; } = null!;
    }

    /// <summary>
    /// Serviceability of the wall (ANTHEA RetainingWall.CalculateService, commit fe4652c): oedometric settlements under the contact of the base of
    /// every SLS combination with the convergence and the depth of the profile checked, rotation from the differential settlements, elastic
    /// displacement of the stem from the curvatures (cantilever fixed at the base) and decoupled head displacement u = u_stem + H/k − θ H,
    /// permanent sliding of Newmark. Units mm, MPa, N/mm, rad.
    /// </summary>
    public static class WallServiceability
    {
        /// <summary>
        /// Serviceability of the SLS combinations of the result. Settlements at the toe, at the centre and at the heel with 40 subdivisions, the centre
        /// again with 80 (convergence within max(0.01 mm, 1%)); the residual stress at the bottom must be at most 10% of the net peak pressure (or a
        /// rigid base); checks of the maximum settlement and of the rotation, without a ratio when the calculation is incomplete and satisfies the limit.
        /// Displacements from the curvatures of each combination (heights above the top of the slab; null = not available, for example a gravity wall
        /// with assigned strengths), one per stem cut below the top.
        /// </summary>
        public static WallServiceResult Calculate(WallInput input, WallResult result, WallServiceOptions options, Func<WallCaseResult, IReadOnlyList<WallCurvaturePoint>?>? curvatures = null,
            CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var checks = new List<WallCheck>(); var cases = new List<WallServiceCase>(); double width = result.Width, height = input.Geometry.Height;
            foreach (var c in result.Cases.Where(c => c.Combination.Service))
            {
                token.ThrowIfCancellationRequested();
                var s = new WallServiceCase { Combination = c.Combination.Name }; var messages = new List<string>();
                if (options.Settlement != null)
                    try
                    {
                        var o = options.Settlement;
                        if (!c.Contact.Valid) throw new ArgumentException("Contatto fondazione non disponibile.");
                        SettlementResult At(double x, int steps = 40) => FoundationSettlement.Calculate(o.Layers, x, c.Contact.Start, c.Contact.End, c.Contact.Toe, c.Contact.Heel, o.RemovedPressure, width, steps);
                        var low = At(0); var mid = At(width / 2); var high = At(width); var fine = At(width / 2, 80);
                        s.ToeSettlement = low.Settlement; s.CentreSettlement = mid.Settlement; s.HeelSettlement = high.Settlement;
                        s.Rotation = (high.Settlement - low.Settlement) / width; s.Slices = mid.Slices;
                        bool converged = Math.Abs(fine.Settlement - mid.Settlement) <= Math.Max(.01, .01 * Math.Abs(fine.Settlement));
                        bool deep = o.RigidBase || Math.Max(low.BottomStress, Math.Max(mid.BottomStress, high.BottomStress)) <= .1 * Math.Max(1e-12, c.Contact.Peak - o.RemovedPressure);
                        string incomplete = !converged ? "Ricerca incompleta: integrare più finemente gli strati"
                            : !deep ? "Profilo insufficiente: tensione residua al fondo >10% del carico netto; approfondire o documentare il substrato rigido" : "";
                        double maximum = Math.Max(low.Settlement, Math.Max(mid.Settlement, high.Settlement));
                        var settlement = WallCheck.Of(WallCheckKind.Settlement, c.Combination.Name, maximum, o.SettlementLimit);
                        checks.Add(incomplete == "" || settlement.Ratio > 1 ? settlement : settlement.With(null, WallCheckStatus.Unavailable, incomplete));
                        var rotation = WallCheck.Of(WallCheckKind.Rotation, c.Combination.Name, Math.Abs(s.Rotation.Value), o.RotationLimit);
                        checks.Add(incomplete == "" || rotation.Ratio > 1 ? rotation : rotation.With(null, WallCheckStatus.Unavailable, incomplete));
                        if (incomplete != "") messages.Add(incomplete);
                    }
                    catch (ArgumentException ex) { checks.Add(WallCheck.Of(WallCheckKind.Settlement, c.Combination.Name, 0, null, ex.Message)); messages.Add(ex.Message); }
                if (options.Displacement.HasValue)
                    try
                    {
                        var (headLimit, stiffness) = options.Displacement.Value;
                        var points = curvatures?.Invoke(c) ?? throw new ArgumentException("Selezionare il materiale della gravità e il modulo elastico per gli spostamenti.");
                        int required = c.Sections.Count(f => f.Member == WallMember.Stem && (f.Position > 0 || f.N != 0 || f.V != 0 || f.M != 0));
                        if (points.Count != required) throw new ArgumentException("Curvature mancanti in una o più sezioni: spostamento non disponibile.");
                        s.Shape = WallDisplacementPoint.Integrate(points, height); s.StemDisplacement = s.Shape[s.Shape.Count - 1].Displacement;
                        if (!(headLimit >= .01 && headLimit <= 1000)) throw new ArgumentException("head_limit: inserire un valore finito fra 0.01 e 1000.");
                        checks.Add(WallCheck.Of(WallCheckKind.StemDisplacement, c.Combination.Name, Math.Abs(s.StemDisplacement.Value), headLimit));
                        if (!(stiffness >= 1e-5 && stiffness <= 1e7)) throw new ArgumentException("horizontal_stiffness: inserire un valore finito fra 0.01 e 10000000000.");
                        if (s.Rotation is null || messages.Count > 0) throw new ArgumentException("Spostamento totale: completare il calcolo convergente dei cedimenti/rotazione.");
                        s.HeadDisplacement = s.StemDisplacement + c.Horizontal / stiffness - s.Rotation * height;
                        checks.Add(WallCheck.Of(WallCheckKind.HeadDisplacement, c.Combination.Name, Math.Abs(s.HeadDisplacement.Value), headLimit));
                    }
                    catch (ArgumentException ex) { checks.Add(WallCheck.Of(WallCheckKind.HeadDisplacement, c.Combination.Name, 0, null, ex.Message)); messages.Add(ex.Message); }
                if (options.Settlement != null || options.Displacement.HasValue) { s.Status = messages.Count == 0 ? "Calcolato" : string.Join("; ", messages); cases.Add(s); }
            }
            var sliding = new List<WallSlidingCase>();
            foreach (var record in options.Accelerograms)
            {
                token.ThrowIfCancellationRequested();
                var r = Sliding(input, record); sliding.Add(r); checks.Add(r.Check);
            }
            return new WallServiceResult { Cases = cases, Sliding = sliding, Checks = checks };
        }

        /// <summary>
        /// Permanent sliding of the wall under an accelerogram (Newmark rigid block, <see cref="NewmarkSliding"/>): the record must be confirmed
        /// compatible with the site at the SLD or SLV, and the wall must be free to slide (not with the Wood method).
        /// </summary>
        public static WallSlidingCase Sliding(WallInput input, WallAccelerogram record)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (record == null) throw new ArgumentNullException(nameof(record));
            var result = new WallSlidingCase { Record = record };
            try
            {
                if ((record.State != "SLD" && record.State != "SLV") || !record.Compatible) throw new ArgumentException("Confermare compatibilità dell’accelerogramma con il sito e lo stato limite SLD/SLV.");
                if (input.Seismic?.Method == WallSeismicMethod.Wood) throw new ArgumentException("Newmark richiede un muro capace di scorrere; Wood descrive un muro vincolato.");
                if (!(record.YieldAcceleration >= .0001 && record.YieldAcceleration <= 2)) throw new ArgumentException("yield_g: inserire un valore finito fra 0.0001 e 2.");
                if (!(record.Scale >= .0001 && record.Scale <= 100)) throw new ArgumentException("scale: inserire un valore finito fra 0.0001 e 100.");
                if (!(record.Limit >= .01 && record.Limit <= 1000)) throw new ArgumentException("limit_mm: inserire un valore finito fra 0.01 e 1000.");
                result.History = NewmarkSliding.Calculate(record.Samples, record.YieldAcceleration, record.Scale);
                result.Check = WallCheck.Of(WallCheckKind.Newmark, record.Name, result.History.Displacement, record.Limit);
            }
            catch (ArgumentException ex) { result.Check = WallCheck.Of(WallCheckKind.Newmark, record.Name, 0, null, ex.Message); }
            return result;
        }
    }
}

