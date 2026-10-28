using System;
using System.Collections.Generic;
using System.Globalization;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    /// <summary>Automatic resolution of ONE compression mode (T or FT) the caller left without value
    /// or reasoned exclusion. Exactly one of Value, Exclusion and Missing is set.</summary>
    internal sealed class SteelTorsionalMode
    {
        internal SteelStabilityQuantity Quantity { get; }
        /// <summary>Gross elastic critical force, N.</summary>
        internal double? Value { get; }
        internal string Exclusion { get; }
        internal string Missing { get; }
        internal string Reference { get; }
        internal IReadOnlyList<SectionResistanceValue> Trace { get; }
        internal SteelTorsionalMode(SteelStabilityQuantity quantity, double? value, string exclusion, string missing,
            string reference, IEnumerable<SectionResistanceValue> trace)
        {
            Quantity = quantity; Value = value; Exclusion = exclusion; Missing = missing; Reference = reference;
            Trace = Array.AsReadOnly(new List<SectionResistanceValue>(trace ?? Array.Empty<SectionResistanceValue>()).ToArray());
        }
    }

    /// <summary>Torsional (T) and flexural-torsional (FT) compression modes when neither a value nor an
    /// exclusion is supplied; explicit caller input always wins and never reaches this class. Gross properties,
    /// N/mm/MPa. Doubly symmetric H: NcrT=(G It+pi^2 E Iw/LcrT^2)/i0^2, i0^2=(I1+I2)/A, LcrT explicit
    /// (Member.TorsionalBucklingLength) or the member length between twist restraints declared at both ends
    /// (free warping: an upper bound of the effective length); shear centre at centroid, so the FT determinant
    /// factorizes and FT is no distinct mode. Closed doubly symmetric hollow sections: length-independent lower
    /// bound NcrT>=G It/i0^2 (warping ignored) with lambda_T&lt;0.2 excludes T, FT is not distinct.
    /// Other families keep requiring explicit input.</summary>
    internal static class SteelTorsionalModes
    {
        internal static SteelTorsionalMode Automatic(SteelVerificationInput input, SteelStabilityQuantity mode)
        {
            if (mode != SteelStabilityQuantity.NcrT && mode != SteelStabilityQuantity.NcrFT) throw new ArgumentOutOfRangeException(nameof(mode));
            var s = input.Section.ReadOnlySection; var shape = s.SectionShape; bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            string label = mode + " or explicit mode exclusion";
            bool h = shape.GetType() == typeof(SectionH) && ((SectionH)shape).IsDoubleSymmetric && !SteelMethodSelector.ColdFormedOpen(s);
            bool hollow = (SteelSectionFamilies.Is(shape, SteelSectionKind.CircularTube) || SteelSectionFamilies.Is(shape, SteelSectionKind.RectangularTube)) && shape.IsDoubleSymmetric;
            double area = s.Area, i02 = (s.J11 + s.J22) / area;
            if (!(h || hollow) || !(area > 0) || !(i02 > 0) || double.IsInfinity(i02)
                || Math.Abs(s.ShearCenter.X - s.Centroid.X) + Math.Abs(s.ShearCenter.Y - s.Centroid.Y) > 1e-9 * Math.Sqrt(i02))
                return new SteelTorsionalMode(mode, null, null, label, null, null);
            double e = input.Section.ElasticModulus, g = e / (2 * (1 + input.Section.PoissonRatio)), it = s.Jt, iw = s.Jw;
            if (!(it > 0) || double.IsInfinity(it) || !(iw >= 0) || double.IsInfinity(iw) || !(g > 0))
                return new SteelTorsionalMode(mode, null, null, label + " (gross It/Iw not available)", null, null);
            var trace = new List<SectionResistanceValue>();
            void Add(string name, double value, string unit) { SteelInputValidation.Finite(value, name); trace.Add(new SectionResistanceValue(mode + "." + name, value, unit)); }
            if (mode == SteelStabilityQuantity.NcrFT)
            {
                // x0=y0=0: the coupled determinant is (N1-N)(N2-N)(NT-N), no distinct root.
                Add("AutomaticExclusion", 1, "1"); Add("ShearCentreOffset", 0, "mm");
                string reason = (ntc ? "NTC 2018 4.2.4.1.3.1 (Ncr of the appropriate mode)" : "EN 1993-1-1:2005 6.3.1.4")
                    + ": doubly symmetric " + (h ? "H" : "closed hollow section") + ", shear centre at centroid; the flexural-torsional determinant factorizes into Ncr1, Ncr2 and NcrT, no distinct NcrFT";
                return new SteelTorsionalMode(mode, null, reason, null, reason, trace);
            }
            Add("ShearModulus", g, "MPa"); Add("TorsionConstant", it, "mm4"); Add("PolarRadiusSquared", i02, "mm2");
            if (hollow)
            {
                double lower = g * it / i02, lambda = Math.Sqrt(area * input.Section.YieldStrength / lower);
                Add("LowerBound", lower, "N"); Add("LowerBoundSlenderness", lambda, "1");
                if (!(lambda < .2)) return new SteelTorsionalMode(mode, null, null, label + " (closed-section lower-bound lambda_T >= 0.2)", null, trace);
                Add("AutomaticExclusion", 1, "1");
                string reason = ntc
                    ? "NTC 2018 4.2.4.1.3.1: closed doubly symmetric hollow section, lower bound NcrT>=G It/i0^2 (warping ignored, any length) gives lambda_T<0.2, instability effects negligible"
                    : "EN 1993-1-1:2005 6.3.1.4(1) requires torsional modes for open cross-sections; closed doubly symmetric hollow section, lower bound NcrT>=G It/i0^2 (warping ignored, any length) gives lambda_T<0.2 (6.3.1.2(4))";
                return new SteelTorsionalMode(mode, null, reason + "; lambda_T=" + lambda.ToString("R", CultureInfo.InvariantCulture), null, reason, trace);
            }
            var m = input.Member; double? length = m.TorsionalBucklingLength;
            bool ends = m.StartRestraints?.Rotation3 == SteelRestraintState.Fixed && m.EndRestraints?.Rotation3 == SteelRestraintState.Fixed;
            if (!length.HasValue && ends) length = m.Length;
            if (!length.HasValue) return new SteelTorsionalMode(mode, null, null, "NcrT, Member.TorsionalBucklingLength (or twist restrained at both ends) or explicit mode exclusion", null, null);
            double ncr = ElasticStabilityArithmetic.Torsional(e, input.Section.PoissonRatio, Math.Sqrt(s.J11 / area), Math.Sqrt(s.J22 / area), 0, 0, it, iw, length.Value);
            SteelInputValidation.Positive(ncr, "NcrT");
            Add("Automatic", 1, "1"); Add("Length", length.Value, "mm"); Add("LengthFromEndTwistRestraints", m.TorsionalBucklingLength.HasValue ? 0 : 1, "1");
            Add("ElasticModulus", e, "MPa"); Add("WarpingConstant", iw, "mm6");
            return new SteelTorsionalMode(mode, ncr, null, null, (ntc ? "NTC 2018 4.2.4.1.3.1" : "EN 1993-1-1:2005 6.3.1.4")
                + ": automatic NcrT=(G It+pi^2 E Iw/LcrT^2)/i0^2, gross doubly symmetric H, LcrT=" + length.Value.ToString("R", CultureInfo.InvariantCulture)
                + " mm" + (m.TorsionalBucklingLength.HasValue ? " (Member.TorsionalBucklingLength)" : " (member length between declared end twist restraints, free warping)"), trace);
        }
    }
}
