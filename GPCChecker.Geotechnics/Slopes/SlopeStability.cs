using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Slopes
{
    /// <summary>
    /// Slices of a circle and bounded, reproducible search of the critical circle with the simplified Bishop method. Transferred from ANTHEA
    /// (Anthea.Calculations.Geotechnics.SlopeStability, commit fe4652c; fixtures geotechnics-slope.csv). Units of Model: mm, N/mm, MPa, N/mm³, rad.
    /// </summary>
    public static class SlopeStability
    {
        private static readonly double[] GaussX = { -.8611363115940526, -.3399810435848563, .3399810435848563, .8611363115940526 };
        private static readonly double[] GaussW = { .3478548451374538, .6521451548625461, .6521451548625461, .3478548451374538 };

        /// <summary>Limits of the method, reported with every result.</summary>
        public static readonly IReadOnlyList<string> Notes = new[]
        {
            BishopSolver.Formula,
            "Circular surfaces towards the valley, lower arc with the centre between the ends, also with a vertical tangent at the entry, passing below every rigid body; horizontal layer interfaces. Grid search and tangent family, refinement from six distinct minima: the absolute minimum is not guaranteed. Widen the domain and compare search density and slices.",
            "Simplified Bishop: moment equilibrium and vertical equilibrium of the slices; tangential interslice forces neglected, horizontal equilibrium not imposed. Tension cracks, non-circular surfaces, hydrodynamic pressures and cyclic degradation are not modelled.",
            "A rigid body (for example a wall) is a mass of the slope: earth pressures and foundation reactions are internal and are not added. External loads have no added inertia."
        };

        /// <summary>
        /// Slices of a circle: weights integrated with four Gauss points per slice (soil above and below the water line, bodies replacing the soil
        /// they occupy), seismic forces kh·W and −kv·W, factored loads, hydrostatic pore pressure at the base (none in undrained analyses) and the
        /// design strength of the base layer.
        /// </summary>
        public static SlopeSlice[] Slices(SlopeSection section, SlipCircle circle, SlopeFactors factors, int count)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (circle == null) throw new ArgumentNullException(nameof(circle));
            if (factors == null) throw new ArgumentNullException(nameof(factors));
            var cuts = SlopeGeometry.Divisions(section, circle, count); var result = new List<SlopeSlice>();
            for (int i = 1; i < cuts.Length; i++)
            {
                double l = cuts[i - 1], r = cuts[i], x = (l + r) / 2, b = r - l, y = circle.Base(x), top = SlopeGeometry.Height(section.Surface, x);
                var layer = section.LayersAt(x).First(s => y >= s.Bottom - 1e-6);
                double soilW = 0, bodyW = 0, wx = 0, wy = 0;
                for (int j = 0; j < 4; j++)
                {
                    double gx = x + b * GaussX[j] / 2, scale = b * GaussW[j] / 2, bottom = circle.Base(gx), surface = SlopeGeometry.Height(section.Surface, gx);
                    double water = section.Water.Count == 0 ? double.NegativeInfinity : SlopeGeometry.Height(section.Water, gx);
                    var intervals = section.Bodies.Select(body => (Body: body, Span: SlopeGeometry.VerticalInterval(body.Polygon, gx))).Where(t => t.Span.HasValue).ToArray();
                    double upper = surface;
                    void Mass(double low, double high, double gamma, bool rigid)
                    {
                        if (high <= low) return;
                        double w = (high - low) * gamma * scale;
                        if (rigid) bodyW += w; else soilW += w;
                        wx += w * gx; wy += w * (high + low) / 2;
                    }
                    foreach (var column in section.LayersAt(gx))
                    {
                        double low = Math.Max(bottom, column.Bottom), high = upper;
                        foreach (bool wet in new[] { false, true })
                        {
                            double lo = wet ? low : Math.Max(low, water), hi = wet ? Math.Min(high, water) : high;
                            double gamma = (wet ? column.Soil.SaturatedUnitWeight : column.Soil.UnitWeight) * factors.SoilWeight;
                            Mass(lo, hi, gamma, false);
                            // The displaced soil is replaced by the weight of the rigid body.
                            foreach (var interval in intervals) Mass(Math.Max(lo, interval.Span!.Value.Bottom), Math.Min(hi, interval.Span.Value.Top), -gamma, false);
                        }
                        upper = Math.Min(upper, column.Bottom);
                    }
                    foreach (var interval in intervals) Mass(interval.Span!.Value.Bottom, interval.Span.Value.Top, interval.Body.UnitWeight * factors.BodyWeight, true);
                }
                double wTotal = soilW + bodyW, xg = wTotal > 0 ? wx / wTotal : x, yg = wTotal > 0 ? wy / wTotal : (top + y) / 2;
                double v = (1 - factors.Kv) * wTotal, h = factors.Kh * wTotal;
                double driving = (v * (xg - circle.X) + h * (circle.Y - yg)) / circle.Radius, vl = 0, hl = 0;
                foreach (var load in section.Loads)
                {
                    double factor = factors.Loads.TryGetValue(load.Id, out var f) ? f : 0;
                    double length = load.Distributed ? Math.Max(0, Math.Min(r, load.Right) - Math.Max(l, load.Left))
                        : load.Left >= l && (load.Left < r || i == cuts.Length - 1 && load.Left <= r) ? 1 : 0;
                    if (factor == 0 || length == 0) continue;
                    double atX = load.Distributed ? (Math.Max(l, load.Left) + Math.Min(r, load.Right)) / 2 : load.Left;
                    vl += load.Vertical * factor * length; hl += load.Horizontal * factor * length;
                    driving += factor * length * (load.Vertical * (atX - circle.X) + load.Horizontal * (circle.Y - load.Y) + load.Moment) / circle.Radius;
                }
                double u = section.Water.Count == 0 || factors.Undrained ? 0 : section.WaterUnitWeight * Math.Max(0, SlopeGeometry.Height(section.Water, x) - y);
                double phi = factors.Undrained ? 0 : Math.Atan(Math.Tan(layer.Soil.FrictionAngle) / factors.TanFrictionAngle);
                double cohesion = factors.Undrained ? layer.Soil.UndrainedShearStrength!.Value / factors.UndrainedShearStrength : layer.Soil.EffectiveCohesion / factors.EffectiveCohesion;
                result.Add(new SlopeSlice(i, l, r, y, top, Math.Asin((x - circle.X) / circle.Radius), layer.Name, soilW, bodyW, xg, yg, vl, hl, u, phi, cohesion, v + vl, driving));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Search of the critical circle of every combination: grid of exits, entries and depths plus the tangent family, refinement around up to six
        /// distinct minima, check of the critical circle with twice the slices (2% tolerance on F).
        /// </summary>
        public static SlopeResult Calculate(SlopeSection section, SlopeSearch search, IReadOnlyList<SlopeFactors> cases, CancellationToken token = default)
        {
            Validate(section, search, cases); var output = new List<SlopeCaseResult>();
            foreach (var factors in cases)
            {
                int tried = 0, valid = 0, solved = 0, failed = 0; SlopeSurfaceResult? best = null;
                var minima = new List<SlopeSurfaceResult>();
                var seen = new HashSet<(double, double, double)>();
                void Evaluate(SlipCircle? c)
                {
                    if (c == null) return;
                    double depth = c.Radius - c.Y;
                    // The circles of the grid are built at the bounds of the depth: a rounding of R − Y must not drop them (1e-9 mm).
                    if (depth < search.DepthMin - 1e-9 || depth > search.DepthMax + 1e-9 || !seen.Add((c.Left, c.Right, depth))) return;
                    tried++;
                    if (!SlopeGeometry.Admissible(section, c)) return; valid++;
                    var slices = Slices(section, c, factors, search.Slices);
                    if (slices.Sum(s => s.Driving) <= 1e-9) return;
                    var check = BishopSolver.Solve(c, slices, factors.ResistanceFactor, token);
                    if (check == null) { failed++; return; } solved++;
                    if (best == null || check.Factor < best.Factor) best = check;
                    minima.Add(check); if (minima.Count > 80) { minima.Sort((a, b) => a.Factor.CompareTo(b.Factor)); minima.RemoveRange(60, minima.Count - 60); }
                }
                void Scan(double xmin, double xmax, double rmin, double rmax, double dmin, double dmax, int n)
                {
                    for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                    {
                        double l = xmin + (xmax - xmin) * i / (n - 1), r = rmin + (rmax - rmin) * j / (n - 1);
                        var exit = new SlopePoint(l, SlopeGeometry.Height(section.Surface, l));
                        var entry = new SlopePoint(r, SlopeGeometry.Height(section.Surface, r));
                        // A volume grid need not hit the tangent border, where a critical surface can lie: sample that family explicitly.
                        Evaluate(SlopeGeometry.TangentAtEntry(exit, entry));
                        for (int k = 0; k < n; k++)
                        {
                            token.ThrowIfCancellationRequested();
                            double depth = dmin + (dmax - dmin) * k / (n - 1);
                            Evaluate(SlopeGeometry.Through(exit, entry, -depth));
                        }
                    }
                }
                Scan(search.ExitMin, search.ExitMax, search.EntryMin, search.EntryMax, search.DepthMin, search.DepthMax, search.Grid);
                double dl = (search.ExitMax - search.ExitMin) / (search.Grid - 1), dr = (search.EntryMax - search.EntryMin) / (search.Grid - 1), dd = (search.DepthMax - search.DepthMin) / (search.Grid - 1);
                for (int level = 0; level < search.Refinements && best != null; level++)
                {
                    var seeds = new List<SlipCircle>();
                    foreach (var candidate in minima.OrderBy(c => c.Factor))
                    {
                        var c = candidate.Circle;
                        if (seeds.All(p => Math.Abs(p.Left - c.Left) > dl * .4 || Math.Abs(p.Right - c.Right) > dr * .4 || Math.Abs((p.Radius - p.Y) - (c.Radius - c.Y)) > dd * .4)) seeds.Add(c);
                        if (seeds.Count == 6) break;
                    }
                    foreach (var c in seeds)
                    {
                        double dep = c.Radius - c.Y;
                        Scan(Math.Max(search.ExitMin, c.Left - dl), Math.Min(search.ExitMax, c.Left + dl), Math.Max(search.EntryMin, c.Right - dr), Math.Min(search.EntryMax, c.Right + dr),
                            Math.Max(search.DepthMin, dep - dd), Math.Min(search.DepthMax, dep + dd), 5);
                    }
                    dl /= 2; dr /= 2; dd /= 2;
                }
                bool boundary = best != null && (Near(best.Circle.Left, search.ExitMin, dl) || Near(best.Circle.Left, search.ExitMax, dl) || Near(best.Circle.Right, search.EntryMin, dr)
                    || Near(best.Circle.Right, search.EntryMax, dr) || Near(best.Circle.Radius - best.Circle.Y, search.DepthMin, dd) || Near(best.Circle.Radius - best.Circle.Y, search.DepthMax, dd));
                var status = best == null ? SlopeSearchStatus.NoSurface : failed > 0 ? SlopeSearchStatus.Incomplete : boundary ? SlopeSearchStatus.BoundaryMinimum : SlopeSearchStatus.Converged;
                double? refinedFactor = null;
                if (best != null)
                {
                    var refined = BishopSolver.Solve(best.Circle, Slices(section, best.Circle, factors, 2 * search.Slices), factors.ResistanceFactor, token);
                    refinedFactor = refined?.Factor;
                    if (refined == null || Math.Abs(refined.Factor / best.Factor - 1) > .02) { failed++; status = SlopeSearchStatus.NotConverged; }
                    else best = refined.Factor < best.Factor ? refined : best;
                }
                output.Add(new SlopeCaseResult(factors, best, tried, valid, solved, failed, boundary, status, refinedFactor));
            }
            return new SlopeResult(section, search, output.ToArray(), Notes.ToArray());
        }

        private static bool Near(double value, double bound, double step) => Math.Abs(value - bound) < Math.Max(1e-3, step * .1);

        /// <summary>
        /// Data of the method (ArgumentException otherwise): polylines of 2-100 finite points with increasing abscissae (vertical steps only in the
        /// surface); columns of 1-50 layers with decreasing bottoms, 10 ≤ γ ≤ γsat ≤ 30 kN/m³, φ' ≤ 50°; water line over the whole surface and not
        /// above it; exits left of the required abscissa, entries right of it, positive depths covered by the layers; 3-21 nodes, 20-200 slices,
        /// 0-4 refinements; 1-256 combinations with positive factors, |kh|, |kv| ≤ 0.5; undrained analyses need cu in every layer.
        /// </summary>
        public static void Validate(SlopeSection s, SlopeSearch q, IReadOnlyList<SlopeFactors> cases)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (cases == null) throw new ArgumentNullException(nameof(cases));
            void Line(IReadOnlyList<SlopePoint> points, string name, bool vertical = false)
            {
                if (points.Count < 2 || points.Count > 100 || points.Any(p => !Finite(p.X) || !Finite(p.Y))) throw new ArgumentException(name + ": 2 to 100 finite points.");
                for (int i = 1; i < points.Count; i++) if (points[i].X < points[i - 1].X || !vertical && points[i].X == points[i - 1].X) throw new ArgumentException(name + ": increasing abscissae.");
            }
            Line(s.Surface, "Ground surface", true);
            double minimumWeight = 10 * SoilUnits.KiloNewtonPerCubicMetre, maximumWeight = 30 * SoilUnits.KiloNewtonPerCubicMetre, maximumFriction = 50 * SoilUnits.Degree;
            foreach (var column in s.Columns)
            {
                if (column.Count < 1 || column.Count > 50) throw new ArgumentException("Slope stability: give the deep layers (1 to 50 per column).");
                double top = double.PositiveInfinity;
                foreach (var layer in column)
                {
                    var soil = layer.Soil;
                    if (!Finite(layer.Bottom) || layer.Bottom >= top || soil.UnitWeight < minimumWeight || soil.SaturatedUnitWeight < soil.UnitWeight || soil.SaturatedUnitWeight > maximumWeight
                        || soil.FrictionAngle < 0 || soil.FrictionAngle > maximumFriction || soil.EffectiveCohesion < 0)
                        throw new ArgumentException("Slope layers: decreasing bottoms, 10 ≤ γ ≤ γsat ≤ 30 kN/m³, 0 ≤ φ' ≤ 50°, c' ≥ 0.");
                    top = layer.Bottom;
                }
            }
            if (!Finite(s.SoilSplitX)) throw new ArgumentException("Invalid split of the soil columns.");
            if (!Finite(s.WaterUnitWeight) || s.WaterUnitWeight <= 0) throw new ArgumentException("Invalid unit weight of water.");
            if (s.Water.Count > 0)
            {
                Line(s.Water, "Water line");
                if (s.Water[0].X > s.Surface[0].X || s.Water[s.Water.Count - 1].X < s.Surface[s.Surface.Count - 1].X) throw new ArgumentException("The water line must cover the whole surface.");
                foreach (double x in s.Surface.Select(p => p.X).Concat(s.Water.Select(p => p.X)).Where(x => x >= s.Surface[0].X && x <= s.Surface[s.Surface.Count - 1].X))
                    if (SlopeGeometry.Height(s.Water, x) > SlopeGeometry.Height(s.Surface, x) + 1e-3) throw new ArgumentException("Water above the ground: free water is not supported.");
            }
            foreach (var body in s.Bodies)
                if (body.Polygon.Count < 3 || body.Polygon.Any(p => !Finite(p.X) || !Finite(p.Y)) || !Finite(body.UnitWeight) || body.UnitWeight <= 0)
                    throw new ArgumentException("Rigid body " + body.Name + ": at least 3 finite vertices and a positive unit weight.");
            foreach (var load in s.Loads)
                if (!new[] { load.Left, load.Right, load.Y, load.Vertical, load.Horizontal, load.Moment }.All(Finite) || load.Distributed && load.Right < load.Left)
                    throw new ArgumentException("Load " + load.Id + ": finite values, right end not before the left one.");
            if (!new[] { q.ExitMin, q.ExitMax, q.EntryMin, q.EntryMax, q.DepthMin, q.DepthMax }.All(Finite) || q.ExitMin >= q.ExitMax || q.EntryMin >= q.EntryMax || q.DepthMin <= 0
                || q.DepthMin >= q.DepthMax || q.ExitMin < s.Surface[0].X || q.ExitMax >= s.RequiredLeft || q.EntryMin <= s.RequiredRight || q.EntryMax > s.Surface[s.Surface.Count - 1].X
                || -q.DepthMax < s.CoveredBottom)
                throw new ArgumentException("Search domain: exits before the required left abscissa, entries after the right one, positive depths covered by the layers and the surface.");
            if (q.Grid < 3 || q.Grid > 21 || q.Slices < 20 || q.Slices > 200 || q.Refinements < 0 || q.Refinements > 4)
                throw new ArgumentException("Search: 3-21 nodes per direction, 20-200 slices, 0-4 refinements.");
            if (cases.Count < 1 || cases.Count > 256) throw new ArgumentException("From 1 to 256 combinations.");
            foreach (var c in cases)
            {
                if (c == null) throw new ArgumentNullException(nameof(cases));
                if (!new[] { c.SoilWeight, c.BodyWeight, c.TanFrictionAngle, c.EffectiveCohesion, c.UndrainedShearStrength, c.ResistanceFactor, c.Kh, c.Kv }.All(Finite)
                    || c.SoilWeight <= 0 || c.BodyWeight <= 0 || c.TanFrictionAngle <= 0 || c.EffectiveCohesion <= 0 || c.UndrainedShearStrength <= 0 || c.ResistanceFactor <= 0
                    || Math.Abs(c.Kh) > .5 || Math.Abs(c.Kv) > .5 || c.Loads.Values.Any(v => !Finite(v)))
                    throw new ArgumentException("Invalid factors of combination " + c.Name + ".");
                if (c.Undrained && s.Columns.SelectMany(l => l).Any(x => !x.Soil.UndrainedShearStrength.HasValue))
                    throw new ArgumentException("Undrained analysis: cu is required in every layer.");
            }
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
