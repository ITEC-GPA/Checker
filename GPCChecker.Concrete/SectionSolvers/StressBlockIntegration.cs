using System;
using System.Collections.Generic;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// Geometric integration of a constant stress over an affine strain band. Coordinates are relative
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
            foreach (var cell in _cells) _whole += cell.Moments;
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
