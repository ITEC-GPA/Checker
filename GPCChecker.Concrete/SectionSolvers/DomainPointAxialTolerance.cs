using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using System;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    /// <summary>
    /// Tolerance on the axial force of a point of the failure domain searched at an assigned axial force (constant N, constant N and Mx,
    /// constant N and My): the point is the resistance at that axial force only if |NRd - N| is within the tolerance.
    /// <see cref="SectionSolver.CalculateDomainPoint(GPC.Model.Results.ResultBeamForces[])"/> does not apply the check: the caller applies it.
    /// Forces in N, compression negative.<br/>
    /// The tolerance depends on the compressive stress-strain diagram of the concrete and is never smaller than the tolerance with which the
    /// iterative strategy converges for the section (<see cref="ConvergenceTolerance(SectionSolver)"/>, 0.25e-4 b h fck):
    /// <list type="bullet">
    /// <item>parabola-rectangle, bilinear, non linear and generic diagrams: max(<see cref="MinimumTolerance"/>; <see cref="RelativeTolerance"/> |N|;
    /// <see cref="ConvergenceTolerance(SectionSolver)"/>), that is max(1 kN; 1e-6 |N|; 0.25e-4 b h fck). The rule used by ANTHEA up to
    /// GPCChecker.Concrete 0.0.17.0 was max(1 kN; 1e-6 |N|): the same up to b h fck = 4e7 N (for instance a circle of D 1069 mm in C35/45), while
    /// for larger sections it could reject points converged within the tolerance of the search (in ANTHEA every pile of D 1600 mm or more in
    /// C35/45 at N = 0: |NRd - N| from 1.01 to 2.33 kN);</item>
    /// <item>rectangular stress block: also at least <see cref="StressBlockFraction"/> times the centred compression resistance
    /// (<see cref="CentredCompressionResistance(SectionSolver)"/>), that is 1/1000 of NRd,c.</item>
    /// </list>
    /// The rule changes only which points are accepted, never their forces: it is never narrower than the rule of 0.0.17.0, so every point
    /// accepted by that rule is still accepted, with the same values. Points returned by the fallbacks of the search on a jump of the forces
    /// (closest point or bisection, up to 10 times the distance tolerance) can still be rejected.<br/>
    /// With the stress block the result can be less precise. The stress jumps from 0 to η fcd at the strain (1 - λ) εcu; the concrete is
    /// integrated on the fixed Gauss points of the mesh, which is not cut along the jump, so the resultant changes by steps when the strain plane
    /// moves, and the iterative strategy can stop at a point whose axial force differs from the assigned one by more than its own tolerance. The
    /// resistant moment can then differ from the one at the exact axial force: in the validation sample (13 sections, 4 diagrams, 8840 points)
    /// by less than 0.5 % in 95 % of the points accepted with the stress block, up to about 1-2 % near the ends of the domain, with either sign.
    /// The path of the search is chaotic with the stress block: the same section can give a different point on another runtime
    /// </summary>
    public static class DomainPointAxialTolerance
    {
        /// <summary>The smallest tolerance: 1000 N</summary>
        public const double MinimumTolerance = 1000.0;

        /// <summary>The part of the tolerance proportional to |N|: 1e-6</summary>
        public const double RelativeTolerance = 1e-6;

        /// <summary>With the stress block, the fraction of the centred compression resistance NRd,c: 1e-3</summary>
        public const double StressBlockFraction = 1e-3;

        /// <summary>
        /// True if the points at an assigned axial force are less precise with the diagram (the rectangular stress block), so that the wider
        /// tolerance of <see cref="Calculate(SectionSolver, double)"/> applies
        /// </summary>
        /// <param name="diagram">The compressive stress-strain diagram of the concrete</param>
        /// <returns>True for <see cref="ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock"/></returns>
        public static bool HasReducedPrecision(ConcreteMaterial.CompressionStressStrainDiagrams diagram) =>
            diagram == ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock;

        /// <summary>
        /// True if the concrete of the solver has a diagram with reduced precision (see
        /// <see cref="HasReducedPrecision(ConcreteMaterial.CompressionStressStrainDiagrams)"/>); false if the section has no concrete material
        /// </summary>
        /// <param name="solver">The section solver</param>
        /// <returns>True for the stress block</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="solver"/> is null</exception>
        public static bool HasReducedPrecision(SectionSolver solver)
        {
            if (solver == null)
                throw new ArgumentNullException(nameof(solver));
            ConcreteMaterial material = solver.ConcreteMaterial;
            return material != null && HasReducedPrecision(material.CompressionStressStrainDiagram);
        }

        /// <summary>
        /// The centred compression resistance NRd,c (N, positive): the modulus of the axial force of the failure domain at the uniform strain of
        /// pure compression (εc2 of the parabola-rectangle diagram, also for the stress block, as the point of pure compression of the plastic
        /// domain), integrated by the solver with bars, steel sections and tendons and with the reduction factor and the compression limit of the
        /// standard, if any. For plain reinforced concrete it is (Ac - As) η fcd + As σs(εc2), with η = 1 for the diagrams other than the stress
        /// block. The same value for plastic and elastic domains
        /// </summary>
        /// <param name="solver">The section solver</param>
        /// <returns>NRd,c; 0 if the integration fails</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="solver"/> is null</exception>
        public static double CentredCompressionResistance(SectionSolver solver)
        {
            if (solver == null)
                throw new ArgumentNullException(nameof(solver));
            return Math.Abs(solver.CalculatePureCompressionAxialForce());
        }

        /// <summary>
        /// The tolerance on N with which the iterative strategy of the solver converges for the section (N, positive): the distance tolerance of
        /// the adimensional forces of the search (0.25e-4) times the scale of N of the solver, b h fck, with b and h the sizes of the bounding box
        /// of the concrete and fck its characteristic strength (for composite sections plus A 15 fck of each steel section). It grows with the
        /// section and does not depend on the diagram: 112.5 N for 300 × 500 in C30/37, 640 N for a circle of D 800 in C40/50, 3500 N for a
        /// circle of D 2000 in C35/45. A converged point can differ from the assigned axial force by up to about this value
        /// </summary>
        /// <param name="solver">The section solver</param>
        /// <returns>The tolerance; 0 if the scale of the section is not finite or not positive</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="solver"/> is null</exception>
        public static double ConvergenceTolerance(SectionSolver solver)
        {
            if (solver == null)
                throw new ArgumentNullException(nameof(solver));
            return solver.CalculateAxialConvergenceTolerance();
        }

        /// <summary>
        /// The tolerance on the axial force of a point searched at the assigned axial force: max(1000 N; 1e-6 |N|; tolerance of convergence of the
        /// search, 0.25e-4 b h fck), and with the stress block at least 1e-3 NRd,c. For the other diagrams the section is not integrated
        /// </summary>
        /// <param name="solver">The section solver of the point</param>
        /// <param name="axialForce">The assigned axial force (N, compression negative)</param>
        /// <returns>The tolerance (N, positive)</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="solver"/> is null</exception>
        /// <exception cref="ArgumentException">If <paramref name="axialForce"/> is not finite</exception>
        public static double Calculate(SectionSolver solver, double axialForce)
        {
            if (solver == null)
                throw new ArgumentNullException(nameof(solver));
            if (double.IsNaN(axialForce) || double.IsInfinity(axialForce))
                throw new ArgumentException("The axial force must be finite", nameof(axialForce));
            double tolerance = Math.Max(Math.Max(MinimumTolerance, RelativeTolerance * Math.Abs(axialForce)), ConvergenceTolerance(solver));
            if (!HasReducedPrecision(solver))
                return tolerance;
            return Math.Max(tolerance, StressBlockFraction * CentredCompressionResistance(solver));
        }

        /// <summary>
        /// True if the point has the assigned axial force within the tolerance: |NRd - N| ≤ <see cref="Calculate(SectionSolver, double)"/>.
        /// A point with a non finite axial force does not have it
        /// </summary>
        /// <param name="solver">The section solver of the point</param>
        /// <param name="point">The point of the failure domain, with the forces in the force reference axes (null: false)</param>
        /// <param name="axialForce">The assigned axial force (N, compression negative)</param>
        /// <returns>True if the point is the resistance at the assigned axial force as far as N is concerned</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="solver"/> is null</exception>
        /// <exception cref="ArgumentException">If <paramref name="axialForce"/> is not finite</exception>
        public static bool HasAxialForce(SectionSolver solver, FailureDomain.FailureDomainPoint point, double axialForce)
        {
            double tolerance = Calculate(solver, axialForce);
            return point != null && Math.Abs(point.NRd - axialForce) <= tolerance;
        }
    }
}
