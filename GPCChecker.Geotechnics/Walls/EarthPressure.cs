using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Walls
{
    /// <summary>
    /// Earth pressure coefficients of a vertical back and a horizontal fill (angles in rad): Rankine, Coulomb and Mononobe-Okabe with the
    /// friction of the wall, at rest of Jaky. Transferred from ANTHEA (RetainingWall.Ka, SeismicKa, ActiveHorizontal, commit fe4652c).
    /// </summary>
    public static class EarthPressure
    {
        /// <summary>Rankine active coefficient tan²(π/4 − φ/2).</summary>
        public static double RankineActive(double frictionAngle) => Math.Pow(Math.Tan(Math.PI / 4 - frictionAngle / 2), 2);

        /// <summary>Rankine passive coefficient 1/Ka.</summary>
        public static double RankinePassive(double frictionAngle) => 1 / RankineActive(frictionAngle);

        /// <summary>At rest coefficient of a normally consolidated soil, 1 − sin φ (Wood).</summary>
        public static double AtRest(double frictionAngle) => 1 - Math.Sin(frictionAngle);

        /// <summary>Seismic angle θ = atan(kh / (1 − kv)).</summary>
        public static double SeismicAngle(double kh, double kv) => Math.Atan2(kh, 1 - kv);

        /// <summary>
        /// Horizontal component of the Coulomb (kh = kv = 0) or Mononobe-Okabe coefficient with the friction δ of the wall:
        /// cos²(φ − θ) cos δ / (cos θ cos(δ + θ) [1 + √(sin(φ + δ) sin(φ − θ) / cos(δ + θ))]²). Requires 0 ≤ δ ≤ φ, θ &lt; φ and δ + θ &lt; π/2.
        /// </summary>
        public static double ActiveHorizontal(double frictionAngle, double wallFriction, double kh = 0, double kv = 0)
        {
            double p = frictionAngle, de = wallFriction, theta = SeismicAngle(kh, kv);
            if (theta >= p || de < 0 || de > p || de + theta >= Math.PI / 2) throw new ArgumentException("Coulomb/Mononobe–Okabe: 0≤δd≤φd e θ<φd richiesti.");
            return Math.Pow(Math.Cos(p - theta), 2) * Math.Cos(de) / (Math.Cos(theta) * Math.Cos(de + theta)
                * Math.Pow(1 + Math.Sqrt(Math.Sin(p + de) * Math.Sin(p - theta) / Math.Cos(de + theta)), 2));
        }

        /// <summary>Mononobe-Okabe coefficient without friction of the wall (δ = 0); requires θ &lt; φ.</summary>
        public static double MononobeOkabe(double frictionAngle, double kh, double kv)
        {
            double p = frictionAngle, theta = SeismicAngle(kh, kv);
            if (theta >= p) throw new ArgumentException("Mononobe–Okabe non applicabile: atan(kh/(1−kv)) deve essere minore di φ′.");
            return Math.Pow(Math.Cos(p - theta), 2) / (Math.Pow(Math.Cos(theta), 2) * Math.Pow(1 + Math.Sqrt(Math.Sin(p) * Math.Sin(p - theta) / Math.Cos(theta)), 2));
        }

        /// <summary>Design friction angle φd = atan(tan φk / γM) (rad).</summary>
        public static double Design(double frictionAngle, double partialFactor) => Math.Atan(Math.Tan(frictionAngle) / partialFactor);
    }

    /// <summary>Friction of an interface: assigned, or k·atan(tan φcv/γM) with k = 1 cast in place, 2/3 precast smooth, 0 smooth.</summary>
    public enum WallFriction { Assigned, CastInPlace, PrecastSmooth, Smooth }

    /// <summary>
    /// Interface of the wall (back) or of the base: mode, assigned characteristic friction angle δk and critical state angle φcv (rad). The design
    /// value is atan(tan δk/γM) when assigned, k·atan(tan φcv/γM) otherwise.
    /// </summary>
    public sealed class WallInterface
    {
        public WallFriction Mode { get; }
        public double Angle { get; }
        public double CriticalStateAngle { get; }
        public WallInterface(WallFriction mode, double angle = 0, double criticalStateAngle = 0)
        {
            if (!Enum.IsDefined(typeof(WallFriction), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == WallFriction.Assigned && !(angle >= 0 && angle < Math.PI / 2)) throw new ArgumentException("Assegnare δ muro finito e non negativo.");
            if ((mode == WallFriction.CastInPlace || mode == WallFriction.PrecastSmooth) && !(criticalStateAngle > 0 && criticalStateAngle < Math.PI / 2))
                throw new ArgumentException("Attrito automatico: assegnare φcv positivo e non superiore a φ′ del terreno a contatto.");
            Mode = mode; Angle = angle; CriticalStateAngle = criticalStateAngle;
        }

        /// <summary>The factor k of the automatic modes (1, 2/3, 0); 1 for the assigned angle.</summary>
        public double Factor => Mode == WallFriction.CastInPlace ? 1 : Mode == WallFriction.PrecastSmooth ? 2.0 / 3 : Mode == WallFriction.Smooth ? 0 : 1;

        /// <summary>Design friction angle with the partial factor γM of tan φ (rad).</summary>
        public double Design(double partialFactor = 1)
            => Mode == WallFriction.Assigned ? EarthPressure.Design(Angle, partialFactor) : Factor * EarthPressure.Design(CriticalStateAngle, partialFactor);
    }

    /// <summary>
    /// Contact of the base of a wall without tension (elastic law): start and end of the compressed length (mm from the toe), pressures at the two
    /// ends and peak (MPa), validity (positive normal force with the resultant inside the base).
    /// </summary>
    public sealed class WallContact
    {
        public double Start { get; }
        public double End { get; }
        public double Toe { get; }
        public double Heel { get; }
        public double Peak { get; }
        public bool Valid { get; }
        internal WallContact(double start, double end, double toe, double heel, double peak, bool valid) { Start = start; End = end; Toe = toe; Heel = heel; Peak = peak; Valid = valid; }

        /// <summary>
        /// Contact under the normal force N (N/mm) applied at x (mm from the toe) on the width B: trapezoid when |e| ≤ B/6, triangle of length
        /// 3 min(x, B − x) otherwise; invalid for N ≤ 0 or x outside the base.
        /// </summary>
        public static WallContact Law(double width, double normal, double x)
        {
            if (normal <= 0 || x <= 0 || x >= width) return new WallContact(0, 0, 0, 0, 0, false);
            double e = width / 2 - x;
            if (Math.Abs(e) <= width / 6) return new WallContact(0, width, normal / width * (1 + 6 * e / width), normal / width * (1 - 6 * e / width), normal / width * (1 + 6 * Math.Abs(e) / width), true);
            double length = 3 * Math.Min(x, width - x), peak = 2 * normal / length;
            return x < width / 2 ? new WallContact(0, length, peak, 0, peak, true) : new WallContact(width - length, width, 0, peak, peak, true);
        }

        /// <summary>Contact pressure at x (MPa), zero outside the compressed length.</summary>
        public double Pressure(double x)
        {
            if (!Valid || x < Start || x > End) return 0;
            return Toe + (Heel - Toe) * (x - Start) / (End - Start);
        }
    }

    /// <summary>A linear distribution between two coordinates (mm) with values P0 and P1 (MPa, or N/mm³ for a weight profile).</summary>
    public sealed class WallPressureSegment
    {
        public double Z0 { get; }
        public double Z1 { get; }
        public double P0 { get; }
        public double P1 { get; }
        public WallPressureSegment(double z0, double z1, double p0, double p1) { Z0 = z0; Z1 = z1; P0 = p0; P1 = p1; }

        /// <summary>
        /// Resultant and moment about the root of the pieces between left and right: F = ∫ p, M = ∫ (z − root) p, closed forms on each trapezoid.
        /// </summary>
        public static (double Force, double Moment) Integrate(IEnumerable<WallPressureSegment> pieces, double left, double right, double root)
        {
            double f = 0, m = 0;
            foreach (var p in pieces)
            {
                double a = Math.Max(left, p.Z0), b = Math.Min(right, p.Z1); if (b <= a) continue;
                double p0 = p.P0 + (p.P1 - p.P0) * (a - p.Z0) / (p.Z1 - p.Z0), p1 = p.P0 + (p.P1 - p.P0) * (b - p.Z0) / (p.Z1 - p.Z0), l = b - a;
                f += (p0 + p1) * l / 2;
                m += (a - root) * (p0 + p1) * l / 2 + l * l * (p0 + 2 * p1) / 6;
            }
            return (f, m);
        }
    }

    /// <summary>A point of the curvature of the stem: height above the top of the slab (mm) and curvature (1/mm).</summary>
    public sealed class WallCurvaturePoint
    {
        public double Height { get; }
        public double Curvature { get; }
        public WallCurvaturePoint(double height, double curvature) { Height = height; Curvature = curvature; }
    }

    /// <summary>A point of the deformed stem: height (mm), curvature (1/mm), rotation (rad) and displacement (mm).</summary>
    public sealed class WallDisplacementPoint
    {
        public double Height { get; }
        public double Curvature { get; }
        public double Rotation { get; }
        public double Displacement { get; }
        internal WallDisplacementPoint(double height, double curvature, double rotation, double displacement) { Height = height; Curvature = curvature; Rotation = rotation; Displacement = displacement; }

        /// <summary>
        /// Rotation and displacement of a cantilever fixed at the base from the curvatures (linear between the points, zero at the top when the
        /// last point is below it): θ += L (κa + κb)/2, u += θ L + L² (2κa + κb)/6. The first point must be at the base.
        /// </summary>
        public static IReadOnlyList<WallDisplacementPoint> Integrate(IEnumerable<WallCurvaturePoint> values, double height)
        {
            var points = values.OrderBy(p => p.Height).ToList();
            if (points.Count < 2 || points.Any(p => double.IsNaN(p.Height + p.Curvature) || double.IsInfinity(p.Height + p.Curvature))) throw new ArgumentException("Curvature insufficienti o non convergenti.");
            if (points[0].Height > 1e-3) throw new ArgumentException("Curvatura al piede non disponibile.");
            if (points[points.Count - 1].Height < height - 1e-3) points.Add(new WallCurvaturePoint(height, 0));
            var result = new List<WallDisplacementPoint> { new WallDisplacementPoint(0, points[0].Curvature, 0, 0) }; double rotation = 0, displacement = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double length = points[i].Height - points[i - 1].Height, a = points[i - 1].Curvature, b = points[i].Curvature;
                displacement += rotation * length + length * length * (2 * a + b) / 6;
                rotation += length * (a + b) / 2; result.Add(new WallDisplacementPoint(points[i].Height, b, rotation, displacement));
            }
            return result;
        }
    }
}
