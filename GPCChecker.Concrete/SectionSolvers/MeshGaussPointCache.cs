using GPC.Geometry.Meshes;
using GPC.Model.Maths.GaussIntegrations;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// Cache of the Gauss points of the concrete meshes, shared by the solvers of the process (<see cref="SectionSolver"/> constructor). The
    /// points read only the faces of the mesh in the order of its face enumerator (quadrangle or triangle, coordinates of the face points)
    /// and the numbers of Gauss points, with additions and products only: the key holds exactly these values, bit for bit, so an equal mesh
    /// (the same slab in the solvers of the phases and of the sections of a composite bridge) gets the array a new computation would give.
    /// The arrays are only read by the solvers. Bounded (least recently used out) and thread-safe
    /// </summary>
    internal static class MeshGaussPointCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<Key, LinkedListNode<KeyValuePair<Key, GaussIntegration.GlobalCoordinateGaussPoint[][]>>> Entries =
            new Dictionary<Key, LinkedListNode<KeyValuePair<Key, GaussIntegration.GlobalCoordinateGaussPoint[][]>>>();
        private static readonly LinkedList<KeyValuePair<Key, GaussIntegration.GlobalCoordinateGaussPoint[][]>> Order =
            new LinkedList<KeyValuePair<Key, GaussIntegration.GlobalCoordinateGaussPoint[][]>>();
        private static int _capacity = 16;
        private static long _hits, _misses;

        /// <summary>Largest number of meshes kept (default 16: a slab of a few faces with 400 points per quadrangle is some tens of kilobytes)</summary>
        internal static int Capacity
        {
            get { lock (Sync) return _capacity; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                lock (Sync) { _capacity = value; Trim(); }
            }
        }

        internal static long Hits => Interlocked.Read(ref _hits);
        internal static long Misses => Interlocked.Read(ref _misses);
        internal static int Count { get { lock (Sync) return Entries.Count; } }

        /// <summary>
        /// The Gauss points of the faces of the mesh, as <see cref="GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(Mesh,
        /// QuadrangleGaussPoints.GaussPointNumber, TriangleGaussPoints.GaussPointNumber)"/> computes them; a null mesh is passed on
        /// </summary>
        internal static GaussIntegration.GlobalCoordinateGaussPoint[][] Get(Mesh mesh, QuadrangleGaussPoints.GaussPointNumber quad,
            TriangleGaussPoints.GaussPointNumber tri)
        {
            if (mesh == null)
                return GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(mesh, quad, tri);
            var key = new Key(mesh, quad, tri);
            lock (Sync)
            {
                if (Entries.TryGetValue(key, out var node))
                {
                    Order.Remove(node);
                    Order.AddFirst(node);
                    _hits++;
                    return node.Value.Value;
                }
                _misses++;
            }
            var points = GaussIntegration.GetGlobalCoordinateGaussPointsLinearShapeFunction(mesh, quad, tri);
            lock (Sync)
            {
                // computed by another solver at the same time: the same points, the one already there stays
                if (Entries.TryGetValue(key, out var node))
                    return node.Value.Value;
                if (_capacity == 0)
                    return points;
                Entries[key] = Order.AddFirst(new KeyValuePair<Key, GaussIntegration.GlobalCoordinateGaussPoint[][]>(key, points));
                Trim();
                return points;
            }
        }

        /// <summary>Empties the cache and its counters (tests and measurements)</summary>
        internal static void Clear()
        {
            lock (Sync)
            {
                Entries.Clear();
                Order.Clear();
                _hits = _misses = 0;
            }
        }

        private static void Trim()
        {
            while (Entries.Count > _capacity)
            {
                var last = Order.Last;
                Order.RemoveLast();
                Entries.Remove(last.Value.Key);
            }
        }

        /// <summary>The values the Gauss points read: numbers of points, number of faces, then kind and point coordinates (bits) of each face</summary>
        private sealed class Key : IEquatable<Key>
        {
            private readonly long[] _values;
            private readonly int _hash;

            internal Key(Mesh mesh, QuadrangleGaussPoints.GaussPointNumber quad, TriangleGaussPoints.GaussPointNumber tri)
            {
                var values = new List<long> { (long)quad, (long)tri, mesh.FacesCount };
                var faces = mesh.GetFacesEnumerator();
                while (faces.MoveNext())
                {
                    var points = mesh.GetFacePoints(faces.Current);
                    values.Add(faces.Current.IsQuad ? 1 : 0);
                    values.Add(points.Length);
                    foreach (var point in points)
                    {
                        values.Add(BitConverter.DoubleToInt64Bits(point.X));
                        values.Add(BitConverter.DoubleToInt64Bits(point.Y));
                        values.Add(BitConverter.DoubleToInt64Bits(point.Z));
                    }
                }
                _values = values.ToArray();
                unchecked
                {
                    long hash = -3750763034362895579;
                    foreach (long value in _values)
                        hash = (hash ^ value) * 1099511628211;
                    _hash = (int)(hash ^ (hash >> 32));
                }
            }

            public bool Equals(Key other)
            {
                if (other == null || other._hash != _hash || other._values.Length != _values.Length)
                    return false;
                for (int i = 0; i < _values.Length; i++)
                    if (_values[i] != other._values[i])
                        return false;
                return true;
            }

            public override bool Equals(object obj) => Equals(obj as Key);

            public override int GetHashCode() => _hash;
        }
    }
}
