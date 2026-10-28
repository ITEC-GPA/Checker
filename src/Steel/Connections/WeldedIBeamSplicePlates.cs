using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedIBeamSplicePlateInput
    {
        public IBeamSplicePlatePolicy Policy { get; }
        public WeldedIBeamSpliceBoltInput Bolts { get; }
        public WeldedIBeamSplicePlateInput(IBeamSplicePlatePolicy policy, WeldedIBeamSpliceBoltInput bolts)
        { Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Bolts = bolts ?? throw new ArgumentNullException(nameof(bolts)); }
        /// <summary>Same policy as <paramref name="physical"/> for another action state of the same prepared detail.</summary>
        internal WeldedIBeamSplicePlateInput(WeldedIBeamSplicePlateInput physical, WeldedIBeamSpliceBoltInput bolts)
        {
            if (!ReferenceEquals(physical.Bolts.Prepared, bolts.Prepared)) throw new ArgumentException("The case must share the prepared bolt detail.", nameof(bolts));
            Policy = physical.Policy; Bolts = bolts; prepared = physical.Prepared;
        }
        private SplicePlatePreparation prepared;
        /// <summary>Demand-independent plate data of this physical detail.</summary>
        internal SplicePlatePreparation Prepared => prepared ?? System.Threading.LazyInitializer.EnsureInitialized(ref prepared, () => new SplicePlatePreparation(this));
    }

    /// <summary>One limiting section of a continuous cover, N positive in tension.
    /// M is about the actual cover centroid. ColumnLimit 0/1 means before/after
    /// the complete bolt column; -1 denotes the butt plane or a free edge.</summary>
    public sealed class SpliceCoverCut
    {
        public double X { get; }
        public int ColumnLimit { get; }
        public double N { get; }
        public double V { get; }
        public double M { get; }
        internal SpliceCoverCut(double x, int limit, double n, double v, double m)
        { X = x; ColumnLimit = limit; N = n; V = v; M = m; }
    }

    public sealed class SpliceCoverPlateAssessment
    {
        public WeldedSpliceCover Cover { get; }
        public IReadOnlyList<SpliceCoverCut> Cuts { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> Sections { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> LongitudinalCompression { get; }
        public ConnectionVerification<WeldedIBeamSplicePlateInput> AxialBlockTearing { get; }
        /// <summary>Typed net area behind the Sections trace entry NetArea.</summary>
        internal double NetArea { get; }
        private readonly HashSet<ConnectionCheck> compressionShearBending;
        /// <summary>True for the P398 compression-bending-shear interaction checks of LongitudinalCompression.</summary>
        internal bool IsCompressionShearBending(ConnectionCheck check) => compressionShearBending.Contains(check);
        internal SpliceCoverPlateAssessment(WeldedSpliceCover cover, IEnumerable<SpliceCoverCut> cuts,
            ConnectionVerification<WeldedIBeamSplicePlateInput> sections,
            ConnectionVerification<WeldedIBeamSplicePlateInput> compression,
            ConnectionVerification<WeldedIBeamSplicePlateInput> block, double netArea = double.NaN, IEnumerable<ConnectionCheck> interaction = null)
        {
            NetArea = netArea; compressionShearBending = new HashSet<ConnectionCheck>(interaction ?? Enumerable.Empty<ConnectionCheck>());
            Cover = cover; Cuts = Array.AsReadOnly(cuts.ToArray()); Sections = sections; LongitudinalCompression = compression; AxialBlockTearing = block; }
    }

    public sealed class WeldedIBeamSplicePlateResult
    {
        public WeldedIBeamSplicePlateInput Input { get; }
        internal WeldedIBeamSpliceBoltResult PreparedBolts { get; }
        public IReadOnlyList<SpliceCoverPlateAssessment> Covers { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool CanQualifyWholeJoint => false;
        public bool HasFailedComponent => Covers.Any(c => new[] { c.Sections, c.LongitudinalCompression, c.AxialBlockTearing }.Any(r => r.Status == ConnectionStatus.Failed));
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "COMBINED_NET_SECTION_FRACTURE_AND_CATEGORY_C_NET_PLASTIC_NVM", "GENERAL_NVM_BLOCK_TEARING",
            "PLATE_SHEAR_AND_LATERAL_STABILITY", "PARENT_MEMBER_SECTION_AND_STABILITY",
            "LOCAL_SECONDARY_BENDING_AND_FORCE_INTRODUCTION" });
        internal WeldedIBeamSplicePlateResult(WeldedIBeamSplicePlateInput input, WeldedIBeamSpliceBoltResult bolts, IEnumerable<SpliceCoverPlateAssessment> covers, IEnumerable<string> issues)
        { Input = input; PreparedBolts = bolts; Covers = Array.AsReadOnly(covers.ToArray()); Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray()); }
    }

    /// <summary>Section and longitudinal strip COMPONENTS of all eight covers.
    /// The exact concentrated-force envelope retains local transverse forces
    /// in the inner strips. No complete plate stability or NVM fracture claim.</summary>
    public static class WeldedIBeamSplicePlates
    {
        public static WeldedIBeamSplicePlateResult Verify(WeldedIBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, WeldedIBeamSpliceBolts.Verify(p.Bolts));
        }
        internal static WeldedIBeamSplicePlateResult Verify(WeldedIBeamSplicePlateInput p, WeldedIBeamSpliceBoltResult bolts)
        {
            if (!ReferenceEquals(p.Bolts, bolts.ComponentAssessment.Input)) throw new ArgumentException("Prepared bolts belong to another input.");
            var issues = new List<string>(bolts.ComponentAssessment.Diagnostics);
            var covers = new List<SpliceCoverPlateAssessment>();
            if (!SpliceIdentity.Code(p.Policy.Code, p.Bolts.Code)) issues.Add("PLATE_BOLT_POLICY_MISMATCH");
            if (issues.Count != 0) return new WeldedIBeamSplicePlateResult(p, bolts, covers, issues);
            var f = bolts.Forces; var prepared = p.Prepared.Layout;
            foreach (var pc in prepared.Covers)
            {
                var left = f.CoverGroups[pc.Left]; var right = f.CoverGroups[pc.Right];
                var localIssues = new List<string>();
                var cuts = Envelope(pc, left.Distribution.Forces, right.Distribution.Forces, localIssues);
                if (!pc.SectionValid) localIssues.Add("NONPOSITIVE_OR_NONFINITE_PLATE_SECTION");
                var trace = new List<SectionResistanceValue>(pc.SectionTrace);
                var sectionChecks = new List<ConnectionCheck>(); var compressionChecks = new List<ConnectionCheck>();
                var compressionIssues = new List<string>(localIssues); var compressionTrace = new List<SectionResistanceValue>();
                var interactionChecks = new List<ConnectionCheck>();
                double maxCompression = 0, area = pc.Area, w = pc.W, half = pc.Half;
                if (localIssues.Count == 0)
                {
                    double nr = pc.Nr, nt = pc.Nt, vg = pc.Vg, vn = pc.Vn, mr = pc.Mr;
                    foreach (var c in cuts.Where(c => c.X > -half && c.X < half))
                    {
                        var ids = pc.CutIds(cuts.IndexOf(c));
                        double vr = Math.Abs(c.V) / Math.Min(vg, vn);
                        double rho = vr <= .5 ? 0 : vr >= 1 ? 1 : Math.Pow(2 * vr - 1, 2);
                        double normalCapacity = c.N >= 0 ? Math.Min(nr, nt) : nr;
                        double remainingM = Math.Max(0, 1 - Math.Abs(c.N) / normalCapacity) * (1 - rho) * mr;
                        double compression = Math.Max(0, -c.N / area + Math.Abs(c.M) / w);
                        if (!(pc.ResistancesFinite && Finite(remainingM) && Finite(compression)))
                        { localIssues.Add("SECTION_RESISTANCE_NUMERIC_RANGE"); break; }
                        Add(sectionChecks, ids[0], Math.Max(0, c.N), nr);
                        Add(sectionChecks, ids[1], Math.Max(0, c.N), nt);
                        Add(sectionChecks, ids[2], Math.Max(0, -c.N), nr);
                        Add(sectionChecks, ids[3], Math.Abs(c.V), vg);
                        Add(sectionChecks, ids[4], Math.Abs(c.V), vn);
                        Add(sectionChecks, ids[5], Math.Abs(c.M), remainingM, "Nmm");
                        maxCompression = Math.Max(maxCompression, compression);
                    }
                    if (maxCompression > 0)
                    {
                        if (pc.OutstandOutsideClass3) compressionIssues.Add("COMPRESSED_OUTSTAND_OUTSIDE_CLASS3_STRIP_SCOPE");
                        if (pc.InternalStripOutsideClass3) compressionIssues.Add("COMPRESSED_INTERNAL_STRIP_OUTSIDE_CLASS3_SCOPE");
                    }
                    if (!pc.StripFinite) compressionIssues.Add("STRIP_BUCKLING_NUMERIC_RANGE");
                    else
                    {
                        compressionChecks.Add(new ConnectionCheck("MaximumCompression.Yield", maxCompression, pc.YieldStress, prepared.NormalReference, "MPa"));
                        compressionChecks.Add(new ConnectionCheck("MaximumCompression.InterFastenerBuckling", maxCompression, pc.Rb,
                            "SCI P398 p57; EN1993-1-8:2005 Table3.3 note2; EC solid-strip curve c with declared gammaM1", "MPa"));
                    }
                    // P398 p59 combines the compressive resistance with the moment
                    // resistance reduced by the SAME cut's shear. Independent N-M-V
                    // yielding and maximum-compression checks do not imply this bound.
                    double nb = pc.Nb;
                    if (pc.NbValid) foreach (var c in cuts.Where(c => c.X > -half && c.X < half && c.N < 0))
                    {
                        double vr = Math.Abs(c.V) / pc.ShearForInteraction;
                        double rho = vr <= .5 ? 0 : vr >= 1 ? 1 : Math.Pow(2 * vr - 1, 2);
                        double remaining = Math.Max(0, 1 - Math.Abs(c.N) / nb) * (1 - rho) * w * pc.Plate.Fy / pc.GammaM0;
                        if (!Finite(remaining)) compressionIssues.Add("COMPRESSION_SHEAR_BENDING_NUMERIC_RANGE");
                        else
                        {
                            var interaction = new ConnectionCheck(pc.CutIds(cuts.IndexOf(c))[6], Math.Abs(c.M), remaining,
                                "SCI P398 p59 compression-bending-shear interaction; conservative Nwp,Rd=min(Afy/gammaM0,Nb,Rd) from Table3.3 strip buckling", "Nmm");
                            compressionChecks.Add(interaction); interactionChecks.Add(interaction);
                        }
                    }
                    else compressionIssues.Add("COMPRESSION_RESISTANCE_NUMERIC_RANGE");
                    compressionTrace.AddRange(pc.CompressionTrace);
                    Value(compressionTrace, "MaximumCompression", maxCompression, "MPa");
                }
                compressionIssues.AddRange(localIssues);
                covers.Add(new SpliceCoverPlateAssessment(pc.Cover, cuts,
                    Result(p, pc.SectionsMethod, "Gross elastic N-V-M envelope and separate net axial/shear components; NOT combined net-section fracture or plate stability.", sectionChecks, trace, localIssues),
                    Result(p, pc.CompressionMethod, "Longitudinal filled-fastener strip compression only; NOT complete shear/lateral plate buckling.", compressionChecks, compressionTrace, compressionIssues),
                    AxialBlocks(p, pc, left, right, localIssues), pc.Net, interactionChecks));

                void Add(IList<ConnectionCheck> list, string id, double demand, double resistance, string unit = "N")
                    => list.Add(new ConnectionCheck(id, demand, resistance, prepared.NormalReference, unit));
            }
            return new WeldedIBeamSplicePlateResult(p, bolts, covers, issues);
        }

        private static ConnectionVerification<WeldedIBeamSplicePlateInput> AxialBlocks(WeldedIBeamSplicePlateInput p,
            SplicePlatePreparation.CoverPlate pc, WeldedSpliceCoverForceGroup left, WeldedSpliceCoverForceGroup right, IEnumerable<string> initialIssues)
        {
            var issues = new List<string>(initialIssues); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            if (pc.BlockEnumerationLimit) issues.Add("AXIAL_BLOCK_ENUMERATION_LIMIT_20_BY_16");
            // Scope is determined by exact input actions/geometry, never an absolute force cutoff.
            var groups = new[] { left, right };
            for (int s = 0; s < 2; s++)
            {
                var a = groups[s].Distribution.InputActions; var side = pc.Sides[s];
                if (a.Fy != 0 || a.Mz != 0 || (a.Fx != 0 && (a.ApplicationY != side.Centre || side.AverageOffCentre)))
                    issues.Add("BLOCK_NVM_OR_ECCENTRIC_FORCE_DISTRIBUTION_NOT_QUALIFIED");
            }
            if (issues.Count == 0)
            {
                for (int s = 0; s < 2; s++)
                {
                    var g = groups[s]; var side = pc.Sides[s];
                    // Left/right external vectors oppose; the physical cover tension is common.
                    double signedN = (g.Side == SpliceSide.Left ? 1 : -1) * g.Distribution.InputActions.Fx;
                    double perBolt = Math.Max(0, signedN) / g.Distribution.Forces.Count;
                    // Every prefix from the free longitudinal end; no undocumented omission
                    // of a short block when it is more critical than the full group.
                    foreach (var path in side.Paths)
                    {
                        if (path.Issue != null) { issues.Add(path.Issue); continue; }
                        checks.Add(new ConnectionCheck(path.Id, path.Fasteners * perBolt, path.Resistance,
                            "EN1993-1-8:2005 3.10.2(3) eq3.10; rectangular axial paths, conservative 0.5 tension term; NTC factors when selected"));
                        trace.Add(path.Ant); trace.Add(path.Anv);
                    }
                }
            }
            return Result(p, pc.BlocksMethod,
                "Rectangular cover block paths under centred axial loading only; all free-edge prefixes, reduced tensile contribution. No combined N-V-M, parent-web block or joint qualification.", checks, trace, issues);
        }

        /// <summary>The cover cut envelope on the prepared column structure: same sums in the same order.</summary>
        private static List<SpliceCoverCut> Envelope(SplicePlatePreparation.CoverPlate pc, IReadOnlyList<ElasticBoltForce> left,
            IReadOnlyList<ElasticBoltForce> right, IList<string> issues)
        {
            var n = new Sum(); var v = new Sum(); var m = new Sum(); double centre = pc.Centre, half = pc.Half;
            double ns = 0, vs = 0, ms = 0; var result = new List<SpliceCoverCut>(pc.MaximumCuts);
            foreach (var x in pc.Stations)
            {
                if (x.Column == null) { Capture(x.X, -1); continue; }
                Capture(x.X, 0);
                foreach (int index in x.Column)
                {
                    var f = index < left.Count ? left[index] : right[index - left.Count];
                    n.Add(f.Fx); v.Add(f.Fy); m.Add(f.Position.X * f.Fy - (f.Position.Y - centre) * f.Fx);
                    ns += Math.Abs(f.Fx); vs += Math.Abs(f.Fy);
                    ms += Math.Abs(f.Position.X * f.Fy) + Math.Abs((f.Position.Y - centre) * f.Fx);
                }
                Capture(x.X, 1);
            }
            if (result.Count == 0) { issues.Add("NO_FINITE_COVER_CUT"); return result; }
            var last = result[result.Count - 1];
            if (!Balanced(last.N, ns) || !Balanced(last.V, vs) || !Balanced(last.M, ms + half * vs)) issues.Add("COVER_FREE_EDGE_EQUILIBRIUM_FAILED");
            return result;
            void Capture(double x, int limit)
            {
                double moment = m.Value - x * v.Value;
                if (!(Finite(n.Value) && Finite(v.Value) && Finite(moment))) issues.Add("CUT_NUMERIC_RANGE");
                else result.Add(new SpliceCoverCut(x, limit, n.Value, v.Value, moment));
            }
        }
        internal static SpliceBoltLayout Grid(WeldedIBeamSpliceForceInput d, WeldedSpliceCoverForceGroup g) => g.IsWeb ? d.WebBolts : Top(g.Cover) ? d.Top.Bolts : d.Bottom.Bolts;
        internal static double End(WeldedIBeamSpliceForceInput d, WeldedSpliceCoverForceGroup g) => g.IsWeb ? d.WebCoverEnd : Top(g.Cover) ? d.Top.EndDistance : d.Bottom.EndDistance;
        internal static bool Top(WeldedSpliceCover c) => c == WeldedSpliceCover.TopOuter || c == WeldedSpliceCover.TopInnerNegative || c == WeldedSpliceCover.TopInnerPositive;
        internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Balanced(double residual, double scale) => Finite(residual) && Finite(scale) && Math.Abs(residual) <= ElasticBoltGroup.EquilibriumRelativeTolerance * scale;
        private static void Value(IList<SectionResistanceValue> values, string name, double value, string unit) { if (Finite(value)) values.Add(new SectionResistanceValue(name, value, unit)); }
        private static string NormalReference(WeldedIBeamSplicePlateInput p) => "SCI P398 pp56-59; EN1993-1-1:2005 6.2.3,6.2.6,6.2.9.2,6.2.10" + (p.Policy.Code.IsNtc ? "; supplementary model with NTC2018 factors" : "");
        private static ConnectionVerification<WeldedIBeamSplicePlateInput> Result(WeldedIBeamSplicePlateInput p, string method, string scope,
            IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace, IEnumerable<string> issues)
            => new ConnectionVerification<WeldedIBeamSplicePlateInput>(p, p.Policy.Code, "Steel.Connection.WeldedIBeamSplice." + method, scope, checks, trace, issues.Distinct(), new[] {
                "Each cover is assessed separately with its own actual centre-plane force field. Both limits of each full column are retained; N,V constant and M affine between columns, so endpoint envelopes are exact for this beam model.",
                "Net area deducts every aligned hole at one transverse section. Net axial and net shear checks do not establish mixed-stress rupture or category-C net-plastic N-V-M resistance.",
                "SCI gross elastic bending interaction uses rho=0 below half shear resistance; no residual bending capacity beyond full shear/axial resistance. Compression holes are filled.",
                "Longitudinal strip curve c, Lcr=0.6 times maximum pitch including central spacing; class-3 screens do not prove continuous support at discrete bolt rows or close shear/lateral buckling.",
                "No plate or joint qualification follows from passing these components. Loads at bolt holes are beam-model resultants, not local stress concentrations or contact/prying solutions." });
        private sealed class Sum { private double sum, correction; internal double Value => sum; internal void Add(double x) { double y = x - correction, next = sum + y; correction = (next - sum) - y; sum = next; } }
    }

    /// <summary>Demand-independent plate data of one physical detail: cover sections, strip
    /// buckling, cut stations and the axial block paths, built once and shared by every case.</summary>
    internal sealed class SplicePlatePreparation
    {
        internal sealed class Station { internal double X; internal int[] Column; }
        internal sealed class BlockPath { internal string Id, Issue; internal double Resistance; internal int Fasteners; internal SectionResistanceValue Ant, Anv; }
        internal sealed class BlockSide { internal double Centre; internal bool AverageOffCentre; internal BlockPath[] Paths; }
        internal sealed class CoverPlate
        {
            internal WeldedSpliceCover Cover; internal int Left, Right;
            internal ConnectionPlate Plate;
            internal double Centre, Half, Area, Net, W, Nr, Nt, Vg, Vn, Mr, YieldStress, Rb, Nb, ShearForInteraction, GammaM0;
            internal bool SectionValid, ResistancesFinite, OutstandOutsideClass3, InternalStripOutsideClass3, StripFinite, NbValid, BlockEnumerationLimit;
            internal SectionResistanceValue[] SectionTrace, CompressionTrace;
            internal Station[] Stations; internal int MaximumCuts;
            internal string SectionsMethod, CompressionMethod, BlocksMethod;
            internal BlockSide[] Sides;
            private string[][] cutIds;
            /// <summary>Check identifiers of cut index k: GrossTension, NetAxialTension, GrossCompression,
            /// GrossShear, NetShear, AxialShearBending, CompressionShearBending.</summary>
            internal string[] CutIds(int k)
            {
                var ids = cutIds;
                if (k >= ids.Length) { ids = new string[k + 1][]; Array.Copy(cutIds, ids, cutIds.Length); for (int i = cutIds.Length; i <= k; i++) ids[i] = Names(i); cutIds = ids; }
                return ids[k];
            }
            internal void PrepareCutIds() { cutIds = Enumerable.Range(0, MaximumCuts).Select(Names).ToArray(); }
            private static string[] Names(int k)
            {
                string id = "Cut" + k;
                return new[] { id + ".GrossTension", id + ".NetAxialTension", id + ".GrossCompression", id + ".GrossShear", id + ".NetShear",
                    id + ".AxialShearBending", id + ".CompressionShearBending" };
            }
        }
        internal sealed class PlateLayout { internal CoverPlate[] Covers; internal string NormalReference; }

        private readonly Lazy<PlateLayout> layout;
        /// <summary>Built on first use, when the force groups of this geometry are known to be supported.</summary>
        internal PlateLayout Layout => layout.Value;
        internal SplicePlatePreparation(WeldedIBeamSplicePlateInput p)
            => layout = new Lazy<PlateLayout>(() => Build(p), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        private static PlateLayout Build(WeldedIBeamSplicePlateInput p)
        {
            var d = p.Bolts.Detail; var descriptors = d.Geometry.Groups; var factors = p.Policy.Code.Plates;
            var covers = new List<CoverPlate>();
            foreach (var cover in descriptors.Select(x => x.Cover).Distinct())
            {
                int left = Array.FindIndex(descriptors, x => x.Cover == cover && x.Side == SpliceSide.Left);
                int right = Array.FindIndex(descriptors, x => x.Cover == cover && x.Side == SpliceSide.Right);
                var g = descriptors[left]; var plate = g.Plate; var leftPoints = g.Group.Value.Points; var rightPoints = descriptors[right].Group.Value.Points;
                bool isWeb = cover == WeldedSpliceCover.WebNegative || cover == WeldedSpliceCover.WebPositive;
                var grid = isWeb ? d.WebBolts : WeldedIBeamSplicePlates.Top(cover) ? d.Top.Bolts : d.Bottom.Bolts;
                var bolt = isWeb ? p.Bolts.WebBolt : p.Bolts.FlangeBolt;
                double end = isWeb ? d.WebCoverEnd : WeldedIBeamSplicePlates.Top(cover) ? d.Top.EndDistance : d.Bottom.EndDistance;
                double half = grid.FirstBoltDistance + grid.LongitudinalSpan + end;
                double centre = (g.Minimum + g.Maximum) / 2;
                int rows = leftPoints.Select(b => b.Y).Distinct().Count();
                double area = plate.Width * plate.Thickness, net = (plate.Width - rows * bolt.HoleDiameter) * plate.Thickness;
                double w = plate.Thickness * plate.Width * plate.Width / 6;
                var c = new CoverPlate
                {
                    Cover = cover, Left = left, Right = right, Plate = plate, Centre = centre, Half = half, Area = area, Net = net, W = w,
                    SectionValid = new[] { area, net, w }.All(v => WeldedIBeamSplicePlates.Finite(v) && v > 0),
                    SectionTrace = Values(("GrossArea", area, "mm2"), ("NetArea", net, "mm2"), ("ElasticModulus", w, "mm3"),
                        ("HolesPerSection", rows, "1"), ("TransverseCentroid", centre, "mm")),
                    GammaM0 = factors.GammaM0,
                    SectionsMethod = "Sections." + cover, CompressionMethod = "LongitudinalCompression." + cover, BlocksMethod = "AxialBlocks." + cover
                };
                c.Nr = area * plate.Fy / factors.GammaM0; c.Nt = .9 * net * plate.Fu / factors.GammaM2;
                c.Vg = PlasticShear.Resistance(area, plate.Fy, 1.27, factors.GammaM0);
                c.Vn = PlasticShear.Resistance(net, plate.Fu, factors.GammaM2);
                c.Mr = w * plate.Fy / factors.GammaM0;
                c.ResistancesFinite = new[] { c.Nr, c.Nt, c.Vg, c.Vn, c.Mr }.All(WeldedIBeamSplicePlates.Finite);
                double pitch = grid.MaximumLongitudinalGap;
                double eps = Math.Sqrt(235 / plate.Fy), length = .6 * pitch;
                double lambda = length * Math.Sqrt(12 * plate.Fy / p.Policy.ElasticModulus) / (Math.PI * plate.Thickness);
                bool exempt = pitch / plate.Thickness <= 9 * eps;
                double chi = exempt ? 1 : BucklingReductionArithmetic.AxialChi(BucklingReductionArithmetic.Phi(.49, lambda), lambda);
                double rb = exempt ? plate.Fy / factors.GammaM0 : chi * plate.Fy / p.Policy.GammaM1;
                double edge = Math.Max(leftPoints.Min(b => b.Y) - g.Minimum, g.Maximum - leftPoints.Max(b => b.Y));
                c.OutstandOutsideClass3 = edge / plate.Thickness > 14 * eps;
                c.InternalStripOutsideClass3 = rows > 1 && grid.TransversePitch / plate.Thickness > 42 * eps;
                c.StripFinite = new[] { rb, lambda, chi }.All(WeldedIBeamSplicePlates.Finite);
                c.YieldStress = plate.Fy / factors.GammaM0; c.Rb = rb;
                c.Nb = area * Math.Min(plate.Fy / factors.GammaM0, rb);
                c.NbValid = WeldedIBeamSplicePlates.Finite(c.Nb) && c.Nb > 0;
                c.ShearForInteraction = Math.Min(PlasticShear.Resistance(area, plate.Fy, 1.27, factors.GammaM0), PlasticShear.Resistance(net, plate.Fu, factors.GammaM2));
                c.CompressionTrace = Values(("CompressionResistanceForInteraction", c.Nb, "N"), ("MaximumUnrestrainedPitch", pitch, "mm"),
                    ("EffectiveLength", length, "mm"), ("Slenderness", lambda, "1"), ("Chi", chi, "1"), ("Exempt", exempt ? 1 : 0, "1"));
                // Cut stations: every column on both sides, the two free edges and the butt plane, in the original order.
                var positions = leftPoints.Concat(rightPoints).ToArray();
                var columns = Enumerable.Range(0, positions.Length).GroupBy(i => positions[i].X).ToDictionary(x => x.Key, x => x.ToArray());
                c.Stations = columns.Keys.Concat(new[] { -half, 0d, half }).Distinct().OrderBy(x => x)
                    .Select(x => new Station { X = x, Column = columns.TryGetValue(x, out var column) ? column : null }).ToArray();
                c.MaximumCuts = c.Stations.Sum(x => x.Column == null ? 1 : 2);
                c.PrepareCutIds();
                c.BlockEnumerationLimit = grid.LongitudinalRows > 20 || rows > 16;
                c.Sides = new[] { Block(descriptors[left], leftPoints), Block(descriptors[right], rightPoints) };
                covers.Add(c);

                BlockSide Block(SpliceForceGeometry.GroupDescriptor side, ConnectionPoint2D[] points)
                {
                    double sideCentre = (side.Minimum + side.Maximum) / 2;
                    var s = new BlockSide { Centre = sideCentre, AverageOffCentre = points.Average(b => b.Y) != sideCentre };
                    var paths = new List<BlockPath>();
                    double[] ys = points.Select(b => b.Y).Distinct().OrderBy(y => y).ToArray();
                    int path = 0;
                    for (int countX = 1; countX <= grid.LongitudinalRows; countX++)
                    {
                        double shearLength = end + (countX - 1) * grid.LongitudinalPitch - (countX - .5) * bolt.HoleDiameter;
                        for (int first = 0; first < ys.Length; first++)
                        for (int last = first; last < ys.Length; last++)
                        {
                            int countY = last - first + 1;
                            if (countY > 1) Path(ys[last] - ys[first] - (countY - 1) * bolt.HoleDiameter, 2 * shearLength, countX * countY, "Internal");
                            if (first == 0) Path(ys[last] - side.Minimum - (countY - .5) * bolt.HoleDiameter, shearLength, countX * countY, "LowerEdge");
                            if (last == ys.Length - 1) Path(side.Maximum - ys[first] - (countY - .5) * bolt.HoleDiameter, shearLength, countX * countY, "UpperEdge");
                        }
                    }
                    s.Paths = paths.ToArray();
                    return s;
                    void Path(double nt, double nv, int fasteners, string mode)
                    {
                        string id = side.Side + ".Path" + (++path) + "." + mode;
                        double ant = nt * plate.Thickness, anv = nv * plate.Thickness;
                        // Always use the reduced tensile contribution of the eccentric EC expression;
                        // no concentrated, variable-direction NVM field is approved by this choice.
                        double resistance = .5 * ant * plate.Fu / p.Policy.Code.Plates.GammaM2
                            + PlasticShear.Resistance(anv, plate.Fy, p.Policy.Code.Plates.GammaM0);
                        if (!(ant > 0) || !(anv > 0) || !WeldedIBeamSplicePlates.Finite(resistance)) { paths.Add(new BlockPath { Issue = id + ".INVALID_BLOCK_LIGAMENT" }); return; }
                        paths.Add(new BlockPath { Id = id, Resistance = resistance, Fasteners = fasteners,
                            Ant = new SectionResistanceValue(id + ".Ant", ant, "mm2"), Anv = new SectionResistanceValue(id + ".Anv", anv, "mm2") });
                    }
                }
            }
            return new PlateLayout
            {
                Covers = covers.ToArray(),
                NormalReference = "SCI P398 pp56-59; EN1993-1-1:2005 6.2.3,6.2.6,6.2.9.2,6.2.10" + (p.Policy.Code.IsNtc ? "; supplementary model with NTC2018 factors" : "")
            };
            SectionResistanceValue[] Values(params (string Name, double Value, string Unit)[] values)
                => values.Where(v => WeldedIBeamSplicePlates.Finite(v.Value)).Select(v => new SectionResistanceValue(v.Name, v.Value, v.Unit)).ToArray();
        }
    }}
