using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace Steel.Members.Tests;

internal static class EndPlateNmvCaseData
{
    internal static EndPlateNmvGeometry Geometry(bool ntc = false, bool flush = false, double shear = 20000,
        double contactWidth = 209.3, bool fullWelds = true, bool shearOnlyLastRow = false, double gap = 0,
        double webThickness = 20, bool nearLowerFlangeRow = false, string contactSource = "Actual centred, initially closed contacts at both flange faces")
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.1, 1.25, "Declared EC parameters");
        var h = new SectionH(533.1, webThickness, 209.3, 15.6, 209.3, 15.6, "Compact analytic test section", 12.7);
        double area = 2 * 209.3 * 15.6 + 501.9 * webThickness + (4 - Math.PI) * 12.7 * 12.7;
        var beam = new ConnectionRolledISection(533.1, 209.3, webThickness, 15.6, 12.7, area, 275, 410);
        var section = new SteelSectionInput(new SteelSection(h, new SteelMaterial("Actual material", 210000, 275, 410),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished), true);
        var beamResult = EndPlateBeamSectionAdapter.Evaluate(new(code, beam, section, new(0, 0, shear, 0, 0, 0),
            275, 410, false, "Actual flange/web material", "Actual H geometry", "Capacity preparation at retained V; not actual NM case",
            ntc ? null : 1.2, ntc ? null! : "EC eta1.2"));
        var policy = ntc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        double[] physicalY = flush ? new[] { 60d, 150, 383.1 } : new[] { -40d, 60, 150, 383.1 };
        double[] internalRows = shearOnlyLastRow ? new[] { 44.4, 134.4 } : new[] { 44.4, 134.4, 367.5 };
        if (nearLowerFlangeRow) { physicalY = physicalY.Concat(new[] { 473.1 }).ToArray(); internalRows = internalRows.Concat(new[] { 457.5 }).ToArray(); }
        var input = new EndPlateMomentComponentInput(policy, new(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410),
            beam, 265, new(250, 25, 345, 470), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true),
            new(250, 258.8, 100, webThickness, 16, 12, 12, 501.9, internalRows,
                new(7.5, "Test declared chart alpha within EN figure domain; not experimental calibration"), "Actual compact four-row analytic detail"),
            flush ? null! : new(250, 100, 40, 12, 50, "Actual one-row top extension"), 1000, 1000, 46.9,
            beamResult.DerivedResistance, 0, Math.Abs(shear), 0, 1, 0, ColumnWebPanelConfiguration.SingleSidedJoint,
            "Single-sided baseline geometry", "Zero stress for geometry preparation; actual stress recomputed in NMV",
            "Actual column sway restraint", "Geometry envelope", TStubMode1Method.DistributedContact, 39.55, "Qualified standard-hole execution");
        var parent = EndPlateMomentWithBeamSection.Verify(input, beamResult);
        var rowShear = Math.Abs(shear) / (2 * physicalY.Length);
        var boltRows = physicalY.Select((y, i) => new EndPlateBoltShearRow(shearOnlyLastRow && i == physicalY.Length - 1 ? null : i,
            y, rowShear, rowShear, "Legacy shear-only declaration, deliberately not sufficient for NMV"));
        var bolts = new EndPlateBoltComponentInput(parent.MomentComponents, new(36, 39.55, 36, 39.55, 41.6, 41.6, "Actual bolt hardware"),
            boltRows, 410, "Actual column flange fu", "Symmetric prescribed vertical shear at all bolts");
        var transfer = fullWelds ? EndPlateCompressionTransfer.FullStrengthWelds : EndPlateCompressionTransfer.QualifiedDirectBearing;
        EndPlateConnectionAssessmentResult connection;
        if (flush)
            connection = EndPlateConnectionAssessment.Verify(bolts, new TopFlushCjpEndPlateWeldInput(parent.MomentComponents,
                new(15.6, 209.3, 12, 0, 355, 510, true, "CJP qualification", "Inspection", "Actual inner toe", "Through-thickness behaviour"),
                580, 0, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S355, 275, 410, "Actual materials",
                FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, transfer,
                "Actual fillets", "Normal transfer including reversed force", "Web shear transfer", "Compatible consumables", "Local eccentricities", "Contact"));
        else
            connection = EndPlateConnectionAssessment.Verify(bolts, new EndPlateWeldComponentInput(parent.MomentComponents,
                FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S355, 275, 410, "Actual materials",
                new(FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, FilletWeldEndCondition.FullSize, transfer,
                    "Actual paired fillets", "Normal transfer including reversed force", "Web shear transfer", "Compatible consumables", "Local eccentricities", "Contact")));
        var hardware = Enumerable.Range(0, parent.MomentComponents.EffectiveRows.Count)
            .Select(i => new EndPlateBoltElongationDetail(i, 70.5, "25+20.5+4+4+(15+19)/2 mm actual hardware"));
        return EndPlateNmvGeometry.Prepare(parent, connection, hardware, contactWidth, 15.6, contactWidth, 15.6, contactSource, gap);
    }

    internal static EndPlateNmvInput Input(EndPlateNmvGeometry g, double n = 40000, double moment = 5e6, double? shear = null,
        double upperShear = double.NaN, double columnN = -10000, bool separatePanel = false, EndPlateNmvTangent? applied = null)
    {
        double v = shear ?? g.Baseline.Input.Shear;
        double vc = double.IsNaN(upperShear) ? n / 2 : upperShear;
        double translatedMoment = moment - v * g.Baseline.Input.Column.Height / 2;
        return new(g, new(n, 0, v, 0, moment, 0), new(columnN, 0, vc, 0, -translatedMoment / 2, 0),
            new(columnN - v, 0, vc - n, 0, translatedMoment / 2, 0), "Same actual analytic combination", "Continuous axes, cuts projected to column centroid at beam midheight",
            separatePanel, separatePanel ? "Column panel separately represented in the test global model" : null!, applied);
    }
}
