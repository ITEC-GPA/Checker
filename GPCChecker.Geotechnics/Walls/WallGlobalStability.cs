using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>
    /// Profile of the global stability of the wall (mm, origin at the toe on the base): ground in front (from the far left to (0; Dv)) and behind
    /// (from (a + s0; H + t) to the far right), soil column behind and, for two columns, the one in front left of <see cref="SoilSplitX"/>, water line
    /// (empty = dry), undrained analysis and the search domain. The geology is a datum of the site: <see cref="WallGlobalStability.Propose"/> only
    /// proposes it from the soils of the wall, to be checked and confirmed.
    /// </summary>
    public sealed class WallGlobalProfile
    {
        public IReadOnlyList<SlopePoint> Valley { get; }
        public IReadOnlyList<SlopePoint> Uphill { get; }
        public IReadOnlyList<SlopeLayer> Layers { get; }
        public IReadOnlyList<SlopeLayer> ValleyLayers { get; }
        public double SoilSplitX { get; }
        public IReadOnlyList<SlopePoint> Water { get; }
        public bool Undrained { get; }
        public SlopeSearch Search { get; }
        public WallGlobalProfile(IEnumerable<SlopePoint> valley, IEnumerable<SlopePoint> uphill, IEnumerable<SlopeLayer> layers, SlopeSearch search, IEnumerable<SlopeLayer>? valleyLayers = null,
            double soilSplitX = 0, IEnumerable<SlopePoint>? water = null, bool undrained = false)
        {
            Valley = (valley ?? throw new ArgumentNullException(nameof(valley))).ToArray(); Uphill = (uphill ?? throw new ArgumentNullException(nameof(uphill))).ToArray();
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray(); Search = search ?? throw new ArgumentNullException(nameof(search));
            ValleyLayers = (valleyLayers ?? Enumerable.Empty<SlopeLayer>()).ToArray(); SoilSplitX = soilSplitX; Water = (water ?? Enumerable.Empty<SlopePoint>()).ToArray(); Undrained = undrained;
        }
    }

    /// <summary>
    /// Seismic coefficients of the global stability of the complex wall-soil (NTC 2018 §7.11.4): kh = βs amax/g with βs = 0.38 from the site, kv =
    /// ±0.5 kh, or assigned (0-0.5).
    /// </summary>
    public sealed class WallGlobalSeismic
    {
        public double Kh { get; }
        public double Kv { get; }
        private WallGlobalSeismic(double kh, double kv) { Kh = kh; Kv = kv; }
        public static WallGlobalSeismic FromSite(NtcSiteAmplification site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            double kh = .38 * site.AmaxG; return new WallGlobalSeismic(kh, .5 * kh);
        }
        public static WallGlobalSeismic Assigned(double kh, double kv)
        {
            if (!(kh >= 0 && kh <= .5)) throw new ArgumentException("Stabilità globale: kh, inserire un valore fra 0 e 0.5.");
            if (!(kv >= 0 && kv <= .5)) throw new ArgumentException("Stabilità globale: kv, inserire un valore fra 0 e 0.5.");
            return new WallGlobalSeismic(kh, kv);
        }
    }

    /// <summary>
    /// Global stability of the complex retaining wall-soil with the simplified Bishop method (<see cref="SlopeStability"/>): the wall is a rigid body,
    /// the actions are loads of the slope. Transferred from ANTHEA (RetainingWall.GlobalStability.cs, commit fe4652c). NTC 2018 §6.5.3.1.1:
    /// Approach 1, Combination 2 (A2+M2+R2): γG1 = 1, γG2 = 0/1.3, γQ = 0/1.3, γM tan φ' = γM c' = 1.25, γM cu = 1.4, γR = 1.1 (Tab. 6.8.I); SLV
    /// (§7.11.4): γA = γM = 1, γR = 1.2. Units mm, N/mm, MPa, N/mm³, rad.
    /// </summary>
    public static class WallGlobalStability
    {
        /// <summary>Help of the method, in Italian for the reports.</summary>
        public const string Help = "Stabilità globale: Bishop semplificato, superfici circolari sotto l’intero muro, verso valle. "
            + "Statica: Approccio 1, combinazione 2 A2+M2+R2; γG1=1, γG2=0/1,3, γQ=0/1,3, γM,tanφ=γM,c′=1,25, γM,cu=1,4, γR=1,10. "
            + "SLV del complesso muro–terreno (§§7.11.6.2.2 e 7.11.4): γA=γM=1, γR=1,20; kh=βs·amax/g con βs=0,38 e kv=±0,5kh. "
            + "βs è distinto dal βm delle spinte. La stabilità del versante naturale e i meccanismi non circolari richiedono una valutazione dedicata.";

        /// <summary>
        /// Combinations of the global stability from the ordinary ones of the wall (without the seismic action): every ULS with the factors of the actions
        /// of A2 (G1 1, the others × 1.3/1.5) and M2, γR 1.1; every accidental combination with unit factors; with the seismic action, the quasi
        /// permanent actions with ±kv and γR 1.2. Identical rows are merged; at most 256.
        /// </summary>
        public static IReadOnlyList<SlopeFactors> Combinations(WallInput input, WallGlobalSeismic? seismic, bool undrained)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var plain = new WallInput(input.Family, input.Geometry, input.UnitWeight, input.Backfill, input.Foundation, input.Valley, input.Back, input.Base, input.Water, input.Actions, null, input.ReinforcementSplit);
            var ordinary = WallCombinations.Generate(plain);
            var result = new List<SlopeFactors>(); var seen = new HashSet<string>();
            void Add(string label, IReadOnlyDictionary<string, double> coefficients, double mphi, double mc, double mcu, double r, double kh = 0, double kv = 0, string state = "")
            {
                var factors = input.Actions.ToDictionary(a => a.Id, a => coefficients.TryGetValue(a.Id, out double f) ? f : 0);
                string signature = state + "/" + R(kh) + "/" + R(kv) + "/" + string.Join(",", input.Actions.Select(a => R(factors[a.Id])));
                if (!seen.Add(signature)) return;
                if (result.Count >= 256) throw new ArgumentException("Oltre 256 combinazioni globali: raggruppare le azioni correlate.");
                result.Add(new SlopeFactors(label + " " + (result.Count + 1), 1, 1, mphi, mc, mcu, r, kh, kv, undrained, factors));
            }
            foreach (var row in ordinary.Where(c => c.State == WallLimitState.Ultimate || c.State == WallLimitState.Exceptional))
            {
                bool statics = row.State == WallLimitState.Ultimate;
                var coefficients = row.Coefficients.ToDictionary(p => p.Key, p => p.Value);
                if (statics)
                    foreach (var a in input.Actions)
                        coefficients[a.Id] = !a.Enabled ? 0 : a.Category == WallActionCategory.G1 ? 1 : coefficients[a.Id] * 1.3 / 1.5;
                Add(statics ? "Globale A2–M2–R2" : "Globale eccezionale", coefficients, statics ? 1.25 : 1, statics ? 1.25 : 1, statics ? 1.4 : 1, statics ? 1.1 : 1, state: statics ? "SLU" : "ECCEZIONALE");
            }
            if (seismic != null)
            {
                var quasi = ordinary.First(c => c.State == WallLimitState.QuasiPermanent).Coefficients;
                foreach (double sign in new[] { -seismic.Kv, seismic.Kv }.Distinct())
                    Add("Globale SLV kv" + (sign < 0 ? "−" : "+"), quasi, 1, 1, 1, 1.2, seismic.Kh, sign, "SISMA");
            }
            return result;
        }

        /// <summary>
        /// Proposal of the profile from the wall: flat ground over max(10 m, 4 (H + t)) on both sides, the two soil columns of the wall (c' = 0, no
        /// cu), the water of the wall, exits from the far left to −0.1 m, entries from B + 0.1 m to the far right, depths from 0.1 m to min(bottom of
        /// the columns, 2 (H + t)); 9 grid nodes, 60 slices, 4 refinements. A proposal, not a survey: the deep geology must be checked.
        /// </summary>
        public static WallGlobalProfile Propose(WallInput input, int grid = 9, int slices = 60, int refinements = 4)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var g = input.Geometry; double height = g.TotalHeight, back = g.Back, width = g.Width, span = Math.Max(10000, height * 4), dv = input.Valley.Height;
            var valley = new[] { new SlopePoint(-span, dv), new SlopePoint(0, dv) };
            var uphill = new[] { new SlopePoint(back, height), new SlopePoint(width + span, height) };
            var layers = input.Backfill.Layers.Select(l => new SlopeLayer(l.Soil, l.Bottom)).ToArray();
            var valleyLayers = input.Valley.Profile.Layers.Select(l => new SlopeLayer(l.Soil, l.Bottom)).ToArray();
            var water = input.Water == null ? new SlopePoint[0] : new[] { new SlopePoint(-span, input.Water.FrontHead), new SlopePoint(0, input.Water.FrontHead),
                new SlopePoint(back, height - input.Water.Depth), new SlopePoint(width + span, height - input.Water.Depth) };
            double bottom = Math.Max(layers[layers.Length - 1].Bottom, valleyLayers[valleyLayers.Length - 1].Bottom);
            double depth = Math.Min(-bottom, 2 * height);
            if (!(depth > 100)) throw new ArgumentException("Stabilità globale: depth_max, inserire un valore fra -10000 e 10000.");
            return new WallGlobalProfile(valley, uphill, layers, new SlopeSearch(-span, -100, width + 100, width + span, 100, depth, grid, slices, refinements), valleyLayers, back, water);
        }

        /// <summary>Ground surface of the section: the profile in front, the face of the stem from max(t, Dv) to the top, the profile behind.</summary>
        public static IReadOnlyList<SlopePoint> Surface(WallInput input, WallGlobalProfile profile)
        {
            var g = input.Geometry; double t = g.Slab, h = g.Height, a = g.Toe, s = g.StemBase, top = g.StemTop, y = Math.Max(t, input.Valley.Height);
            return profile.Valley.Concat(new[] { new SlopePoint(0, y), new SlopePoint(a + (s - top) * (y - t) / h, y), new SlopePoint(a + s - top, h + t) }).Concat(profile.Uphill).Distinct().ToArray();
        }

        /// <summary>The loads of the slope from the enabled actions of the wall (the factors by identifier are in the combinations).</summary>
        public static IReadOnlyList<SlopeLoad> Loads(WallInput input, WallGlobalProfile profile)
        {
            var g = input.Geometry; double back = g.Back, top = g.TotalHeight, far = profile.Uphill[profile.Uphill.Count - 1].X;
            return input.Actions.Where(a => a.Enabled).Select(a =>
            {
                switch (a.Type)
                {
                    case WallActionType.UniformSurcharge: return new SlopeLoad(a.Id, back, far, top, a.Value, 0, 0, true);
                    case WallActionType.VerticalForce: return new SlopeLoad(a.Id, a.X, a.X, a.Z, a.Value, 0, 0, false);
                    case WallActionType.Moment: return new SlopeLoad(a.Id, back, back, a.Z, 0, 0, a.Value, false);
                    case WallActionType.LateralPressure: return new SlopeLoad(a.Id, back, back, (a.Z + a.Z0) / 2, 0, a.Value * (a.Z - a.Z0), 0, false);
                    default: return new SlopeLoad(a.Id, back, back, a.Z, 0, a.Value, 0, false);
                }
            }).ToArray();
        }

        /// <summary>
        /// Global stability of the wall: the wall is a rigid body with its outline and unit weight, the profile ends at the face and at the back of the
        /// stem, every circle passes below the whole wall (exit before the toe, entry beyond the heel).
        /// </summary>
        public static SlopeResult Calculate(WallInput input, WallGlobalProfile profile, IReadOnlyList<SlopeFactors> cases, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var g = input.Geometry; double h = g.Height, t = g.Slab, back = g.Back, width = g.Width;
            var valley = profile.Valley; var uphill = profile.Uphill;
            if (valley.Count < 2 || uphill.Count < 2 || Math.Abs(valley[valley.Count - 1].X) > 1e-5 || Math.Abs(valley[valley.Count - 1].Y - input.Valley.Height) > 1e-5
                || Math.Abs(uphill[0].X - back) > 1e-5 || Math.Abs(uphill[0].Y - h - t) > 1e-5 || uphill[1].X < width)
                throw new ArgumentException("Profilo globale: valle termina in (0;Dv), monte inizia in (a+s₀;H+t); il punto successivo deve superare la fondazione. Aggiornare il profilo dopo modifiche geometriche.");
            if (uphill.Any(p => p.Y < h + t - 1e-5 && p.X <= width)) throw new ArgumentException("Il profilo globale non può scendere attraverso il riempimento sopra la fondazione.");
            if (profile.Undrained && profile.Layers.Concat(profile.ValleyLayers).Any(l => !l.Soil.UndrainedShearStrength.HasValue))
                throw new ArgumentException("Stabilità globale: cu, inserire un valore fra 0.001 e 10000.");
            var outline = new[] { new SlopePoint(0, 0), new SlopePoint(width, 0), new SlopePoint(width, t), new SlopePoint(back, t), new SlopePoint(back, t + h),
                new SlopePoint(back - g.StemTop, t + h), new SlopePoint(g.Toe, t), new SlopePoint(0, t) };
            var body = new SlopeBody("Muro · " + (input.Family == WallFamily.Gravity ? "gravity" : "cantilever"), outline, input.UnitWeight);
            var section = new SlopeSection(Surface(input, profile), profile.Layers, profile.Water, new[] { body }, Loads(input, profile), 0, width, profile.ValleyLayers, profile.SoilSplitX);
            return SlopeStability.Calculate(section, profile.Search, cases, token);
        }

        /// <summary>
        /// The checks of the global stability: demand γR, resistance F of the critical circle, ratio γR/F when it is a verdict (η &gt; 1, or a converged
        /// search inside the domain without numerical failures), otherwise unavailable with the status of the search.
        /// </summary>
        public static IReadOnlyList<WallCheck> Checks(SlopeResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return result.Cases.Select(c =>
            {
                double? ratio = c.Critical != null && (c.Critical.Ratio > 1 || !c.Boundary && c.NumericalFailures == 0) ? c.Critical.Ratio : (double?)null;
                var status = ratio is null ? WallCheckStatus.Unavailable : ratio <= 1 ? WallCheckStatus.Satisfied : WallCheckStatus.NotSatisfied;
                return new WallCheck(WallCheckKind.GlobalStability, c.Factors.Name, c.Factors.ResistanceFactor, c.Critical?.Factor, ratio, status, c.Status.ToString());
            }).ToArray();
        }

        private static string R(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
