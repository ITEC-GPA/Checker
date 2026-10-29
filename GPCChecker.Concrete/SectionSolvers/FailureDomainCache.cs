using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// What a computation of the failure domain of a solver depends on besides the section, the standards and the state built by the solver
    /// constructor (see <see cref="SectionSolver.GetDomainCore"/>): the 3D domain of <see cref="Teta"/> meridians, or the two meridians at
    /// <see cref="Angle"/> and <see cref="Angle"/> + π of the 2D domains and of the range of N
    /// </summary>
    internal sealed class FailureDomainRequest
    {
        internal readonly bool TwoMeridians;
        internal readonly int Teta;
        internal readonly double Angle;
        internal readonly SectionSolver.FailureDomainTypes Type;
        internal readonly bool ConsiderTensileConcrete;
        /// <summary>The state of a derived solver that the domain reads (<see cref="SectionSolver.DomainState"/>: the spiral of ACI 318)</summary>
        internal readonly int SolverState;
        /// <summary>The force reference axes of the points: the computation reads only the X and Y of the origin (GetExternalForces)</summary>
        internal readonly CoordinateSystem Axes;

        private FailureDomainRequest(bool twoMeridians, int teta, double angle, SectionSolver.FailureDomainTypes type, SectionSolver solver, CoordinateSystem axes)
        {
            TwoMeridians = twoMeridians; Teta = teta; Angle = angle; Type = type; Axes = axes;
            ConsiderTensileConcrete = solver.ConsiderTensileConcrete;
            SolverState = solver.DomainState;
        }

        /// <summary>The 3D domain of the current state of the solver: its meridians, its tensile concrete and its force reference axes</summary>
        internal static FailureDomainRequest Domain3d(SectionSolver solver, SectionSolver.FailureDomainTypes type) =>
            new FailureDomainRequest(false, solver.TetaDiscretization, 0, type, solver, solver.SectionOption.ForceReferenceCoordinateSystem);

        /// <summary>
        /// The 3D domain as the solver would compute it now, kept for a later computation: the intersection strategy computed its domain in
        /// its constructor, so the meridians, the tensile concrete, the type and the axes are those of the moment the strategy is created
        /// (a strategy of the solver constructor uses the 64 meridians of the solver default, before the checker assigns the options, and the
        /// state of a derived solver before its constructor body: no spiral for ACI 318)
        /// </summary>
        internal static FailureDomainRequest Snapshot3d(SectionSolver solver, SectionSolver.FailureDomainTypes type)
        {
            // the computation reads only the origin: a copy of it, in axes that cannot fail the orthogonality check of a copy of the axes
            var origin = solver.SectionOption.ForceReferenceCoordinateSystem?.Origin;
            var axes = origin == null ? null : new CoordinateSystem(new Point3d(origin.X, origin.Y, origin.Z), Vector3d.XAxis, Vector3d.YAxis);
            return new FailureDomainRequest(false, solver.TetaDiscretization, 0, type, solver, axes);
        }

        /// <summary>The two meridians at the angle (2D domains, range of N) for the current state of the solver</summary>
        internal static FailureDomainRequest Meridians(SectionSolver solver, SectionSolver.FailureDomainTypes type, double angle) =>
            new FailureDomainRequest(true, 2, angle, type, solver, solver.SectionOption.ForceReferenceCoordinateSystem);
    }

    /// <summary>
    /// A computed failure domain shared by the solvers whose computation would give the same points (same key of
    /// <see cref="SectionSolver.GetDomainCore"/>). Its points are never handed out: the results get copies (<see cref="FailureDomainCopy"/>),
    /// so that the setters of the domain and of the points (working ratio, interpolation, subdivisions) act on the copy of the caller only.
    /// The mesh of the intersection strategy is built from it once, on the first use
    /// </summary>
    internal sealed class FailureDomainCore
    {
        internal static readonly FailureDomainCore None = new FailureDomainCore(null);

        private readonly object _sync = new object();
        private Mesh _mesh;
        private Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> _vertexToDomainPoint;

        private FailureDomainCopy.Plan _copyPlan;

        internal FailureDomainCore(FailureDomain domain) { Domain = domain; }

        /// <summary>The domain as computed (null: no domain, or a failed computation). Read only: the results get <see cref="Copy"/></summary>
        internal FailureDomain Domain { get; }

        /// <summary>A copy of the domain (null if there is none): new points with the same numbers, see <see cref="FailureDomainCopy"/></summary>
        internal FailureDomain Copy()
        {
            if (Domain == null)
                return null;
            var plan = Volatile.Read(ref _copyPlan);
            if (plan == null)
            {
                plan = new FailureDomainCopy.Plan(Domain);
                Interlocked.CompareExchange(ref _copyPlan, plan, null);
            }
            return plan.Copy(Domain);
        }

        /// <summary>
        /// The mesh of the intersection strategy and the domain point of each vertex, as <see cref="FailureDomain.GetMesh(FailureDomain, out Dictionary{MeshVertex, FailureDomain.FailureDomainPoint})"/>
        /// of the domain. Built once; its bounding volume hierarchy is built here, so that the picks of parallel searches only read the mesh. A
        /// failure is not kept: the next call tries again, as the strategy did before
        /// </summary>
        internal Mesh GetIntersectionMesh(out Dictionary<MeshVertex, FailureDomain.FailureDomainPoint> vertexToDomainPoint)
        {
            lock (_sync)
            {
                if (_mesh == null)
                {
                    FailureDomainCache.CountMesh();
                    var mesh = Domain.GetMesh(Domain, out var map);
                    mesh.UpdateFaceBVH();
                    _vertexToDomainPoint = map;
                    _mesh = mesh;
                }
                vertexToDomainPoint = _vertexToDomainPoint;
                return _mesh;
            }
        }

        internal bool HasIntersectionMesh { get { lock (_sync) return _mesh != null; } }
    }

    /// <summary>
    /// Cache of the failure domains shared by the solvers of the process, for the same section content, standards and options of the domain
    /// (key of <see cref="SectionSolver.GetDomainCore"/>): a new checker on a section already computed, as the detached copies of ReBear or
    /// the checkers of a new verification of the same model, finds its domain here. Bounded (least recently used out) and thread-safe
    /// </summary>
    internal static class FailureDomainCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, FailureDomainCore>>> Entries =
            new Dictionary<string, LinkedListNode<KeyValuePair<string, FailureDomainCore>>>(StringComparer.Ordinal);
        private static readonly LinkedList<KeyValuePair<string, FailureDomainCore>> Order = new LinkedList<KeyValuePair<string, FailureDomainCore>>();
        private static int _capacity = 32;
        private static long _hits, _misses, _computations, _meshes;

        /// <summary>Largest number of domains kept (default 32: a 3D domain of 64 meridians is about half a megabyte, its mesh a few)</summary>
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
        /// <summary>Domains computed by the solvers of the process (cached or not)</summary>
        internal static long Computations => Interlocked.Read(ref _computations);
        /// <summary>Meshes of the intersection strategy built in the process</summary>
        internal static long MeshBuilds => Interlocked.Read(ref _meshes);
        internal static int Count { get { lock (Sync) return Entries.Count; } }

        internal static void CountComputation() => Interlocked.Increment(ref _computations);
        internal static void CountMesh() => Interlocked.Increment(ref _meshes);

        internal static bool TryGet(string key, out FailureDomainCore core)
        {
            lock (Sync)
            {
                if (Entries.TryGetValue(key, out var node))
                {
                    Order.Remove(node);
                    Order.AddFirst(node);
                    core = node.Value.Value;
                    _hits++;
                    return true;
                }
                _misses++;
                core = null;
                return false;
            }
        }

        /// <summary>Adds the domain of the key; the one already there (computed by another solver at the same time, the same points) stays</summary>
        internal static FailureDomainCore Add(string key, FailureDomainCore core)
        {
            lock (Sync)
            {
                if (Entries.TryGetValue(key, out var node))
                    return node.Value.Value;
                if (_capacity == 0)
                    return core;
                node = Order.AddFirst(new KeyValuePair<string, FailureDomainCore>(key, core));
                Entries[key] = node;
                Trim();
                return core;
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
                Interlocked.Exchange(ref _computations, 0);
                Interlocked.Exchange(ref _meshes, 0);
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
    }

    /// <summary>
    /// Copies of the points of a cached domain with the same values (ForceTuple, field, immersion, strain plane and reference point copied
    /// field by field, no arithmetic): what a new computation of the domain returned. Objects shared by several points of the source are
    /// shared by the same points of the copy
    /// </summary>
    internal sealed class FailureDomainCopy
    {
        private readonly Dictionary<FailureDomain.FailureDomainPoint, FailureDomain.FailureDomainPoint> _points =
            new Dictionary<FailureDomain.FailureDomainPoint, FailureDomain.FailureDomainPoint>(ReferenceComparer<FailureDomain.FailureDomainPoint>.Instance);
        private readonly Dictionary<StrainPlane, StrainPlane> _planes = new Dictionary<StrainPlane, StrainPlane>(ReferenceComparer<StrainPlane>.Instance);
        private readonly Dictionary<Point2d, Point2d> _references = new Dictionary<Point2d, Point2d>(ReferenceComparer<Point2d>.Instance);

        /// <summary>A copy of the domain (null: null), with its points, type, interpolation and subdivisions</summary>
        internal static FailureDomain Of(FailureDomain domain)
        {
            if (domain == null)
                return null;
            var copy = new FailureDomainCopy();
            var source = domain.DomainPoints;
            var points = new FailureDomain.FailureDomainPoint[source.Length][];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    continue;
                points[i] = new FailureDomain.FailureDomainPoint[source[i].Length];
                for (int j = 0; j < source[i].Length; j++)
                    points[i][j] = copy.Point(source[i][j]);
            }
            return new FailureDomain(points, domain.FailureDomainAnalysisTypes)
            {
                ForceLinearInterpolation = domain.ForceLinearInterpolation,
                AxialForceSubdivision = domain.AxialForceSubdivision
            };
        }

        /// <summary>A copy of a single point (null: null)</summary>
        internal static FailureDomain.FailureDomainPoint OfPoint(FailureDomain.FailureDomainPoint point) => new FailureDomainCopy().Point(point);

        /// <summary>
        /// The objects of a domain that never changes, numbered once: its copies are then made without looking up the shared objects (the
        /// same copy as <see cref="Of"/>)
        /// </summary>
        internal sealed class Plan
        {
            private readonly FailureDomain.FailureDomainPoint[] _points;
            private readonly int[] _planeOfPoint;
            private readonly StrainPlane[] _planes;
            private readonly int[] _referenceOfPlane;
            private readonly Point2d[] _references;
            /// <summary>For each meridian the index of each point in <see cref="_points"/> (-1 null; a null meridian is null)</summary>
            private readonly int[][] _layout;

            internal Plan(FailureDomain domain)
            {
                var points = new Dictionary<FailureDomain.FailureDomainPoint, int>(ReferenceComparer<FailureDomain.FailureDomainPoint>.Instance);
                var planes = new Dictionary<StrainPlane, int>(ReferenceComparer<StrainPlane>.Instance);
                var references = new Dictionary<Point2d, int>(ReferenceComparer<Point2d>.Instance);
                var pointList = new List<FailureDomain.FailureDomainPoint>();
                var planeOfPoint = new List<int>();
                var planeList = new List<StrainPlane>();
                var referenceOfPlane = new List<int>();
                var referenceList = new List<Point2d>();
                var source = domain.DomainPoints;
                _layout = new int[source.Length][];
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null)
                        continue;
                    _layout[i] = new int[source[i].Length];
                    for (int j = 0; j < source[i].Length; j++)
                    {
                        var point = source[i][j];
                        if (point == null) { _layout[i][j] = -1; continue; }
                        if (!points.TryGetValue(point, out int index))
                        {
                            index = pointList.Count;
                            points.Add(point, index);
                            pointList.Add(point);
                            int plane = -1;
                            if (point.StrainPlane != null && !planes.TryGetValue(point.StrainPlane, out plane))
                            {
                                plane = planeList.Count;
                                planes.Add(point.StrainPlane, plane);
                                planeList.Add(point.StrainPlane);
                                int reference = -1;
                                var referencePoint = point.StrainPlane.ReferencePoint;
                                if (referencePoint != null && !references.TryGetValue(referencePoint, out reference))
                                {
                                    reference = referenceList.Count;
                                    references.Add(referencePoint, reference);
                                    referenceList.Add(referencePoint);
                                }
                                referenceOfPlane.Add(reference);
                            }
                            planeOfPoint.Add(plane);
                        }
                        _layout[i][j] = index;
                    }
                }
                _points = pointList.ToArray();
                _planeOfPoint = planeOfPoint.ToArray();
                _planes = planeList.ToArray();
                _referenceOfPlane = referenceOfPlane.ToArray();
                _references = referenceList.ToArray();
            }

            /// <summary>The copy of the domain of the plan (its type, interpolation and subdivisions are read from it)</summary>
            internal FailureDomain Copy(FailureDomain domain)
            {
                var references = new Point2d[_references.Length];
                for (int k = 0; k < references.Length; k++)
                    references[k] = new Point2d(_references[k].X, _references[k].Y);
                var planes = new StrainPlane[_planes.Length];
                for (int k = 0; k < planes.Length; k++)
                {
                    var plane = _planes[k];
                    planes[k] = new StrainPlane(plane.ChiX, plane.ChiY, _referenceOfPlane[k] < 0 ? null : references[_referenceOfPlane[k]],
                        plane.StrainReferencePoint, plane.Id, plane.Name);
                }
                var copies = new FailureDomain.FailureDomainPoint[_points.Length];
                for (int k = 0; k < copies.Length; k++)
                {
                    var point = _points[k];
                    copies[k] = new FailureDomain.FailureDomainPoint(point.ForceTuple, point.FailureIndex, _planeOfPoint[k] < 0 ? null : planes[_planeOfPoint[k]],
                        point.Immersione)
                    {
                        WorkingRatio = point.WorkingRatio,
                        InterpolatedStrainPlane = point.InterpolatedStrainPlane
                    };
                }
                var meridians = new FailureDomain.FailureDomainPoint[_layout.Length][];
                for (int i = 0; i < _layout.Length; i++)
                {
                    if (_layout[i] == null)
                        continue;
                    meridians[i] = new FailureDomain.FailureDomainPoint[_layout[i].Length];
                    for (int j = 0; j < _layout[i].Length; j++)
                        meridians[i][j] = _layout[i][j] < 0 ? null : copies[_layout[i][j]];
                }
                return new FailureDomain(meridians, domain.FailureDomainAnalysisTypes)
                {
                    ForceLinearInterpolation = domain.ForceLinearInterpolation,
                    AxialForceSubdivision = domain.AxialForceSubdivision
                };
            }
        }

        private FailureDomain.FailureDomainPoint Point(FailureDomain.FailureDomainPoint point)
        {
            if (point == null)
                return null;
            if (_points.TryGetValue(point, out var copy))
                return copy;
            copy = new FailureDomain.FailureDomainPoint(point.ForceTuple, point.FailureIndex, Plane(point.StrainPlane), point.Immersione)
            {
                WorkingRatio = point.WorkingRatio,
                InterpolatedStrainPlane = point.InterpolatedStrainPlane
            };
            _points.Add(point, copy);
            return copy;
        }

        private StrainPlane Plane(StrainPlane plane)
        {
            if (plane == null)
                return null;
            if (_planes.TryGetValue(plane, out var copy))
                return copy;
            copy = new StrainPlane(plane.ChiX, plane.ChiY, Reference(plane.ReferencePoint), plane.StrainReferencePoint, plane.Id, plane.Name);
            _planes.Add(plane, copy);
            return copy;
        }

        private Point2d Reference(Point2d point)
        {
            if (point == null)
                return null;
            if (_references.TryGetValue(point, out var copy))
                return copy;
            copy = new Point2d(point.X, point.Y);
            _references.Add(point, copy);
            return copy;
        }
    }

    /// <summary>Equality by reference (netstandard2.0 has no ReferenceEqualityComparer)</summary>
    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
