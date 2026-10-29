using GPC.Model.Design.Combinations;

namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    /// <summary>
    /// The ultimate rows of a model with <see cref="PartialFactorSource.Ntc2018RoadBridges"/>: the factors of the structural permanent actions
    /// (G1: concrete, steel and upper structure), of the non structural ones (G2: surfacing and barriers) and of the traffic of every ultimate row
    /// of GPC.Model (RoadBridgeCombinations.Deck, set A1 of NTC 2018 Tab. 5.1.V), with the unfavourable factors of the set. ANTHEA census of the
    /// combinations, D8 (decision of the user of 29/10/2026: «d8 e bridge design devono usare model»).
    /// </summary>
    private sealed record ProfileFactors(double Structural, double NonStructural, double Traffic, (double G1, double G2, double Q)[] Rows);

    private static readonly Lazy<ProfileFactors> RoadBridgeA1 = new(() =>
    {
        var set = RoadBridgeCombinations.Structural;
        var rows = RoadBridgeCombinations.Deck(set).Where(r => r.State == GeotechnicalLimitState.Ultimate)
            .Select(r => (r.Factors["g1"], r.Factors["g2"], r.Factors["traffic"])).ToArray();
        return new(set.StructuralUnfavourable, set.NonStructuralUnfavourable, set.TrafficUnfavourable, rows);
    });

    private static bool FromProfile(BridgeModel model) => model.Input.PartialFactors == PartialFactorSource.Ntc2018RoadBridges;

    /// <summary>
    /// The assumptions with the unfavourable factors of the profile (γG = γG1 1.35, γQ = 1.35) for the formulas that use only the unfavourable
    /// factors (members of the upper structures, towers); the multipliers of the project otherwise.
    /// </summary>
    private static BridgeAssumptions UnfavourableAssumptions(BridgeModel model) =>
        FromProfile(model) ? model.Assumptions with { GammaG = RoadBridgeA1.Value.Structural, GammaQ = RoadBridgeA1.Value.Traffic } : model.Assumptions;

    /// <summary>The design permanent load with the unfavourable factors: γG1 G1 + γG2 G2 with the profile, γG (G1 + G2) otherwise.</summary>
    private static double DesignPermanent(BridgeModel model, double permanent, double nonStructural) => FromProfile(model)
        ? RoadBridgeA1.Value.Structural * (permanent - nonStructural) + RoadBridgeA1.Value.NonStructural * nonStructural
        : model.Assumptions.GammaG * permanent;

    /// <summary>
    /// Ultimate envelopes of the deck beam with the rows of the profile: for every reaction and section the maximum and the minimum over the rows
    /// of γG1 G1 + γG2 G2 + γQ Q, with Q the maximum or the minimum of the traffic (Schema 1 envelope, or the uniform traffic of the legacy model).
    /// G1, G2 and the uniform traffic from one analysis with a unit load (linear beam).
    /// </summary>
    private static BeamLoadResult ProfileLoads(BridgeModel model, double[] spans, double permanent, double nonStructural, double ei)
    {
        var i = model.Input; var rows = RoadBridgeA1.Value.Rows; double structural = permanent - nonStructural;
        var unit = Analyze(spans, 1, ei, i.Continuous);
        double Max(double g, double qMax) => rows.Max(r => r.G1 * structural * g + r.G2 * nonStructural * g + r.Q * qMax);
        double Min(double g, double qMin) => rows.Min(r => r.G1 * structural * g + r.G2 * nonStructural * g + r.Q * qMin);
        if (i.TrafficModel == TrafficModel.LegacyUniform)
        {
            double width = i.Lanes * i.LaneWidth + 2 * i.Shoulder + i.Median + 2 * i.BarrierWidth, live = width * model.Assumptions.TrafficLoad;
            var maxima = unit.Reactions.Select(r => Max(r, live * r)).ToArray(); var minima = unit.Reactions.Select(r => Min(r, live * r)).ToArray();
            var legacyStations = unit.Stations.Select(s => new Station(s.Span, s.X, Max(s.Moment, live * s.Moment), Max(s.Shear, live * s.Shear), Max(s.DeflectionMm, live * s.DeflectionMm))
            { MomentMinimum = Min(s.Moment, live * s.Moment), ShearMinimum = Min(s.Shear, live * s.Shear), DeflectionMinimumMm = Min(s.DeflectionMm, live * s.DeflectionMm) }).ToArray();
            return new(maxima, minima, legacyStations);
        }
        var q = TrafficEnvelope(spans, i.Continuous, TrafficLayout(i));
        var reactionMaxima = unit.Reactions.Zip(q.Reactions, (g, t) => Max(g, t.Maximum)).ToArray();
        var reactionMinima = unit.Reactions.Zip(q.Reactions, (g, t) => Min(g, t.Minimum)).ToArray();
        var fixedStations = unit.Stations.ToDictionary(s => (s.Span, s.X));
        double offset = 0;
        for (int k = 0; k < spans.Length; k++)
        {
            offset += spans[k];
            fixedStations[(k + 1, offset)] = unit.Stations.Last(s => s.Span == k + 1);
        }
        var stations = q.Sections.Select(t =>
        {
            var p = fixedStations[(t.Span, t.X)];
            return new Station(t.Span, t.X, Max(p.Moment, t.Moment.Maximum), Max(p.Shear, t.Shear.Maximum), Max(p.DeflectionMm, t.DeflectionMm.Maximum / ei))
            { MomentMinimum = Min(p.Moment, t.Moment.Minimum), ShearMinimum = Min(p.Shear, t.Shear.Minimum), DeflectionMinimumMm = Min(p.DeflectionMm, t.DeflectionMm.Minimum / ei) };
        }).ToArray();
        return new(reactionMaxima, reactionMinima, stations);
    }

    /// <summary>
    /// Ultimate reactions of the upper structures with the rows of the profile: G1 and G2 through the same reaction scheme with a unit load, the
    /// traffic as today (uniform traffic, Schema 1 envelope, or the tributary bounds of the towers), maximum and minimum over the rows.
    /// </summary>
    private static BeamLoadResult ProfileUpperLoads(BridgeModel data, string id, double[] spans, double permanent, double nonStructural, UpperStructure structure)
    {
        var rows = RoadBridgeA1.Value.Rows; double structural = permanent - nonStructural;
        var unit = ExtendedReactions(id, spans, 1, structure);
        double[] trafficMaximum, trafficMinimum;
        if (data.Input.TrafficModel == TrafficModel.LegacyUniform)
        {
            double width = data.Input.Lanes * data.Input.LaneWidth + 2 * data.Input.Shoulder + data.Input.Median + 2 * data.Input.BarrierWidth;
            trafficMaximum = ExtendedReactions(id, spans, width * data.Assumptions.TrafficLoad, structure); trafficMinimum = trafficMaximum;
        }
        else
        {
            var t = TrafficLayout(data.Input);
            if (!HasTowers(id))
            {
                var traffic = TrafficEnvelope(spans, false, t);
                trafficMaximum = traffic.Reactions.Select(r => r.Maximum).ToArray(); trafficMinimum = traffic.Reactions.Select(r => r.Minimum).ToArray();
            }
            else
            {
                trafficMaximum = new double[unit.Length]; trafficMinimum = new double[unit.Length];
                if (id == "cable_stayed")
                {
                    trafficMaximum[1] = t.UniformKnPerMetre * spans.Sum() / 2 + 2 * t.AxleKn; trafficMaximum[2] = trafficMaximum[1];
                }
                else
                {
                    double side = spans[0], main = spans[1];
                    double hq = EquivalentMomentTraffic(t, main) * main * main / (8 * structure.Rise), back = hq * structure.Tower / side;
                    trafficMinimum[0] = -back; trafficMinimum[3] = trafficMinimum[0];
                    trafficMaximum[0] = t.UniformKnPerMetre * side / 2 + 2 * t.AxleKn; trafficMaximum[3] = trafficMaximum[0];
                    trafficMaximum[1] = t.UniformKnPerMetre * (side + main) / 2 + 2 * t.AxleKn + back; trafficMaximum[2] = trafficMaximum[1];
                }
            }
        }
        var maxima = unit.Select((g, k) => rows.Max(r => r.G1 * structural * g + r.G2 * nonStructural * g + r.Q * trafficMaximum[k])).ToArray();
        var minima = unit.Select((g, k) => rows.Min(r => r.G1 * structural * g + r.G2 * nonStructural * g + r.Q * trafficMinimum[k])).ToArray();
        return new(maxima, minima, []);
    }
}
