using System.Globalization;
using System.Runtime.CompilerServices;

namespace GPC.Checkers.CompositeBridge
{
    internal static class BridgeNumbers
    {
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(maximum, value));
        internal static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
        internal static double Require(double value, string name, double minimum = 0, bool strict = false)
        {
            if (!IsFinite(value) || value < minimum || (strict && value == minimum))
                throw new ArgumentException($"{name}: inserire un numero finito {(strict ? "maggiore di" : "non inferiore a")} {minimum}.");
            return value;
        }
    }
    internal static class BridgeNumericFormat
    {
        internal static string Number(double value) => !BridgeNumbers.IsFinite(value) ? "—" :
            value == 0 || Math.Abs(value) >= .01 ? value.ToString("0.00", CultureInfo.CurrentCulture) : value.ToString("0.00E+0", CultureInfo.CurrentCulture);
    }
    internal sealed class BridgePhaseIdentity : IEqualityComparer<BridgePhase>
    {
        public bool Equals(BridgePhase? x, BridgePhase? y) => ReferenceEquals(x, y);
        public int GetHashCode(BridgePhase obj) => RuntimeHelpers.GetHashCode(obj);
    }
    /// <summary>The same effective state, by reference</summary>
    internal sealed class BridgeStateIdentity : IEqualityComparer<BridgeEffective>
    {
        public bool Equals(BridgeEffective? x, BridgeEffective? y) => ReferenceEquals(x, y);
        public int GetHashCode(BridgeEffective obj) => RuntimeHelpers.GetHashCode(obj);
    }
    /// <summary>The same effective state, phase and solver of a solution, by reference</summary>
    internal sealed class BridgeSolutionIdentity : IEqualityComparer<(BridgeEffective State, BridgePhase Phase, bool Native)>
    {
        public bool Equals((BridgeEffective State, BridgePhase Phase, bool Native) x, (BridgeEffective State, BridgePhase Phase, bool Native) y) =>
            ReferenceEquals(x.State, y.State) && ReferenceEquals(x.Phase, y.Phase) && x.Native == y.Native;
        public int GetHashCode((BridgeEffective State, BridgePhase Phase, bool Native) key) =>
            (RuntimeHelpers.GetHashCode(key.State) * 397 ^ RuntimeHelpers.GetHashCode(key.Phase)) * 2 + (key.Native ? 1 : 0);
    }
    /// <summary>
    /// Former construction gate of the concrete solver. Not needed: the section meshes are created once under the section lock (Model) and
    /// independent engines on the same section are thread-safe (architecture review R7; ConcurrencyTests of Concrete and CompositeBridge).
    /// Kept so that the callers that still lock on it keep compiling; a lock on it is harmless and serializes only those callers.
    /// </summary>
    [Obsolete("Not needed: the concrete engine construction is thread-safe (architecture review R7). Remove the lock.")]
    public static class CompositeSolverSynchronization
    {
        public static object Construction { get; } = new object();
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
