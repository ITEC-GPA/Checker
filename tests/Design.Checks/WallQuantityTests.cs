using System.Security.Cryptography;
using System.Text.Json.Nodes;
using GPC.Design.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Design.Checks.WallDetailTests;

namespace Design.Checks;
[TestClass]
public class WallQuantityTests
{
    [TestMethod]
    public void UndetailedMassRetainsTwentyTwoFrozenWallResults()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/wall-primary-mass-f4.json"));
        Assert.AreEqual("804828000300C2CC318A89E76A4118A8E29CD1683A4A7D57C07DFDE66C5E0901",RuntimeCompatibility.Sha256Hex(bytes));
        var rows=Fixture().OfType<JsonObject>().ToDictionary(r=>S(r,"id"));
        var expected=JsonNode.Parse(bytes)!.AsArray();Assert.AreEqual(22,expected.Count);
        foreach(var row in expected.OfType<JsonObject>()) Same(D(row,"steelKg"),WallRebarQuantities.PrincipalSteelKgPerMetre(Input(rows[S(row,"id")])));
    }
    [TestMethod]
    public void StandardDensityAndExplicitFacesGiveExpectedKilograms()
    {
        var p=Input(Fixture()[0]!); var zones=((WallRebarZone[])Enum.GetValues(typeof(WallRebarZone))).ToDictionary(z=>z,z=>new WallZoneBars(new(10,4),new(20,6),10,200));
        p=p with {Reinforcement=new(false,1,zones),Geometry=new(3,.45,.8,.4,.25,1.8)};
        // Total axial length = 3 + (0.8+0.2) + (1.8+0.2) = 6 m;
        // combined steel area = (4*10²+6*20²)*pi/4 = 700*pi mm².
        Same(700*Math.PI*6*.00785,WallRebarQuantities.PrincipalSteelKgPerMetre(p));
    }
}
