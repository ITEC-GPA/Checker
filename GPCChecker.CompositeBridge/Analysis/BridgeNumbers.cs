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
    /// <summary>Shared construction gate for the existing concrete solver's static initialization.</summary>
    public static class CompositeSolverSynchronization
    {
        public static object Construction { get; } = new object();
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
