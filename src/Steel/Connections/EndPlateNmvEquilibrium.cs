using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One two-bolt tension row of the elastic component model. Z is positive
    /// upwards from the beam centroid (mm); stiffness is the whole row stiffness (N/mm).</summary>
    public sealed class EndPlateNmvSpringRow
    {
        public string Id { get; }
        public double Z { get; }
        public double Stiffness { get; }
        public EndPlateNmvSpringRow(string id, double z, double stiffness)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A physical row id is required.", nameof(id));
            ConnectionNumbers.Finite(z, nameof(z));
            ConnectionValidation.Positive(stiffness, nameof(stiffness));
            Id = id; Z = z; Stiffness = stiffness;
        }
    }

    /// <summary>Initially closed compression-only band. The total transverse stiffness
    /// is spread uniformly over its height; contact pressure is integrated exactly over
    /// the closed part. This is a declared component idealisation, not a solid contact FE.</summary>
    public sealed class EndPlateNmvContactBand
    {
        public string Id { get; }
        public double LowerZ { get; }
        public double UpperZ { get; }
        public double Stiffness { get; }
        public EndPlateNmvContactBand(string id, double lowerZ, double upperZ, double stiffness)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A physical contact id is required.", nameof(id));
            ConnectionNumbers.Finite(lowerZ, nameof(lowerZ)); ConnectionNumbers.Finite(upperZ, nameof(upperZ));
            if (!(upperZ > lowerZ) || double.IsInfinity(upperZ - lowerZ)) throw new ArgumentOutOfRangeException(nameof(upperZ));
            ConnectionValidation.Positive(stiffness, nameof(stiffness));
            Id = id; LowerZ = lowerZ; UpperZ = upperZ; Stiffness = stiffness;
        }
    }

    /// <summary>[dN,dM] = K [du,dtheta]. Units N/mm, N and Nmm/rad. A local tangent,
    /// not M/theta and not a plastic rotation capacity or an entire-frame spring.</summary>
    public sealed class EndPlateNmvTangent
    {
        public double Axial { get; }
        public double Coupling { get; }
        public double Rotation { get; }
        public bool IsPositiveDefinite => Axial > 0 && Rotation > 0
            && 1 - Math.Pow(Coupling / Math.Sqrt(Axial) / Math.Sqrt(Rotation), 2) > 1e-12;
        public double? RotationAtConstantAxialForce => IsPositiveDefinite ? Rotation - Coupling / Axial * Coupling : (double?)null;
        public EndPlateNmvTangent(double axial, double coupling, double rotation)
        {
            ConnectionValidation.Nonnegative(axial, nameof(axial)); ConnectionValidation.Nonnegative(rotation, nameof(rotation));
            ConnectionNumbers.Finite(coupling, nameof(coupling));
            Axial = axial; Coupling = coupling; Rotation = rotation;
        }
    }

    public sealed class EndPlateNmvRowState
    {
        public EndPlateNmvSpringRow Row { get; }
        public double Opening { get; }
        public double Tension { get; }
        internal EndPlateNmvRowState(EndPlateNmvSpringRow row, double opening)
        { Row = row; Opening = opening; Tension = row.Stiffness * Math.Max(0, opening); }
    }

    public sealed class EndPlateNmvContactState
    {
        public EndPlateNmvContactBand Band { get; }
        public double Compression { get; }
        public double SignedMoment { get; }
        public double ClosedLength { get; }
        public double? ClosedLowerZ { get; }
        public double? ClosedUpperZ { get; }
        public double? ResultantZ => Compression > 0 ? -SignedMoment / Compression : (double?)null;
        public double MaximumCompressionPerLength { get; }
        internal EndPlateNmvContactState(EndPlateNmvContactBand band, double force, double moment,
            double a, double b, double maximum)
        {
            Band = band; Compression = Math.Max(0, -force); SignedMoment = moment;
            ClosedLength = Math.Max(0, b - a); MaximumCompressionPerLength = maximum;
            if (ClosedLength > 0) { ClosedLowerZ = a; ClosedUpperZ = b; }
        }
    }

    public sealed class EndPlateNmvEquilibriumResult
    {
        public double AxialForce { get; }
        public double Moment { get; }
        public double AxialOpening { get; }
        public double Rotation { get; }
        public double AxialResidual { get; }
        public double MomentResidual { get; }
        public bool IsConverged { get; }
        public bool IsAtContactSwitch { get; }
        public bool CanQualifyWholeJoint => false;
        public double ElasticEnergy { get; }
        public EndPlateNmvTangent Tangent { get; }
        public IReadOnlyList<EndPlateNmvRowState> Rows { get; }
        public IReadOnlyList<EndPlateNmvContactState> Contacts { get; }
        internal EndPlateNmvEquilibriumResult(double n, double m, double u, double theta,
            IEnumerable<EndPlateNmvSpringRow> rows, IEnumerable<EndPlateNmvContactBand> bands, double referenceLength)
        {
            AxialForce = n; Moment = m; AxialOpening = u; Rotation = theta;
            var rr = rows.Select(r => new EndPlateNmvRowState(r, u + theta * r.Z)).ToArray();
            var cc = new List<EndPlateNmvContactState>();
            double nn = rr.Sum(r => r.Tension), mm = rr.Sum(r => r.Tension * r.Row.Z);
            double ka = 0, kc = 0, kr = 0, energy = 0;
            double switchTolerance = 1e-10 * Math.Max(1, Math.Abs(u) + Math.Abs(theta) * referenceLength);
            bool atSwitch = false;
            foreach (var r in rr)
            {
                atSwitch |= Math.Abs(r.Opening) <= switchTolerance;
                if (r.Opening <= 0) continue;
                ka += r.Row.Stiffness; kc += r.Row.Stiffness * r.Row.Z; kr += r.Row.Stiffness * r.Row.Z * r.Row.Z;
                energy += .5 * r.Tension * r.Opening;
            }
            foreach (var band in bands)
            {
                var c = EndPlateNmvEquilibrium.Integrate(band, u, theta);
                cc.Add(new EndPlateNmvContactState(band, c.Force, c.Moment, c.Lower, c.Upper, c.Maximum));
                nn += c.Force; mm += c.Moment; ka += c.Ka; kc += c.Kc; kr += c.Kr; energy += c.Energy;
                atSwitch |= Math.Abs(u + theta * band.LowerZ) <= switchTolerance || Math.Abs(u + theta * band.UpperZ) <= switchTolerance;
            }
            AxialResidual = nn - n; MomentResidual = mm - m; ElasticEnergy = energy;
            double scale = Math.Max(1, Math.Max(Math.Abs(n), Math.Abs(m) / referenceLength));
            IsConverged = Math.Abs(AxialResidual) <= 2e-9 * scale && Math.Abs(MomentResidual) <= 2e-9 * scale * referenceLength;
            IsAtContactSwitch = atSwitch;
            Tangent = new EndPlateNmvTangent(ka, kc, kr);
            Rows = Array.AsReadOnly(rr); Contacts = Array.AsReadOnly(cc.ToArray());
        }
    }

    /// <summary>Two-DOF analytic unilateral elastic equilibrium. Nested monotone solves
    /// avoid an arbitrary active-set choice at zero opening. No material plasticity,
    /// generic plate FE, resistance, normative secant law or panel shear stiffness.</summary>
    internal static class EndPlateNmvEquilibrium
    {
        public static EndPlateNmvEquilibriumResult Solve(IEnumerable<EndPlateNmvSpringRow> rows,
            IEnumerable<EndPlateNmvContactBand> contacts, double axialForce, double moment)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            ConnectionNumbers.Finite(axialForce, nameof(axialForce)); ConnectionNumbers.Finite(moment, nameof(moment));
            var rr = rows.ToArray(); var cc = contacts.ToArray();
            if (rr.Length < 1 || rr.Length > 20 || rr.Any(r => r == null) || rr.Select(r => r.Id).Distinct().Count() != rr.Length)
                throw new ArgumentException("One to twenty uniquely identified tension rows required.", nameof(rows));
            if (cc.Length != 2 || cc.Any(c => c == null) || cc[0].Id == cc[1].Id)
                throw new ArgumentException("Two distinct compression bands required.", nameof(contacts));
            if (Math.Max(cc[0].LowerZ, cc[1].LowerZ) < Math.Min(cc[0].UpperZ, cc[1].UpperZ))
                throw new ArgumentException("Compression bands must not overlap.", nameof(contacts));
            if (rr.Length > 1 && rr.Max(r => r.Z) == rr.Min(r => r.Z)) throw new ArgumentException("Rows must span distinct heights.", nameof(rows));
            if (rr.Length == 1 && !(rr[0].Z > cc.Min(c => c.UpperZ) && rr[0].Z < cc.Max(c => c.LowerZ)))
                throw new ArgumentException("A single tension row must lie strictly between both contact bands.", nameof(rows));
            double length = Math.Max(1, rr.Select(r => Math.Abs(r.Z)).Concat(cc.SelectMany(c => new[] { Math.Abs(c.LowerZ), Math.Abs(c.UpperZ) })).Max());
            double kt = rr.Sum(r => r.Stiffness), kc = cc.Sum(c => c.Stiffness);
            double scale = Math.Max(1, Math.Max(Math.Abs(axialForce), Math.Abs(moment) / length));
            if (double.IsInfinity(kt + kc + length + scale)) throw new ArgumentOutOfRangeException(nameof(rows), "Numeric range exceeded.");
            double u = 0, theta = 0;
            if (axialForce != 0 || moment != 0)
            {
                double step = Math.Max(1e-12, scale / Math.Min(kt, kc) / length);
                double lower = -step, upper = step;
                for (int i = 0; i < 100 && Response(lower).Moment > moment; i++) lower *= 2;
                for (int i = 0; i < 100 && Response(upper).Moment < moment; i++) upper *= 2;
                if (!(Response(lower).Moment <= moment && Response(upper).Moment >= moment))
                    throw new InvalidOperationException("The declared components cannot bracket a stable N-M equilibrium.");
                theta = (lower + upper) / 2; double rotationStep = upper - lower;
                for (int i = 0; i < 90; i++)
                {
                    var response = Response(theta); u = response.Opening;
                    double residual = response.Moment - moment;
                    if (Math.Abs(residual) <= 1e-11 * scale * length || i == 89) break;
                    if (residual < 0) lower = theta; else upper = theta;
                    // dM/dtheta at the constant axial force: Kr - Kc^2/Ka.
                    double slope = response.Ka > 0 ? response.Kr - response.Kc * response.Kc / response.Ka : 0;
                    theta = Next(theta, residual, slope, lower, upper, ref rotationStep);
                }
            }
            return new EndPlateNmvEquilibriumResult(axialForce, moment, u, theta, rr, cc, length);

            (double Opening, double Moment, double Ka, double Kc, double Kr) Response(double rotation)
            {
                double lo = -Math.Abs(axialForce) / kc - Math.Abs(rotation) * length - 1;
                double hi = Math.Abs(axialForce) / kt + Math.Abs(rotation) * length + 1;
                double opening = (lo + hi) / 2, openingStep = hi - lo, mm = 0, ka = 0, kcu = 0, kr = 0;
                for (int j = 0; j < 90; j++)
                {
                    double nn = 0; mm = 0; ka = 0; kcu = 0; kr = 0;
                    foreach (var row in rr)
                    {
                        double e = opening + rotation * row.Z;
                        if (!(e > 0)) continue;
                        double f = row.Stiffness * e; nn += f; mm += f * row.Z;
                        ka += row.Stiffness; kcu += row.Stiffness * row.Z; kr += row.Stiffness * row.Z * row.Z;
                    }
                    foreach (var band in cc) { var v = Integrate(band, opening, rotation); nn += v.Force; mm += v.Moment; ka += v.Ka; kcu += v.Kc; kr += v.Kr; }
                    double residual = nn - axialForce;
                    if (Math.Abs(residual) <= 1e-12 * scale || j == 89) break;
                    if (residual < 0) lo = opening; else hi = opening;
                    opening = Next(opening, residual, ka, lo, hi, ref openingStep);
                }
                return (opening, mm, ka, kcu, kr);
            }

            // Safeguarded Newton on a monotone, piecewise-quadratic response with its exact
            // derivative (the moving contact edge has zero opening, so dN/du=Ka and
            // dM/dtheta|N=Kr-Kc^2/Ka). A Newton step is accepted only inside the bracket and
            // when it at least halves the previous step, otherwise bisection: same bracket,
            // tolerances and 90-step limit as the former nested bisections, about 40x fewer
            // evaluations. The active set is never guessed: it follows from each evaluation.
            double Next(double x, double residual, double slope, double lo, double hi, ref double previousStep)
            {
                double newton = slope > 0 ? x - residual / slope : double.NaN, step = Math.Abs(newton - x);
                if (newton > lo && newton < hi && step <= previousStep / 2) { previousStep = step; return newton; }
                previousStep = (hi - lo) / 2; return (lo + hi) / 2;
            }
        }

        internal struct BandIntegral
        { internal double Force, Moment, Lower, Upper, Maximum, Ka, Kc, Kr, Energy; }

        internal static BandIntegral Integrate(EndPlateNmvContactBand band, double u, double theta)
        {
            double a = band.LowerZ, b = band.UpperZ;
            if (theta > 0) b = Math.Min(b, -u / theta);
            else if (theta < 0) a = Math.Max(a, -u / theta);
            else if (u >= 0) return new BandIntegral();
            if (!(b > a)) return new BandIntegral();
            double width = b - a, mid = (a + b) / 2, k = band.Stiffness / (band.UpperZ - band.LowerZ);
            double centreOpening = u + theta * mid, force = k * width * centreOpening;
            return new BandIntegral {
                Force = force, Moment = force * mid + k * theta * width * width * width / 12,
                Lower = a, Upper = b, Maximum = Math.Max(0, -k * Math.Min(u + theta * a, u + theta * b)),
                Ka = k * width, Kc = k * width * mid, Kr = k * width * (mid * mid + width * width / 12),
                Energy = .5 * k * width * (centreOpening * centreOpening + theta * theta * width * width / 12)
            };
        }
    }
}
