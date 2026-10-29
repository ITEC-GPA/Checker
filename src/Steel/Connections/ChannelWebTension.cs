using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Resistance model of a channel connected by its web only. There is no default:
    /// EN1993-1-8 3.10.3 covers single angles, not channels, and 3.10.4 lug angles are a
    /// different physical detail.</summary>
    public enum ChannelWebTensionMethod
    {
        Unspecified,
        /// <summary>Lower-bound field: the web strip h*tw carries the whole channel force on its
        /// mid-plane along the member, the outstanding flanges carry nothing. Gross yielding and
        /// 0.9 Anet fu/gammaM2 of the web strip (EN1993-1-1 6.2.3), web block tearing (EN1993-1-8
        /// 3.10.2). No shear-lag coefficient is needed and none is imported.</summary>
        ConnectedWebOnly,
        /// <summary>Declared alternative: AISC 360-16 D3/Table D3.1 case2 U=1-xbar/l, without the
        /// connected-element relaxation, applied as 0.9 U An fu/gammaM2 with the gross
        /// yielding of the whole channel; web block tearing as EN1993-1-8 3.10.2.</summary>
        AiscShearLagAlternative
    }

    /// <summary>One hot-finished SectionC with equal parallel flanges (no taper/UPN, not cold
    /// formed), in axial tension, connected through its WEB only by Lines x BoltsPerLine
    /// bolts in normal round holes. Lines are parallel to the member and symmetric about the
    /// channel mid-height, LineSpacing apart; Pitch is along the member, EndDistance from the
    /// end bolt to the channel end. For 2C back to back the section is the identical pair and
    /// TensionPerChannel is the explicit share of ONE channel, each through bolt then having one
    /// shear plane per channel. N, mm, MPa. Gusset, collector and support are not included.</summary>
    public sealed class ChannelWebTensionInput
    {
        public ConnectionDesignCode Code { get; }
        public SteelSectionInput Section { get; }
        public double TensionPerChannel { get; }
        public ChannelWebTensionMethod Method { get; }
        public ConnectionBolt Bolt { get; }
        public int Lines { get; }
        public double LineSpacing { get; }
        public int BoltsPerLine { get; }
        public double Pitch { get; }
        public double EndDistance { get; }
        public double HardwareDiameter { get; }
        public string GeometrySource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        /// <summary>Project acceptance of the non-EN AISC alternative; required only for it.</summary>
        public string AlternativeMethodSource { get; }
        public ChannelWebTensionInput(ConnectionDesignCode code, SteelSectionInput section, double tensionPerChannel,
            ChannelWebTensionMethod method, ConnectionBolt bolt, int lines, double lineSpacing, int boltsPerLine, double pitch,
            double endDistance, double hardwareDiameter, string geometrySource, string holeClearanceServiceabilitySource = null,
            string alternativeMethodSource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            if (!Enum.IsDefined(typeof(ChannelWebTensionMethod), method)) throw new ArgumentOutOfRangeException(nameof(method));
            ConnectionValidation.Nonnegative(tensionPerChannel, nameof(tensionPerChannel));
            if (lines < 1 || lines > 10) throw new ArgumentOutOfRangeException(nameof(lines));
            if (boltsPerLine < 1 || boltsPerLine > 100) throw new ArgumentOutOfRangeException(nameof(boltsPerLine));
            ConnectionValidation.Nonnegative(lineSpacing, nameof(lineSpacing));
            ConnectionValidation.Positive(pitch, nameof(pitch));
            ConnectionValidation.Positive(endDistance, nameof(endDistance));
            ConnectionValidation.Positive(hardwareDiameter, nameof(hardwareDiameter));
            TensionPerChannel = tensionPerChannel; Method = method; Lines = lines; LineSpacing = lineSpacing;
            BoltsPerLine = boltsPerLine; Pitch = pitch; EndDistance = endDistance; HardwareDiameter = hardwareDiameter;
            GeometrySource = geometrySource; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
            AlternativeMethodSource = alternativeMethodSource;
        }
    }

    /// <summary>Tension end of a channel connected by the web only: section resistance by the
    /// explicitly chosen method, web block tearing, bolt shear and web bearing. The connected
    /// end only: gusset/collector, support and the remote member remain separate checks.</summary>
    public static class ChannelWebTension
    {
        public const string MethodId = "Steel.Connection.Channel.WebOnlyTension";
        public static ConnectionVerification<ChannelWebTensionInput> Verify(ChannelWebTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var property = p.Section.ReadOnlySection; var shape = property.SectionShape; var code = p.Code; var b = p.Bolt;
            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + ":OutsideNumericRange"); }
            void Check(string id, double demand, double resistance, string reference)
            {
                if (!Finite(resistance) || resistance <= 0 || !Finite(demand)) issues.Add(id + ":OutsideNumericRange");
                else checks.Add(new ConnectionCheck(id, demand, resistance, reference));
            }
            // Exact SectionC: SectionCTaperFlange derives from it but is a different field.
            SectionC c = shape != null && shape.GetType() == typeof(SectionC) ? (SectionC)shape : null;
            if (shape is SectionBuiltUp pair && pair.Parts.Count == 2)
            {
                var first = pair.Parts[0].Section; var second = pair.Parts[1].Section;
                bool identical = first.GetType() == typeof(SectionC) && second is SectionC s && s.GetType() == typeof(SectionC)
                    && ((SectionC)first).Equals(s);
                var g = identical ? GPC.Checkers.Steel.ModelAdapters.ModelBuiltUpGeometry.Create(pair) : null;
                double gap = Math.Abs(pair.Parts[0].X - pair.Parts[1].X);
                Require(identical && pair.Parts[0].MirrorX != pair.Parts[1].MirrorX && !(g is null) && g.CentroidSpacing > gap,
                    "IDENTICAL_BACK_TO_BACK_CHANNEL_PAIR_REQUIRED");
                if (identical) c = (SectionC)first;
            }
            // SectionC overloads ==; compare references only.
            Require(!(c is null), "EXACT_PARALLEL_FLANGE_SECTIONC_REQUIRED");
            Require(p.Method != ChannelWebTensionMethod.Unspecified, "EXPLICIT_METHOD_CHOICE_REQUIRED");
            Require(p.Method != ChannelWebTensionMethod.AiscShearLagAlternative || !string.IsNullOrWhiteSpace(p.AlternativeMethodSource),
                "ALTERNATIVE_NON_EN_METHOD_ACCEPTANCE_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.GeometrySource), "ACTUAL_GEOMETRY_AND_HARDWARE_SOURCE_REQUIRED");
            Require(property.FormedType == Section.FormedTypes.HotFinished, "HOT_FINISHED_CHANNEL_REQUIRED");
            Require(p.Section.YieldStrength <= 460, "CARBON_STEEL_UP_TO_S460_ONLY");
            BoltComponentRules.RoundHole(b, issues);
            if (c is null) return Result();
            Require(c.LengthTop == c.LengthBottom && c.ThicknessTop == c.ThicknessBottom, "EQUAL_FLANGES_REQUIRED");
            double h = c.Height, tw = c.ThicknessWeb, tf = c.ThicknessBottom, d0 = b.HoleDiameter, half = p.HardwareDiameter / 2;
            int r = p.Lines, n = p.BoltsPerLine; double p1 = p.Pitch, p2 = p.LineSpacing, e1 = p.EndDistance;
            double groupHeight = (r - 1) * p2, lowLine = h / 2 - groupHeight / 2, edge = lowLine;
            double flatLow = tf + c.R1, flatHigh = h - c.ThicknessTop - c.R1, jointLength = (n - 1) * p1;
            Require(n >= 2, "AT_LEAST_TWO_BOLTS_PER_LINE_REQUIRED");
            Require(r == 1 || p2 >= Math.Max(2.4 * d0, p.HardwareDiameter), "MINIMUM_LINE_SPACING_OR_HARDWARE_CLEARANCE");
            Require(r > 1 || p2 == 0, "LINE_SPACING_ONLY_FOR_SEVERAL_LINES");
            Require(p1 >= Math.Max(2.2 * d0, p.HardwareDiameter), "MINIMUM_PITCH_OR_HARDWARE_CLEARANCE");
            Require(p1 <= Math.Min(14 * tw, 200) && (r == 1 || p2 <= Math.Min(14 * tw, 200)), "TABLE3_3_MAXIMUM_SPACING");
            Require(e1 >= Math.Max(1.2 * d0, half), "MINIMUM_END_OR_HARDWARE_DISTANCE");
            Require(lowLine - half >= flatLow && lowLine + groupHeight + half <= flatHigh,
                "BOLTS_AND_HARDWARE_MUST_LIE_ON_THE_FLAT_WEB");
            double webArea = h * tw, webNet = (h - r * d0) * tw, grossArea = c.Area, net = grossArea - r * d0 * tw;
            double xbar = c.Centroid.X;
            Value("Channel.Height", h, "mm"); Value("Channel.WebThickness", tw, "mm"); Value("Channel.GrossArea", grossArea, "mm2");
            Value("Channel.CentroidFromWebBack", xbar, "mm"); Value("Web.StripArea", webArea, "mm2"); Value("Web.StripNetArea", webNet, "mm2");
            Value("Channel.NetArea", net, "mm2"); Value("Bolts.JointLength", jointLength, "mm"); Value("Bolts.OuterLineEdge", edge, "mm");
            Value("Bolts.FlatWebLow", flatLow, "mm"); Value("Bolts.FlatWebHigh", flatHigh, "mm");
            Require(webNet > 0 && net > 0, "NONPOSITIVE_NET_AREA");
            if (issues.Count != 0) return Result();

            double gm0 = code.Plates.GammaM0, gm2 = code.Plates.GammaM2, fy = p.Section.YieldStrength, fu = p.Section.UltimateStrength;
            double N = p.TensionPerChannel;
            string sectionRef = code.IsNtc ? "NTC2018 4.2.4.1.2.1; supplementary " : "EN1993-1-1:2005 6.2.3; ";
            if (p.Method == ChannelWebTensionMethod.ConnectedWebOnly)
            {
                Check("Web.GrossYielding", N, webArea * fy / gm0, sectionRef + "web strip alone carries the channel force (statically admissible field, flanges unstressed)");
                Check("Web.NetRupture", N, .9 * webNet * fu / gm2, sectionRef + "0.9 Anet fu/gammaM2 of the web strip; no shear-lag credit to the flanges");
            }
            else
            {
                // The D3 relaxation "U need not be less than Aconnected/Ag" is deliberately not used.
                double u = 1 - xbar / jointLength;
                Value("Aisc.U", u, "1"); Value("Aisc.ConnectedWebRatioNotUsed", webArea / grossArea, "1");
                Value("Aisc.EffectiveNetArea", u * net, "mm2");
                if (!(u > 0)) { issues.Add("AISC_SHEAR_LAG_FACTOR_NOT_POSITIVE_FOR_THIS_JOINT_LENGTH"); return Result(); }
                Check("Channel.GrossYielding", N, grossArea * fy / gm0, sectionRef + "whole channel");
                Check("Channel.EffectiveNetRupture", N, .9 * u * net * fu / gm2,
                    "AISC 360-16 D3, Table D3.1 case2 U=1-xbar/l, declared alternative without the connected-element relaxation; with EN 0.9 fu/gammaM2");
            }
            // EN1993-1-8 3.10.2 in the web strip: interior block between the outer lines
            // (concentric, all bolts) and one-sided blocks from each outer line to the strip edge
            // (eccentric, the outer line's share; the whole force for a single line).
            double shearPlane = (e1 + jointLength - (n - .5) * d0) * tw;
            Value("Block.ShearPlaneArea", shearPlane, "mm2");
            string blockRef = "EN1993-1-8:2005 3.10.2 in the web strip; strip edges at the flange outer faces (flange material neglected)";
            if (r > 1)
            {
                double interior = (groupHeight - (r - 1) * d0) * tw;
                Value("Block.Interior.Ant", interior, "mm2");
                Check("Block.Interior", N, interior * fu / gm2 + PlasticShear.Resistance(2 * shearPlane, fy, gm0), blockRef + "(2), concentric");
            }
            double edgeTension = (edge - d0 / 2) * tw;
            Value("Block.Edge.Ant", edgeTension, "mm2");
            double edgeResistance = .5 * edgeTension * fu / gm2 + PlasticShear.Resistance(shearPlane, fy, gm0);
            foreach (string side in new[] { "Bottom", "Top" })
                Check("Block.Edge" + side, r == 1 ? N : N / r, edgeResistance, blockRef + "(3), eccentric one-sided block");
            // Bolts: concentric equal shares, one shear plane per channel, long-joint factor.
            double perBolt = N / (r * n);
            Value("Bolts.ForcePerBoltPerPlane", perBolt, "N");
            var strength = BoltStrength.Verify(new BoltStrengthInput(code, b, perBolt, 0, 1, jointLength, 0, p.HoleClearanceServiceabilitySource));
            foreach (var issue in strength.Diagnostics) issues.Add("Bolt:" + issue);
            foreach (var check in strength.Checks.Where(x => x.Id == "Bolt.Shear")) checks.Add(check);
            foreach (var v in strength.Trace) trace.Add(new SectionResistanceValue("Bolt." + v.Name, v.Value, v.Unit));
            var web = new ConnectionPlate(h, tw, fy, fu);
            foreach (bool endBolt in new[] { true, false })
            foreach (bool edgeBolt in r > 2 ? new[] { true, false } : new[] { true })
            {
                var bearing = BoltBearing.Verify(new BoltBearingInput(code, b, web, perBolt, e1, edge, p1, r > 1 ? (double?)p2 : null,
                    endBolt, edgeBolt, p.HoleClearanceServiceabilitySource));
                string id = "Web.Bearing." + (endBolt ? "EndBolt" : "InnerBolt") + "." + (edgeBolt ? "OuterLine" : "InnerLine");
                foreach (var issue in bearing.Diagnostics) issues.Add(id + ":" + issue);
                foreach (var check in bearing.Checks) checks.Add(check.WithId(id));
            }
            return Result();

            ConnectionVerification<ChannelWebTensionInput> Result() => new ConnectionVerification<ChannelWebTensionInput>(p, code,
                MethodId + "." + p.Method, "Tension end of a hot-finished parallel-flange channel connected through its web only, by an explicitly chosen method: "
                + "section resistance, web block tearing, bolt shear and web bearing. No gusset/collector, support, compression, remote member or whole-joint result.",
                checks, trace, issues, new[] {
                    "EN1993-1-8 2.7(1)/3.10.3(1) require the eccentricity of the non-symmetrically connected channel to be considered; 3.10.3(2) gives coefficients for single angles only and is not extended to channels.",
                    "ConnectedWebOnly: the web strip h*tw alone carries the channel force on its mid-plane over the whole member length with zero flange stress. With the bolt group centred on the mid-height this field is in equilibrium everywhere, so its gross-yield and net-rupture limits are a lower bound; no shear-lag formula and no transfer to the flanges is assumed.",
                    "AiscShearLagAlternative is a foreign code formula (AISC 360-16 D3, Table D3.1 case2), used only when explicitly chosen and accepted, without the D3 connected-element relaxation; xbar is measured from the web back face. Under NTC it remains a supplementary method with NTC material factors.",
                    "Single channel: single-lap web-to-gusset eccentricity t/2 as for lapped plates; 2C: symmetric double shear with the explicit share of one channel. Unequal shares, packings, slotted/oversize holes and compression are outside this component.",
                    "The SCI P358 8.2 reinforcement detail and EN1993-1-8 3.10.4 lug angles are different physical details and are not represented." });
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
