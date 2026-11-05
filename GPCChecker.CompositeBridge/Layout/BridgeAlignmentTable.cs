#nullable disable
using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;

namespace GPC.Checkers.CompositeBridge.Layout
{
    /// <summary>
    /// Exact chainage table of an alignment defined by a smooth host curve (e.g. a Rhino NURBS with continuous tangent), without
    /// approximating it by a Geometry curve (review B4). Stations increase from 0 to the length measured on the host curve, in mm, with
    /// the points and the unit tangents at those stations. The length is the host's own, so stations measured on the host curve stay
    /// exact; between two stations a point is placed by the cubic Hermite interpolation of the end points and tangents (C1 continuous),
    /// which is never used to recompute the length.
    /// </summary>
    [Serializable, DataContract]
    public sealed class BridgeAlignmentTable
    {
        [field: DataMember(Order = 0)] public double[] Stations { get; set; } = Array.Empty<double>();
        [field: DataMember(Order = 1)] public Point3d[] Points { get; set; } = Array.Empty<Point3d>();
        [field: DataMember(Order = 2)] public Vector3d[] Tangents { get; set; } = Array.Empty<Vector3d>();

        /// <summary>The host curve length (mm): the last station.</summary>
        public double Length => Stations[Stations.Length - 1];

        public void Validate()
        {
            if (Stations == null || Points == null || Tangents == null || Stations.Length < 2 || Points.Length != Stations.Length || Tangents.Length != Stations.Length
                || Points.Any(p => p == null) || Tangents.Any(t => t == null))
                throw new ArgumentException("InvalidBridgeAlignmentTable");
            if (Stations[0] != 0) throw new ArgumentException("AlignmentTableMustStartAtZero");
            for (int i = 0; i < Stations.Length; i++)
            {
                BridgeDimensions.Finite(Stations[i]); BridgeDimensions.Finite(Points[i].X); BridgeDimensions.Finite(Points[i].Y); BridgeDimensions.Finite(Points[i].Z);
                var t = Tangents[i]; double norm = Math.Sqrt(t.X * t.X + t.Y * t.Y + t.Z * t.Z);
                if (double.IsNaN(norm) || Math.Abs(norm - 1) > 1e-9) throw new ArgumentException("AlignmentTangentMustBeUnit");
                if (i == 0) continue;
                double step = Stations[i] - Stations[i - 1];
                if (!(step > BridgeDimensions.Tolerance)) throw new ArgumentException("AlignmentStationsMustIncrease");
                // A curve is never shorter than its chord.
                if (Distance(Points[i - 1], Points[i]) > step * (1 + 1e-12) + BridgeDimensions.Tolerance) throw new ArgumentException("AlignmentChordExceedsStationStep");
            }
        }

        /// <summary>The point at a station (mm), exact at the tabulated stations.</summary>
        public Point3d PointAt(double station)
        {
            int i = Interval(station, out double t, out double h);
            double h00 = 2 * t * t * t - 3 * t * t + 1, h10 = t * t * t - 2 * t * t + t, h01 = -2 * t * t * t + 3 * t * t, h11 = t * t * t - t * t;
            Point3d a = Points[i], b = Points[i + 1]; Vector3d ta = Tangents[i], tb = Tangents[i + 1];
            return new Point3d(h00 * a.X + h10 * h * ta.X + h01 * b.X + h11 * h * tb.X, h00 * a.Y + h10 * h * ta.Y + h01 * b.Y + h11 * h * tb.Y,
                h00 * a.Z + h10 * h * ta.Z + h01 * b.Z + h11 * h * tb.Z);
        }

        /// <summary>The unit tangent at a station, exact at the tabulated stations.</summary>
        public Vector3d TangentAt(double station)
        {
            int i = Interval(station, out double t, out double h);
            double d00 = (6 * t * t - 6 * t) / h, d10 = 3 * t * t - 4 * t + 1, d01 = (-6 * t * t + 6 * t) / h, d11 = 3 * t * t - 2 * t;
            Point3d a = Points[i], b = Points[i + 1]; Vector3d ta = Tangents[i], tb = Tangents[i + 1];
            double x = d00 * a.X + d10 * ta.X + d01 * b.X + d11 * tb.X, y = d00 * a.Y + d10 * ta.Y + d01 * b.Y + d11 * tb.Y, z = d00 * a.Z + d10 * ta.Z + d01 * b.Z + d11 * tb.Z;
            double norm = Math.Sqrt(x * x + y * y + z * z);
            if (!(norm > 0)) throw new InvalidOperationException("DegenerateAlignmentTangent");
            return new Vector3d(x / norm, y / norm, z / norm);
        }

        private int Interval(double station, out double t, out double h)
        {
            Validate(); BridgeDimensions.Finite(station);
            if (station < -BridgeDimensions.Tolerance || station > Length + BridgeDimensions.Tolerance) throw new ArgumentOutOfRangeException(nameof(station));
            int i = Array.BinarySearch(Stations, Math.Min(Math.Max(station, 0), Length));
            if (i < 0) i = ~i - 1;
            i = Math.Min(Math.Max(i, 0), Stations.Length - 2);
            h = Stations[i + 1] - Stations[i]; t = Math.Min(Math.Max((station - Stations[i]) / h, 0), 1);
            return i;
        }

        private static double Distance(Point3d a, Point3d b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
