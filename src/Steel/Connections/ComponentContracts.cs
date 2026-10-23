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
    /// Blocked calculations expose diagnostics and trace, never favourable partial checks.</summary>
    public sealed class ConnectionVerification<TInput> where TInput : class
    {
        public TInput Input { get; }
        public ConnectionDesignCode Code { get; }
        public string MethodId { get; }
        public string Scope { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete => Status != ConnectionStatus.NotSupported;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> Assumptions { get; }
        public ConnectionCheck GoverningCheck => IsComplete ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;

        internal ConnectionVerification(TInput input, ConnectionDesignCode code, string methodId, string scope,
            IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<string> diagnostics, IEnumerable<string> assumptions = null)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            MethodId = methodId; Scope = scope;
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            var evaluated = Diagnostics.Count == 0 ? checks.ToArray() : Array.Empty<ConnectionCheck>();
            Checks = Array.AsReadOnly(evaluated);
            Trace = Array.AsReadOnly(trace.ToArray());
            Assumptions = Array.AsReadOnly((assumptions ?? Array.Empty<string>()).ToArray());
            Status = Diagnostics.Count != 0 || evaluated.Length == 0 ? ConnectionStatus.NotSupported
                : evaluated.All(c => c.Passed) ? ConnectionStatus.Passed : ConnectionStatus.Failed;
        }
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
