using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>Broms (concentrated closure in granular soils, distributed in cohesive ones) or the stratified extension with distributed reactions.</summary>
    public enum LateralPileMethod { Broms, Stratified }

    public enum LateralMechanism { Short, Intermediate, Long }

    /// <summary>Side of a point of a diagram at a depth: before or after a jump, or a node without jump.</summary>
    public enum DiagramSide { Before, After, Node }

    /// <summary>
    /// Single pile under a horizontal force at the head: diameter D, embedded length L and eccentricity e of the force above the ground (mm), head
    /// free or restrained against rotation (then e = 0), yield moment My of the section (N·mm) with its provenance (for example the RC domain of
    /// GPCChecker.Concrete at the axial force, or <see cref="MicropileTube.LateralResistance"/>), the design action HEd (N), the step of the
    /// diagrams (mm) and the tolerance of the roots (1e-12 … 1e-5).
    /// </summary>
    public sealed class LateralPile
    {
        public double Diameter { get; }
        public double Length { get; }
        public double Eccentricity { get; }
        public bool FixedHead { get; }
        public double ResistingMoment { get; }
        public string MomentSource { get; }
        public LateralPileMethod Method { get; }
        public double DesignAction { get; }
        public double Step { get; }
        public double Tolerance { get; }
        public LateralPile(double diameter, double length, double eccentricity, bool fixedHead, double resistingMoment, string momentSource, LateralPileMethod method,
            double designAction, double step = 100, double tolerance = 1e-8)
        {
            Diameter = diameter; Length = length; Eccentricity = eccentricity; FixedHead = fixedHead; ResistingMoment = resistingMoment; MomentSource = momentSource;
            Method = method; DesignAction = designAction; Step = step; Tolerance = tolerance;
        }
    }

    /// <summary>
    /// An investigated vertical: a Model <see cref="SoilProfile"/> (pile head at the ground surface, hydrostatic water table of the profile) and the
    /// behaviour of each of its layers. Cohesive layers use cu; granular ones φ', γ and γsat with c' = 0.
    /// </summary>
    public sealed class LateralPileSurvey
    {
        public SoilProfile Profile { get; }
        public IReadOnlyList<SoilBehaviour> Behaviours { get; }
        public LateralPileSurvey(SoilProfile profile, IEnumerable<SoilBehaviour> behaviours)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Behaviours = (behaviours ?? throw new ArgumentNullException(nameof(behaviours))).ToArray();
            if (Behaviours.Count != profile.Layers.Count) throw new ArgumentException("One behaviour per layer of the profile.");
        }
    }

    /// <summary>Segment of the limit reaction diagram p(z) = p0 + k (z − top), N/mm, inside one layer and on one side of the water table and of 1.5D.</summary>
    public sealed class LateralLimitSegment
    {
        public int Layer { get; }
        public SoilBehaviour Behaviour { get; }
        public double Top { get; }
        public double Bottom { get; }
        public double InitialReaction { get; }
        public double Slope { get; }
        /// <summary>σ'v at the top (MPa); null when the unit weights are not available (cohesive profile).</summary>
        public double? EffectiveStressAtTop { get; internal set; }
        public double Resultant => Force(Bottom);
        public double FirstMoment => First(Bottom);
        internal double SolverStress { get; }
        internal LateralLimitSegment(int layer, SoilBehaviour behaviour, double top, double bottom, double p0, double slope, double stress)
        { Layer = layer; Behaviour = behaviour; Top = top; Bottom = bottom; InitialReaction = p0; Slope = slope; SolverStress = stress; }
        internal double Force(double z) { double t = BearingCapacityFactors.Clamp(z - Top, 0, Bottom - Top); return InitialReaction * t + Slope * t * t / 2; }
        internal double First(double z) { double t = BearingCapacityFactors.Clamp(z - Top, 0, Bottom - Top); return Top * Force(z) + InitialReaction * t * t / 2 + Slope * t * t * t / 3; }
    }

    /// <summary>Point of the diagrams at the capacity: depth (mm), side, reaction p (N/mm), shear V (N), moment M (N·mm), equivalent pressure q = p/D (MPa).</summary>
    public sealed class LateralDiagramPoint
    {
        public double Depth { get; }
        public DiagramSide Side { get; }
        public double Reaction { get; }
        public double Shear { get; }
        public double Moment { get; }
        public double Pressure { get; }
        internal LateralDiagramPoint(double depth, DiagramSide side, double p, double v, double m, double pressure) { Depth = depth; Side = side; Reaction = p; Shear = v; Moment = m; Pressure = pressure; }
    }

    /// <summary>Soil diagnostics at a depth: layer, σv, u, σ'v (MPa; σv and σ'v null without the unit weights), qlim = plim/D (MPa), plim (N/mm), ∫p (N).</summary>
    public sealed class LateralGroundPoint
    {
        public double Depth { get; }
        public DiagramSide Side { get; }
        public int Layer { get; }
        public double? TotalStress { get; }
        public double PorePressure { get; }
        public double? EffectiveStress { get; }
        public double LimitPressure { get; }
        public double LimitReaction { get; }
        public double IntegratedReaction { get; }
        internal LateralGroundPoint(double depth, DiagramSide side, int layer, double? total, double u, double? effective, double qlim, double plim, double q)
        { Depth = depth; Side = side; Layer = layer; TotalStress = total; PorePressure = u; EffectiveStress = effective; LimitPressure = qlim; LimitReaction = plim; IntegratedReaction = q; }
    }

    /// <summary>A mechanism examined: capacity (N; null when it cannot develop in the length) and whether it governs.</summary>
    public sealed class LateralCandidate
    {
        public LateralMechanism Mechanism { get; }
        public double? Capacity { get; }
        public bool Governing { get; }
        internal LateralCandidate(LateralMechanism mechanism, double? capacity, bool governing) { Mechanism = mechanism; Capacity = capacity; Governing = governing; }
    }

    /// <summary>Capacity of the pile on one investigated vertical, with the limit diagram, the diagrams at the capacity and the equilibrium residuals.</summary>
    public sealed class LateralSurveyResult
    {
        public double Capacity { get; internal set; }
        public LateralMechanism Mechanism { get; internal set; }
        public bool Cohesive { get; internal set; }
        public bool Mixed { get; internal set; }
        public bool Uniform { get; internal set; }
        /// <summary>Distributed closure of the equilibrium (cohesive profiles and the stratified extension) or concentrated resultant (Broms in granular soils).</summary>
        public bool DistributedClosure { get; internal set; }
        public IReadOnlyList<LateralLimitSegment> LimitDiagram { get; internal set; } = new LateralLimitSegment[0];
        public IReadOnlyList<LateralCandidate> Candidates { get; internal set; } = new LateralCandidate[0];
        public double HeadMoment { get; internal set; }
        public double MaximumMoment { get; internal set; }
        public double MaximumMomentDepth { get; internal set; }
        public double ZeroShearDepth { get; internal set; }
        public IReadOnlyList<double> Hinges { get; internal set; } = new double[0];
        public double ReactionEnd { get; internal set; }
        public double ConcentratedResultant { get; internal set; }
        public double? ReversalDepth { get; internal set; }
        public double ResidualForce { get; internal set; }
        public double ResidualMoment { get; internal set; }
        public IReadOnlyList<LateralDiagramPoint> Diagram { get; internal set; } = new LateralDiagramPoint[0];
        public IReadOnlyList<LateralGroundPoint> Ground { get; internal set; } = new LateralGroundPoint[0];
        public bool StressesAvailable { get; internal set; }
        /// <summary>Model of the vertical as ANTHEA names it: "Omogeneo", "Multistrato sperimentale", "Diagramma stratificato · terreno omogeneo/multistrato".</summary>
        public string ModelName { get; internal set; } = "";
    }

    /// <summary>Group efficiency η of the transverse capacity: assigned (0 &lt; η ≤ 1) or Reese and Van Impe for eight interfering piles.</summary>
    public sealed class LateralGroupEfficiency
    {
        public bool ReeseVanImpe { get; }
        public double Eta { get; }
        public double Front { get; }
        public double Back { get; }
        public double Left { get; }
        public double Right { get; }
        public double FrontLeft { get; }
        public double FrontRight { get; }
        public double BackLeft { get; }
        public double BackRight { get; }
        private LateralGroupEfficiency(bool reese, double eta, double[] parts)
        {
            ReeseVanImpe = reese; Eta = eta;
            if (parts.Length == 8) { Front = parts[0]; Back = parts[1]; Left = parts[2]; Right = parts[3]; FrontLeft = parts[4]; FrontRight = parts[5]; BackLeft = parts[6]; BackRight = parts[7]; }
        }

        /// <summary>Assigned efficiency, 0 &lt; η ≤ 1.</summary>
        public static LateralGroupEfficiency Manual(double eta)
        {
            if (double.IsNaN(eta) || double.IsInfinity(eta) || eta <= 0 || eta > 1) throw new ArgumentException("Assigned efficiency: 0 < η ≤ 1.");
            return new LateralGroupEfficiency(false, eta, new double[0]);
        }

        /// <summary>
        /// Reese and Van Impe (SMath sheet): spacings in front, behind, left and right of the pile in the direction of H (mm, at least D):
        /// ηa = min(1; 0.7 (s/D)^0.26), ηb = min(1; 0.48 (s/D)^0.38), ηs = min(1; 0.64 (s/D)^0.34), diagonals by the elliptical rule; η = product of the eight.
        /// </summary>
        public static LateralGroupEfficiency ReeseVanImpeEfficiency(double diameter, double front, double back, double left, double right)
        {
            foreach (double s in new[] { front, back, left, right })
                if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0 || s < diameter) throw new ArgumentException("The spacings of the piles must be at least the diameter.");
            double a = Math.Min(1, .7 * Math.Pow(front / diameter, .26)), p = Math.Min(1, .48 * Math.Pow(back / diameter, .38)),
                sx = Math.Min(1, .64 * Math.Pow(left / diameter, .34)), dx = Math.Min(1, .64 * Math.Pow(right / diameter, .34));
            double Diagonal(double longitudinal, double transverse, double axialEta, double sideEta)
            {
                double angle = Math.Atan2(transverse, longitudinal);
                return Math.Sqrt(Math.Pow(axialEta * Math.Cos(angle), 2) + Math.Pow(sideEta * Math.Sin(angle), 2));
            }
            double als = Diagonal(front, left, a, sx), ald = Diagonal(front, right, a, dx), pls = Diagonal(back, left, p, sx), pld = Diagonal(back, right, p, dx);
            return new LateralGroupEfficiency(true, a * p * sx * dx * als * ald * pls * pld, new[] { a, p, sx, dx, als, ald, pls, pld });
        }
    }

    /// <summary>Transverse capacity of the pile: governing vertical, characteristic and design resistance with ξ3, ξ4, γR and η, and the verdict on HEd.</summary>
    public sealed class LateralPileResult
    {
        public LateralPile Pile { get; internal set; } = null!;
        public IReadOnlyList<LateralSurveyResult> Surveys { get; internal set; } = new LateralSurveyResult[0];
        /// <summary>Minimum capacity of the verticals Hu, N.</summary>
        public double Capacity { get; internal set; }
        /// <summary>Governing vertical, 1-based.</summary>
        public int GoverningSurvey { get; internal set; }
        public LateralMechanism Mechanism { get; internal set; }
        public double MeanCapacity { get; internal set; }
        public LateralPileFactors Factors { get; internal set; } = null!;
        public LateralGroupEfficiency Efficiency { get; internal set; } = null!;
        public double MeanBranch { get; internal set; }
        public double MinimumBranch { get; internal set; }
        /// <summary>The mean branch (mean/ξ3) governs; otherwise the minimum branch (min/ξ4).</summary>
        public bool MeanGoverns { get; internal set; }
        public double CharacteristicResistance { get; internal set; }
        public double DesignResistance { get; internal set; }
        /// <summary>HEd/Hu.</summary>
        public double MechanicalRatio { get; internal set; }
        /// <summary>HEd/Rd.</summary>
        public double Utilization { get; internal set; }
        public bool Satisfied { get; internal set; }
        public string ModelName { get; internal set; } = "";
        /// <summary>Stratified extension or multilayer Broms: experimental models of ANTHEA, not the original Broms formulas.</summary>
        public bool Experimental { get; internal set; }
        public string EngineVersion { get; internal set; } = "";
        public string Source { get; internal set; } = "";
        public IReadOnlyList<string> Warnings { get; internal set; } = new string[0];
    }
}
