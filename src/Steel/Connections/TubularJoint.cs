using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Static ULS resistance of one uniplanar unreinforced welded lattice joint between
    /// hollow sections, EN1993-1-8:2005 (with AC:2009) section7, the edition in force in Italy.
    /// CHS braces on CHS chords: Table7.1 validity, Table7.2 axial resistances, Table7.5 moments and
    /// eq.(7.3) on T, Y and X joints. RHS or CHS braces on RHS chords: Table7.8 validity, Tables 7.11
    /// and 7.12 (axial), Table7.10 for K and N overlap joints. Every joint outside the range of
    /// validity is refused (7.4.1(3), 7.5.1(3) would require all failure modes and secondary moments).
    /// Welds must be full strength (7.3.1(4)): butt welds by declaration, fillet welds by a
    /// sufficient throat bound; a thinner fillet is a missing requirement, never a pass.
    /// prEN1993-1-8 / EN1993-1-8:2024 formulas are not used.</summary>
    public static class TubularJoint
    {
        public const string MethodId = "Steel.Connection.TubularJoint.EN1993_1_8_2005_Section7";
        public const string Scope = "EN1993-1-8:2005 (AC:2009) section7: predominantly static ULS resistance of one uniplanar unreinforced welded lattice joint. "
            + "CHS braces on CHS chords: T, Y, X, K and N gap or overlap joints (Tables 7.1, 7.2; brace moments on T, Y and X joints by Table7.5 and eq.7.3). "
            + "RHS or CHS braces on RHS chords: T, Y, X and K/N gap joints (Tables 7.8, 7.11, 7.12), K/N overlap joints (Table7.10), axial forces only. "
            + "Member resistance and stability, multiplanar or reinforced joints, I/H chords, plates, special joints of Tables 7.6/7.15, fatigue and EN1993-1-8:2024 rules are not included.";
        /// <summary>EN1993-1-1 3.2.6(1); NTC2018 11.3.4.1.</summary>
        internal const double ElasticModulus = 210000;

        public static ConnectionVerification<TubularJointInput> Verify(TubularJointInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return new Evaluation(p).Run();
        }

        private sealed class Evaluation
        {
            private readonly TubularJointInput p;
            private readonly List<string> issues = new List<string>();
            private readonly List<ConnectionCheck> checks = new List<ConnectionCheck>();
            private readonly List<SectionResistanceValue> trace = new List<SectionResistanceValue>();
            private readonly bool circularChord, twoBraces;
            private readonly double gm5;
            private double sigma0, sigmaP, n, np, reduction = 1;

            internal Evaluation(TubularJointInput input)
            {
                p = input; gm5 = p.Code.GammaM5; circularChord = p.Chord.Shape == TubularSectionShape.Circular;
                twoBraces = p.Type == TubularJointType.K || p.Type == TubularJointType.N;
            }

            internal ConnectionVerification<TubularJointInput> Run()
            {
                Value("GammaM5", gm5, "1");
                Situation();
                Members();
                if (issues.Count == 0) ChordStresses();
                if (issues.Count == 0) { if (circularChord) ChsValidity(); else RhsValidity(); }
                if (issues.Count == 0) { if (circularChord) ChsResistances(); else RhsResistances(); }
                Welds();
                Sources();
                return new ConnectionVerification<TubularJointInput>(p, p.Code.Connection, MethodId, Scope, checks, trace, issues.Distinct(),
                    Conditions());
            }

            // ---- field of application (7.1.1, 7.1.2) and declared situation ----

            private void Situation()
            {
                Require(!p.Multiplanar, "MULTIPLANAR_JOINT_NOT_COVERED:7.4.3/7.5.3 reduction factors are not implemented");
                Require(!p.Reinforced, "REINFORCED_JOINT_NOT_COVERED:7.5.2.2 reinforced joints are not implemented");
                Require(!p.FatigueLoading, "FATIGUE_NOT_COVERED:7.1.1(7) refers fatigue to EN1993-1-9");
            }

            private void Members()
            {
                foreach (var m in Members(true)) Member(m.Key, m.Value);
                if (circularChord)
                    Require(Braces().All(b => b.Value.Section.Shape == TubularSectionShape.Circular),
                        "CHS_CHORD_REQUIRES_CHS_BRACES:Table7.4 (I, H or RHS braces on CHS chords) is not implemented");
                else if (twoBraces)
                    Require(p.BraceA.Section.Shape == p.BraceB.Section.Shape, "MIXED_CHS_AND_RHS_BRACES_NOT_COVERED:Tables 7.10-7.12 replace both b and h by d");
                // 7.1.2(3): angles to the chord and between adjacent braces.
                foreach (var b in Braces()) AtLeast("7.1.2(3)", b.Key + ".Angle", b.Value.AngleDegrees, 30, "deg");
                double a = p.BraceA.AngleDegrees;
                switch (p.Type)
                {
                    case TubularJointType.T: Require(Right(a), "T_JOINT_REQUIRES_90_DEGREE_BRACE:use Y for an inclined brace"); break;
                    case TubularJointType.Y: Require(!Right(a), "Y_JOINT_REQUIRES_INCLINED_BRACE:use T at 90 degrees"); break;
                    case TubularJointType.K: Require(!Right(a) && !Right(p.BraceB.AngleDegrees), "K_JOINT_REQUIRES_TWO_INCLINED_BRACES:use N with one brace at 90 degrees"); break;
                    case TubularJointType.N: Require(Right(a) != Right(p.BraceB.AngleDegrees), "N_JOINT_REQUIRES_EXACTLY_ONE_BRACE_AT_90_DEGREES"); break;
                }
                if (twoBraces)
                {
                    AtLeast("7.1.2(3)", "Braces.IncludedAngle", 180 - a - p.BraceB.AngleDegrees, 30, "deg");
                    // 1.5(4): i=1 is the compression brace, i=2 the tension brace; Tables 7.2 and 7.10
                    // describe one brace in compression and the other in tension.
                    double na = p.BraceA.AxialForce, nb = p.BraceB.AxialForce;
                    Require(na * nb < 0 || na == 0 && nb == 0,
                        "K_N_JOINT_REQUIRES_ONE_COMPRESSED_AND_ONE_TENSIONED_BRACE:Table7.6 special joints are not implemented");
                }
                // Table7.5 gives in-plane moments for T, X and Y joints only; RHS moments (Table7.14) are not implemented.
                bool momentsAllowed = circularChord && !twoBraces;
                foreach (var b in Braces())
                    Require(momentsAllowed || b.Value.InPlaneMoment == 0 && b.Value.OutOfPlaneMoment == 0,
                        "BRACE_MOMENTS_NOT_COVERED_FOR_THIS_JOINT:" + b.Key + " (only T, Y and X joints between CHS members; 5.1.5(3) secondary moments)");
            }

            private void Member(string name, TubularJointSection s)
            {
                Require(s.NominalGrade == 235 || s.NominalGrade == 275 || s.NominalGrade == 355 || s.NominalGrade == 420 || s.NominalGrade == 460,
                    "NOMINAL_GRADE_OUTSIDE_S235_TO_S460:" + name + " (7.1.1(4))");
                Require(s.Fy <= s.NominalGrade, "DESIGN_FY_ABOVE_NOMINAL_GRADE:" + name);
                AtLeast("7.1.1(5)", name + ".Thickness", s.Thickness, 2.5, "mm");
                // 7.1.1(4): end products above 355 N/mm2 take 0.9 on every resistance of section7.
                if (s.NominalGrade > 355) reduction = .9;
            }

            // ---- chord stresses (7.2.1(3), 1.5(5)) and equilibrium of the declared forces ----

            private void ChordStresses()
            {
                var c = p.Chord; double a0 = c.Area;
                double BendingStress(TubularChordActions s) => circularChord
                    ? Math.Sqrt(s.InPlaneMoment * s.InPlaneMoment + s.OutOfPlaneMoment * s.OutOfPlaneMoment) / c.ElasticModulusInPlane
                    : Math.Abs(s.InPlaneMoment) / c.ElasticModulusInPlane + Math.Abs(s.OutOfPlaneMoment) / c.ElasticModulusOutOfPlane;
                // sigma0,Ed: maximum compressive stress at the joint (compression positive). For biaxial
                // bending this is the exact extreme fibre: the CHS resultant, the RHS corner.
                double bendA = BendingStress(p.ChordSideA), bendB = BendingStress(p.ChordSideB);
                sigma0 = Math.Max(-p.ChordSideA.N / a0 + bendA, -p.ChordSideB.N / a0 + bendB);
                // sigmap,Ed (7.2): Np,Ed = N0,Ed - sum Ni cos(theta_i). The two chord cuts differ exactly by
                // the brace components, so Np,Ed is the axial force of the less compressed cut; the larger
                // bending stress of the two cuts is kept (GPC reading, never below either cut's value).
                double np0 = Math.Max(p.ChordSideA.N, p.ChordSideB.N);
                sigmaP = -np0 / a0 + Math.Max(bendA, bendB);
                // 1.5(5) literally: n = (sigma0,Ed/fy0)/gammaM5 and np = (sigmap,Ed/fy0)/gammaM5.
                n = sigma0 / c.Fy / gm5; np = sigmaP / c.Fy / gm5;
                Value("Chord.Sigma0Ed", sigma0, "MPa"); Value("Chord.NpEd", np0, "N"); Value("Chord.SigmapEd", sigmaP, "MPa");
                Value(circularChord ? "Chord.np" : "Chord.n", circularChord ? np : n, "1");
                Value("Joint.HighGradeFactor", reduction, "1");
                // Joint equilibrium along the chord with the side/lean convention of the input: a
                // tension brace leaning towards side A pulls the joint towards A (-cos), towards B (+cos).
                double braces = p.Type == TubularJointType.X ? 0
                    : p.BraceA.AxialForce * Cos(p.BraceA) - (twoBraces ? p.BraceB.AxialForce * Cos(p.BraceB) : 0);
                double residual = p.ChordSideB.N - p.ChordSideA.N - braces;
                double scale = Math.Abs(p.ChordSideA.N) + Math.Abs(p.ChordSideB.N) + Math.Abs(p.BraceA.AxialForce)
                    + (twoBraces ? Math.Abs(p.BraceB.AxialForce) : 0);
                Value("Chord.AxialEquilibriumResidual", residual, "N");
                Require(Math.Abs(residual) <= 1e-3 * scale + 1,
                    "CHORD_AND_BRACE_AXIAL_FORCES_NOT_IN_EQUILIBRIUM:same-case forces with the side A/B lean convention are required");
            }

            // ---- range of validity: Table7.1 (CHS) ----

            private void ChsValidity()
            {
                var c = p.Chord; double d0 = c.Depth, t0 = c.Thickness;
                const string table = "Table7.1";
                foreach (var b in Braces())
                {
                    var s = b.Value.Section; double di = s.Depth, ti = s.Thickness;
                    Between(table, b.Key + ".di/d0", di / d0, .2, 1);
                    double nEd = b.Value.AxialForce;
                    bool tension = di / ti <= 50, compression = Ntc2018DesignPolicy.CircularClass(di, ti, s.Fy) <= 2;
                    Value(b.Key + ".di/ti", di / ti, "1"); Value(b.Key + ".CompressionClass", Ntc2018DesignPolicy.CircularClass(di, ti, s.Fy), "1");
                    if (nEd > 0) Require(tension, Outside(table, b.Key + ".di/ti", di / ti, 50) + " (tension brace)");
                    else if (nEd < 0) Require(compression, "OUTSIDE_VALIDITY:" + table + ":" + b.Key + " compression brace Class 1 or 2 required (7.1.2(2))");
                    else Require(tension || compression, "OUTSIDE_VALIDITY:" + table + ":" + b.Key + " neither the tension nor the compression limits hold");
                }
                // X joints: d0/t0 <= 40. The AC:2009 print of Table7.1 keeps "(generally), but:" without
                // the continuation; the X-joint limit 40 is applied (da verificare on the UNI text).
                Between(table, "Chord.d0/t0", d0 / t0, 10, p.Type == TubularJointType.X ? 40 : 50);
                int chordClass = Ntc2018DesignPolicy.CircularClass(d0, t0, c.Fy);
                Value("Chord.CompressionClass", chordClass, "1");
                if (sigma0 > 0) Require(chordClass <= 2, "OUTSIDE_VALIDITY:" + table + ":compressed chord Class 1 or 2 required (7.1.2(2))");
                if (twoBraces) GapOrOverlap(d0, table);
            }

            // ---- range of validity: Table7.8 (RHS chords) ----

            private void RhsValidity()
            {
                var c = p.Chord; double b0 = c.Width, h0 = c.Depth, t0 = c.Thickness;
                const string table = "Table7.8";
                bool overlap = twoBraces && p.Gap < 0, gap = twoBraces && p.Gap >= 0;
                Between(table, "Chord.h0/b0", h0 / b0, .5, 2);
                if (!overlap) { AtMost(table, "Chord.b0/t0", b0 / t0, 35); AtMost(table, "Chord.h0/t0", h0 / t0, 35); }
                int chordClass = RectangularClass(c);
                Value("Chord.CompressionClass", chordClass, "1");
                Require(chordClass <= 2, "OUTSIDE_VALIDITY:" + table + ":chord Class 1 or 2 required");
                foreach (var b in Braces())
                {
                    var s = b.Value.Section; double nEd = b.Value.AxialForce; string k = b.Key;
                    if (s.Shape == TubularSectionShape.Circular)
                    {
                        double di = s.Depth, ti = s.Thickness;
                        Between(table, k + ".di/b0", di / b0, .4, .8);
                        bool tensionLimit = di / ti <= 50, classOne = Ntc2018DesignPolicy.CircularClass(di, ti, s.Fy) == 1;
                        Value(k + ".di/ti", di / ti, "1");
                        if (nEd > 0) Require(tensionLimit, Outside(table, k + ".di/ti", di / ti, 50) + " (tension brace)");
                        else if (nEd < 0) Require(classOne, "OUTSIDE_VALIDITY:" + table + ":" + k + " circular compression brace Class 1 required");
                        else Require(tensionLimit || classOne, "OUTSIDE_VALIDITY:" + table + ":" + k + " neither the tension nor the compression limits hold");
                        continue;
                    }
                    double bi = s.Width, hi = s.Depth, ti2 = s.Thickness, ratio = bi / b0;
                    Between(table, k + ".hi/bi", hi / bi, .5, 2);
                    AtMost("Table7.11/7.12", k + ".bi/b0", ratio, 1, false);
                    if (gap) { AtLeast(table, k + ".bi/b0", ratio, .35); AtLeast(table, k + ".bi/b0(0.1+0.01b0/t0)", ratio, .1 + .01 * b0 / t0); }
                    else AtLeast(table, k + ".bi/b0", ratio, .25);
                    bool slender = bi / ti2 <= 35 && hi / ti2 <= 35; int braceClass = RectangularClass(s);
                    Value(k + ".bi/ti", bi / ti2, "1"); Value(k + ".hi/ti", hi / ti2, "1"); Value(k + ".CompressionClass", braceClass, "1");
                    bool compression = overlap ? braceClass == 1 : slender && braceClass <= 2;
                    if (nEd > 0) Require(slender, "OUTSIDE_VALIDITY:" + table + ":" + k + " tension brace bi/ti and hi/ti <= 35 required");
                    else if (nEd < 0) Require(compression, "OUTSIDE_VALIDITY:" + table + ":" + k + (overlap ? " overlap compression brace Class 1 required"
                        : " compression brace bi/ti, hi/ti <= 35 and Class 1 or 2 required"));
                    else Require(slender || compression, "OUTSIDE_VALIDITY:" + table + ":" + k + " neither the tension nor the compression limits hold");
                }
                if (!twoBraces) return;
                GapOrOverlap(h0, table);
                if (gap)
                {
                    double beta = KBeta(), g = p.Gap, t12 = p.BraceA.Section.Thickness + p.BraceB.Section.Thickness;
                    Value("Joint.Beta", beta, "1");
                    AtLeast(table, "Joint.g/b0", g / b0, .5 * (1 - beta));
                    if (ConnectionValidation.BelowMinimumWithRoundoff(1.5 * (1 - beta), g / b0))
                        issues.Add(g > t12 ? Outside(table, "Joint.g/b0", g / b0, 1.5 * (1 - beta)) + ":TREAT_AS_TWO_SEPARATE_T_OR_Y_JOINTS (Table7.8 note1)"
                            : Outside(table, "Joint.g/b0", g / b0, 1.5 * (1 - beta)));
                }
                else
                {
                    var i = Overlapping(); var j = Overlapped(); var si = i.Value.Section; var sj = j.Value.Section;
                    // AD434 (SCI, 2019): the printed bi/bj <= 0.75 is a typographical error for bi/bj >= 0.75;
                    // with 7.1.2(8) (the narrower brace overlaps) the admitted field is 0.75 <= bi/bj <= 1.
                    AtLeast(table, "Joint.bi/bj", si.Width / sj.Width, .75);
                    if (si.Shape == TubularSectionShape.Rectangular)
                        Require(si.Depth >= si.Width && sj.Depth >= sj.Width,
                            "BRACE_TO_CHORD_SHEAR_CHECK_REQUIRED:Table7.8 note2 (rectangular braces with hi < bi or hj < bj) is not implemented");
                }
            }

            /// <summary>K and N joints: gap (7.1.2(5)) or overlap (7.1.2(6)-(8), 7.3.1(2)) and the
            /// eccentricity limits of 5.1.5(5) that allow the joint design for axial forces only.</summary>
            private void GapOrOverlap(double depth0, string table)
            {
                var a = p.BraceA; var b = p.BraceB; double g = p.Gap;
                double sa = Sin(a), sb = Sin(b), ha = InPlaneDepth(a.Section), hb = InPlaneDepth(b.Section);
                Value("Joint.Gap", g, "mm");
                // Own derivation (side view, chord face line at depth0/2 above the chord axis, brace axes
                // meeting at e below it): brace toes at -(depth0/2+e)cot(thetaA)+hA/(2 sin thetaA) and
                // (depth0/2+e)cot(thetaB)-hB/(2 sin thetaB); g is their distance and
                // cot(thetaA)+cot(thetaB) = sin(thetaA+thetaB)/(sin thetaA sin thetaB).
                double e = (g + ha / (2 * sa) + hb / (2 * sb)) * sa * sb / Math.Sin(Rad(a.AngleDegrees + b.AngleDegrees)) - depth0 / 2;
                Value("Joint.Eccentricity", e, "mm");
                Between("5.1.5(5)", "Joint.e/depth0", e / depth0, -.55, .25);
                if (g >= 0)
                {
                    double t12 = a.Section.Thickness + b.Section.Thickness;
                    Value("Joint.MinimumGap", t12, "mm");
                    if (ConnectionValidation.BelowMinimumWithRoundoff(g, t12)) issues.Add(Outside(table + "/7.1.2(5)", "Joint.Gap", g, t12));
                    return;
                }
                var i = Overlapping(); var j = Overlapped();
                double pLength = InPlaneDepth(i.Value.Section) / Sin(i.Value), lambda = -g / pLength * 100;
                double limit = p.HiddenSeamWelded ? 80 : 60;
                Value("Joint.OverlapProjectedLength", pLength, "mm"); Value("Joint.OverlapLimit", limit, "%");
                AtLeast(table + "/7.1.2(6)", "Joint.OverlapRatio", lambda, 25, "%");
                Require(!ConnectionValidation.BelowMinimumWithRoundoff(limit, lambda),
                    Outside(table + "/7.1.2(6)", "Joint.OverlapRatio", lambda, limit) + ":BRACE_TO_CHORD_SHEAR_CHECK_REQUIRED (not implemented)");
                var si = i.Value.Section; var sj = j.Value.Section;
                // 7.1.2(7): the brace with the lower t*fy overlaps; 7.1.2(8): the narrower brace overlaps.
                Require(!ConnectionValidation.BelowMinimumWithRoundoff(sj.Thickness * sj.Fy, si.Thickness * si.Fy),
                    "OUTSIDE_VALIDITY:7.1.2(7):the overlapping brace must have the lower ti*fyi");
                Require(si.Width <= sj.Width, "OUTSIDE_VALIDITY:7.1.2(8):the narrower brace must overlap the wider one");
                if (!p.HiddenSeamWelded && lambda < 100)
                {
                    // 7.3.1(2): perpendicular components differing by no more than 20 % (of the larger).
                    double va = Math.Abs(a.AxialForce * sa), vb = Math.Abs(b.AxialForce * sb), larger = Math.Max(va, vb);
                    Value("Joint.PerpendicularComponentDifference", larger > 0 ? Math.Abs(va - vb) / larger : 0, "1");
                    Require(Math.Abs(va - vb) <= .2 * larger,
                        "HIDDEN_SEAM_MUST_BE_WELDED:7.3.1(2) perpendicular brace components differ by more than 20%");
                }
            }

            // ---- resistances: Tables 7.2 and 7.5 (CHS) ----

            private void ChsResistances()
            {
                var c = p.Chord; double d0 = c.Depth, t0 = c.Thickness, fy0 = c.Fy, gamma = d0 / (2 * t0);
                double kp = np > 0 ? Math.Max(0, Math.Min(1, 1 - .3 * np * (1 + np))) : 1;
                double f = fy0 * t0 * t0 * reduction / gm5;
                Value("Joint.Gamma", gamma, "1"); Value("Joint.kp", kp, "1");
                if (!twoBraces)
                {
                    var br = p.BraceA; double d1 = br.Section.Depth, beta = d1 / d0, sin = Sin(br);
                    Value("Joint.Beta", beta, "1");
                    double face = p.Type == TubularJointType.X ? kp * f / sin * 5.2 / (1 - .81 * beta)
                        : Math.Pow(gamma, .2) * kp * f / sin * (2.8 + 14.2 * beta * beta);
                    Check("BraceA.ChordFace", Math.Abs(br.AxialForce), face,
                        Ref("Table7.2 chord face failure, " + (p.Type == TubularJointType.X ? "X joints" : "T and Y joints")));
                    double? punching = Punching(br, d0, t0, fy0);
                    if (punching.HasValue) Check("BraceA.PunchingShear", Math.Abs(br.AxialForce), punching.Value, Ref("Table7.2 punching shear, di <= d0-2t0"));
                    if (br.InPlaneMoment != 0 || br.OutOfPlaneMoment != 0) Moments(br, d0, t0, fy0, gamma, beta, kp, face, punching);
                    return;
                }
                // K and N joints: i=1 is the compression brace (1.5(4)); overlaps take g < 0 in kg (7.4.2(6)).
                bool aIsFirst = p.BraceA.AxialForce <= 0;
                var first = aIsFirst ? p.BraceA : p.BraceB; var second = aIsFirst ? p.BraceB : p.BraceA;
                double g = p.Gap, kg = Math.Pow(gamma, .2) * (1 + .024 * Math.Pow(gamma, 1.2) / (1 + Math.Exp(.5 * g / t0 - 1.33)));
                double n1 = kg * kp * f / Sin(first) * (1.8 + 10.2 * first.Section.Depth / d0), n2 = Sin(first) / Sin(second) * n1;
                Value("Joint.kg", kg, "1"); Value("Joint.Brace1IsA", aIsFirst ? 1 : 0, "1"); Value("Joint.Beta", KBeta(), "1");
                string faceRef = Ref("Table7.2 chord face failure, K and N " + (g < 0 ? "overlap" : "gap") + " joints");
                Check((aIsFirst ? "BraceA" : "BraceB") + ".ChordFace", Math.Abs(first.AxialForce), n1, faceRef + " (N1,Rd, compression brace)");
                Check((aIsFirst ? "BraceB" : "BraceA") + ".ChordFace", Math.Abs(second.AxialForce), n2, faceRef + " (N2,Rd = sin(theta1)/sin(theta2) N1,Rd)");
                if (g < 0) return;
                foreach (var b in Braces())
                {
                    double? punching = Punching(b.Value, d0, t0, fy0);
                    if (punching.HasValue) Check(b.Key + ".PunchingShear", Math.Abs(b.Value.AxialForce), punching.Value, Ref("Table7.2 punching shear, K and N gap joints, di <= d0-2t0"));
                }
            }

            private double? Punching(TubularBrace br, double d0, double t0, double fy0)
            {
                double di = br.Section.Depth, sin = Sin(br);
                if (di > d0 - 2 * t0) return null;
                return fy0 / Math.Sqrt(3) * t0 * Math.PI * di * (1 + sin) / (2 * sin * sin) * reduction / gm5;
            }

            /// <summary>Table7.5 (T, X and Y joints) and eq.(7.3), with the governing axial resistance.</summary>
            private void Moments(TubularBrace br, double d0, double t0, double fy0, double gamma, double beta, double kp,
                double face, double? punching)
            {
                double d1 = br.Section.Depth, sin = Sin(br), scale = reduction / gm5;
                double ipFace = 4.85 * fy0 * t0 * t0 * d1 / sin * Math.Sqrt(gamma) * beta * kp * scale;
                double opFace = fy0 * t0 * t0 * d1 / sin * 2.7 / (1 - .81 * beta) * kp * scale;
                double? ipPunching = null, opPunching = null;
                if (punching.HasValue)
                {
                    ipPunching = fy0 * t0 * d1 * d1 / Math.Sqrt(3) * (1 + 3 * sin) / (4 * sin * sin) * scale;
                    opPunching = fy0 * t0 * d1 * d1 / Math.Sqrt(3) * (3 + sin) / (4 * sin * sin) * scale;
                }
                double mip = Math.Abs(br.InPlaneMoment), mop = Math.Abs(br.OutOfPlaneMoment);
                if (mip != 0)
                {
                    Check("BraceA.ChordFace.InPlaneMoment", mip, ipFace, Ref("Table7.5 chord face failure, T, X and Y joints"), "Nmm");
                    if (ipPunching.HasValue) Check("BraceA.PunchingShear.InPlaneMoment", mip, ipPunching.Value, Ref("Table7.5 punching shear, d1 <= d0-2t0"), "Nmm");
                }
                if (mop != 0)
                {
                    Check("BraceA.ChordFace.OutOfPlaneMoment", mop, opFace, Ref("Table7.5 chord face failure"), "Nmm");
                    if (opPunching.HasValue) Check("BraceA.PunchingShear.OutOfPlaneMoment", mop, opPunching.Value, Ref("Table7.5 punching shear, d1 <= d0-2t0"), "Nmm");
                }
                double nRd = Math.Min(face, punching ?? double.PositiveInfinity);
                double ipRd = Math.Min(ipFace, ipPunching ?? double.PositiveInfinity), opRd = Math.Min(opFace, opPunching ?? double.PositiveInfinity);
                double sum = Ratio(Math.Abs(br.AxialForce), nRd) + Math.Pow(Ratio(mip, ipRd), 2) + Ratio(mop, opRd);
                Value("BraceA.MipRd", ipRd, "Nmm"); Value("BraceA.MopRd", opRd, "Nmm");
                Check("BraceA.Interaction", sum, 1, Ref("7.4.2(2) eq.(7.3)"), "1");
            }

            // ---- resistances: Tables 7.11, 7.12 and 7.10 (RHS chords) ----

            private void RhsResistances()
            {
                var c = p.Chord; double b0 = c.Width, h0 = c.Depth, t0 = c.Thickness, fy0 = c.Fy, gamma = b0 / (2 * t0), scale = reduction / gm5;
                Value("Joint.Gamma", gamma, "1");
                if (!twoBraces) { RhsSingle(b0, h0, t0, fy0, gamma, scale); return; }
                if (p.Gap < 0) { RhsOverlap(b0, t0, fy0, scale); return; }
                bool circ = p.BraceA.Section.Shape == TubularSectionShape.Circular; double factor = circ ? Math.PI / 4 : 1;
                double beta = KBeta(), kn = Kn(beta), g = p.Gap;
                double widths = circ ? 2 * (p.BraceA.Section.Depth + p.BraceB.Section.Depth)
                    : p.BraceA.Section.Width + p.BraceB.Section.Width + p.BraceA.Section.Depth + p.BraceB.Section.Depth;
                // Av = (2h0 + alpha b0) t0; alpha = 0 for circular braces (Table7.12).
                double alpha = circ ? 0 : 1 / Math.Sqrt(1 + 4 * g * g / (3 * t0 * t0)), av = (2 * h0 + alpha * b0) * t0;
                Value("Joint.kn", kn, "1"); Value("Joint.Alpha", alpha, "1"); Value("Chord.ShearArea", av, "mm2");
                Require(av <= c.Area, "CHORD_SHEAR_AREA_EXCEEDS_CHORD_AREA:Table7.12");
                if (av > c.Area) return;
                foreach (var b in Braces())
                {
                    var br = b.Value; var s = br.Section; double sin = Sin(br), nEd = Math.Abs(br.AxialForce);
                    double bi = s.Width, hi = s.Depth, ti = s.Thickness;
                    double face = 8.9 * kn * fy0 * t0 * t0 * Math.Sqrt(gamma) / sin * (widths / (4 * b0)) * factor * scale;
                    Check(b.Key + ".ChordFace", nEd, face, Ref("Table7.12 chord face failure, K and N gap joints" + (circ ? ", circular braces x pi/4" : "")));
                    Check(b.Key + ".ChordShear", nEd, fy0 * av / (Math.Sqrt(3) * sin) * scale, Ref("Table7.12 chord shear"));
                    double beff = Math.Min(bi, 10 / (b0 / t0) * fy0 * t0 / (s.Fy * ti) * bi);
                    Value(b.Key + ".beff", beff, "mm");
                    Check(b.Key + ".BraceFailure", nEd, s.Fy * ti * (2 * hi - 4 * ti + bi + beff) * factor * scale, Ref("Table7.12 brace failure"));
                    // Punching where the brace wall lands on the flexible face: the table's beta <= 1-1/gamma,
                    // also checked when the brace's own bi/b0 satisfies it (conservative reading).
                    if (beta <= 1 - 1 / gamma || bi / b0 <= 1 - 1 / gamma)
                    {
                        double bep = Math.Min(bi, 10 / (b0 / t0) * bi);
                        Value(b.Key + ".bep", bep, "mm");
                        Check(b.Key + ".PunchingShear", nEd, fy0 * t0 / (Math.Sqrt(3) * sin) * (2 * hi / sin + bi + bep) * factor * scale,
                            Ref("Table7.12 punching shear, beta <= 1-1/gamma"));
                    }
                }
                // Chord cross-section in the gap. VEd and Vpl,Rd are not defined in the table: Vpl,Rd =
                // fy0 Av/(sqrt3 gammaM5) as in the chord shear row, VEd bounded by |V| of one chord cut
                // plus the transverse component of the brace on that side (the lower of the two bounds).
                double vpl = fy0 * av / Math.Sqrt(3) * scale;
                double vEd = Math.Min(Math.Abs(p.ChordSideA.InPlaneShear) + Math.Abs(p.BraceA.AxialForce * Sin(p.BraceA)),
                    Math.Abs(p.ChordSideB.InPlaneShear) + Math.Abs(p.BraceB.AxialForce * Sin(p.BraceB)));
                double nGap = Math.Max(Math.Abs(p.ChordSideA.N + p.BraceA.AxialForce * Cos(p.BraceA)),
                    Math.Abs(p.ChordSideB.N + p.BraceB.AxialForce * Cos(p.BraceB)));
                double ratio = Math.Min(1, vEd / vpl);
                double n0 = ((c.Area - av) * fy0 + av * fy0 * Math.Sqrt(1 - ratio * ratio)) * scale;
                Value("ChordGap.VEd", vEd, "N"); Value("ChordGap.N0Ed", nGap, "N");
                Check("ChordGap.Shear", vEd, vpl, Ref("Table7.12 chord shear, Vpl,Rd = fy0 Av/(sqrt3 gammaM5) (GPC reading of VEd/Vpl,Rd)"));
                Check("ChordGap.Axial", nGap, n0, Ref("Table7.12 chord shear, N0,Rd in the gap"));
            }

            private void RhsSingle(double b0, double h0, double t0, double fy0, double gamma, double scale)
            {
                var br = p.BraceA; var s = br.Section; bool circ = s.Shape == TubularSectionShape.Circular;
                double factor = circ ? Math.PI / 4 : 1, b1 = s.Width, h1 = s.Depth, t1 = s.Thickness;
                double beta = b1 / b0, eta = h1 / b0, sin = Sin(br), cos = Cos(br), nEd = Math.Abs(br.AxialForce);
                bool x = p.Type == TubularJointType.X;
                Value("Joint.Beta", beta, "1"); Value("Joint.Eta", eta, "1"); Value("Joint.kn", Kn(beta), "1");
                double Face(double b) => Kn(b) * fy0 * t0 * t0 / ((1 - b) * sin) * (2 * eta / sin + 4 * Math.Sqrt(1 - b)) * factor * scale;
                string sideRef = "";
                double SideWall()
                {
                    double fb = fy0;
                    if (br.AxialForce < 0)
                    {
                        // fb = chi fy0 (T, Y) or 0.8 chi fy0 sin(theta1) (X); chi from EN1993-1-1 6.3.1.2 with the
                        // hollow-section curve of Table6.2 / NTC Tab.4.2.VIII (GPC reading of "relevant curve").
                        double slenderness = 3.46 * (h0 / t0 - 2) * Math.Sqrt(1 / sin) / (Math.PI * Math.Sqrt(ElasticModulus / fy0));
                        var curve = p.Chord.Fabrication == TubularFabrication.ColdFormed ? SteelBucklingCurve.C
                            : p.Chord.NominalGrade == 460 ? SteelBucklingCurve.A0 : SteelBucklingCurve.A;
                        double chi = BucklingCurves.Reduction(slenderness, curve);
                        fb = x ? .8 * chi * fy0 * sin : chi * fy0;
                        Value("BraceA.SideWall.Slenderness", slenderness, "1"); Value("BraceA.SideWall.Chi", chi, "1");
                        Value("BraceA.SideWall.Curve", (int)curve, "enum");
                    }
                    Value("BraceA.SideWall.fb", fb, "MPa");
                    double side = Kn(1) * fb * t0 / sin * (2 * h1 / sin + 10 * t0) * factor * scale;
                    sideRef = "Table7.11 chord side wall buckling (beta = 1.0)";
                    if (x && cos > h1 / h0)
                    {
                        // Note 1: smaller of the side wall buckling and the side wall shear of Table7.12, side
                        // walls only (alpha = 0, Av = 2h0t0), not multiplied by pi/4 ("except for chord shear").
                        double shear = fy0 * 2 * h0 * t0 / (Math.Sqrt(3) * sin) * scale;
                        Value("BraceA.SideWall.Shear", shear, "N");
                        if (shear < side) { side = shear; sideRef = "Table7.11 note1, X joint: chord side wall shear of Table7.12 (Av = 2h0t0)"; }
                    }
                    return side;
                }
                const double low = .85;
                if (beta <= low) Check("BraceA.ChordFace", nEd, Face(beta), Ref("Table7.11 chord face failure, beta <= 0.85" + (circ ? ", circular brace x pi/4" : "")));
                else if (ConnectionValidation.BelowMinimumWithRoundoff(beta, 1))
                {
                    double atLow = Face(low), side = SideWall(), value = atLow + (beta - low) / (1 - low) * (side - atLow);
                    Value("BraceA.ChordFaceAtBeta085", atLow, "N"); Value("BraceA.SideWallAtBeta1", side, "N");
                    Check("BraceA.ChordFaceToSideWall", nEd, value, Ref("Table7.11 note2: linear interpolation between chord face at beta = 0.85 and " + sideRef));
                }
                else Check("BraceA.ChordSideWall", nEd, SideWall(), Ref(sideRef));
                if (beta >= low)
                {
                    double beff = Math.Min(b1, 10 / (b0 / t0) * fy0 * t0 / (s.Fy * t1) * b1);
                    Value("BraceA.beff", beff, "mm");
                    Check("BraceA.BraceFailure", nEd, s.Fy * t1 * (2 * h1 - 4 * t1 + 2 * beff) * factor * scale, Ref("Table7.11 brace failure, beta >= 0.85"));
                    if (beta <= 1 - 1 / gamma)
                    {
                        double bep = Math.Min(b1, 10 / (b0 / t0) * b1);
                        Value("BraceA.bep", bep, "mm");
                        Check("BraceA.PunchingShear", nEd, fy0 * t0 / (Math.Sqrt(3) * sin) * (2 * h1 / sin + 2 * bep) * factor * scale,
                            Ref("Table7.11 punching shear, 0.85 <= beta <= 1-1/gamma"));
                    }
                }
            }

            /// <summary>Table7.10 (via Table7.12): brace failure of the overlapping brace i; the overlapped
            /// brace j takes the same efficiency (joint resistance over the brace plastic resistance).</summary>
            private void RhsOverlap(double b0, double t0, double fy0, double scale)
            {
                var i = Overlapping(); var j = Overlapped(); var si = i.Value.Section; var sj = j.Value.Section;
                bool circ = si.Shape == TubularSectionShape.Circular; double factor = circ ? Math.PI / 4 : 1;
                double bi = si.Width, hi = si.Depth, ti = si.Thickness, fyi = si.Fy, bj = sj.Width, tj = sj.Thickness, fyj = sj.Fy;
                double lambda = -p.Gap / (InPlaneDepth(si) / Sin(i.Value)) * 100;
                double beff = Math.Min(bi, 10 / (b0 / t0) * fy0 * t0 / (fyi * ti) * bi);
                double beov = Math.Min(bi, 10 / (bj / tj) * fyj * tj / (fyi * ti) * bi);
                Value(i.Key + ".beff", beff, "mm"); Value(i.Key + ".beov", beov, "mm");
                double resistance; string range;
                if (lambda < 50) { resistance = fyi * ti * (beff + beov + 2 * hi * lambda / 50 - 4 * ti); range = "25% <= lambda_ov < 50%"; }
                else if (lambda < 80) { resistance = fyi * ti * (beff + beov + 2 * hi - 4 * ti); range = "50% <= lambda_ov < 80%"; }
                else { resistance = fyi * ti * (bi + beov + 2 * hi - 4 * ti); range = "lambda_ov >= 80%"; }
                resistance *= factor * scale;
                Check(i.Key + ".BraceFailure", Math.Abs(i.Value.AxialForce), resistance,
                    Ref("Table7.10 brace failure of the overlapping brace, " + range + (circ ? ", circular braces x pi/4" : "")));
                double efficiency = resistance / (si.Area * fyi);
                Value("Joint.OverlapEfficiency", efficiency, "1");
                Check(j.Key + ".BraceFailure", Math.Abs(j.Value.AxialForce), efficiency * sj.Area * fyj,
                    Ref("Table7.10 note: overlapped brace with the efficiency of the overlapping brace"));
            }

            // ---- welds (7.3) and declared sources ----

            private void Welds()
            {
                var code = p.Code.Connection;
                foreach (var b in Braces())
                {
                    var w = b.Value.Weld; var s = b.Value.Section;
                    Require(!string.IsNullOrWhiteSpace(w.ExecutionSource),
                        b.Key + "_WELD_EXECUTION_SOURCE_REQUIRED:7.3.1(2) all-round weld, 4.2 matching consumables");
                    if (w.Kind != TubularWeldKind.Fillet) continue;
                    // 7.3.1(4): weld resistance per unit perimeter not below the brace wall t fy/gammaM0
                    // (upper bound of A fy/gammaM0 per unit perimeter). The simplified method 4.5.3.3 (NTC
                    // [4.2.83]) is valid in every direction: a fu/(sqrt3 betaw gammaM2) >= t fy/gammaM0 is
                    // sufficient. A thinner throat needs the 7.3.1(6) justification, outside this method.
                    var parts = new List<TubularJointSection> { s, p.Chord };
                    if (twoBraces && p.Gap < 0 && b.Key == Overlapping().Key) parts.Add(Overlapped().Value.Section);
                    double fu = parts.Min(x => x.Fu); int grade = parts.Min(x => x.NominalGrade);
                    double betaW = grade <= 235 ? .8 : grade <= 275 ? .85 : grade <= 355 ? .9 : 1;
                    double required = Math.Sqrt(3) * betaW * code.GammaM2Connection * s.Fy * s.Thickness / (fu * code.Plates.GammaM0);
                    Value(b.Key + ".Weld.Throat", w.Throat, "mm"); Value(b.Key + ".Weld.FullStrengthThroat", required, "mm");
                    Require(!ConnectionValidation.BelowMinimumWithRoundoff(w.Throat, required),
                        string.Format(CultureInfo.InvariantCulture, "{0}_FILLET_NOT_SHOWN_FULL_STRENGTH:7.3.1(4) throat {1:0.###} < {2:0.###} mm ({3})",
                            b.Key, w.Throat, required, code.IsNtc ? "NTC2018 4.2.8.2.4 [4.2.83]" : "EN1993-1-8 4.5.3.3"));
                }
            }

            private void Sources()
            {
                Require(!string.IsNullOrWhiteSpace(p.ActionSource), "SAME_CASE_CHORD_AND_BRACE_FORCES_SOURCE_REQUIRED");
                Require(!string.IsNullOrWhiteSpace(p.GlobalAnalysisSource),
                    "LATTICE_GLOBAL_ANALYSIS_SOURCE_REQUIRED:5.1.5(2)-(3) model, system length/depth not below the applicable minimum");
                Require(p.Chord.Thickness <= 25 || !string.IsNullOrWhiteSpace(p.ThroughThicknessSource),
                    "CHORD_WALL_OVER_25MM_THROUGH_THICKNESS_SOURCE_REQUIRED:7.1.1(6)");
            }

            private IEnumerable<string> Conditions()
            {
                var list = new List<string> {
                    "EN1993-1-8:2005 with AC:2009, section7; EN1993-1-8:2024 (prEN) formulas are not used.",
                    "Chord and brace members are verified separately (7.2.1(1)P; EN1993-1-1 or NTC2018 4.2.4), with the eccentricity moments of 5.1.5(6) in the compression chord.",
                    "Lattice analysed per 5.1.5: secondary moments neglected only within the range of validity and with system length/depth not below the applicable minimum (6 for buildings).",
                    "Brace ends are neither flattened nor cropped (7.1.2(4)); predominantly static loading; uniplanar unreinforced joint.",
                    "Welds all round with consumables matching the parent metal; full strength per 7.3.1(4) as declared or as shown by the throat bound." };
                if (p.Code.IsNtc)
                    list.Add("NTC route: NTC2018 4.2.8 and Circolare 2019 C4.2 contain no hollow section lattice joint rules; UNI EN1993-1-8 applied through NTC2018 chapter12 with the Italian National Annex (gammaM5 = 1.0).");
                if (p.Type == TubularJointType.X) list.Add("X joint: the brace on the opposite chord face is identical, coaxial and carries the same force.");
                if (!twoBraces) list.Add("Single-brace joint: the brace axis meets the chord axis (no eccentricity); brace moments, if any, are the declared ones.");
                return list;
            }

            // ---- helpers ----

            private IEnumerable<KeyValuePair<string, TubularJointSection>> Members(bool withChord)
            {
                if (withChord) yield return new KeyValuePair<string, TubularJointSection>("Chord", p.Chord);
                foreach (var b in Braces()) yield return new KeyValuePair<string, TubularJointSection>(b.Key, b.Value.Section);
            }
            private IEnumerable<KeyValuePair<string, TubularBrace>> Braces()
            {
                yield return new KeyValuePair<string, TubularBrace>("BraceA", p.BraceA);
                if (p.BraceB != null) yield return new KeyValuePair<string, TubularBrace>("BraceB", p.BraceB);
            }
            private KeyValuePair<string, TubularBrace> Overlapping() => p.OverlappingBrace == TubularOverlappingBrace.A
                ? new KeyValuePair<string, TubularBrace>("BraceA", p.BraceA) : new KeyValuePair<string, TubularBrace>("BraceB", p.BraceB);
            private KeyValuePair<string, TubularBrace> Overlapped() => p.OverlappingBrace == TubularOverlappingBrace.A
                ? new KeyValuePair<string, TubularBrace>("BraceB", p.BraceB) : new KeyValuePair<string, TubularBrace>("BraceA", p.BraceA);
            /// <summary>1.5(6), K and N joints: (d1+d2)/(2d0), (d1+d2)/(2b0) or (b1+b2+h1+h2)/(4b0).</summary>
            private double KBeta()
            {
                var a = p.BraceA.Section; var b = p.BraceB.Section; double w0 = p.Chord.Width;
                return a.Shape == TubularSectionShape.Circular ? (a.Depth + b.Depth) / (2 * w0) : (a.Width + b.Width + a.Depth + b.Depth) / (4 * w0);
            }
            /// <summary>kn of Tables 7.10-7.12: 1.3-0.4n/beta for n > 0, not above 1.0 (not below zero:
            /// a non-positive value leaves no joint resistance); 1.0 for n <= 0.</summary>
            private double Kn(double beta) => n > 0 ? Math.Max(0, Math.Min(1, 1.3 - .4 * n / beta)) : 1;
            /// <summary>EN1993-1-1 Table5.2 internal compression parts (NTC Tab.4.2.III), c = width - 3t:
            /// the EN10210 corner radius 1.5t; larger cold-formed radii give a smaller flat width.</summary>
            private static int RectangularClass(TubularJointSection s)
            {
                double eps = Math.Sqrt(235 / s.Fy), ratio = Math.Max(s.Depth, s.Width) / s.Thickness - 3;
                return ratio <= 33 * eps ? 1 : ratio <= 38 * eps ? 2 : ratio <= 42 * eps ? 3 : 4;
            }
            private static double InPlaneDepth(TubularJointSection s) => s.Depth;
            private static bool Right(double degrees) => Math.Abs(degrees - 90) <= 1e-9;
            private static double Rad(double degrees) => degrees * Math.PI / 180;
            private static double Sin(TubularBrace b) => Right(b.AngleDegrees) ? 1 : Math.Sin(Rad(b.AngleDegrees));
            private static double Cos(TubularBrace b) => Right(b.AngleDegrees) ? 0 : Math.Cos(Rad(b.AngleDegrees));
            private static double Ratio(double demand, double resistance)
                => demand == 0 ? 0 : resistance > 0 ? demand / resistance : double.PositiveInfinity;
            private string Ref(string clause) => (p.Code.IsNtc ? "UNI EN1993-1-8:2005 " : "EN1993-1-8:2005 ") + clause
                + (p.Code.IsNtc ? " (NTC2018 cap.12)" : "") + (reduction < 1 ? "; x0.9 per 7.1.1(4)" : "");
            private void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            private void Value(string name, double value, string unit)
            {
                if (!double.IsNaN(value) && !double.IsInfinity(value)) trace.Add(new SectionResistanceValue(name, value, unit));
                else issues.Add(name + ":OutsideNumericRange");
            }
            private void Check(string id, double demand, double resistance, string reference, string unit = "N")
            {
                if (double.IsNaN(resistance) || double.IsInfinity(resistance) || resistance < 0 || double.IsNaN(demand) || double.IsInfinity(demand))
                    issues.Add(id + ":OutsideNumericRange");
                else checks.Add(new ConnectionCheck(id, demand, resistance, reference, unit));
            }
            private static string Outside(string clause, string name, double value, double limit)
                => string.Format(CultureInfo.InvariantCulture, "OUTSIDE_VALIDITY:{0}:{1}={2:0.####}{3}{4:0.####}", clause, name, value, value < limit ? "<" : ">", limit);
            private void AtLeast(string clause, string name, double value, double limit, string unit = "1")
            {
                Value(name, value, unit);
                if (ConnectionValidation.BelowMinimumWithRoundoff(value, limit)) issues.Add(Outside(clause, name, value, limit));
            }
            private void AtMost(string clause, string name, double value, double limit, bool record = true)
            {
                if (record) Value(name, value, "1");
                if (ConnectionValidation.BelowMinimumWithRoundoff(limit, value)) issues.Add(Outside(clause, name, value, limit));
            }
            private void Between(string clause, string name, double value, double min, double max)
            {
                Value(name, value, "1");
                if (ConnectionValidation.BelowMinimumWithRoundoff(value, min)) issues.Add(Outside(clause, name, value, min));
                else if (ConnectionValidation.BelowMinimumWithRoundoff(max, value)) issues.Add(Outside(clause, name, value, max));
            }
        }
    }
}
