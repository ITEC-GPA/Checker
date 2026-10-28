using System;
using System.Collections.Generic;

namespace GPC.Checkers.Concrete.Slenderness
{
    /// <summary>
    /// Biaxial bending of EN 1992-1-1 5.8.9 on two model columns of the same column in the two principal directions (5.8.9(2): separate design, the
    /// imperfection only in the more unfavourable direction, a choice of the caller): (5.38a) λ1/λ2 ≤ 2 and λ2/λ1 ≤ 2, (5.38b) relative eccentricities
    /// (e1/heq,1)/(e2/heq,2) ≤ 0.2 or its inverse, with ej = MEd,j/NEd including the second-order moment and heq,j = ij √12; when both hold the
    /// two separate checks suffice, otherwise (5.39) (MEd,1/MRd,1)^a + (MEd,2/MRd,2)^a ≤ 1 with MRd,j = MRd(NEd) of the domain in the direction j,
    /// a = 2 for circular and elliptical sections, otherwise a = 1.0 / 1.5 / 2.0 at NEd/NRd = 0.1 / 0.7 / 1.0 (linear in between, 1 below 0.1),
    /// NRd = Ac fcd + As fyd. Both results need the first-order moment.
    /// </summary>
    public sealed class ModelColumnBiaxialResult
    {
        public double SlendernessRatio { get; internal set; }
        public bool SlendernessCondition { get; internal set; }
        /// <summary>(e1/heq,1)/(e2/heq,2); infinite when the second moment is zero; null when an equilibrium is missing.</summary>
        public double? EccentricityRatio { get; internal set; }
        public bool EccentricityCondition { get; internal set; }
        /// <summary>(5.38a) and (5.38b): the two separate checks suffice.</summary>
        public bool SeparateChecksSufficient { get; internal set; }
        public double RelativeAxialForce { get; internal set; }
        public double Exponent { get; internal set; }
        /// <summary>(5.39); null when the separate checks suffice, an equilibrium is missing or MRd(N) is not available.</summary>
        public double? Utilization { get; internal set; }
        public bool Verified { get; internal set; }
        public IReadOnlyList<ModelColumnTraceStep> Trace { get; internal set; }
    }

    /// <summary>Check of EN 1992-1-1 5.8.9 on two model columns (<see cref="ModelColumnBiaxialResult"/>).</summary>
    public static class ModelColumnBiaxialCheck
    {
        public static ModelColumnBiaxialResult Calculate(ModelColumnResult first, ModelColumnResult second, bool circularOrElliptic = false)
        {
            if (first == null || second == null) throw new ArgumentNullException();
            if (first.Equilibrium == null || second.Equilibrium == null)
                throw new ArgumentException("Model column, biaxial bending: both directions need the first-order moment.");
            if (Math.Abs(first.AxialForce - second.AxialForce) > 1e-9 * Math.Max(1, Math.Abs(first.AxialForce)))
                throw new ArgumentException("Model column, biaxial bending: the two directions must have the same axial force.");
            const string reference = "EN 1992-1-1 5.8.9";
            var trace = new List<ModelColumnTraceStep>();
            var r = new ModelColumnBiaxialResult();
            double l1 = first.Slenderness.Slenderness, l2 = second.Slenderness.Slenderness;
            r.SlendernessRatio = l1 / l2;
            r.SlendernessCondition = l1 / l2 <= 2 && l2 / l1 <= 2;
            trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialSlendernessRatio, "Rapporto delle snellezze", "λ1/λ2 ≤ 2 e λ2/λ1 ≤ 2", r.SlendernessRatio, "−", reference + " (5.38a)"));
            bool found = first.Equilibrium.Found && second.Equilibrium.Found;
            double m1 = found ? first.Equilibrium.DesignMoment.Value : double.NaN, m2 = found ? second.Equilibrium.DesignMoment.Value : double.NaN;
            if (found)
            {
                double h1 = first.Section.RadiusOfGyration * Math.Sqrt(12), h2 = second.Section.RadiusOfGyration * Math.Sqrt(12);
                double e1 = m1 / h1, e2 = m2 / h2;
                r.EccentricityRatio = e2 > 0 ? e1 / e2 : double.PositiveInfinity;
                r.EccentricityCondition = r.EccentricityRatio.Value <= 0.2 || 1 / r.EccentricityRatio.Value <= 0.2;
                trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialEccentricityRatio, "Rapporto delle eccentricità relative, con il secondo ordine",
                    "(e1/heq,1)/(e2/heq,2) ≤ 0,2 oppure (e2/heq,2)/(e1/heq,1) ≤ 0,2; heq = i·√12", r.EccentricityRatio.Value, "−", reference + " (5.38b)"));
            }
            r.SeparateChecksSufficient = found && r.SlendernessCondition && r.EccentricityCondition;
            double nRd = first.Section.AxialResistance, n = Math.Abs(first.AxialForce);
            r.RelativeAxialForce = n / nRd;
            r.Exponent = circularOrElliptic ? 2 : Exponent(r.RelativeAxialForce);
            bool separate = first.Equilibrium.Verified && second.Equilibrium.Verified;
            if (r.SeparateChecksSufficient)
            {
                r.Verified = separate;
                trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialOutcome, "Esito", "(5.38) soddisfatte: bastano le due verifiche separate", null, "", reference + "(3)"));
                r.Trace = trace.AsReadOnly();
                return r;
            }
            trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialRelativeAxialForce, "Sforzo normale relativo", "NEd/NRd; NRd = Ac·fcd + As·fyd", r.RelativeAxialForce, "−",
                reference + "(4)"));
            trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialExponent, "Esponente", circularOrElliptic ? "a = 2 (sezione circolare o ellittica)"
                : "a = 1,0 / 1,5 / 2,0 per NEd/NRd = 0,1 / 0,7 / 1,0", r.Exponent, "−", reference + "(4)"));
            double? mRd1 = first.NominalCurvature.ResistingMoment, mRd2 = second.NominalCurvature.ResistingMoment;
            if (found && mRd1.HasValue && mRd2.HasValue)
            {
                r.Utilization = Math.Pow(m1 / mRd1.Value, r.Exponent) + Math.Pow(m2 / mRd2.Value, r.Exponent);
                trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialUtilization, "Flessione deviata", "(MEd,1/MRd,1)^a + (MEd,2/MRd,2)^a ≤ 1", r.Utilization.Value, "−",
                    reference + " (5.39)"));
            }
            r.Verified = separate && r.Utilization.HasValue && r.Utilization.Value <= 1;
            trace.Add(new ModelColumnTraceStep(ModelColumnTraceCodes.BiaxialOutcome, "Esito", r.Verified ? "verificato" : "non verificato", null, "", reference));
            r.Trace = trace.AsReadOnly();
            return r;
        }

        /// <summary>Exponent a of (5.39) for rectangular sections: 1.0 / 1.5 / 2.0 at NEd/NRd = 0.1 / 0.7 / 1.0, linear in between.</summary>
        public static double Exponent(double relativeAxialForce)
        {
            double x = relativeAxialForce;
            if (x <= 0.1) return 1;
            if (x <= 0.7) return 1 + 0.5 * (x - 0.1) / 0.6;
            if (x <= 1) return 1.5 + 0.5 * (x - 0.7) / 0.3;
            return 2;
        }
    }
}
