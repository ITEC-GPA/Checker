using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D = Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

/// <summary>Independent published resistances: Corus Tubes, Design of SHS welded joints,
/// chapter 6, pp35-42, Celsius275, 55 degrees. The book predates EN1993-1-8:2005;
/// only the unchanged component formulae are compared, not its full design procedure.
/// https://usermanual.wiki/Document/DesignofStructualHollowSectionsWeldedJoints.1925965821.pdf
/// Chord forces below enforce exact equilibrium of the rounded published brace forces,
/// and keep both chord cuts in tension (kp=kn=1). They do not reproduce the entire girder.
/// Tolerance 0.11kN covers the printed 0.1kN precision and rounded gap dimensions;
/// it is not used by the existing high precision hand-calculation tests.</summary>
[TestClass]
public class TubularJointPublishedExamplesTests
{
    [DataTestMethod]
    [DataRow(139.7, 5.0, 12.5, 548.0, 577.7, 944.6)] // p41, joint6
    [DataRow(114.3, 5.0, 13.9, 427.0, 492.9, 772.8)] // p41, joint7; brace IDs misprinted in the table, sizes on p40
    [DataRow(88.9, 3.2, 44.9, 183.0, 360.9, 601.1)] // p41, joint9
    [DataRow(168.3, 5.0, 12.5, 548.0, 670.3, 1138.0)] // p42, revised joint6
    public void CorusCircularKGapComponents(double diameter, double thickness, double gap,
        double braceKn, double faceKn, double punchingKn)
    {
        var chord = D.Chs(219.1, 10, 275, 275, 430);
        var brace = D.Chs(diameter, thickness, 275, 275, 430);
        double force = braceKn * 1000;
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, chord, D.Cut(1200000),
            D.Cut(1200000 - 2 * force * Math.Cos(55 * Math.PI / 180)),
            D.Brace(brace, 55, -force), D.Brace(brace, 55, force), gap));
        D.Complete(r);
        D.Close(1, D.Trace(r, "Joint.kp"));
        foreach (var side in new[] { "BraceA", "BraceB" })
        {
            Published(faceKn, D.Check(r, side + ".ChordFace").Resistance);
            Published(punchingKn, D.Check(r, side + ".PunchingShear").Resistance);
        }
    }

    [DataTestMethod]
    [DataRow(80.0, 8.0, 548.0, 609.9, 828.6, 633.6)] // p38, joint6
    [DataRow(90.0, 5.0, 427.0, 686.1, 932.2, 467.5)] // p38, joint7
    [DataRow(70.0, 3.0, 183.0, 533.7, 725.0, 221.1)] // p38, joint9
    public void CorusSquareKGapComponents(double width, double thickness, double braceKn,
        double faceKn, double punchingKn, double effectiveWidthKn)
    {
        var chord = D.Rhs(150, 150, 12.5, 275, 275, 430);
        var brace = D.Rhs(width, width, thickness, 275, 275, 430);
        double force = braceKn * 1000;
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, chord, D.Cut(1200000),
            D.Cut(1200000 - 2 * force * Math.Cos(55 * Math.PI / 180)),
            D.Brace(brace, 55, -force), D.Brace(brace, 55, force), 40));
        D.Complete(r);
        D.Close(1, D.Trace(r, "Joint.kn"));
        foreach (var side in new[] { "BraceA", "BraceB" })
        {
            Published(faceKn, D.Check(r, side + ".ChordFace").Resistance);
            Published(821.8, D.Check(r, side + ".ChordShear").Resistance);
            Published(punchingKn, D.Check(r, side + ".PunchingShear").Resistance);
            Published(effectiveWidthKn, D.Check(r, side + ".BraceFailure").Resistance);
        }
    }

    private static void Published(double expectedKn, double actualN)
        => Assert.AreEqual(expectedKn, actualN / 1000, .11, "Corus published component resistance [kN]");
}
