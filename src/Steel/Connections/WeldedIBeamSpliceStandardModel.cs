using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Applicability of the conventional balanced double-cover model. This is
    /// deliberately narrower than the equilibrium calculator: identical cover thickness
    /// and area on the two flange faces, identical materials, centred inner-strip bolt
    /// groups, no packing, contact bearing or externally applied bolt-axis load.
    /// Unequal TOP/BOTTOM parent flanges are permitted. Not a stiffness calculation.</summary>
    public static class WeldedIBeamSpliceStandardModel
    {
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var b = p.Plates.Bolts; var d = b.Detail;
            var f = WeldedIBeamSpliceForces.Calculate(d);
            var issues = new List<string>(f.Diagnostics);
            var checks = new List<ConnectionCheck>();
            Flange(d.Top, "Top"); Flange(d.Bottom, "Bottom");
            if (d.WebBolts.LongitudinalRows > 20 || d.WebBolts.TransverseRows > 16)
                issues.Add("STANDARD_WEB_GRID_LIMIT_20_BY_16");
            foreach (var plate in new[] { d.Section.TopFlange, d.Section.Web, d.Section.BottomFlange,
                d.Top.Outer, d.Top.EachInner, d.Bottom.Outer, d.Bottom.EachInner, d.EachWebCover })
                if (plate.Fy > 460) issues.Add("STANDARD_CARBON_STEEL_FY_UP_TO_460_REQUIRED");
            if (f.IsBalanced)
                checks.Add(new ConnectionCheck("ForceModel.Equilibrium", 0, 1,
                    "EN1993-1-8:2005 2.5,3.12; SCI P357 section12; balanced double-cover idealisation", "1"));
            return new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, b.Code,
                "Steel.Connection.WeldedIBeamSplice.StandardModel",
                "Applicability of the balanced, aligned double-cover static force model; not resistance or rotational stiffness.",
                checks, f.Trace, issues.Distinct(), new[] {
                    "SCI P357 example1 section12 and SCI P398 section4: ordinary double-cover splice with in-plane bolt shear. Same thickness and total axial area on opposite flange faces are a conservative library restriction, not a numerical tolerance or an EC requirement.",
                    "Exact parent-plate integrals and the flange self-bending couple are retained. Equal cover areas and offsets make the force split compatible with an axial-plus-linear through-depth strain field; parent and cover strains need not be equal across a bolt load-introduction region.",
                    "Contact-face transport couples alone are not an externally applied bolt tension. This conventional symmetric double-lap model does not solve contact, prying, packing or plate lifting. Those details must use another method.",
                    "All covers are continuous, flat and in contact with the parent. No packings, stiffener cut-outs, notches, weld reinforcement, credits for butt contact, section changes or out-of-plane actions are modelled.",
                    "The structural analysis must justify continuity, restraints and second-order actions separately. This assessment supplies no rotational stiffness or fatigue qualification." });

            void Flange(SpliceFlangeCoverDetail x, string name)
            {
                if (x.Outer.Thickness != x.EachInner.Thickness || x.Outer.Width != 2 * x.EachInner.Width)
                    issues.Add(name + ".BALANCED_FACE_THICKNESS_AND_AREA_REQUIRED");
                if (x.Outer.Fy != x.EachInner.Fy || x.Outer.Fu != x.EachInner.Fu)
                    issues.Add(name + ".IDENTICAL_FACE_MATERIAL_REQUIRED");
                if (x.Bolts.LongitudinalRows > 20 || x.Bolts.TransverseRows > 16)
                    issues.Add(name + ".STANDARD_FLANGE_GRID_LIMIT_20_BY_16");
                var ys = x.Bolts.Points(SpliceSide.Left).Where(q => q.Y > 0).Select(q => q.Y).ToArray();
                if (ys.Length == 0 || !Equal(ys.Average(), (x.InnerGap + x.EachInner.Width) / 2))
                    issues.Add(name + ".CENTRED_INNER_STRIP_BOLT_GROUP_REQUIRED");
            }
        }
        internal static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-12 * Math.Max(Math.Abs(a), Math.Abs(b));
    }
}
