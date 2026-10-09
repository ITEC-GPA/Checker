namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>Member of the wall where the internal forces are given: stem (position = depth below the top), toe and heel (position = distance from the free end).</summary>
    public enum WallMember { Stem, Toe, Heel }

    /// <summary>Internal forces per unit length at a cut: thickness (mm), N (N/mm, compression positive), M (N·mm/mm), V (N/mm).</summary>
    public sealed class WallSectionForce
    {
        public WallMember Member { get; }
        public double Position { get; }
        public double Thickness { get; }
        public double N { get; }
        public double M { get; }
        public double V { get; }
        internal WallSectionForce(WallMember member, double position, double thickness, double n, double m, double v) { Member = member; Position = position; Thickness = thickness; N = n; M = m; V = v; }
    }

    /// <summary>
    /// Earth pressure on a band of the back (depths below the top, mm): φ and design φ (rad), static and seismic coefficients, factored σ'v at the two
    /// ends, soil, surcharge, water and dynamic contributions and totals (MPa).
    /// </summary>
    public sealed class WallPressureDetail
    {
        public double Z0 { get; }
        public double Z1 { get; }
        public double FrictionAngle { get; }
        public double DesignFrictionAngle { get; }
        public double K { get; }
        public double Ke { get; }
        public double Sigma0 { get; }
        public double Sigma1 { get; }
        public double Soil0 { get; }
        public double Soil1 { get; }
        public double Surcharge { get; }
        public double Water0 { get; }
        public double Water1 { get; }
        public double Dynamic { get; }
        public double Total0 { get; }
        public double Total1 { get; }
        internal WallPressureDetail(double z0, double z1, double phi, double phiD, double k, double ke, double s0, double s1, double soil0, double soil1, double q, double w0, double w1, double dynamic, double t0, double t1)
        {
            Z0 = z0; Z1 = z1; FrictionAngle = phi; DesignFrictionAngle = phiD; K = k; Ke = ke; Sigma0 = s0; Sigma1 = s1; Soil0 = soil0; Soil1 = soil1; Surcharge = q; Water0 = w0; Water1 = w1;
            Dynamic = dynamic; Total0 = t0; Total1 = t1;
        }
    }

    /// <summary>An action with its factor in a combination.</summary>
    public sealed class WallAppliedAction
    {
        public WallAction Action { get; }
        public double Factor { get; }
        internal WallAppliedAction(WallAction action, double factor) { Action = action; Factor = factor; }
        public double Design => Action.Value * Factor;
    }

    /// <summary>
    /// Soil quantities of a combination: valley height and free height, weight of the valley soil and its moment, effective overburden q' at the
    /// base, design friction of the back and of the base and μ, friction on the equilibrium plane, passive available, used, mobilised fraction and
    /// equilibrium scale, bearing factors and inclination factors, rough base (δb ≥ φd/2).
    /// </summary>
    public sealed class WallSoilAudit
    {
        public double ValleyHeight { get; internal set; }
        public double FreeHeight { get; internal set; }
        public double ValleyWeight { get; internal set; }
        public double ValleyMoment { get; internal set; }
        public double Overburden { get; internal set; }
        public double WallFriction { get; internal set; }
        public double BaseFriction { get; internal set; }
        public double BaseFrictionCoefficient { get; internal set; }
        public double EquilibriumPlaneFriction { get; internal set; }
        public double PassiveAvailable { get; internal set; }
        public double PassiveUsed { get; internal set; }
        public double PassiveFraction { get; internal set; }
        public double PassiveScale { get; internal set; }
        public double Nq { get; internal set; }
        public double Ngamma { get; internal set; }
        public double Iq { get; internal set; }
        public double Igamma { get; internal set; }
        public bool RoughBase { get; internal set; }
    }

    /// <summary>
    /// Equilibrium of the wall in a combination (per unit length, N/mm and N·mm/mm about the toe at the base): horizontal and normal forces, uplift,
    /// stabilising and overturning moments, position x of the normal force, eccentricity, effective width, contact, design resistances to sliding,
    /// overturning and bearing (null out of the field), pressures on the equilibrium plane, on the stem and in front, details, internal forces.
    /// </summary>
    public sealed class WallCaseResult
    {
        public WallCombination Combination { get; internal set; } = null!;
        public double Horizontal { get; internal set; }
        public double Vertical { get; internal set; }
        public double Uplift { get; internal set; }
        public double Stabilizing { get; internal set; }
        public double Overturning { get; internal set; }
        public double X { get; internal set; }
        public double Eccentricity { get; internal set; }
        public double EffectiveWidth { get; internal set; }
        public WallContact Contact { get; internal set; } = null!;
        public double SlidingResistance { get; internal set; }
        public double OverturningResistance { get; internal set; }
        public double? BearingResistance { get; internal set; }
        public IReadOnlyList<WallPressureSegment> Pressures { get; internal set; } = null!;
        public IReadOnlyList<WallPressureSegment> StemPressures { get; internal set; } = null!;
        public IReadOnlyList<WallPressureSegment> ValleyPressures { get; internal set; } = null!;
        public IReadOnlyList<WallPressureDetail> PressureDetails { get; internal set; } = null!;
        public IReadOnlyList<WallPressureDetail> StemPressureDetails { get; internal set; } = null!;
        public IReadOnlyList<WallAppliedAction> Actions { get; internal set; } = null!;
        public IReadOnlyList<WallSectionForce> Sections { get; internal set; } = null!;
        public WallSoilAudit Soil { get; internal set; } = null!;
        /// <summary>Seismic bearing capacity (EN 1998-5 Annex F) of a seismic combination, null otherwise or out of the field (see <see cref="SeismicBearingError"/>).</summary>
        public Foundations.SeismicBearingResult? SeismicBearing { get; internal set; }
        public string SeismicBearingError { get; internal set; } = "";
    }

    /// <summary>
    /// Kind of a geotechnical check of the wall: equilibrium (sliding, overturning, bearing, contact), serviceability (final oedometric settlement,
    /// rotation from the differential settlements, elastic displacement of the stem, decoupled head displacement, Newmark permanent sliding) and
    /// global stability (Bishop).
    /// </summary>
    public enum WallCheckKind { Sliding, Overturning, Bearing, Contact, Settlement, Rotation, StemDisplacement, HeadDisplacement, Newmark, GlobalStability }

    /// <summary>Outcome of a check.</summary>
    public enum WallCheckStatus { Satisfied, NotSatisfied, ZeroResistance, Unavailable, ContactInCompression, LossOfEquilibrium }

    /// <summary>A check: demand and design resistance (N/mm, N·mm/mm or mm), ratio |demand|/resistance (null when unavailable), status and message.</summary>
    public sealed class WallCheck
    {
        public WallCheckKind Kind { get; }
        public string Combination { get; }
        public double Demand { get; }
        public double? Resistance { get; }
        public double? Ratio { get; }
        public WallCheckStatus Status { get; }
        public string Message { get; }
        internal WallCheck(WallCheckKind kind, string combination, double demand, double? resistance, double? ratio, WallCheckStatus status, string message)
        { Kind = kind; Combination = combination; Demand = demand; Resistance = resistance; Ratio = ratio; Status = status; Message = message; }

        /// <summary>
        /// The check of a demand against a resistance as ANTHEA: ratio |D|/R when R &gt; 0; R = 0 satisfied only with zero demand; null resistance
        /// unavailable with the message.
        /// </summary>
        public static WallCheck Of(WallCheckKind kind, string combination, double demand, double? resistance, string unavailable = "Non disponibile")
        {
            double? ratio = resistance > 0 ? Math.Abs(demand) / resistance : null;
            if (resistance == 0) return new WallCheck(kind, combination, Math.Abs(demand), 0, demand == 0 ? 0 : (double?)null, demand == 0 ? WallCheckStatus.Satisfied : WallCheckStatus.ZeroResistance,
                demand == 0 ? "Soddisfatta" : "Non soddisfatta: resistenza nulla");
            WallCheckStatus status = ratio is null ? WallCheckStatus.Unavailable : ratio <= 1 ? WallCheckStatus.Satisfied : WallCheckStatus.NotSatisfied;
            return new WallCheck(kind, combination, Math.Abs(demand), resistance, ratio, status, ratio is null ? unavailable : ratio <= 1 ? "Soddisfatta" : "Non soddisfatta");
        }

        internal WallCheck With(double? ratio, WallCheckStatus status, string message) => new WallCheck(Kind, Combination, Demand, Resistance, ratio, status, message);
    }

    /// <summary>Results of the wall: width, area of the section, the combinations and the geotechnical checks, warnings.</summary>
    public sealed class WallResult
    {
        public double Width { get; internal set; }
        public double Area { get; internal set; }
        public IReadOnlyList<WallCaseResult> Cases { get; internal set; } = null!;
        public IReadOnlyList<WallCheck> Checks { get; internal set; } = null!;
        public IReadOnlyList<string> Warnings { get; internal set; } = null!;
    }
}
