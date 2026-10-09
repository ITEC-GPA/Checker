using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Foundations
{
    /// <summary>Layer below the foundation for the oedometric settlement: thickness, mm, and constrained modulus Eoed, MPa.</summary>
    public sealed class SettlementLayer
    {
        public string Name { get; }
        public double Thickness { get; }
        public double ConstrainedModulus { get; }
        public SettlementLayer(string name, double thickness, double constrainedModulus)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name)); Thickness = thickness; ConstrainedModulus = constrainedModulus;
        }

        /// <summary>
        /// The layers of a Model profile below the foundation level (mm), with their constrained modulus. A layer without Eoed is missing data:
        /// ArgumentException, never an assumed modulus.
        /// </summary>
        public static SettlementLayer[] FromProfile(SoilProfile profile, double foundationElevation)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (double.IsNaN(foundationElevation) || foundationElevation > profile.GroundSurface + 1e-9 || foundationElevation <= profile.Base)
                throw new ArgumentException("The foundation level must lie inside the profile.", nameof(foundationElevation));
            var layers = new List<SettlementLayer>();
            foreach (var layer in profile.Layers)
            {
                double top = Math.Min(layer.Top, foundationElevation);
                if (top <= layer.Bottom) continue;
                if (!layer.Soil.ConstrainedModulus.HasValue) throw new ArgumentException("Soil " + layer.Soil.Name + ": the constrained modulus Eoed is required for the settlement.");
                layers.Add(new SettlementLayer(layer.Soil.Name, top - layer.Bottom, layer.Soil.ConstrainedModulus.Value));
            }
            return layers.ToArray();
        }
    }

    /// <summary>Slice of the integration: layer, top and bottom depth below the foundation (mm), net stress increment (MPa), Eoed (MPa), settlement (mm).</summary>
    public sealed class SettlementSlice
    {
        public string Soil { get; }
        public double Top { get; }
        public double Bottom { get; }
        public double Stress { get; }
        public double ConstrainedModulus { get; }
        public double Settlement { get; }
        internal SettlementSlice(string soil, double top, double bottom, double stress, double modulus, double settlement)
        {
            Soil = soil; Top = top; Bottom = bottom; Stress = stress; ConstrainedModulus = modulus; Settlement = settlement;
        }
    }

    public sealed class SettlementResult
    {
        /// <summary>Total settlement, mm.</summary>
        public double Settlement { get; }
        /// <summary>Net stress increment at the bottom of the last layer, MPa.</summary>
        public double BottomStress { get; }
        public IReadOnlyList<SettlementSlice> Slices { get; }
        internal SettlementResult(double settlement, double bottomStress, SettlementSlice[] slices) { Settlement = settlement; BottomStress = bottomStress; Slices = slices; }
    }

    /// <summary>
    /// One-dimensional oedometric settlement under an infinitely long strip: vertical stress increments of Boussinesq for a linearly varying strip
    /// load (exact integral) minus the excavation unloading over the footing width, integrated with two Gauss points per slice. Transferred from
    /// ANTHEA (Anthea.Calculations.Geotechnics.FoundationSettlement, commit fe4652c). Units: mm, MPa (N/mm²).
    /// </summary>
    public static class FoundationSettlement
    {
        /// <summary>
        /// Vertical stress at abscissa x and depth z (mm) under a strip load varying linearly from pLeft at left to pRight at right (same unit
        /// as the stress): exact integral of 2 z³/(π((x − s)² + z²)²).
        /// </summary>
        public static double Stress(double x, double z, double left, double right, double pLeft, double pRight)
        {
            if (z <= 0 || right <= left) throw new ArgumentException("Influence: positive depth and width are required.");
            double slope = (pRight - pLeft) / (right - left), atX = pLeft + slope * (x - left);
            double A(double u) => (Math.Atan(u / z) + z * u / (u * u + z * z)) / Math.PI;
            double B(double u) => -z * z * z / (Math.PI * (u * u + z * z));
            double l = left - x, r = right - x;
            return atX * (A(r) - A(l)) + slope * (B(r) - B(l));
        }

        /// <summary>
        /// Settlement at abscissa x of the strip load (left, right, pLeft, pRight) less the removed pressure over the footing width (0 to
        /// footingWidth), through the layers from the foundation level down. Each layer has at least the given subdivisions, more when it is thicker
        /// than the footing width. A net unloading needs a recompression modulus: ArgumentException, no fictitious settlement.
        /// </summary>
        public static SettlementResult Calculate(IReadOnlyList<SettlementLayer> layers, double x, double left, double right, double pLeft, double pRight,
            double removedPressure, double footingWidth, int subdivisions = 40)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            if (layers.Count == 0 || layers.Any(l => l == null || !Finite(l.Thickness + l.ConstrainedModulus) || l.Thickness <= 0 || l.ConstrainedModulus <= 0)
                || !Finite(x + pLeft + pRight + removedPressure + footingWidth) || footingWidth <= 0 || subdivisions < 10 || subdivisions > 1000)
                throw new ArgumentException("Settlement: positive thicknesses and constrained moduli of the layers below the foundation, footing width and 10-1000 subdivisions.");
            var slices = new List<SettlementSlice>(); double z = 0;
            double Delta(double depth) => Stress(x, depth, left, right, pLeft, pRight) - Stress(x, depth, 0, footingWidth, removedPressure, removedPressure);
            foreach (var layer in layers)
            {
                int count = Math.Max(subdivisions, (int)Math.Ceiling(layer.Thickness / footingWidth * subdivisions));
                if (count > 20000) throw new ArgumentException("Settlement discretisation too fine: check the thicknesses.");
                double dz = layer.Thickness / count;
                for (int i = 0; i < count; i++)
                {
                    double mid = z + dz / 2, off = dz / (2 * Math.Sqrt(3));
                    double stress = (Delta(mid - off) + Delta(mid + off)) / 2;
                    if (stress < -1e-11) throw new ArgumentException("Net unloading: a recompression modulus and a dedicated stress history are required; no fictitious settlement.");
                    slices.Add(new SettlementSlice(layer.Name, z, z + dz, stress, layer.ConstrainedModulus, stress / layer.ConstrainedModulus * dz)); z += dz;
                }
            }
            return new SettlementResult(slices.Sum(s => s.Settlement), Delta(z), slices.ToArray());
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
