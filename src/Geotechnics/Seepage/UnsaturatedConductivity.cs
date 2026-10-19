using GPC.Model.Geotechnics.Seepage;

namespace GPC.Checkers.Geotechnics.Seepage;

public readonly struct UnsaturatedState
{
    public double EffectiveSaturation { get; }
    public double RelativeConductivity { get; }
    internal UnsaturatedState(double saturation, double conductivity) { EffectiveSaturation = saturation; RelativeConductivity = conductivity; }
}

/// <summary>Van Genuchten (1980)–Mualem, m=1−1/n and pore-connectivity exponent 1/2.
/// No regularization floor or unreported residual conductivity.</summary>
public static class UnsaturatedConductivity
{
    public static UnsaturatedState At(VanGenuchtenCurve curve, double pressureHead)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        if (!Finite(curve.Alpha) || curve.Alpha <= 0 || !Finite(curve.N) || curve.N <= 1 || !Finite(pressureHead))
            throw new ArgumentException("Van Genuchten richiede α>0, n>1 e carico di pressione finito.");
        if (pressureHead >= 0) return new(1, 1);
        double logX = curve.N * (Math.Log(curve.Alpha) + Math.Log(-pressureHead));
        // log(1+exp(x)) avoids overflow. The tail expression avoids subtracting two numbers close to one.
        double logOnePlus = logX > 35 ? logX + Math.Exp(-logX) : Math.Log(1 + Math.Exp(logX));
        double m = 1 - 1 / curve.N, saturation = Math.Exp(-m * logOnePlus);
        double inverse = Math.Exp(-logOnePlus);
        double bracket = inverse < 1e-7 ? m * inverse * (1 + (1 - m) * inverse / 2)
            : 1 - Math.Pow(1 - inverse, m);
        return new(saturation, Math.Sqrt(saturation) * bracket * bracket);
    }
    internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
}
