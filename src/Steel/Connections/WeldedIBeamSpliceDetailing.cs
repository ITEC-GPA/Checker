using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum SpliceExposure { ProtectedCarbonSteel, ExposedCarbonSteel, UnprotectedWeatheringSteel }

    public sealed class WeldedIBeamSpliceDetailInput
    {
        public WeldedIBeamSplicePlateInput Plates { get; }
        public SpliceExposure Exposure { get; }
        public WeldedIBeamSpliceDetailInput(WeldedIBeamSplicePlateInput plates, SpliceExposure exposure)
        {
            Plates = plates ?? throw new ArgumentNullException(nameof(plates));
            if (!Enum.IsDefined(typeof(SpliceExposure), exposure)) throw new ArgumentOutOfRangeException(nameof(exposure));
            Exposure = exposure;
        }
    }

    /// <summary>Couples required to move the cut's in-plane force from the contact face
    /// to the cover midsurface, in a local right-handed (u,v,w) frame. Not a solved
    /// contact/bolt-tension field or a resistance. Units Nmm; w is normal to the cover.</summary>
    public sealed class SpliceCoverFaceTransfer
    {
        public WeldedSpliceCover Cover { get; }
        public SpliceCoverCut Cut { get; }
        public double ContactOffsetW { get; }
        public double TorsionU => -ContactOffsetW * Cut.V;
        public double BendingV => ContactOffsetW * Cut.N;
        internal SpliceCoverFaceTransfer(WeldedSpliceCover cover, SpliceCoverCut cut, double offset)
        { Cover = cover; Cut = cut; ContactOffsetW = offset; }
    }

    public sealed class WeldedIBeamSpliceDetailResult
    {
        public WeldedIBeamSpliceDetailInput Input { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> Detailing { get; }
        public IReadOnlyList<SpliceCoverFaceTransfer> FaceTransfers { get; }
        public IReadOnlyList<SectionResistanceValue> ElasticFaceDiagnostics { get; }
        public bool CanQualifyLocalTransfer => false;
        public bool CanQualifyWholeJoint => false;
        internal WeldedIBeamSpliceDetailResult(WeldedIBeamSpliceDetailInput input,
            ConnectionVerification<WeldedIBeamSpliceDetailInput> detailing,
            IEnumerable<SpliceCoverFaceTransfer> transfer, IEnumerable<SectionResistanceValue> strain)
        { Input = input; Detailing = detailing; FaceTransfers = Array.AsReadOnly(transfer.ToArray()); ElasticFaceDiagnostics = Array.AsReadOnly(strain.ToArray()); }
    }

    public static class WeldedIBeamSpliceDetailing
    {
        public static WeldedIBeamSpliceDetailResult Verify(WeldedIBeamSpliceDetailInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, WeldedIBeamSplicePlates.Verify(p.Plates));
        }
        internal static WeldedIBeamSpliceDetailResult Verify(WeldedIBeamSpliceDetailInput p, WeldedIBeamSplicePlateResult plateResult)
        {
            if (!ReferenceEquals(p.Plates, plateResult.Input)) throw new ArgumentException("Prepared plates belong to another input.");
            var issues = new List<string>(plateResult.Diagnostics); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var transfers = new List<SpliceCoverFaceTransfer>();
            var strain = new List<SectionResistanceValue>(); var d = p.Plates.Bolts.Detail;
            if (p.Exposure == SpliceExposure.UnprotectedWeatheringSteel) issues.Add("UNPROTECTED_WEATHERING_DETAIL_NOT_SUPPORTED");
            if (issues.Count == 0)
            {
                var forces = plateResult.PreparedBolts.Forces;
                foreach (var c in plateResult.Covers)
                {
                    var g = forces.CoverGroups.Single(x => x.Cover == c.Cover && x.Side == SpliceSide.Left);
                    var grid = WeldedIBeamSplicePlates.Grid(d, g);
                    bool top = WeldedIBeamSplicePlates.Top(g.Cover);
                    var flange = top ? d.Top : d.Bottom;
                    // Table3.3 note3: the thinner OUTER ply, never the sandwiched parent.
                    double outerThickness = g.IsWeb ? d.EachWebCover.Thickness : Math.Min(flange.Outer.Thickness, flange.EachInner.Thickness);
                    double limit = Math.Min(14 * outerThickness, 200);
                    string name = c.Cover.ToString();
                    if (grid.LongitudinalRows > 1) Check(name + ".LongitudinalPitch", grid.LongitudinalPitch, limit);
                    Check(name + ".CentralPitch", 2 * grid.FirstBoltDistance, limit);
                    if (g.Distribution.Points.Select(b => b.Y).Distinct().Count() > 1)
                        Check(name + ".TransversePitch", grid.TransversePitch, limit);
                    trace.Add(new SectionResistanceValue(name + ".ThinnerOuterThickness", outerThickness, "mm"));
                    if (p.Exposure == SpliceExposure.ExposedCarbonSteel)
                    {
                        double edgeLimit = 4 * outerThickness + 40;
                        Check(name + ".OuterEnd", WeldedIBeamSplicePlates.End(d, g), edgeLimit);
                        Check(name + ".LowerEdge", g.Distribution.Points.Min(b => b.Y) - g.TransverseMinimum, edgeLimit);
                        Check(name + ".UpperEdge", g.TransverseMaximum - g.Distribution.Points.Max(b => b.Y), edgeLimit);
                        Check(name + ".ParentButtEnd", grid.FirstBoltDistance - d.ButtGap / 2, edgeLimit);
                        if (!g.IsWeb)
                        {
                            var parent = top ? d.Section.TopFlange : d.Section.BottomFlange;
                            Check(name + ".ParentFlangeEdge", (parent.Width - grid.TransverseSpan) / 2, edgeLimit);
                        }
                    }
                    double sign = g.IsWeb ? (g.Cover == WeldedSpliceCover.WebNegative ? 1 : -1)
                        : (top ? 1 : -1) * (g.Cover == WeldedSpliceCover.TopOuter || g.Cover == WeldedSpliceCover.BottomOuter ? 1 : -1);
                    foreach (var cut in c.Cuts)
                    {
                        var transfer = new SpliceCoverFaceTransfer(c.Cover, cut, sign * g.Plate.Thickness / 2);
                        if (!WeldedIBeamSplicePlates.Finite(transfer.TorsionU) || !WeldedIBeamSplicePlates.Finite(transfer.BendingV))
                            issues.Add("FACE_TRANSFER_NUMERIC_RANGE");
                        else transfers.Add(transfer);
                    }
                }
                FaceDiagnostic(true); FaceDiagnostic(false);
                void FaceDiagnostic(bool top)
                {
                    var cover = top ? d.Top : d.Bottom; var parent = top ? d.Section.TopFlange : d.Section.BottomFlange;
                    string name = top ? "Top" : "Bottom";
                    double outward = top ? 1 : -1;
                    double yo = outward * ((parent.Thickness + cover.Outer.Thickness) / 2 + (top ? d.Packing.TopOuter : d.Packing.BottomOuter));
                    double yi = -outward * ((parent.Thickness + cover.EachInner.Thickness) / 2 + (top ? d.Packing.TopInner : d.Packing.BottomInner));
                    double ao = cover.Outer.Width * cover.Outer.Thickness, ai = 2 * cover.EachInner.Width * cover.EachInner.Thickness;
                    var typed = forces.Flange(SpliceSide.Left, top);
                    double n = typed.ParentAxial, m = typed.ParentLocalMoment, no = typed.OuterForce, ni = typed.BothInnerForce;
                    double e = p.Plates.Policy.ElasticModulus;
                    double eo = no / (e * ao), ei = ni / (e * ai);
                    double k = (eo - ei) / (yo - yi), e0 = eo - k * yo;
                    double ep = n / (e * parent.Width * parent.Thickness);
                    double kp = -m / (e * parent.Width * Math.Pow(parent.Thickness, 3) / 12);
                    Value("AreaCentroidOffset", (ao * yo + ai * yi) / (ao + ai), "mm");
                    Value("OuterAxialStrain", eo, "1"); Value("InnerAxialStrain", ei, "1");
                    Value("FaceStrainGradient", k, "1/mm"); Value("ParentStrainGradient", kp, "1/mm");
                    Value("AtParentCentroidStrainJump", e0 - ep, "1"); Value("StrainGradientJump", k - kp, "1/mm");
                    void Value(string key, double value, string unit)
                    { if (WeldedIBeamSplicePlates.Finite(value)) strain.Add(new SectionResistanceValue(name + "." + key, value, unit)); else issues.Add(name + ".ELASTIC_FACE_NUMERIC_RANGE"); }
                }
            }
            var result = new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, p.Plates.Policy.Code,
                "Steel.Connection.WeldedIBeamSplice.Detailing", "Static maximum-pitch/exposure detail checks of ordinary carbon-steel double covers; not fatigue detailing or local force-transfer resistance.",
                checks, trace, issues.Distinct(), new[] {
                    "Maximum pitches conservatively use min(14*tOuter,200) even for protected tension, including across the butt. The parent ply is not an outer ply. Exposed ends/edges use 4*tOuter+40; unprotected weathering steel is excluded.",
                    "Minimum holes/edges/hardware and seam collisions are inherited from the same bolt input. Parent-web lateral edges are welded boundaries, not exposed free cover edges.",
                    "FaceTransfers are the transport couples r cross F from the contact face to each cover midsurface, with u=X; v=Y,w=Z for webs and v=Z,w=-Y for flanges. They do not solve contact normal forces, prying or bolt tension.",
                    "ElasticFaceDiagnostics interpolate the two axial face strains and compare them with the parent flange strain field. Zero residual is NOT a qualification; the diagnostic excludes local plate bending, slip and weld restraint.",
                    "No numerical acceptance tolerance on face strain mismatch, percentage stiffness rule or user provenance string is used to certify mechanical compatibility. Full local force introduction remains open." });
            return new WeldedIBeamSpliceDetailResult(p, result, transfers, strain);
            void Check(string id, double demand, double resistance) => checks.Add(new ConnectionCheck(id, demand, resistance,
                "EN1993-1-8:2005 Table3.3, notes1-3; conservative compression-pitch limit for every cover; supplementary for NTC2018", "mm"));
        }
    }
}
