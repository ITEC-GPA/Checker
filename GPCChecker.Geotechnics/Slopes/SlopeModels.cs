using GPC.Model.Geotechnics;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Slopes
{
    /// <summary>Point of a slope section, mm: X horizontal, Y upwards. Everything is per unit length out of plane.</summary>
    public readonly struct SlopePoint : IEquatable<SlopePoint>
    {
        public double X { get; }
        public double Y { get; }
        public SlopePoint(double x, double y) { X = x; Y = y; }
        public bool Equals(SlopePoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is SlopePoint p && Equals(p);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 31 + Y.GetHashCode());
        public override string ToString() => "(" + X + "; " + Y + ")";
    }

    /// <summary>
    /// Layer of a soil column: the soil from the layer above (or the ground surface) down to <see cref="Bottom"/>, mm. The interfaces are
    /// horizontal. Characteristic parameters from Model (γ, γsat, φ', c', cu).
    /// </summary>
    public sealed class SlopeLayer
    {
        public Soil Soil { get; }
        public double Bottom { get; }
        public string Name => Soil.Name;
        public SlopeLayer(Soil soil, double bottom) { Soil = soil ?? throw new ArgumentNullException(nameof(soil)); Bottom = bottom; }
    }

    /// <summary>Rigid body inside the slope (for example a retaining wall): polygon, mm, and unit weight, N/mm³. It replaces the soil it occupies.</summary>
    public sealed class SlopeBody
    {
        public string Name { get; }
        public IReadOnlyList<SlopePoint> Polygon { get; }
        public double UnitWeight { get; }
        public SlopeBody(string name, IEnumerable<SlopePoint> polygon, double unitWeight)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name)); Polygon = (polygon ?? throw new ArgumentNullException(nameof(polygon))).ToArray(); UnitWeight = unitWeight;
        }
    }

    /// <summary>
    /// External load identified by <see cref="Id"/> (its partial factor comes from <see cref="SlopeFactors.Loads"/>). Concentrated: forces per
    /// unit length (N/mm, vertical downwards and horizontal towards +X positive) at abscissa <see cref="Left"/> and height <see cref="Y"/>, and a
    /// moment (N·mm/mm) positive when it increases the driving moment. Distributed: the same three quantities per unit length of the segment
    /// between <see cref="Left"/> and <see cref="Right"/> (pressures in MPa).
    /// </summary>
    public sealed class SlopeLoad
    {
        public string Id { get; }
        public double Left { get; }
        public double Right { get; }
        public double Y { get; }
        public double Vertical { get; }
        public double Horizontal { get; }
        public double Moment { get; }
        public bool Distributed { get; }
        public SlopeLoad(string id, double left, double right, double y, double vertical, double horizontal, double moment, bool distributed)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id)); Left = left; Right = right; Y = y; Vertical = vertical; Horizontal = horizontal; Moment = moment; Distributed = distributed;
        }
    }

    /// <summary>
    /// Slope section: ground surface (abscissae non decreasing, vertical steps allowed), soil columns with horizontal interfaces (a valley column
    /// left of <see cref="SoilSplitX"/> when given), hydrostatic water line, rigid bodies and loads. Every slip circle must exit left of
    /// <see cref="RequiredLeft"/> and enter right of <see cref="RequiredRight"/> (for example both sides of a wall). Units mm, N/mm³.
    /// </summary>
    public sealed class SlopeSection
    {
        public IReadOnlyList<SlopePoint> Surface { get; }
        public IReadOnlyList<SlopeLayer> Layers { get; }
        public IReadOnlyList<SlopePoint> Water { get; }
        public IReadOnlyList<SlopeBody> Bodies { get; }
        public IReadOnlyList<SlopeLoad> Loads { get; }
        public double RequiredLeft { get; }
        public double RequiredRight { get; }
        /// <summary>Layers of the valley column, used left of <see cref="SoilSplitX"/>; empty = one column.</summary>
        public IReadOnlyList<SlopeLayer> ValleyLayers { get; }
        public double SoilSplitX { get; }
        /// <summary>γw, N/mm³ (9.81 kN/m³ as the legacy calculation).</summary>
        public double WaterUnitWeight { get; }

        public SlopeSection(IEnumerable<SlopePoint> surface, IEnumerable<SlopeLayer> layers, IEnumerable<SlopePoint>? water, IEnumerable<SlopeBody>? bodies,
            IEnumerable<SlopeLoad>? loads, double requiredLeft, double requiredRight, IEnumerable<SlopeLayer>? valleyLayers = null, double soilSplitX = 0,
            double waterUnitWeight = SoilUnits.WaterUnitWeight)
        {
            Surface = (surface ?? throw new ArgumentNullException(nameof(surface))).ToArray();
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray();
            Water = (water ?? Enumerable.Empty<SlopePoint>()).ToArray(); Bodies = (bodies ?? Enumerable.Empty<SlopeBody>()).ToArray();
            Loads = (loads ?? Enumerable.Empty<SlopeLoad>()).ToArray(); RequiredLeft = requiredLeft; RequiredRight = requiredRight;
            ValleyLayers = (valleyLayers ?? Enumerable.Empty<SlopeLayer>()).ToArray(); SoilSplitX = soilSplitX; WaterUnitWeight = waterUnitWeight;
        }

        /// <summary>The column at the abscissa: the valley column left of the split, otherwise the main one.</summary>
        public IReadOnlyList<SlopeLayer> LayersAt(double x) => ValleyLayers.Count > 0 && x < SoilSplitX ? ValleyLayers : Layers;
        public IEnumerable<IReadOnlyList<SlopeLayer>> Columns => ValleyLayers.Count > 0 ? new[] { ValleyLayers, Layers } : new[] { Layers };
        /// <summary>Lowest elevation described by the columns, mm.</summary>
        public double CoveredBottom => Columns.Max(c => c[c.Count - 1].Bottom);
    }

    /// <summary>
    /// Partial factors of one combination: on the weight of the soil and of the bodies, on tan φ', c' and cu, γR on the resistance,
    /// seismic coefficients kh and kv (kv positive upwards: V = (1 − kv) W) and the factors of the loads by identifier (missing = 0).
    /// </summary>
    public sealed class SlopeFactors
    {
        public string Name { get; }
        public double SoilWeight { get; }
        public double BodyWeight { get; }
        public double TanFrictionAngle { get; }
        public double EffectiveCohesion { get; }
        public double UndrainedShearStrength { get; }
        public double ResistanceFactor { get; }
        public double Kh { get; }
        public double Kv { get; }
        /// <summary>Undrained analysis: φ = 0, c = cu,d, no pore pressure.</summary>
        public bool Undrained { get; }
        public IReadOnlyDictionary<string, double> Loads { get; }

        public SlopeFactors(string name, double soilWeight, double bodyWeight, double tanFrictionAngle, double effectiveCohesion, double undrainedShearStrength,
            double resistanceFactor, double kh, double kv, bool undrained, IReadOnlyDictionary<string, double>? loads = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name)); SoilWeight = soilWeight; BodyWeight = bodyWeight; TanFrictionAngle = tanFrictionAngle;
            EffectiveCohesion = effectiveCohesion; UndrainedShearStrength = undrainedShearStrength; ResistanceFactor = resistanceFactor; Kh = kh; Kv = kv;
            Undrained = undrained; Loads = (loads ?? new Dictionary<string, double>()).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }

        /// <summary>
        /// Factors of a combination of a Model standard (for example NTC 2018 A2+M2+R2 for <see cref="GeotechnicalCheck.SlopeStability"/>):
        /// soil weight γG1/γγ, body weight γG1, the M set and γR. The weight of the soil acts both ways in a slope, so the action set must
        /// have the same favourable and unfavourable γG1; otherwise the factors are given explicitly.
        /// </summary>
        public static SlopeFactors FromCombination(GeotechnicalCombination combination, double kh, double kv, bool undrained, IReadOnlyDictionary<string, double>? loads = null)
        {
            if (combination == null) throw new ArgumentNullException(nameof(combination));
            if (combination.Check != GeotechnicalCheck.SlopeStability && combination.Check != GeotechnicalCheck.GlobalStability)
                throw new ArgumentException("The combination is not a slope or global stability combination: " + combination.Check + ".", nameof(combination));
            var a = combination.Actions; var m = combination.Materials;
            if (a.PermanentUnfavourable != a.PermanentFavourable)
                throw new ArgumentException("Action set " + a.Name + ": γG1 differs between favourable and unfavourable weights; give the slope factors explicitly.", nameof(combination));
            return new SlopeFactors(combination.Name, a.PermanentUnfavourable / m.UnitWeight, a.PermanentUnfavourable, m.TanFrictionAngle, m.EffectiveCohesion,
                m.UndrainedShearStrength, combination.ResistanceFactor, kh, kv, undrained, loads);
        }
    }

    /// <summary>
    /// Search domain: exits (left end of the circle) and entries (right end) on the ground surface, depths of the lowest point of the circle
    /// below Y = 0 (mm), grid nodes per direction, slices and refinements.
    /// </summary>
    public sealed class SlopeSearch
    {
        public double ExitMin { get; }
        public double ExitMax { get; }
        public double EntryMin { get; }
        public double EntryMax { get; }
        public double DepthMin { get; }
        public double DepthMax { get; }
        public int Grid { get; }
        public int Slices { get; }
        public int Refinements { get; }
        public SlopeSearch(double exitMin, double exitMax, double entryMin, double entryMax, double depthMin, double depthMax, int grid, int slices, int refinements)
        {
            ExitMin = exitMin; ExitMax = exitMax; EntryMin = entryMin; EntryMax = entryMax; DepthMin = depthMin; DepthMax = depthMax; Grid = grid; Slices = slices; Refinements = refinements;
        }
    }

    /// <summary>Circle of centre (X, Y) and radius, mm; the slip surface is the lower arc between Left and Right.</summary>
    public sealed class SlipCircle
    {
        public double X { get; }
        public double Y { get; }
        public double Radius { get; }
        public double Left { get; }
        public double Right { get; }
        public SlipCircle(double x, double y, double radius, double left, double right) { X = x; Y = y; Radius = radius; Left = left; Right = right; }
        /// <summary>Elevation of the lower arc at x, mm.</summary>
        public double Base(double x) => Y - Math.Sqrt(Math.Max(0, Radius * Radius - (x - X) * (x - X)));
        /// <summary>Depth of the lowest point below Y = 0, mm.</summary>
        public double Depth => Radius - Y;
    }

    /// <summary>
    /// Slice of a slip circle. Lengths mm, forces per unit length N/mm, pressures and strengths MPa, angles rad. Weights and load resultants
    /// are factored; Vertical = (1 − kv)·W + loads, Driving = moment about the centre / R. NormalEffective, Resistance, Mobilized and
    /// MAlpha are filled by <see cref="BishopSolver"/>.
    /// </summary>
    public sealed class SlopeSlice
    {
        public int Index { get; }
        public double Left { get; }
        public double Right { get; }
        public double BaseY { get; }
        public double TopY { get; }
        public double Alpha { get; }
        public string Soil { get; }
        public double SoilWeight { get; }
        public double BodyWeight { get; }
        public double WeightX { get; }
        public double WeightY { get; }
        public double VerticalLoad { get; }
        public double HorizontalLoad { get; }
        public double PorePressure { get; }
        public double FrictionAngle { get; }
        public double Cohesion { get; }
        public double Vertical { get; }
        public double Driving { get; }
        public double NormalEffective { get; }
        public double Resistance { get; }
        public double Mobilized { get; }
        public double MAlpha { get; }
        public double Width => Right - Left;

        public SlopeSlice(int index, double left, double right, double baseY, double topY, double alpha, string soil, double soilWeight, double bodyWeight,
            double weightX, double weightY, double verticalLoad, double horizontalLoad, double porePressure, double frictionAngle, double cohesion, double vertical,
            double driving, double normalEffective = 0, double resistance = 0, double mobilized = 0, double mAlpha = 0)
        {
            Index = index; Left = left; Right = right; BaseY = baseY; TopY = topY; Alpha = alpha; Soil = soil; SoilWeight = soilWeight; BodyWeight = bodyWeight;
            WeightX = weightX; WeightY = weightY; VerticalLoad = verticalLoad; HorizontalLoad = horizontalLoad; PorePressure = porePressure; FrictionAngle = frictionAngle;
            Cohesion = cohesion; Vertical = vertical; Driving = driving; NormalEffective = normalEffective; Resistance = resistance; Mobilized = mobilized; MAlpha = mAlpha;
        }

        internal SlopeSlice Solved(double normal, double resistance, double mobilized, double mAlpha) => new SlopeSlice(Index, Left, Right, BaseY, TopY, Alpha, Soil,
            SoilWeight, BodyWeight, WeightX, WeightY, VerticalLoad, HorizontalLoad, PorePressure, FrictionAngle, Cohesion, Vertical, Driving, normal, resistance, mobilized, mAlpha);
        internal SlopeSlice WithDriving(double driving) => new SlopeSlice(Index, Left, Right, BaseY, TopY, Alpha, Soil, SoilWeight, BodyWeight, WeightX, WeightY,
            VerticalLoad, HorizontalLoad, PorePressure, FrictionAngle, Cohesion, Vertical, driving);
    }

    /// <summary>Bishop solution of one circle: factor F (resistance/driving), ratio η = γR/F, iterations, residual, sums (N/mm) and solved slices.</summary>
    public sealed class SlopeSurfaceResult
    {
        public SlipCircle Circle { get; }
        public double Factor { get; }
        public double Ratio { get; }
        public int Iterations { get; }
        public double Residual { get; }
        public double Driving { get; }
        public double Resistance { get; }
        public IReadOnlyList<SlopeSlice> Slices { get; }
        internal SlopeSurfaceResult(SlipCircle circle, double factor, double ratio, int iterations, double residual, double driving, double resistance, SlopeSlice[] slices)
        {
            Circle = circle; Factor = factor; Ratio = ratio; Iterations = iterations; Residual = residual; Driving = driving; Resistance = resistance; Slices = slices;
        }
    }

    /// <summary>Quality of the search of one combination (the verdict is <see cref="SlopeCaseResult.Ratio"/> ≤ 1).</summary>
    public enum SlopeSearchStatus
    {
        /// <summary>No circle solved: widen the domain or correct the model.</summary>
        NoSurface,
        /// <summary>Some circles had tension or no equilibrium: the search is incomplete.</summary>
        Incomplete,
        /// <summary>The minimum lies on the border of the search domain.</summary>
        BoundaryMinimum,
        /// <summary>
        /// With twice the slices the factor of the critical circle changes more than 2%, or the circle has no solution (tension at the base of a
        /// slice), as in the legacy code: <see cref="SlopeCaseResult.RefinedFactor"/> tells the two apart.
        /// </summary>
        NotConverged,
        /// <summary>Minimum inside the domain, every circle solved, discretisation converged.</summary>
        Converged
    }

    /// <summary>Result of one combination: critical circle, counters of the search and status.</summary>
    public sealed class SlopeCaseResult
    {
        public SlopeFactors Factors { get; }
        public SlopeSurfaceResult? Critical { get; }
        public int Tried { get; }
        public int GeometricallyValid { get; }
        public int Solved { get; }
        public int NumericalFailures { get; }
        public bool Boundary { get; }
        public SlopeSearchStatus Status { get; }
        /// <summary>F of the critical circle with twice the slices; null without a critical circle or when that solution does not exist.</summary>
        public double? RefinedFactor { get; }
        /// <summary>η = γR/F of the critical circle; null without a solved circle.</summary>
        public double? Ratio => Critical?.Ratio;
        /// <summary>The ratio is a verdict: η &gt; 1 (a circle already fails) or a converged search inside the domain without numerical failures.</summary>
        public bool IsConclusive => Critical != null && (Critical.Ratio > 1 || !Boundary && NumericalFailures == 0);
        internal SlopeCaseResult(SlopeFactors factors, SlopeSurfaceResult? critical, int tried, int valid, int solved, int failures, bool boundary, SlopeSearchStatus status,
            double? refinedFactor)
        {
            Factors = factors; Critical = critical; Tried = tried; GeometricallyValid = valid; Solved = solved; NumericalFailures = failures; Boundary = boundary; Status = status;
            RefinedFactor = refinedFactor;
        }
    }

    public sealed class SlopeResult
    {
        public SlopeSection Section { get; }
        public SlopeSearch Search { get; }
        public IReadOnlyList<SlopeCaseResult> Cases { get; }
        public IReadOnlyList<string> Notes { get; }
        internal SlopeResult(SlopeSection section, SlopeSearch search, SlopeCaseResult[] cases, string[] notes) { Section = section; Search = search; Cases = cases; Notes = notes; }
    }
}
