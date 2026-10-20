using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Members
{
    /// <summary>A flat compression part for EN 1993-1-1:2005, 5.5.2(9).
    /// Class3LimitCoefficient multiplies epsilon in Table 5.2 (e.g. 42 for an
    /// internal part in uniform compression, 124 in pure bending, 14 for a
    /// uniformly compressed outstand). It must match the actual stress distribution.
    /// This linear-epsilon input is NOT the CHS D/t limit, which uses epsilon squared.
    /// SigmaComEd is the maximum design compressive stress in this part, positive,
    /// in MPa, from first-order or, where required, second-order analysis.
    /// Supply every compressed part; omit parts with no compression.
    /// NominalClass must already be established at fy for this stress distribution.</summary>
    public sealed class Ec3CompressionPart
    {
        public int NominalClass { get; }
        public double WidthThicknessRatio { get; }
        public double Class3LimitCoefficient { get; }
        public double SigmaComEd { get; }

        public Ec3CompressionPart(int nominalClass, double widthThicknessRatio,
            double class3LimitCoefficient, double sigmaComEd)
        {
            if (nominalClass < 1 || nominalClass > 4)
                throw new ArgumentOutOfRangeException(nameof(nominalClass));
            Ec3StressBasedClassification.Positive(widthThicknessRatio, nameof(widthThicknessRatio));
            Ec3StressBasedClassification.Positive(class3LimitCoefficient, nameof(class3LimitCoefficient));
            Ec3StressBasedClassification.Positive(sigmaComEd, nameof(sigmaComEd));
            NominalClass = nominalClass;
            WidthThicknessRatio = widthThicknessRatio;
            Class3LimitCoefficient = class3LimitCoefficient;
            SigmaComEd = sigmaComEd;
        }
    }

    public sealed class Ec3CompressionPartTreatment
    {
        public Ec3CompressionPart Part { get; }
        public double NominalClass3Limit { get; }
        public double IncreasedClass3Limit { get; }
        public bool CanTreatAsClass3 { get; }
        internal Ec3CompressionPartTreatment(Ec3CompressionPart part, double nominalLimit,
            double increasedLimit, bool permitted)
        {
            Part = part; NominalClass3Limit = nominalLimit;
            IncreasedClass3Limit = increasedLimit; CanTreatAsClass3 = permitted;
        }
    }

    /// <summary>Classification treatment for the supplied load state only, NOT a
    /// load-independent resistance or a completed member verification.</summary>
    public sealed class Ec3SectionClassificationTreatment
    {
        public int NominalClass { get; }
        public int SectionResistanceClass { get; }
        /// <summary>5.5.2(10): no stress-based increase for member buckling checks in 6.3.</summary>
        public int MemberBucklingClass => NominalClass;
        public bool UsesClause5529 => NominalClass == 4 && SectionResistanceClass == 3;
        public double Epsilon { get; }
        public double YieldStrength { get; }
        public double GammaM0 { get; }
        public IReadOnlyList<Ec3CompressionPartTreatment> Parts { get; }
        internal Ec3SectionClassificationTreatment(int nominalClass, int resistanceClass,
            double epsilon, double fy, double gamma, Ec3CompressionPartTreatment[] parts)
        {
            NominalClass = nominalClass; SectionResistanceClass = resistanceClass;
            Epsilon = epsilon; YieldStrength = fy; GammaM0 = gamma;
            Parts = Array.AsReadOnly(parts);
        }
    }

    /// <summary>EN 1993-1-1:2005, 5.5.2(9)-(10), explicit section-only treatment.
    /// No NTC rule, nominal class, steel strength, effective width or buckling
    /// property is changed. No stress is inferred from N/A or from an envelope:
    /// the caller provides the maximum design compression of each flat part.
    /// The legacy bulk checker is intentionally not relaxed globally.</summary>
    public static class Ec3StressBasedClassification
    {
        public static Ec3SectionClassificationTreatment Evaluate(double yieldStrength,
            double gammaM0, params Ec3CompressionPart[] compressionParts)
        {
            Positive(yieldStrength, nameof(yieldStrength));
            Positive(gammaM0, nameof(gammaM0));
            if (compressionParts == null) throw new ArgumentNullException(nameof(compressionParts));
            if (compressionParts.Length == 0)
                throw new ArgumentException("At least one compressed part is required.", nameof(compressionParts));

            double epsilon = Math.Sqrt(235 / yieldStrength);
            double designYield = yieldStrength / gammaM0;
            PositiveResult(epsilon); PositiveResult(designYield);
            var treatments = new Ec3CompressionPartTreatment[compressionParts.Length];
            for (int i = 0; i < compressionParts.Length; i++)
            {
                var part = compressionParts[i];
                if (part == null) throw new ArgumentException("A compression part is null.", nameof(compressionParts));
                double nominalLimit = part.Class3LimitCoefficient * epsilon;
                PositiveResult(nominalLimit);
                if ((part.NominalClass == 4) != (part.WidthThicknessRatio > nominalLimit))
                    throw new ArgumentException("Nominal class and Table 5.2 class-3 limit disagree.", nameof(compressionParts));
                // Increase epsilon only; an overstressed part cannot use the exemption.
                double factor = Math.Max(1, Math.Sqrt(designYield) / Math.Sqrt(part.SigmaComEd));
                double increasedLimit = nominalLimit * factor;
                PositiveResult(increasedLimit);
                bool permitted = part.NominalClass == 4 && part.SigmaComEd <= designYield
                    && part.WidthThicknessRatio <= increasedLimit;
                treatments[i] = new Ec3CompressionPartTreatment(part, nominalLimit, increasedLimit, permitted);
            }
            int nominal = compressionParts.Max(part => part.NominalClass);
            bool allPartsAdmissible = treatments.All(t => t.Part.NominalClass <= 3 || t.CanTreatAsClass3);
            int resistance = nominal == 4 && allPartsAdmissible ? 3 : nominal;
            return new Ec3SectionClassificationTreatment(nominal, resistance,
                epsilon, yieldStrength, gammaM0, treatments);
        }

        internal static void Positive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, "A finite positive value is required.");
        }
        private static void PositiveResult(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArithmeticException("Classification limit outside finite positive numerical range.");
        }
    }
}
