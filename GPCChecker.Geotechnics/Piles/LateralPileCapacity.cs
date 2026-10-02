using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>
    /// Transverse capacity of a single pile: Broms (Viggiani, Fondazioni, pp. 400-415, §§13.2.2-13.2.5) with the layered extension of G. Pacini
    /// (limit diagrams per layer, depth of zero shear, global equilibrium with distributed reactions; experimental model, not the original Broms
    /// formulas). Transferred from ANTHEA (Anthea.Calculations.PaloOrizzontale with .Stratified and .Diagnostics, commit fe4652c; fixtures
    /// piles-lateral.jsonl). Units: mm, N, N/mm (reactions per unit length), N·mm, MPa.
    /// </summary>
    public static class LateralPileCapacity
    {
        public const string Source = "Viggiani, Fondazioni, pp. 400–415 (PDF 205–212), §§13.2.2–13.2.5";
        public const string StratifiedSource = "Estensione stratificata di G. Pacini: diagrammi per strato e profondità a taglio nullo; equilibrio globale con reazioni distribuite. Modello sperimentale, non formula originale di Broms.";

        /// <summary>Passive pressure coefficient (1 + sin φ)/(1 − sin φ), φ in rad, 0 ≤ φ &lt; π/2.</summary>
        public static double PassivePressureCoefficient(double frictionAngle)
        {
            if (double.IsNaN(frictionAngle) || double.IsInfinity(frictionAngle) || frictionAngle < 0 || frictionAngle >= Math.PI / 2)
                throw new ArgumentException("Friction angle: a finite value between 0 included and 90° excluded is required.");
            double sine = Math.Sin(frictionAngle);
            return (1 + sine) / (1 - sine);
        }

        /// <summary>
        /// Capacity on every vertical, the governing one, the characteristic resistance min(mean/ξ3; min/ξ4) and the design resistance Rk/γR·η, with
        /// the verdict on HEd. N stays constant; H grows with the moment H·e.
        /// </summary>
        public static LateralPileResult Calculate(LateralPile pile, IReadOnlyList<LateralPileSurvey> surveys, LateralPileFactors factors, LateralGroupEfficiency efficiency)
        {
            if (pile == null) throw new ArgumentNullException(nameof(pile));
            if (surveys == null) throw new ArgumentNullException(nameof(surveys));
            if (factors == null) throw new ArgumentNullException(nameof(factors));
            if (efficiency == null) throw new ArgumentNullException(nameof(efficiency));
            double l = pile.Length, d = pile.Diameter, e = pile.Eccentricity;
            if (!Positive(l) || !Positive(d) || !Positive(pile.Step) || double.IsNaN(e) || double.IsInfinity(e) || e < 0 || double.IsNaN(pile.DesignAction) || double.IsInfinity(pile.DesignAction) || pile.DesignAction < 0)
                throw new ArgumentException("Lateral pile: positive diameter, length and step, non negative eccentricity and action.");
            if (double.IsNaN(pile.Tolerance) || pile.Tolerance < 1e-12 || pile.Tolerance > 1e-5 || l / pile.Step > 20000)
                throw new ArgumentException("Tolerance 1e-12…1e-5; at most 20000 intervals of the diagrams.");
            if (pile.FixedHead && e != 0) throw new ArgumentException("Restrained head: the model needs the restraint and the force at the ground surface (e = 0).");
            if (!Positive(pile.ResistingMoment)) throw new ArgumentException("Resisting moment: a positive finite value is required.");
            if (string.IsNullOrWhiteSpace(pile.MomentSource)) throw new ArgumentException("State the nature and provenance of the resisting moment.");
            if (surveys.Count == 0) throw new ArgumentException("At least one investigated vertical is required.");
            bool distributed = pile.Method == LateralPileMethod.Stratified;
            if (!distributed && pile.Method != LateralPileMethod.Broms) throw new ArgumentOutOfRangeException(nameof(pile));
            var results = new List<LateralSurveyResult>(); bool uniform = true;
            foreach (var survey in surveys)
            {
                if (survey == null) throw new ArgumentNullException(nameof(surveys));
                var ground = new Ground(survey, l, d, pile.Tolerance, distributed);
                uniform &= ground.Uniform;
                var r = Solve(ground, e, pile.ResistingMoment, pile.FixedHead, pile.Step);
                AddGroundDiagnostics(r, ground, d);
                r.ModelName = ModelName(ground.Uniform, distributed); results.Add(r);
            }
            double hu = results.Min(r => r.Capacity); int governing = results.FindIndex(r => r.Capacity == hu);
            double mean = results.Average(r => r.Capacity), meanBranch = mean / factors.Xi3, minBranch = hu / factors.Xi4;
            double rk = Math.Min(meanBranch, minBranch), rd = rk / factors.ResistanceFactor * efficiency.Eta;
            var warnings = new List<string>
            {
                "Palo singolo, capacità ultima al primo ordine; spostamenti, gruppo, ciclicità, taglio, secondo ordine e duttilità delle cerniere non verificati.",
                "Resistenza ridotta con ξ3, ξ4 e γR. HEd deve essere un'azione di progetto. Le altre verifiche strutturali restano escluse: nessuna conformità complessiva automatica.",
                "I diagrammi si riferiscono alla capacità ultima, non all'azione inserita. N rimane costante; cresce H con momento H·e."
            };
            if (distributed) warnings.Add("MODELLO STRATIFICATO SPERIMENTALE: estensione anche per alternanze coesivo/granulare. Equilibrio globale con reazioni distribuite limitate da p_lim; nessuna validazione sperimentale indipendente. Nei granulari corto/intermedio non coincide con la chiusura concentrata di Broms.");
            else if (!uniform) warnings.Add("MULTISTRATO SPERIMENTALE: estensione integrale ANTHEA, non formula originale Broms né validazione indipendente per stratificazioni reali.");
            if (results.Any(r => !r.DistributedClosure)) warnings.Add("Terreno granulare: chiusura di equilibrio con risultante concentrata F indicata separatamente. Il completamento sotto la cerniera è idealizzato, non univoco.");
            if (efficiency.ReeseVanImpe) warnings.Add("Efficienza dal foglio SMath: schema di otto pali interferenti (quattro allineati e quattro diagonali). Verificare ulteriori interferenze nelle maglie fitte; lo schema non rappresenta una palificata arbitraria. Distanze riferite alla direzione di H.");
            return new LateralPileResult
            {
                Pile = pile, Surveys = results, Capacity = hu, GoverningSurvey = governing + 1, Mechanism = results[governing].Mechanism, MeanCapacity = mean, Factors = factors,
                Efficiency = efficiency, MeanBranch = meanBranch, MinimumBranch = minBranch, MeanGoverns = meanBranch <= minBranch, CharacteristicResistance = rk, DesignResistance = rd,
                MechanicalRatio = pile.DesignAction / hu, Utilization = pile.DesignAction / rd, Satisfied = pile.DesignAction <= rd, ModelName = ModelName(uniform, distributed),
                Experimental = distributed || !uniform, EngineVersion = distributed ? "Stratificato-ANTHEA-1" : "Broms-ANTHEA-1",
                Source = distributed ? StratifiedSource + " Leggi locali: " + Source : Source, Warnings = warnings
            };
        }

        private static string ModelName(bool uniform, bool distributed) => distributed
            ? "Diagramma stratificato · " + (uniform ? "terreno omogeneo" : "terreno multistrato")
            : uniform ? "Omogeneo" : "Multistrato sperimentale";

        private static LateralSurveyResult Solve(Ground soil, double e, double my, bool fixedHead, double step)
        {
            double l = soil.L, q = soil.Q(l), hShort, hIntermediate = double.PositiveInfinity;
            double Peak(double h) { double z = soil.Depth(h); return h * e + soil.S(z); }
            if (soil.Distributed)
            {
                double Balance(double h, double m0) { double z = soil.Depth(h); return m0 + h * e + soil.S(z) - soil.Tail(z, l).Couple; }
                hShort = fixedHead ? q : soil.Root(h => Balance(h, 0), 0, q);
                if (fixedHead && soil.S(l) > my) hIntermediate = soil.Root(h => Balance(h, -my), 0, q);
            }
            else
            {
                hShort = fixedHead ? q : soil.A(l) / (l + e);
                if (fixedHead && soil.S(l) > my) hIntermediate = (my + soil.A(l)) / l;
            }
            double target = fixedHead ? 2 * my : my;
            double hLong = Peak(q) > target ? soil.Root(h => Peak(h) - target, 0, q) : double.PositiveInfinity;
            double hu = Math.Min(hShort, Math.Min(hIntermediate, hLong));
            if (!(hu > 0) || double.IsInfinity(hu)) throw new ArgumentException("Zero or infinite capacity.");
            var mechanism = hu == hShort ? LateralMechanism.Short : hu == hIntermediate ? LateralMechanism.Intermediate : LateralMechanism.Long;
            double m0 = fixedHead ? mechanism == LateralMechanism.Short ? -soil.S(l) : -my : hu * e;
            double zMax = soil.Depth(hu), end = l, change = l, tip = 0;
            if (soil.Distributed)
            {
                if (mechanism != LateralMechanism.Short || !fixedHead)
                {
                    double peak = m0 + soil.S(zMax);
                    if (mechanism == LateralMechanism.Long) end = soil.Root(t => soil.Tail(zMax, t).Couple - peak, zMax, l);
                    change = soil.Tail(zMax, end).Switch;
                }
            }
            else
            {
                if (mechanism == LateralMechanism.Long) end = soil.Root(z => m0 + hu * z - soil.A(z), zMax, l);
                tip = soil.Q(end) - hu;
            }
            (double P, double V, double M) State(double z, bool after = false, bool before = false)
            {
                if (z > end || (z == end && after)) return (0, 0, 0);
                if (!soil.Distributed) return (soil.P(z, before), hu - soil.Q(z), m0 + hu * z - soil.A(z));
                double qr = z <= change ? soil.Q(z) : 2 * soil.Q(change) - soil.Q(z);
                double sr = z <= change ? soil.S(z) : 2 * soil.S(change) - soil.S(z);
                return ((z < change || before && z == change ? 1 : -1) * soil.P(z, before), hu - qr, m0 + hu * z - z * qr + sr);
            }
            var final = State(end); double residualForce = final.V + tip, residualMoment = final.M;
            double maxMoment = Math.Max(Math.Abs(m0), Math.Abs(State(zMax).M));
            double residualScale = Math.Max(1, Math.Max(my, q * l));
            if (Math.Abs(residualForce) > 1e-5 * Math.Max(1, q) || Math.Abs(residualMoment) > 1e-5 * residualScale || maxMoment > my * (1 + 1e-5))
                throw new ArgumentException("Equilibrium or moment limit not satisfied: inadmissible solution.");
            var depths = new SortedSet<double> { 0, l, zMax, end, change };
            foreach (var s in soil.Segments) { depths.Add(s.Top); depths.Add(s.Bottom); }
            for (int i = 1; i < Math.Ceiling(l / step); i++) depths.Add(i * step);
            var diagram = new List<LateralDiagramPoint>();
            foreach (double z in depths)
            {
                // Two sides only at actual jumps, not at each plotting sample.
                var before = State(z, before: true);
                var current = State(z, z == end);
                bool jump = z > 0 && (Math.Abs(before.P - current.P) > 1e-7 * Math.Max(1, Math.Abs(before.P)) || Math.Abs(before.V - current.V) > 1e-7 * Math.Max(1, q));
                if (jump) diagram.Add(new LateralDiagramPoint(z, DiagramSide.Before, before.P, before.V, before.M, before.P / soil.D));
                diagram.Add(new LateralDiagramPoint(z, jump ? DiagramSide.After : DiagramSide.Node, current.P, current.V, current.M, current.P / soil.D));
            }
            var candidates = new List<LateralCandidate>();
            void Candidate(LateralMechanism m, double value) => candidates.Add(new LateralCandidate(m, double.IsInfinity(value) ? (double?)null : value, m == mechanism));
            Candidate(LateralMechanism.Short, hShort); if (fixedHead) Candidate(LateralMechanism.Intermediate, hIntermediate); Candidate(LateralMechanism.Long, hLong);
            var hinges = new List<double>(); if (fixedHead && mechanism != LateralMechanism.Short) hinges.Add(0); if (mechanism == LateralMechanism.Long) hinges.Add(zMax);
            return new LateralSurveyResult
            {
                Capacity = hu, Mechanism = mechanism, Cohesive = soil.Clay, Mixed = soil.Mixed, Uniform = soil.Uniform, DistributedClosure = soil.Distributed, LimitDiagram = soil.Segments,
                Candidates = candidates, HeadMoment = m0, MaximumMoment = maxMoment, MaximumMomentDepth = Math.Abs(m0) >= Math.Abs(State(zMax).M) ? 0 : zMax, ZeroShearDepth = zMax,
                Hinges = hinges, ReactionEnd = end, ConcentratedResultant = tip, ReversalDepth = soil.Distributed ? change : (double?)null, ResidualForce = residualForce,
                ResidualMoment = residualMoment, Diagram = diagram
            };
        }

        // Diagnostics only: no local design factors and no change to the capacity. A purely cohesive profile does not need the unit weights.
        private static void AddGroundDiagnostics(LateralSurveyResult result, Ground ground, double d)
        {
            bool water = ground.Water; double zw = ground.WaterDepth, gw = ground.WaterUnitWeight;
            bool weightsAvailable = true;
            var intervals = new List<(double Top, double Bottom, double Gamma, double Sat)>();
            foreach (var (soil, top, end) in ground.Layers)
            {
                double gamma = soil.UnitWeight, sat = water && zw < end ? soil.SaturatedUnitWeight : gamma;
                weightsAvailable &= gamma > 0 && (!water || zw >= end || sat > gw);
                intervals.Add((top, end, gamma, sat));
            }
            (double Total, double U, double Effective) Stress(double z)
            {
                double total = 0;
                foreach (var a in intervals)
                {
                    double t = BearingCapacityFactors.Clamp(z - a.Top, 0, a.Bottom - a.Top);
                    double dry = water ? BearingCapacityFactors.Clamp(zw - a.Top, 0, t) : t;
                    total += a.Gamma * dry + a.Sat * (t - dry);
                }
                double u = water ? gw * Math.Max(0, z - zw) : 0;
                return (total, u, total - u);
            }
            var points = new List<LateralGroundPoint>();
            var nodes = new SortedSet<double>(ground.Segments.SelectMany(s => new[] { s.Top, s.Bottom }));
            foreach (var row in result.Diagram) nodes.Add(row.Depth);
            if (water && zw > 0 && zw < ground.L) nodes.Add(zw);
            foreach (double z in nodes)
                foreach (bool before in z == 0 ? new[] { false } : z == ground.L ? new[] { true } : new[] { true, false })
                {
                    var s = ground.Segments.First(v => before ? z > v.Top && z <= v.Bottom : z >= v.Top && z < v.Bottom);
                    var stress = Stress(z); double p = ground.P(z, before);
                    points.Add(new LateralGroundPoint(z, before ? DiagramSide.Before : DiagramSide.After, s.Layer, weightsAvailable ? stress.Total : (double?)null, stress.U,
                        weightsAvailable ? stress.Effective : (double?)null, p / d, p, ground.Q(z)));
                }
            foreach (var segment in ground.Segments) segment.EffectiveStressAtTop = weightsAvailable ? Stress(segment.Top).Effective : (double?)null;
            result.StressesAvailable = weightsAvailable; result.Ground = points;
        }

        private static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

        // p is a force per unit length (N/mm), not a pressure. Each segment belongs to one layer and lies entirely above or below the water table
        // and the depth 1.5D.
        private sealed class Ground
        {
            public List<LateralLimitSegment> Segments { get; } = new List<LateralLimitSegment>();
            public List<(Soil Soil, double Top, double End)> Layers { get; } = new List<(Soil, double, double)>();
            public bool Clay { get; }
            public bool Mixed { get; }
            public bool Distributed { get; }
            public bool Uniform { get; private set; } = true;
            public double L { get; }
            public double D { get; }
            public double Tolerance { get; }
            public bool Water { get; }
            public double WaterDepth { get; }
            public double WaterUnitWeight { get; }
            public double Q(double z) => Segments.Sum(s => s.Force(z));
            public double S(double z) => Segments.Sum(s => s.First(z));
            public double A(double z) => z * Q(z) - S(z);
            public double P(double z, bool before = false)
            {
                var s = Segments.FirstOrDefault(v => before ? z > v.Top && z <= v.Bottom : z >= v.Top && z < v.Bottom);
                return s == null ? 0 : s.InitialReaction + s.Slope * (z - s.Top);
            }

            public Ground(LateralPileSurvey survey, double l, double d, double tolerance, bool distributed)
            {
                var profile = survey.Profile;
                L = l; D = d; Tolerance = tolerance; WaterUnitWeight = profile.WaterUnitWeight;
                Water = profile.GroundwaterElevation.HasValue; double zw = Water ? profile.GroundSurface - profile.GroundwaterElevation!.Value : l;
                if (Water && zw < 0) throw new ArgumentException("Water table above the ground surface: not supported by the lateral model.");
                WaterDepth = zw;
                // Clip the actual layers, without moving the interfaces by 1.5D.
                var layers = new List<(Soil Soil, SoilBehaviour Kind, double Top, double End, int Index)>();
                double top = 0;
                for (int i = 0; i < profile.Layers.Count; i++)
                {
                    if (top >= l) break;
                    double bottom = profile.GroundSurface - profile.Layers[i].Bottom;
                    // Only absorb accumulated floating-point error, not a real gap.
                    if (Math.Abs(bottom - l) <= 16 * 2.2204460492503131e-16 * profile.Layers.Count * Math.Max(1, l)) bottom = l;
                    layers.Add((profile.Layers[i].Soil, survey.Behaviours[i], top, Math.Min(bottom, l), layers.Count + 1)); top = bottom;
                }
                if (top < l) throw new ArgumentException("The layers must cover the whole embedded length.");
                Clay = layers.All(s => s.Kind == SoilBehaviour.Cohesive);
                Mixed = !Clay && layers.Any(s => s.Kind == SoilBehaviour.Cohesive);
                if (Mixed && !distributed) throw new ArgumentException("Mixed sequences: select the stratified model with distributed reactions.");
                Distributed = distributed || Clay;
                double sigma = 0; (SoilBehaviour, double, double, double)? signature = null;
                foreach (var (soil, kind, a0, end, index) in layers)
                {
                    bool clay = kind == SoilBehaviour.Cohesive;
                    double cu = clay ? soil.UndrainedShearStrength ?? throw new ArgumentException("Layer " + index + " (" + soil.Name + "): cu is required for a cohesive layer.") : 0;
                    double phi = clay ? 0 : soil.FrictionAngle;
                    if (phi >= 60 * SoilUnits.Degree) throw new ArgumentException("Friction angle outside the accepted range [0, 60°); the limit is an input check, not a Broms threshold.");
                    if (!clay && soil.EffectiveCohesion != 0) throw new ArgumentException("c–φ soil not supported: c' must be zero.");
                    // Cohesive overburden also loads the granular layers below; in an entirely cohesive profile the weights do not enter.
                    double gamma = Clay ? 0 : soil.UnitWeight;
                    double saturated = !Clay && Water && zw < end ? soil.SaturatedUnitWeight : gamma;
                    if (!Clay && Water && zw < end && saturated <= WaterUnitWeight) throw new ArgumentException("γsat must be larger than γw.");
                    var sig = clay ? (kind, cu, 0.0, 0.0) : (kind, phi, gamma, saturated);
                    if (signature.HasValue && !signature.Value.Equals(sig)) Uniform = false;
                    if (!signature.HasValue) signature = sig;
                    Layers.Add((soil, a0, end));
                    var breaks = new SortedSet<double> { a0, end };
                    if (clay && 1.5 * d > a0 && 1.5 * d < end) breaks.Add(1.5 * d);
                    if (!Clay && Water && zw > a0 && zw < end) breaks.Add(zw);
                    var nodes = breaks.ToArray(); double kp = PassivePressureCoefficient(phi);
                    for (int i = 0; i < nodes.Length - 1; i++)
                    {
                        double a = nodes[i], b = nodes[i + 1], effective = Water && a >= zw ? saturated - (Clay ? 0 : WaterUnitWeight) : gamma;
                        double p = clay ? a < 1.5 * d ? 0 : 9 * cu * d : 3 * kp * d * sigma;
                        double slope = clay ? 0 : 3 * kp * d * effective;
                        Segments.Add(new LateralLimitSegment(index, kind, a, b, p, slope, sigma));
                        sigma += effective * (b - a);
                    }
                }
                if (!Clay && Water && zw > 0 && zw < l) Uniform = false;
                double total = Q(l), first = S(l);
                if (!(total > 0) || double.IsInfinity(total) || double.IsNaN(first) || double.IsInfinity(first))
                    throw new ArgumentException("No finite lateral resistance can be mobilised (cohesive soils need L > 1.5D).");
            }

            public double Root(Func<double, double> f, double lo, double hi)
            {
                double a = f(lo), b = f(hi);
                if (double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b) || Math.Sign(a) == Math.Sign(b) && a != 0 && b != 0)
                    throw new ArgumentException("Root not bracketed: inadmissible mechanism.");
                if (a == 0) return lo; if (b == 0) return hi;
                // Keep the equilibrium checks reliable even with a coarse requested tolerance.
                double epsilon = Math.Min(Tolerance, 1e-12) * Math.Max(1, hi - lo);
                for (int i = 0; i < 100; i++)
                {
                    double mid = lo + (hi - lo) / 2, c = f(mid);
                    if (double.IsNaN(c) || double.IsInfinity(c)) throw new ArgumentException("Non finite solution.");
                    if (c == 0 || hi - lo <= epsilon) return mid;
                    if (Math.Sign(c) == Math.Sign(a)) { lo = mid; a = c; } else hi = mid;
                }
                throw new ArgumentException("More than 100 iterations.");
            }

            // Depth where the resultant of the limit diagram reaches h (PileChecker FindDepthForForce), with zero-area intervals and the stable
            // quadratic inverse y = 2 area / (p0 + √(p0² + 2 k area)).
            public double Depth(double h)
            {
                double total = Q(L), cumulative = 0;
                if (double.IsNaN(h) || double.IsInfinity(h) || h < 0 || h > total * (1 + 1e-12)) throw new ArgumentException("Resultant outside the limit diagram.");
                if (h == 0) return 0;
                foreach (var s in Segments)
                {
                    double area = s.Force(s.Bottom);
                    if (area > 0 && h <= cumulative + area)
                    {
                        double remaining = Math.Max(0, h - cumulative);
                        double dz = s.Slope == 0 ? remaining / s.InitialReaction : 2 * remaining / (s.InitialReaction + Math.Sqrt(s.InitialReaction * s.InitialReaction + 2 * s.Slope * remaining));
                        return BearingCapacityFactors.Clamp(s.Top + dz, s.Top, s.Bottom);
                    }
                    cumulative += area;
                }
                return L; // Only end-point round-off remains after the range check.
            }

            public (double Couple, double Switch) Tail(double start, double end)
            {
                if (end <= start) return (0, start);
                double mid = BearingCapacityFactors.Clamp(Depth((Q(start) + Q(end)) / 2), start, end);
                return (S(end) + S(start) - 2 * S(mid), mid);
            }
        }
    }
}
