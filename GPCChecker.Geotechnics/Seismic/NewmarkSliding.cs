namespace GPC.Checkers.Geotechnics.Seismic
{
    /// <summary>Sample of an accelerogram: time, s, and ground acceleration in units of g.</summary>
    public readonly struct AccelerogramSample
    {
        public double Time { get; }
        public double Acceleration { get; }
        public AccelerogramSample(double time, double acceleration) { Time = time; Acceleration = acceleration; }
    }

    /// <summary>State of the block at a time: scaled ground acceleration (g), relative velocity (mm/s) and cumulated displacement (mm).</summary>
    public readonly struct SlidingPoint
    {
        public double Time { get; }
        public double Acceleration { get; }
        public double Velocity { get; }
        public double Displacement { get; }
        public SlidingPoint(double time, double acceleration, double velocity, double displacement) { Time = time; Acceleration = acceleration; Velocity = velocity; Displacement = displacement; }
    }

    public sealed class SlidingResult
    {
        /// <summary>Permanent displacement, mm.</summary>
        public double Displacement { get; }
        /// <summary>Peak relative velocity, mm/s.</summary>
        public double PeakVelocity { get; }
        /// <summary>Peak ground acceleration of the scaled record, g.</summary>
        public double Pga { get; }
        public IReadOnlyList<SlidingPoint> Points { get; }
        internal SlidingResult(double displacement, double peakVelocity, double pga, SlidingPoint[] points) { Displacement = displacement; PeakVelocity = peakVelocity; Pga = pga; Points = points; }
    }

    /// <summary>
    /// Newmark rigid block sliding in one direction under a piecewise-linear accelerogram: the block slides while the ground acceleration exceeds the
    /// yield acceleration and until its relative velocity returns to zero; exact integration between threshold crossings and stops; after the record
    /// the block stops under zero ground acceleration. No magnitude regression. Transferred from ANTHEA (Anthea.Calculations.Geotechnics.NewmarkSliding,
    /// commit fe4652c). Units: s, g, mm, mm/s; g = 9.81 m/s² as the legacy calculation.
    /// </summary>
    public static class NewmarkSliding
    {
        /// <summary>Gravity acceleration, mm/s² (9.81 m/s²).</summary>
        public const double Gravity = 9810;

        public static SlidingResult Calculate(IReadOnlyList<AccelerogramSample> samples, double yieldAcceleration, double scale = 1)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (samples.Count < 2 || samples.Count > 200000 || double.IsNaN(yieldAcceleration + scale) || double.IsInfinity(yieldAcceleration + scale) || yieldAcceleration <= 0 || scale <= 0
                || samples.Any(s => double.IsNaN(s.Time + s.Acceleration) || double.IsInfinity(s.Time + s.Acceleration)) || samples[0].Time < 0
                || Enumerable.Range(1, samples.Count - 1).Any(i => samples[i].Time <= samples[i - 1].Time))
                throw new ArgumentException("Newmark: accelerogram with increasing times and finite accelerations, positive scale and yield acceleration.");
            double speed = 0, displacement = 0, peak = 0;
            var output = new List<SlidingPoint> { new SlidingPoint(samples[0].Time, samples[0].Acceleration * scale, 0, 0) };
            for (int i = 1; i < samples.Count; i++)
            {
                double dt = samples[i].Time - samples[i - 1].Time;
                double aa = Gravity * (samples[i - 1].Acceleration * scale - yieldAcceleration), bb = Gravity * (samples[i].Acceleration * scale - yieldAcceleration);
                var cuts = new List<double> { 0, dt }; if (aa * bb < 0) cuts.Insert(1, dt * -aa / (bb - aa));
                for (int j = 1; j < cuts.Count; j++)
                {
                    double start = cuts[j - 1], length = cuts[j] - start, acceleration = aa + (bb - aa) * start / dt, jerk = (bb - aa) / dt;
                    double end = speed + acceleration * length + .5 * jerk * length * length;
                    if (speed <= 0 && acceleration + jerk * length / 2 <= 0) continue;
                    double run = length;
                    if (end < 0)
                    {
                        double lo = 0, hi = length;
                        for (int k = 0; k < 60; k++) { double mid = (lo + hi) / 2; if (speed + acceleration * mid + .5 * jerk * mid * mid > 0) lo = mid; else hi = mid; }
                        run = (lo + hi) / 2;
                    }
                    displacement += speed * run + .5 * acceleration * run * run + jerk * run * run * run / 6;
                    // At the stop the velocity is zero: the residual of the bisection (±1e-17) must not keep the block sliding.
                    speed = end < 0 ? 0 : Math.Max(0, speed + acceleration * run + .5 * jerk * run * run); peak = Math.Max(peak, speed);
                }
                output.Add(new SlidingPoint(samples[i].Time, samples[i].Acceleration * scale, speed, displacement));
            }
            // After the record the block stops under zero ground acceleration.
            if (speed > 0)
            {
                displacement += speed * speed / (2 * Gravity * yieldAcceleration);
                output.Add(new SlidingPoint(samples[samples.Count - 1].Time + speed / (Gravity * yieldAcceleration), 0, 0, displacement));
            }
            return new SlidingResult(displacement, peak, samples.Max(s => Math.Abs(s.Acceleration * scale)), output.ToArray());
        }
    }
}
