namespace GPC.Design.Compatibility;

/// <summary>Runtime-independent primitives for the netstandard2.0 target. No engineering formulas or tolerances.</summary>
internal static class PortableMath
{
    internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    internal static int Clamp(int value, int min, int max)
    {
        if (min > max) throw new ArgumentException("min must be less than or equal to max.");
        return value < min ? min : value > max ? max : value;
    }

    internal static double Clamp(double value, double min, double max)
    {
        if (min > max) throw new ArgumentException("min must be less than or equal to max.");
        return value < min ? min : value > max ? max : value;
    }

    internal static double CopySign(double value, double sign) => BitConverter.Int64BitsToDouble(
        (BitConverter.DoubleToInt64Bits(value) & long.MaxValue) | (BitConverter.DoubleToInt64Bits(sign) & long.MinValue));

    internal static double BitDecrement(double value)
    {
        if (double.IsNaN(value) || value == double.NegativeInfinity) return value;
        if (value == double.PositiveInfinity) return double.MaxValue;
        if (value == 0) return -double.Epsilon; // Both signed zeros have the same predecessor.
        long bits = BitConverter.DoubleToInt64Bits(value);
        return BitConverter.Int64BitsToDouble(bits + (value < 0 ? 1 : -1));
    }

    internal static double Hypot(double x, double y)
    {
        x = Math.Abs(x); y = Math.Abs(y);
        if (double.IsInfinity(x) || double.IsInfinity(y)) return double.PositiveInfinity;
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        double large = Math.Max(x, y), small = Math.Min(x, y);
        if (large == 0) return 0;
        double ratio = small / large;
        return large * Math.Sqrt(1 + ratio * ratio); // Scaling avoids overflow/underflow in x*x + y*y.
    }

    internal static double Asinh(double value)
    {
        double x = Math.Abs(value);
        if (double.IsNaN(x) || double.IsInfinity(x) || x < 1e-8) return value;
        double result;
        if (x > 1e154) result = Math.Log(x) + 0.69314718055994530941723212145818;
        else if (x > 2) result = Math.Log(x + Math.Sqrt(x * x + 1));
        else
        {
            // asinh(x) = log1p(x + x*x/(1+sqrt(1+x*x))). Compensate the rounding of 1+t.
            double t = x + x * x / (1 + Math.Sqrt(1 + x * x)), u = 1 + t;
            result = u == 1 ? t : Math.Log(u) * (t / (u - 1));
        }
        return CopySign(result, value);
    }
}

internal static class Guard
{
    internal static void NotNull([System.Diagnostics.CodeAnalysis.NotNull] object? value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? parameterName = null)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
    }
}

internal static class PortableEnum
{
    internal static bool IsDefined<T>(T value) where T : struct, Enum => Enum.IsDefined(typeof(T), value);
    internal static T[] GetValues<T>() where T : struct, Enum => (T[])Enum.GetValues(typeof(T));
}
