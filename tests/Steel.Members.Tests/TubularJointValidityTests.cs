using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D = Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

/// <summary>Every refusal of the range of validity (Tables 7.1 and 7.8), of the field of
/// application (7.1.1, 7.1.2, 5.1.5(5), 7.3.1(2)) and of the excluded situations. A refused joint
/// has no evaluated check and is NotSupported, never Passed. Each case changes one quantity of a
/// joint that the resistance tests show to be inside the field.</summary>
[TestClass]
public class TubularJointValidityTests
{
    private static readonly double Cos45 = Math.Cos(Math.PI / 4);
    private static TubularJointSection Chs168 => D.Chs(168.3, 8);
    private static TubularJointSection Rhs200 => D.Rhs(200, 200, 10);

    /// <summary>T or Y joint with consistent cuts: N_B = N_A + N1 cos(theta) (brace leaning towards A).</summary>
    private static TubularJointInput Single(TubularJointSection chord, TubularJointSection brace, double n1 = -150000, double angle = 90,
        double chordN = -300000, TubularJointType? type = null, double mip = 0, bool multiplanar = false, bool reinforced = false, bool fatigue = false)
    {
        double cos = angle == 90 ? 0 : Math.Cos(angle * Math.PI / 180);
        var t = type ?? (angle == 90 ? TubularJointType.T : TubularJointType.Y);
        return D.Single(t, chord, D.Cut(chordN), D.Cut(t == TubularJointType.X ? chordN : chordN + n1 * cos), D.Brace(brace, angle, n1, mip),
            multiplanar: multiplanar, reinforced: reinforced, fatigue: fatigue);
    }

    /// <summary>K or N joint with consistent cuts: N_B = N_A + N_A,br cos(thetaA) - N_B,br cos(thetaB).</summary>
    private static TubularJointInput Two(TubularJointSection chord, TubularJointSection a, TubularJointSection b, double gap,
        double na = -200000, double nb = 200000, double angleA = 45, double angleB = 45, TubularJointType type = TubularJointType.K,
        TubularOverlappingBrace overlapping = TubularOverlappingBrace.A, bool welded = false, double mipA = 0)
    {
        double ca = angleA == 90 ? 0 : Math.Cos(angleA * Math.PI / 180), cb = angleB == 90 ? 0 : Math.Cos(angleB * Math.PI / 180);
        return D.Two(type, chord, D.Cut(-300000), D.Cut(-300000 + na * ca - nb * cb), D.Brace(a, angleA, na, mipA), D.Brace(b, angleB, nb),
            gap, overlapping, welded);
    }

    /// <summary>Overlap of brace A on brace B at 45/45 with the requested ratio of p = hA/sin45.</summary>
    private static TubularJointInput Overlap(TubularJointSection chord, TubularJointSection a, TubularJointSection b, double ratio,
        double na = -180000, double nb = 180000, bool welded = false)
        => Two(chord, a, b, -ratio * a.Depth / D.Sin45, na, nb, welded: welded);

