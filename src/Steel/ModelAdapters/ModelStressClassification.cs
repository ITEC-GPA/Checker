using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Class of one cut under its concomitant N, M1, M2 (EN 1993-1-1:2005 5.5.2 and
    /// Table 5.2; NTC 2018 4.2.3.1 Tables 4.2.III-IV, same limits). Class 1/2 limits use the
    /// plastic compressed fraction alpha, the class 3 limit the elastic stress ratio psi.
    /// Covered: doubly symmetric H, boxes of uniform wall thickness, C with M2=0, all with
    /// principal axes on the geometry. Conservative choices: an outstand or a wall whose
    /// in-plane plastic state is not resolved by N and a single moment takes alpha=1; a part
    /// with no elastic compression keeps the alpha limits for classes 1/2 and is at most class 3.
    /// Other families and states return null and keep uniform-compression classification.</summary>
    internal static class ModelStressClassification
    {
        internal sealed class Result
        {
            internal int Class { get; }
            internal string Source { get; }
            internal Result(int cl, string source) { Class = cl; Source = source; }
        }

        private sealed class Part
        {
            internal string Id; internal double Ratio; internal double Alpha; internal double? Psi;
            internal double First, Second, Third; internal bool ElasticCompression; internal int Class;
        }

        internal static Result TryEvaluate(SteelVerificationInput input, ISteelSection section)
        {
            var shape = section.SectionShape;
            var family = SteelSectionFamilies.Of(shape);
            if (family == null || Math.Abs(shape.AngleX1) > 1e-10 || section.FormedType == Section.FormedTypes.ColdFormed && family.OpenProfile)
                return null;
            var a = input.EffectiveActions;
            double fy = input.Section.YieldStrength, area = input.Section.Area;
            double eps = Math.Sqrt(235 / fy), sigmaN = -a.N / area, nc = -a.N;
            double i1 = section.J11, i2 = section.J22;
            if (!(i1 > 0) || !(i2 > 0) || double.IsInfinity(i1) || double.IsInfinity(i2)) return null;
            double m1 = Math.Abs(a.M1), m2 = Math.Abs(a.M2);
            if (m1 == 0 && m2 == 0) return null; // Uniform compression or tension: the nominal route applies as is.
            List<Part> parts;
            if (family.Kind == SteelSectionKind.IOrH) parts = H((SectionH)shape, fy, eps, sigmaN, nc, m1, m2, i1, i2);
            else if (family.Kind == SteelSectionKind.Channel) parts = C((SectionC)shape, fy, eps, sigmaN, nc, m1, m2, i1);
            else if (family.Kind == SteelSectionKind.RectangularTube) parts = Box(section, fy, eps, sigmaN, nc, m1, m2, i1, i2);
            else return null;
            if (parts == null) return null;
            foreach (var p in parts) Classify(p);
            int cl = parts.Max(p => p.Class);
            bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            var governing = parts.Where(p => p.Class == cl).Select(Describe);
            return new Result(cl, (ntc ? "NTC 2018 4.2.3.1 Tables 4.2.III-IV" : "EN 1993-1-1:2005 5.5.2, Table 5.2")
                + "; concomitant N-M1-M2 distribution (plastic alpha, elastic psi); governing " + string.Join(", ", governing));
        }

        private static List<Part> H(SectionH h, double fy, double eps, double sigmaN, double nc, double m1, double m2, double i1, double i2)
        {
            if (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange) return null;
            double b = h.LenghtTopFlange, tf = h.ThicknessTopFlange, tw = h.ThicknessWeb, d = h.D;
            double c = (b - tw) / 2 - h.R;
            if (!(c > 0) || !(d > 0)) return null;
            var parts = new List<Part>();
            double webAlpha = InPlaneAlpha(sigmaN, nc, m1, 0, d * tw * fy);
            parts.Add(Internal("Web", d / tw, webAlpha, sigmaN + m1 * d / 2 / i1, sigmaN - m1 * d / 2 / i1, eps));
            bool tensionFlangePlastic = m1 > 0 && m2 == 0 && webAlpha > 0 && webAlpha < 1;
            double xRoot = tw / 2 + h.R, xTip = b / 2, yFlange = h.Height / 2;
            foreach (int f in new[] { 1, -1 })
            {
                double sigmaF = sigmaN + f * m1 * yFlange / i1;
                double alpha = m1 == 0 && m2 == 0 ? (sigmaN > 0 ? 1 : 0) : f < 0 && tensionFlangePlastic ? 0 : 1;
                foreach (int s in new[] { 1, -1 })
                    parts.Add(Outstand((f > 0 ? "CompressedFlange" : "OtherFlange") + (s > 0 ? ".Side+" : ".Side-"), c / tf, alpha,
                        sigmaF + s * m2 * xRoot / i2, sigmaF + s * m2 * xTip / i2, eps));
            }
            return parts;
        }

        private static List<Part> C(SectionC c, double fy, double eps, double sigmaN, double nc, double m1, double m2, double i1)
        {
            if (m2 != 0 || c.LengthTop != c.LengthBottom || c.ThicknessTop != c.ThicknessBottom) return null;
            double tw = c.ThicknessWeb, tf = c.ThicknessTop, d = c.HeightWeb - 2 * c.R1, w = c.LengthTop - tw - c.R1;
            if (!(d > 0) || !(w > 0)) return null;
            var parts = new List<Part>();
            double webAlpha = InPlaneAlpha(sigmaN, nc, m1, 0, d * tw * fy);
            parts.Add(Internal("Web", d / tw, webAlpha, sigmaN + m1 * d / 2 / i1, sigmaN - m1 * d / 2 / i1, eps));
            bool tensionFlangePlastic = m1 > 0 && webAlpha > 0 && webAlpha < 1;
            foreach (int f in new[] { 1, -1 })
            {
                double sigmaF = sigmaN + f * m1 * c.Height / 2 / i1;
                parts.Add(Outstand(f > 0 ? "CompressedFlange" : "OtherFlange", w / tf, m1 == 0 ? (sigmaN > 0 ? 1 : 0) : f < 0 && tensionFlangePlastic ? 0 : 1, sigmaF, sigmaF, eps));
            }
            return parts;
        }

        private static List<Part> Box(ISteelSection section, double fy, double eps, double sigmaN, double nc, double m1, double m2, double i1, double i2)
        {
            double width, height, t, cw, ch;
            if (section.SectionShape is SectionRHSRoundedCorners r)
            {
                if (r.ThicknessTop != r.ThicknessBottom || r.ThicknessTop != r.ThicknessWebLeft || r.ThicknessTop != r.ThicknessWebRight) return null;
                double corner = Math.Min(r.OutsideRadius, r.Thickness + r.InsideRadius);
                width = r.Base; height = r.Height; t = r.Thickness; cw = r.Base - 2 * corner; ch = r.Height - 2 * corner;
            }
            else
            {
                var rhs = (SectionRHS)section.SectionShape;
                if (rhs.R != 0 || rhs.ThicknessTop != rhs.ThicknessBottom || rhs.ThicknessTop != rhs.ThicknessWebLeft || rhs.ThicknessTop != rhs.ThicknessWebRight) return null;
                width = rhs.Base; height = rhs.Height; t = rhs.ThicknessTop; cw = rhs.BaseInternal; ch = rhs.Heightinternal;
            }
            if (!(cw > 0) || !(ch > 0)) return null;
            // Side walls bend in plane with M1, top/bottom walls with M2; the two walls of a pair share N.
            double sideAlpha = InPlaneAlpha(sigmaN, nc, m1, m2, 2 * ch * t * fy);
            double flangeAlpha = InPlaneAlpha(sigmaN, nc, m2, m1, 2 * cw * t * fy);
            // A wall without in-plane bending is uniformly stressed: the compressed one is fully
            // compressed; the other is fully in tension only if the plastic axis lies in the pair
            // of walls bending in plane.
            double Uniform(int side, double inPlaneOfOthers, double othersAlpha)
                => inPlaneOfOthers == 0 ? (sigmaN > 0 ? 1 : 0) : side > 0 || !(othersAlpha > 0 && othersAlpha < 1) ? 1 : 0;
            var parts = new List<Part>();
            foreach (int f in new[] { 1, -1 })
            {
                double uniform = sigmaN + f * m1 * height / 2 / i1;
                double alpha = m2 != 0 ? flangeAlpha : Uniform(f, m1, sideAlpha);
                parts.Add(Internal(f > 0 ? "CompressedFlangeWall" : "OtherFlangeWall", cw / t, alpha,
                    uniform + m2 * cw / 2 / i2, uniform - m2 * cw / 2 / i2, eps));
            }
            foreach (int s in new[] { 1, -1 })
            {
                double uniform = sigmaN + s * m2 * width / 2 / i2;
                double alpha = m1 != 0 ? sideAlpha : Uniform(s, m2, flangeAlpha);
                parts.Add(Internal(s > 0 ? "CompressedSideWall" : "OtherSideWall", ch / t, alpha,
                    uniform + m1 * ch / 2 / i1, uniform - m1 * ch / 2 / i1, eps));
            }
            return parts;
        }

        // Plastic compressed fraction of a part bending in its own plane with moment m, where
        // the parts sharing that plane resist N up to squash (fully yielded). A concurrent moment
        // in the other plane leaves the plastic state unresolved: alpha=1 when compressed.
        private static double InPlaneAlpha(double sigmaN, double nc, double m, double otherMoment, double squash)
        {
            if (m == 0) return sigmaN > 0 ? 1 : 0;
            if (otherMoment != 0) return 1;
            return Math.Max(0, Math.Min(1, 0.5 + nc / (2 * squash)));
        }

        private static Part Internal(string id, double ratio, double alpha, double sigmaA, double sigmaB, double eps)
        {
            var p = new Part { Id = id, Ratio = ratio, Alpha = alpha };
            double max = Math.Max(sigmaA, sigmaB), min = Math.Min(sigmaA, sigmaB);
            PlateClassificationArithmetic.InternalLimits(alpha > 0 ? alpha : 1, 1, eps, out p.First, out p.Second, out _);

            p.ElasticCompression = max > 0;
            if (p.ElasticCompression)
            {
                p.Psi = min / max;
                PlateClassificationArithmetic.InternalLimits(0.5, p.Psi.Value, eps, out _, out _, out p.Third);
            }
            return p;
        }

        private static Part Outstand(string id, double ratio, double alpha, double sigmaRoot, double sigmaTip, double eps)
        {
            var p = new Part { Id = id, Ratio = ratio, Alpha = alpha };
            p.ElasticCompression = Math.Max(sigmaRoot, sigmaTip) > 0;
            double root = p.ElasticCompression ? sigmaRoot : 1, tip = p.ElasticCompression ? sigmaTip : 1;
            PlateClassificationArithmetic.OutstandLimits(alpha > 0 ? alpha : 1, true, root, tip, eps,
                out p.First, out p.Second, out p.Third, out var psi, out _);
            if (p.ElasticCompression) p.Psi = psi;
            return p;
        }

        private static void Classify(Part p)
        {
            if (p.Alpha <= 0) { p.Class = 1; return; }
            p.Class = p.Ratio <= p.First ? 1 : p.Ratio <= p.Second ? 2 : !p.ElasticCompression ? 3 : p.Ratio <= p.Third ? 3 : 4;
            if (double.IsNaN(p.First) || double.IsNaN(p.Second) || p.ElasticCompression && double.IsNaN(p.Third))
                throw new ArithmeticException("Classification limits outside the finite range.");
        }

        private static string Describe(Part p)
        {
            var c = CultureInfo.InvariantCulture;
            return p.Id + " c/t=" + p.Ratio.ToString("0.###", c) + " alpha=" + p.Alpha.ToString("0.###", c)
                + (p.Psi.HasValue ? " psi=" + p.Psi.Value.ToString("0.###", c) : " no elastic compression") + " class " + p.Class;
        }
    }
}
