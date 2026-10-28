using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    internal static partial class IBeamSplicePlates
    {
        /// <summary>The additional flange net-plastic tension criterion for a
        /// category C splice: EN1993-1-1:2005 6.2.3(4), equation6.8. Explicit
        /// component invocation for ONE supplied ULS state; it neither selects
        /// a bolt category nor qualifies preloading, slip or the whole joint.</summary>
        public static ConnectionVerification<IBeamSplicePlateInput> VerifyFlangeNetPlastic(IBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p, issues, trace);
            if (issues.Count == 0)
            {
                double d0 = p.Bolts.FlangeBolt.HoleDiameter;
                int holes = p.Forces.FlangeBolts.TransverseRows;
                // Prepare restricts the flange to two transverse rows. A cut
                // crosses two holes, irrespective of the longitudinal row count.
                foreach (bool top in new[] { true, false })
                {
                    double signedAxial = top ? distribution.Resultants.TopFlangeAxial
                        : distribution.Resultants.BottomFlangeAxial;
                    double tension = Math.Max(0, signedAxial);
                    foreach (bool cover in new[] { false, true })
                    {
                        var plate = cover ? p.FlangeCover : Main(p, false);
                        string id = (top ? "TopFlange" : "BottomFlange") + (cover ? ".Cover" : ".Member");
                        double gross = plate.Width * plate.Thickness;
                        double net = (plate.Width - holes * d0) * plate.Thickness;
                        double resistance = SectionResistanceArithmetic.Normal(net, plate.Fy, p.Policy.Code.Plates.GammaM0);
                        if (!Finite(gross) || !Finite(net) || !Finite(resistance)
                            || gross <= 0 || net <= 0 || resistance <= 0)
                        {
                            issues.Add(id + ".NET_PLASTIC_RESISTANCE_OUTSIDE_NUMERIC_RANGE");
                            continue;
                        }
                        string reference = "EN1993-1-1:2005 6.2.3(4), equation6.8; EN1993-1-8:2005 3.4.1(1)c"
                            + (p.Policy.Code.IsNtc ? "; supplementary EC category-C criterion with NTC2018 gammaM0" : "");
                        checks.Add(new ConnectionCheck(id + ".NetPlasticTension", tension, resistance, reference));
                        trace.Add(new SectionResistanceValue(id + ".GrossArea", gross, "mm2"));
                        trace.Add(new SectionResistanceValue(id + ".NetArea", net, "mm2"));
                        trace.Add(new SectionResistanceValue(id + ".HolesAtSection", holes, "1"));
                        trace.Add(new SectionResistanceValue(id + ".HoleDiameter", d0, "mm"));
                        trace.Add(new SectionResistanceValue(id + ".YieldStrength", plate.Fy, "MPa"));
                        trace.Add(new SectionResistanceValue(id + ".SignedAxialForce", signedAxial, "N"));
                    }
                }
            }
            return Result(p, "FlangeNetPlastic", "CATEGORY C FLANGE NET-PLASTIC COMPONENT ONLY: tensile yielding of the "
                + "net section in both member flanges and their single external covers at ONE supplied ULS state. "
                + "No web net-plastic N-V-M assessment, compression resistance, block tearing, slip, preloading "
                + "or whole-joint qualification.", checks, trace, issues,
                "Explicit invocation checks the category C net-plastic requirement; it does not select category C or establish its preloading/surface/slip conditions. Use separate ULS slip evidence and assess every concomitant ULS case.",
                "Anet=(width-2*d0)*thickness at an aligned transverse section. Each hole is deducted in full even when filled; no stagger or hole omission. Longitudinal bolt-row count does not multiply the holes in one section.",
                "Resistance is Anet*fy/gammaM0, not 0.9*Anet*fu/gammaM2. Member-flange material is distinct from the web and cover material. NTC uses this supplementary EC criterion with its own gammaM0.",
                "Demand is max(0,Nflange) from the shared signed N-V-M distribution after action transport. One external cover carries the full flange force. Opposite member-end force signs are not a second tension/compression state.",
                "Zero demand on a compressed flange only means this tensile failure mode is inactive; the existing compression, instability and secondary-bending checks remain necessary. Both identical ends share the same net section and demand magnitude.");
        }
    }
}
