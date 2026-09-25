using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.CompositeBridge
{
    /// <summary>Plastic reference capacities only for EC4-2 6.2.2.4(3) / EC3-1-5 7.1.
    /// They do not replace the elastic effective-section resistance check for class 4.</summary>
    public static class BridgeBendingShear
    {
        public sealed class Block
        {
            public double Bottom, Top, Width, Compression, Tension;
            public Block(double bottom, double top, double width, double compression, double tension)
            { Bottom = bottom; Top = top; Width = width; Compression = compression; Tension = tension; }
        }
        /// <summary>Exact rectangular stress-block integration at N=0. Positive M compresses the top.
        /// Strengths are positive design magnitudes, units mm / MPa; output N mm.</summary>
        public static double PlasticMoment(IEnumerable<Block> source, bool positiveMoment)
        {
            var blocks = source.ToArray();
            if (blocks.Length == 0) throw new ArgumentException("At least one block required.");
            foreach (var b in blocks)
                if (!Finite(b.Bottom) || !Finite(b.Top) || !Finite(b.Width) || !Finite(b.Compression) || !Finite(b.Tension)
                    || b.Top <= b.Bottom || b.Width <= 0 || b.Compression <= 0 || b.Tension < 0)
                    throw new ArgumentException("Invalid plastic stress block.");
            double low = blocks.Min(b => b.Bottom), high = blocks.Max(b => b.Top);
            Func<double, Tuple<double, double>> resultants = y =>
            {
                double n = 0, m = 0;
                foreach (var b in blocks)
                {
                    double split = Math.Max(b.Bottom, Math.Min(b.Top, y));
                    double sb = positiveMoment ? b.Tension : -b.Compression;
                    double st = positiveMoment ? -b.Compression : b.Tension;
                    n += b.Width * (sb * (split - b.Bottom) + st * (b.Top - split));
                    m -= b.Width / 2 * (sb * (split * split - b.Bottom * b.Bottom) + st * (b.Top * b.Top - split * split));
                }
                return Tuple.Create(n, m);
            };
            for (int i = 0; i < 100; i++)
            {
                double mid = (low + high) / 2, n = resultants(mid).Item1;
                if (positiveMoment ? n < 0 : n > 0) low = mid; else high = mid;
            }
            return Math.Abs(resultants((low + high) / 2).Item2);
        }
        private static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        /// <summary>Plastic equilibrium at assigned tensile-positive N. Signed M is about y=0.
        /// Returns both moment bounds, avoiding a fictitious favourable result when N puts zero moment outside the domain.</summary>
        public static AxialMomentBounds AtAxialForce(IEnumerable<Block> source, double axial)
        {
            var blocks = source.ToArray();
            if (blocks.Length == 0 || !Finite(axial)) throw new ArgumentException("Invalid axial equilibrium data.");
            double compression = 0, tension = 0;
            foreach (var b in blocks)
            {
                if (!Finite(b.Bottom) || !Finite(b.Top) || !Finite(b.Width) || !Finite(b.Compression) || !Finite(b.Tension)
                    || b.Top <= b.Bottom || b.Width <= 0 || b.Compression <= 0 || b.Tension < 0) throw new ArgumentException("Invalid block.");
                compression += (b.Top - b.Bottom) * b.Width * b.Compression;
                tension += (b.Top - b.Bottom) * b.Width * b.Tension;
            }
            if (axial < -compression || axial > tension) return new AxialMomentBounds { Feasible = false, Compression = compression, Tension = tension };
            Tuple<double, double> Bound(bool positive)
            {
                double lo = blocks.Min(b => b.Bottom), hi = blocks.Max(b => b.Top);
                Tuple<double, double> Integrate(double y)
                {
                    double n = 0, m = 0;
                    foreach (var b in blocks)
                    {
                        double z = Math.Max(b.Bottom, Math.Min(b.Top, y));
                        double sb = positive ? b.Tension : -b.Compression, st = positive ? -b.Compression : b.Tension;
                        n += b.Width * (sb * (z - b.Bottom) + st * (b.Top - z));
                        m -= b.Width / 2 * (sb * (z * z - b.Bottom * b.Bottom) + st * (b.Top * b.Top - z * z));
                    }
                    return Tuple.Create(n, m);
                }
                for (int i = 0; i < 100; i++)
                { double mid = (lo + hi) / 2; if (positive ? Integrate(mid).Item1 < axial : Integrate(mid).Item1 > axial) lo = mid; else hi = mid; }
                double axis = (lo + hi) / 2; return Tuple.Create(Integrate(axis).Item2, axis);
            }
            var max = Bound(true); var min = Bound(false);
            return new AxialMomentBounds { Feasible = true, Minimum = min.Item1, Maximum = max.Item1,
                AxisPositive = max.Item2, AxisNegative = min.Item2, Compression = compression, Tension = tension };
        }
        public static double Interaction(double moment, double plasticMoment, double flangeMoment, double shear, double webResistance)
        {
            if (!Finite(moment) || !Finite(shear) || !Finite(plasticMoment) || !Finite(flangeMoment) || !Finite(webResistance)
                || plasticMoment <= 0 || flangeMoment < 0 || flangeMoment > plasticMoment * (1 + 1e-10) || webResistance <= 0)
                throw new ArgumentException("Invalid bending/shear interaction data.");
            double eta1 = Math.Abs(moment) / plasticMoment, eta3 = Math.Abs(shear) / webResistance;
            // EC3-1-5 7.1: interaction term only when both thresholds are exceeded.
            return eta3 <= .5 || Math.Abs(moment) <= flangeMoment ? eta1
                : eta1 + (1 - flangeMoment / plasticMoment) * Math.Pow(2 * eta3 - 1, 2);
        }
        /// <summary>Conservative EC3-1-5 7.1 envelope: no flange reserve, elastic normal-stress
        /// utilization instead of a plastic reference. Also covers the fully compressed web branch.
        /// The caller includes all materials and the effective-section elastic checks.</summary>
        public static double ElasticInteraction(double elasticNormalUtilization, double shear, double webResistance)
        {
            if (!Finite(elasticNormalUtilization) || elasticNormalUtilization < 0 || !Finite(shear) || !Finite(webResistance) || webResistance <= 0)
                throw new ArgumentException("Invalid elastic interaction inputs.");
            double eta = Math.Abs(shear) / webResistance;
            return elasticNormalUtilization + (eta <= .5 ? 0 : Math.Pow(2 * eta - 1, 2));
        }
    }
    public sealed class AxialMomentBounds
    { public bool Feasible; public double Minimum, Maximum, AxisPositive, AxisNegative, Compression, Tension; }
}
