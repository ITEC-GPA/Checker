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
    /// the verticals with γb, γs (γst in tension) and the group efficiency; actions with the factored weight of the pile.
    /// </para>
    /// </summary>
    public static class AxialPileCapacity
    {
        /// <summary>K for loose and dense soil and the rule of μ, by installation (ANTHEA table).</summary>
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
        /// other piles 0.7, 0.7 − 0.008 (cu − 25), 0.35.
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
            if (!Positive(pile.UnitWeight)) throw new ArgumentException("Pile: positive unit weight is required.");
            if (!Finite(pile.CompressionAction) || !Finite(pile.TensionAction)) throw new ArgumentException("Pile: finite actions are required.");
            if (surveys.Count == 0) throw new ArgumentException("Add at least one investigated vertical.");
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
                    if (p.Behaviour == SoilBehaviour.Cohesive && falda && Math.Min(bottom, l) > zf && !layer.Soil.UndrainedShearStrength.HasValue)
                        throw new ArgumentException("Layer " + (i + 1) + " (" + layer.Soil.Name + "): cu is required for the undrained capacity.");
                    list.Add((layer.Soil, p, top, bottom));
                }
                strata.Add(list);
            }
            double max = Math.Min(l, strata.Min(s => s[s.Count - 1].Bottom));
            var depths = new SortedSet<double>(Enumerable.Range(0, 101).Select(i => max * i / 100));
            foreach (var list in strata) foreach (var s in list) if (s.Bottom <= max) depths.Add(s.Bottom);
            if (falda && zf <= max) depths.Add(zf);
            for (int i = 0; i <= (int)(max / 500); i++) depths.Add(i * 500.0);
            double area = Math.PI * d * d / 4, perimeter = Math.PI * d;
            double Increment(Soil soil, double top, double bottom)
            {
                if (!falda) return soil.UnitWeight * (bottom - top);
                return soil.UnitWeight * Math.Max(0, Math.Min(bottom, zf) - top) + Math.Max(soil.SaturatedUnitWeight - gw, 0) * Math.Max(0, bottom - Math.Max(top, zf));
            }
            AxialSurveyResistance Resistances(List<(Soil Soil, AxialPileLayer Layer, double Top, double Bottom)> list, double z)
            {
                double effective = 0, total = 0, ld = 0, lu = 0; var tip = list[0]; var segments = new List<AxialShaftSegment>();
                foreach (var st in list)
                {
                    if (st.Top >= z) break;
                    double bottom = Math.Min(st.Bottom, z), length = bottom - st.Top; if (length <= 0) continue;
                    var soil = st.Soil; bool cohesive = st.Layer.Behaviour == SoilBehaviour.Cohesive;
                    double sigmaTop = effective, increment = Increment(soil, st.Top, bottom), mean = effective + increment / 2;
                    if (falda && st.Top < zf && zf < bottom)
                    {
                        double ds1 = Increment(soil, st.Top, zf), ds2 = Increment(soil, zf, bottom);
                        mean = effective + (ds1 / 2 * (zf - st.Top) + (ds1 + ds2 / 2) * (bottom - zf)) / length;
                    }
                    effective += increment;
                    double wet = falda ? Math.Max(0, bottom - Math.Max(st.Top, zf)) : 0;
                    total += soil.UnitWeight * (length - wet) + soil.SaturatedUnitWeight * wet; tip = st;
                    // In drained granular soil the effective-cohesion contribution is zero.
                    var (k, mu) = LateralCoefficients(pileInstallation, st.Layer.Density, soil.FrictionAngle);
                    double cohesion = cohesive ? soil.EffectiveCohesion : 0, friction = k * mu * mean;
                    bool active = st.Layer.ShaftActive; double td = active ? cohesion + friction : 0, tu; double? alpha = null; ld += perimeter * length * td;
                    if (cohesive)
                    {
                        double cu = soil.UndrainedShearStrength ?? 0; alpha = Alpha(pileInstallation, cu); tu = alpha.Value * cu;
                        double dryBottom = Math.Min(bottom, Math.Max(st.Top, falda ? zf : bottom)), dryLength = dryBottom - st.Top;
                        if (dryLength == length) tu = td;
                        else if (dryLength > 0)
                        {
                            double dryMean = sigmaTop + Increment(soil, st.Top, dryBottom) / 2, dryTau = cohesion + k * mu * dryMean;
                            tu = (dryTau * dryLength + tu * (length - dryLength)) / length;
                        }
                    }
                    else tu = td;
                    if (!active) tu = 0; lu += perimeter * length * tu;
                    segments.Add(new AxialShaftSegment(st.Top, bottom, mean, effective, k, mu, td, tu, alpha, perimeter * length * td, perimeter * length * tu, active));
                    if (bottom >= z) break;
                }
                double phi = tip.Soil.FrictionAngle;
                var nq = z > 0 ? BearingCapacityFactors.Nq(phi, z / d, d > BearingCapacityFactors.LargeDiameter, pile.BaseFrictionAngle) : null;
                double bd = area * effective * (nq?.Nq ?? 0), bu = bd; double? nc = null;
                if (tip.Layer.Behaviour == SoilBehaviour.Cohesive)
                {
                    nc = tip.Layer.Nc;
                    // Gross formulation: total σv, distinct from the buoyancy of the actions.
                    bu = area * (nc.Value * (tip.Soil.UndrainedShearStrength ?? 0) + total);
                    if (!falda || z <= zf) bu = bd;
                }
                return new AxialSurveyResistance(segments.ToArray(), effective, phi, total, nq, nc, bd, ld, bu, lu);
            }
            double Weight(double z)
            {
                double weight = pile.UnitWeight * area * z;
                if (falda && pile.Buoyancy) weight -= gw * area * Math.Max(0, z - zf);
                return Math.Max(weight, 0);
            }
            var warnings = new List<string>();
            if (efficiency.Method == PileGroupMethod.Feld || efficiency.Method == PileGroupMethod.ConverseLabarre) warnings.Add("Scelta progettuale 2026-09-15: efficienza geometrica applicata sia a compressione sia a trazione.");
            var result = Assemble(depths, z => strata.Select(s => Resistances(s, z)).ToArray(), Weight, new[] { AxialCondition.Drained, AxialCondition.Undrained },
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
            if (traces.Any(t => t!.SlendernessClipped)) warnings.Add("ATTENZIONE Nq: alcune quote hanno z/D fuori dall'intervallo " + (d > BearingCapacityFactors.LargeDiameter ? "4–32" : "5–50") + "; adottata la curva di bordo, senza estrapolare.");
            result.MaximumDepth = max; result.PileLength = l; result.FullCoverage = max >= l - 1e-6; result.SurveyCount = strata.Count; result.Warnings = warnings;
            return result;
        }

        /// <summary>Axial capacity of a grouted micropile along its axis (Bustamante-Doix); the profiles give the vertical thicknesses, s = z / cos θ.</summary>
        public static AxialCapacityResult<MicropileSurveyResistance> Calculate(Micropile pile, IReadOnlyList<MicropileSurvey> surveys, PileResistanceFactors factors, PileGroupEfficiency efficiency)
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
            var strata = new List<MicropileLayer[]>();
            foreach (var survey in surveys)
            {
                if (survey == null) throw new ArgumentNullException(nameof(surveys));
                var list = MicropileLayer.FromProfile(survey.Profile, survey.Layers, pile.Inclination);
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
            var result = Assemble(depths, z => strata.Select(s => Resistances(s, z)).ToArray(), z => weight.Total * z * cos, new[] { AxialCondition.Drained },
                (s, c) => (s.Shaft, s.Base), true, pct, pile.CompressionAction, pile.TensionAction, factors, efficiency);
            var warnings = new List<string>();
            if (efficiency.Method == PileGroupMethod.Feld || efficiency.Method == PileGroupMethod.ConverseLabarre) warnings.Add("Scelta progettuale 2026-09-15: efficienza geometrica applicata sia a compressione sia a trazione.");
            warnings.Add("Strati orizzontali; θ dalla verticale. L, sb e coordinate delle curve sono lungo l'asse; z = s cos θ. Azioni inserite assiali; peso proprio proiettato sull'asse. Verifiche trasversali non comprese.");
            warnings.Add(BustamanteDoix.Source + ". Abachi digitalizzati dalla scansione: letture approssimate, senza estrapolazione.");
            if (sb == 0 && pile.Injection == MicropileInjection.IRS) warnings.Add("Scelta di progetto: IRS applicato anche nei primi 5 m, in deroga alla raccomandazione IGU superficiale di Viggiani p. 396.");
            if (l - sb < 4000) warnings.Add("ATTENZIONE: zona iniettata inferiore ai 4 m raccomandati.");
            warnings.Add("Ipotesi progettuale autorizzata: p_l = p_i. La pressione di iniezione generale alimenta gli abachi di tutti gli strati; non è una misura Ménard del terreno.");
            warnings.Add("Verificare le condizioni esecutive di p. 392 e i volumi minimi di miscela della tabella 13.12; il programma non li certifica.");
            result.MaximumDepth = max; result.PileLength = l; result.FullCoverage = max >= l - 1e-6; result.SurveyCount = strata.Count; result.Warnings = warnings;
            return result;
        }

        private static AxialCapacityResult<TSurvey> Assemble<TSurvey>(SortedSet<double> depths, Func<double, TSurvey[]> resistances, Func<double, double> weightAt,
            AxialCondition[] conditions, Func<TSurvey, AxialCondition, (double Shaft, double Base)> select, bool micro, double pct, double? compressionAction, double? tensionAction,
            PileResistanceFactors f, PileGroupEfficiency eff)
        {
            var curves = new Dictionary<(AxialCondition, bool), (List<(double, double)> Mean, List<(double, double)> Min, List<(double, double)> Design)>();
            foreach (var c in conditions) foreach (bool compression in new[] { true, false }) curves[(c, compression)] = (new List<(double, double)>(), new List<(double, double)>(), new List<(double, double)>());
            var details = new List<AxialDepthResult<TSurvey>>(); var nc = new List<(double, double)>(); var nt = new List<(double, double)>();
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
                if (compressionAction.HasValue) nc.Add((z, compressionAction.Value + f.WeightUnfavourable * weight));
                if (tensionAction.HasValue) nt.Add((z, Math.Max(0, tensionAction.Value - f.WeightFavourable * weight)));
            }
            return new AxialCapacityResult<TSurvey>
            {
                Curves = curves.ToDictionary(p => p.Key, p => new AxialCapacityCurve(p.Value.Mean, p.Value.Min, p.Value.Design)), Depths = details,
                CompressionActions = nc, TensionActions = nt, Efficiency = eff, Factors = f
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
        private static bool Finite(double? v) => !v.HasValue || !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);
    }
}
