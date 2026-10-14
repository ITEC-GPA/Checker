using GPC.Design.Bridges;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text.Json;
namespace DesignTests;

[TestClass]
public sealed class TrafficEnvelopeTests
{
    [TestMethod]
    public void LaneLayoutFollowsNormativeBoundariesAndGlobalNumbering()
    {
        foreach (var (width, count, udl, axle) in new[] { (7.5, 2, 38.25, 500d), (9.5, 3, 43.25, 600d), (11.5, 3, 48.25, 600d), (5.4, 2, 31.05, 500d), (6d, 2, 34.5, 500d) })
        { var r = RoadTraffic.Ntc2018(width); Assert.AreEqual(count, r.Lanes.Count); Assert.AreEqual(udl, r.UniformKnPerMetre, 1e-10); Assert.AreEqual(axle, r.AxleKn); }
        var divided = RoadTraffic.Ntc2018(8.8, 8.8);
        Assert.AreEqual(4, divided.Lanes.Count); Assert.AreEqual(63.5, divided.UniformKnPerMetre, 1e-10);
        Assert.AreEqual(600, divided.AxleKn); Assert.AreEqual(100, divided.Lanes[2].AxleKn);
        Assert.ThrowsException<ArgumentException>(() => RoadTraffic.Ntc2018(double.NaN));
        Assert.ThrowsException<ArgumentException>(() => RoadTraffic.Ntc2018(2.9));
    }
    [TestMethod]
    public void SimplySupportedPointTrainAndContinuousUniformLoadsMatchClosedForms()
    {
        var tandem = new RoadTraffic([], 0, 0, 600);
        var beam = new BeamTrafficEnvelope([30], false, tandem);
        // Maximum M beneath an axle, x = L/2 - s/4. Total tandem = 1200 kN.
        Assert.AreEqual(1200 * 14.7 * 14.7 / 30, beam.At(0, 14.7, 1e8).Moment.Maximum, 1e-9);
        Assert.AreEqual(1200 * (1 - .6 / 30), beam.Reaction(0).Maximum, 1e-9);
        Assert.AreEqual(0, beam.At(0, 15, 1e8).Moment.Minimum, 1e-9);
        var udl = new RoadTraffic([], 0, 1, 0);
        var pair = new BeamTrafficEnvelope([30, 30], true, udl);
        Assert.AreEqual(-30 * 30 / 8d, pair.At(0, 30, 1e8).Moment.Minimum, 1e-9);
        Assert.AreEqual(1.25 * 30, pair.Reaction(1).Maximum, 1e-9);
        foreach (double[] spans in new[] { new[] { 30d }, [30d, 30], [24d, 30, 24], [16d, 20, 20, 20, 16] })
        {
            var actual = new BeamTrafficEnvelope(spans, true, udl).Calculate(1e8);
            var uniform = BridgeConcept.Analyze(spans, 1, 1e8, true);
            for (int k = 0; k < actual.Reactions.Count; k++)
                Assert.AreEqual(uniform.Reactions[k], actual.Reactions[k].Minimum + actual.Reactions[k].Maximum, 1e-9);
            foreach (var s in actual.Sections)
            {
                var expected = uniform.Stations.Single(p => p.Span == s.Span && Math.Abs(p.X - s.X) < 1e-9);
                Assert.AreEqual(expected.Moment, s.Moment.Minimum + s.Moment.Maximum, 1e-8);
                Assert.AreEqual(expected.Shear, s.Shear.Minimum + s.Shear.Maximum, 1e-9);
                Assert.AreEqual(expected.DeflectionMm, s.DeflectionMm.Minimum + s.DeflectionMm.Maximum, 1e-9);
            }
        }
        var def = new BeamTrafficEnvelope([30], false, udl).At(0, 15, 1e8).DeflectionMm;
        Assert.AreEqual(5 * Math.Pow(30, 4) / (384 * 1e8) * 1000, def.Maximum, 1e-9);
        Assert.IsTrue(pair.Reaction(0).Minimum < 0, "Unfavourable remote-span loading must expose uplift.");
    }
    [TestMethod]
    public void EnvelopesMatchIndependentPythonResearchAndRetainOppositeSigns()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures/traffic-research.json");
        Assert.AreEqual("7BB22B105C47E700D477B0FF5B50AC620151C61C19657F2073E34E51289D5335", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture))));
        var cases = JsonSerializer.Deserialize<ReferenceCase[]>(File.ReadAllText(fixture))!;
        foreach (var c in cases)
        {
            double[] spans = c.Schema switch { "appoggiata" => [c.L], "2 appoggiate" => [c.L, c.L], "2 continue" => [c.L, c.L], "3 continue" => [.8 * c.L, c.L, .8 * c.L], "5 continue" => [.8 * c.L, c.L, c.L, c.L, .8 * c.L], _ => throw new Exception(c.Schema) };
            var model = new BeamTrafficEnvelope(spans, c.Schema.Contains("continue"), RoadTraffic.Ntc2018(9.5));
            var r = model.Calculate(1e8);
            double actual = c.Effect switch {
                "M+ campata" => r.Sections.Max(s => s.Moment.Maximum),
                "M+ centrale" => r.Sections.Where(s => s.Span == spans.Length / 2 + 1).Max(s => s.Moment.Maximum),
                "M- appoggio" => -r.Sections.Min(s => s.Moment.Minimum),
                "R pila" => r.Reactions.Skip(1).SkipLast(1).Max(s => s.Maximum),
                "R spalla" => r.Reactions[0].Maximum,
                _ => throw new Exception(c.Effect) };
            // Independent study used load grid 0.05 m and sections 0.10 m. Native positions are exact;
            // 41 displayed sections/span introduce <0.2% spatial sampling error for these cases.
            Assert.AreEqual(c.Expected, actual, Math.Abs(c.Expected) * .002, $"{c.Schema}/{c.L}/{c.Effect}");
        }
        var watch = Stopwatch.StartNew();
        var full = new BeamTrafficEnvelope([24, 30, 24], true, RoadTraffic.Ntc2018(8.8, 8.8)).Calculate(1e8);
        Console.WriteLine($"Three-span complete traffic envelope: {watch.Elapsed.TotalMilliseconds:F3} ms, {full.Sections.Count} sections");
        Assert.IsTrue(full.Sections.Any(s => s.Moment.Minimum < 0 && s.Moment.Maximum > 0));
        Assert.ThrowsException<OperationCanceledException>(() => new BeamTrafficEnvelope([30], false, RoadTraffic.Ntc2018(9.5)).Calculate(1e8, new CancellationToken(true)));
    }
    public sealed record ReferenceCase(string Schema, double L, string Effect, double Expected);
}
