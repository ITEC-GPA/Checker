namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    private static double EquivalentMomentTraffic(RoadTraffic t, double span) => t.UniformKnPerMetre + 4 * t.AxleKn / span * Math.Pow(1 - .6 / span, 2);
    private static double EquivalentShearTraffic(RoadTraffic t, double span) => t.UniformKnPerMetre + 4 * t.AxleKn / span * (1 - .6 / span);
    private static PlaneTraffic OuterPlane(BridgeInput i)
    {
        double carriage = i.Lanes * i.LaneWidth + 2 * i.Shoulder;
        double width = carriage + i.Median + 2 * i.BarrierWidth;
        return i.Median > 0 ? RoadTraffic.OuterPlane(width, new(i.BarrierWidth, carriage / 2), new(i.BarrierWidth + carriage / 2 + i.Median, carriage / 2))
            : RoadTraffic.OuterPlane(width, new CarriagewayStrip(i.BarrierWidth, carriage));
    }
    private static BeamLoadResult UpperLoads(BridgeModel data, string id, double[] spans, double permanent, UpperStructure structure, bool factored)
    {
        double gg = factored ? data.Assumptions.GammaG : 1, gq = factored ? data.Assumptions.GammaQ : 1;
        if (data.Input.TrafficModel == TrafficModel.LegacyUniform)
        {
            double width = data.Input.Lanes * data.Input.LaneWidth + 2 * data.Input.Shoulder + data.Input.Median + 2 * data.Input.BarrierWidth;
            double live = width * data.Assumptions.TrafficLoad;
            var r = ExtendedReactions(id, spans, gg * permanent + gq * live, structure);
            return new(r, r, []);
        }
        var t = TrafficLayout(data.Input); var g = ExtendedReactions(id, spans, gg * permanent, structure);
        if (!HasTowers(id))
        {
            var traffic = TrafficEnvelope(spans, false, t);
            return new(g.Zip(traffic.Reactions, (r, q) => r + gq * q.Maximum).ToArray(), g.Zip(traffic.Reactions, (r, q) => r + gq * q.Minimum).ToArray(), []);
        }
        var maximum = (double[])g.Clone(); var minimum = (double[])g.Clone();
        if (id == "cable_stayed")
        {
            // Conceptual tributary half-bridge to each tower. One complete tandem, never once per panel.
            maximum[1] += gq * (t.UniformKnPerMetre * spans.Sum() / 2 + 2 * t.AxleKn);
            maximum[2] = maximum[1];
        }
        else
        {
            double side = spans[0], main = spans[1];
            double hq = EquivalentMomentTraffic(t, main) * main * main / (8 * structure.Rise);
            double back = hq * structure.Tower / side;
            // Conservative separate bounds of the idealized suspension equilibrium. They are not concomitant.
            minimum[0] -= gq * back; minimum[3] = minimum[0];
            maximum[0] += gq * (t.UniformKnPerMetre * side / 2 + 2 * t.AxleKn); maximum[3] = maximum[0];
            maximum[1] += gq * (t.UniformKnPerMetre * (side + main) / 2 + 2 * t.AxleKn + back); maximum[2] = maximum[1];
        }
        return new(maximum, minimum, []);
    }
}
