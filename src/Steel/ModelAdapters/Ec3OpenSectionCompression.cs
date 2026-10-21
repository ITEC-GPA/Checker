using System;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Axial compression only, N/mm/MPa. Fork ends, free warping, same
    /// sinusoidal mode and length in both flexural planes and torsion. EN 1993-1-1
    /// 6.3.1 including 6.3.1.4; no eccentric one-leg connection or N-M interaction.</summary>
    public static class Ec3OpenSectionCompression
    {
        public static OpenSectionCompressionResistance Evaluate(ISteelSection section, double length,
            double yieldStrength, double gammaM1, string parameterSet)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (string.IsNullOrWhiteSpace(parameterSet)) throw new ArgumentException("Explicit EC3 parameter provenance required.", nameof(parameterSet));
            Ec3AngleCompression.Positive(length, nameof(length));
            Ec3AngleCompression.Positive(yieldStrength, nameof(yieldStrength));
            Ec3AngleCompression.Positive(gammaM1, nameof(gammaM1));
            var shape = section.SectionShape;
            if (shape == null || (shape.GetType() != typeof(SectionC) && shape.GetType() != typeof(SectionL)) ||
                !section.IsRolled || section.FormedType != Section.FormedTypes.HotFinished)
                throw new NotSupportedException("Only exact hot-rolled C/L shapes are qualified.");
            int nominalClass;
            double alpha;
            if (shape is SectionL angle)
            {
                if (angle.VerticalLegThickness != angle.HorizontalLegThickness)
                    throw new NotSupportedException("Angle classification requires equal leg thicknesses.");
                nominalClass = Ec3AngleCompression.Classify(angle.VerticalLegLength, angle.HorizontalLegLength,
                    angle.VerticalLegThickness, yieldStrength, angle.R);
                alpha = .34; // Table 6.2: angles, curve b.
            }
            else
            {
                var channel = (SectionC)shape;
                if (channel.LengthTop != channel.LengthBottom || channel.ThicknessTop != channel.ThicknessBottom)
                    throw new NotSupportedException("Channel torsional qualification requires equal flanges.");
                var classification = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), yieldStrength);
                if (!classification.IsEvaluated) throw new ArgumentException(classification.DiagnosticCode, nameof(section));
                nominalClass = classification.NominalClass.Value;
                alpha = .49; // Table 6.2: channels, curve c.
            }
            if (nominalClass == 4) throw new NotSupportedException("Class4EffectivePropertiesRequired");
            double area = section.Area, i1 = section.J11, i2 = section.J22;
            double e = section.SteelMaterial.E, nu = section.SteelMaterial.Ni;
            Ec3AngleCompression.Positive(area, "Area"); Ec3AngleCompression.Positive(i1, "I1"); Ec3AngleCompression.Positive(i2, "I2");
            Ec3AngleCompression.Positive(e, "E"); Ec3AngleCompression.Positive(section.Jt, "Jt");
            if (!(section.Jw >= 0) || double.IsInfinity(section.Jw) || !(nu > -1 && nu < .5))
                throw new ArgumentException("Finite nonnegative warping constant and -1 < nu < 0.5 required.");
            double dx = section.ShearCenter.X - section.Centroid.X, dy = section.ShearCenter.Y - section.Centroid.Y;
            double theta = shape.AngleX1;
            double x = dx * Math.Cos(theta) + dy * Math.Sin(theta), y = -dx * Math.Sin(theta) + dy * Math.Cos(theta);
            double i02 = (i1 + i2) / area + x * x + y * y;
            Ec3AngleCompression.Positive(i02, "PolarRadiusSquared");
            double n1 = ElasticStabilityArithmetic.Euler(length, i1, e), n2 = ElasticStabilityArithmetic.Euler(length, i2, e);
            double nt = (e / (2 * (1 + nu)) * section.Jt + Math.PI * Math.PI * e * section.Jw / (length * length)) / i02;
            Ec3AngleCompression.Positive(n1, "Ncr1"); Ec3AngleCompression.Positive(n2, "Ncr2"); Ec3AngleCompression.Positive(nt, "NcrT");
            double ncr = ElasticStabilityArithmetic.FlexuralTorsional(n1, n2, nt, x, y, i02);
            double lambda = Math.Sqrt(area * yieldStrength / ncr);
            double chi = lambda <= .2 ? 1 : BucklingReductionArithmetic.AxialChi(BucklingReductionArithmetic.Phi(alpha, lambda), lambda);
            double nb = chi * area * yieldStrength / gammaM1;
            Ec3AngleCompression.Positive(nb, "NbRd");
            return new OpenSectionCompressionResistance(nominalClass, n1, n2, nt, ncr, lambda, chi, nb, alpha, parameterSet);
        }
    }

    public sealed class OpenSectionCompressionResistance
    {
        public int NominalClass { get; }
        public double Ncr1 { get; }
        public double Ncr2 { get; }
        public double NcrT { get; }
        /// <summary>Lowest coupled critical force, including uncoupled flexural modes.</summary>
        public double Ncr { get; }
        public double Slenderness { get; }
        public double Chi { get; }
        public double NbRd { get; }
        public double Alpha { get; }
        public string ParameterSet { get; }
        internal OpenSectionCompressionResistance(int cls, double n1, double n2, double nt, double ncr,
            double lambda, double chi, double nb, double alpha, string parameters)
        { NominalClass = cls; Ncr1 = n1; Ncr2 = n2; NcrT = nt; Ncr = ncr; Slenderness = lambda; Chi = chi; NbRd = nb; Alpha = alpha; ParameterSet = parameters; }
    }
}
