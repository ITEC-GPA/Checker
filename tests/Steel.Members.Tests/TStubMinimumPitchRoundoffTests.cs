using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class TStubMinimumPitchRoundoffTests
{
    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)]
    [DataRow(true, false)] [DataRow(true, true)]
    public void ExactMinimumPitchRemainsValidAfterCoordinateTranslation(bool ntc, bool distributed)
    {
        // Physical p=106.8-44.4=62.4=2.4*d0. Adding tf=15.6 before
        // subtraction produces62.39999999999999 in IEEE754 arithmetic.
        var geometry = Geometry(0);
        Assert.IsTrue(geometry.MinimumRowPitch < 2.4 * 26);
        var input = Input(geometry, ntc, distributed);
        Assert.AreEqual(ConnectionStatus.Passed, TStubBoltRows.Verify(input).Status);
        Assert.AreEqual(2, input.OrderedRows.Count);
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(false, true)]
    [DataRow(true, false)] [DataRow(true, true)]
    public void RealPitchDeficitIsRejectedRatherThanTreatedAsFabricationTolerance(bool ntc, bool distributed)
    {
        // A deficit of1e-6mm is still thousands of times larger than the
        // bounded arithmetic roundoff. No detailing tolerance is granted.
        Assert.ThrowsException<ArgumentException>(() => Input(Geometry(-1e-6), ntc, distributed));
    }

    private static TStubDerivedGeometry Geometry(double delta)
        => TStubEffectiveGeometry.UnstiffenedColumnFlange(new(258.8,12.8,12.7,250,100,
            new[] {44.4+15.6,106.8+15.6+delta},400,400,
            "Analytical minimum-pitch boundary after beam-to-column coordinate translation"));
    private static TStubBoltRowsInput Input(TStubDerivedGeometry geometry, bool ntc, bool distributed)
        => geometry.CreateInput(ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1,1.1,1.25,"EC test factors"),
            new(24,26,353,ConnectionBoltGrade.Class8_8,true),20.5,265,new[] {0.0,0},
            distributed ? TStubMode1Method.DistributedContact : TStubMode1Method.PointForce,distributed ? 39.55 : 0,
            holeClearanceServiceabilitySource: "Qualified test clearance SLE");
}
