using System.Globalization;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>
    /// Automatic combinations of the wall, NTC 2018 §6.5.3.1.1 Approach 2 (A1+M1+R3) as the preset of ANTHEA (RetainingWall.GenerateCombinations,
    /// commit fe4652c): SLS characteristic and frequent with every leading variable action, quasi permanent; ULS with every pattern of the factors
    /// of the wall (1/1.3), of the fill (1/1.3), of the water (1/1.3), of every permanent group (G1 1/1.3, G2 0/1.5), of the presence of every
    /// variable group and every leading one (1.5, the others 1.5 ψ0), and of the valley soil (1/1.3) when there is soil in front; γR 1.1 sliding,
    /// 1.15 overturning, 1.4 bearing. Seismic with the quasi permanent actions: assigned kh and ±kv, or from the site general and overturning (γR
    /// 1, 1, 1.2, NTC Tab. 7.11.III). Accidental: every group of accidental actions with the quasi permanent ones. Identical rows are merged; at
    /// most 4096 rows.
    /// </summary>
    public static class WallCombinations
    {
        public static IReadOnlyList<WallCombination> Generate(WallInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var actions = input.Actions.Where(x => x.Enabled).ToArray();
            var groups = actions.GroupBy(x => x.Key).ToArray();
            var vars = groups.Where(gr => gr.First().Category == WallActionCategory.Q).ToArray();
            var permanent = groups.Where(gr => gr.First().Category == WallActionCategory.G1 || gr.First().Category == WallActionCategory.G2).ToArray();
            var result = new List<WallCombination>(); var seen = new HashSet<string>();
            bool valley = input.Valley.Height > 0, water = input.Water != null;
            void Add(WallLimitState state, string label, double wall, double soil, double waterFactor, Dictionary<string, double> factors, double kh = 0, double kv = 0, WallSeismicPurpose purpose = WallSeismicPurpose.None)
            {
                foreach (double valleyFactor in state == WallLimitState.Ultimate && valley ? new[] { 1d, 1.3 } : new[] { 1d })
                {
                    var coefficients = input.Actions.ToDictionary(x => x.Id, x => x.Enabled && factors.TryGetValue(x.Id, out double f) ? f : 0);
                    string fingerprint = string.Join("/", state, purpose, R(wall), R(soil), R(waterFactor), R(kh), R(kv), R(valleyFactor)) + "/" + string.Join(",", input.Actions.Select(x => R(coefficients[x.Id])));
                    if (!seen.Add(fingerprint)) continue;
                    if (result.Count >= 4096) throw new ArgumentException("Oltre 4096 combinazioni: raggruppare le azioni correlate o predisporre una matrice personalizzata.");
                    bool design = state == WallLimitState.Ultimate || state == WallLimitState.Seismic;
                    string approach = purpose != WallSeismicPurpose.None ? "SLV · NTC 7.11.III" : state == WallLimitState.Seismic ? "Sismica · M1/R3" : design ? "A1+M1+R3"
                        : state == WallLimitState.Exceptional ? "Eccezionale · M1/R=1" : "SLE";
                    double slide = purpose != WallSeismicPurpose.None ? 1 : design ? 1.1 : 1, over = purpose != WallSeismicPurpose.None ? 1 : design ? 1.15 : 1, bearing = purpose != WallSeismicPurpose.None ? 1.2 : design ? 1.4 : 1;
                    result.Add(new WallCombination(label + " " + (result.Count + 1), state, wall, soil, valleyFactor, waterFactor, 1, slide, over, bearing, kh, kv, coefficients, purpose, approach));
                }
            }
            Dictionary<string, double> Service(WallLimitState state, string? lead = null) => actions.ToDictionary(x => x.Id, x =>
                x.Category == WallActionCategory.G1 || x.Category == WallActionCategory.G2 ? 1d : x.Category == WallActionCategory.A ? 0d
                : state == WallLimitState.QuasiPermanent ? x.Psi2 : x.Key == lead ? (state == WallLimitState.Characteristic ? 1 : x.Psi1) : (state == WallLimitState.Characteristic ? x.Psi0 : x.Psi2));
            foreach (var state in new[] { WallLimitState.Characteristic, WallLimitState.Frequent })
                foreach (string? lead in vars.Length == 0 ? new string?[] { null } : vars.Select(v => (string?)v.Key))
                    Add(state, state == WallLimitState.Characteristic ? "SLE" : "SLE_FREQ", 1, 1, 1, Service(state, lead));
            Add(WallLimitState.QuasiPermanent, "Quasi permanente", 1, 1, 1, Service(WallLimitState.QuasiPermanent));
            int bits = vars.Length + permanent.Length + 2 + (water ? 1 : 0);
            if (bits > 12 || (1L << bits) * Math.Max(1, vars.Length) > 4090) throw new ArgumentException("Generazione troppo estesa: massimo 4096 combinazioni. Raggruppare i carichi correlati.");
            for (int mask = 0; mask < (1 << bits); mask++)
            {
                double wall = (mask & 1) == 0 ? 1 : 1.3, soil = (mask & 2) == 0 ? 1 : 1.3;
                int baseBit = 2; double waterFactor = 1;
                if (water) waterFactor = (mask & (1 << baseBit++)) == 0 ? 1 : 1.3;
                var f = actions.ToDictionary(x => x.Id, _ => 0d);
                foreach (var p in permanent) { bool high = (mask & (1 << baseBit++)) != 0; foreach (var x in p) f[x.Id] = x.Category == WallActionCategory.G1 ? (high ? 1.3 : 1) : (high ? 1.5 : 0); }
                var active = vars.Where((_, i) => (mask & (1 << (baseBit + i))) != 0).ToArray();
                if (active.Length == 0) Add(WallLimitState.Ultimate, "SLU", wall, soil, waterFactor, f);
                foreach (var lead in active)
                {
                    foreach (var v in active) foreach (var x in v) f[x.Id] = 1.5 * (v.Key == lead.Key ? 1 : x.Psi0);
                    Add(WallLimitState.Ultimate, "SLU", wall, soil, waterFactor, f);
                }
            }
            if (input.Seismic != null)
            {
                var s = input.Seismic;
                if (s.Site == null)
                    foreach (double kv in new[] { -s.Kv, s.Kv }.Distinct()) Add(WallLimitState.Seismic, kv < 0 ? "Sisma kv−" : "Sisma kv+", 1, 1, 1, Service(WallLimitState.QuasiPermanent), s.Kh, kv);
                else
                    foreach (var (purpose, kh, magnitude) in new[] { (WallSeismicPurpose.General, s.Kh, s.Kv), (WallSeismicPurpose.Overturning, s.KhOverturning, s.KvOverturning) })
                    {
                        string label = purpose == WallSeismicPurpose.General ? "Generale" : "Ribaltamento";
                        if (kh > .4 || magnitude > .2) throw new ArgumentException($"Sisma {label}: kh={kh.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"))}, |kv|={magnitude.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"))} fuori dal campo del motore (kh≤0,4; |kv|≤0,2). I coefficienti non vengono troncati.");
                        foreach (double kv in new[] { -magnitude, magnitude }.Distinct()) Add(WallLimitState.Seismic, $"SLV {label} kv{(kv < 0 ? "−" : "+")}", 1, 1, 1, Service(WallLimitState.QuasiPermanent), kh, kv, purpose);
                    }
            }
            foreach (var accident in groups.Where(gr => gr.First().Category == WallActionCategory.A))
            {
                var f = Service(WallLimitState.QuasiPermanent); foreach (var x in accident) f[x.Id] = 1;
                Add(WallLimitState.Exceptional, "Eccezionale " + accident.First().Name, 1, 1, 1, f);
            }
            return result;
        }

        private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
