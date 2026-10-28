using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Immutable point in the connection's local XY plane; mm.</summary>
    public readonly struct ConnectionPoint2D
    {
        public double X { get; }
        public double Y { get; }
        public ConnectionPoint2D(double x, double y)
        {
            ConnectionNumbers.Finite(x, nameof(x));
            ConnectionNumbers.Finite(y, nameof(y));
            X = x; Y = y;
        }
    }

    /// <summary>One concomitant factored action state in connection-local axes.
    /// The joint lies in XY, +Z is its normal; all moments follow the right-hand rule.
    /// Forces N, moments Nmm, application point mm. These axes are NOT SteelActions
    /// member axes: conversion is an explicit responsibility of the calling joint.
    /// Transport to another point C uses M(C)=M(A)+(A-C) cross F.</summary>
    public sealed class PlanarConnectionActions
    {
        public double Fx { get; }
        public double Fy { get; }
        public double Fz { get; }
        public double Mx { get; }
        public double My { get; }
        public double Mz { get; }
        public double ApplicationX { get; }
        public double ApplicationY { get; }
        public double ApplicationZ { get; }
        public PlanarConnectionActions(double fx, double fy, double fz,
            double mx, double my, double mz, double applicationX = 0,
            double applicationY = 0, double applicationZ = 0)
        {
            ConnectionNumbers.Finite(fx, nameof(fx)); ConnectionNumbers.Finite(fy, nameof(fy));
            ConnectionNumbers.Finite(fz, nameof(fz)); ConnectionNumbers.Finite(mx, nameof(mx));
            ConnectionNumbers.Finite(my, nameof(my)); ConnectionNumbers.Finite(mz, nameof(mz));
            ConnectionNumbers.Finite(applicationX, nameof(applicationX));
            ConnectionNumbers.Finite(applicationY, nameof(applicationY));
            ConnectionNumbers.Finite(applicationZ, nameof(applicationZ));
            Fx = fx; Fy = fy; Fz = fz; Mx = mx; My = my; Mz = mz;
            ApplicationX = applicationX; ApplicationY = applicationY; ApplicationZ = applicationZ;
        }
    }

    /// <summary>Common immutable assessment for one explicitly scoped component or joint.
    /// A component passing does not qualify its parent joint. Input must itself be immutable.
    /// Blocked calculations expose diagnostics and trace, never favourable partial checks;
    /// an evaluated failed check is a known failure and stays visible (Failed, incomplete).</summary>
    public sealed class ConnectionVerification<TInput> where TInput : class
    {
        public TInput Input { get; }
        public ConnectionDesignCode Code { get; }
        public string MethodId { get; }
        public string Scope { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> Assumptions { get; }
        public ConnectionCheck GoverningCheck => IsComplete ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;
        /// <summary>The trace entries the producer marks for reports, in trace order. Reports
        /// select these typed entries instead of matching trace labels.</summary>
        internal IReadOnlyList<SectionResistanceValue> ReportedValues { get; }
        /// <summary>The role of this component in its calling joint, shared by its own checks.</summary>
        internal ConnectionCheckRole Role { get; }

        internal ConnectionVerification(TInput input, ConnectionDesignCode code, string methodId, string scope,
            IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<string> diagnostics, IEnumerable<string> assumptions = null, IEnumerable<SectionResistanceValue> reported = null,
            ConnectionCheckRole role = ConnectionCheckRole.Resistance)
        {
            Role = role;
            ReportedValues = Array.AsReadOnly(reported == null ? Array.Empty<SectionResistanceValue>() : reported.ToArray());
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            MethodId = methodId; Scope = scope;
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            var evaluated = ConnectionOutcome.Retained(checks, Diagnostics.Count);
            Checks = Array.AsReadOnly(evaluated);
            Trace = Array.AsReadOnly(trace.ToArray());
            Assumptions = Array.AsReadOnly((assumptions ?? Array.Empty<string>()).ToArray());
            IsComplete = ConnectionOutcome.IsComplete(evaluated, Diagnostics.Count);
            Status = ConnectionOutcome.Status(evaluated, Diagnostics.Count);
        }
    }

    /// <summary>One rule for the leaf assessments: a failure wins over incompleteness.
    /// With diagnostics only the FAILED evaluated checks are retained, never favourable
    /// partial ones; completeness is reported separately from the status.</summary>
    internal static class ConnectionOutcome
    {
        internal static ConnectionCheck[] Retained(IEnumerable<ConnectionCheck> checks, int diagnostics)
            => diagnostics == 0 ? checks.ToArray() : checks.Where(c => !c.Passed).ToArray();
        internal static bool IsComplete(IReadOnlyCollection<ConnectionCheck> retained, int diagnostics)
            => diagnostics == 0 && retained.Count != 0;
        internal static ConnectionStatus Status(IReadOnlyCollection<ConnectionCheck> retained, int diagnostics)
            => retained.Any(c => !c.Passed) ? ConnectionStatus.Failed
                : IsComplete(retained, diagnostics) ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        /// <summary>Checks that compare an actual action effect with a capacity. Applicability and
        /// detailing rules still decide the status, but never the load utilization.</summary>
        internal static IEnumerable<ConnectionCheck> LoadChecks(IEnumerable<ConnectionCheck> checks)
            => checks.Where(c => IsActionUtilization(c.Role));
        internal static bool IsActionUtilization(ConnectionCheckRole role)
            => role == ConnectionCheckRole.Resistance || role == ConnectionCheckRole.Serviceability;
        /// <summary>A failed applicability check means "outside this sufficient method": it is a
        /// missing requirement of the calling joint, never a known load failure.</summary>
        internal static bool IsDomainPrerequisite(ConnectionCheckRole role) => role == ConnectionCheckRole.Applicability;
    }

    internal static class ConnectionNumbers
    {
        internal static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "A finite connection value is required.");
        }
    }
}
