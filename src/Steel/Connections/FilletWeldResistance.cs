using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Local throat-stress checks in MPa, actual throat plane only. Does not check weld geometry,
    /// compatible consumables, execution, local bending or parent parts. Compression is included in the quadratic
    /// criterion; the separate 0.9fu/gammaM2 criterion applies to tensile normal stress.</summary>
    public static class FilletWeldResistance
    {
        public static FilletWeldStressResult Verify(ConnectionDesignCode code, FilletWeldMaterial firstMaterial,
            FilletWeldMaterial secondMaterial, double sigmaPerpendicular, double tauPerpendicular,
            double tauParallel, FilletWeldMethod method)
            => Evaluate(code, firstMaterial, secondMaterial, sigmaPerpendicular, tauPerpendicular, tauParallel, method, 1);

        internal static FilletWeldStressResult Evaluate(ConnectionDesignCode code, FilletWeldMaterial first,
            FilletWeldMaterial second, double sigma, double perpendicular, double parallel, FilletWeldMethod method, double factor)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            FilletWeldNumbers.Finite(sigma, nameof(sigma)); FilletWeldNumbers.Finite(perpendicular, nameof(perpendicular));
            FilletWeldNumbers.Finite(parallel, nameof(parallel));
            if (!Enum.IsDefined(typeof(FilletWeldMethod), method)) throw new ArgumentOutOfRangeException(nameof(method));
            ConnectionValidation.Positive(factor, nameof(factor));
            if (factor > 1) throw new ArgumentOutOfRangeException(nameof(factor));
            var checks = new List<ConnectionCheck>(); var diagnostics = new List<string>();
            if (Math.Min(first.Thickness, second.Thickness) < 4) diagnostics.Add("PARENT_THICKNESS_BELOW_4_MM_SCOPE");
            double fu = Math.Min(first.Fu, second.Fu);
            var grade = (FilletWeldSteelGrade)Math.Min((int)first.Grade, (int)second.Grade);
            double beta = grade == FilletWeldSteelGrade.S235 ? .8 : grade == FilletWeldSteelGrade.S275 ? .85
                : grade == FilletWeldSteelGrade.S355 ? .9 : 1;
            if (diagnostics.Count == 0)
            {
                double strength = fu / (beta * code.GammaM2Connection) * factor;
                string source = code.IsNtc ? "NTC2018 4.2.8.2.4" : "EN1993-1-8:2005 4.5.3";
                if (method == FilletWeldMethod.Directional)
                {
                    double equivalent = FilletWeldNumbers.Norm(sigma, Math.Sqrt(3) * perpendicular, Math.Sqrt(3) * parallel);
                    checks.Add(new ConnectionCheck("DirectionalEquivalent", equivalent, strength,
                        source + (code.IsNtc ? " eq.4.2.81" : ".2 eq.4.1"), "MPa"));
                    checks.Add(new ConnectionCheck("NormalTension", Math.Max(0, sigma), .9 * fu / code.GammaM2Connection * factor,
                        source + (code.IsNtc ? " eq.4.2.81" : ".2 eq.4.1"), "MPa"));
                }
                else
                    checks.Add(new ConnectionCheck("SimplifiedResultant", FilletWeldNumbers.Norm(sigma, perpendicular, parallel),
                        strength / Math.Sqrt(3), source + (code.IsNtc ? " eq.4.2.82-83" : ".3 eq.4.2-4.4"), "MPa"));
            }
            return new FilletWeldStressResult(sigma, perpendicular, parallel, fu, beta, factor, checks, diagnostics);
        }
    }
}
