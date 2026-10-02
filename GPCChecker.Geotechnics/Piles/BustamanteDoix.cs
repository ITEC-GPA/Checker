using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>Injection of the micropile: IGU single global injection, IRS repeated selective injection.</summary>
    public enum MicropileInjection { IGU, IRS }

    /// <summary>Soils of Viggiani Table 13.12 (Italian names in <see cref="BustamanteDoix.Label"/>).</summary>
    public enum BustamanteDoixSoil
    {
        Gravel, SandyGravel, GravellySand, CoarseSand, MediumSand, FineSand, SiltySand, Silt, Clay, Marl, MarlyLimestone, WeatheredLimestone, WeatheredRock
    }

    /// <summary>Unit shaft resistance read on a chart: curve (SG1 … R2), limit pressure pl (MPa), adopted α, recommended α range and s (MPa).</summary>
    public sealed class BustamanteDoixShaft
    {
        public string Curve { get; }
        public double LimitPressure { get; }
        public double Alpha { get; }
        public (double Min, double Max) RecommendedAlpha { get; }
        public double UnitResistance { get; }
        public MicropileInjection Injection { get; }
        internal BustamanteDoixShaft(string curve, double pl, double alpha, (double, double) recommended, double s, MicropileInjection injection)
        { Curve = curve; LimitPressure = pl; Alpha = alpha; RecommendedAlpha = recommended; UnitResistance = s; Injection = injection; }
    }

    /// <summary>Layer of a micropile: top and bottom along the axis (mm), soil of Table 13.12, adopted α, shaft resistance active or not.</summary>
    public sealed class MicropileLayer
    {
        public double Top { get; }
        public double Bottom { get; }
        public BustamanteDoixSoil Soil { get; }
        public double Alpha { get; }
        public bool ShaftActive { get; }
        public MicropileLayer(double top, double bottom, BustamanteDoixSoil soil, double alpha, bool shaftActive = true)
        { Top = top; Bottom = bottom; Soil = soil; Alpha = alpha; ShaftActive = shaftActive; }

        /// <summary>
        /// The layers of a Model profile along the axis of a micropile inclined by θ (rad) from the vertical, with the head at the ground surface:
        /// s = depth / cos θ. The parameters follow the layers of the profile.
        /// </summary>
        public static MicropileLayer[] FromProfile(SoilProfile profile, IReadOnlyList<(BustamanteDoixSoil Soil, double Alpha, bool ShaftActive)> parameters, double inclination = 0)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (parameters == null || parameters.Count != profile.Layers.Count) throw new ArgumentException("One Bustamante-Doix parameter set per layer of the profile.");
            double cos = MicropileTube.AxisCosine(inclination);
            return profile.Layers.Select((l, i) => new MicropileLayer((profile.GroundSurface - l.Top) / cos, (profile.GroundSurface - l.Bottom) / cos, parameters[i].Soil, parameters[i].Alpha,
                parameters[i].ShaftActive)).ToArray();
        }
    }

    /// <summary>Shaft segment of a micropile: layer (1-based), top and bottom (mm), the chart reading (null for an inactive layer), Ds = α D (mm), lateral resistance π Ds L s (N).</summary>
    public sealed class MicropileShaftSegment
    {
        public int Layer { get; }
        public double Top { get; }
        public double Bottom { get; }
        public BustamanteDoixShaft? Shaft { get; }
        public double? DrillDiameter { get; }
        public double UnitResistance { get; }
        public double Lateral { get; }
        public bool ShaftActive => Shaft != null;
        internal MicropileShaftSegment(int layer, double top, double bottom, BustamanteDoixShaft? shaft, double? ds, double s, double lateral)
        { Layer = layer; Top = top; Bottom = bottom; Shaft = shaft; DrillDiameter = ds; UnitResistance = s; Lateral = lateral; }
    }

    /// <summary>
    /// Shaft resistance of micropiles with the Bustamante-Doix method: Viggiani, Fondazioni, §13.1.6 pp. 392-396, eq. 13.21, Tables 13.12-13.13,
    /// Figures 13.16-13.19 (charts digitised from the scan, linear interpolation, no extrapolation). Transferred from ANTHEA
    /// (Anthea.Calculations.BustamanteDoix, version BD-VIGGIANI-2026-09-10, commit fe4652c). Units: pressures and unit resistances MPa, lengths mm.
    /// </summary>
    public static class BustamanteDoix
    {
        public const string Version = "BD-VIGGIANI-2026-09-10";
        public const string Source = "C. Viggiani, Fondazioni, §13.1.6, pp. 392–396; eq. 13.21, tab. 13.12–13.13, fig. 13.16–13.19";

        private static readonly Dictionary<BustamanteDoixSoil, (string Label, string Family, double[] IRS, double[] IGU)> Soils = new Dictionary<BustamanteDoixSoil, (string, string, double[], double[])>
        {
            [BustamanteDoixSoil.Gravel] = ("Ghiaia", "SG", new[] { 1.8, 1.8 }, new[] { 1.3, 1.4 }),
            [BustamanteDoixSoil.SandyGravel] = ("Ghiaia sabbiosa", "SG", new[] { 1.6, 1.8 }, new[] { 1.2, 1.4 }),
            [BustamanteDoixSoil.GravellySand] = ("Sabbia ghiaiosa", "SG", new[] { 1.5, 1.6 }, new[] { 1.2, 1.3 }),
            [BustamanteDoixSoil.CoarseSand] = ("Sabbia grossa", "SG", new[] { 1.4, 1.5 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.MediumSand] = ("Sabbia media", "SG", new[] { 1.4, 1.5 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.FineSand] = ("Sabbia fine", "SG", new[] { 1.4, 1.5 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.SiltySand] = ("Sabbia limosa", "SG", new[] { 1.4, 1.5 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.Silt] = ("Limo", "AL", new[] { 1.4, 1.6 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.Clay] = ("Argilla", "AL", new[] { 1.8, 2 }, new[] { 1.2, 1.2 }),
            [BustamanteDoixSoil.Marl] = ("Marne", "MC", new[] { 1.8, 1.8 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.MarlyLimestone] = ("Calcari marnosi", "MC", new[] { 1.8, 1.8 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.WeatheredLimestone] = ("Calcari alterati o fratturati", "MC", new[] { 1.8, 1.8 }, new[] { 1.1, 1.2 }),
            [BustamanteDoixSoil.WeatheredRock] = ("Roccia alterata e/o fratturata", "R", new[] { 1.2, 1.2 }, new[] { 1.1, 1.1 })
        };

        // Charts: pl (MPa), s (MPa); 1 = IRS, 2 = IGU.
        private static readonly Dictionary<string, (double X, double Y)[]> Charts = new Dictionary<string, (double, double)[]>
        {
            ["SG1"] = new[] { (.25, .075), (1, .15), (2, .25), (3, .35), (4, .45), (5, .55), (6, .65), (6.7, .72) },
            ["SG2"] = new[] { (.25, .025), (1, .10), (2, .20), (3, .30), (4, .40), (5, .50), (6, .60), (6.7, .67) },
            ["AL1"] = new[] { (.30, .085), (.50, .125), (.75, .155), (1, .180), (1.5, .220), (2, .260), (2.4, .295) },
            ["AL2"] = new[] { (.30, .040), (.50, .065), (.75, .083), (1, .100), (1.5, .130), (2, .160), (2.4, .184) },
            ["MC1"] = new[] { (1.0, .20), (2, .27), (3, .34), (4, .41), (5, .48), (6, .55), (7, .62), (8, .69) },
            ["MC2"] = new[] { (1.0, .15), (2, .20), (3, .25), (4, .30), (5, .35), (6, .40), (7, .45), (8, .50) },
            ["R1"] = new[] { (1.3, .20), (2, .30), (3, .43), (4, .55), (5, .68), (6, .81), (7, .94), (8, 1.07), (9, 1.20), (9.4, 1.25) },
            ["R2"] = new[] { (1.3, .17), (2, .25), (3, .34), (4, .44), (5, .54), (6, .64), (7, .74), (8, .85), (9, .95), (9.4, .99) }
        };

        /// <summary>Name of the soil in Table 13.12.</summary>
        public static string Label(BustamanteDoixSoil soil) => Entry(soil).Label;
        /// <summary>Family of the charts: SG sands and gravels, AL silts and clays, MC marls and limestones, R weathered rock.</summary>
        public static string Family(BustamanteDoixSoil soil) => Entry(soil).Family;
        /// <summary>The digitised chart (pl, s in MPa) of a code SG1 … R2.</summary>
        public static IReadOnlyList<(double LimitPressure, double UnitResistance)> Chart(string code)
            => Charts.TryGetValue(code ?? "", out var points) ? points : throw new ArgumentException("Unknown Bustamante-Doix chart: " + code);

        /// <summary>Recommended α (Table 13.12) of the soil and injection.</summary>
        public static (double Min, double Max) AlphaRange(BustamanteDoixSoil soil, MicropileInjection injection)
        {
            var e = Entry(soil); var v = injection == MicropileInjection.IGU ? e.IGU : injection == MicropileInjection.IRS ? e.IRS : throw new ArgumentOutOfRangeException(nameof(injection));
            return (v[0], v[1]);
        }

        /// <summary>
        /// Unit shaft resistance s for the limit pressure pl (MPa) on the chart of the soil family and injection; the adopted α may lie outside the
        /// recommended range but must be positive. A pressure outside the chart is rejected (no extrapolation).
        /// </summary>
        public static BustamanteDoixShaft UnitShaftResistance(BustamanteDoixSoil soil, MicropileInjection injection, double limitPressure, double alpha)
        {
            var range = AlphaRange(soil, injection);
            if (double.IsNaN(alpha) || double.IsInfinity(alpha) || alpha <= 0) throw new ArgumentException($"α {injection}: a positive value is required.");
            string code = Family(soil) + (injection == MicropileInjection.IGU ? "2" : "1"); var points = Charts[code];
            if (double.IsNaN(limitPressure) || double.IsInfinity(limitPressure) || limitPressure < points[0].X || limitPressure > points[points.Length - 1].X)
                throw new ArgumentException($"p_l outside chart {code}: use {points[0].X:g}–{points[points.Length - 1].X:g} MPa; no extrapolation.");
            for (int i = 1; i < points.Length; i++)
                if (limitPressure <= points[i].X)
                {
                    var (x1, y1) = points[i - 1]; var (x2, y2) = points[i];
                    return new BustamanteDoixShaft(code, limitPressure, alpha, range, y1 + (y2 - y1) * (limitPressure - x1) / (x2 - x1), injection);
                }
            throw new InvalidOperationException("Chart not evaluable.");
        }

        /// <summary>
        /// Shaft segments of the layers between the start of the grouting and the depth reached (mm along the axis), with the drilling diameter D
        /// (mm) and the injection pressure taken as pl. Inactive layers give a segment without resistance.
        /// </summary>
        public static MicropileShaftSegment[] Segments(IReadOnlyList<MicropileLayer> layers, double depth, double groutStart, double diameter, MicropileInjection injection, double pressure)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            var output = new List<MicropileShaftSegment>();
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i]; double top = Math.Max(layer.Top, groutStart), bottom = Math.Min(layer.Bottom, depth);
                if (bottom <= top) continue;
                if (!layer.ShaftActive) { output.Add(new MicropileShaftSegment(i + 1, top, bottom, null, null, 0, 0)); continue; }
                BustamanteDoixShaft shaft;
                try { shaft = UnitShaftResistance(layer.Soil, injection, pressure, layer.Alpha); }
                catch (ArgumentException ex) { throw new ArgumentException($"Layer {i + 1}: {ex.Message}"); }
                double ds = diameter * shaft.Alpha;
                output.Add(new MicropileShaftSegment(i + 1, top, bottom, shaft, ds, shaft.UnitResistance, Math.PI * ds * (bottom - top) * shaft.UnitResistance));
            }
            return output.ToArray();
        }

        private static (string Label, string Family, double[] IRS, double[] IGU) Entry(BustamanteDoixSoil soil)
            => Soils.TryGetValue(soil, out var e) ? e : throw new ArgumentOutOfRangeException(nameof(soil));
    }
}
