using System;
using GPC.Model.Materials;

namespace GPC.Checkers.CompositeBridge;



/// <summary>
/// EN 1992-1-1:2004, 3.1.4 and Annex B, normal-weight concrete and constant RH.
/// Negative contraction, MPa/mm/days. Ported from the frozen BridGHe source without changing the historical GPC GetEpsilonCSInfiniteTime API.
/// Numerical behavior is checked against the source smoke references; no new applicability range is inferred.
/// Reference: JRC Handbook 3 (2005), annex pp.18-19 and 30, handbook3.pdf.
/// </summary>
public static class ConcreteShrinkage
{
    private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    public static (double Increment,double Total,double Drying,double Autogenous,double NotionalSize,double Kh) Calculate(double fck, ConcreteMaterialEuropeanCommon.CementTypes cement,
        double rh, double area, double dryingPerimeter, double age, double dryingStart, double referenceAge = 0)
    {
        if (!Finite(fck) || fck < 12 || fck > 90) throw new ArgumentException("Fck must be 12..90 MPa.");
        if (!Finite(rh) || rh < 40 || rh > 100) throw new ArgumentException("RH must be 40..100 percent.");
        if (!Enum.IsDefined(typeof(ConcreteMaterialEuropeanCommon.CementTypes),cement)) throw new ArgumentException("Unknown cement class.");
        if (!Finite(dryingStart) || dryingStart < 0 || !Finite(referenceAge) || referenceAge < 0
            || double.IsNaN(age) || age < referenceAge) throw new ArgumentException("Require 0 <= reference age <= age and drying start >= 0 days.");
        double h0 = ConcreteTimeEffects.NotionalSize(area, dryingPerimeter);
        double kh = h0 <= 100 ? 1 : h0 <= 200 ? 1 - .15 * (h0 - 100) / 100 :
            h0 <= 300 ? .85 - .10 * (h0 - 200) / 100 : h0 <= 500 ? .75 - .05 * (h0 - 300) / 200 : .70;
        var material = new ConcreteMaterialEN1992("Shrinkage", fck, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle, cementType: cement);
        var (a1, a2) = cement switch
        {
            ConcreteMaterialEuropeanCommon.CementTypes.ClassS => (3d, .13),
            ConcreteMaterialEuropeanCommon.CementTypes.ClassN => (4d, .12),
            _ => (6d, .11)
        };
        double dryInfinity = .85 * (220 + 110 * a1) * Math.Exp(-a2 * Math.Abs(material.Fcm) / 10) * 1e-6 * 1.55 * (1 - Math.Pow(rh / 100, 3)) * kh;
        double autoInfinity = 2.5 * (Math.Abs(material.Fck) - 10) * 1e-6;
        (double Dry, double Auto) At(double t)
        {
            if (double.IsPositiveInfinity(t)) return (-dryInfinity, -autoInfinity);
            double elapsed = Math.Max(0, t - dryingStart);
            return (-dryInfinity * elapsed / (elapsed + .04 * Math.Pow(h0, 1.5)),
                -autoInfinity * (1 - Math.Exp(-.2 * Math.Sqrt(t))));
        }
        var end = At(age); var start = At(referenceAge);
        return (end.Dry + end.Auto - start.Dry - start.Auto, end.Dry + end.Auto, end.Dry, end.Auto, h0, kh);
    }
}
