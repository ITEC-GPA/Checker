using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace Steel.Members.Tests;

internal static class EndPlateInitialCaseTestData
{
    internal static EndPlateMomentWithBeamSectionResult Parent(bool ntc = false, bool cjp = false,
        double moment = 100e6, double shear = 300000, double beta = 0, double elasticModulus = 210000,
        string actionSource = "Actual shared test ULS case", double? mismatchedJointMoment = null)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "SCI C1 policy");
        var shape = new SectionH(533.1, 10.1, 209.3, 15.6, 209.3, 15.6, "SCI C1", 12.7);
        double area = 2 * 209.3 * 15.6 + 501.9 * 10.1 + (4 - Math.PI) * 12.7 * 12.7;
        var beam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, area, 275, 410);
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("Actual material", elasticModulus, 275, 410),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished), true);
        var beamResult = EndPlateBeamSectionAdapter.Evaluate(new(code, beam, section, new(0, 0, shear, 0, moment, 0),
            275, 410, false, "Actual web/flange strengths", "SCI C1 actual beam outline", "Actual Model action set",
            ntc ? null : 1.2, ntc ? null! : "EC eta1.2"));
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        var joint = new EndPlateMomentComponentInput(policy, new(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410),
            beam, 265, new(250, 25, cjp ? 345 : 265, cjp ? 470 : 410),
            new(24, 26, 353, ConnectionBoltGrade.Class8_8, true),
            new(250, 258.8, 100, 10.1, 8, 12, 8, 501.9, new[] { 44.4, 134.4 },
                new(7.5, "SCI C1 graphical alpha"), "C1 geometry; column continuation1000mm is test detail"),
            cjp ? null! : new(250, 100, 40, 12, 50, "C1 extended row"), 1000, 1000, 46.9,
            beamResult.DerivedResistance, mismatchedJointMoment ?? moment, Math.Abs(shear), 0, beta, 0,
            beta == 1 ? ColumnWebPanelConfiguration.SingleSidedJoint : ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Explicit concomitant beta" + beta, "Actual zero longitudinal column stress", "Qualified sway restraint", actionSource,
            TStubMode1Method.DistributedContact, 39.55, "NTC hole clearance evidence");
        return EndPlateMomentWithBeamSection.Verify(joint, beamResult);
    }

    internal static JointStiffnessEffectiveLength Length(params double[] values)
        => new(values, "Independent EN Tables6.4/6.6/6.11 contribution of THIS row; derive_end_plate_initial_case.py");
    internal static EndPlateStiffnessRowDetail[] Details(bool cjp = false, double boltLength = 70.5)
    {
        const string hardware = "Test actual grip25+20.5+washers4+4+half(head15+nut19)=70.5mm; not a published C1 hardware dimension";
        if (cjp) return new[] {
            new EndPlateStiffnessRowDetail(0, Length(210.10971667208534, 161.505), Length(242.21679359177306, 210.15), boltLength, hardware),
            new(1, Length(210.10971667208534, 161.505), Length(242.21679359177306, 168.975), boltLength, hardware) };
        return new[] {
            new EndPlateStiffnessRowDetail(0, Length(210.10971667208534, 166.505, 166.505), Length(125), boltLength, hardware),
            new(1, Length(210.10971667208534, 166.505, 161.505, 95), Length(242.21679359177306, 210.15), boltLength, hardware),
            new(2, Length(210.10971667208534, 161.505, 161.505), Length(242.21679359177306, 168.975), boltLength, hardware) };
    }
    internal static EndPlateInitialStiffnessFromMomentResult Initial(EndPlateMomentWithBeamSectionResult parent,
        IEnumerable<EndPlateStiffnessRowDetail>? rows = null)
        => EndPlateInitialStiffnessFromMoment.Calculate(new(parent, rows ?? Details(parent.Input.ExtensionGeometry == null)));

    internal static EndPlateConnectionAssessmentResult Connection(EndPlateMomentComponentResult parent,
        bool overloadedBolt = false, bool compressionFull = false, string hardwareSource = "Actual hardware dimensions",
        string weldSource = "Actual weld materials")
    {
        var m = parent.ComponentAssessment.Input; bool cjp = m.ExtensionGeometry == null;
        double rowShear = m.Shear == 0 ? 0 : overloadedBolt ? 40000 : 30000;
        int nTension = cjp ? 2 : 3; var rows = new List<EndPlateBoltShearRow>();
        if (!cjp) rows.Add(new(0, -40, rowShear, rowShear));
        rows.Add(new(cjp ? 0 : 1, 60, rowShear, rowShear)); rows.Add(new(cjp ? 1 : 2, 150, rowShear, rowShear));
        double lower = (m.Shear - 2 * nTension * rowShear) / 2;
        rows.Add(new(null, 470, lower, lower, "Qualified shear-only row;470mm is fixture detail"));
        var bolts = new EndPlateBoltComponentInput(parent, new(36, 39.55, 36, 39.55, 41.6, 41.6, hardwareSource),
            rows, 410, "Column flange actualFu410", "Prescribed balanced same-case vertical shear");
        if (cjp)
            return EndPlateConnectionAssessment.Verify(bolts, new TopFlushCjpEndPlateWeldInput(parent,
                new(15.6, 209.3, 12, 0, 355, 510, true, "Full penetration", "Inspection", "Real inner toe", "Through-thickness evidence"),
                580, 0, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S355, 275, 410, weldSource,
                FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize,
                compressionFull ? EndPlateCompressionTransfer.FullStrengthWelds : EndPlateCompressionTransfer.QualifiedDirectBearing,
                "Actual fillets", "Normal transfer", "All V through web", "Compatible consumables", "Eccentricities", "Contact"));
        return EndPlateConnectionAssessment.Verify(bolts, new EndPlateWeldComponentInput(parent,
            FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275, 275, 410, weldSource,
            new(FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize,
                compressionFull ? EndPlateCompressionTransfer.FullStrengthWelds : EndPlateCompressionTransfer.QualifiedDirectBearing,
                "Real paired fillets", "Root/transition transfer", "All V through web", "Compatible consumables", "Eccentricities", "Contact")));
    }
}
