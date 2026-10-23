using System;
using System.Collections.Generic;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// Geometric integration of constant or quadratic stress over affine strain bands. Coordinates are relative
    /// to the force reference point. The existing mesh is copied once; evaluations never remesh it.
    /// Holes are already excluded by the mesh. All clipping buffers belong to the current call.
    /// </summary>
    internal sealed class StressBlockIntegration
    {
        internal readonly struct Law
        {
            public readonly double Ultimate, Threshold, Stress;
            public Law(double ultimate, double threshold, double stress)
            { Ultimate = ultimate; Threshold = threshold; Stress = stress; }
            // The material table returns zero at the first of the two repeated threshold abscissae.
            public double At(double strain) => strain >= Ultimate && strain < Threshold ? Stress : 0;
        }

        private struct Vertex
        {
            public double X, Y, E;
            public Vertex(double x, double y, double e = 0) { X = x; Y = y; E = e; }
        }

        private readonly struct Cell
        {
            public readonly Vertex A, B, C;
            public readonly ForceTuple Moments;
            public Cell(Vertex a, Vertex b, Vertex c)
            {
                A = a; B = b; C = c;
                double area = Math.Abs(Cross(a, b, c)) / 2;
                Moments = new ForceTuple(area, -area * (a.Y + b.Y + c.Y) / 3,
                    area * (a.X + b.X + c.X) / 3);
            }
        }

        private readonly Cell[] _cells;
        private readonly Point2d _reference;
        private readonly ForceTuple _whole;
        private readonly double _minX, _maxX, _minY, _maxY;

        internal StressBlockIntegration(Mesh mesh, Point2d reference)
        {
            _reference = new Point2d(reference.X, reference.Y);
            var cells = new List<Cell>();
            Vertex Read(int id) => new Vertex(mesh.Vertices[id].Point.X - reference.X,
                mesh.Vertices[id].Point.Y - reference.Y);
            foreach (var face in mesh.Faces)
            {
                var a = Read(face.A); var b = Read(face.B); var c = Read(face.C);
                if (face.IsTriangle) { cells.Add(new Cell(a, b, c)); continue; }
                var d = Read(face.D);
                // Choose an interior diagonal also for a simple concave quadrangle.
                double abc = Cross(a, b, c), acd = Cross(a, c, d);
                if (abc * acd >= 0)
                { cells.Add(new Cell(a, b, c)); cells.Add(new Cell(a, c, d)); }
                else
                { cells.Add(new Cell(a, b, d)); cells.Add(new Cell(b, c, d)); }
            }
            _cells = cells.ToArray();
            _minX = _minY = double.PositiveInfinity;
            _maxX = _maxY = double.NegativeInfinity;
            foreach (var cell in _cells)
            {
                _whole += cell.Moments;
                _minX = Math.Min(_minX, Math.Min(cell.A.X, Math.Min(cell.B.X, cell.C.X)));
                _maxX = Math.Max(_maxX, Math.Max(cell.A.X, Math.Max(cell.B.X, cell.C.X)));
                _minY = Math.Min(_minY, Math.Min(cell.A.Y, Math.Min(cell.B.Y, cell.C.Y)));
                _maxY = Math.Max(_maxY, Math.Max(cell.A.Y, Math.Max(cell.B.Y, cell.C.Y)));
            }
        }

        private static double Cross(Vertex a, Vertex b, Vertex c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        internal ForceTuple Integrate(StrainPlane plane, Law law)
        {
            double e0 = plane.GetStrain(_reference), gx = plane.ChiX, gy = plane.ChiY;
            if (gx == 0 && gy == 0) return _whole * law.At(e0);
            var first = new Vertex[6]; var second = new Vertex[6];
            var result = new ForceTuple();
            foreach (var cell in _cells)
            {
                var a = cell.A; var b = cell.B; var c = cell.C;
                a.E = e0 + gx * a.X + gy * a.Y;
                b.E = e0 + gx * b.X + gy * b.Y;
                c.E = e0 + gx * c.X + gy * c.Y;
                double min = Math.Min(a.E, Math.Min(b.E, c.E)), max = Math.Max(a.E, Math.Max(b.E, c.E));
                if (min >= law.Threshold || max < law.Ultimate) continue;
                if (min >= law.Ultimate && max <= law.Threshold) { result += cell.Moments; continue; }
                first[0] = a; first[1] = b; first[2] = c;
                int count = Clip(first, 3, second, law.Threshold, true);
                count = Clip(second, count, first, law.Ultimate, false);
                // Fan integration uses vertex differences, avoiding cancellation for thin clipped slivers.
                for (int i = 1; i + 1 < count; i++)
                    result += new Cell(first[0], first[i], first[i + 1]).Moments;
            }
            return result * law.Stress;
        }

        internal ForceTuple Integrate(StrainPlane plane, ParabolaRectangleLaw law)
        {
            double e0 = plane.GetStrain(_reference), gx = plane.ChiX, gy = plane.ChiY;
            if (gx == 0 && gy == 0) return _whole * law.At(e0);
            double boundMin = e0 + Math.Min(gx * _minX, gx * _maxX) + Math.Min(gy * _minY, gy * _maxY);
            double boundMax = e0 + Math.Max(gx * _minX, gx * _maxX) + Math.Max(gy * _minY, gy * _maxY);
            if (boundMin >= law.Ultimate && boundMax <= law.Peak / 2)
                return IntegrateNearPlateau(e0, gx, gy, law);
            var first = new Vertex[6]; var second = new Vertex[6];
            var result = new ForceTuple();
            foreach (var cell in _cells)
            {
                var a = cell.A; var b = cell.B; var c = cell.C;
                a.E = e0 + gx * a.X + gy * a.Y;
                b.E = e0 + gx * b.X + gy * b.Y;
                c.E = e0 + gx * c.X + gy * c.Y;
                double min = Math.Min(a.E, Math.Min(b.E, c.E)), max = Math.Max(a.E, Math.Max(b.E, c.E));
                if (min >= 0 || max < law.Ultimate) continue;
                if (min >= law.Ultimate && max <= law.Peak) { result += cell.Moments; continue; }
                if (min >= law.Peak && max <= 0) { result += Quadratic(a, b, c, law.Peak); continue; }
                if (max > law.Peak)
                {
                    first[0] = a; first[1] = b; first[2] = c;
                    int count = Clip(first, 3, second, 0, true);
                    count = Clip(second, count, first, law.Peak, false);
                    for (int i = 1; i + 1 < count; i++) result += Quadratic(first[0], first[i], first[i + 1], law.Peak);
                }
                if (min < law.Peak)
                {
                    first[0] = a; first[1] = b; first[2] = c;
                    int count = Clip(first, 3, second, law.Peak, true);
                    // This cut only matters for trial planes beyond the ultimate strain.
                    count = Clip(second, count, first, law.Ultimate, false);
                    for (int i = 1; i + 1 < count; i++) result += new Cell(first[0], first[i], first[i + 1]).Moments;
                }
            }
            return result * law.Stress;
        }

        // Near centred compression the bending is the tiny deficit from uniform plateau stress.
        // Accumulate that deficit separately; subtracting large per-cell moments destroys its direction.
        private ForceTuple IntegrateNearPlateau(double e0, double gx, double gy, ParabolaRectangleLaw law)
        {
            var input = new Vertex[6]; var output = new Vertex[6];
            var deficit = new ForceTuple();
            foreach (var cell in _cells)
            {
                var a = cell.A; var b = cell.B; var c = cell.C;
                a.E = e0 + gx * a.X + gy * a.Y;
                b.E = e0 + gx * b.X + gy * b.Y;
                c.E = e0 + gx * c.X + gy * c.Y;
                if (Math.Max(a.E, Math.Max(b.E, c.E)) <= law.Peak) continue;
                if (Math.Min(a.E, Math.Min(b.E, c.E)) >= law.Peak)
                    deficit += PlateauDeficit(a, b, c, law.Peak);
                else
                {
                    input[0] = a; input[1] = b; input[2] = c;
                    int count = Clip(input, 3, output, law.Peak, false);
                    for (int i = 1; i + 1 < count; i++) deficit += PlateauDeficit(output[0], output[i], output[i + 1], law.Peak);
                }
            }
            return (_whole - deficit) * law.Stress;
        }

        private static ForceTuple PlateauDeficit(Vertex a, Vertex b, Vertex c, double peak)
        {
            double area = Math.Abs(Cross(a, b, c)) / 2;
            double ra = (peak - a.E) / peak, rb = (peak - b.E) / peak, rc = (peak - c.E) / peak;
            double sum = ra + rb + rc, quadratic = sum * sum + ra * ra + rb * rb + rc * rc;
            double Moment(double x, double y, double z) => area * ((x + y + z) * quadratic +
                2 * sum * (x * ra + y * rb + z * rc) + 2 * (x * ra * ra + y * rb * rb + z * rc * rc)) / 60;
            return new ForceTuple(area * quadratic / 12, -Moment(a.Y, b.Y, c.Y), Moment(a.X, b.X, c.X));
        }

        // Exact barycentric moments: integral(lambda_i^a lambda_j^b lambda_k^c)
        // = 2*A*a!*b!*c!/(a+b+c+2)!. Integrate 2*r-r^2 and its first spatial moments.
        private static ForceTuple Quadratic(Vertex a, Vertex b, Vertex c, double peak)
        {
            double area = Math.Abs(Cross(a, b, c)) / 2;
            double ra = a.E / peak, rb = b.E / peak, rc = c.E / peak;
            double sum = ra + rb + rc, squares = ra * ra + rb * rb + rc * rc;
            double quadratic = sum * sum + squares;
            double Moment(double x, double y, double z)
            {
                double coordinates = x + y + z, dot = x * ra + y * rb + z * rc;
                double dotSquare = x * ra * ra + y * rb * rb + z * rc * rc;
                return area * ((coordinates * sum + dot) / 6 -
                    (coordinates * quadratic + 2 * sum * dot + 2 * dotSquare) / 60);
            }
            return new ForceTuple(area * (2 * sum / 3 - quadratic / 12),
                -Moment(a.Y, b.Y, c.Y), Moment(a.X, b.X, c.X));
        }

        private static int Clip(Vertex[] input, int count, Vertex[] output, double threshold, bool below)
        {
            if (count == 0) return 0;
            int length = 0;
            var previous = input[count - 1];
            bool previousInside = below ? previous.E <= threshold : previous.E >= threshold;
            for (int i = 0; i < count; i++)
            {
                var current = input[i];
                bool inside = below ? current.E <= threshold : current.E >= threshold;
                if (inside != previousInside)
                {
                    // Interpolate from the endpoint nearest the cut: a tiny sliver must not be obtained
                    // by subtracting nearly equal full-edge lengths (t close to one).
                    var near = Math.Abs(previous.E - threshold) <= Math.Abs(current.E - threshold) ? previous : current;
                    var far = Math.Abs(previous.E - threshold) <= Math.Abs(current.E - threshold) ? current : previous;
                    double t = (threshold - near.E) / (far.E - near.E);
                    output[length++] = new Vertex(near.X + t * (far.X - near.X), near.Y + t * (far.Y - near.Y), threshold);
                }
                if (inside) output[length++] = current;
                previous = current; previousInside = inside;
            }
            return length;
        }

    }
}
