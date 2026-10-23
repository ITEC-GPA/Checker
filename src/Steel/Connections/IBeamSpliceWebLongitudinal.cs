using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Longitudinal compression between fasteners in each web cover.
    /// Uses the exact section-force envelope of the existing concentrated-bolt
    /// model. This component is NOT a complete N-V-M plate-stability assessment:
    /// shear buckling, lateral torsional deformation, block tearing, load
    /// introduction and member/joint qualification remain separate.</summary>
    public static class IBeamSpliceWebLongitudinal
    {
        private const string Prefix = "Web.Longitudinal.";

        public static ConnectionVerification<IBeamSplicePlateInput> Verify(IBeamSplicePlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>();
            var distribution = IBeamSplicePlates.Prepare(p, issues, trace);
            void Value(string name, double value, string unit)
                => trace.Add(new SectionResistanceValue(Prefix + name, value, unit));
            if (issues.Count == 0)
            {
                var plate = p.WebCover; var grid = p.Forces.WebBolts;
                double area = plate.Width * plate.Thickness;
                double modulus = plate.Thickness * plate.Width * plate.Width / 6;
                double halfLength = grid.FirstBoltDistance + grid.LongitudinalSpan + p.WebCoverEnd;
                double pitch = Math.Max(grid.LongitudinalPitch, 2 * grid.FirstBoltDistance);
                double length = .6 * pitch, i = plate.Thickness / Math.Sqrt(12);
                double epsilon = Math.Sqrt(235 / plate.Fy);
                double lambda = length / (Math.PI * i) * Math.Sqrt(plate.Fy / p.Policy.ElasticModulus);
                // SCI P398 p57 states the exemption as p/t<=9*epsilon.
                bool exempt = pitch / plate.Thickness <= 9 * epsilon;
                double chi = exempt ? 1 : BucklingReductionArithmetic.AxialChi(
                    BucklingReductionArithmetic.Phi(.49, lambda), lambda);
                double yieldStress = plate.Fy / p.Policy.Code.Plates.GammaM0;
                double bucklingStress = exempt ? yieldStress : chi * plate.Fy / p.Policy.GammaM1;
                double edge = (plate.Width - grid.TransverseSpan) / 2;
                if (new[] { area, modulus, halfLength, pitch, length, i, lambda, chi, yieldStress, bucklingStress }
                    .Any(v => !IBeamSplicePlates.Finite(v) || v <= 0))
                    issues.Add("WEB_LONGITUDINAL_PROPERTIES_OUTSIDE_NUMERIC_RANGE");
                if (issues.Count == 0)
                {
                    Value("GrossArea", area, "mm2"); Value("ElasticModulus", modulus, "mm3");
                    Value("CoverHalfLength", halfLength, "mm");
                    Value("MaximumUnrestrainedPitch", pitch, "mm");
                    Value("EffectiveBucklingLength", length, "mm");
                    Value("RadiusOfGyration", i, "mm"); Value("Slenderness", lambda, "1");
                    Value("Chi", chi, "1"); Value("BucklingExempt", exempt ? 1 : 0, "1");
                    Value("PitchThicknessRatio", pitch / plate.Thickness, "1");
                    Value("ExemptionLimit", 9 * epsilon, "1");
                    Value("OutstandWidth", edge, "mm"); Value("OutstandThicknessRatio", edge / plate.Thickness, "1");
                    Value("OutstandClass3Limit", 14 * epsilon, "1");
                    Value("InternalWidthThicknessRatio", grid.TransversePitch / plate.Thickness, "1");
                    Value("InternalClass3Limit", 42 * epsilon, "1");
                    var bolts = distribution.BoltGroups.Where(g => g.Part == IBeamSplicePart.Web)
                        .SelectMany(g => g.Distribution.Forces).OrderBy(f => f.Position.X).ThenBy(f => f.Position.Y).ToArray();
                    var columns = bolts.GroupBy(f => f.Position.X).ToDictionary(g => g.Key, g => g.ToArray());
                    var coordinates = columns.Keys.Concat(new[] { -halfLength, 0d, halfLength }).Distinct().OrderBy(x => x);
                    var axial = new StableSum(); var shear = new StableSum(); var momentAtOrigin = new StableSum();
                    var scaleN = new StableSum(); var scaleV = new StableSum(); var scaleM = new StableSum();
                    var cuts = new List<Cut>();
                    void Capture(double x, int columnLimit)
                    {
                        double n = axial.Value, v = shear.Value, m = momentAtOrigin.Value - x * v;
                        double compression = Math.Max(0, -n / area + Math.Abs(m) / modulus);
                        double tension = Math.Max(0, n / area + Math.Abs(m) / modulus);
                        var cut = new Cut(x, columnLimit, n, v, m, compression);
                        cuts.Add(cut); string name = "Cut" + cuts.Count + ".";
                        Value(name + "X", x, "mm"); Value(name + "ColumnLimit", columnLimit, "1");
                        Value(name + "AxialForce", n, "N"); Value(name + "ShearForce", v, "N");
                        Value(name + "Moment", m, "Nmm");
                        Value(name + "CompressionStress", compression, "MPa");
                        Value(name + "TensionStress", tension, "MPa");
                    }
                    foreach (double x in coordinates)
                    {
                        if (!columns.TryGetValue(x, out var column)) { Capture(x, -1); continue; }
                        Capture(x, 0);
                        foreach (var bolt in column)
                        {
                            // Existing forces act TO the member. With a left cut and
                            // N positive in tension these signs give the cover's N.
                            // Every cover carries HALF the total web-bolt force.
                            double fx = bolt.Fx / 2, fy = bolt.Fy / 2;
                            axial.Add(fx); shear.Add(fy);
                            momentAtOrigin.Add(bolt.Position.X * fy - bolt.Position.Y * fx);
                            scaleN.Add(Math.Abs(fx)); scaleV.Add(Math.Abs(fy));
                            scaleM.Add(Math.Abs(bolt.Position.X * fy) + Math.Abs(bolt.Position.Y * fx));
                        }
                        Capture(x, 1);
                    }
                    var last = cuts[cuts.Count - 1];
                    double referenceMoment = scaleM.Value + halfLength * scaleV.Value;
                    Value("RightFreeEdgeResidual.N", last.N, "N");
                    Value("RightFreeEdgeResidual.V", last.V, "N");
                    Value("RightFreeEdgeResidual.M", last.M, "Nmm");
                    if (!Balanced(last.N, scaleN.Value) || !Balanced(last.V, scaleV.Value)
                        || !Balanced(last.M, referenceMoment))
                        issues.Add("WEB_COVER_FREE_EDGE_EQUILIBRIUM_NOT_SATISFIED");
                    if (cuts.Any(c => new[] { c.N, c.V, c.M, c.Compression }.Any(v => !IBeamSplicePlates.Finite(v))))
                        issues.Add("WEB_SECTION_ENVELOPE_OUTSIDE_NUMERIC_RANGE");
                    if (issues.Count == 0)
                    {
                        // The free edges are unloaded by definition. Retain their raw
                        // equilibrium residuals above; do not promote rounding noise
                        // there to a fictitious compressed longitudinal strip.
                        var critical = cuts.Where(c => c.X > -halfLength && c.X < halfLength)
                            .OrderByDescending(c => c.Compression).First();
                        Value("CutCount", cuts.Count, "1");
                        Value("MaximumCompression.X", critical.X, "mm");
                        Value("MaximumCompression.ColumnLimit", critical.ColumnLimit, "1");
                        Value("MaximumCompression.AxialForce", critical.N, "N");
                        Value("MaximumCompression.ShearForce", critical.V, "N");
                        Value("MaximumCompression.Moment", critical.M, "Nmm");
                        Value("MaximumCompression.Stress", critical.Compression, "MPa");
                        Value("CompressionPresent", critical.Compression > 0 ? 1 : 0, "1");
                        // Full distances, without deducting a hole radius: conservative
                        // uniform-compression geometry gates for the strip model.
                        // Pure tension/zero actions do not activate this failure mode.
                        if (critical.Compression > 0)
                        {
                            if (edge / plate.Thickness > 14 * epsilon)
                                issues.Add("WEB_COMPRESSED_OUTSTAND_OUTSIDE_CLASS3_STRIP_SCOPE");
                            if (grid.TransversePitch / plate.Thickness > 42 * epsilon)
                                issues.Add("WEB_COMPRESSED_INTERNAL_STRIP_OUTSIDE_CLASS3_SCOPE");
                        }
                        string reference = "EN1993-1-8:2005 Table3.3 note2; EN1993-1-1:2005 6.3.1 solid-strip curve c; "
                            + "SCI P398 p57 longitudinal fastener-strip analogy; conservative maximum-compression envelope"
                            + (p.Policy.Code.IsNtc ? "; supplementary model with NTC2018 partial factors" : "");
                        checks.Add(new ConnectionCheck(Prefix + "GrossCompression", critical.Compression, yieldStress, reference, "MPa"));
                        checks.Add(new ConnectionCheck(Prefix + "InterFastenerBuckling", critical.Compression, bucklingStress, reference, "MPa"));
                    }
                }
            }
            return IBeamSplicePlates.Result(p, "WebLongitudinal", "LONGITUDINAL WEB-COVER COMPONENT ONLY: exact section-force envelope of "
                + "the concentrated-bolt model and conservative longitudinal compression between fasteners. "
                + "No complete N-V-M plate stability, shear buckling, lateral torsional deformation, block tearing, "
                + "load introduction, member stability or whole-joint qualification.", checks, trace, issues,
                "All web bolts on both sides contribute half their transmitted force to each identical cover. N>0 is tension; X and Y follow the web force-distribution frame.",
                "ColumnLimit=-1 identifies a free-edge/joint cut, 0 the limit before a bolt column, 1 the limit after it. All bolts at the same X enter together; the in-plane moment jump due to their Fx couples is retained.",
                "N and V are constant between columns, M is affine. The maximum of max(0,-N/A+|M|/W) is attained at an interval endpoint. Both limits of every column and the actual free edges are traced; no group-centroid substitution.",
                "Compression acts on gross filled-hole strips: A=h*t, W=t*h^2/6. The peak stress is conservatively applied uniformly to longitudinal strips ONLY for the inter-fastener mode; concomitant V remains recorded but is not a shear-stability verification.",
                "Direct contacting, unpacked covers and effective out-of-plane restraint at filled fasteners are the assumed boundary conditions. Lcr=0.6*max(pitch,central bolt spacing), curve c. No restraint is inferred from the butt contact.",
                "Uniform-compression transverse geometry screens use full outstand width/t<=14*epsilon and inter-row width/t<=42*epsilon when compression exists. They do not prove continuous support along discrete bolt lines or close lateral/shear plate modes.",
                "The two unloaded free edges retain raw force/moment residuals and must satisfy scaled equilibrium. Their round-off stresses are excluded from selecting a fictitious governing compressed strip.");
        }

        private static bool Balanced(double residual, double scale)
            => IBeamSplicePlates.Finite(residual) && IBeamSplicePlates.Finite(scale)
                && (scale == 0 ? residual == 0 : Math.Abs(residual) <= ElasticBoltGroup.EquilibriumRelativeTolerance * scale);

        private sealed class Cut
        {
            internal double X { get; } internal int ColumnLimit { get; }
            internal double N { get; } internal double V { get; } internal double M { get; }
            internal double Compression { get; }
            internal Cut(double x, int limit, double n, double v, double m, double compression)
            { X = x; ColumnLimit = limit; N = n; V = v; M = m; Compression = compression; }
        }

        private sealed class StableSum
        {
            private double sum, correction;
            internal double Value => sum;
            internal void Add(double value)
            {
                double corrected = value - correction, next = sum + corrected;
                correction = (next - sum) - corrected; sum = next;
            }
        }
    }
}
