using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Foundations
{
    /// <summary>Verdict of the seismic bearing capacity.</summary>
    public enum SeismicBearingStatus { Satisfied, NotSatisfied, DomainExhausted }

    /// <summary>
    /// Result per unit length: capacity along the load ray (N/mm), ratio NEd/capacity (null when the capacity is zero), Nmax (N/mm), soil inertia
    /// F̄, normalised N̄, V̄, M̄, vertical limit (1 − m F̄)^k' and the interaction value of the design loads.
    /// </summary>
    public sealed class SeismicBearingResult
    {
        public double Capacity { get; }
        public double? Ratio { get; }
        public double NMax { get; }
        public double SoilInertia { get; }
        public double NBar { get; }
        public double VBar { get; }
        public double MBar { get; }
        public double VerticalLimit { get; }
        public double? Interaction { get; }
        public SeismicBearingStatus Status { get; }
        internal SeismicBearingResult(double capacity, double? ratio, double nMax, double soilInertia, double nBar, double vBar, double mBar, double verticalLimit,
            double? interaction, SeismicBearingStatus status)
        {
            Capacity = capacity; Ratio = ratio; NMax = nMax; SoilInertia = soilInertia; NBar = nBar; VBar = vBar; MBar = mBar; VerticalLimit = verticalLimit;
            Interaction = interaction; Status = status;
        }
    }

    /// <summary>
    /// Seismic bearing capacity of a strip footing on dry cohesionless soil, EN 1998-5:2004 Annex F with the coefficients of purely cohesionless
    /// soils (a = c = 0.92, b = d = 1.25, e = 0.41, f = 0.32, m = 0.96, k = 1, k' = 0.39, cT = 1.14, cM = c'M = 1.01, β = 2.90, γ = 2.80) and
    /// Nmax = ½ ρ g (1 ∓ av/g) B² Nγ. The capacity is searched along the ray of the design loads N, V, M; the eccentricity is already in M, so no
    /// effective width or inclination factors are applied again. Transferred from ANTHEA (Anthea.Calculations.Geotechnics.ShallowFoundationSeismic,
    /// commit fe4652c). Units per unit length: mm, N/mm³, rad, N/mm, N·mm/mm.
    /// </summary>
    public static class ShallowFoundationSeismic
    {
        /// <param name="width">Footing width B, mm.</param>
        /// <param name="unitWeight">Unit weight of the dry soil, N/mm³.</param>
        /// <param name="frictionAngle">Design friction angle φ'd, rad (0 &lt; φ'd ≤ 45°).</param>
        /// <param name="axial">Design vertical load NEd &gt; 0, N/mm.</param>
        /// <param name="shear">Design horizontal load VEd, N/mm (sign ignored).</param>
        /// <param name="moment">Design moment MEd, N·mm/mm (sign ignored).</param>
        /// <param name="groundKh">Soil inertia acceleration ratio (ag S / g as used by the caller), ≥ 0.</param>
        /// <param name="groundKv">Vertical acceleration ratio, |kv| &lt; 1.</param>
        /// <param name="modelFactor">γRd ≥ 1.</param>
        /// <param name="resistanceFactor">Additional resistance factor of the national approach (for example NTC γR), ≥ 1.</param>
        public static SeismicBearingResult Calculate(double width, double unitWeight, double frictionAngle, double axial, double shear, double moment,
            double groundKh, double groundKv, double modelFactor, double resistanceFactor)
        {
            if (new[] { width, unitWeight, frictionAngle, axial, shear, moment, groundKh, groundKv, modelFactor, resistanceFactor }.Any(x => double.IsNaN(x) || double.IsInfinity(x))
                || width <= 0 || unitWeight <= 0 || frictionAngle <= 0 || frictionAngle > 45 * SoilUnits.Degree || axial <= 0 || groundKh < 0 || Math.Abs(groundKv) >= 1
                || modelFactor < 1 || resistanceFactor < 1)
                throw new ArgumentException("Seismic bearing capacity: check geometry, strengths, accelerations and the vertical resultant.");
            double tan = Math.Tan(frictionAngle);
            double nq = Math.Exp(Math.PI * tan) * Math.Pow(Math.Tan(Math.PI / 4 + frictionAngle / 2), 2);
            double ng = 2 * (nq - 1) * tan;
            double nmax = .5 * unitWeight * (1 - groundKv) * width * width * ng;
            double f = modelFactor * groundKh / tan;
            double cap = 1 - .96 * f;
            double factor = modelFactor * resistanceFactor / nmax;
            double nn = axial * factor, vv = Math.Abs(shear) * factor, mm = Math.Abs(moment) * factor / width;
            if (cap <= 0 || 1 - .41 * f <= 0 || 1 - .32 * f <= 0)
                return new SeismicBearingResult(0, null, nmax, f, nn, vv, mm, 0, null, SeismicBearingStatus.DomainExhausted);
            double limit = Math.Pow(cap, .39);
            double Interaction(double scale)
            {
                double x = nn * scale, gap = limit - x;
                if (x <= 0) return 0;
                if (gap <= 0) return double.PositiveInfinity;
                double denominator = Math.Pow(x, .92) * Math.Pow(gap, 1.25);
                return (Math.Pow(1 - .41 * f, 1.14) * Math.Pow(2.90 * vv * scale, 1.14) + Math.Pow(1 - .32 * f, 1.01) * Math.Pow(2.80 * mm * scale, 1.01)) / denominator;
            }
            double low = 0, high = limit / nn;
            if (vv > 0 || mm > 0)
                for (int i = 0; i < 100; i++) { double mid = (low + high) / 2; if (Interaction(mid) <= 1) low = mid; else high = mid; }
            else low = high;
            double ratio = 1 / low, interaction = Interaction(1);
            bool finite = !double.IsInfinity(ratio) && !double.IsNaN(ratio);
            return new SeismicBearingResult(axial * low, finite ? ratio : (double?)null, nmax, f, nn, vv, mm, limit,
                double.IsInfinity(interaction) || double.IsNaN(interaction) ? (double?)null : interaction, ratio <= 1 ? SeismicBearingStatus.Satisfied : SeismicBearingStatus.NotSatisfied);
        }
    }
}
