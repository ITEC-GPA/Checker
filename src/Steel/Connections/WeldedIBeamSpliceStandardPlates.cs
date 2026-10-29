using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class WeldedIBeamSpliceStandardPlateResult
    {
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> NetSections { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> BlockHierarchy { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> DirectBlockTearing { get; }
        public ConnectionVerification<WeldedIBeamSpliceDetailInput> CompactPlateDomain { get; }
        /// <summary>A failed hierarchy is an unsuccessful sufficient applicability test,
        /// not proof that the actual loaded block has failed.</summary>
        public bool IsQualified => NetSections.IsComplete && (BlockHierarchy.Checks.Count == 0 ? BlockHierarchy.Diagnostics.Count == 0 : BlockHierarchy.Status == ConnectionStatus.Passed)
            && (DirectBlockTearing.Checks.Count == 0 ? DirectBlockTearing.Diagnostics.Count == 0 : DirectBlockTearing.Status == ConnectionStatus.Passed)
            && CompactPlateDomain.Status == ConnectionStatus.Passed;
        internal WeldedIBeamSpliceStandardPlateResult(ConnectionVerification<WeldedIBeamSpliceDetailInput> net,
            ConnectionVerification<WeldedIBeamSpliceDetailInput> block, ConnectionVerification<WeldedIBeamSpliceDetailInput> compact, ConnectionVerification<WeldedIBeamSpliceDetailInput> direct)
        { NetSections = net; BlockHierarchy = block; CompactPlateDomain = compact; DirectBlockTearing = direct; }
    }

    /// <summary>Sufficient conservative checks for the standard splice, added to the
    /// existing gross sections, strip buckling, bolts and detailing. No plastic redistribution.
    /// The net stress check deliberately retains holes under compression as well.</summary>
    internal static class WeldedIBeamSpliceStandardPlates
    {
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p) => Verify(p, false);

        /// <summary>Opt-in direct action checks for collinear longitudinal bolt forces.
        /// Mixed-direction groups retain the sufficient fastener hierarchy.</summary>
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces)
            => Verify(p, actualCollinearBlockForces, SpliceFlangeFaceModel.BalancedEqualFaces);
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces, SpliceFlangeFaceModel faceModel)
            => Verify(p, actualCollinearBlockForces, faceModel, SplicePlateStabilityMethod.CompactCovers);
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, bool actualCollinearBlockForces,
            SpliceFlangeFaceModel faceModel, SplicePlateStabilityMethod plateStability)
            => Verify(p, actualCollinearBlockForces ? SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback
                : SpliceBlockVerificationMethod.FastenerHierarchy, faceModel, plateStability);
        public static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, SpliceBlockVerificationMethod blockMethod,
            SpliceFlangeFaceModel faceModel, SplicePlateStabilityMethod plateStability)
        {
            if (!Enum.IsDefined(typeof(SplicePlateStabilityMethod), plateStability)) throw new ArgumentOutOfRangeException(nameof(plateStability));
            if (!Enum.IsDefined(typeof(SpliceBlockVerificationMethod), blockMethod)) throw new ArgumentOutOfRangeException(nameof(blockMethod));
            if (p == null) throw new ArgumentNullException(nameof(p));
            var plates = WeldedIBeamSplicePlates.Verify(p.Plates);
            return Verify(p, blockMethod, plateStability, plates,
                WeldedIBeamSpliceStandardModel.Verify(p, faceModel, plates.PreparedBolts.Forces));
        }
        internal static WeldedIBeamSpliceStandardPlateResult Verify(WeldedIBeamSpliceDetailInput p, SpliceBlockVerificationMethod blockMethod,
            SplicePlateStabilityMethod plateStability, WeldedIBeamSplicePlateResult plates, ConnectionVerification<WeldedIBeamSpliceDetailInput> applicability)
        {
            if (!Enum.IsDefined(typeof(SplicePlateStabilityMethod), plateStability)) throw new ArgumentOutOfRangeException(nameof(plateStability));
            if (!Enum.IsDefined(typeof(SpliceBlockVerificationMethod), blockMethod)) throw new ArgumentOutOfRangeException(nameof(blockMethod));
            if (!ReferenceEquals(p.Plates, plates.Input) || !ReferenceEquals(p, applicability.Input)) throw new ArgumentException("Prepared results belong to another input.");
            bool actualCollinearBlockForces = blockMethod != SpliceBlockVerificationMethod.FastenerHierarchy;
            bool mixedInteraction = blockMethod == SpliceBlockVerificationMethod.ActualForcesWithMixedBlockInteraction;
            bool shearBuckling = plateStability == SplicePlateStabilityMethod.EffectiveCompressionWithShearBuckling;
            var code = p.Plates.Policy.Code;
            var f = plates.PreparedBolts.Forces;
            var issues = new List<string>(applicability.Diagnostics.Concat(plates.Diagnostics));
            if (!SpliceIdentity.Code(code, p.Plates.Bolts.Code)) issues.Add("PLATE_CODE_MISMATCH");
            var net = new List<ConnectionCheck>(); var blocks = new List<ConnectionCheck>(); var compact = new List<ConnectionCheck>();
            var direct = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var reported = new List<SectionResistanceValue>();
            if (issues.Count == 0)
            {
                var prepared = p.Prepared.StandardPlates;
                for (int ci = 0; ci < plates.Covers.Count; ci++)
                {
                    var c = plates.Covers[ci]; var pc = prepared.Covers[ci];
                    if (pc.Invalid) { issues.Add(pc.InvalidIssue); continue; }
                    Prepared(pc.Anet); Prepared(pc.Inet); Prepared(pc.Wnet);
                    for (int k = 0; k < c.Cuts.Count; k++)
                    {
                        var cut = c.Cuts[k];
                        double sigma = Math.Abs(cut.N) / pc.A + Math.Abs(cut.M) / pc.W;
                        double tau = 1.5 * Math.Abs(cut.V) / pc.A;
                        net.Add(new ConnectionCheck(pc.CutId(k), ElasticBoltGroup.Hypot(sigma, Math.Sqrt(3) * tau), pc.Limit, NetReference, "MPa"));
                    }
                    // Sufficient no-shear-buckling domain, eta=1.2 conservatively for all admitted grades.
                    // Longitudinal compression and transverse outstands are checked independently by Plates.
                    bool zeroShear = c.Cuts.All(cut => cut.V == 0);
                    // Exact absence of shear is an independent sufficient condition;
                    // no force tolerance and no waiver of compression/net-section checks.
                    Prepared(zeroShear ? pc.ZeroShear1 : pc.ZeroShear0);
                    if (plateStability == SplicePlateStabilityMethod.StandardBoltedCovers)
                    {
                        // Published conventional bolted-cover procedure, not a free web
                        // panel with fictitious continuous supports through the bolt rows.
                        // Extra conservative class3 outstand bounds also apply at zero N.
                        compact.AddRange(pc.StandardBoltedDomain);
                        Prepared(pc.UsesStandardBolted); Prepared(pc.WholeWidthRatio);
                    }
                    else if (shearBuckling && !zeroShear)
                    {
                        // The EN1993-1-5 section5 resistance of the whole-width panel replaces the
                        // no-buckling screen; only the restraint its idealization needs is a domain.
                        compact.AddRange(pc.ShearPanelDomain);
                        Prepared(pc.UsesShearBucklingResistance); Prepared(pc.WholeWidthRatio);
                    }
                    else compact.Add(zeroShear ? pc.NoShearBucklingAtZeroShear : pc.NoShearBuckling);
                    Hierarchy(pc.Hierarchy);
                }
                ParentNet(prepared.Parent);
                // The unnotched beam WEB is not a block-tearing case (SCI P398 p59).
                // Flange blocks are bounded using both shear planes and the actual free butt edge.
                foreach (var h in prepared.ParentHierarchies) Hierarchy(h);
            }
            return new WeldedIBeamSpliceStandardPlateResult(Result("NetSections", net, ConnectionCheckRole.Resistance), Result("BlockHierarchy", blocks, ConnectionCheckRole.Applicability),
                Result("CompactDomain", compact, ConnectionCheckRole.Applicability), Result("DirectBlockTearing", direct, ConnectionCheckRole.Resistance));
            ConnectionVerification<WeldedIBeamSpliceDetailInput> Result(string name, IEnumerable<ConnectionCheck> checks, ConnectionCheckRole role)
                => new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, code, "Steel.Connection.WeldedIBeamSplice.Standard." + name,
                    "DirectBlockTearing uses collinear actual bolt forces and eccentric resistance, without cancellation. Other block groups retain the hierarchy. Conservative standard-detail sufficient checks. Block/compact-domain failure means outside this method, not an actual load-capacity failure."
                    + (mixedInteraction ? " Mixed-direction paths outside the hierarchy are verified in DirectBlockTearing by the actual-force block interaction." : ""),
                    checks, trace, issues.Distinct(), Assumptions(mixedInteraction, shearBuckling), reported, role);
            void Prepared(StandardPlatePreparation.Entry e) { if (e.Value == null) issues.Add(e.Issue); else trace.Add(e.Value); }
            void ParentNet(StandardPlatePreparation.ParentSection s)
            {
                if (s.Invalid) { issues.Add("INVALID_PARENT_NET_SECTION"); return; }
                double n = f.ActionsAtJoint.Fx, v = f.ActionsAtJoint.Fy;
                double m = Math.Abs(f.ActionsAtJoint.Mz + s.Yc * n) + s.Half * Math.Abs(v);
                double sigma = Math.Abs(n) / s.Area + m * s.Lever / s.Inertia;
                double tau = 1.5 * Math.Abs(v) / s.WebArea;
                net.Add(new ConnectionCheck("Parent.NetElasticNVM", ElasticBoltGroup.Hypot(sigma, Math.Sqrt(3) * tau), s.Limit,
                    "EN1993-1-1:2005 6.2.1,6.2.3; full-load, all-holes-aligned elastic sufficient check, no splice-cover credit", "MPa"));
                Prepared(s.Anet); Prepared(s.Offset); Prepared(s.Inet);
                if (WeldedIBeamSplicePlates.Finite(m)) trace.Add(new SectionResistanceValue("Parent.BoundingMomentAboutNetCentroid", m, "Nmm"));
                else issues.Add("Parent.BoundingMomentAboutNetCentroid.NUMERIC_RANGE");
            }
            void Hierarchy(StandardPlatePreparation.HierarchyPreparation h)
            {
                // For a parent block, opposite face forces must not cancel: retain the sum
                // of absolute actions on all physical planes (also bounds a local flange couple).
                var sumX = new double[h.PositionCount]; var sumY = new double[h.PositionCount];
                for (int j = 0; j < h.AppliedGroup.Length; j++)
                {
                    var force = f.CoverGroups[h.AppliedGroup[j]].Distribution.Forces[h.AppliedForce[j]];
                    int q = h.AppliedPosition[j]; sumX[q] += Math.Abs(force.Fx); sumY[q] += Math.Abs(force.Fy);
                }
                double scale = 0;
                for (int q = 0; q < h.PositionCount; q++) scale += Math.Abs(sumX[q]) + Math.Abs(sumY[q]);
                bool collinear = actualCollinearBlockForces;
                for (int q = 0; collinear && q < h.PositionCount; q++) collinear = Math.Abs(sumY[q]) <= 32 * 2.2204460492503131e-16 * scale;
                var method = collinear ? h.Collinear : h.Hierarchical;
                trace.Add(method); reported.Add(method);
                if (!h.CapacityComplete) { issues.AddRange(h.CapacityDiagnostics); return; }
                bool interaction = mixedInteraction && !collinear;
                StandardPlatePreparation.MixedPath[][] mixed = null;
                double[] netX = null, netY = null;
                if (interaction)
                {
                    // Signed resultants per position, used only for the moment about the enclosed-bolt
                    // centroid; the force components keep the absolute sums above (no cancellation).
                    mixed = h.Mixed; netX = new double[h.PositionCount]; netY = new double[h.PositionCount];
                    for (int j = 0; j < h.AppliedGroup.Length; j++)
                    {
                        var force = f.CoverGroups[h.AppliedGroup[j]].Distribution.Forces[h.AppliedForce[j]];
                        int q = h.AppliedPosition[j]; netX[q] += force.Fx; netY[q] += force.Fy;
                    }
                }
                int interacted = 0; ConnectionCheck governing = null; double[] governingTerms = null;
                for (int axis = 0; axis < (collinear ? 1 : 2); axis++) for (int sign = 0; sign < 2; sign++)
                {
                    var paths = h.Paths[2 * axis + sign];
                    for (int index = 0; index < paths.Length; index++)
                    {
                        var path = paths[index];
                        if (path.Issue != null) { issues.Add(path.Issue); continue; }
                        if (interaction)
                        {
                            // Per path: the unchanged sufficient hierarchy, or the actual-force interaction.
                            var sufficient = new ConnectionCheck(path.Id, path.Fasteners * h.Rb, path.Resistance, HierarchyReference, "N", ConnectionCheckRole.Applicability);
                            if (sufficient.Passed) { blocks.Add(sufficient); continue; }
                            var m = mixed[2 * axis + sign][index];
                            if (m.Issue != null) { issues.Add(m.Issue); continue; }
                            var terms = MixedTerms(m, axis);
                            var check = m.Edge ? new ConnectionCheck(path.Id + ".MixedInteraction", terms[0] / path.Resistance + terms[1] / m.Rn + terms[2] / m.Rm, 1, MixedEdgeReference, "1")
                                : new ConnectionCheck(path.Id + ".MixedInteraction", terms[0], path.Resistance, MixedConfinedReference, "N");
                            if (!WeldedIBeamSplicePlates.Finite(check.Utilization)) { issues.Add(path.Id + ".MIXED_INTERACTION_NUMERIC_RANGE"); continue; }
                            direct.Add(check); interacted++;
                            if (governing == null || check.Utilization > governing.Utilization)
                            { governing = check; governingTerms = m.Edge ? new[] { terms[0], path.Resistance, terms[1], m.Rn, terms[2], m.Rm } : new[] { terms[0], path.Resistance }; }
                            continue;
                        }
                        double demand;
                        if (collinear) { demand = 0; foreach (int point in path.Subset) demand += Math.Abs(sumX[h.PointPosition[point]]); }
                        else demand = path.Fasteners * h.Rb;
                        (collinear ? direct : blocks).Add(new ConnectionCheck(path.Id, demand, path.Resistance, collinear ? DirectReference : HierarchyReference,
                            "N", collinear ? ConnectionCheckRole.Resistance : ConnectionCheckRole.Applicability));
                    }
                }
                if (!interaction) return;
                Reported(h.Name + ".MixedInteractionPaths", interacted, "1");
                if (governing == null) return;
                Reported(h.Name + ".MixedInteraction.GoverningUtilization", governing.Utilization, "1");
                string[] names = { "Pt", "Rt", "Pn", "Rn", "M", "Rm" }; string[] units = { "N", "N", "N", "N", "Nmm", "Nmm" };
                for (int k = 0; k < governingTerms.Length; k++) Reported(h.Name + ".MixedInteraction.Governing" + names[k], governingTerms[k], units[k]);

                // Pt and Pn are sums of absolute bolt-plane components (no cancellation, as the collinear
                // checks); M is the resultant moment of the same applied forces about the enclosed-bolt centroid.
                double[] MixedTerms(StandardPlatePreparation.MixedPath m, int axis)
                {
                    double pt = 0, pn = 0, moment = 0;
                    foreach (int point in m.Subset)
                    {
                        int q = h.PointPosition[point];
                        pt += axis == 0 ? sumX[q] : sumY[q]; pn += axis == 0 ? sumY[q] : sumX[q];
                        if (m.Edge) moment += (h.Points[point].X - m.Gx) * netY[q] - (h.Points[point].Y - m.Gy) * netX[q];
                    }
                    return new[] { pt, pn, Math.Abs(moment) };
                }
            }
            void Reported(string name, double value, string unit)
            {
                if (!WeldedIBeamSplicePlates.Finite(value)) { issues.Add(name + ".NUMERIC_RANGE"); return; }
                var entry = new SectionResistanceValue(name, value, unit); trace.Add(entry); reported.Add(entry);
            }
        }
        private static readonly string[] BaseAssumptions = {
            "Net rectangular holes remove both area and second moment. Every cover cut conservatively uses the perforated section, including the unperforated centre. Stress maxima are combined conservatively with 1.5 V/Anet; limit=min(fy/gammaM0,0.9fu/gammaM2). This elastic sufficient criterion is not a new plastic rupture interaction formula.",
            "Block hierarchy enumerates orthogonal rectangular U and edge L paths, all free-edge prefixes and contiguous transverse subsets, in both directions. Eccentric expression 0.5 Ant fu/gammaM2 + Anv fy/(sqrt(3) gammaM0) is always used. Demand is the SUM of UNREDUCED design shear resistances of the enclosed bolt planes, not the load resultant. No cancellation or plastic bolt redistribution is credited.",
            "Opt-in direct checks are limited to longitudinal collinear bolt actions (including sign reversal). All rectangular prefixes and transverse subsets are checked using sum(abs(Fx)); opposite loads on a continuous cover do not cancel. The eccentric coefficient 0.5 is retained for every subset. No mixed-direction N-V-M rupture interaction is inferred. Non-collinear groups use the unchanged hierarchy.",
            "Hierarchy requires every path to be stronger than all its fasteners. It is a sufficient exclusion of block tearing for this regular detail; a failed hierarchy requires a separate loaded-block method and prevents whole-detail qualification.",
            "Parent net-section check assumes all flange/web holes aligned longitudinally and the full member actions retained in the parent, with no cover credit. Maximum lap-end moment is included. Unnotched parent-web block tearing is inapplicable per SCI P398 p59.",
            "CompactCovers retains the whole-width no-shear-buckling screen. StandardBoltedCovers explicitly uses SCI P398 pp57-59 for continuous flat rectangular covers with regular closely spaced filled normal fasteners, class1-3 strips/outstands in both directions and every existing compression/shear/bending/net/block check. It is not an EN1993-1-5 free-panel buckling or tension-field method; locally class4 strips remain outside StandardBoltedCovers. EffectiveCompressionWithLowShear is assessed separately and retains the whole-width screen. No continuous line supports are inferred from discrete bolts." };
        private static string[] Assumptions(bool mixedInteraction, bool shearBuckling)
        {
            if (!mixedInteraction && !shearBuckling) return BaseAssumptions;
            var a = new List<string>(BaseAssumptions);
            if (mixedInteraction) a.Add("ActualForcesWithMixedBlockInteraction: a mixed-direction path failing the hierarchy is verified with the actual bolt actions of the same enumerated rectangle. "
                + "A U path admits no rigid separation except translation towards its free edge (side and end cuts are confined), so only sum(abs(F)) along that direction is compared with its Veff,2. "
                + "An edge L path can translate towards either free edge and rotate: Pt/Veff,2,t + Pn/Veff,2,n + |M|/Rm <= 1 at the centroid of the enclosed bolts. Veff,2,n is the same rectangle torn towards the side edge; "
                + "Rm=min(0.5fu/gammaM2,fy/gammaM0)*t*(Z_end+Z_side) is the pure couple of antisymmetric normal tractions on the two net cuts, a static lower bound. "
                + "The block domain is convex, so the linear interaction of these single-action resistances lies inside it. Pt, Pn are absolute sums over all bolt planes; M is the resultant moment of the enclosed bolt forces. "
                + "Contact pressures of confined cuts are carried by the surrounding plate checks (bearing, cover N-V-M cuts, compression). Non-rectangular paths are not enumerated.");
            if (shearBuckling) a.Add("EffectiveCompressionWithShearBuckling replaces the whole-width screen of a sheared cover by the EN1993-1-5 section5 resistance of the whole-width panel, "
                + "restrained along its long edges by the outer fastener rows (at least two rows, longitudinal gaps <= min(14t,200mm)); interior fasteners are not credited as stiffeners.");
            return a.ToArray();
        }
        private const string NetReference = "EN1993-1-1:2005 6.2.1(5),6.2.3; conservative elastic NET stress screen, also sufficient for category C net yielding";
        private const string DirectReference = "EN1993-1-8:2005 3.10.2(3) eq3.10; collinear actual force sum, eccentric factor0.5; SCI P398 pp136,139 path geometry";
        private const string HierarchyReference = "EN1993-1-8:2005 3.10.2(3) eq3.10; sufficient fastener/block hierarchy, NOT actual action demand";
        private const string MixedConfinedReference = "EN1993-1-8:2005 3.10.2(3) eq3.10; confined U path, actual sum(abs(F)) along its only admissible tearing direction, eccentric factor0.5";
        private const string MixedEdgeReference = "EN1993-1-8:2005 3.10.2(3) eq3.10 in both tearing directions; convex linear interaction Pt/Veff,2t+Pn/Veff,2n+|M|/Rm, static lower-bound couple Rm";
    }

    /// <summary>Net sections, compact-domain checks and the complete block-path enumeration of one
    /// physical detail: all demand independent. Paths keep the original order and numbering.</summary>
    internal sealed class StandardPlatePreparation
    {
        internal sealed class Entry { internal SectionResistanceValue Value; internal string Issue; }
        /// <summary>Mode 0 U, 1 edge L at the minimum transverse edge, 2 at the maximum. Lt/Ls are the net
        /// lengths of the end and of the side cut(s) used by the original resistance.</summary>
        internal sealed class Path
        {
            internal string Issue, Id; internal double Resistance; internal int Fasteners; internal int[] Subset;
            internal int Axis, Sign, K, First, Last, Mode; internal double Lt, Ls;
        }
        /// <summary>Demand-independent data of one path for the mixed-direction interaction.</summary>
        internal sealed class MixedPath { internal string Issue; internal int[] Subset; internal bool Edge; internal double Rn, Rm, Gx, Gy; }
        internal sealed class HierarchyPreparation
        {
            internal SectionResistanceValue Collinear, Hierarchical;
            internal bool CapacityComplete; internal string[] CapacityDiagnostics; internal double Rb;
            internal int[] AppliedGroup, AppliedForce, AppliedPosition, PointPosition;
            internal int PositionCount;
            internal Path[][] Paths;
            internal string Name; internal ConnectionPoint2D[] Points; internal double XMin, XMax, YMin, YMax, Hole;
            internal ConnectionPlate Plate; internal double GammaM0, GammaM2;
            private MixedPath[][] mixed;
            /// <summary>Built on first use by the opt-in mixed method only; identical for every case.</summary>
            internal MixedPath[][] Mixed => mixed ?? System.Threading.LazyInitializer.EnsureInitialized(ref mixed, BuildMixed);

            private MixedPath[][] BuildMixed()
            {
                var result = new MixedPath[4][];
                // Normal tractions of the static couple never exceed the eccentric tension level, nor yield.
                double sigma = Math.Min(.5 * Plate.Fu / GammaM2, Plate.Fy / GammaM0), t = Plate.Thickness;
                for (int list = 0; list < 4; list++)
                {
                    int axis = list / 2, sign = list % 2 == 0 ? -1 : 1;
                    double[] us = Points.Select(q => sign * (axis == 0 ? q.X : q.Y)).Distinct().OrderBy(x => x).ToArray();
                    double[] vs = Points.Select(q => axis == 0 ? q.Y : q.X).Distinct().OrderBy(x => x).ToArray();
                    double start = sign == 1 ? (axis == 0 ? XMin : YMin) : -(axis == 0 ? XMax : YMax);
                    double vmin = axis == 0 ? YMin : XMin, vmax = axis == 0 ? YMax : XMax;
                    var paths = Paths[list]; var mixedPaths = new MixedPath[paths.Length];
                    for (int i = 0; i < paths.Length; i++)
                    {
                        var path = paths[i];
                        if (path.Issue != null) continue;
                        double uk = us[path.K - 1], v0 = vs[path.First], v1 = vs[path.Last];
                        var m = new MixedPath
                        {
                            Edge = path.Mode != 0,
                            Subset = path.Subset ?? Enumerable.Range(0, Points.Length).Where(n => sign * (axis == 0 ? Points[n].X : Points[n].Y) <= uk
                                && (axis == 0 ? Points[n].Y : Points[n].X) >= v0 && (axis == 0 ? Points[n].Y : Points[n].X) <= v1).ToArray()
                        };
                        mixedPaths[i] = m;
                        if (!m.Edge) continue;
                        m.Gx = m.Subset.Average(n => Points[n].X); m.Gy = m.Subset.Average(n => Points[n].Y);
                        // End cut from the free transverse edge to the boundary row; side cut along that row from the free end.
                        double ze = path.Mode == 1 ? Couple(v1 - vmin, vs.Take(path.Last + 1).Select(v => v - vmin))
                            : Couple(vmax - v0, vs.Skip(path.First).Select(v => v - v0));
                        double zs = Couple(uk - start, us.Take(path.K).Select(u => u - start));
                        // Veff,2 of the same rectangle torn towards its side edge: tension on the side cut, shear on the end cut.
                        m.Rn = (.5 * path.Ls * Plate.Fu / GammaM2 + PlasticShear.Resistance(path.Lt, Plate.Fy, GammaM0)) * t;
                        m.Rm = sigma * t * (ze + zs);
                        if (!(m.Rn > 0 && m.Rm > 0) || !WeldedIBeamSplicePlates.Finite(m.Rn + m.Rm + m.Gx + m.Gy)) m.Issue = Name + ".INVALID_MIXED_BLOCK_PATH";
                    }
                    result[list] = mixedPaths;
                }
                return result;
            }
            /// <summary>Z=sum of |s-s_h| ds over the net cut [0,length] minus the holes, with s_h halving the net
            /// length: the pure couple of a unit rectangular antisymmetric normal traction, per unit thickness.</summary>
            private double Couple(double length, IEnumerable<double> centres)
            {
                var segments = new List<double[]>(); double from = 0;
                foreach (double c in centres.OrderBy(x => x))
                {
                    double lo = Math.Max(0, c - Hole / 2), hi = Math.Min(length, c + Hole / 2);
                    if (lo > from) segments.Add(new[] { from, lo });
                    from = Math.Max(from, hi);
                }
                if (length > from) segments.Add(new[] { from, length });
                double net = segments.Sum(s => s[1] - s[0]), split = 0, below = 0;
                foreach (var s in segments)
                {
                    if (below + s[1] - s[0] >= net / 2) { split = s[0] + net / 2 - below; break; }
                    below += s[1] - s[0];
                }
                return segments.Sum(s => s[1] <= split ? (Math.Pow(split - s[0], 2) - Math.Pow(split - s[1], 2)) / 2
                    : s[0] >= split ? (Math.Pow(s[1] - split, 2) - Math.Pow(s[0] - split, 2)) / 2
                    : (Math.Pow(split - s[0], 2) + Math.Pow(s[1] - split, 2)) / 2);
            }
        }
        internal sealed class Cover
        {
            internal bool Invalid; internal string InvalidIssue;
            internal double A, W, Limit;
            internal Entry Anet, Inet, Wnet, ZeroShear0, ZeroShear1, UsesStandardBolted, WholeWidthRatio, UsesShearBucklingResistance;
            internal ConnectionCheck[] StandardBoltedDomain, ShearPanelDomain;
            internal ConnectionCheck NoShearBuckling, NoShearBucklingAtZeroShear;
            internal HierarchyPreparation Hierarchy;
            private string[] cutIds; private string name;
            internal string CutId(int k)
            {
                var ids = cutIds;
                if (ids == null || k >= ids.Length)
                {
                    ids = Enumerable.Range(0, Math.Max(k + 1, 2 * (ids?.Length ?? 8))).Select(i => name + ".Cut" + i + ".NetElasticNVM").ToArray();
                    cutIds = ids;
                }
                return ids[k];
            }
            internal Cover(string name) { this.name = name; }
        }
        internal sealed class ParentSection
        {
            internal bool Invalid; internal double Area, Yc, Inertia, Half, Lever, WebArea, Limit; internal Entry Anet, Offset, Inet;
        }
        internal readonly Cover[] Covers;
        internal readonly ParentSection Parent;
        internal readonly HierarchyPreparation[] ParentHierarchies;

        internal StandardPlatePreparation(WeldedIBeamSpliceDetailInput p)
        {
            var d = p.Plates.Bolts.Detail; var code = p.Plates.Policy.Code; var descriptors = d.Geometry.Groups;
            var coverOrder = descriptors.Select(x => x.Cover).Distinct().ToArray();
            Covers = new Cover[coverOrder.Length];
            for (int ci = 0; ci < coverOrder.Length; ci++)
            {
                var cover = coverOrder[ci];
                int left = Array.FindIndex(descriptors, x => x.Cover == cover && x.Side == SpliceSide.Left);
                var members = Enumerable.Range(0, descriptors.Length).Where(m => descriptors[m].Cover == cover).ToArray();
                var g = descriptors[left]; var plate = g.Plate; var points = g.Group.Value.Points;
                bool isWeb = cover == WeldedSpliceCover.WebNegative || cover == WeldedSpliceCover.WebPositive;
                var bolt = isWeb ? p.Plates.Bolts.WebBolt : p.Plates.Bolts.FlangeBolt;
                var grid = isWeb ? d.WebBolts : WeldedIBeamSplicePlates.Top(cover) ? d.Top.Bolts : d.Bottom.Bolts;
                double end = isWeb ? d.WebCoverEnd : WeldedIBeamSplicePlates.Top(cover) ? d.Top.EndDistance : d.Bottom.EndDistance;
                double centre = (g.Minimum + g.Maximum) / 2;
                var holes = points.Select(x => x.Y - centre).Distinct().ToArray();
                double a = (plate.Width - holes.Length * bolt.HoleDiameter) * plate.Thickness;
                double i = plate.Thickness * Math.Pow(plate.Width, 3) / 12
                    - holes.Sum(y => plate.Thickness * (Math.Pow(bolt.HoleDiameter, 3) / 12 + bolt.HoleDiameter * y * y));
                double w = i / (plate.Width / 2);
                string name = cover.ToString();
                var c = new Cover(name) { A = a, W = w, InvalidIssue = name + ".INVALID_NET_SECTION" };
                Covers[ci] = c;
                if (!(a > 0) || !(w > 0)) { c.Invalid = true; continue; }
                c.Anet = Value(name + ".Anet", a, "mm2"); c.Inet = Value(name + ".Inet", i, "mm4"); c.Wnet = Value(name + ".Wnet", w, "mm3");
                c.Limit = Math.Min(plate.Fy / code.Plates.GammaM0, .9 * plate.Fu / code.Plates.GammaM2);
                c.ZeroShear0 = Value(name + ".ZeroShearAction", 0, "1"); c.ZeroShear1 = Value(name + ".ZeroShearAction", 1, "1");
                string prefix = name + ".StandardBoltedCover.";
                double eps = Math.Sqrt(235 / plate.Fy), t = plate.Thickness;
                var domain = new List<ConnectionCheck>();
                Domain("MaximumLongitudinalGap", grid.MaximumLongitudinalGap, Math.Min(14 * t, 200), "mm");
                if (holes.Length > 1)
                {
                    Domain("TransversePitch", grid.TransversePitch, Math.Min(14 * t, 200), "mm");
                    Domain("InternalStripClass3", grid.TransversePitch / t, 42 * eps);
                }
                Domain("LowerOutstandClass3", (points.Min(q => q.Y) - g.Minimum) / t, 14 * eps);
                Domain("UpperOutstandClass3", (g.Maximum - points.Max(q => q.Y)) / t, 14 * eps);
                Domain("EndOutstandClass3", end / t, 14 * eps);
                c.StandardBoltedDomain = domain.ToArray();
                c.UsesStandardBolted = Value(name + ".UsesStandardBoltedCoverMethod", 1, "1");
                // Restraint assumed by the whole-width EN1993-1-5 panel: two outer rows, Table3.3 gaps along them.
                c.ShearPanelDomain = new[] {
                    new ConnectionCheck(name + ".ShearPanel.MinimumTransverseRows", 2, holes.Length, ShearPanelReference, "1", ConnectionCheckRole.Applicability),
                    new ConnectionCheck(name + ".ShearPanel.MaximumLongitudinalGap", grid.MaximumLongitudinalGap, Math.Min(14 * t, 200), ShearPanelReference, "mm", ConnectionCheckRole.Applicability) };
                c.UsesShearBucklingResistance = Value(name + ".UsesShearBucklingResistance", 1, "1");
                c.WholeWidthRatio = Value(name + ".WholeWidthThicknessRatio", plate.Width / t, "1");
                c.NoShearBuckling = NoShearBuckling(plate.Width / plate.Thickness);
                c.NoShearBucklingAtZeroShear = NoShearBuckling(0);
                double half = grid.FirstBoltDistance + grid.LongitudinalSpan + end;
                c.Hierarchy = Hierarchy(name, plate, bolt, members.SelectMany(m => descriptors[m].Group.Value.Points).Distinct().ToArray(),
                    -half, half, g.Minimum, g.Maximum, 1, members);
                void Domain(string id, double demand, double resistance, string unit = "1") => domain.Add(new ConnectionCheck(prefix + id,
                    demand, resistance, "SCI P398 4.3 pp57-59 conventional bolted cover procedure; EC Table3.3 fastener spacing; supplementary conservative class3 bounds on every outstand", unit, ConnectionCheckRole.Applicability));
                ConnectionCheck NoShearBuckling(double demand) => new ConnectionCheck(name + ".NoShearBucklingDomain", demand,
                    60 * Math.Sqrt(235 / plate.Fy), "EN1993-1-1:2005 6.2.6(6), EN1993-1-5:2006 5.1; eta=1.2 sufficient domain, or exactly zero shear at every cut", "1", ConnectionCheckRole.Applicability);
            }
            Parent = ParentNet();
            var parents = new List<HierarchyPreparation>();
            foreach (bool top in new[] { true, false }) foreach (SpliceSide side in new[] { SpliceSide.Left, SpliceSide.Right })
            {
                var flange = top ? d.Top : d.Bottom; var parent = top ? d.Section.TopFlange : d.Section.BottomFlange;
                var points = flange.Bolts.Points(side).ToArray();
                double far = flange.Bolts.FirstBoltDistance + flange.Bolts.LongitudinalSpan + flange.EndDistance;
                var members = Enumerable.Range(0, descriptors.Length).Where(i => descriptors[i].Side == side && (top
                    ? descriptors[i].Cover == WeldedSpliceCover.TopOuter || descriptors[i].Cover == WeldedSpliceCover.TopInnerNegative || descriptors[i].Cover == WeldedSpliceCover.TopInnerPositive
                    : descriptors[i].Cover == WeldedSpliceCover.BottomOuter || descriptors[i].Cover == WeldedSpliceCover.BottomInnerNegative || descriptors[i].Cover == WeldedSpliceCover.BottomInnerPositive)).ToArray();
                // Artificial far free edge is conservative; the real parent continues.
                parents.Add(Hierarchy((top ? "Top" : "Bottom") + ".Parent." + side, parent, p.Plates.Bolts.FlangeBolt,
                    points, side == SpliceSide.Left ? -far : d.ButtGap / 2,
                    side == SpliceSide.Left ? -d.ButtGap / 2 : far, -parent.Width / 2, parent.Width / 2, 2, members));
            }
            ParentHierarchies = parents.ToArray();

            ParentSection ParentNet()
            {
                var s = d.Section; double df = p.Plates.Bolts.FlangeBolt.HoleDiameter, dw = p.Plates.Bolts.WebBolt.HoleDiameter;
                double at = (s.TopFlange.Width - d.Top.Bolts.TransverseRows * df) * s.TopFlange.Thickness;
                double ab = (s.BottomFlange.Width - d.Bottom.Bolts.TransverseRows * df) * s.BottomFlange.Thickness;
                double aw = (s.Web.Width - d.WebBolts.TransverseRows * dw) * s.Web.Thickness;
                double area = at + ab + aw, yc = (at * s.TopY + aw * s.WebY + ab * s.BottomY) / area;
                double iw = s.Web.Thickness * Math.Pow(s.Web.Width, 3) / 12
                    - d.WebBolts.Points(SpliceSide.Left).Select(x => x.Y).Distinct().Sum(y => s.Web.Thickness * (Math.Pow(dw, 3) / 12 + dw * y * y));
                double inertia = at * (s.TopFlange.Thickness * s.TopFlange.Thickness / 12 + Math.Pow(s.TopY - yc, 2))
                    + ab * (s.BottomFlange.Thickness * s.BottomFlange.Thickness / 12 + Math.Pow(s.BottomY - yc, 2))
                    + iw + aw * Math.Pow(s.WebY - yc, 2);
                if (!(at > 0 && ab > 0 && aw > 0 && inertia > 0)) return new ParentSection { Invalid = true };
                double half = Math.Max(d.WebBolts.FirstBoltDistance + d.WebBolts.LongitudinalSpan + d.WebCoverEnd,
                    Math.Max(d.Top.Bolts.FirstBoltDistance + d.Top.Bolts.LongitudinalSpan + d.Top.EndDistance,
                    d.Bottom.Bolts.FirstBoltDistance + d.Bottom.Bolts.LongitudinalSpan + d.Bottom.EndDistance));
                return new ParentSection
                {
                    Area = area, Yc = yc, Inertia = inertia, Half = half, Lever = Math.Max(s.Height - s.CentroidY - yc, s.CentroidY + yc), WebArea = aw,
                    Limit = new[] { s.TopFlange, s.Web, s.BottomFlange }.Min(q => Math.Min(q.Fy / code.Plates.GammaM0, .9 * q.Fu / code.Plates.GammaM2)),
                    Anet = Value("Parent.Anet", area, "mm2"), Offset = Value("Parent.NetCentroidOffsetY", yc, "mm"), Inet = Value("Parent.Inet", inertia, "mm4")
                };
            }
            HierarchyPreparation Hierarchy(string name, ConnectionPlate plate, ConnectionBolt bolt, ConnectionPoint2D[] points,
                double xmin, double xmax, double ymin, double ymax, int planes, int[] members)
            {
                var h = new HierarchyPreparation
                {
                    Collinear = new SectionResistanceValue(name + ".ActualCollinearBlockMethod", 1, "1"),
                    Hierarchical = new SectionResistanceValue(name + ".ActualCollinearBlockMethod", 0, "1"),
                    Name = name, Points = points, XMin = xmin, XMax = xmax, YMin = ymin, YMax = ymax, Hole = bolt.HoleDiameter,
                    Plate = plate, GammaM0 = code.Plates.GammaM0, GammaM2 = code.Plates.GammaM2
                };
                var applied = members.SelectMany(m => descriptors[m].Group.Value.Points.Select((point, k) => (m, k, point))).ToArray();
                var positions = new Dictionary<ConnectionPoint2D, int>();
                foreach (var x in applied) if (!positions.ContainsKey(x.point)) positions.Add(x.point, positions.Count);
                h.AppliedGroup = applied.Select(x => x.m).ToArray(); h.AppliedForce = applied.Select(x => x.k).ToArray();
                h.AppliedPosition = applied.Select(x => positions[x.point]).ToArray(); h.PositionCount = positions.Count;
                h.PointPosition = points.Select(q => positions[q]).ToArray();
                var capacity = BoltStrength.Verify(new BoltStrengthInput(code, bolt, 0, 0, planes, 0, 0, p.Plates.Bolts.HoleClearanceServiceabilitySource), out var capacityValues);
                h.CapacityComplete = capacity.IsComplete; h.CapacityDiagnostics = capacity.Diagnostics.ToArray(); h.Rb = capacityValues.ShearResistance;
                h.Paths = new Path[4][];
                for (int axis = 0; axis < 2; axis++) for (int signIndex = 0; signIndex < 2; signIndex++)
                {
                    int sign = signIndex == 0 ? -1 : 1; var list = new List<Path>();
                    double[] us = points.Select(q => sign * (axis == 0 ? q.X : q.Y)).Distinct().OrderBy(x => x).ToArray();
                    double[] vs = points.Select(q => axis == 0 ? q.Y : q.X).Distinct().OrderBy(x => x).ToArray();
                    double start = sign == 1 ? (axis == 0 ? xmin : ymin) : -(axis == 0 ? xmax : ymax);
                    double vmin = axis == 0 ? ymin : xmin, vmax = axis == 0 ? ymax : xmax;
                    int id = 0;
                    for (int k = 1; k <= us.Length; k++) for (int first = 0; first < vs.Length; first++) for (int last = first; last < vs.Length; last++)
                    {
                        int count = last - first + 1;
                        double lv = us[k - 1] - start - (k - .5) * bolt.HoleDiameter;
                        Add(vs[last] - vs[first] - (count - 1) * bolt.HoleDiameter, 2 * lv, "U", 0);
                        if (first == 0) Add(vs[last] - vmin - (count - .5) * bolt.HoleDiameter, lv, "Lmin", 1);
                        if (last == vs.Length - 1) Add(vmax - vs[first] - (count - .5) * bolt.HoleDiameter, lv, "Lmax", 2);
                        void Add(double lt, double ls, string mode, int modeIndex)
                        {
                            double resistance = (.5 * lt * plate.Fu / code.Plates.GammaM2 + PlasticShear.Resistance(ls, plate.Fy, code.Plates.GammaM0)) * plate.Thickness;
                            if (lt < 0 || ls <= 0 || !WeldedIBeamSplicePlates.Finite(resistance)) { list.Add(new Path { Issue = name + ".INVALID_BLOCK_PATH" }); return; }
                            double uk = us[k - 1], v0 = vs[first], v1 = vs[last];
                            list.Add(new Path
                            {
                                Id = name + ".Axis" + axis + ".Direction" + sign + ".Path" + ++id + "." + mode, Resistance = resistance, Fasteners = k * count,
                                Subset = axis == 0 ? Enumerable.Range(0, points.Length).Where(n => sign * points[n].X <= uk && points[n].Y >= v0 && points[n].Y <= v1).ToArray() : null,
                                Axis = axis, Sign = sign, K = k, First = first, Last = last, Mode = modeIndex, Lt = lt, Ls = ls
                            });
                        }
                    }
                    h.Paths[2 * axis + signIndex] = list.ToArray();
                }
                return h;
            }
        }
        private static Entry Value(string name, double value, string unit)
            => new Entry { Value = WeldedIBeamSplicePlates.Finite(value) ? new SectionResistanceValue(name, value, unit) : null, Issue = name + ".NUMERIC_RANGE" };
        private const string ShearPanelReference = "EN1993-1-5:2006 5.1-5.3 whole-width panel idealization; outer fastener rows at EN1993-1-8:2005 Table3.3 maximum spacing";
    }
}