    private static readonly Dictionary<string, (Func<TubularJointInput> Input, string Expected)> Cases = new()
    {
        // ---- Table7.1, CHS ----
        ["CHS di/d0 below 0.2"] = (() => Single(Chs168, D.Chs(26.9, 3.2)), "OUTSIDE_VALIDITY:Table7.1:BraceA.di/d0=0.1598<0.2"),
        ["CHS di/d0 above 1.0"] = (() => Single(Chs168, D.Chs(177.8, 8)), "OUTSIDE_VALIDITY:Table7.1:BraceA.di/d0=1.0564>1"),
        ["CHS d0/t0 below 10"] = (() => Single(D.Chs(168.3, 20), D.Chs(114.3, 6)), "OUTSIDE_VALIDITY:Table7.1:Chord.d0/t0=8.415<10"),
        ["CHS d0/t0 above 50"] = (() => Single(D.Chs(323.9, 6), D.Chs(114.3, 6), chordN: 300000), "OUTSIDE_VALIDITY:Table7.1:Chord.d0/t0=53.9833>50"),
        ["CHS X joint d0/t0 above 40"] = (() => Single(D.Chs(225, 5), D.Chs(114.3, 6), -60000, chordN: 100000, type: TubularJointType.X),
            "OUTSIDE_VALIDITY:Table7.1:Chord.d0/t0=45>40"),
        ["CHS tension brace di/ti above 50"] = (() => Single(Chs168, D.Chs(139.7, 2.6), 100000), "OUTSIDE_VALIDITY:Table7.1:BraceA.di/ti=53.7308>50 (tension brace)"),
        ["CHS compression brace Class 3"] = (() => Single(Chs168, D.Chs(139.7, 2.6), -100000), "Table7.1:BraceA compression brace Class 1 or 2 required"),
        ["CHS compressed chord Class 3"] = (() => Single(D.Chs(168.3, 4.5, 460, 460, 540), D.Chs(114.3, 6)), "Table7.1:compressed chord Class 1 or 2 required"),
        ["CHS gap below t1+t2"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 8), "OUTSIDE_VALIDITY:Table7.1/7.1.2(5):Joint.Gap=8<10"),
        ["CHS overlap below 25%"] = (() => Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .2), "OUTSIDE_VALIDITY:Table7.1/7.1.2(6):Joint.OverlapRatio=20<25"),
        ["CHS overlap above 60% with unwelded hidden seam"] = (() => Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .7),
            "OUTSIDE_VALIDITY:Table7.1/7.1.2(6):Joint.OverlapRatio=70>60:BRACE_TO_CHORD_SHEAR_CHECK_REQUIRED"),
        ["CHS overlap above 80% with welded hidden seam"] = (() => Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .9, welded: true),
            "OUTSIDE_VALIDITY:Table7.1/7.1.2(6):Joint.OverlapRatio=90>80"),
        ["Overlapping brace with the larger t*fy"] = (() => Overlap(Chs168, D.Chs(88.9, 6.3), D.Chs(88.9, 5), .5), "OUTSIDE_VALIDITY:7.1.2(7)"),
        ["Wider brace overlapping"] = (() => Overlap(Chs168, D.Chs(101.6, 5), D.Chs(88.9, 5), .5), "OUTSIDE_VALIDITY:7.1.2(8)"),
        ["Unwelded hidden seam with unbalanced components"] = (() => Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .5, -180000, 120000),
            "HIDDEN_SEAM_MUST_BE_WELDED:7.3.1(2)"),
        ["Brace angle below 30"] = (() => Single(Chs168, D.Chs(114.3, 6), angle: 25), "OUTSIDE_VALIDITY:7.1.2(3):BraceA.Angle=25<30"),
        ["Angle between braces below 30"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 20, angleA: 80, angleB: 80),
            "OUTSIDE_VALIDITY:7.1.2(3):Braces.IncludedAngle=20<30"),
        ["Eccentricity above 0.25 d0"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 140), "OUTSIDE_VALIDITY:5.1.5(5):Joint.e/depth0=0.2894>0.25"),
        // N overlap, A 168.3x8 at 30 deg overlapping B 168.3x8 at 90 deg by 80 % (welded): p = 336.6, g = -269.28,
        // e = (-269.28 + 168.3 + 84.15)*sin30*sin90/sin120 - 84.15 = -93.87 mm, e/d0 = -0.5577.
        ["Eccentricity below -0.55 d0"] = (() => Two(Chs168, D.Chs(168.3, 8), D.Chs(168.3, 8), -0.8 * 168.3 / 0.5, angleA: 30, angleB: 90,
            type: TubularJointType.N, welded: true), "OUTSIDE_VALIDITY:5.1.5(5):Joint.e/depth0=-0.5577<-0.55"),
        ["CHS unloaded brace outside both brace limits"] = (() => Single(Chs168, D.Chs(139.7, 2.6), 0), "Table7.1:BraceA neither the tension nor the compression limits hold"),
        ["Wall below 2.5 mm"] = (() => Single(Chs168, D.Chs(42.4, 2.0), 50000), "OUTSIDE_VALIDITY:7.1.1(5):BraceA.Thickness=2<2.5"),
        ["Grade above S460"] = (() => Single(D.Chs(168.3, 8, 500, 500, 550), D.Chs(114.3, 6)), "NOMINAL_GRADE_OUTSIDE_S235_TO_S460:Chord (7.1.1(4))"),
        ["Design fy above the nominal grade"] = (() => Single(Chs168, D.Chs(114.3, 6, 355, 375, 510)), "DESIGN_FY_ABOVE_NOMINAL_GRADE:BraceA"),
        ["Multiplanar joint"] = (() => Single(Chs168, D.Chs(114.3, 6), multiplanar: true), "MULTIPLANAR_JOINT_NOT_COVERED"),
        ["Reinforced joint"] = (() => Single(Chs168, D.Chs(114.3, 6), reinforced: true), "REINFORCED_JOINT_NOT_COVERED"),
        ["Fatigue loading"] = (() => Single(Chs168, D.Chs(114.3, 6), fatigue: true), "FATIGUE_NOT_COVERED:7.1.1(7)"),
        ["RHS brace on CHS chord"] = (() => Single(Chs168, D.Rhs(100, 100, 6)), "CHS_CHORD_REQUIRES_CHS_BRACES:Table7.4"),
        ["K joint with two compressed braces"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 20, -200000, -100000),
            "K_N_JOINT_REQUIRES_ONE_COMPRESSED_AND_ONE_TENSIONED_BRACE"),
        ["Brace moment on a K joint"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 20, mipA: 1e6), "BRACE_MOMENTS_NOT_COVERED_FOR_THIS_JOINT:BraceA"),
        ["Chord cuts not in equilibrium"] = (() => D.Single(TubularJointType.T, Chs168, D.Cut(-300000), D.Cut(-350000), D.Brace(D.Chs(114.3, 6), 90, -150000)),
            "CHORD_AND_BRACE_AXIAL_FORCES_NOT_IN_EQUILIBRIUM"),
        ["T joint with an inclined brace"] = (() => Single(Chs168, D.Chs(114.3, 6), angle: 60, type: TubularJointType.T), "T_JOINT_REQUIRES_90_DEGREE_BRACE"),
        ["Y joint at 90 degrees"] = (() => Single(Chs168, D.Chs(114.3, 6), type: TubularJointType.Y), "Y_JOINT_REQUIRES_INCLINED_BRACE"),
        ["K joint with a vertical brace"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 15, angleA: 90), "K_JOINT_REQUIRES_TWO_INCLINED_BRACES"),
        ["N joint with two inclined braces"] = (() => Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 20, type: TubularJointType.N),
            "N_JOINT_REQUIRES_EXACTLY_ONE_BRACE_AT_90_DEGREES"),
        // ---- Table7.8, RHS chords ----
        ["RHS h0/b0 below 0.5"] = (() => Single(D.Rhs(90, 200, 8), D.Rhs(120, 120, 6)), "OUTSIDE_VALIDITY:Table7.8:Chord.h0/b0=0.45<0.5"),
        ["RHS h0/b0 above 2.0"] = (() => Single(D.Rhs(250, 120, 8), D.Rhs(100, 100, 5)), "OUTSIDE_VALIDITY:Table7.8:Chord.h0/b0=2.0833>2"),
        ["RHS b0/t0 above 35"] = (() => Single(D.Rhs(300, 300, 8), D.Rhs(150, 150, 6)), "OUTSIDE_VALIDITY:Table7.8:Chord.b0/t0=37.5>35"),
        ["RHS h0/t0 above 35"] = (() => Single(D.Rhs(300, 200, 8), D.Rhs(120, 120, 6)), "OUTSIDE_VALIDITY:Table7.8:Chord.h0/t0=37.5>35"),
        ["RHS chord Class 3"] = (() => Single(D.Rhs(250, 250, 8, 460, 460, 540), D.Rhs(150, 150, 6)), "OUTSIDE_VALIDITY:Table7.8:chord Class 1 or 2 required"),
        ["RHS brace hi/bi below 0.5"] = (() => Single(Rhs200, D.Rhs(50, 120, 5)), "OUTSIDE_VALIDITY:Table7.8:BraceA.hi/bi=0.4167<0.5"),
        ["RHS brace hi/bi above 2.0"] = (() => Single(Rhs200, D.Rhs(200, 80, 6)), "OUTSIDE_VALIDITY:Table7.8:BraceA.hi/bi=2.5>2"),
        ["RHS brace wider than the chord"] = (() => Single(Rhs200, D.Rhs(120, 220, 8)), "OUTSIDE_VALIDITY:Table7.11/7.12:BraceA.bi/b0=1.1>1"),
        ["RHS T joint bi/b0 below 0.25"] = (() => Single(Rhs200, D.Rhs(40, 40, 3), -20000), "OUTSIDE_VALIDITY:Table7.8:BraceA.bi/b0=0.2<0.25"),
        ["RHS K gap bi/b0 below 0.35"] = (() => Two(Rhs200, D.Rhs(60, 60, 4), D.Rhs(60, 60, 4), 100, -50000, 50000), "OUTSIDE_VALIDITY:Table7.8:BraceA.bi/b0=0.3<0.35"),
        ["RHS K gap bi/b0 below 0.1+0.01 b0/t0"] = (() => Two(D.Rhs(300, 300, 10), D.Rhs(114, 114, 6), D.Rhs(114, 114, 6), 120),
            "OUTSIDE_VALIDITY:Table7.8:BraceA.bi/b0(0.1+0.01b0/t0)=0.38<0.4"),
        ["RHS compression brace bi/ti above 35"] = (() => Single(Rhs200, D.Rhs(160, 160, 4), -100000),
            "Table7.8:BraceA compression brace bi/ti, hi/ti <= 35 and Class 1 or 2 required"),
        ["RHS tension brace bi/ti above 35"] = (() => Single(Rhs200, D.Rhs(160, 160, 4), 100000), "Table7.8:BraceA tension brace bi/ti and hi/ti <= 35 required"),
        ["RHS compression brace Class 3"] = (() => Single(Rhs200, D.Rhs(120, 120, 3.8, 460, 460, 540), -100000),
            "Table7.8:BraceA compression brace bi/ti, hi/ti <= 35 and Class 1 or 2 required"),
        ["RHS overlap compression brace Class 2"] = (() => Overlap(Rhs200, D.Rhs(120, 120, 4), D.Rhs(120, 120, 8), .5, -150000, 150000),
            "Table7.8:BraceA overlap compression brace Class 1 required"),
        ["CHS brace on RHS chord di/b0 below 0.4"] = (() => Single(Rhs200, D.Chs(60.3, 4), -50000), "OUTSIDE_VALIDITY:Table7.8:BraceA.di/b0=0.3015<0.4"),
        ["CHS brace on RHS chord di/b0 above 0.8"] = (() => Single(Rhs200, D.Chs(168.3, 6)), "OUTSIDE_VALIDITY:Table7.8:BraceA.di/b0=0.8415>0.8"),
        ["CHS tension brace on RHS chord di/ti above 50"] = (() => Single(Rhs200, D.Chs(139.7, 2.6), 100000),
            "OUTSIDE_VALIDITY:Table7.8:BraceA.di/ti=53.7308>50 (tension brace)"),
        ["CHS compression brace on RHS chord Class 2"] = (() => Single(Rhs200, D.Chs(139.7, 3.6), -100000), "Table7.8:BraceA circular compression brace Class 1 required"),
        ["CHS unloaded brace on RHS chord outside both limits"] = (() => Single(Rhs200, D.Chs(139.7, 2.6), 0), "Table7.8:BraceA neither the tension nor the compression limits hold"),
        ["RHS unloaded brace outside both limits"] = (() => Single(Rhs200, D.Rhs(160, 160, 4), 0), "Table7.8:BraceA neither the tension nor the compression limits hold"),
        ["RHS gap below 0.5(1-beta) b0"] = (() => Two(Rhs200, D.Rhs(120, 120, 6), D.Rhs(120, 120, 6), 30), "OUTSIDE_VALIDITY:Table7.8:Joint.g/b0=0.15<0.2"),
        ["RHS gap above 1.5(1-beta) b0"] = (() => Two(Rhs200, D.Rhs(120, 120, 6), D.Rhs(120, 120, 6), 140), "TREAT_AS_TWO_SEPARATE_T_OR_Y_JOINTS"),
        ["RHS overlap above 80%"] = (() => Overlap(Rhs200, D.Rhs(120, 120, 6), D.Rhs(120, 120, 8), .85, -300000, 300000, true),
            "OUTSIDE_VALIDITY:Table7.8/7.1.2(6):Joint.OverlapRatio=85>80"),
        ["RHS overlap bi/bj below 0.75"] = (() => Overlap(Rhs200, D.Rhs(80, 80, 5), D.Rhs(120, 120, 8), .5, -150000, 150000, true),
            "OUTSIDE_VALIDITY:Table7.8:Joint.bi/bj=0.6667<0.75"),
        ["RHS overlap with hi below bi"] = (() => Overlap(Rhs200, D.Rhs(100, 120, 6), D.Rhs(120, 120, 8), .5, -150000, 150000, true),
            "BRACE_TO_CHORD_SHEAR_CHECK_REQUIRED:Table7.8 note2"),
        ["RHS K joint with mixed brace shapes"] = (() => Two(Rhs200, D.Rhs(120, 120, 6), D.Chs(114.3, 6), 60), "MIXED_CHS_AND_RHS_BRACES_NOT_COVERED"),
        ["Brace moment on an RHS chord"] = (() => Single(Rhs200, D.Rhs(120, 120, 6), mip: 2e6), "BRACE_MOMENTS_NOT_COVERED_FOR_THIS_JOINT:BraceA"),
    };

    public static IEnumerable<object[]> Names => Cases.Keys.Select(k => new object[] { k });

    [DataTestMethod]
    [DynamicData(nameof(Names))]
    public void OutsideTheFieldIsRefusedWithAPreciseDiagnostic(string name)
    {
        var (input, expected) = Cases[name];
        var r = TubularJoint.Verify(input());
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status, name);
        Assert.IsFalse(r.IsComplete, name); Assert.IsNull(r.Utilization, name);
        Assert.AreEqual(0, r.Checks.Count, name + ": no resistance is evaluated outside the field");
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(expected)), name + " -> " + string.Join(" | ", r.Diagnostics));
    }

    [TestMethod]
    public void LimitsThatDependOnTheJointTypeOrDeclarationsAdmitTheOtherCase()
    {
        // X joints only: d0/t0 = 45 is admitted for a T joint (Table7.1 generally 50).
        D.Complete(TubularJoint.Verify(Single(D.Chs(225, 5), D.Chs(114.3, 6), -60000, chordN: 100000)));
        // Overlap 70 % with the hidden seam welded (lambda_ov,lim = 80 %).
        D.Complete(TubularJoint.Verify(Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .7, welded: true)));
        // A welded hidden seam lifts the 20 % rule of 7.3.1(2).
        D.Complete(TubularJoint.Verify(Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .5, -180000, 120000, true)));
        // Tension brace in an RHS overlap needs bi/ti <= 35 only, not Class 1: the Class 2 brace 120x120x4 (c/t = 27 > 33 eps)
        // overlaps in tension (lower t*fy, 7.1.2(7)) the Class 1 compression brace 120x120x8.
        D.Complete(TubularJoint.Verify(Overlap(Rhs200, D.Rhs(120, 120, 4), D.Rhs(120, 120, 8), .5, 150000, -150000, true)));
        // Compressed CHS brace of Class 2 (d/t = 41.1 > 50 eps^2 = 33.1, <= 70 eps^2 = 46.3) on a CHS chord.
        D.Complete(TubularJoint.Verify(Single(Chs168, D.Chs(114.3, 2.78), -40000)));
        // The same compressed chord of Class 3 is admitted in tension (Table7.1 tension row).
        D.Complete(TubularJoint.Verify(Single(D.Chs(168.3, 4.5, 460, 460, 540), D.Chs(114.3, 6), 50000, chordN: 300000)));
    }

    [TestMethod]
    public void TraceNamesAreUniqueInEveryEvaluatedJointType()
    {
        var inputs = new[] {
            Single(Chs168, D.Chs(114.3, 6)), Single(Chs168, D.Chs(88.9, 5), 120000, 60, 200000, mip: 1e6),
            Single(D.Chs(139.7, 5), D.Chs(76.1, 4), -60000, type: TubularJointType.X),
            Two(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 5), 20), Overlap(Chs168, D.Chs(88.9, 5), D.Chs(88.9, 6.3), .5),
            Single(Rhs200, D.Rhs(120, 120, 6)), Single(D.Rhs(160, 160, 8), D.Rhs(140, 140, 6), -250000, 60, -200000),
            Single(D.Rhs(200, 150, 8), D.Rhs(100, 150, 6), 200000, 45, 100000, TubularJointType.X),
            Two(Rhs200, D.Rhs(120, 120, 6), D.Rhs(120, 120, 6), 60), Overlap(Rhs200, D.Rhs(120, 120, 6), D.Rhs(120, 120, 8), .6, -300000, 300000, true) };
        foreach (var input in inputs)
        {
            var r = TubularJoint.Verify(input);
            Assert.AreEqual(0, r.Diagnostics.Count, input.Type + ": " + string.Join(";", r.Diagnostics));
            var duplicated = r.Trace.GroupBy(v => v.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.AreEqual(0, duplicated.Length, input.Type + ": " + string.Join(",", duplicated));
            Assert.AreEqual(r.Checks.Count, r.Checks.Select(c => c.Id).Distinct().Count());
        }
    }

    [TestMethod]
    public void RefusedFlagsDoNotHideOtherDiagnostics()
    {
        var r = TubularJoint.Verify(Single(Chs168, D.Chs(114.3, 6), multiplanar: true, reinforced: true, fatigue: true));
        Assert.AreEqual(3, r.Diagnostics.Count(d => d.EndsWith("NOT_COVERED", StringComparison.Ordinal) || d.Contains("_NOT_COVERED:")));
        Assert.IsFalse(r.Trace.Any(v => v.Name == "Chord.Sigma0Ed"), "No chord stress is computed for a refused situation.");
    }
}
