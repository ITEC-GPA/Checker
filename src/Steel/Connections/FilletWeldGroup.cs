using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    internal static class FilletWeldGroup
    {
        private sealed class EffectiveSegment
        {
            internal FilletWeldSegment Source;
            internal double X1, Y1, X2, Y2, Length, Area, Factor;
        }
        private sealed class ResultantSum
        {
            internal double Sum, AbsoluteTerms;
            private double compensation;
            internal void Add(double term)
            {
                double corrected = term - compensation, next = Sum + corrected;
                compensation = (next - Sum) - corrected; Sum = next; AbsoluteTerms += Math.Abs(term);
            }
            internal bool Matches(double expected)
                => Math.Abs(Sum - expected) <= 128 * 2.2204460492503131e-16 * (AbsoluteTerms + Math.Abs(expected));
        }

        /// <summary>Exact line integrals and endpoint checks, not a mesh or finite element model.
        /// Stress is affine on each straight segment; both verification criteria are convex there, so no
        /// interior station can exceed both endpoints. Forces are transferred at the weld roots.</summary>
        public static FilletWeldGroupResult Verify(FilletWeldGroupInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var trace = new List<SectionResistanceValue>(); var diagnostics = new List<string>();
            var points = new List<FilletWeldPointResult>(); var segments = new List<EffectiveSegment>();
            void Value(string name, double value, string unit)
            { FilletWeldNumbers.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Require(bool condition, string reason) { if (!condition) diagnostics.Add(reason); }
            FilletWeldGroupResult Result() => new FilletWeldGroupResult(input, points, trace, diagnostics);

            Require(Math.Min(input.FirstMaterial.Thickness, input.SecondMaterial.Thickness) >= 4, "PARENT_THICKNESS_BELOW_4_MM_SCOPE");
            Require(!string.IsNullOrWhiteSpace(input.ConsumableCompatibilitySource), "CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED");
            Require(input.LocalEccentricityAccounted && !string.IsNullOrWhiteSpace(input.LocalEccentricitySource), "LOCAL_ECCENTRICITY_MODEL_REQUIRED");
            for (int i = 0; i < input.Segments.Count; i++)
            {
                var s = input.Segments[i];
                Require(s.Throat >= 3, "SEGMENT_" + i + "_THROAT_BELOW_3_MM_SCOPE");
                Require(s.FusionAngleDegrees >= 60 && s.FusionAngleDegrees <= 120, "SEGMENT_" + i + "_FUSION_ANGLE_OUTSIDE_FILLET_SCOPE");
                Require(s.EndCondition != FilletWeldEndCondition.Defective, "SEGMENT_" + i + "_DEFECTIVE_ENDS");
                // NTC uses the full cord length when ends are not missing/defective. EC requires full-size
                // evidence or a total 2a deduction. The EC deduction is symmetric about the nominal midpoint.
                double trim = !input.Code.IsNtc && s.EndCondition == FilletWeldEndCondition.NominalWithoutFullSizeEvidence ? s.Throat : 0;
                double length = s.Length - 2 * trim;
                Require(length >= Math.Max(30, 6 * s.Throat), "SEGMENT_" + i + "_EFFECTIVE_LENGTH_BELOW_SCOPE");
                Require(input.LapLength > 0 || s.Length <= 1700, "SEGMENT_" + i + "_LONG_NONLAP_WELD_REQUIRES_SPECIAL_MODEL");
                double factor = input.LapLength <= 150 * s.Throat ? 1 : 1.2 - .2 * input.LapLength / (150 * s.Throat);
                Require(factor > 0, "SEGMENT_" + i + "_LONG_LAP_FACTOR_NONPOSITIVE");
                double tx = (s.End.X - s.Start.X) / s.Length, ty = (s.End.Y - s.Start.Y) / s.Length;
                segments.Add(new EffectiveSegment { Source = s, X1 = s.Start.X + trim * tx, Y1 = s.Start.Y + trim * ty,
                    X2 = s.End.X - trim * tx, Y2 = s.End.Y - trim * ty, Length = length, Area = length * s.Throat, Factor = factor });
                Value("Segment" + i + ".EffectiveLength", length, "mm"); Value("Segment" + i + ".LongJointFactor", factor, "-");
            }
            // Coincident physical roots must not be counted twice; a shared endpoint is harmless.
            for (int i = 0; i < segments.Count; i++)
                for (int j = i + 1; j < segments.Count; j++)
                    Require(!Overlap(segments[i], segments[j]), "OVERLAPPING_ROOTS_" + i + "_" + j);
            if (diagnostics.Count != 0) return Result();

            double area = segments.Sum(s => s.Area);
            // Shift before summation to avoid losing the group size when its coordinates have a large offset.
            double originX = segments[0].X1, originY = segments[0].Y1;
            double cxLocal = segments.Sum(s => s.Area * ((s.X1 - originX) + (s.X2 - originX)) / 2) / area;
            double cyLocal = segments.Sum(s => s.Area * ((s.Y1 - originY) + (s.Y2 - originY)) / 2) / area;
            double cx = originX + cxLocal, cy = originY + cyLocal;
            double xx = 0, yy = 0, xy = 0;
            foreach (var s in segments)
            {
                double x1 = s.X1 - originX - cxLocal, x2 = s.X2 - originX - cxLocal;
                double y1 = s.Y1 - originY - cyLocal, y2 = s.Y2 - originY - cyLocal;
                xx += s.Area * (x1 * x1 + x1 * x2 + x2 * x2) / 3;
                yy += s.Area * (y1 * y1 + y1 * y2 + y2 * y2) / 3;
                xy += s.Area * (2 * x1 * y1 + x1 * y2 + x2 * y1 + 2 * x2 * y2) / 6;
            }
            double polar = xx + yy;
            var a = input.Actions;
            double rx = a.ApplicationX - originX - cxLocal, ry = a.ApplicationY - originY - cyLocal, rz = a.ApplicationZ;
            double mx = a.Mx + ry * a.Fz - rz * a.Fy;
            double my = a.My + rz * a.Fx - rx * a.Fz;
            double mz = a.Mz + rx * a.Fy - ry * a.Fx;
            Value("Area", area, "mm2"); Value("CentroidX", cx, "mm"); Value("CentroidY", cy, "mm");
            Value("Ix", yy, "mm4"); Value("Iy", xx, "mm4"); Value("Ixy", xy, "mm4"); Value("J", polar, "mm4");
            Value("MxAtCentroid", mx, "Nmm"); Value("MyAtCentroid", my, "Nmm"); Value("MzAtCentroid", mz, "Nmm");

            double bx, by, rootX, rootY;
            bool collinear = Collinear(segments, out rootX, out rootY);
            if (!SolveBending(xx, yy, xy, -my, mx, collinear, rootX, rootY, out bx, out by))
            { diagnostics.Add("BENDING_NOT_REPRESENTABLE_BY_LINE_GROUP"); return Result(); }
            double sx0 = a.Fx / area, sy0 = a.Fy / area, sz0 = a.Fz / area, torsion = mz / polar;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                double tx = (s.Source.End.X - s.Source.Start.X) / s.Source.Length;
                double ty = (s.Source.End.Y - s.Source.Start.Y) / s.Source.Length;
                double cosine = Math.Cos(s.Source.ThroatNormalAngleRadians), sine = Math.Sin(s.Source.ThroatNormalAngleRadians);
                for (int end = 0; end < 2; end++)
                {
                    double x = end == 0 ? s.X1 : s.X2, y = end == 0 ? s.Y1 : s.Y2;
                    double dx = x - originX - cxLocal, dy = y - originY - cyLocal;
                    double sx = sx0 - torsion * dy, sy = sy0 + torsion * dx, sz = sz0 + bx * dx + by * dy;
                    double transverse = -ty * sx + tx * sy;
                    double sigma = cosine * transverse + sine * sz;
                    double shearPerpendicular = -sine * transverse + cosine * sz;
                    double shearParallel = tx * sx + ty * sy;
                    var verification = FilletWeldResistance.Evaluate(input.Code, input.FirstMaterial, input.SecondMaterial,
                        sigma, shearPerpendicular, shearParallel, input.Method, s.Factor);
                    points.Add(new FilletWeldPointResult(i, end == 0, new ConnectionPoint2D(x, y), sx, sy, sz, s.Source.Throat, verification));
                }
            }
            // Integrate the actual endpoint line forces. Simpson is exact for these linear force and quadratic
            // moment functions. The gate uses each resultant's own terms, never an unrelated larger action.
            var recovered = new[] { new ResultantSum(), new ResultantSum(), new ResultantSum(),
                new ResultantSum(), new ResultantSum(), new ResultantSum() };
            for (int i = 0; i < segments.Count; i++)
            {
                var p = points[2 * i]; var q = points[2 * i + 1];
                for (int station = 0; station <= 2; station++)
                {
                    double fraction = station / 2.0, weight = segments[i].Length / 6 * (station == 1 ? 4 : 1);
                    double x = (p.Position.X - originX - cxLocal) * (1 - fraction) + (q.Position.X - originX - cxLocal) * fraction;
                    double y = (p.Position.Y - originY - cyLocal) * (1 - fraction) + (q.Position.Y - originY - cyLocal) * fraction;
                    double qx = p.ForcePerLengthX * (1 - fraction) + q.ForcePerLengthX * fraction;
                    double qy = p.ForcePerLengthY * (1 - fraction) + q.ForcePerLengthY * fraction;
                    double qz = p.ForcePerLengthZ * (1 - fraction) + q.ForcePerLengthZ * fraction;
                    recovered[0].Add(qx * weight); recovered[1].Add(qy * weight); recovered[2].Add(qz * weight);
                    recovered[3].Add(y * qz * weight); recovered[4].Add(-x * qz * weight);
                    recovered[5].Add(x * qy * weight); recovered[5].Add(-y * qx * weight);
                }
            }
            var names = new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" }; var expected = new[] { a.Fx, a.Fy, a.Fz, mx, my, mz };
            for (int i = 0; i < recovered.Length; i++)
            {
                Value("Recovered" + names[i], recovered[i].Sum, i < 3 ? "N" : "Nmm");
                Require(recovered[i].Matches(expected[i]), "EQUILIBRIUM_" + names[i].ToUpperInvariant() + "_NOT_RECOVERED");
            }
            if (diagnostics.Count != 0) points.Clear();
            return Result();
        }

        private static bool SolveBending(double xx, double yy, double xy, double targetX, double targetY,
            bool collinear, double rootX, double rootY, out double bx, out double by)
        {
            if (targetX == 0 && targetY == 0) { bx = by = 0; return true; }
            double scale = Math.Max(xx, yy);
            if (!(scale > 0)) { bx = by = 0; return targetX == 0 && targetY == 0; }
            double x = xx / scale, y = yy / scale, c = xy / scale;
            double determinant = x * y - c * c;
            if (determinant > 1e-12)
            {
                bx = (y * (targetX / scale) - c * (targetY / scale)) / determinant;
                by = (x * (targetY / scale) - c * (targetX / scale)) / determinant;
                return true;
            }
            if (!collinear) { bx = by = 0; return false; }
            // Rank-one line groups can carry only the moment perpendicular to their root.
            // Tolerance is only the floating-point cancellation of the null projection's two terms.
            // In particular, a tiny unsupported moment alongside a large supported moment is NOT discarded.
            double ux = rootX, uy = rootY;
            double longitudinal = targetX * ux + targetY * uy;
            double first = -targetX * uy, second = targetY * ux;
            if (!CancellationZero(first, second)) { bx = by = 0; return false; }
            double eigenvalue = ux * ux * xx + 2 * ux * uy * xy + uy * uy * yy;
            bx = longitudinal / eigenvalue * ux; by = longitudinal / eigenvalue * uy;
            return true;
        }

        private static bool CancellationZero(double first, double second)
            => Math.Abs(first + second) <= 16 * 2.2204460492503131e-16 * (Math.Abs(first) + Math.Abs(second));

        private static bool Collinear(IList<EffectiveSegment> segments, out double ux, out double uy)
        {
            var reference = segments[0].Source;
            double dx = reference.End.X - reference.Start.X, dy = reference.End.Y - reference.Start.Y;
            ux = dx / reference.Length; uy = dy / reference.Length;
            foreach (var s in segments)
                foreach (var p in new[] { s.Source.Start, s.Source.End })
                {
                    double x = p.X - reference.Start.X, y = p.Y - reference.Start.Y;
                    if (!CancellationZero(x * dy, -y * dx)) return false;
                }
            return true;
        }

        private static bool Overlap(EffectiveSegment first, EffectiveSegment second)
        {
            double dx = first.Source.End.X - first.Source.Start.X, dy = first.Source.End.Y - first.Source.Start.Y;
            double length = first.Source.Length, ux = dx / length, uy = dy / length;
            double ax = second.Source.Start.X - first.Source.Start.X, ay = second.Source.Start.Y - first.Source.Start.Y;
            double bx = second.Source.End.X - first.Source.Start.X, by = second.Source.End.Y - first.Source.Start.Y;
            double tolerance = 1e-10 * Math.Max(length, second.Source.Length);
            if (Math.Abs(ax * uy - ay * ux) > tolerance || Math.Abs(bx * uy - by * ux) > tolerance) return false;
            double a = ax * ux + ay * uy, b = bx * ux + by * uy;
            return Math.Min(length, Math.Max(a, b)) - Math.Max(0, Math.Min(a, b)) > tolerance;
        }
    }
}
