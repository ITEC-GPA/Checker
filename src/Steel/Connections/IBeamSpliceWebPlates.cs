using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    internal static partial class IBeamSplicePlates
    {
        /// <summary>SCI P398 section4 Steps5-6, pp58-59: web-cover cross-section
        /// shear, axial and elastic bending interaction on BOTH sides; beam-web
        /// gross/net shear. Does not qualify web block tearing, local buckling,
        /// section classification or load introduction. A passed component is
        /// not the complete resistance of the web connection.</summary>
        public static ConnectionVerification<IBeamSplicePlateInput> VerifyWebSections(IBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>();
            var distribution = Prepare(p, issues, trace);
            if (issues.Count == 0)
            {
                var plate = p.WebCover; var section = p.Forces.Section; var code = p.Policy.Code;
                int holes = p.Forces.WebBolts.TransverseRows; double d0 = p.Bolts.WebBolt.HoleDiameter;
                double area = plate.Width * plate.Thickness, net = (plate.Width - holes * d0) * plate.Thickness;
                double grossV = PlasticShear.Resistance(area, plate.Fy, 1.27, code.Plates.GammaM0);
                double netV = PlasticShear.Resistance(net, plate.Fu, code.Plates.GammaM2);
                double shear = Math.Min(grossV, netV);
                double grossN = SectionResistanceArithmetic.Normal(area, plate.Fy, code.Plates.GammaM0);
                double netTension = SectionResistanceArithmetic.NetTension(net, plate.Fu, code.Plates.GammaM2);
                double modulus = plate.Thickness * plate.Width * plate.Width / 6;
                double elasticM = SectionResistanceArithmetic.Normal(modulus, plate.Fy, code.Plates.GammaM0);
                // EN1993-1-1 6.2.6 rolled-I formula, eta=1 conservative policy.
                // Root radius belongs to the supplied section: no substitution of
                // the inconsistent radius in the rounded P398 numerical example.
                double av = Math.Max(section.Area - 2 * section.Width * section.FlangeThickness
                    + (section.WebThickness + 2 * section.RootRadius) * section.FlangeThickness,
                    section.ClearWeb * section.WebThickness);
                double avNet = av - holes * d0 * section.WebThickness;
                double memberGrossV = SectionResistanceArithmetic.Shear(av, section.Fy, code.Plates.GammaM0);
                double memberNetV = PlasticShear.Resistance(avNet, section.Fu, code.Plates.GammaM2);
                if (new[] { area, net, grossV, netV, grossN, netTension, modulus, elasticM, av, avNet, memberGrossV, memberNetV }
                    .Any(v => !Finite(v) || v <= 0)) issues.Add("WEB_SECTION_PROPERTIES_OUTSIDE_NUMERIC_RANGE");
                if (issues.Count == 0)
                {
                    string reference = "SCI P398 section4 Steps5-6 pp58-59; EN1993-1-1:2005 6.2.6,6.2.8-6.2.10"
                        + (code.IsNtc ? "; supplementary plate model with NTC2018 partial factors" : "");
                    trace.Add(new SectionResistanceValue("Web.EachCover.GrossArea", area, "mm2"));
                    trace.Add(new SectionResistanceValue("Web.EachCover.NetArea", net, "mm2"));
                    trace.Add(new SectionResistanceValue("Web.EachCover.ElasticModulus", modulus, "mm3"));
                    trace.Add(new SectionResistanceValue("Web.Member.ShearArea", av, "mm2"));
                    trace.Add(new SectionResistanceValue("Web.Member.NetShearArea", avNet, "mm2"));
                    double n = distribution.Resultants.WebAxial / 2, v = Math.Abs(distribution.Resultants.WebShear) / 2;
                    double nr = n >= 0 ? Math.Min(grossN, netTension) : grossN;
                    double shearRatio = v / shear, axialRatio = Math.Abs(n) / nr;
                    if (!Finite(shearRatio) || !Finite(axialRatio)) issues.Add("WEB_INTERACTION_OUTSIDE_NUMERIC_RANGE");
                    else
                    {
                        // SCI threshold is HALF of Vrd (general rule p59), not Vrd
                        // as erroneously printed in the D.1 narrative on p140.
                        double rho = shearRatio <= .5 ? 0 : shearRatio >= 1 ? 1 : Math.Pow(2 * shearRatio - 1, 2);
                        double reducedM = (1 - rho) * elasticM;
                        // Equivalent form N/Nrd + M/Mrd<=1 avoids division by zero
                        // when high shear or axial force exhausts the moment resistance.
                        double remainingM = Math.Max(0, 1 - axialRatio) * reducedM;
                        foreach (var group in distribution.BoltGroups.Where(g => g.Part == IBeamSplicePart.Web))
                        {
                            string id = group.Side + ".Web.EachCover";
                            double m = group.Distribution.ActionsAtCentroid.Mz / 2;
                            checks.Add(new ConnectionCheck(id + ".GrossShear", v, grossV, reference));
                            checks.Add(new ConnectionCheck(id + ".NetShear", v, netV, reference));
                            checks.Add(new ConnectionCheck(id + ".GrossTension", Math.Max(0, n), grossN, reference));
                            checks.Add(new ConnectionCheck(id + ".NetTension", Math.Max(0, n), netTension, reference));
                            checks.Add(new ConnectionCheck(id + ".GrossCompression", Math.Max(0, -n), grossN, reference));
                            checks.Add(new ConnectionCheck(id + ".ShearReducedBending", Math.Abs(m), reducedM, reference, "Nmm"));
                            checks.Add(new ConnectionCheck(id + ".AxialMomentInteraction", Math.Abs(m), remainingM, reference, "Nmm"));
                            trace.Add(new SectionResistanceValue(id + ".AxialForce", n, "N"));
                            trace.Add(new SectionResistanceValue(id + ".ShearForce", v, "N"));
                            trace.Add(new SectionResistanceValue(id + ".SignedMomentAtBoltCentroid", m, "Nmm"));
                            trace.Add(new SectionResistanceValue(id + ".ShearRatio", shearRatio, "1"));
                            trace.Add(new SectionResistanceValue(id + ".Rho", rho, "1"));
                            trace.Add(new SectionResistanceValue(id + ".AxialRatio", axialRatio, "1"));
                        }
                        checks.Add(new ConnectionCheck("Web.Member.GrossShear", 2 * v, memberGrossV, reference));
                        checks.Add(new ConnectionCheck("Web.Member.NetShear", 2 * v, memberNetV, reference));
                    }
                }
            }
            return Result(p, "WebSections", "WEB SECTION COMPONENTS ONLY: gross/net shear of two equal covers and the unnotched beam web; "
                + "cover axial resistance, elastic bending reduced for shear and linear N-M interaction on both sides. "
                + "No web block tearing/local buckling/classification, secondary bending, member N-M stability or whole-joint qualification.", checks, trace, issues,
                "SCI P398 pp58-59: each cover takes half N,V and its own side's signed group-centroid moment. Web moment is not shed to the flanges; opposite sides do not share the same |V*e|.",
                "Cover gross shear uses 1.27*sqrt(3); net shear uses fu/sqrt(3). rho=0 below Vrd/2, (2V/Vrd-1)^2 above, with no negative moment resistance after shear failure.",
                "Cover bending is the SCI gross elastic section model. Gross/net axial and shear checks do not independently qualify fracture paths or local stability under combined N-V-M.",
                "Interaction is evaluated as |M| <= max(0,1-|N|/Nrd)*(1-rho)*Mel,Rd, with independent axial/shear checks. No NaN and no artificial residual capacity at exhausted resistance.",
                "The unnotched member-web shear area uses eta=1 and the actual root radius. Full member resistance and section classification remain separate.");
        }
    }
}
