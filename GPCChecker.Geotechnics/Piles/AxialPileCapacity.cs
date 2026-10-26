using System.Globalization;
using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>
    /// Axial capacity of piles and grouted micropiles along the depth, with several investigated verticals. Transferred from ANTHEA
    /// (Anthea.Calculations.Calcolo, port of calcolo.py/core.py, commit fe4652c; fixtures piles-vertical.jsonl): the sheet stays in ANTHEA as an
    /// adapter. Units: mm, N, MPa, N/mm³.
    /// <para>
    /// Piles: shaft τ = c' + K μ σ'v,mean (drained; K by installation and density, μ = tan 20°, tan(3φ'/4) or tan φ'), undrained τ = α(cu) cu below
    /// the water table and the drained τ above it; base A σ'v Nq (Nq of <see cref="BearingCapacityFactors"/> with z/D and φ' of the layer or reduced by
    /// Kishida, <see cref="AxialPile.BaseFrictionAngle"/>; the shaft keeps the φ' of the layer) and, undrained below the water
    /// table, A (Nc cu + σv). Micropiles: Bustamante-Doix shaft along the axis and a base share of 0-15%. Design curves: mean/ξ3 and minimum/ξ4 of
    /// the verticals with γb, γs (γst in tension) and the group efficiency; actions with the factored weight of the pile by condition and direction
    /// (tension with the buoyant weight, undrained compression with the gross base with the full weight, drained compression as chosen).
    /// </para>
    /// </summary>
    public static class AxialPileCapacity
    {
        /// <summary>
        /// K for loose and dense soil and the rule of μ, by installation: C. Viggiani, Fondazioni, Tab. 13.2 (checked on the scan; bored piles 0.5
        /// loose and 0.4 dense as printed).
        /// </summary>
        public static (double Loose, double Dense, string Mu) ShaftCoefficients(PileInstallation installation)
        {
            switch (installation)
            {
                case PileInstallation.DrivenSteelSection: return (.7, 1, "tan20");
                case PileInstallation.DrivenClosedSteelTube: return (1, 2, "tan20");
                case PileInstallation.DrivenPrecastConcrete: return (1, 2, "tan3phi4");
                case PileInstallation.DrivenCastInPlace: return (1, 3, "tanphi");
                case PileInstallation.Bored: return (.5, .4, "tanphi");
                case PileInstallation.ContinuousFlightAuger: return (.7, .9, "tanphi");
                default: throw new ArgumentOutOfRangeException(nameof(installation));
            }
        }

        /// <summary>K and μ of a layer: μ = tan 20° for steel, tan(3φ'/4) for precast concrete, tan φ' otherwise (φ' in rad).</summary>
        public static (double K, double Mu) LateralCoefficients(PileInstallation installation, SoilDensity density, double frictionAngle)
        {
            var p = ShaftCoefficients(installation);
            double k = density == SoilDensity.Loose ? p.Loose : p.Dense;
            double mu = p.Mu == "tan20" ? Math.Tan(20 * Math.PI / 180) : Math.Tan((p.Mu == "tan3phi4" ? 3 * (frictionAngle / SoilUnits.Degree) / 4 : frictionAngle / SoilUnits.Degree) * Math.PI / 180);
            return (k, mu);
        }

        /// <summary>
        /// Adhesion factor α of the undrained shaft (cu in MPa, the rule in kPa): driven piles 1 up to 25 kPa, 1 − 0.0111 (cu − 25) up to 70 kPa, then 0.5;
        /// other piles 0.7, 0.7 − 0.008 (cu − 25), 0.35. Viggiani, Fondazioni, Tab. 13.3 (checked on the scan) prints 0.011 for the driven piles:
        /// ANTHEA uses 0.0111, continuous at 70 kPa (0.5005 instead of 0.505), lower by at most 0.9% (safe side), kept.
        /// </summary>
        public static double Alpha(PileInstallation installation, double undrainedShearStrength)
        {
            double cu = undrainedShearStrength / SoilUnits.KiloPascal;
            bool driven = installation != PileInstallation.Bored && installation != PileInstallation.ContinuousFlightAuger;
            return driven ? (cu <= 25 ? 1 : cu < 70 ? 1 - .0111 * (cu - 25) : .5) : (cu <= 25 ? .7 : cu < 70 ? .7 - .008 * (cu - 25) : .35);
        }

        /// <summary>Axial capacity of a pile on the investigated verticals (they must share the water table).</summary>
        public static AxialCapacityResult<AxialSurveyResistance> Calculate(AxialPile pile, IReadOnlyList<AxialPileSurvey> surveys, PileResistanceFactors factors, PileGroupEfficiency efficiency)
        {
            if (pile == null) throw new ArgumentNullException(nameof(pile));
            if (surveys == null) throw new ArgumentNullException(nameof(surveys));
            if (factors == null) throw new ArgumentNullException(nameof(factors));
            if (efficiency == null) throw new ArgumentNullException(nameof(efficiency));
            double d = pile.Diameter, l = pile.Length;
            if (!Positive(d) || !Positive(l)) throw new ArgumentException("Pile: positive diameter and length are required.");
            if (!NonNegative(pile.UnitWeight)) throw new ArgumentException("Pile: a non-negative unit weight is required.");
            if (!Finite(pile.CompressionAction) || !Finite(pile.TensionAction)) throw new ArgumentException("Pile: finite actions are required.");
            if (surveys.Count == 0) throw new ArgumentException("Add at least one investigated vertical.");
            // Head below the ground surface (request of 19/10/2026): ground depths of the profiles, results from the head; d0 = 0 is the former calculation.
            double d0 = pile.HeadDepth;
            if (!NonNegative(d0)) throw new ArgumentException("Pile: the depth of the head below the ground surface must be zero or positive.");
            var water = Water(surveys.Select(s => s?.Profile ?? throw new ArgumentNullException(nameof(surveys))));
            bool falda = water.HasValue; double zf = water?.Depth ?? 0, gw = water?.UnitWeight ?? SoilUnits.WaterUnitWeight;
            var pileInstallation = pile.Installation;
            var strata = new List<List<(Soil Soil, AxialPileLayer Layer, double Top, double Bottom)>>();
            foreach (var survey in surveys)
            {
                var list = new List<(Soil, AxialPileLayer, double, double)>();
                for (int i = 0; i < survey.Profile.Layers.Count; i++)
                {
                    var layer = survey.Profile.Layers[i]; var p = survey.Layers[i];
                    double top = survey.Profile.GroundSurface - layer.Top, bottom = survey.Profile.GroundSurface - layer.Bottom;
                    // cu only where it is used: a cohesive layer crossed by the pile (top < d0 + L, bottom > d0) below the water table (PV-2).
                    if (p.Behaviour == SoilBehaviour.Cohesive && falda && top < d0 + l && bottom > d0 && Math.Min(bottom, d0 + l) > Math.Max(zf, d0) && !layer.Soil.UndrainedShearStrength.HasValue)
                        throw new ArgumentException("Layer " + (i + 1) + " (" + layer.Soil.Name + "): cu is required for the undrained capacity.");
                    list.Add((layer.Soil, p, top, bottom));
                }
                strata.Add(list);
            }
            // Depths from the head (z = ground depth − d0).
            double max = Math.Min(l, strata.Min(s => s[s.Count - 1].Bottom) - d0);
            if (!(max > 0)) throw new ArgumentException("The stratigraphy does not reach below the pile head.");
            var depths = new SortedSet<double>(Enumerable.Range(0, 101).Select(i => max * i / 100));
            foreach (var list in strata) foreach (var s in list) if (s.Bottom - d0 > 0 && s.Bottom - d0 <= max) depths.Add(s.Bottom - d0);
            if (falda && zf - d0 > 0 && zf - d0 <= max) depths.Add(zf - d0);
            for (int i = 0; i <= (int)(max / 500); i++) depths.Add(i * 500.0);
            double area = Math.PI * d * d / 4, perimeter = Math.PI * d;
            double Increment(Soil soil, double top, double bottom)
            {
                if (!falda) return soil.UnitWeight * (bottom - top);
                return soil.UnitWeight * Math.Max(0, Math.Min(bottom, zf) - top) + Math.Max(soil.SaturatedUnitWeight - gw, 0) * Math.Max(0, bottom - Math.Max(top, zf));
            }
            AxialSurveyResistance Resistances(List<(Soil Soil, AxialPileLayer Layer, double Top, double Bottom)> list, double z)
            {
                // z from the head; zg = d0 + z below the ground surface. The layers above the head add their weight and no shaft.
                double zg = d0 + z;
                double effective = 0, total = 0, ld = 0, lu = 0; var tip = list[0]; var segments = new List<AxialShaftSegment>();
                foreach (var st in list)
                {
                    if (st.Top >= zg) break;
                    double bottom = Math.Min(st.Bottom, zg), length = bottom - st.Top; if (length <= 0) continue;
                    var soil = st.Soil; bool cohesive = st.Layer.Behaviour == SoilBehaviour.Cohesive;
                    double wet = falda ? Math.Max(0, bottom - Math.Max(st.Top, zf)) : 0;
                    double layerTotal = soil.UnitWeight * (length - wet) + soil.SaturatedUnitWeight * wet;
                    if (bottom <= d0) { effective += Increment(soil, st.Top, bottom); total += layerTotal; continue; }
                    // The shaft of the layer starts at the head when the head is inside the layer.
                    double start = Math.Max(st.Top, d0), shaft = bottom - start;
                    if (start > st.Top) effective += Increment(soil, st.Top, start);
                    double sigmaTop = effective, increment = Increment(soil, start, bottom), mean = effective + increment / 2;
                    if (falda && start < zf && zf < bottom)
                    {
                        double ds1 = Increment(soil, start, zf), ds2 = Increment(soil, zf, bottom);
                        mean = effective + (ds1 / 2 * (zf - start) + (ds1 + ds2 / 2) * (bottom - zf)) / shaft;
                    }
                    effective += increment; length = shaft;
                    total += layerTotal; tip = st;
                    // In drained granular soil the effective-cohesion contribution is zero.
                    var (k, mu) = LateralCoefficients(pileInstallation, st.Layer.Density, soil.FrictionAngle);
                    double cohesion = cohesive ? soil.EffectiveCohesion : 0, friction = k * mu * mean;
                    bool active = st.Layer.ShaftActive; double td = active ? cohesion + friction : 0, tu; double? alpha = null; ld += perimeter * length * td;
                    if (cohesive)
                    {
                        double cu = soil.UndrainedShearStrength ?? 0; alpha = Alpha(pileInstallation, cu); tu = alpha.Value * cu;
                        double dryBottom = Math.Min(bottom, Math.Max(start, falda ? zf : bottom)), dryLength = dryBottom - start;
                        if (dryLength == length) tu = td;
                        else if (dryLength > 0)
                        {
                            double dryMean = sigmaTop + Increment(soil, start, dryBottom) / 2, dryTau = cohesion + k * mu * dryMean;
                            tu = (dryTau * dryLength + tu * (length - dryLength)) / length;
                        }
                    }
                    else tu = td;
                    if (!active) tu = 0; lu += perimeter * length * tu;
                    segments.Add(new AxialShaftSegment(start - d0, bottom - d0, mean, effective, k, mu, td, tu, alpha, perimeter * length * td, perimeter * length * tu, active));
                    if (bottom >= zg) break;
                }
                double phi = tip.Soil.FrictionAngle;
                // Nq with the depth of the base below the ground surface (z/D of the curves; safe side, Nq decreases with z/D).
                var nq = z > 0 ? BearingCapacityFactors.Nq(phi, zg / d, d > BearingCapacityFactors.LargeDiameter, pile.BaseFrictionAngle) : null;
                double bd = area * effective * (nq?.Nq ?? 0), bu = bd; double? nc = null;
                if (tip.Layer.Behaviour == SoilBehaviour.Cohesive)
                {
                    nc = tip.Layer.Nc;
                    // Gross formulation: total σv, distinct from the buoyancy of the actions.
                    bu = area * (nc.Value * (tip.Soil.UndrainedShearStrength ?? 0) + total);
                    if (!falda || zg <= zf) bu = bd;
                }
                return new AxialSurveyResistance(segments.ToArray(), effective, phi, total, nq, nc, bd, ld, bu, lu);
            }
            // Weight of the pile: full W = γ A z, buoyant W' = W − γw A max(0, z − zf) (never negative); W' only with a water table.
            double FullWeight(double z) => pile.UnitWeight * area * z;
            double BuoyantWeight(double z) => Math.Max(0, FullWeight(z) - (falda ? gw * area * Math.Max(0, d0 + z - Math.Max(zf, d0)) : 0));
            // Drained compression: the choice of the user (full W is on the safe side, W' is exact).
            double Weight(double z) => pile.Buoyancy ? BuoyantWeight(z) : FullWeight(z);
            // Weight in the actions by condition and direction (PV-1): tension always W' (the water pushes the base up); undrained compression with
            // the gross base A (Nc cu + σv) of a cohesive tip below the water table always W, because σv already holds the water pressure at the base.
            double ActionWeight(double z, AxialSurveyResistance[] tips, AxialCondition condition, bool compression)
                => !compression ? BuoyantWeight(z) : condition == AxialCondition.Undrained && falda && d0 + z > zf && tips.Any(s => s.Nc.HasValue) ? FullWeight(z) : Weight(z);
            var warnings = new List<string>();
            if (efficiency.Method == PileGroupMethod.Feld || efficiency.Method == PileGroupMethod.ConverseLabarre) warnings.Add("Scelta progettuale 2026-09-15: efficienza geometrica applicata sia a compressione sia a trazione.");
            if (pile.UnitWeight == 0) warnings.Add("Peso specifico del palo nullo: peso proprio non considerato nelle azioni.");
            var result = Assemble(depths, z => strata.Select(s => Resistances(s, z)).ToArray(), Weight, ActionWeight, new[] { AxialCondition.Drained, AxialCondition.Undrained },
                (s, c) => c == AxialCondition.Drained ? (s.DrainedShaft, s.DrainedBase) : (s.UndrainedShaft, s.UndrainedBase), false, 0, pile.CompressionAction, pile.TensionAction, factors, efficiency);
            warnings.Add("Ipotesi progettuale 2026-09-15: nella verifica non drenata la porzione sopra falda è drenata; senza falda le due verifiche coincidono.");
            string rule = pile.BaseFrictionAngle == NqFrictionAngle.Layer ? " φ non ridotto."
                : pile.BaseFrictionAngle == NqFrictionAngle.KishidaDriven ? " φ' alla punta ridotto secondo Kishida (1967) per pali battuti: φ' = (φ'1 + 40°)/2 (Viggiani, Fondazioni, §13.1.2)."
                : " φ' alla punta ridotto secondo Kishida (1967) per pali trivellati: φ' = φ'1 − 3° (Viggiani, Fondazioni, §13.1.2).";
            warnings.Add("Nq: Parametrizzata " + BearingCapacityFactors.Version + "; " + (d > BearingCapacityFactors.LargeDiameter ? "Nq* per D > 0,80 m." : "Nq per D ≤ 0,80 m.") + rule);
            if (pile.BaseFrictionAngle != NqFrictionAngle.Layer && pile.BaseFrictionAngle != BearingCapacityFactors.Kishida(pileInstallation))
                warnings.Add("ATTENZIONE Nq: la regola di Kishida scelta (" + (pile.BaseFrictionAngle == NqFrictionAngle.KishidaDriven ? "pali battuti" : "pali trivellati") +
                    ") non corrisponde alla tecnologia del palo.");
            var traces = result.Depths.SelectMany(x => x.Surveys).Select(s => s.Nq).Where(n => n != null).ToArray();
            if (traces.Any(t => t!.FrictionAngleClipped)) warnings.Add("ATTENZIONE Nq: φ fuori dal tratto visibile di almeno una curva; adottato il bordo. Vedere le φ adottate nel dettaglio export.");
            // z/D outside the curves (PV-3): beyond the upper bound, at the deepest depths and so at the tip, the border curve overestimates Nq (it
            // decreases with z/D): warning with the depth and the value; below the lower bound, near the head, the border is on the safe side.
            var it = CultureInfo.GetCultureInfo("it-IT");
            bool large = d > BearingCapacityFactors.LargeDiameter; double lowRatio = large ? 4 : 5, highRatio = large ? 32 : 50;
            var deep = result.Depths.SelectMany(x => x.Surveys.Where(s => s.Nq != null && s.Nq.Slenderness > s.Nq.AdoptedSlenderness).Select(s => (x.Depth, Nq: s.Nq!))).ToArray();
            bool shallow = traces.Any(t => t!.Slenderness < t.AdoptedSlenderness);
            string shallowText = $"nel tratto superficiale con z/D < {lowRatio.ToString(it)} (z < {(lowRatio * d / SoilUnits.Metre).ToString("0.00", it)} m) adottata la curva z/D = {lowRatio.ToString(it)}, "
                + "senza estrapolare: a favore di sicurezza (Nq cresce al diminuire di z/D).";
            if (deep.Length > 0)
            {
                double last = deep.Max(x => x.Depth); var atLast = deep.Where(x => x.Depth == last).Select(x => x.Nq).ToArray();
                double nqMin = atLast.Min(x => x.Nq), nqMax = atLast.Max(x => x.Nq);
                string value = nqMin == nqMax ? nqMax.ToString("0.00", it) : nqMin.ToString("0.00", it) + "–" + nqMax.ToString("0.00", it);
                warnings.Add($"ATTENZIONE {atLast[0].Factor}: oltre z/D = {highRatio.ToString(it)} (z > {(highRatio * d / SoilUnits.Metre).ToString("0.00", it)} m) adottata la curva di bordo z/D = "
                    + $"{highRatio.ToString(it)}, senza estrapolare; {(last >= l - 1e-6 ? "alla punta" : "alla quota massima")} z = {(last / SoilUnits.Metre).ToString("0.00", it)} m, "
                    + $"z/D = {atLast[0].Slenderness.ToString("0.0", it)}: {atLast[0].Factor} = {value}, valore non cautelativo (Nq diminuisce al crescere di z/D)."
                    + (shallow ? " Inoltre " + shallowText : ""));
            }
            else if (shallow) warnings.Add("Nq: " + shallowText);
            result.MaximumDepth = max; result.PileLength = l; result.FullCoverage = max >= l - 1e-6; result.SurveyCount = strata.Count; result.Warnings = warnings;
            return result;
        }

        /// <summary>Axial capacity of a grouted micropile along its axis (Bustamante-Doix); the profiles give the vertical thicknesses, s = z / cos θ.</summary>
        public static AxialCapacityResult<MicropileSurveyResistance> Calculate(Micropile pile, IReadOnlyList<MicropileSurvey> surveys, PileResistanceFactors factors, PileGroupEfficiency efficiency)
            => CalculateMicropile(pile, surveys, factors, efficiency, s => MicropileLayer.FromProfile(s.Profile, s.Layers, pile.Inclination));

        /// <summary>Same micropile calculation from vertical thicknesses and Bustamante–Doix parameters only.</summary>
        public static AxialCapacityResult<MicropileSurveyResistance> CalculateFromDepths(Micropile pile, IReadOnlyList<MicropileDepthSurvey> surveys, PileResistanceFactors factors, PileGroupEfficiency efficiency)
            => CalculateMicropile(pile, surveys, factors, efficiency, s => s.AlongAxis(pile.Inclination));

        private static AxialCapacityResult<MicropileSurveyResistance> CalculateMicropile<T>(Micropile pile, IReadOnlyList<T> surveys, PileResistanceFactors factors,
            PileGroupEfficiency efficiency, Func<T, MicropileLayer[]> axisLayers) where T : class
        {
            if (pile == null) throw new ArgumentNullException(nameof(pile));
            if (surveys == null) throw new ArgumentNullException(nameof(surveys));
            if (factors == null) throw new ArgumentNullException(nameof(factors));
            if (efficiency == null) throw new ArgumentNullException(nameof(efficiency));
            double cos = MicropileTube.AxisCosine(pile.Inclination);
            double d = pile.Diameter, l = pile.Length, sb = pile.GroutStart, pct = pile.BaseShare ?? 0;
            if (!Positive(d) || !Positive(l)) throw new ArgumentException("Micropile: positive diameter and length are required.");
            if (!Positive(pile.Pressure)) throw new ArgumentException("Micropile: positive injection pressure is required.");
            if (!Finite(pile.CompressionAction) || !Finite(pile.TensionAction)) throw new ArgumentException("Micropile: finite actions are required.");
            var weight = MicropileTube.Weight(pile.Tube, pile.TubeMaterial, d, pile.GroutUnitWeight);
            if (double.IsNaN(sb) || double.IsInfinity(sb) || sb < 0) throw new ArgumentException("Invalid start of the grouted length.");
            if (sb >= l) throw new ArgumentException("The grouted length must start before the tip.");
            if (double.IsNaN(pct) || pct < 0 || pct > 15) throw new ArgumentException("The base contribution must lie between 0% and 15% of the shaft resistance.");
            if (surveys.Count == 0) throw new ArgumentException("Add at least one investigated vertical.");
            // Head below the ground surface (request of 19/10/2026): the axis from the head starts d0 / cos θ below the ground along the axis; the soil
            // above has no shaft (Bustamante-Doix does not use σ'v). d0 = 0 leaves the layers as they are.
            double d0 = pile.HeadDepth;
            if (!(d0 >= 0) || double.IsInfinity(d0)) throw new ArgumentException("Micropile: the depth of the head below the ground surface must be zero or positive.");
            double s0 = d0 / cos;
            var strata = new List<MicropileLayer[]>();
            foreach (var survey in surveys)
            {
                if (survey == null) throw new ArgumentNullException(nameof(surveys));
                var list = axisLayers(survey);
                if (s0 > 0)
                {
                    list = list.Where(x => x.Bottom > s0).Select(x => new MicropileLayer(Math.Max(0, x.Top - s0), x.Bottom - s0, x.Soil, x.Alpha, x.ShaftActive)).ToArray();
                    if (list.Length == 0) throw new ArgumentException($"Vertical {strata.Count + 1}: the stratigraphy does not reach below the micropile head.");
                }
                try { BustamanteDoix.Segments(list, l, sb, d, pile.Injection, pile.Pressure); }
                catch (ArgumentException ex) { throw new ArgumentException($"Vertical {strata.Count + 1}: {ex.Message}"); }
                strata.Add(list);
            }
            double max = Math.Min(l, strata.Min(s => s[s.Length - 1].Bottom));
            var depths = new SortedSet<double>(Enumerable.Range(0, 101).Select(i => max * i / 100));
            foreach (var list in strata) foreach (var s in list) if (s.Bottom <= max) depths.Add(s.Bottom);
            for (int i = 0; i <= (int)(max / 500); i++) depths.Add(i * 500.0);
            if (sb <= max) depths.Add(sb);
            double perimeter = Math.PI * d;
            MicropileSurveyResistance Resistances(MicropileLayer[] list, double z)
            {
                // The source derives Db from perimeter/π in this step as well.
                var segments = BustamanteDoix.Segments(list, z, sb, perimeter / Math.PI, pile.Injection, pile.Pressure);
                double lateral = CompensatedSum(segments.Select(s => s.Lateral));
                return new MicropileSurveyResistance(segments, lateral, lateral * pct / 100);
            }
            var result = Assemble(depths, z => strata.Select(s => Resistances(s, z)).ToArray(), z => weight.Total * z * cos, (z, s, c, compression) => weight.Total * z * cos, new[] { AxialCondition.Drained },
                (s, c) => (s.Shaft, s.Base), true, pct, pile.CompressionAction, pile.TensionAction, factors, efficiency);
            var warnings = new List<string>();
            if (efficiency.Method == PileGroupMethod.Feld || efficiency.Method == PileGroupMethod.ConverseLabarre) warnings.Add("Scelta progettuale 2026-09-15: efficienza geometrica applicata sia a compressione sia a trazione.");
            warnings.Add("Strati orizzontali; θ dalla verticale. L, sb e coordinate delle curve sono lungo l'asse; z = s cos θ. Azioni inserite assiali; peso proprio proiettato sull'asse. Verifiche trasversali non comprese.");
            if (pile.GroutUnitWeight == 0) warnings.Add("Peso specifico della malta nullo: nel peso proprio entra solo il tubo.");
            warnings.Add(BustamanteDoix.Source + ". Abachi digitalizzati dalla scansione: letture approssimate, senza estrapolazione.");
            if (sb == 0 && pile.Injection == MicropileInjection.IRS) warnings.Add("Scelta di progetto: IRS applicato anche nei primi 5 m, in deroga alla raccomandazione IGU superficiale di Viggiani p. 396.");
            if (l - sb < 4000) warnings.Add("ATTENZIONE: zona iniettata inferiore ai 4 m raccomandati.");
            warnings.Add("Ipotesi progettuale autorizzata: p_l = p_i. La pressione di iniezione generale alimenta gli abachi di tutti gli strati; non è una misura Ménard del terreno.");
            warnings.Add("Verificare le condizioni esecutive di p. 392 e i volumi minimi di miscela della tabella 13.12; il programma non li certifica.");
            result.MaximumDepth = max; result.PileLength = l; result.FullCoverage = max >= l - 1e-6; result.SurveyCount = strata.Count; result.Warnings = warnings;
            return result;
        }

        private static AxialCapacityResult<TSurvey> Assemble<TSurvey>(SortedSet<double> depths, Func<double, TSurvey[]> resistances, Func<double, double> weightAt,
            Func<double, TSurvey[], AxialCondition, bool, double> actionWeight, AxialCondition[] conditions, Func<TSurvey, AxialCondition, (double Shaft, double Base)> select, bool micro, double pct, double? compressionAction, double? tensionAction,
            PileResistanceFactors f, PileGroupEfficiency eff)
        {
            var curves = new Dictionary<(AxialCondition, bool), (List<(double, double)> Mean, List<(double, double)> Min, List<(double, double)> Design)>();
            foreach (var c in conditions) foreach (bool compression in new[] { true, false }) curves[(c, compression)] = (new List<(double, double)>(), new List<(double, double)>(), new List<(double, double)>());
            var details = new List<AxialDepthResult<TSurvey>>(); var actions = new Dictionary<(AxialCondition, bool), List<(double, double)>>();
            foreach (var c in conditions) foreach (bool compression in new[] { true, false }) actions[(c, compression)] = new List<(double, double)>();
            foreach (double z in depths)
            {
                var surveys = resistances(z); double weight = weightAt(z); var components = new Dictionary<(AxialCondition, bool), AxialComponents>();
                foreach (var c in conditions)
                {
                    var values = surveys.Select(s => select(s, c)).ToArray();
                    double[] bases = values.Select(v => v.Base).ToArray(), sides = values.Select(v => v.Shaft).ToArray();
                    double bm = CompensatedSum(bases) / bases.Length, lm = CompensatedSum(sides) / sides.Length, bmin = bases.Min(), lmin = sides.Min();
                    // Micropile: the source derives the bases after the mean of the shafts.
                    if (micro) { bm = lm * pct / 100; bmin = lmin * pct / 100; }
                    components[(c, true)] = new AxialComponents(new AxialComponentBranch(lm, bm, f.Xi3, f.ShaftCompression, f.Base, eff.Compression),
                        new AxialComponentBranch(lmin, bmin, f.Xi4, f.ShaftCompression, f.Base, eff.Compression));
                    components[(c, false)] = new AxialComponents(new AxialComponentBranch(lm, 0, f.Xi3, f.ShaftTension, f.Base, eff.Tension),
                        new AxialComponentBranch(lmin, 0, f.Xi4, f.ShaftTension, f.Base, eff.Tension));
                    foreach (bool compression in new[] { true, false })
                    {
                        double mean, min;
                        if (compression)
                        {
                            mean = micro ? (lm / f.ShaftCompression + bm / f.Base) / f.Xi3 : bm / (f.Xi3 * f.Base) + lm / (f.Xi3 * f.ShaftCompression);
                            min = micro ? (lmin / f.ShaftCompression + bmin / f.Base) / f.Xi4 : bmin / (f.Xi4 * f.Base) + lmin / (f.Xi4 * f.ShaftCompression);
                            mean *= eff.Compression; min *= eff.Compression;
                        }
                        else { mean = lm / (f.Xi3 * f.ShaftTension) * eff.Tension; min = lmin / (f.Xi4 * f.ShaftTension) * eff.Tension; }
                        var curve = curves[(c, compression)]; curve.Mean.Add((z, mean)); curve.Min.Add((z, min)); curve.Design.Add((z, Math.Min(mean, min)));
                    }
                }
                details.Add(new AxialDepthResult<TSurvey>(z, weight, surveys, components));
                foreach (var c in conditions)
                {
                    if (compressionAction.HasValue) actions[(c, true)].Add((z, compressionAction.Value + f.WeightUnfavourable * actionWeight(z, surveys, c, true)));
                    if (tensionAction.HasValue) actions[(c, false)].Add((z, Math.Max(0, tensionAction.Value - f.WeightFavourable * actionWeight(z, surveys, c, false))));
                }
            }
            return new AxialCapacityResult<TSurvey>
            {
                Curves = curves.ToDictionary(p => p.Key, p => new AxialCapacityCurve(p.Value.Mean, p.Value.Min, p.Value.Design)), Depths = details,
                Actions = actions.ToDictionary(p => p.Key, p => (IReadOnlyList<(double Depth, double Value)>)p.Value),
                CompressionActions = actions[(AxialCondition.Drained, true)], TensionActions = actions[(AxialCondition.Drained, false)], Efficiency = eff, Factors = f
            };
        }

        private static (double Depth, double UnitWeight)? Water(IEnumerable<SoilProfile> profiles)
        {
            (double, double)? water = null; bool first = true;
            foreach (var p in profiles)
            {
                (double, double)? w = p.GroundwaterElevation.HasValue ? (p.GroundSurface - p.GroundwaterElevation.Value, p.WaterUnitWeight) : ((double, double)?)null;
                if (w.HasValue && w.Value.Item1 < 0) throw new ArgumentException("Water table above the ground surface: not supported.");
                if (!first && !Equals(w, water)) throw new ArgumentException("The investigated verticals must share the water table.");
                water = w; first = false;
            }
            return water;
        }

        /// <summary>Compensated (Neumaier) sum, as the legacy calculation.</summary>
        internal static double CompensatedSum(IEnumerable<double> values)
        {
            double sum = 0, correction = 0;
            foreach (var value in values) { var next = sum + value; correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum; sum = next; }
            return sum + correction;
        }

        private static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
        private static bool NonNegative(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0;
        private static bool Finite(double? v) => !v.HasValue || !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);
    }
}
