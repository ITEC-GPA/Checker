using System;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Qualified EC3:2005 6.3.4 route for an isolated, uniform sharp H/T
    /// with fork ends, free warping, no sway or intermediate restraints. Constant
    /// compression at the centroid and constant first-order major-axis moment.
    /// No transverse loads, minor-axis moment, applied torsion or plastic hinges.
    /// Conservative elastic section resistance, also for classes 1/2. N/mm/MPa/Nmm.
    /// Explicit curves record the caller's fabrication/National Annex choices.</summary>
    public static class Ec3MonosymmetricMember
    {
        public static MonosymmetricMemberResistance EvaluateUniformFork(ISteelSection section,
            double length, double normalForce, double majorMoment,
            Ec3GeneralMethodCurve inPlaneCurve, Ec3GeneralMethodCurve lateralCurve,
            Ec3GeneralMethodCurve lateralTorsionalCurve, double gammaM0, double gammaM1, string parameterSet)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            Ec3GeneralMethod.Positive(length, nameof(length)); Ec3GeneralMethod.Positive(gammaM0, nameof(gammaM0));
            Ec3GeneralMethod.Positive(gammaM1, nameof(gammaM1));
            Ec3GeneralMethod.Finite(normalForce, nameof(normalForce)); Ec3GeneralMethod.Finite(majorMoment, nameof(majorMoment));
            if (normalForce > 0) throw new NotSupportedException("This 6.3.4 route supports compression (N<=0), not axial tension.");
            if (normalForce == 0 && majorMoment == 0) throw new ArgumentException("A nonzero load pattern is required.");
            double[][] rects = Rectangles(section);
            double fy = section.SteelMaterial.Fyk, e = section.SteelMaterial.E, nu = section.SteelMaterial.Ni;
            Ec3GeneralMethod.Positive(fy, "fy"); Ec3GeneralMethod.Positive(e, "E");
            if (!(nu > -1 && nu < .5)) throw new ArgumentOutOfRangeException("nu");
            var classification = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), fy);
            if (!classification.IsEvaluated) throw new ArgumentException(classification.DiagnosticCode);
            if (classification.NominalClass.Value == 4) throw new NotSupportedException("Class4EffectivePropertiesRequired");
            double area = section.Area, ix = section.J11, iz = section.J22, j = section.Jt, iw = section.Jw;
            foreach (double value in new[] { area, ix, iz, j, section.Wel1 }) Ec3GeneralMethod.Positive(value, "section property");
            if (!(iw >= 0) || double.IsInfinity(iw)) throw new ArgumentOutOfRangeException("Iw");
            double yc = section.Centroid.Y, ys = section.ShearCenter.Y, y0 = ys - yc;
            Ec3GeneralMethod.Finite(y0, "shear-centre offset");
            double integral = 0;
            foreach (double[] r in rects)
            {
                double b = r[0], depth = r[2] - r[1], y = (r[1] + r[2]) / 2 - yc;
                integral += b * depth * y * (y * y + depth * depth / 4 + b * b / 12);
            }
            double geo = integral / ix - 2 * y0;
            double i02 = (ix + iz) / area + y0 * y0;
            double ncrIn = ElasticStabilityArithmetic.Euler(length, ix, e);
            double ncrOut = ElasticStabilityArithmetic.Euler(length, iz, e);
            double kt = e / (2 * (1 + nu)) * j + Math.PI * Math.PI * e * iw / (length * length);
            Ec3GeneralMethod.Positive(ncrIn, "NcrInPlane"); Ec3GeneralMethod.Positive(ncrOut, "NcrOutOfPlane");
            Ec3GeneralMethod.Positive(kt, "torsional stiffness");
            double p = -normalForce;
            double ac = UniformForkMemberArithmetic.CriticalFactor(p, majorMoment, ncrOut, kt, i02, y0, geo);
            double bow = length / Ec3GeneralMethod.ElasticBowDivisor(inPlaneCurve);
            double au = UniformForkMemberArithmetic.InPlaneResistanceFactor(p, Math.Abs(majorMoment),
                area, section.Wel1, fy, ncrIn, bow);
            var result = Ec3GeneralMethod.EvaluateInPlane(au, ac, lateralCurve, lateralTorsionalCurve, gammaM1,
                parameterSet, "Uniform fork H/T: exact elastic secant response + sinusoidal Table 5.1 bow; " +
                "first elastic yield envelope; coupled sine-mode lateral/torsional eigenproblem (first-order stresses).");
            double sectionUtilization = (p / area + Math.Abs(majorMoment) / section.Wel1) * gammaM0 / fy;
            return new MonosymmetricMemberResistance(result, classification.NominalClass.Value,
                sectionUtilization, bow, ncrIn, ncrOut, kt, geo, y0, inPlaneCurve);
        }

        private static double[][] Rectangles(ISteelSection section)
        {
            var shape = section.SectionShape;
            if (shape == null || Math.Abs(shape.AngleX1) > 1e-10)
                throw new NotSupportedException("Horizontal major principal axis required.");
            if (shape.GetType() == typeof(SectionH))
            {
                var h = (SectionH)shape;
                if (h.R != 0) throw new NotSupportedException("Sharp H section required.");
                Validate(h.Height, h.ThicknessWeb, h.LenghtTopFlange, h.ThicknessTopFlange);
                Validate(h.Height, h.ThicknessWeb, h.LenghtBottomFlange, h.ThicknessBottomFlange);
                Ec3GeneralMethod.Positive(h.HeightWeb, "web height");
                return new[] { new[] { h.LenghtBottomFlange, 0d, h.ThicknessBottomFlange },
                    new[] { h.ThicknessWeb, h.ThicknessBottomFlange, h.Height - h.ThicknessTopFlange },
                    new[] { h.LenghtTopFlange, h.Height - h.ThicknessTopFlange, h.Height } };
            }
            if (shape.GetType() == typeof(SectionT))
            {
                var t = (SectionT)shape;
                if (t.R != 0) throw new NotSupportedException("Sharp T section required.");
                Validate(t.Height, t.ThicknessWeb, t.LenghtFlange, t.ThicknessFlange);
                return new[] { new[] { t.ThicknessWeb, 0d, t.HeightWeb },
                    new[] { t.LenghtFlange, t.HeightWeb, t.Height } };
            }
            throw new NotSupportedException("Only exact sharp H and T sections are qualified.");
        }
        private static void Validate(double h, double tw, double b, double tf)
        {
            foreach (double v in new[] { h, tw, b, tf, h - tf }) Ec3GeneralMethod.Positive(v, "geometry");
            if (tw > b) throw new ArgumentException("Web must fit inside the flange.");
        }
    }

    internal static class UniformForkMemberArithmetic
    {
        internal static double CriticalFactor(double p, double m, double ncr, double kt, double i02, double y0, double geo)
        {
            // K=diag(Ncr,z,Kt); destabilizing G=[[P,M+P*y0],[M+P*y0,P*i0^2-M*geo]].
            // The smallest positive alpha is reciprocal to the largest eigenvalue
            // of K^(-1/2) G K^(-1/2). In-plane flexural mode is deliberately excluded.
            double a = p / ncr, d = (p * i02 - m * geo) / kt, b = (m + p * y0) / Math.Sqrt(ncr) / Math.Sqrt(kt);
            double scale = Math.Max(Math.Abs(b), Math.Max(Math.Abs(a), Math.Abs(d)));
            Ec3GeneralMethod.Positive(scale, "eigenproblem scale");
            a /= scale; b /= scale; d /= scale;
            double halfTrace = .5 * (a + d), root = .5 * Math.Sqrt((a - d) * (a - d) + 4 * b * b);
            double eigenvalue = halfTrace >= 0 ? halfTrace + root : (b * b - a * d) / (root - halfTrace);
            double factor = (1 / scale) / eigenvalue;
            Ec3GeneralMethod.Positive(factor, "alphaCrOp");
            return factor;
        }

        internal static double InPlaneResistanceFactor(double p, double m, double area, double wel, double fy, double ncr, double bow)
        {
            if (p == 0) return fy * wel / m;
            double low = 0, high = Math.Min(area * fy / p, ncr / p);
            if (m > 0) high = Math.Min(high, fy * wel / m);
            for (int i = 0; i < 80; i++)
            {
                double alpha = (low + high) / 2, ratio = alpha * p / ncr;
                // Exact midspan moment for uniform end moments + sinusoidal bow.
                // Worst imperfection sign and smallest elastic modulus form a
                // conservative envelope for unequal extreme-fibre distances.
                double amplified = alpha * m / Math.Cos(.5 * Math.PI * Math.Sqrt(ratio))
                    + alpha * p * bow / (1 - ratio);
                if (alpha * p / area + amplified / wel < fy) low = alpha; else high = alpha;
            }
            return (low + high) / 2;
        }
    }

    public sealed class MonosymmetricMemberResistance
    {
        public Ec3GeneralMethodResult GeneralMethod { get; }
        public int NominalCompressionClass { get; }
        public double SectionUtilization { get; }
        public double Utilization => Math.Max(SectionUtilization, GeneralMethod.Utilization);
        public bool IsSatisfied => Utilization <= 1;
        public double InitialBow { get; }
        public double NcrInPlane { get; }
        public double NcrOutOfPlane { get; }
        public double TorsionalStiffness { get; }
        public double StressGeometricCoefficient { get; }
        public double ShearCentreOffset { get; }
        public Ec3GeneralMethodCurve InPlaneCurve { get; }
        public string Scope => "Uniform sharp H/T, fork ends/free warping, centroidal compression + constant first-order major moment; elastic resistance; no transverse actions.";
        internal MonosymmetricMemberResistance(Ec3GeneralMethodResult result, int cls, double section, double bow,
            double ni, double no, double kt, double geo, double y0, Ec3GeneralMethodCurve curve)
        { GeneralMethod=result; NominalCompressionClass=cls; SectionUtilization=section; InitialBow=bow;
            NcrInPlane=ni; NcrOutOfPlane=no; TorsionalStiffness=kt; StressGeometricCoefficient=geo; ShearCentreOffset=y0; InPlaneCurve=curve; }
    }
}
