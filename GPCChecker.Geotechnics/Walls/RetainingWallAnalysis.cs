using GPC.Checkers.Geotechnics.Foundations;
using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>A band of the soil in front of the wall: elevations above the base (mm), total and effective unit weight (N/mm³), φ (rad), σ'v at the ends (MPa).</summary>
    public sealed class WallValleyBand
    {
        public double Top { get; }
        public double Bottom { get; }
        public double UnitWeight { get; }
        public double EffectiveUnitWeight { get; }
        public double FrictionAngle { get; }
        public double SigmaTop { get; }
        public double SigmaBottom { get; }
        internal WallValleyBand(double top, double bottom, double gamma, double effective, double phi, double sigmaTop, double sigmaBottom)
        { Top = top; Bottom = bottom; UnitWeight = gamma; EffectiveUnitWeight = effective; FrictionAngle = phi; SigmaTop = sigmaTop; SigmaBottom = sigmaBottom; }
    }

    /// <summary>Weight of a soil region (N/mm) and its static moments about the toe (x) and about the base (y), N·mm/mm.</summary>
    public sealed class WallSoilMass
    {
        public double Weight { get; }
        public double MomentX { get; }
        public double MomentY { get; }
        internal WallSoilMass(double weight, double mx, double my) { Weight = weight; MomentX = mx; MomentY = my; }
    }

    /// <summary>
    /// Equilibrium of a retaining wall per unit length, transferred from ANTHEA (RetainingWall.Calculate, geotechnical part and internal forces, commit
    /// fe4652c; fixtures walls-*.jsonl). Units of Model: mm, N/mm, N·mm/mm, MPa, N/mm³, rad.
    /// <para>
    /// Earth pressure: Rankine by layers on the vertical plane behind the heel (δ = 0; with the friction of the back when there is no heel), on the stem
    /// Coulomb with the friction of the back; effective stresses and water separate; seismic Mononobe-Okabe (increment at mid height) or simplified Wood
    /// (at rest, kh γ Ht² uniform); passive Rankine in front optional, limited to the driving thrust, never seismic. Equilibrium about the toe with
    /// uplift, weights of the wall, of the fill on the heel and of the soil in front, actions; contact without tension; sliding μ N/γR, overturning
    /// stabilising/γR, bearing EN 1997-1 Annex D (c' = 0, q' of the soil in front, Nγ = 2(Nq − 1) tan φ, iq = (1 − H/V)², iγ = (1 − H/V)³, rough base
    /// δb ≥ φd/2, |e| ≤ B/3) and in the seismic combinations EN 1998-5 Annex F. Internal forces of the stem at 21 cuts (and at the loads, the change of
    /// the reinforcement and the valley bands) and of the toe and heel at 21 cuts.
    /// </para>
    /// </summary>
    public static class RetainingWallAnalysis
    {
        private const double Gw = SoilUnits.WaterUnitWeight;

        /// <summary>Equilibrium of the wall in the combinations and the geotechnical checks.</summary>
        public static WallResult Calculate(WallInput input, IEnumerable<WallCombination> combinations, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var rows = (combinations ?? throw new ArgumentNullException(nameof(combinations))).ToArray();
            Validate(input, rows);
            var g = input.Geometry;
            double h = g.Height, t = g.Slab, a = g.Toe, s = g.StemBase, top = g.StemTop, heel = g.Heel, width = g.Width, ht = g.TotalHeight, gc = input.UnitWeight;
            bool wood = input.Seismic?.Method == WallSeismicMethod.Wood;
            bool water = input.Water != null; double zw = water ? input.Water!.Depth : double.PositiveInfinity, front = water ? input.Water!.FrontHead : 0;
            // Stem: trapezoid with the vertical back at a + s.
            double stemArea = (s + top) * h / 2;
            double stemX = (a + s) - (s * s + s * top + top * top) / (3 * (s + top)), stemY = t + h * (s + 2 * top) / (3 * (s + top));
            double stemWeight = stemArea * gc, slabWeight = width * t * gc;
            // Bands of the fill, depths below the top: cuts at the water table, at the top of the slab and at the front head.
            var weights = new List<WallPressureSegment>(); var soil = new List<(double Z0, double Z1, double Gamma, double Effective, double Phi)>();
            foreach (var layer in input.Backfill.Layers)
            {
                double start = Math.Max(0, ht - layer.Top), end = Math.Min(ht, ht - layer.Bottom); if (end <= start) continue;
                var cuts = Distinct(new[] { start, end, zw, h, ht - front }.Where(z => z >= start - Tolerance && z <= end + Tolerance).OrderBy(z => z));
                for (int i = 0; i < cuts.Length - 1; i++)
                {
                    double z0 = cuts[i], z1 = cuts[i + 1], gamma = (z0 + z1) / 2 >= zw ? layer.Soil.SaturatedUnitWeight : layer.Soil.UnitWeight;
                    soil.Add((z0, z1, gamma, gamma - ((z0 + z1) / 2 >= zw ? Gw : 0), layer.Soil.FrictionAngle));
                    weights.Add(new WallPressureSegment(z0, z1, gamma, gamma));
                }
            }
            var soilIntegral = WallPressureSegment.Integrate(weights, 0, h, ht); double soilWeight = soilIntegral.Force * heel;
            double soilHeight = soilWeight > 0 ? -soilIntegral.Moment * heel / soilWeight : 0;
            double gammaF = water ? input.Foundation.SaturatedUnitWeight - Gw : input.Foundation.UnitWeight;
            var valleyBands = ValleyBands(input); var frontMass = FrontSoilMass(g, valleyBands);
            double frontColumn = valleyBands.Sum(b => Math.Max(0, b.Top - Math.Max(t, b.Bottom)) * b.UnitWeight);
            double overburden = valleyBands.Count > 0 ? valleyBands[valleyBands.Count - 1].SigmaBottom : 0;
            var stations = Enumerable.Range(0, 21).Select(i => h * i / 20).ToList();
            foreach (var act in input.Actions.Where(x => x.Enabled && x.Type != WallActionType.UniformSurcharge)) { double zz = ht - act.Z; stations.Add(zz); if (zz > 0) stations.Add(Math.Max(0, zz - 1e-4)); }
            if (input.ReinforcementSplit.HasValue) { double zz = h - input.ReinforcementSplit.Value; stations.Add(zz); stations.Add(Math.Max(0, zz - 1e-4)); }
            stations.AddRange(valleyBands.SelectMany(b => new[] { ht - b.Top, ht - b.Bottom }).Where(z => z > 0 && z < h));
            var stemCuts = Distinct(stations.OrderBy(z => z));

            WallCaseResult Case(WallCombination c)
            {
                token.ThrowIfCancellationRequested();
                double fc = c.Wall, fs = c.Soil, fw = c.Water, fp = c.ValleySoil, kh = c.Kh, kv = c.Kv, vf = 1 - kv, sigma = 0, mf = c.FrictionFactor;
                bool seismic = c.State == WallLimitState.Seismic;
                double phiF = EarthPressure.Design(input.Foundation.FrictionAngle, mf);
                double factorNq = Math.Exp(Math.PI * Math.Tan(phiF)) * Math.Pow(Math.Tan(Math.PI / 4 + phiF / 2), 2), ng = 2 * (factorNq - 1) * Math.Tan(phiF);
                var actions = input.Actions.Where(x => x.Enabled).Select(x => new WallAppliedAction(x, c.Coefficients.TryGetValue(x.Id, out double f) ? f : 0)).ToList();
                double q = actions.Where(x => x.Action.Type == WallActionType.UniformSurcharge).Sum(x => x.Action.Value * x.Factor);
                var horizontalActions = actions.Where(x => x.Action.Type == WallActionType.HorizontalForce || x.Action.Type == WallActionType.Impact).ToArray();
                var verticalActions = actions.Where(x => x.Action.Type == WallActionType.VerticalForce).ToArray();
                var momentActions = actions.Where(x => x.Action.Type == WallActionType.Moment).ToArray();
                var pressures = new List<WallPressureSegment>(); var stemPressures = new List<WallPressureSegment>(); var verticalFriction = new List<WallPressureSegment>();
                var details = new List<WallPressureDetail>(); var stemDetails = new List<WallPressureDetail>();
                double deltaWall = wood ? 0 : input.Back.Design(mf), deltaBase = input.Base.Design(mf);
                foreach (var z in soil)
                {
                    double phi = EarthPressure.Design(z.Phi, mf), virtualDelta = heel > 0 ? 0 : deltaWall, k = wood ? EarthPressure.AtRest(phi) : EarthPressure.ActiveHorizontal(phi, virtualDelta), ke = k;
                    double sig0 = sigma * fs * vf, sig1 = (sigma + z.Effective * (z.Z1 - z.Z0)) * fs * vf, dynamic = 0, surcharge = k * q;
                    if (seismic)
                    {
                        ke = wood ? k : EarthPressure.ActiveHorizontal(phi, virtualDelta, kh, kv);
                        dynamic = fs * (wood ? kh * z.Gamma * ht : (ke - k) * vf * z.Gamma * ht / 2);
                        surcharge = (wood ? k : ke * vf) * q;
                    }
                    double w0 = fw * Gw * (Math.Max(0, z.Z0 - zw) - Math.Max(0, z.Z0 - (ht - front))), w1 = fw * Gw * (Math.Max(0, z.Z1 - zw) - Math.Max(0, z.Z1 - (ht - front)));
                    double p0 = k * sig0 + surcharge + dynamic + w0, p1 = k * sig1 + surcharge + dynamic + w1;
                    details.Add(new WallPressureDetail(z.Z0, z.Z1, z.Phi, phi, k, ke, sig0, sig1, k * sig0, k * sig1, surcharge, w0, w1, dynamic, p0, p1));
                    pressures.Add(new WallPressureSegment(z.Z0, z.Z1, p0, p1));
                    double ks = wood ? k : EarthPressure.ActiveHorizontal(phi, deltaWall), kes = seismic && !wood ? EarthPressure.ActiveHorizontal(phi, deltaWall, kh, kv) : ks;
                    double ds = seismic ? fs * (wood ? kh * z.Gamma * ht : (kes - ks) * vf * z.Gamma * ht / 2) : 0;
                    double qs = (seismic && !wood ? kes * vf : ks) * q;
                    double sp0 = ks * sig0 + qs + ds, sp1 = ks * sig1 + qs + ds;
                    stemPressures.Add(new WallPressureSegment(z.Z0, z.Z1, sp0 + w0, sp1 + w1));
                    stemDetails.Add(new WallPressureDetail(z.Z0, z.Z1, z.Phi, phi, ks, kes, sig0, sig1, ks * sig0, ks * sig1, qs, w0, w1, ds, sp0 + w0, sp1 + w1));
                    verticalFriction.Add(new WallPressureSegment(z.Z0, z.Z1, sp0 * Math.Tan(deltaWall), sp1 * Math.Tan(deltaWall)));
                    sigma += z.Effective * (z.Z1 - z.Z0);
                }
                foreach (var act in actions.Where(x => x.Action.Type == WallActionType.LateralPressure))
                { var p = new WallPressureSegment(ht - act.Action.Z, ht - act.Action.Z0, act.Design, act.Design); pressures.Add(p); stemPressures.Add(p); }
                var valleyPressures = new List<WallPressureSegment>();
                double eta = input.Valley.Passive && !seismic ? input.Valley.Mobilization : 0;
                foreach (var b in valleyBands)
                {
                    double kp = EarthPressure.RankinePassive(EarthPressure.Design(b.FrictionAngle, mf));
                    // A resistance is not amplified by an unfavourable permanent-action factor.
                    valleyPressures.Add(new WallPressureSegment(ht - b.Top, ht - b.Bottom, eta * kp * b.SigmaTop * Math.Min(1, fp) * vf, eta * kp * b.SigmaBottom * Math.Min(1, fp) * vf));
                }
                double availablePassive = WallPressureSegment.Integrate(valleyPressures, 0, ht, ht).Force;
                double activeDrive = WallPressureSegment.Integrate(pressures, 0, ht, ht).Force + horizontalActions.Sum(x => x.Design);
                double passiveScale = availablePassive > 0 ? Clamp(activeDrive / availablePassive, 0, 1) : 0;
                valleyPressures = valleyPressures.Select(p => new WallPressureSegment(p.Z0, p.Z1, p.P0 * passiveScale, p.P1 * passiveScale)).ToList();
                foreach (var p in valleyPressures) { pressures.Add(new WallPressureSegment(p.Z0, p.Z1, -p.P0, -p.P1)); stemPressures.Add(new WallPressureSegment(p.Z0, p.Z1, -p.P0, -p.P1)); }
                var thrust = WallPressureSegment.Integrate(pressures, 0, ht, ht);
                double rearHead = water ? ht - zw : 0, u0 = front * Gw * fw, u1 = rearHead * Gw * fw;
                double uplift = (u0 + u1) * width / 2, upliftMoment = width * width * (u0 + 2 * u1) / 6;
                double pointN = verticalActions.Sum(x => x.Design);
                double wStem = stemWeight * fc, wSlab = slabWeight * fc, wSoil = soilWeight * fs, live = heel * q + pointN;
                double backFriction = heel == 0 ? WallPressureSegment.Integrate(verticalFriction, 0, ht, ht).Force : 0;
                double normal = (wStem + wSlab + wSoil + frontMass.Weight * fp + live) * vf - uplift + backFriction;
                double inertial = kh * (wStem + wSlab + wSoil + frontMass.Weight * fp + live);
                double horizontal = thrust.Force + horizontalActions.Sum(x => x.Design) + inertial;
                double stabilizing = (wStem * stemX + wSlab * width / 2 + (wSoil + heel * q) * (a + s + heel / 2) + verticalActions.Sum(x => x.Design * x.Action.X)) * vf;
                double overturning = -thrust.Moment + horizontalActions.Sum(x => x.Design * x.Action.Z) + momentActions.Sum(x => x.Design) + upliftMoment
                    + kh * (wStem * stemY + wSlab * t / 2 + wSoil * soilHeight + heel * q * ht + verticalActions.Sum(x => x.Design * x.Action.Z));
                stabilizing += frontMass.MomentX * fp * vf + backFriction * (a + s);
                overturning += kh * frontMass.MomentY * fp;
                // Uplift moment is included in overturning, and U is subtracted from the normal force.
                double x = normal > 0 ? (stabilizing - overturning) / normal : 0;
                double e = width / 2 - x, be = Math.Max(0, width - 2 * Math.Abs(e)); var contact = WallContact.Law(width, normal, x);
                double sliding = Math.Max(0, normal) * Math.Tan(deltaBase) / c.SlidingFactor;
                double inclination = normal > 0 ? Math.Max(0, 1 - Math.Abs(horizontal) / normal) : 0;
                bool roughBase = deltaBase + 1e-9 * SoilUnits.Degree >= phiF / 2;
                double? bearing = seismic || !roughBase || Math.Abs(e) > width / 3 || normal <= 0 ? (double?)null
                    : (overburden * be * factorNq * inclination * inclination + .5 * gammaF * be * be * ng * Math.Pow(inclination, 3)) / c.BearingFactor;
                SeismicBearingResult? seismicBearing = null; string seismicBearingError = "";
                if (seismic)
                {
                    if (!roughBase || !contact.Valid) seismicBearingError = "Fuori campo: base non ruvida o contatto non equilibrato";
                    else seismicBearing = SeismicBearing(input, width, gammaF, phiF, normal, horizontal, e, kv, c.BearingFactor, out seismicBearingError);
                    bearing = seismicBearing?.Capacity;
                }
                var sections = new List<WallSectionForce>();
                // Stem cuts: exact trapezoid weight and lever arm at each station; water above slab included.
                foreach (double z in stemCuts)
                {
                    double thick = top + (s - top) * z / h;
                    var f = WallPressureSegment.Integrate(stemPressures, 0, z, z);
                    double weight = gc * fc * z * (top + thick) / 2;
                    double xg = z == 0 ? a + s - top / 2 : a + s - (top * top + top * thick + thick * thick) / (3 * (top + thick));
                    double root = a + s - thick / 2;
                    var localN = verticalActions.Where(v => ht - v.Action.Z <= z).ToArray(); double localPointN = localN.Sum(v => v.Design);
                    double verticalMoment = vf * (weight * (xg - root) + localN.Sum(v => v.Design * (v.Action.X - root)));
                    double yi = z == 0 ? 0 : z * (2 * top + thick) / (3 * (top + thick));
                    double shear = f.Force + horizontalActions.Where(v => ht - v.Action.Z <= z).Sum(v => v.Design) + kh * (weight + localPointN);
                    double moment = -f.Moment + horizontalActions.Where(v => ht - v.Action.Z <= z).Sum(v => v.Design * (z - ht + v.Action.Z)) + momentActions.Where(v => ht - v.Action.Z <= z).Sum(v => v.Design)
                        + kh * (weight * yi + localN.Sum(v => v.Design * (z - ht + v.Action.Z))) - verticalMoment;
                    var wedge = FrontSoilMass(g, valleyBands, ht - z, true);
                    double friction = WallPressureSegment.Integrate(verticalFriction, 0, z, z).Force;
                    moment -= friction * (a + s - root) + (wedge.MomentX - wedge.Weight * root) * fp * vf;
                    moment += kh * fp * (wedge.MomentY - wedge.Weight * (ht - z)); shear += kh * fp * wedge.Weight;
                    sections.Add(new WallSectionForce(WallMember.Stem, z, thick, (weight + localPointN + wedge.Weight * fp) * vf + friction, moment, shear));
                }
                // Net upward reaction on the slab includes groundwater separately from effective soil contact.
                var upward = contact.Valid ? new List<WallPressureSegment> { new WallPressureSegment(contact.Start, contact.End, contact.Toe, contact.Heel) } : new List<WallPressureSegment>();
                upward.Add(new WallPressureSegment(0, width, u0, u1));
                foreach (var (member, l, r, downward) in new[] { (WallMember.Toe, 0d, a, (gc * t * fc + frontColumn * fp) * vf), (WallMember.Heel, a + s, width, (gc * t * fc + soilIntegral.Force * fs + q) * vf) })
                {
                    if (r <= l) continue;
                    var load = new List<WallPressureSegment>(upward) { new WallPressureSegment(l, r, -downward, -downward) };
                    for (int i = 0; i <= 20; i++)
                    {
                        double length = (r - l) * i / 20, cut = member == WallMember.Toe ? l + length : r - length;
                        var force = member == WallMember.Toe ? WallPressureSegment.Integrate(load, l, cut, cut) : WallPressureSegment.Integrate(load, cut, r, cut);
                        sections.Add(new WallSectionForce(member, length, t, 0, member == WallMember.Toe ? -force.Moment : force.Moment, force.Force));
                    }
                }
                return new WallCaseResult
                {
                    Combination = c, Horizontal = horizontal, Vertical = normal, Uplift = uplift, Stabilizing = stabilizing, Overturning = overturning, X = x, Eccentricity = e,
                    EffectiveWidth = be, Contact = contact, SlidingResistance = sliding, OverturningResistance = stabilizing / c.OverturningFactor, BearingResistance = bearing,
                    Pressures = pressures, StemPressures = stemPressures, ValleyPressures = valleyPressures, PressureDetails = details, StemPressureDetails = stemDetails, Actions = actions,
                    Sections = sections, SeismicBearing = seismicBearing, SeismicBearingError = seismicBearingError,
                    Soil = new WallSoilAudit
                    {
                        ValleyHeight = input.Valley.Height, FreeHeight = ht - input.Valley.Height, ValleyWeight = frontMass.Weight * fp * vf, ValleyMoment = frontMass.MomentX * fp * vf,
                        Overburden = overburden, WallFriction = deltaWall, BaseFriction = deltaBase, BaseFrictionCoefficient = Math.Tan(deltaBase), EquilibriumPlaneFriction = heel > 0 ? 0 : deltaWall,
                        PassiveAvailable = availablePassive, PassiveUsed = availablePassive * passiveScale, PassiveFraction = eta, PassiveScale = passiveScale, Nq = factorNq, Ngamma = ng,
                        Iq = inclination * inclination, Igamma = Math.Pow(inclination, 3), RoughBase = roughBase
                    }
                };
            }

            var cases = rows.Select(Case).ToList();
            var checks = new List<WallCheck>();
            foreach (var c in cases.Where(c => c.Combination.State == WallLimitState.Ultimate || c.Combination.State == WallLimitState.Seismic || c.Combination.State == WallLimitState.Exceptional))
            {
                string name = c.Combination.Name;
                if (c.Combination.Purpose == WallSeismicPurpose.Overturning) { checks.Add(WallCheck.Of(WallCheckKind.Overturning, name, c.Overturning, c.OverturningResistance)); continue; }
                checks.Add(WallCheck.Of(WallCheckKind.Sliding, name, Math.Abs(c.Horizontal), c.SlidingResistance));
                if (c.Combination.Purpose != WallSeismicPurpose.General) checks.Add(WallCheck.Of(WallCheckKind.Overturning, name, c.Overturning, c.OverturningResistance));
                checks.Add(WallCheck.Of(WallCheckKind.Bearing, name, Math.Max(0, c.Vertical), c.BearingResistance, c.Combination.State == WallLimitState.Seismic
                    ? (c.SeismicBearingError.Length > 0 ? c.SeismicBearingError : "Portanza sismica non disponibile")
                    : !c.Soil.RoughBase ? "Fuori campo: base liscia, δd < φd/2" : "Fuori campo: e > B/3 o risultante verticale non positiva"));
                var contact = WallCheck.Of(WallCheckKind.Contact, name, Math.Abs(c.Eccentricity), width / 2);
                checks.Add(contact.With(c.Contact.Valid ? 2 * Math.Abs(c.Eccentricity) / width : (double?)null, c.Contact.Valid ? WallCheckStatus.ContactInCompression : WallCheckStatus.LossOfEquilibrium,
                    c.Contact.Valid ? "Contatto in compressione" : "Perdita di equilibrio"));
            }
            var warnings = new List<string>();
            if (input.Backfill.Layers.Count > 1) warnings.Add("Strati: Rankine locale con σ′v integrata; discontinuità di pressione alle interfacce. Non è una ricerca del cuneo di rottura multistrato.");
            if (cases.Any(c => !c.Contact.Valid)) warnings.Add("Perdita di equilibrio in almeno una combinazione: le verifiche strutturali che dipendono dalle reazioni di fondazione non sono disponibili.");
            if (cases.Any(c => c.Combination.State != WallLimitState.Seismic && c.BearingResistance is null)) warnings.Add("Portanza fuori campo in almeno una combinazione: non sostituire questo stato con un esito favorevole.");
            double area = stemArea + width * t;
            return new WallResult { Width = width, Area = area, Cases = cases, Checks = checks, Warnings = warnings };
        }

        /// <summary>Bands of the soil in front of the wall from its ground down to the base, cut at the front water head and at the top of the slab.</summary>
        public static IReadOnlyList<WallValleyBand> ValleyBands(WallInput input)
        {
            double height = input.Valley.Height, top = height, sigma = 0, water = input.Water != null ? input.Water.FrontHead : double.NegativeInfinity;
            var result = new List<WallValleyBand>();
            foreach (var layer in input.Valley.Profile.Layers)
            {
                double thickness = layer.Thickness, bottom = Math.Max(0, top - thickness);
                var cuts = Distinct(new[] { top, bottom, water, input.Geometry.Slab }.Where(y => y >= bottom - Tolerance && y <= top + Tolerance).OrderByDescending(y => y));
                for (int n = 1; n < cuts.Length; n++)
                {
                    double hi = cuts[n - 1], lo = cuts[n];
                    bool wet = (hi + lo) / 2 < water;
                    double gamma = wet ? layer.Soil.SaturatedUnitWeight : layer.Soil.UnitWeight, effective = gamma - (wet ? Gw : 0), next = sigma + effective * (hi - lo);
                    result.Add(new WallValleyBand(hi, lo, gamma, effective, layer.Soil.FrictionAngle, sigma, next)); sigma = next;
                }
                top = bottom; if (top <= 0) break;
            }
            return result;
        }

        /// <summary>
        /// Weight and static moments of the soil in front of the wall above the slab (or only of the wedge over the inclined face of the stem above the
        /// elevation <paramref name="above"/>): trapezoids between the end of the toe (or the root of the stem) and the face.
        /// </summary>
        public static WallSoilMass FrontSoilMass(WallGeometry g, IEnumerable<WallValleyBand> bands, double above = 0, bool wedgeOnly = false)
        {
            double a = g.Toe, t = g.Slab, slope = (g.StemBase - g.StemTop) / g.Height;
            double w = 0, mx = 0, my = 0;
            foreach (var band in bands)
            {
                double low = Math.Max(Math.Max(t, above), band.Bottom), high = band.Top; if (high <= low) continue;
                double left = wedgeOnly ? a : 0, x0 = a + slope * (low - t), x1 = a + slope * (high - t);
                if (Math.Max(x0, x1) <= left) continue;
                // Trapezoid with the vertical side at x = left: widths b0 at y = low and b1 at y = high.
                double b0 = x0 - left, b1 = x1 - left, height = high - low, area = (b0 + b1) * height / 2;
                if (area <= 0) continue;
                double cx = left + (b0 * b0 + b0 * b1 + b1 * b1) / (3 * (b0 + b1)), cy = low + height * (b0 + 2 * b1) / (3 * (b0 + b1));
                double weight = area * band.UnitWeight;
                w += weight; mx += weight * cx; my += weight * cy;
            }
            return new WallSoilMass(w, mx, my);
        }

        private static SeismicBearingResult? SeismicBearing(WallInput input, double width, double gamma, double phi, double n, double v, double eccentricity, double kv, double r, out string error)
        {
            error = "";
            try
            {
                var b = input.Seismic!.Bearing; double kh, vertical;
                if (b.GroundKh is null)
                {
                    var site = input.Seismic.Site ?? throw new ArgumentException("Portanza sismica: inserire l’accelerazione del terreno (non il kh ridotto del muro), oppure completare i parametri del sito.");
                    kh = site.AmaxG; vertical = .5 * kh;
                }
                else
                {
                    if (!(b.GroundKh >= 0 && b.GroundKh <= 1)) throw new ArgumentException("ground_kh: inserire un valore finito fra 0 e 1.");
                    if (!(b.GroundKv >= 0 && b.GroundKv <= .9)) throw new ArgumentException("ground_kv: inserire un valore finito fra 0 e 0.9.");
                    kh = b.GroundKh.Value; vertical = b.GroundKv!.Value;
                }
                if (!(b.ModelFactor >= 1 && b.ModelFactor <= 2)) throw new ArgumentException("model_factor: inserire un valore finito fra 1 e 2.");
                return ShallowFoundationSeismic.Calculate(width, gamma, phi, n, v, n * eccentricity, kh, vertical * (kv < 0 ? -1 : 1), b.ModelFactor, r);
            }
            catch (ArgumentException ex) { error = ex.Message; return null; }
        }

        private static void Validate(WallInput input, WallCombination[] rows)
        {
            var g = input.Geometry; double ht = g.TotalHeight, t = g.Slab, h = g.Height;
            if (!(input.UnitWeight > 0) || double.IsInfinity(input.UnitWeight)) throw new ArgumentException("Peso specifico muro: positivo e finito.");
            void Layers(SoilProfile profile, double ground, string label)
            {
                if (Math.Abs(profile.GroundSurface - ground) > 1e-6 * Math.Max(1, Math.Abs(ground))) throw new ArgumentException(label + ": il piano campagna della colonna deve essere alla sua quota.");
                if (profile.GroundwaterElevation.HasValue) throw new ArgumentException(label + ": la falda è un dato del muro (WallWater), non della colonna.");
                foreach (var l in profile.Layers)
                {
                    var s = l.Soil;
                    if (s.FrictionAngle < 10 * SoilUnits.Degree - 1e-12 || s.FrictionAngle > 45 * SoilUnits.Degree + 1e-12 || s.UnitWeight < 10 * SoilUnits.KiloNewtonPerCubicMetre - 1e-18
                        || s.SaturatedUnitWeight < s.UnitWeight || s.SaturatedUnitWeight > 28 * SoilUnits.KiloNewtonPerCubicMetre + 1e-18 || l.Thickness > 100 * SoilUnits.Metre)
                        throw new ArgumentException(label + ": 10° ≤ φ′ ≤ 45°, 10 ≤ γ ≤ γsat ≤ 28 kN/m³, spessore ≤ 100 m.");
                    if (s.EffectiveCohesion != 0) throw new ArgumentException(label + ": terreno granulare, c′ = 0.");
                }
            }
            Layers(input.Backfill, ht, "Strati");
            if (input.Backfill.Base > 1e-6) throw new ArgumentException("La stratigrafia deve coprire l’altezza H+t fino al piano di posa.");
            if (input.Foundation.SaturatedUnitWeight < input.Foundation.UnitWeight) throw new ArgumentException("Terreno di posa: γsat deve essere ≥ γ.");
            if (input.Valley.Height > ht) throw new ArgumentException("Altezza libera: 0≤Hlib≤H+t.");
            Layers(input.Valley.Profile, input.Valley.Height, "Valle");
            if (input.Valley.Height - input.Valley.Profile.Layers.Sum(l => l.Thickness) > 1e-6) throw new ArgumentException("La colonna di valle deve raggiungere il piano di posa.");
            if (input.Water != null)
            {
                double z = input.Water.Depth, front = input.Water.FrontHead;
                if (z > ht || front > Math.Max(t, input.Valley.Height) || front > ht - z) throw new ArgumentException("Falda: 0≤profondità≤H+t; battente a valle ≤max(t,Dv) e ≤battente a monte.");
            }
            foreach (var (face, limit) in new[] { (input.Back, input.Backfill.Layers.Min(l => l.Soil.FrictionAngle)), (input.Base, input.Foundation.FrictionAngle) })
            {
                if ((face.Mode == WallFriction.CastInPlace || face.Mode == WallFriction.PrecastSmooth) && face.CriticalStateAngle > limit)
                    throw new ArgumentException("Attrito automatico: assegnare φcv positivo e non superiore a φ′ del terreno a contatto.");
                if (face.Design(1) > limit) throw new ArgumentException("L’attrito di interfaccia non può superare φ′ del terreno.");
            }
            var ids = new HashSet<string>();
            foreach (var x in input.Actions)
            {
                if (!ids.Add(x.Id)) throw new ArgumentException("Azioni: identificatore univoco e nome obbligatori.");
                if (!x.Enabled) continue;
                foreach (double psi in new[] { x.Psi0, x.Psi1, x.Psi2 }) if (!(psi >= 0 && psi <= 1)) throw new ArgumentException("Coefficienti ψ compresi fra 0 e 1.");
                if (x.Psi2 > x.Psi1 || x.Psi1 > x.Psi0) throw new ArgumentException("Deve risultare ψ₂ ≤ ψ₁ ≤ ψ₀.");
                if (!(x.Value >= 0) || double.IsInfinity(x.Value)) throw new ArgumentException(x.Name + ": valore finito e non negativo.");
                if (x.Type == WallActionType.Impact && x.Category != WallActionCategory.A) throw new ArgumentException("L’urto deve avere natura eccezionale A.");
                if (x.Type != WallActionType.UniformSurcharge && (x.Z < t || x.Z > ht)) throw new ArgumentException(x.Name + ": quota z dal piano di posa compresa fra t e H+t.");
                if (x.Type == WallActionType.LateralPressure && (x.Z0 < t || x.Z0 >= x.Z)) throw new ArgumentException("Pressione laterale: t ≤ z₀ < z₁ ≤ H+t.");
                if (x.Type == WallActionType.VerticalForce)
                {
                    double thick = g.StemTop + (g.StemBase - g.StemTop) * (ht - x.Z) / h;
                    if (x.X < g.Back - thick || x.X > g.Back) throw new ArgumentException("La forza verticale deve ricadere nel fusto alla quota assegnata.");
                }
            }
            foreach (var group in input.Actions.Where(x => x.Enabled && x.Group.Length > 0).GroupBy(x => x.Group))
                if (group.Select(x => (x.Category, x.Psi0, x.Psi1, x.Psi2)).Distinct().Count() > 1) throw new ArgumentException("Azioni correlate dello stesso gruppo: natura e coefficienti ψ devono coincidere.");
            if (input.Seismic != null)
            {
                if (!(input.Seismic.Kh <= .4)) throw new ArgumentException("Coefficiente orizzontale assegnato: inserire un valore fra 0 e 0.4 −.");
                if (!(input.Seismic.Kv <= .2)) throw new ArgumentException("Coefficiente verticale (entrambi i segni): inserire un valore fra 0 e 0.2 −.");
                var layers = input.Backfill.Layers; var first = layers[0].Soil;
                if (input.Water != null || layers.Any(l => l.Soil.FrictionAngle != first.FrictionAngle || l.Soil.UnitWeight != first.UnitWeight))
                    throw new ArgumentException("Pseudostatica: questa versione richiede terreno omogeneo asciutto; disattivare il sisma per il caso con falda o strati differenti.");
                if (input.Seismic.Method == WallSeismicMethod.MononobeOkabe) _ = EarthPressure.MononobeOkabe(first.FrictionAngle, input.Seismic.Kh, input.Seismic.Kv);
            }
            if (rows.Length == 0) throw new ArgumentException("Abilitare almeno una combinazione.");
            var names = new HashSet<string>();
            foreach (var c in rows)
            {
                if (!names.Add(c.Name)) throw new ArgumentException("Combinazioni: nome univoco e stato limite valido obbligatori.");
                if (c.State == WallLimitState.Seismic && input.Seismic == null) throw new ArgumentException("Abilitare il sisma prima di usare combinazioni sismiche.");
                foreach (var x in input.Actions)
                {
                    double f = c.Coefficients.TryGetValue(x.Id, out double v) ? v : 0;
                    if (!(f >= 0 && f <= 5) || (!x.Enabled && f != 0) || (x.Category == WallActionCategory.A && f != 0 && c.State != WallLimitState.Exceptional))
                        throw new ArgumentException("Fattore azione incompatibile con abilitazione o stato limite.");
                }
                if (c.Coefficients.Keys.Any(k => !ids.Contains(k))) throw new ArgumentException("Matrice non allineata all’elenco delle azioni: rigenerare le combinazioni.");
            }
        }

        private static double Clamp(double value, double low, double high) => value < low ? low : value > high ? high : value;

        /// <summary>Coordinates closer than this (mm) are the same cut: the elevations of Model are sums in mm of the legacy thicknesses in m.</summary>
        private const double Tolerance = 1e-6;

        /// <summary>The ordered values without the ones closer than <see cref="Tolerance"/> to the previous kept one (the first of a cluster is kept).</summary>
        private static double[] Distinct(IEnumerable<double> ordered)
        {
            var kept = new List<double>();
            foreach (double v in ordered) if (kept.Count == 0 || Math.Abs(v - kept[kept.Count - 1]) > Tolerance) kept.Add(v);
            return kept.ToArray();
        }
    }
}
