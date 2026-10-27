using System.Globalization;
using GPC.Model.Design.Combinations;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>
    /// Automatic combinations of the wall, NTC 2018 §6.5.3.1.1 Approach 2 (A1+M1+R3) as the preset of ANTHEA (RetainingWall.GenerateCombinations,
    /// commit fe4652c): SLS characteristic and frequent with every leading variable action, quasi permanent; ULS with every pattern of the factors
    /// of the wall (1/1.3), of the fill (1/1.3), of the water (1/1.3), of every permanent group (G1 1/1.3, G2 0/1.5), of the presence of every
    /// variable group and every leading one (1.5, the others 1.5 ψ0), and of the valley soil (1/1.3) when there is soil in front; γR 1.1 sliding,
    /// 1.15 overturning, 1.4 bearing, 1.4 the passive resistance in front (γR,e, NTC 2018 Tab. 6.5.I; 1 in the other rows). Seismic with the quasi permanent actions: assigned kh and ±kv, or from the site general and overturning (γR
    /// 1, 1, 1.2, NTC Tab. 7.11.III). Accidental: every group of accidental actions with the quasi permanent ones. Identical rows are merged; at
    /// most 4096 rows.
    /// <para>
    /// Every ULS, seismic (assigned, SLV general and overturning) and accidental row with a variable uniform surcharge (factor &gt; 0: γQ, ψ0 γQ or
    /// ψ2) on a wall with a heel is followed by its twin, named with the suffix «b» (for example SLU 8 and SLU 8b, SLV Generale kv− 12 and
    /// SLV Generale kv− 12b), with <see cref="WallCombination.VariableSurchargeOnHeel"/> false: the surcharge keeps the thrust and is absent from
    /// the heel (a favourable variable action is not applied: γQ = 0, NTC 2018 Tab. 2.6.I; ψ2 Qk only where unfavourable in the seismic and
    /// accidental combinations, NTC 2018 §2.5.3). Both rows enter every check (sliding, overturning, bearing, contact) and the internal forces of
    /// the heel. The numbers of the other rows do not change.
    /// </para>
    /// <para>
    /// The rows come from the generator of Model (<see cref="GeotechnicalCombinations"/>, ANTHEA 1.14.4: «vorrei però che le combo venissero fatte
    /// tutte in model»), with A1 and the γR of <see cref="StandardNTC2018Geotechnics"/>; the wall, the fill and the water are bodies of the pattern,
    /// the valley soil a body that doubles every ULS row. This adapter keeps the names, the twins, the seismic coefficients and the limit of the
    /// rows, and declares the choices of the walls that differ from the standard: G2 favourable 0 instead of 0.8 (set «A1 muri»), the static γR in
    /// the rows with assigned kh and kv, the passive in front only in the ULS. The previous generator is the frozen reference of the tests.
    /// </para>
    /// </summary>
    public static class WallCombinations
    {
        public static IReadOnlyList<WallCombination> Generate(WallInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var actions = input.Actions.Where(x => x.Enabled).ToArray();
            var groups = actions.GroupBy(x => x.Key).ToArray();
            if (groups.Any(gr => gr.Select(x => x.Category).Distinct().Count() > 1))
                throw new ArgumentException("Azioni correlate dello stesso gruppo: natura e coefficienti ψ devono coincidere.");
            int vars = groups.Count(gr => gr.First().Category == WallActionCategory.Q);
            int permanent = groups.Count(gr => gr.First().Category == WallActionCategory.G1 || gr.First().Category == WallActionCategory.G2);
            bool valley = input.Valley.Height > 0, water = input.Water != null, heel = input.Geometry.Heel > 0;
            int bits = vars + permanent + 2 + (water ? 1 : 0);
            if (bits > 12 || (1L << bits) * Math.Max(1, vars) > 4090) throw new ArgumentException("Generazione troppo estesa: massimo 4096 combinazioni. Raggruppare i carichi correlati.");
            var seismic = Variants(input.Seismic);
            var standard = new StandardNTC2018Geotechnics();
            var a1 = standard.Combinations(GeotechnicalCheck.RetainingWallSliding, GeotechnicalSituation.PersistentTransient).Single().Actions;
            // Declared choice of the walls (census D4, open): the favourable G2 is absent, not 0.8 of NTC 2018 Tab. 6.2.I.
            var set = new GeotechnicalActionFactors("A1 muri (G2 favorevole 0)", a1.PermanentUnfavourable, a1.PermanentFavourable, a1.NonStructuralUnfavourable, 0,
                a1.VariableUnfavourable, a1.VariableFavourable);
            var bodies = water ? new[] { "muro", "terreno", "acqua" } : new[] { "muro", "terreno" };
            var request = new GeotechnicalCombinationRequest(set, actions.Select(Action), bodies, valley ? new[] { "terreno a valle" } : null, seismic.Count);
            double Resistance(GeotechnicalCheck check, GeotechnicalSituation situation) => standard.ResistanceFactor(check, situation, "R3")
                ?? throw new InvalidOperationException("γR " + check + " " + situation);
            var result = new List<WallCombination>(); var checkedPurposes = new HashSet<WallSeismicPurpose>();
            foreach (var row in GeotechnicalCombinations.Generate(request))
            {
                var state = State(row.State);
                var variant = row.SeismicVariant >= 0 ? seismic[row.SeismicVariant] : default;
                var purpose = variant.Purpose;
                if (purpose != WallSeismicPurpose.None && checkedPurposes.Add(purpose) && (variant.Kh > .4 || variant.Magnitude > .2))
                    throw new ArgumentException($"Sisma {Label(purpose)}: kh={variant.Kh.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"))}, |kv|={variant.Magnitude.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"))} fuori dal campo del motore (kh≤0,4; |kv|≤0,2). I coefficienti non vengono troncati.");
                string label = row.State switch
                {
                    GeotechnicalLimitState.Characteristic => "SLE",
                    GeotechnicalLimitState.Frequent => "SLE_FREQ",
                    GeotechnicalLimitState.QuasiPermanent => "Quasi permanente",
                    GeotechnicalLimitState.Ultimate => "SLU",
                    GeotechnicalLimitState.Seismic => variant.Label,
                    _ => "Eccezionale " + actions.First(x => x.Key == row.AccidentalGroup).Name
                };
                double wall = row.BodyFactors[0], soil = row.BodyFactors[1], waterFactor = water ? row.BodyFactors[2] : 1, valleyFactor = valley ? row.BodyFactors[bodies.Length] : 1;
                var coefficients = input.Actions.ToDictionary(x => x.Id, x => x.Enabled && row.Factors.TryGetValue(x.Id, out double f) ? f : 0);
                bool design = state == WallLimitState.Ultimate || state == WallLimitState.Seismic;
                string approach = purpose != WallSeismicPurpose.None ? "SLV · NTC 7.11.III" : state == WallLimitState.Seismic ? "Sismica · M1/R3" : design ? "A1+M1+R3"
                    : state == WallLimitState.Exceptional ? "Eccezionale · M1/R=1" : "SLE";
                // γR of NTC 2018 Tab. 6.5.I (R3) and, with the coefficients of the site, Tab. 7.11.III; the rows with assigned kh and kv keep the static
                // values (declared choice, census D5); accidental rows R = 1, SLS without resistance checks.
                var situation = purpose != WallSeismicPurpose.None ? GeotechnicalSituation.Seismic : GeotechnicalSituation.PersistentTransient;
                bool resisted = purpose != WallSeismicPurpose.None || design;
                double slide = resisted ? Resistance(GeotechnicalCheck.RetainingWallSliding, situation) : 1, over = resisted ? Resistance(GeotechnicalCheck.RetainingWallOverturning, situation) : 1,
                    bearing = resisted ? Resistance(GeotechnicalCheck.RetainingWallBearing, situation) : 1;
                // γR,e of the passive in front (NTC 2018 Tab. 6.5.I, R3): 1.4 in the ULS; the seismic rows never count the passive, the
                // accidental ones have R = 1 like the other resistances, the SLS have no resistance checks (mobilised η Pp in the equilibrium).
                double passive = state == WallLimitState.Ultimate ? Resistance(GeotechnicalCheck.RetainingWallPassiveResistance, GeotechnicalSituation.PersistentTransient) : 1;
                string name = label + " " + row.Number;
                // ULS, seismic and accidental rows with a variable surcharge on a heel: also the twin «b» without the surcharge on the heel
                // (favourable variable action excluded: γQ = 0, or ψ2 Qk not applied where favourable), same thrust.
                bool twin = (state == WallLimitState.Ultimate || state == WallLimitState.Seismic || state == WallLimitState.Exceptional) && heel && input.Actions.Any(x => x.Enabled && x.Type == WallActionType.UniformSurcharge && x.Category == WallActionCategory.Q && coefficients[x.Id] > 0);
                foreach (bool onHeel in twin ? new[] { true, false } : new[] { true })
                {
                    if (result.Count >= 4096) throw new ArgumentException("Oltre 4096 combinazioni: raggruppare le azioni correlate o predisporre una matrice personalizzata.");
                    result.Add(new WallCombination(onHeel ? name : name + "b", state, wall, soil, valleyFactor, waterFactor, 1, slide, over, bearing, variant.Kh, variant.Kv, coefficients, purpose, approach, onHeel, passive));
                }
            }
            return result;
        }

        // The ψ of a variable action enter the rows; the generator of Model refuses ψ outside 0 ≤ ψ2 ≤ ψ1 ≤ ψ0 ≤ 1, as ANTHEA does.
        private static GeotechnicalDesignAction Action(WallAction x) => x.Category == WallActionCategory.Q
            ? new GeotechnicalDesignAction(x.Id, x.Key, GeotechnicalActionNature.Q, x.Psi0, x.Psi1, x.Psi2)
            : new GeotechnicalDesignAction(x.Id, x.Key, x.Category == WallActionCategory.G1 ? GeotechnicalActionNature.G1 : x.Category == WallActionCategory.G2 ? GeotechnicalActionNature.G2 : GeotechnicalActionNature.A);

        private static WallLimitState State(GeotechnicalLimitState state) => state switch
        {
            GeotechnicalLimitState.Ultimate => WallLimitState.Ultimate,
            GeotechnicalLimitState.Characteristic => WallLimitState.Characteristic,
            GeotechnicalLimitState.Frequent => WallLimitState.Frequent,
            GeotechnicalLimitState.QuasiPermanent => WallLimitState.QuasiPermanent,
            GeotechnicalLimitState.Seismic => WallLimitState.Seismic,
            _ => WallLimitState.Exceptional
        };

        private static string Label(WallSeismicPurpose purpose) => purpose == WallSeismicPurpose.General ? "Generale" : "Ribaltamento";

        // Seismic rows: assigned kh with −kv and +kv, or from the site the general and the overturning coefficients, each with −kv and +kv
        // (one row when kv = 0); |kv| of the site is checked with its use.
        private static IReadOnlyList<(string Label, double Kh, double Kv, double Magnitude, WallSeismicPurpose Purpose)> Variants(WallSeismic? s)
        {
            var list = new List<(string, double, double, double, WallSeismicPurpose)>();
            if (s == null) return list;
            if (s.Site == null)
                foreach (double kv in new[] { -s.Kv, s.Kv }.Distinct()) list.Add((kv < 0 ? "Sisma kv−" : "Sisma kv+", s.Kh, kv, Math.Abs(kv), WallSeismicPurpose.None));
            else
                foreach (var (purpose, kh, magnitude) in new[] { (WallSeismicPurpose.General, s.Kh, s.Kv), (WallSeismicPurpose.Overturning, s.KhOverturning, s.KvOverturning) })
                    foreach (double kv in new[] { -magnitude, magnitude }.Distinct()) list.Add(($"SLV {Label(purpose)} kv{(kv < 0 ? "−" : "+")}", kh, kv, magnitude, purpose));
            return list;
        }
    }
}
