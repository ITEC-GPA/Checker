using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateNmvTests
{
    [DataTestMethod][DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void ActualAxialMomentAndShearUseAllPhysicalComponents(bool ntc, bool flush)
    {
        var g = EndPlateNmvCaseData.Geometry(ntc, flush);
        Assert.IsTrue(g.IsSupported, string.Join(";", g.Diagnostics));
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, Details(r));
        Assert.IsTrue(r.IsComplete); Assert.IsTrue(r.CanQualifyWholeJoint); Assert.IsFalse(r.CanQualifyWholeMember);
        Assert.IsTrue(r.Equilibrium.IsConverged); Assert.AreEqual(40000, r.Equilibrium.AxialForce);
        Assert.AreEqual(flush ? 3 : 4, r.Equilibrium.Rows.Count);
        Assert.IsTrue(r.Checks.Any(c => c.Id.Contains("Rows") && c.Id.Contains("TStub")));
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("InPlane.") && c.Id.Contains("Block")));
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("Bolts.")));
        Assert.IsTrue(r.Checks.Any(c => c.Id == "Contact0.ColumnWeb"));
        Assert.IsTrue(r.Checks.Any(c => c.Id == "Contact1.ColumnWeb"));
        Assert.IsTrue(r.AxialTensionResistance > 0 && r.AxialCompressionResistance > 0);
    }

    [DataTestMethod][DataRow(-40000.0,-5e6)][DataRow(-40000.0,0.0)][DataRow(-40000.0,5e6)]
    [DataRow(0.0,-5e6)][DataRow(0.0,0.0)][DataRow(0.0,5e6)]
    [DataRow(40000.0,-5e6)][DataRow(40000.0,0.0)][DataRow(40000.0,5e6)]
    public void SignedOrdinaryCasesHaveEquilibriumAndConservativeInteraction(double n, double m)
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, n, m));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, Details(r));
        double sumN = r.Equilibrium.Rows.Sum(x => x.Tension) - r.Equilibrium.Contacts.Sum(x => x.Compression);
        double sumM = r.Equilibrium.Rows.Sum(x => x.Tension * x.Row.Z) + r.Equilibrium.Contacts.Sum(x => x.SignedMoment);
        Assert.AreEqual(n, sumN, 1e-4); Assert.AreEqual(m, sumM, .01);
        double nr = n >= 0 ? r.AxialTensionResistance!.Value : r.AxialCompressionResistance!.Value;
        double mr = m >= 0 ? r.PositiveMomentResistance!.Value : r.NegativeMomentResistance!.Value;
        Assert.AreEqual(Math.Abs(n) / nr + Math.Abs(m) / mr, r.Checks.Single(c => c.Id == "Joint.ConservativeNMInteraction").Utilization, 1e-12);
    }

    [DataTestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void AllThreeColumnEquilibriumConditionsAreEnforced(int which)
    {
        var p = EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry()); var lo = p.ColumnLower;
        var wrong = new SteelActions(lo.N + (which == 0 ? 1000 : 0), 0, lo.V2 + (which == 1 ? 1000 : 0), 0, lo.M1 + (which == 2 ? 1000 : 0), 0);
        var r = EndPlateNmv.Verify(new(p.Geometry, p.BeamFaceActions, p.ColumnUpper, wrong, p.CombinationSource, p.ColumnReferenceSource));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsNull(r.Equilibrium); Assert.IsNull(r.PositiveMomentResistance);
        Assert.IsTrue(r.Diagnostics.Any(x => x.Contains("Jump")));
    }

    [TestMethod]
    public void FaceShearArmCannotBeSilentlyDropped()
    {
        var p = EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry());
        var r = EndPlateNmv.Verify(p);
        Assert.AreEqual(5e6 - 20000 * 266.7 / 2, r.Trace.Single(x => x.Name == "MomentAtColumnCentroid").Value, 1e-8);
        var upper = new SteelActions(p.ColumnUpper.N, 0, p.ColumnUpper.V2, 0, -2.5e6, 0);
        var lower = new SteelActions(p.ColumnLower.N, 0, p.ColumnLower.V2, 0, 2.5e6, 0);
        var wrong = EndPlateNmv.Verify(new(p.Geometry, p.BeamFaceActions, upper, lower, p.CombinationSource, p.ColumnReferenceSource));
        Assert.AreEqual(ConnectionStatus.NotSupported, wrong.Status);
    }

    [DataTestMethod][DataRow("shearOnly")][DataRow("bearingOnly")][DataRow("gap")][DataRow("slender")][DataRow("noSource")][DataRow("nearLowerFlange")]
    public void AnExistingPartialOrOutOfScopeDetailIsNeverPromoted(string reason)
    {
        var g = EndPlateNmvCaseData.Geometry(fullWelds: reason != "bearingOnly", shearOnlyLastRow: reason == "shearOnly", gap: reason == "gap" ? .01 : 0,
            webThickness: reason == "slender" ? 10.1 : 20, nearLowerFlangeRow: reason == "nearLowerFlange", contactSource: reason == "noSource" ? "" : "Actual contact");
        Assert.IsFalse(g.IsSupported);
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g));
        Assert.AreNotEqual(ConnectionStatus.Passed, r.Status); Assert.IsFalse(r.IsComplete); Assert.IsNull(r.PositiveMomentResistance);
    }

    [TestMethod]
    public void SmallContactAndOverloadedColumnCannotPass()
    {
        var contact = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0, contactWidth: 1), -40000, 0));
        Assert.AreEqual(ConnectionStatus.Failed, contact.Status, Details(contact));
        Assert.IsTrue(contact.Checks.Any(x => x.Id.Contains("PeakPressure") && !x.Passed));
        var column = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(), columnN: -4e6));
        Assert.AreEqual(ConnectionStatus.Failed, column.Status);
        Assert.IsTrue(column.Checks.Any(x => x.Id.StartsWith("Column.Cut") && !x.Passed));
    }

    [TestMethod]
    public void AxialLimitIsContinuousAtZeroWithoutChangingTheOldPureBendingApi()
    {
        var g = EndPlateNmvCaseData.Geometry(shear: 0);
        var a = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, 0));
        foreach (double n in new[] { -1e-3, 1e-3 })
        {
            var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, n));
            Assert.AreEqual(ConnectionStatus.Passed, r.Status, Details(r));
            Assert.AreEqual(a.Equilibrium.Rotation, r.Equilibrium.Rotation, 1e-9);
            Assert.AreEqual(a.Equilibrium.Rows[0].Tension, r.Equilibrium.Rows[0].Tension, .01);
        }
        Assert.IsFalse(g.Baseline.MomentComponents.CanQualifyWholeJoint);
        Assert.AreEqual(0, g.Baseline.Input.Axial);
    }

    [TestMethod]
    public void RetainedShearIsBoundToThePreparedFullStrengthEnvelopes()
    {
        var g = EndPlateNmvCaseData.Geometry();
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(g, shear: 20001));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsTrue(r.Diagnostics.Contains("PreparedShearEnvelopeDoesNotMatchActualCase"));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void BeamStressAndRetainedShearMarginHaveIndependentRectangleOracles(bool ntc)
    {
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(ntc)));
        double area = 2 * 209.3 * 15.6 + 501.9 * 20;
        double z = (533.1 - 15.6) / 2;
        double inertia = 2 * (209.3 * Math.Pow(15.6, 3) / 12 + 209.3 * 15.6 * z * z) + 20 * Math.Pow(501.9, 3) / 12;
        double modulus = inertia / (533.1 / 2);
        double normal = 40000 / area + 5e6 / modulus;
        double tau = 1.5 * 20000 / (20 * (501.9 - 2 * 12.7));
        double limit = Math.Min(275 / (ntc ? 1.05 : 1) - Math.Sqrt(3) * tau, .9 * 410 / (ntc ? 1.25 : 1.1));
        var check = r.Checks.Single(c => c.Id == "Beam.ElasticNMAtRetainedV");
        Assert.AreEqual(normal, check.Demand, 1e-8); Assert.AreEqual(limit, check.Resistance, 1e-8);
    }

    [DataTestMethod][DataRow(-40000.0)][DataRow(40000.0)]
    public void PureAxialCasesHaveTheIndependentMonotoneColumnShearEnvelope(double n)
    {
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0), n, 0));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, Details(r));
        Assert.AreEqual(20000, r.Trace.Single(v => v.Name == "Column.PanelShearEnvelope").Value, 1e-5);
        if (n > 0) Assert.IsTrue(r.Equilibrium.Contacts.All(c => c.Compression == 0));
        else { Assert.IsTrue(r.Equilibrium.Rows.All(c => c.Tension == 0)); foreach (var contact in r.Equilibrium.Contacts) Assert.AreEqual(15.6, contact.ClosedLength, 1e-10); }
        double area = (258.8 + 206.8) * 20.5 + 225.7 * 12.8;
        double centroid = (206.8 - 258.8) * 20.5 * 123.1 / area;
        Assert.AreEqual(area, r.Trace.Single(v => v.Name == "Column.NetArea").Value, 1e-7);
        Assert.AreEqual(centroid, r.Trace.Single(v => v.Name == "Column.NetCentroidShift").Value, 1e-10);
    }

    [TestMethod]
    public void AsymmetricPlateDoesNotReuseThePositiveMomentCapacityForReversal()
    {
        var r = EndPlateNmv.Verify(EndPlateNmvCaseData.Input(EndPlateNmvCaseData.Geometry(shear: 0), 0, 5e6));
        Assert.IsTrue(Math.Abs(r.PositiveMomentResistance!.Value - r.NegativeMomentResistance!.Value) > 1e5);
    }

    internal static string Details(EndPlateNmvResult r) => string.Join(";", r.Diagnostics) + " | " +
        string.Join(";", r.Checks.Where(c => !c.Passed).Select(c => c.Id + "=" + c.Utilization));
}
