using GPC.Checkers.Steel.Connections;

namespace Steel.Members.Tests;

internal static class JointClassificationTestData
{
    internal static ConnectionRolledISection Column(double h = 330, double b = 300,
        double tw = 9.5, double tf = 16.5, double r = 27, double fy = 235)
        => new(h, b, tw, tf, r, 2 * b * tf + (h - 2 * tf) * tw + (4 - Math.PI) * r * r, fy, Math.Max(360, fy + 100));

    internal static EndPlateRotationalStiffnessResult JrcStiffness(double moment = 0, bool supported = true)
    {
        double[] h = [527, 442, 362], lc = [166.05, 82.5, 163.55], lp = [120, 234.69, 175.98];
        var rows = Enumerable.Range(0, 3).Select(i => new EndPlateStiffnessRow(h[i],
            new JointStiffnessEffectiveLength([lc[i]], "JRC96658 Table3.8 minima incl relevant groups"),
            new JointStiffnessEffectiveLength([lp[i]], "JRC96658 Table3.8 minima incl relevant groups"),
            33.65, i == 0 ? 25.95 : 49.24, 15, 353, 48.5, "JRC96658 example3.3 geometry"));
        return EndPlateRotationalStiffness.Calculate(new EndPlateRotationalStiffnessInput(
            ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EC test policy"), Column(),
            new ColumnWebEffectiveWidth(274.81, "JRC96658 compression dispersion"),
            ColumnWebPanelConfiguration.SingleSidedJoint, 1, rows, 0, 1e6, moment, 300e6,
            "Single sided beta1", "Npl1MN/Mj300kNm explicit TEST values, not published resistance",
            supported ? "JRC96658 column flange detail and selected tension side" : ""));
    }

    internal static JointFrameStoreyStiffness Storey(double ratio)
        => new([new JointFrameMemberStiffness(ratio * 1000, 1000)],
            [new JointFrameMemberStiffness(1000, 1000)], "All members of this test storey; same frame plane");

    internal static JointFrameClassificationContext Frame(double reduction = .8,
        IEnumerable<JointFrameStoreyStiffness>? storeys = null, bool all = true, string source = "Same load, frame and direction; lateral displacement comparison")
        => new(reduction, storeys ?? [Storey(.1)], all, source);

    internal static EndPlateJointResistanceEvidence Resistance(double mj = 150e6, double mb = 300e6,
        double mc = 200e6, double n = 0, EndPlateGoverningMechanism mode = EndPlateGoverningMechanism.EndPlateBending,
        IEnumerable<EndPlateGoverningMechanism>? modes = null, ConnectionRolledISection? column = null,
        ConnectionPlate? plate = null, ConnectionBolt? bolt = null, double columnFlangeFy = 235,
        JointResistanceEvidenceScope scope = JointResistanceEvidenceScope.CompleteJoint,
        string detail = "Qualified TEST end-plate at column flange, strong axis, positive moment",
        string resistance = "External complete-joint TEST assessment including all modes and adjacent members; N/V/M concomitant",
        string governing = "All critical ultimate-resistance mechanisms, not zero-load utilization")
        => new(ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EC test policy"), column ?? Column(),
            plate ?? new ConnectionPlate(250, 15, 235, 360),
            bolt ?? new ConnectionBolt(24, 26, 353, ConnectionBoltGrade.Class10_9, true),
            columnFlangeFy, mj, mb, mc, 1e6, n, scope, modes ?? [mode], detail, resistance, governing);

    internal static EndPlateRotationCapacityResult Rotation(EndPlateJointResistanceEvidence evidence,
        JointNominalSteelGrade grade = JointNominalSteelGrade.S235, string source = "Nominal grades for actual members and plate")
        => EndPlateRotationCapacity.Assess(new EndPlateRotationCapacityInput(evidence, grade, grade, grade, source));
}
