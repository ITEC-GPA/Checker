using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>Family of the wall: reinforced concrete cantilever or gravity wall with a trapezoidal section.</summary>
    public enum WallFamily { Cantilever, Gravity }

    /// <summary>
    /// Section of the wall per unit length (mm): height H of the stem above the slab, thickness of the stem at the base and at the top (vertical back
    /// towards the fill, inclined face towards the valley), thickness t of the slab, toe a and heel b. Origin at the toe, at the base of the slab.
    /// </summary>
    public sealed class WallGeometry
    {
        public double Height { get; }
        public double StemBase { get; }
        public double StemTop { get; }
        public double Slab { get; }
        public double Toe { get; }
        public double Heel { get; }
        public double Width => Toe + StemBase + Heel;
        public double TotalHeight => Height + Slab;
        /// <summary>Abscissa of the back of the stem, a + s0.</summary>
        public double Back => Toe + StemBase;
        public WallGeometry(double height, double stemBase, double stemTop, double slab, double toe, double heel)
        {
            foreach (double v in new[] { height, stemBase, stemTop, slab }) if (!(v > 0) || double.IsInfinity(v)) throw new ArgumentException("Geometria del muro: dimensioni positive e finite.");
            foreach (double v in new[] { toe, heel }) if (!(v >= 0) || double.IsInfinity(v)) throw new ArgumentException("Geometria del muro: mensole non negative e finite.");
            if (stemTop > stemBase) throw new ArgumentException("Lo spessore in testa deve essere ≤ quello al piede.");
            Height = height; StemBase = stemBase; StemTop = stemTop; Slab = slab; Toe = toe; Heel = heel;
        }

        /// <summary>Thickness of the stem at the depth z below its top (mm).</summary>
        public double StemThickness(double depth) => StemTop + (StemBase - StemTop) * depth / Height;
    }

    /// <summary>
    /// Water: depth of the table behind the wall below the top of the fill and head in front of the wall above the base (mm). Hydrostatic pressure
    /// on the back net of the one in front, linear uplift between the two heads, saturated weights below the tables.
    /// </summary>
    public sealed class WallWater
    {
        public double Depth { get; }
        public double FrontHead { get; }
        public WallWater(double depth, double frontHead = 0)
        {
            if (!(depth >= 0) || double.IsInfinity(depth) || !(frontHead >= 0) || double.IsInfinity(frontHead)) throw new ArgumentException("Falda: profondità e battente finiti e non negativi.");
            Depth = depth; FrontHead = frontHead;
        }
    }

    /// <summary>
    /// Soil in front of the wall: height Dv of the ground above the base (0 = entirely free face), its Model column (<see cref="SoilProfile"/>
    /// with the ground at Dv, c' = 0, without groundwater: the water is <see cref="WallWater"/>), Rankine passive resistance with the mobilised
    /// fraction 0-1 (never in the seismic combinations).
    /// </summary>
    public sealed class WallValley
    {
        public double Height { get; }
        public SoilProfile Profile { get; }
        public bool Passive { get; }
        public double Mobilization { get; }
        public WallValley(double height, SoilProfile profile, bool passive = false, double mobilization = 0)
        {
            if (!(height >= 0) || double.IsInfinity(height)) throw new ArgumentException("Altezza libera: 0≤Hlib≤H+t.");
            if (!(mobilization >= 0 && mobilization <= 1)) throw new ArgumentException("Frazione di passiva mobilitata: 0–1.");
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (profile.GroundwaterElevation.HasValue) throw new ArgumentException("Valle: la falda è un dato del muro (WallWater), non della colonna.");
            Height = height; Passive = passive; Mobilization = mobilization;
        }

        /// <summary>A free face (Dv = 0) with the column of the profile (used by the global stability only).</summary>
        public static WallValley Free(SoilProfile profile) => new WallValley(0, profile);
    }

    /// <summary>Type of an action of the wall.</summary>
    public enum WallActionType { UniformSurcharge, HorizontalForce, VerticalForce, Moment, LateralPressure, Impact }

    /// <summary>Nature of an action: permanent structural G1, non structural G2, variable Q, accidental A.</summary>
    public enum WallActionCategory { G1, G2, Q, A }

    /// <summary>
    /// Characteristic action per unit length. Uniform surcharge on the fill and lateral pressure between z0 and z on the back in MPa, forces in N/mm,
    /// moment in N·mm/mm (positive overturning towards the valley); z, z0 elevations above the base, x abscissa from the toe (vertical force on the
    /// stem), mm. ψ0 ≥ ψ1 ≥ ψ2 and the group of the correlated actions (same nature and ψ).
    /// </summary>
    public sealed class WallAction
    {
        public string Id { get; }
        public string Name { get; }
        public WallActionType Type { get; }
        public WallActionCategory Category { get; }
        public double Value { get; }
        public double Z { get; }
        public double Z0 { get; }
        public double X { get; }
        public double Psi0 { get; }
        public double Psi1 { get; }
        public double Psi2 { get; }
        public string Group { get; }
        public bool Enabled { get; }
        public WallAction(string id, string name, WallActionType type, WallActionCategory category, double value, double z = 0, double z0 = 0, double x = 0,
            double psi0 = .7, double psi1 = .5, double psi2 = .3, string group = "", bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Azioni: identificatore univoco e nome obbligatori.");
            if (!Enum.IsDefined(typeof(WallActionType), type) || !Enum.IsDefined(typeof(WallActionCategory), category)) throw new ArgumentException("Tipo o natura dell’azione non supportati.");
            Id = id; Name = name; Type = type; Category = category; Value = value; Z = z; Z0 = z0; X = x; Psi0 = psi0; Psi1 = psi1; Psi2 = psi2; Group = group ?? ""; Enabled = enabled;
        }
        /// <summary>Key of the group of correlated actions: the group, or the id of an isolated action.</summary>
        public string Key => Group.Length > 0 ? Group : Id;
    }

    /// <summary>Limit state of a combination: ULS, characteristic, frequent and quasi permanent SLS, seismic, accidental.</summary>
    public enum WallLimitState { Ultimate, Characteristic, Frequent, QuasiPermanent, Seismic, Exceptional }

    /// <summary>Use of a seismic combination with the coefficients of the site: every check, or overturning only (β amplified).</summary>
    public enum WallSeismicPurpose { None, General, Overturning }

    /// <summary>
    /// A combination of the wall: limit state, factors of the weight of the wall, of the fill (also on the thrust), of the valley soil and of the
    /// water, γM of tan φ, γR of sliding, overturning and bearing, seismic coefficients and the factor of every action (by id).
    /// </summary>
    public sealed class WallCombination
    {
        public string Name { get; }
        public WallLimitState State { get; }
        public string Approach { get; }
        public double Wall { get; }
        public double Soil { get; }
        public double ValleySoil { get; }
        public double Water { get; }
        public double FrictionFactor { get; }
        public double SlidingFactor { get; }
        public double OverturningFactor { get; }
        public double BearingFactor { get; }
        public double Kh { get; }
        public double Kv { get; }
        public WallSeismicPurpose Purpose { get; }
        public IReadOnlyDictionary<string, double> Coefficients { get; }
        public WallCombination(string name, WallLimitState state, double wall, double soil, double valleySoil, double water, double frictionFactor, double slidingFactor,
            double overturningFactor, double bearingFactor, double kh, double kv, IReadOnlyDictionary<string, double> coefficients, WallSeismicPurpose purpose = WallSeismicPurpose.None, string approach = "")
        {
            if (string.IsNullOrWhiteSpace(name) || !Enum.IsDefined(typeof(WallLimitState), state)) throw new ArgumentException("Combinazioni: nome univoco e stato limite valido obbligatori.");
            foreach (double v in new[] { wall, soil, valleySoil, water }) if (!(v >= 0 && v <= 5)) throw new ArgumentException("Coefficiente fuori campo.");
            foreach (double v in new[] { frictionFactor, slidingFactor, overturningFactor, bearingFactor }) if (!(v >= .1 && v <= 5)) throw new ArgumentException("Coefficiente fuori campo.");
            if (!(kh >= 0 && kh <= .4)) throw new ArgumentException("Coefficiente fuori campo: kh");
            if (!(Math.Abs(kv) <= .2)) throw new ArgumentException("kv deve essere compreso fra −0,2 e +0,2.");
            if ((kh != 0 || kv != 0) && state != WallLimitState.Seismic) throw new ArgumentException("kh/kv ammessi soltanto in combinazioni SISMA con sisma abilitato.");
            if (purpose != WallSeismicPurpose.None && state != WallLimitState.Seismic) throw new ArgumentException("Destinazione della combinazione sismica non valida.");
            Name = name; State = state; Approach = approach ?? ""; Wall = wall; Soil = soil; ValleySoil = valleySoil; Water = water; FrictionFactor = frictionFactor; SlidingFactor = slidingFactor;
            OverturningFactor = overturningFactor; BearingFactor = bearingFactor; Kh = kh; Kv = kv; Purpose = purpose;
            Coefficients = (coefficients ?? throw new ArgumentNullException(nameof(coefficients))).ToDictionary(p => p.Key, p => p.Value);
        }
        public bool Design => State == WallLimitState.Ultimate || State == WallLimitState.Seismic;
        public bool Service => State == WallLimitState.Characteristic || State == WallLimitState.Frequent || State == WallLimitState.QuasiPermanent;
    }

    /// <summary>Seismic earth pressure: Mononobe-Okabe (wall free to move) or simplified Wood (rigid wall, at rest, uniform increment kh γ Ht²).</summary>
    public enum WallSeismicMethod { MononobeOkabe, Wood }

    /// <summary>
    /// Acceleration of the ground for the seismic bearing capacity (EN 1998-5 Annex F): from the site (amax/g, av = 0.5 ah) or assigned, with the
    /// model factor γRd (1-2).
    /// </summary>
    public sealed class WallSeismicBearing
    {
        public double? GroundKh { get; }
        public double? GroundKv { get; }
        public double ModelFactor { get; }
        /// <summary>γRd also on the soil inertia F̄, as ANTHEA (EN 1998-5 (F.7) has F̄ without γRd: false by default).</summary>
        public bool ModelFactorOnInertia { get; }
        /// <summary>From the site: the maximum acceleration of the site (amax/g) with av/g = 0.5 ah/g.</summary>
        public static WallSeismicBearing FromSite(double modelFactor = 1.15, bool modelFactorOnInertia = false) => new WallSeismicBearing(null, null, modelFactor, modelFactorOnInertia);
        public static WallSeismicBearing Assigned(double groundKh, double groundKv, double modelFactor = 1.15, bool modelFactorOnInertia = false)
            => new WallSeismicBearing(groundKh, groundKv, modelFactor, modelFactorOnInertia);
        private WallSeismicBearing(double? kh, double? kv, double modelFactor, bool onInertia) { GroundKh = kh; GroundKv = kv; ModelFactor = modelFactor; ModelFactorOnInertia = onInertia; }
    }

    /// <summary>
    /// Seismic options of the wall: method of the earth pressure, NTC site amplification at the SLV or assigned kh and |kv| (0-0.4 and 0-0.2), and
    /// the acceleration of the ground for the bearing capacity. With the site: βm = 0.38 (Mononobe-Okabe, wall free to move) or 1 (Wood), kh =
    /// βm amax/g, kv = ±0.5 kh; for the overturning βm = min(1; 1.5 βm) (NTC 2018 §7.11.6.2.1).
    /// </summary>
    public sealed class WallSeismic
    {
        public WallSeismicMethod Method { get; }
        public Seismic.NtcSiteAmplification? Site { get; }
        public double Kh { get; }
        public double Kv { get; }
        public WallSeismicBearing Bearing { get; }
        private WallSeismic(WallSeismicMethod method, Seismic.NtcSiteAmplification? site, double kh, double kv, WallSeismicBearing? bearing)
        {
            if (!Enum.IsDefined(typeof(WallSeismicMethod), method)) throw new ArgumentException("Metodo sismico non riconosciuto.");
            Method = method; Site = site; Kh = kh; Kv = kv; Bearing = bearing ?? WallSeismicBearing.FromSite();
        }

        /// <summary>Assigned coefficients kh (0-0.4) and |kv| (0-0.2), both signs of kv.</summary>
        public static WallSeismic Assigned(WallSeismicMethod method, double kh, double kv, WallSeismicBearing? bearing = null)
        {
            if (!(kh >= 0 && kh <= .4)) throw new ArgumentException("Coefficiente orizzontale assegnato: inserire un valore fra 0 e 0.4 −.");
            if (!(kv >= 0 && kv <= .2)) throw new ArgumentException("Coefficiente verticale (entrambi i segni): inserire un valore fra 0 e 0.2 −.");
            return new WallSeismic(method, null, kh, kv, bearing);
        }

        /// <summary>Coefficients from the site (SLV).</summary>
        public static WallSeismic FromSite(WallSeismicMethod method, Seismic.NtcSiteAmplification site, WallSeismicBearing? bearing = null)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            double beta = method == WallSeismicMethod.Wood ? 1 : .38;
            return new WallSeismic(method, site, beta * site.AmaxG, .5 * beta * site.AmaxG, bearing);
        }

        /// <summary>βm of the general checks (site only): 0.38 or 1.</summary>
        public double Beta => Method == WallSeismicMethod.Wood ? 1 : .38;
        /// <summary>βm of the overturning, min(1; 1.5 βm).</summary>
        public double BetaOverturning => Math.Min(1, 1.5 * Beta);
        public double KhOverturning => Site == null ? Kh : BetaOverturning * Site.AmaxG;
        public double KvOverturning => Site == null ? Kv : .5 * BetaOverturning * Site.AmaxG;
    }

    /// <summary>
    /// Input of the wall: family, geometry, unit weight of the wall (N/mm³; 25 kN/m³ of reinforced concrete in ANTHEA), fill behind the wall (Model
    /// <see cref="SoilProfile"/> with the ground at the top of the stem, H + t above the base, granular c' = 0, without groundwater), foundation soil
    /// (Model <see cref="Soil"/>), valley, interfaces of the back and of the base, water, actions, seismic options and the elevations of
    /// additional stem cuts (change of the reinforcement).
    /// </summary>
    public sealed class WallInput
    {
        public WallFamily Family { get; }
        public WallGeometry Geometry { get; }
        public double UnitWeight { get; }
        public SoilProfile Backfill { get; }
        public Soil Foundation { get; }
        public WallValley Valley { get; }
        public WallInterface Back { get; }
        public WallInterface Base { get; }
        public WallWater? Water { get; }
        public IReadOnlyList<WallAction> Actions { get; }
        public WallSeismic? Seismic { get; }
        /// <summary>Height above the top of the slab where the reinforcement of the stem changes (mm; null: one zone).</summary>
        public double? ReinforcementSplit { get; }
        public WallInput(WallFamily family, WallGeometry geometry, double unitWeight, SoilProfile backfill, Soil foundation, WallValley valley, WallInterface back, WallInterface @base,
            WallWater? water = null, IEnumerable<WallAction>? actions = null, WallSeismic? seismic = null, double? reinforcementSplit = null)
        {
            Family = family; Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry)); UnitWeight = unitWeight;
            Backfill = backfill ?? throw new ArgumentNullException(nameof(backfill)); Foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
            Valley = valley ?? throw new ArgumentNullException(nameof(valley)); Back = back ?? throw new ArgumentNullException(nameof(back)); Base = @base ?? throw new ArgumentNullException(nameof(@base));
            Water = water; Actions = (actions ?? Enumerable.Empty<WallAction>()).ToArray(); Seismic = seismic; ReinforcementSplit = reinforcementSplit;
        }
    }
}
