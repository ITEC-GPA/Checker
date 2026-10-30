using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Force state of the two-side rigid-plastic model of EN1993-1-8 Table6.7 (side A is the
    /// +Y flange). AxialCompressionOnly is the 6.2.8.2 three-T-stub resistance (M1 exactly zero).</summary>
    public enum ColumnBaseEquilibriumState
    {
        NotEvaluated, NoAxialForceOrMoment, AxialCompressionOnly,
        SideATensionSideBCompression, BothSidesInTension, SideACompressionSideBTension, BothSidesInCompression
    }

    /// <summary>Complete static ULS procedure for the declared base. A failed load or detailing check is a
    /// failure; an unestablished prerequisite (missing source or declaration, field outside the method) is a
    /// missing requirement, never a pass. With missing requirements only the failed checks are retained.</summary>
    public sealed class ColumnBaseResult
    {
        public ColumnBaseInput Input { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public bool IsStaticResistanceComplete => MissingRequirements.Count == 0 && Checks.Count != 0;
        public bool HasFailedComponent => Checks.Any(c => !c.Passed);
        public ConnectionStatus Status => HasFailedComponent ? ConnectionStatus.Failed
            : IsStaticResistanceComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => IsStaticResistanceComplete;
        /// <summary>Largest load utilization; detailing and applicability ratios are excluded.</summary>
        public double? Utilization => IsStaticResistanceComplete
            ? (double?)ConnectionOutcome.LoadChecks(Checks).Max(c => c.Utilization) : null;
        public ColumnBaseEquilibriumState State { get; }
        /// <summary>fjd of EN1993-1-8 6.2.5(7), MPa.</summary>
        public double? BearingStrength { get; }
        /// <summary>c of EN1993-1-8 (6.5), mm.</summary>
        public double? AdditionalBearingWidth { get; }
        public double? CompressionLeverArm { get; }
        public double? TensionLeverArm { get; }
        /// <summary>FC per side: min(Fc,pl,Rd, Fc,fc,Rd), N.</summary>
        public double? SideCompressionResistance { get; }
        /// <summary>FT per side (anchors outside the flanges): base plate and anchors, N.</summary>
        public double? SideTensionResistance { get; }
        public bool? PryingDevelops { get; }
        /// <summary>Mj,Rd of Table6.7 at the actual eccentricity M1/N, Nmm (signed like M1).</summary>
        public double? MomentResistance { get; }
        /// <summary>N at the resistance on the same N-M1 ray, N (signed like N).</summary>
        public double? AxialResistance { get; }
        /// <summary>Fv,Rd of EN1993-1-8 (6.3) with the credited anchors, N.</summary>
        public double? ShearResistance { get; }
        public FilletWeldGroupResult Welds { get; }
        /// <summary>Welds under N and M1 scaled to the joint resistance (EN1993-1-8 6.2.3(4)).</summary>
        public FilletWeldGroupResult WeldsAtJointResistance { get; }
        public string Scope => "Static ULS resistance of one I/H column base: unstiffened rectangular plate welded concentrically, grout and concrete, "
            + "four anchors outside the flanges (tension T-stub) or anchors between the flanges (no anchor tension). N, M1 (major axis), V1, V2. "
            + "EN1993-1-8:2005 6.2.2(5)-(8), 6.2.5, 6.2.6.7, 6.2.6.9-6.2.6.12, 6.2.8 and Table6.7; EN1992-1-1 6.7 for FRdu. "
            + "Concrete failure of the anchors is a declared input. No minor-axis/biaxial bending, stiffened or embedded bases, hollow columns, seismic capacity design or fatigue.";
        public IReadOnlyList<string> ExternalConditions { get; } = Array.AsReadOnly(new[] {
            "Concrete failure modes of the anchors (EN1992-4: pull-out, cone, splitting, edge, pry-out) and the foundation design, including the splitting reinforcement of EN1992-1-1 6.7(1), are declared by the caller and not computed.",
            "The column cross-section and member at the base cut (N, M1, V) are verified separately; this result is a local joint resistance.",
            "Execution: plate flatness and grout filling as assumed in the uniform bearing pressure, grout strength and thickness as declared, anchor positions within tolerances (EN1993-1-8 6.2.6.12(2) note).",
            "The joint stiffness (EN1993-1-8 6.3.4) and its compatibility with the global model are not part of this result." });
        internal ColumnBaseResult(ColumnBaseInput input, IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<string> missing, ColumnBaseEquilibriumState state, double? fjd, double? c, double? zc, double? zt,
            double? fc, double? ft, bool? prying, double? mj, double? nj, double? fv, FilletWeldGroupResult welds, FilletWeldGroupResult weldsAtResistance)
        {
            Input = input; MissingRequirements = Array.AsReadOnly(missing.Distinct().ToArray());
            Checks = Array.AsReadOnly(ConnectionOutcome.Retained(checks, MissingRequirements.Count));
            Trace = Array.AsReadOnly(trace.ToArray()); State = state;
            BearingStrength = fjd; AdditionalBearingWidth = c; CompressionLeverArm = zc; TensionLeverArm = zt; PryingDevelops = prying;
            Welds = welds; WeldsAtJointResistance = weldsAtResistance;
            // Resistances are exposed only for a calculation without missing requirements; the trace keeps the evaluated values.
            bool evaluated = MissingRequirements.Count == 0;
            SideCompressionResistance = evaluated ? fc : null; SideTensionResistance = evaluated ? ft : null;
            MomentResistance = evaluated ? mj : null; AxialResistance = evaluated ? nj : null; ShearResistance = evaluated ? fv : null;
        }
    }

    /// <summary>Opt-in standard column-base workflow. It composes the existing extended-row T-stub
    /// geometry and mechanism arithmetic, bolt tension/punching formulas and the elastic fillet-weld group
    /// with the base-specific EN1993-1-8 components; nothing in the reused components changes.</summary>
    public static class ColumnBase
    {
        public const string MethodId = "Steel.Connection.ColumnBase.Standard";
        private const double BetaJ = 2.0 / 3.0;

        public static ColumnBaseResult Verify(ColumnBaseInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var code = p.Code; var col = p.Column; var plate = p.Plate; var a = p.Anchor; var pattern = p.Pattern;
            var f = p.Foundation; var w = p.Welds; var act = p.Actions; var concrete = p.AnchorConcrete;
            var missing = new List<string>(); var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var state = ColumnBaseEquilibriumState.NotEvaluated;
            double? fjdOut = null, cOut = null, zcOut = null, ztOut = null, fcOut = null, ftOut = null, mjOut = null, njOut = null, fvOut = null;
            bool? pryingOut = null; FilletWeldGroupResult welds = null, weldsAtResistance = null;
            string ec = code.IsNtc ? "EN1993-1-8:2005 (supplementary EC component model with NTC2018 factors, NTC2018 Cap.12)" : "EN1993-1-8:2005";
            bool outside = pattern.Layout == ColumnBaseAnchorLayout.OutsideFlanges;

            // Field of the method: every other typology or design situation is refused by name.
            if (p.Detail == ColumnBaseDetail.StiffenedPlate) missing.Add("STIFFENED_BASE_PLATE_OUT_OF_SCOPE");
            if (p.Detail == ColumnBaseDetail.EmbeddedColumn) missing.Add("EMBEDDED_COLUMN_BASE_OUT_OF_SCOPE");
            if (p.Detail == ColumnBaseDetail.HollowSectionColumn) missing.Add("HOLLOW_SECTION_COLUMN_OUT_OF_SCOPE");
            if (p.Situation == ColumnBaseDesignSituation.SeismicCapacityDesign) missing.Add("SEISMIC_CAPACITY_DESIGN_OUT_OF_SCOPE");
            if (p.Situation == ColumnBaseDesignSituation.Fatigue) missing.Add("FATIGUE_OUT_OF_SCOPE");
            if (act.M2 != 0) missing.Add("MINOR_AXIS_OR_BIAXIAL_BENDING_OUT_OF_SCOPE");
            if (act.T != 0) missing.Add("TORSION_OUT_OF_SCOPE");
            Required(p.ActionCombinationSource, "ACTION_COMBINATION_SOURCE_REQUIRED");
            Required(pattern.DetailSource, "ANCHOR_PATTERN_DETAIL_SOURCE_REQUIRED");
            Required(a.MaterialSource, "ANCHOR_MATERIAL_SOURCE_REQUIRED");
            Required(f.ConcentratedResistanceSource, "CONCENTRATED_RESISTANCE_AC1_SOURCE_REQUIRED");
            Required(p.ColumnMomentResistance.Source, "COLUMN_MOMENT_RESISTANCE_SOURCE_REQUIRED");
            if (!ReferenceEquals(p.ColumnMomentResistance.Code, code) || !ReferenceEquals(p.ColumnMomentResistance.Column, col)
                || !Equal(p.ColumnMomentResistance.ConcomitantShear, Math.Abs(act.V2)))
                missing.Add("COLUMN_MOMENT_RESISTANCE_MUST_USE_SAME_CODE_COLUMN_AND_CONCOMITANT_V2");
            // Materials and geometry of the field.
            if (col.Fy > 460 || plate.Fy > 460) missing.Add("CARBON_STEEL_UP_TO_S460_ONLY");
            if (col.Fy > (int)w.ColumnGrade || plate.Fy > (int)w.PlateGrade) missing.Add("YIELD_STRENGTH_EXCEEDS_DECLARED_NOMINAL_GRADE");
            // EN1993-1-8 6.2.6.7(1): above 600 mm the web contribution to Fc,fc,Rd is limited to 20%; not modelled.
            if (col.Height > 600) missing.Add("COLUMN_DEEPER_THAN_600MM_REQUIRES_WEB_CONTRIBUTION_LIMIT");
            if (plate.Width < col.Width || p.PlateLength < col.Height) missing.Add("PLATE_MUST_COVER_THE_COLUMN_FOOTPRINT");
            // EN1993-1-8 3.3(1): anchor yield strength not above 900 MPa (640 MPa when acting in shear, below).
            if (a.Fyb > 900) missing.Add("ANCHOR_YIELD_STRENGTH_ABOVE_900_MPA");
            if (a.Diameter < 12 || a.Diameter > 64) missing.Add("ANCHOR_DIAMETER_OUTSIDE_12_TO_64_MM");
            // EN1993-1-8 6.2.6.12(5): hooked anchors only up to fyb=300 MPa.
            if (a.Anchorage == ColumnBaseAnchorage.Hook && a.Fyb > 300) missing.Add("HOOKED_ANCHOR_ABOVE_FYB_300_MPA");
            double d0 = a.HoleDiameter, half = d0 / 2;
            double gauge = pattern.Gauge, row = pattern.RowDistance;
            double edgeX = (plate.Width - gauge) / 2, edgeY = p.PlateLength / 2 - row;
            bool twoRows = pattern.AnchorCount == 4;
            // EN1993-1-8 Table3.3 minima, applied in both directions because the shear may reverse.
            if (ConnectionValidation.BelowMinimumWithRoundoff(edgeX, 1.2 * d0) || ConnectionValidation.BelowMinimumWithRoundoff(edgeY, 1.2 * d0))
                missing.Add("ANCHOR_EDGE_DISTANCE_BELOW_1.2D0");
            if (ConnectionValidation.BelowMinimumWithRoundoff(gauge, 2.4 * d0) || twoRows && ConnectionValidation.BelowMinimumWithRoundoff(2 * row, 2.4 * d0))
                missing.Add("ANCHOR_SPACING_BELOW_2.4D0");
            if (outside)
            {
                if (!(row - col.Height / 2 - w.FlangeLeg > half)) missing.Add("ANCHOR_HOLE_INTERSECTS_FLANGE_WELD");
            }
            else
            {
                if (twoRows && !(col.Height / 2 - col.FlangeThickness - w.FlangeLeg - row > half)) missing.Add("ANCHOR_HOLE_INTERSECTS_INNER_FLANGE_WELD");
                if (!(gauge / 2 - col.WebThickness / 2 - w.WebLeg > half)) missing.Add("ANCHOR_HOLE_INTERSECTS_WEB_WELD");
            }
            // EN1993-1-8 6.2.5(7): beta_j=2/3 only under these grout conditions; no other value is supplied here.
            double minWidth = Math.Min(plate.Width, p.PlateLength);
            if (f.GroutFck < .2 * f.Fck || f.GroutThickness > .2 * minWidth || f.GroutThickness > 50 && f.GroutFck < f.Fck)
                missing.Add("GROUT_OUTSIDE_BETA_J_TWO_THIRDS_CONDITIONS");
            // EN1992-1-1 3.1.6(1)P and its note; NTC2018 4.1.2.1.1.1 fixes alpha_cc=0.85 and gamma_c=1.5 (1.4 for controlled production).
            if (code.IsNtc ? f.AlphaCC != .85 || f.GammaC != 1.5 && f.GammaC != 1.4
                : f.AlphaCC < .8 || f.AlphaCC > 1 || f.GammaC < 1)
                missing.Add(code.IsNtc ? "NTC_CONCRETE_FACTORS_FIXED_BY_4.1.2.1.1.1" : "EC2_CONCRETE_FACTORS_OUTSIDE_3.1.6");
            double plateArea = plate.Width * p.PlateLength;
            if (f.DesignDistributionArea < plateArea) missing.Add("DESIGN_DISTRIBUTION_AREA_BELOW_PLATE_FOOTPRINT");
            if (f.Grout == ColumnBaseGrout.OtherTested && (!f.FrictionCoefficient.HasValue || string.IsNullOrWhiteSpace(f.FrictionSource)))
                missing.Add("TESTED_GROUT_FRICTION_COEFFICIENT_AND_SOURCE_REQUIRED");
            if (missing.Count != 0) return Result();

            // Concrete in compression: EN1992-1-1 6.7(2) for the whole plate footprint, then EN1993-1-8 (6.6).
            // Taking Ac0 as the footprint instead of beff*leff (6.2.5(7)) is a lower bound: for a T-stub inside the
            // footprint the same concentric extensions remain admissible, so its Ac1/Ac0 is not smaller, and FC grows with fjd.
            double fcd = f.AlphaCC * f.Fck / f.GammaC;
            double ratio = Math.Min(Math.Sqrt(f.DesignDistributionArea / plateArea), 3);
            double frdu = plateArea * fcd * ratio;
            double fjd = BetaJ * frdu / plateArea;
            double gM0 = code.Plates.GammaM0, gM2 = code.GammaM2Connection;
            double c = plate.Thickness * Math.Sqrt(plate.Fy / (3 * fjd * gM0));
            Value("Concrete.Fcd", fcd, "MPa"); Value("Concrete.PlateFootprint", plateArea, "mm2");
            Value("Concrete.Ac1", f.DesignDistributionArea, "mm2"); Value("Concrete.SqrtAc1OverAc0Capped3", ratio, "1");
            Value("Concrete.FRduOfFootprint", frdu, "N"); Value("Concrete.BetaJ", BetaJ, "1"); Value("Concrete.Fjd", fjd, "MPa");
            Value("Plate.AdditionalBearingWidthC", c, "mm");
            fjdOut = fjd; cOut = c;

            // Compression T-stub under each flange (6.2.5, 6.2.6.9-10): flange plus c on every side, limited by
            // the plate edges and, inside, by half the clear depth so that the flange T-stubs never overlap.
            double hc = col.Height, tf = col.FlangeThickness;
            double outward = Math.Min(c, (p.PlateLength - hc) / 2), inward = Math.Min(c, (hc - 2 * tf) / 2);
            double halfLength = Math.Min(col.Width / 2 + c, plate.Width / 2);
            var flangeStub = Bearing(halfLength, hc / 2 - tf - inward, hc / 2 + outward);
            Value("Compression.FlangeTStub.Length", 2 * halfLength, "mm"); Value("Compression.FlangeTStub.Width", tf + inward + outward, "mm");
            Value("Compression.FlangeTStub.DeductedHoles", flangeStub.Holes, "1"); Value("Compression.FlangeTStub.EffectiveArea", flangeStub.Area, "mm2");
            Value("Compression.FlangeTStub.Centroid", flangeStub.Centroid, "mm");
            double fcpl = fjd * flangeStub.Area;
            double fcfc = p.ColumnMomentResistance.Value / (hc - tf);
            double fc = Math.Min(fcpl, fcfc), zc = flangeStub.Centroid;
            Value("Compression.FcPlRd", fcpl, "N"); Value("Compression.FcFcRd", fcfc, "N"); Value("Compression.SideResistance", fc, "N");
            Value("Compression.LeverArm", zc, "mm");
            fcOut = fc; zcOut = zc;
            string compressionRef = ec + " 6.2.5 (6.4)-(6.6), 6.2.6.9, 6.2.6.10; EN1992-1-1:2004 6.7(2) (6.63); holes deducted (GPC, conservative)";
            string flangeRef = ec + " 6.2.6.7 (6.21); " + p.ColumnMomentResistance.Source;

            // Tension side: anchors outside the flanges form the extended-row T-stub of 6.2.6.11 / 6.2.6.5.
            double ft = 0, zt = 0, plateMode = 0, mode3 = 0; double? mode2 = null; bool prying = false;
            if (outside)
            {
                zt = row; ztOut = zt;
                var geometry = TStubEffectiveGeometry.ExtendedEndPlateOuterRow(new TStubExtendedEndPlateGeometry(
                    plate.Width, gauge, row - hc / 2, w.FlangeLeg, edgeY, pattern.DetailSource));
                foreach (var v in geometry.Trace) Value("Tension.TStub." + v.Name, v.Value, v.Unit);
                foreach (var d in geometry.Diagnostics) missing.Add("Tension.TStub:" + d);
                if (missing.Count != 0) return Result();
                double m = geometry.M, emin = geometry.Emin, t = plate.Thickness;
                if (!(m > half)) missing.Add("ANCHOR_HOLE_INTERSECTS_PLASTIC_HINGE_LINE");
                var lengths = geometry.IndividualRows[0];
                double n = Math.Min(emin, 1.25 * m);
                double mpl1 = .25 * lengths.Mode1 * t * t * plate.Fy / gM0, mpl2 = .25 * lengths.Mode2 * t * t * plate.Fy / gM0;
                // Per anchor: EN1993-1-8 Table3.4 / NTC2018 (4.2.68), x0.85 for cut threads not to EN1090 (3.6.1(3));
                // plate punching under the nut; declared concrete resistance (6.2.6.12(3)).
                double steel = BoltResistance.Tension(a.Fub, a.TensileArea, gM2) * (a.ThreadsComplyWithEn1090 ? 1 : .85);
                var punching = BoltPunching.Verify(new BoltPunchingInput(code, plate, 0, d0, a.MeanNutDiameter));
                foreach (var d in punching.Diagnostics) missing.Add("Tension.Punching:" + d);
                double? concreteTension = concrete.TensionPerAnchor;
                double lb = 8 * a.Diameter + f.GroutThickness + t + a.WasherThickness + .5 * a.NutHeight;
                double lbStar = 8.8 * m * m * m * a.TensileArea * 1 / (lengths.Mode1 * t * t * t);
                prying = lb <= lbStar; pryingOut = prying;
                Value("Tension.TStub.N", n, "mm"); Value("Tension.TStub.Mpl1", mpl1, "Nmm"); Value("Tension.TStub.Mpl2", mpl2, "Nmm");
                Value("Tension.Anchor.SteelTensionResistance", steel, "N");
                Value("Tension.Anchor.AnchorElongationLength", lb, "mm"); Value("Tension.Anchor.PryingLengthLimit", lbStar, "mm");
                Value("Tension.TStub.PryingDevelops", prying ? 1 : 0, "1"); Value("Tension.LeverArm", zt, "mm");
                if (missing.Count != 0) return Result();
                double punchingResistance = punching.Checks.Single().Resistance;
                Value("Tension.Anchor.PlatePunchingResistance", punchingResistance, "N");
                if (concreteTension.HasValue)
                {
                    if (string.IsNullOrWhiteSpace(concrete.Source)) missing.Add("ANCHOR_CONCRETE_RESISTANCE_SOURCE_REQUIRED");
                    double anchor = Math.Min(steel, Math.Min(punchingResistance, concreteTension.Value));
                    double sumFt = 2 * anchor;
                    // EN1993-1-8 6.2.6.11(2): prying is not relied upon for the plate, so the plate keeps the no-prying
                    // mechanism 2Mpl,1/m; with prying (Lb<=Lb*) the anchors are also checked in Mode 2 (Table6.2).
                    var noPrying = TStubMechanismArithmetic.Calculate(mpl1, mpl2, m, n, 0, sumFt, false, TStubMode1Method.PointForce);
                    plateMode = noPrying.PlateMode1; mode3 = noPrying.BoltsMode3;
                    if (prying) mode2 = TStubMechanismArithmetic.Calculate(mpl1, mpl2, m, n, 0, sumFt, true, TStubMode1Method.PointForce).CombinedMode2;
                    ft = Math.Min(plateMode, Math.Min(mode3, mode2 ?? double.PositiveInfinity));
                    Value("Tension.Anchor.ConcreteTensionResistance", concreteTension.Value, "N"); Value("Tension.Anchor.DesignTensionResistance", anchor, "N");
                    Value("Tension.TStub.PlateWithoutPrying", plateMode, "N"); if (mode2.HasValue) Value("Tension.TStub.Mode2", mode2.Value, "N");
                    Value("Tension.TStub.Mode3", mode3, "N"); Value("Tension.SideResistance", ft, "N");
                    ftOut = ft;
                }
            }
            if (missing.Count != 0) return Result();

            // N-M1 resistance: Table6.7 is the equilibrium of one force per side at its own lever arm,
            // FA at y=+zA and FB at y=-zB (tension positive): FA=(N zB+M)/(zA+zB), FB=(N zA-M)/(zA+zB).
            double nEd = act.N, mEd = act.M1;
            double u = 0;
            if (mEd == 0 && nEd < 0)
            {
                // 6.2.8.2: three non-overlapping T-stubs, the web one between the inner edges of the flange ones.
                state = ColumnBaseEquilibriumState.AxialCompressionOnly;
                double webHalfLength = hc / 2 - tf - inward;
                double webAreaResistance = 0;
                if (webHalfLength > 0)
                {
                    var webStub = Bearing(Math.Min(col.WebThickness / 2 + c, plate.Width / 2), -webHalfLength, webHalfLength);
                    webAreaResistance = fjd * webStub.Area;
                    Value("Compression.WebTStub.EffectiveArea", webStub.Area, "mm2"); Value("Compression.WebTStub.DeductedHoles", webStub.Holes, "1");
                }
                double nj = 2 * fcpl + webAreaResistance;
                Value("Axial.FcPlRdWebTStub", webAreaResistance, "N"); Value("Axial.NjRd", nj, "N");
                checks.Add(new ConnectionCheck("Axial.ThreeTStubs", -nEd, nj, ec + " 6.2.8.2, Figure6.19; " + compressionRef));
                u = -nEd / nj; njOut = -nj;
            }
            else if (nEd != 0 || mEd != 0)
            {
                var states = new List<Tuple<ColumnBaseEquilibriumState, bool, bool>>();
                if (outside)
                {
                    states.Add(Tuple.Create(ColumnBaseEquilibriumState.SideATensionSideBCompression, true, false));
                    states.Add(Tuple.Create(ColumnBaseEquilibriumState.BothSidesInTension, true, true));
                    states.Add(Tuple.Create(ColumnBaseEquilibriumState.SideACompressionSideBTension, false, true));
                }
                states.Add(Tuple.Create(ColumnBaseEquilibriumState.BothSidesInCompression, false, false));
                double best = double.NegativeInfinity, fa = 0, fb = 0; bool aTension = false, bTension = false;
                foreach (var s in states)
                {
                    double za = s.Item2 ? zt : zc, zb = s.Item3 ? zt : zc;
                    double forceA = (nEd * zb + mEd) / (za + zb), forceB = (nEd * za - mEd) / (za + zb);
                    // Signed consistency: tension sides need F>=0, compression sides F<=0. In exact arithmetic one
                    // state (two at a shared boundary, with equal forces) has a non-negative minimum.
                    double consistency = Math.Min(s.Item2 ? forceA : -forceA, s.Item3 ? forceB : -forceB);
                    if (consistency > best) { best = consistency; state = s.Item1; fa = forceA; fb = forceB; aTension = s.Item2; bTension = s.Item3; }
                }
                // Without tension capacity a state with anchor tension cannot be a resistance mechanism.
                if (!outside && best < -1e-12 * (Math.Abs(nEd) + Math.Abs(mEd) / zc))
                {
                    missing.Add("ANCHOR_TENSION_BETWEEN_FLANGES_NOT_IMPLEMENTED");
                    return Result();
                }
                if ((aTension || bTension) && !concrete.TensionPerAnchor.HasValue)
                {
                    missing.Add("ANCHOR_CONCRETE_TENSION_RESISTANCE_REQUIRED");
                    return Result();
                }
                double z = (aTension ? zt : zc) + (bTension ? zt : zc);
                Value("Equilibrium.LeverArmZ", z, "mm"); Value("Equilibrium.SideAForce", fa, "N"); Value("Equilibrium.SideBForce", fb, "N");
                Side("SideA", fa, aTension); Side("SideB", fb, bTension);
                u = Math.Max(Math.Abs(fa) / (aTension ? ft : fc), Math.Abs(fb) / (bTension ? ft : fc));
                if (u > 0) { mjOut = mEd / u; njOut = nEd / u; }
                if (mjOut.HasValue) Value("Equilibrium.MjRdAtActualEccentricity", Math.Abs(mjOut.Value), "Nmm");
                if (njOut.HasValue) Value("Equilibrium.NjRdAtActualEccentricity", Math.Abs(njOut.Value), "N");
            }
            else state = ColumnBaseEquilibriumState.NoAxialForceOrMoment;
            Value("Equilibrium.State", (int)state, "1");
            if (missing.Count != 0) return Result();

            // Shear (6.2.2(5)-(8), 6.2.8.1(5)): friction plus the anchors, only with holes that are not oversized.
            double vEd = Math.Sqrt(act.V1 * act.V1 + act.V2 * act.V2);
            double cfd = f.Grout == ColumnBaseGrout.SandCementMortar ? .2 : f.FrictionCoefficient.Value;
            double ff = cfd * Math.Max(0, -nEd);
            Value("Shear.Demand", vEd, "N"); Value("Shear.Cfd", cfd, "1"); Value("Shear.FfRd", ff, "N");
            var reasons = new List<string>();
            double maxClearance = a.Diameter <= 14 ? 1 : a.Diameter <= 24 ? 2 : 3;
            if (a.HoleDiameter - a.Diameter > maxClearance + 1e-10) reasons.Add("OVERSIZED_HOLES");
            if (code.IsNtc && a.HoleDiameter - a.Diameter > (a.Diameter <= 20 ? 1 : 1.5) + 1e-10
                && string.IsNullOrWhiteSpace(a.HoleClearanceServiceabilitySource)) reasons.Add("NTC_HOLE_CLEARANCE_REQUIRES_SERVICEABILITY_EVIDENCE");
            if (a.Fyb < 235 || a.Fyb > 640) reasons.Add("ANCHOR_FYB_OUTSIDE_235_TO_640_MPA");
            if (!concrete.ShearPerAnchor.HasValue || string.IsNullOrWhiteSpace(concrete.Source)) reasons.Add("ANCHOR_CONCRETE_SHEAR_RESISTANCE_NOT_DECLARED");
            int total = pattern.AnchorCount;
            // GPC choice: an anchor carrying tension in this state is not credited in shear (no tension-shear interaction model).
            int free = state == ColumnBaseEquilibriumState.BothSidesInTension ? 0
                : state == ColumnBaseEquilibriumState.SideATensionSideBCompression || state == ColumnBaseEquilibriumState.SideACompressionSideBTension ? total / 2 : total;
            int credited = reasons.Count == 0 ? free : 0;
            double fvb = 0;
            if (reasons.Count == 0)
            {
                double factor = a.ThreadsComplyWithEn1090 ? 1 : .85;
                // F1: Table3.4 shear with alpha_v=0.5, the lower tabulated value; F2 (6.2) with alpha_bc<0.37 always governs it.
                double f1 = BoltResistance.Shear(1, .5, a.Fub, a.TensileArea, gM2) * factor;
                double alphaBc = .44 - .0003 * a.Fyb;
                double f2 = alphaBc * a.Fub * a.TensileArea / gM2;
                // Plate bearing, Table3.4 note: verified separately for the X and Y components; the smaller value is applied to the resultant.
                double bx = AnchorBearing(edgeX, gauge, edgeY, twoRows ? 2 * row : (double?)null);
                double by = AnchorBearing(edgeY, twoRows ? 2 * row : (double?)null, edgeX, gauge);
                fvb = Math.Min(Math.Min(f1, f2), Math.Min(Math.Min(bx, by), concrete.ShearPerAnchor.Value));
                Value("Shear.Anchor.F1vbRd", f1, "N"); Value("Shear.Anchor.AlphaBc", alphaBc, "1"); Value("Shear.Anchor.F2vbRd", f2, "N");
                Value("Shear.Anchor.PlateBearingX", bx, "N"); Value("Shear.Anchor.PlateBearingY", by, "N");
                Value("Shear.Anchor.ConcreteShearResistance", concrete.ShearPerAnchor.Value, "N"); Value("Shear.Anchor.FvbRd", fvb, "N");
            }
            double fv = ff + credited * fvb;
            Value("Shear.CreditedAnchors", credited, "1"); Value("Shear.FvRd", fv, "N");
            fvOut = fv;
            string shearRef = ec + " 6.2.2(5)-(8) (6.1)-(6.3), 6.2.8.1(5); Table3.4" + (code.IsNtc ? "; NTC2018 (4.2.67)" : "");
            if (vEd <= fv || reasons.Count == 0 && credited == total) checks.Add(new ConnectionCheck("Shear.FrictionAndAnchors", vEd, fv, shearRef));
            else if (reasons.Count != 0) missing.Add("SHEAR_EXCEEDS_FRICTION:ANCHOR_SHEAR_NOT_ADMISSIBLE(" + string.Join(",", reasons) + "):SHEAR_KEY_OUT_OF_SCOPE");
            else missing.Add("SHEAR_EXCEEDS_RESISTANCE_WITHOUT_TENSIONED_ANCHORS:TENSION_SHEAR_INTERACTION_NOT_IMPLEMENTED");

            // Welds all round the column with the module's elastic line-weld group (EN1993-1-8 4.5.3 / NTC 4.2.8.2.4):
            // every action passes through the welds; no direct bearing of the column end is credited.
            welds = WeldGroup(nEd, mEd, act.V1, act.V2);
            IncludeWelds("Welds", welds);
            if (welds.IsComplete || welds.Status == ConnectionStatus.Failed)
                checks.Add(new ConnectionCheck("Welds.ActualActions", welds.Points.Max(x => x.Verification.Utilization ?? 0), 1,
                    (code.IsNtc ? "NTC2018 4.2.8.2.4" : "EN1993-1-8:2005 4.5.3") + "; elastic line-weld group, governing root point", "1"));
            // 6.2.3(4): the welds must not limit Mj,Rd. N and M1 scaled to the resistance on the same ray, same shears.
            if (mEd != 0 && u > 0 && welds.IsComplete)
            {
                weldsAtResistance = WeldGroup(nEd / u, mEd / u, act.V1, act.V2);
                IncludeWelds("WeldsAtJointResistance", weldsAtResistance);
                if (weldsAtResistance.IsComplete || weldsAtResistance.Status == ConnectionStatus.Failed)
                    checks.Add(new ConnectionCheck("Welds.NotLimitingJointMomentResistance", weldsAtResistance.Points.Max(x => x.Verification.Utilization ?? 0), 1,
                        ec + " 6.2.3(4); welds at N/u, M1/u with the actual shears", "1", ConnectionCheckRole.Detailing));
            }
            return Result();

            void Side(string name, double signedForce, bool tension)
            {
                // The selected state is consistent up to round-off at a shared boundary: a force of the other sign is zero.
                double force = Math.Max(0, tension ? signedForce : -signedForce);
                if (tension)
                {
                    string tref = ec + " 6.2.6.11(1)-(2), 6.2.6.12, 6.2.4 Table6.2, Table6.6 outer row; 6.2.8.3(2)";
                    checks.Add(new ConnectionCheck(name + ".TStub.PlateWithoutPrying", force, plateMode, tref));
                    if (mode2.HasValue) checks.Add(new ConnectionCheck(name + ".TStub.Mode2", force, mode2.Value, tref + "; prying develops, Lb<=Lb*"));
                    checks.Add(new ConnectionCheck(name + ".TStub.Mode3", force, mode3, tref + "; anchors min(steel, punching, declared concrete): " + concrete.Source));
                }
                else
                {
                    checks.Add(new ConnectionCheck(name + ".ConcreteAndPlate", force, fcpl, compressionRef + "; 6.2.8.3(4)-(5)"));
                    checks.Add(new ConnectionCheck(name + ".ColumnFlangeAndWeb", force, fcfc, flangeRef));
                }
            }
            double AnchorBearing(double end, double? pitchAlong, double edge, double? pitchAcross)
            {
                // Each anchor is an end bolt towards its own edge and an inner bolt towards its row neighbour.
                double alphaD = end / (3 * d0);
                if (pitchAlong.HasValue) alphaD = Math.Min(alphaD, pitchAlong.Value / (3 * d0) - .25);
                double alphaB = Math.Min(1, Math.Min(alphaD, a.Fub / plate.Fu));
                double k1 = Math.Min(2.5, 2.8 * edge / d0 - 1.7);
                if (!code.IsNtc && pitchAcross.HasValue) k1 = Math.Min(k1, 1.4 * pitchAcross.Value / d0 - 1.7);
                return k1 * alphaB * plate.Fu * a.Diameter * plate.Thickness / gM2;
            }
            ColumnBaseBearingArea Bearing(double halfX, double yLow, double yHigh)
            {
                // Rectangle |x|<=halfX, yLow<=y<=yHigh minus every anchor hole whose circle reaches inside it
                // (the whole hole is deducted at its centre: conservative for a hole cut by the boundary).
                double area = 2 * halfX * (yHigh - yLow), moment = area * (yLow + yHigh) / 2; int holes = 0;
                double holeArea = Math.PI * d0 * d0 / 4;
                foreach (var y in pattern.AnchorCount == 4 ? new[] { row, -row } : new[] { 0.0 })
                    foreach (var x in new[] { gauge / 2, -gauge / 2 })
                    {
                        double dx = Math.Max(0, Math.Abs(x) - halfX), dy = Math.Max(0, Math.Max(yLow - y, y - yHigh));
                        if (dx * dx + dy * dy < half * half) { area -= holeArea; moment -= holeArea * y; holes++; }
                    }
                return new ColumnBaseBearingArea(area, moment / area, holes);
            }
            FilletWeldGroupResult WeldGroup(double axial, double moment, double v1, double v2)
            {
                double h2 = hc / 2, inner = h2 - tf, b2 = col.Width / 2, tw2 = col.WebThickness / 2, rr = col.RootRadius;
                double af = w.FlangeLeg / Math.Sqrt(2), aw = w.WebLeg / Math.Sqrt(2), dw = inner - rr;
                // Root lines on the plate; each segment runs so that its in-plane normal p=(-t.Y,t.X) points into the member face.
                var segments = new[] {
                    Seg(b2, h2, -b2, h2, af, w.FlangeEnds), Seg(tw2 + rr, inner, b2, inner, af, w.FlangeEnds), Seg(-b2, inner, -tw2 - rr, inner, af, w.FlangeEnds),
                    Seg(-b2, -h2, b2, -h2, af, w.FlangeEnds), Seg(b2, -inner, tw2 + rr, -inner, af, w.FlangeEnds), Seg(-tw2 - rr, -inner, -b2, -inner, af, w.FlangeEnds),
                    Seg(tw2, -dw, tw2, dw, aw, w.WebEnds), Seg(-tw2, dw, -tw2, -dw, aw, w.WebEnds) };
                var columnMaterial = new FilletWeldMaterial(w.ColumnGrade, Math.Min(col.Fu, p.ColumnFlangeFu), Math.Min(col.WebThickness, tf));
                var plateMaterial = new FilletWeldMaterial(w.PlateGrade, plate.Fu, plate.Thickness);
                return FilletWeldGroup.Verify(new FilletWeldGroupInput(code, segments, columnMaterial, plateMaterial,
                    new PlanarConnectionActions(v1, v2, axial, moment, 0, 0), FilletWeldMethod.Directional,
                    w.ConsumableCompatibilitySource, !string.IsNullOrWhiteSpace(w.LocalEccentricitySource), w.LocalEccentricitySource));
            }
            FilletWeldSegment Seg(double x1, double y1, double x2, double y2, double throat, FilletWeldEndCondition ends)
                => new FilletWeldSegment(new ConnectionPoint2D(x1, y1), new ConnectionPoint2D(x2, y2), throat, ends);
            void IncludeWelds(string name, FilletWeldGroupResult r)
            {
                foreach (var d in r.Diagnostics) missing.Add(name + ":" + d);
                if (!r.IsComplete && r.Status != ConnectionStatus.Failed && r.Diagnostics.Count == 0) missing.Add(name + ":INCOMPLETE_WELD_GROUP");
                if (r.Utilization.HasValue) Value(name + ".Utilization", r.Utilization.Value, "1");
            }
            void Required(string source, string issue) { if (string.IsNullOrWhiteSpace(source)) missing.Add(issue); }
            void Value(string name, double value, string unit)
            { if (double.IsNaN(value) || double.IsInfinity(value)) missing.Add(name + "_OUTSIDE_NUMERIC_RANGE"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            ColumnBaseResult Result() => new ColumnBaseResult(p, checks, trace, missing, state, fjdOut, cOut, zcOut, ztOut,
                fcOut, ftOut, pryingOut, mjOut, njOut, fvOut, welds, weldsAtResistance);
        }
        private static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    public sealed partial class StandardConnectionCaseReport
    {
        public static StandardConnectionCaseReport Create(ColumnBaseResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            var p = r.Input; var code = p.Code; var a = p.Actions; var col = p.Column; var plate = p.Plate; var f = p.Foundation;
            var input = Factors(code); input.AddRange(new[] { V("N", a.N, "N"), V("V1", a.V1, "N"), V("V2", a.V2, "N"), V("M1", a.M1, "Nmm"),
                V("Column.Height", col.Height, "mm"), V("Column.Width", col.Width, "mm"), V("Column.WebThickness", col.WebThickness, "mm"),
                V("Column.FlangeThickness", col.FlangeThickness, "mm"), V("Plate.Width", plate.Width, "mm"), V("Plate.Length", p.PlateLength, "mm"),
                V("Plate.Thickness", plate.Thickness, "mm"), V("Plate.Fy", plate.Fy, "MPa"), V("Anchor.Diameter", p.Anchor.Diameter, "mm"),
                V("Anchor.HoleDiameter", p.Anchor.HoleDiameter, "mm"), V("Anchor.Count", p.Pattern.AnchorCount, "1"), V("Anchor.Gauge", p.Pattern.Gauge, "mm"),
                V("Anchor.RowDistance", p.Pattern.RowDistance, "mm"), V("Concrete.Fck", f.Fck, "MPa"), V("Concrete.Ac1", f.DesignDistributionArea, "mm2"),
                V("Grout.Thickness", f.GroutThickness, "mm") });
            var values = new List<SectionResistanceValue>();
            Add("Joint.BearingStrength", r.BearingStrength, "MPa"); Add("Joint.AdditionalBearingWidth", r.AdditionalBearingWidth, "mm");
            Add("Joint.CompressionLeverArm", r.CompressionLeverArm, "mm"); Add("Joint.TensionLeverArm", r.TensionLeverArm, "mm");
            Add("Joint.SideCompressionResistance", r.SideCompressionResistance, "N"); Add("Joint.SideTensionResistance", r.SideTensionResistance, "N");
            Add("Joint.MomentResistanceAtActualEccentricity", r.MomentResistance, "Nmm"); Add("Joint.AxialResistanceAtActualEccentricity", r.AxialResistance, "N");
            Add("Joint.ShearResistance", r.ShearResistance, "N");
            return new StandardConnectionCaseReport(ColumnBase.MethodId, code, r.Scope, r.Status, r.IsStaticResistanceComplete, input, values,
                r.Checks, r.MissingRequirements, r.ExternalConditions,
                new[] { p.ActionCombinationSource, p.Pattern.DetailSource, p.Anchor.MaterialSource, f.ConcentratedResistanceSource,
                    p.AnchorConcrete.Source, p.ColumnMomentResistance.Source, "COLUMN_BASE_USAGE.txt" }.Where(s => !string.IsNullOrWhiteSpace(s)));
            void Add(string id, double? value, string unit) { if (value.HasValue) values.Add(V(id, value.Value, unit)); }
        }
    }

    internal sealed class ColumnBaseBearingArea
    {
        internal double Area { get; }
        internal double Centroid { get; }
        internal int Holes { get; }
        internal ColumnBaseBearingArea(double area, double centroid, int holes) { Area = area; Centroid = centroid; Holes = holes; }
    }
}
