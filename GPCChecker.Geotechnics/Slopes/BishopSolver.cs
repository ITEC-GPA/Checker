namespace GPC.Checkers.Geotechnics.Slopes
{
    /// <summary>
    /// Simplified Bishop equilibrium on assigned slices, without geometry or partial factors. Transferred from ANTHEA
    /// (Anthea.Calculations.Geotechnics.BishopSolver, commit fe4652c). Units: mm, N/mm, MPa, rad.
    /// </summary>
    public static class BishopSolver
    {
        public const string Formula = "F = Σ{[c'd·b + (V − u·b)·tan φ'd]/mα}/D; mα = cos α + sin α·tan φ'd/F. D = Σ[V·(xG − xc) + H·(yc − yH) + M]/R; "
            + "N' = [V − u·l·cos α − c'd·l·sin α/F]/mα; l = b/cos α. V = (1 − kv)·W + Vext. Undrained: φ = 0, c = cu,d, u = 0. η = γR/F.";

        /// <summary>
        /// Factor of safety F of the circle by bisection on the residual Σ R(F) − F·D, above every singularity of mα. Null when the driving moment
        /// is not positive, when no root exists, or when a slice would need tension (N' &lt; 0) or mα ≤ 0: no tensile strength and no clipping.
        /// </summary>
        public static SlopeSurfaceResult? Solve(SlipCircle circle, IReadOnlyList<SlopeSlice> input, double resistanceFactor, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Count == 0 || double.IsNaN(resistanceFactor) || double.IsInfinity(resistanceFactor) || resistanceFactor <= 0)
                throw new ArgumentException("Bishop: slices and a positive γR are required.");
            double driving = input.Sum(s => s.Driving);
            if (driving <= 1e-9 || double.IsNaN(driving) || double.IsInfinity(driving)) return null;
            double Resistance(SlopeSlice s, double f)
            {
                double tan = Math.Tan(s.FrictionAngle);
                double m = Math.Cos(s.Alpha) + Math.Sin(s.Alpha) * tan / f;
                return (s.Cohesion * s.Width + (s.Vertical - s.PorePressure * s.Width) * tan) / m;
            }
            double Residual(double f) => input.Sum(s => Resistance(s, f)) - f * driving;
            // Stay above every singularity of mα; never converge across a pole.
            double lower = Math.Max(1e-7, input.Max(s => -Math.Tan(s.Alpha) * Math.Tan(s.FrictionAngle)) + 1e-7);
            double upper = Math.Max(2, lower * 2), fl = Residual(lower);
            if (double.IsNaN(fl) || double.IsInfinity(fl) || fl < 0) return null;
            while (Residual(upper) > 0 && upper < 1e5) { token.ThrowIfCancellationRequested(); upper *= 2; }
            if (Residual(upper) > 0) return null;
            int iteration = 0; double factor = 0, residual = 0;
            for (; iteration < 100; iteration++)
            {
                token.ThrowIfCancellationRequested(); factor = (lower + upper) / 2; residual = Residual(factor);
                if (Math.Abs(residual) <= 1e-9 * Math.Max(1, factor * driving)) break;
                if (residual > 0) lower = factor; else upper = factor;
            }
            var output = new SlopeSlice[input.Count];
            for (int i = 0; i < input.Count; i++)
            {
                var s = input[i];
                double tan = Math.Tan(s.FrictionAngle), b = s.Width, l = b / Math.Cos(s.Alpha);
                double m = Math.Cos(s.Alpha) + Math.Sin(s.Alpha) * tan / factor;
                double normal = (s.Vertical - s.PorePressure * b - s.Cohesion * l * Math.Sin(s.Alpha) / factor) / m;
                if (normal < -1e-6 || m <= 0 || double.IsNaN(normal) || double.IsInfinity(normal)) return null;
                double r = Resistance(s, factor);
                output[i] = s.Solved(normal, r, r / factor, m);
            }
            return new SlopeSurfaceResult(circle, factor, resistanceFactor / factor, iteration + 1, residual, driving, output.Sum(s => s.Resistance), output);
        }
    }
}
