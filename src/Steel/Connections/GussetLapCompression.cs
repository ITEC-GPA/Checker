using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum GussetLapEndRestraint { Unspecified, FixedAgainstOutOfPlaneRotation, Pinned }
    public enum GussetLapProportion { Unspecified, Compact, Elongated }

    /// <summary>SCI P358:2014 8.5, one-edge perpendicular support, two rectangular plates
    /// in direct single-lap contact and a rectangular group of four bolts (two rows, two columns).
    /// Dimensions mm, stresses MPa. Free lengths run from the real fixed yield lines to the
    /// nearest bolt row; Pitch runs between the rows, not between the plate ends.
    /// Local XY is the plate plane, +X runs from support towards brace, +Z is normal.
    /// Actions are one concomitant state, referred to the tab yield-line centroid as origin;
    /// compression is Fx negative. Application offsets are transported before validation.
    /// Physical end fixity/compactness are explicit external assessments, not inferred from labels.
    /// This component computes plate sway resistance only, not bolt or weld resistance.</summary>
    public sealed class GussetLapCompressionInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate Tab { get; }
        public ConnectionPlate Gusset { get; }
        public PlanarConnectionActions Actions { get; }
        public double TabFreeLength { get; }
        public double GussetFreeLength { get; }
        public double Pitch { get; }
        public double Gauge { get; }
        public GussetLapEndRestraint TabRestraint { get; }
        public GussetLapEndRestraint GussetRestraint { get; }
        public GussetLapProportion Proportion { get; }
        public string RestraintAssessmentSource { get; }
        public string CompactDetailSource { get; }

        public GussetLapCompressionInput(ConnectionDesignCode code, ConnectionPlate tab,
            ConnectionPlate gusset, PlanarConnectionActions actions, double tabFreeLength,
            double gussetFreeLength, double pitch, double gauge,
            GussetLapEndRestraint tabRestraint, GussetLapEndRestraint gussetRestraint,
            GussetLapProportion proportion, string restraintAssessmentSource, string compactDetailSource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Tab = tab ?? throw new ArgumentNullException(nameof(tab));
            Gusset = gusset ?? throw new ArgumentNullException(nameof(gusset));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ConnectionValidation.Positive(tabFreeLength, nameof(tabFreeLength));
            ConnectionValidation.Positive(gussetFreeLength, nameof(gussetFreeLength));
            ConnectionValidation.Positive(pitch, nameof(pitch));
            ConnectionValidation.Positive(gauge, nameof(gauge));
            if (!Enum.IsDefined(typeof(GussetLapEndRestraint), tabRestraint)) throw new ArgumentOutOfRangeException(nameof(tabRestraint));
            if (!Enum.IsDefined(typeof(GussetLapEndRestraint), gussetRestraint)) throw new ArgumentOutOfRangeException(nameof(gussetRestraint));
            if (!Enum.IsDefined(typeof(GussetLapProportion), proportion)) throw new ArgumentOutOfRangeException(nameof(proportion));
            TabFreeLength = tabFreeLength; GussetFreeLength = gussetFreeLength;
            Pitch = pitch; Gauge = gauge; TabRestraint = tabRestraint; GussetRestraint = gussetRestraint;
            Proportion = proportion; RestraintAssessmentSource = restraintAssessmentSource;
            CompactDetailSource = compactDetailSource;
        }
    }

    /// <summary>SCI plate sway mechanism with actual end fixity and inherent lap eccentricity.
    /// A supplementary design model for both EC3 and NTC2018, never an invented code equation
    /// or an Euler-column check with an assumed effective length. The partial-plastic modulus
    /// is wt^2/5, exactly as in P358 step 5.</summary>
    public static class GussetLapCompression
    {
        public static ConnectionVerification<GussetLapCompressionInput> Verify(GussetLapCompressionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>();
            var a = p.Actions; var tab = p.Tab; var gusset = p.Gusset;
            double mx = a.Mx + a.ApplicationY * a.Fz - a.ApplicationZ * a.Fy;
            double my = a.My + a.ApplicationZ * a.Fx - a.ApplicationX * a.Fz;
            double mz = a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx;
            Require(a.Fx <= 0, "CompressionOnly");
            Require(a.Fy == 0 && a.Fz == 0 && mx == 0 && my == 0 && mz == 0,
                "PureAxialActionAtTabYieldLineRequired");
            Require(tab.Thickness <= 20 && gusset.Thickness <= 20, "P358MaximumPlateThickness20mm");
            // S355 is an intentional implementation boundary, not a claimed P358 limit.
            Require(tab.Fy <= 355 && gusset.Fy <= 355, "ImplementationCarbonSteelUpToS355Only");
            Require(p.Gauge < Math.Min(tab.Width, gusset.Width), "BoltCentresOutsidePlateWidths");
            Require(p.TabRestraint == GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation && p.GussetRestraint == GussetLapEndRestraint.FixedAgainstOutOfPlaneRotation,
                "BothYieldLinesRequireRealFixedEndRestraint");
            Require(!string.IsNullOrWhiteSpace(p.RestraintAssessmentSource), "RestraintAssessmentSourceRequired");
            Require(p.Proportion == GussetLapProportion.Compact, "CompactConnectionRequired");
            Require(!string.IsNullOrWhiteSpace(p.CompactDetailSource), "CompactDetailSourceRequired");

            double wt = Math.Min(tab.Width, 20 * tab.Thickness);
            double wg = Math.Min(gusset.Width, 20 * gusset.Thickness);
            double it = wt * tab.Thickness * tab.Thickness * tab.Thickness / 12;
            double ig = wg * gusset.Thickness * gusset.Thickness * gusset.Thickness / 12;
            double thicknessSum = tab.Thickness + gusset.Thickness;
            double ilap = .5 * (wt + wg) * thicknessSum * thicknessSum * thicknessSum / 12;
            // E is common to both steel plates and cancels in the stiffness ratios.
            double ft = p.TabFreeLength / it + p.Pitch / (2 * ilap);
            double fg = p.GussetFreeLength / ig + p.Pitch / (2 * ilap);
            double fsum = ft + fg;
            double mut = fg / fsum;
            double mug = ft / fsum;
            double e0 = .5 * thicknessSum;
            const double kamp = 1.2; // One-edge support; the two-edge case-A factor 1.05 does not apply.
            double ea = kamp * e0;
            double compression = -a.Fx;
            double mt = compression * ea * mut;
            double mg = compression * ea * mug;
            double gamma = p.Code.Plates.GammaM0;
            double nt = wt * tab.Fy * tab.Thickness * tab.Thickness / ((5 * ea * mut + tab.Thickness) * gamma);
            double ng = wg * gusset.Fy * gusset.Thickness * gusset.Thickness / ((5 * ea * mug + gusset.Thickness) * gamma);
            Value("TabEffectiveYieldLineWidth", wt, "mm"); Value("GussetEffectiveYieldLineWidth", wg, "mm");
            Value("TabInertia", it, "mm4"); Value("GussetInertia", ig, "mm4"); Value("LapInertia", ilap, "mm4");
            Value("TabReducedFlexibility", ft, "1/mm3"); Value("GussetReducedFlexibility", fg, "1/mm3");
            Value("TabMomentFraction", mut, "1"); Value("GussetMomentFraction", mug, "1");
            Value("InitialLapEccentricity", e0, "mm"); Value("MomentAmplification", kamp, "1");
            Value("AmplifiedLapEccentricity", ea, "mm"); Value("TabMomentMagnitude", mt, "Nmm");
            Value("GussetMomentMagnitude", mg, "Nmm"); Value("GammaM0", gamma, "1");
            Value("TabGrossYieldResistance", wt * tab.Thickness * tab.Fy / gamma, "N");
            Value("GussetGrossYieldResistance", wg * gusset.Thickness * gusset.Fy / gamma, "N");
            Value("TabPartialPlasticMomentResistance", wt * tab.Thickness * tab.Thickness * tab.Fy / (5 * gamma), "Nmm");
            Value("GussetPartialPlasticMomentResistance", wg * gusset.Thickness * gusset.Thickness * gusset.Fy / (5 * gamma), "Nmm");
            Require(Finite(it) && it > 0 && Finite(ig) && ig > 0 && Finite(ilap) && ilap > 0
                && Finite(ft) && ft > 0 && Finite(fg) && fg > 0 && Finite(fsum) && fsum > 0
                && Finite(mut) && mut > 0 && mut < 1 && Finite(mug) && mug > 0 && mug < 1
                && Finite(nt) && nt > 0 && Finite(ng) && ng > 0, "ResistanceOutsideNumericRange");
            if (issues.Count == 0)
            {
                string reference = "SCI P358:2014 8.5, pp261-263, steps1-5; supplementary model; "
                    + (p.Code.IsNtc ? "NTC2018 gammaM0" : "explicit EC3 gammaM0");
                checks.Add(new ConnectionCheck("Tab.CompressionSwayInteraction", compression, nt, reference));
                checks.Add(new ConnectionCheck("Gusset.CompressionSwayInteraction", compression, ng, reference));
            }
            return new ConnectionVerification<GussetLapCompressionInput>(p, p.Code,
                "Steel.Connection.Gusset.SingleLapCompression.PlateSway",
                "Plate sway mechanism only: rectangular single-lap tab and one-edge gusset, perpendicular support, "
                + "four bolts in a rectangular group, direct plate contact, compact detail and two real fixed ends. "
                + "Not a whole-joint or whole-member assessment; bolt/hole/contact/weld/support capacities are outside this scope.",
                checks, trace, issues, new[]
                {
                    "SCI P358:2014 8.5 is a supplementary joint model; its expressions are not attributed to EN1993-1-8 or NTC2018.",
                    "Both end restraints must develop the yield-line moments and axial forces; the supplied assessment sources record external evidence, not automatic certification.",
                    "The support is perpendicular to the brace (90 degrees) and restrains one gusset edge. No two-edge, angled, fork/double-cover, packing, staggered-bolt or elongated detail is represented.",
                    "Compactness relative to the real brace and support must be established outside this component. P358 supplies no universal numerical length ratio; none is invented here.",
                    "Tab and gusset are rectangular carbon-steel plates of thickness at most 20 mm; this first implementation is restricted to fy <= 355 MPa. Both use the same elastic modulus.",
                    "The inherent out-of-plane eccentricity is half the sum of plate thicknesses and is amplified by 1.2. No additional applied bending or shear is silently discarded.",
                    "Full-strength attachments and adequate supporting members, bolt shear/bearing/detailing, connected-member stability, other plate failure modes and serviceability require separate checks."
                });

            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            {
                if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit));
                else issues.Add(name + ":OutsideNumericRange");
            }
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
