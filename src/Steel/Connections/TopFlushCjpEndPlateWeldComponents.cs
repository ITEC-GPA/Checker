using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual complete-penetration seam through a beam flange, with an additional
    /// real inner equal-leg fillet. Dimensions mm; all-weld minimum strengths MPa.
    /// The inner fillet defines the existing T-stub toe geometry but adds no credited
    /// weld resistance. InnerToeGeometrySource includes continuous full-size starts,
    /// ends and root transitions. This contract does not convert partial penetration into CJP.</summary>
    public sealed class EndPlateCjpFlangeDetail
    {
        public double FusionDepth { get; }
        public double EffectiveSeamWidth { get; }
        public double InnerFilletLeg { get; }
        public double OuterFilletLeg { get; }
        public double AllWeldMinimumFy { get; }
        public double AllWeldMinimumFu { get; }
        public bool FullPenetrationQualified { get; }
        public string PenetrationQualificationSource { get; }
        public string PreparationAndInspectionSource { get; }
        public string InnerToeGeometrySource { get; }
        public string ThroughThicknessBehaviourSource { get; }
        public EndPlateCjpFlangeDetail(double fusionDepth, double effectiveSeamWidth, double innerFilletLeg,
            double outerFilletLeg, double allWeldMinimumFy, double allWeldMinimumFu, bool fullPenetrationQualified,
            string penetrationQualificationSource, string preparationAndInspectionSource,
            string innerToeGeometrySource, string throughThicknessBehaviourSource)
        {
            foreach (var value in new[] { fusionDepth, effectiveSeamWidth, innerFilletLeg, allWeldMinimumFy, allWeldMinimumFu })
                ConnectionValidation.Positive(value, nameof(value));
            ConnectionValidation.Nonnegative(outerFilletLeg, nameof(outerFilletLeg));
            if (allWeldMinimumFu < allWeldMinimumFy) throw new ArgumentException("All-weld fu must not be below fy.");
            FusionDepth = fusionDepth; EffectiveSeamWidth = effectiveSeamWidth;
            InnerFilletLeg = innerFilletLeg; OuterFilletLeg = outerFilletLeg;
            AllWeldMinimumFy = allWeldMinimumFy; AllWeldMinimumFu = allWeldMinimumFu;
            FullPenetrationQualified = fullPenetrationQualified;
            PenetrationQualificationSource = penetrationQualificationSource; PreparationAndInspectionSource = preparationAndInspectionSource;
            InnerToeGeometrySource = innerToeGeometrySource; ThroughThicknessBehaviourSource = throughThicknessBehaviourSource;
        }
    }

    /// <summary>End plate flush at the TOP flange only, with a real lower projection.
    /// All geometry and concomitant M/V/N remain those of MomentComponents. PlateHeight
    /// and TopProjection are explicit measured geometry, not inferred from the absence
    /// of an external bolt row. Fy/Fu are actual-product/thickness values in MPa.</summary>
    public sealed class TopFlushCjpEndPlateWeldInput
    {
        public EndPlateMomentComponentResult MomentComponents { get; }
        public EndPlateCjpFlangeDetail Cjp { get; }
        public double PlateHeight { get; }
        public double TopProjection { get; }
        public FilletWeldSteelGrade BeamGrade { get; }
        public FilletWeldSteelGrade EndPlateGrade { get; }
        public double BeamFlangeFy { get; }
        public double BeamFlangeFu { get; }
        public string MaterialSource { get; }
        public FilletWeldEndCondition WebEnds { get; }
        public FilletWeldEndCondition CompressionFlangeEnds { get; }
        public EndPlateCompressionTransfer CompressionTransfer { get; }
        public string FilletDetailSource { get; }
        public string NormalTransferSource { get; }
        public string ShearTransferSource { get; }
        public string ConsumableCompatibilitySource { get; }
        public string LocalEccentricitySource { get; }
        public string CompressionBearingSource { get; }
        public TopFlushCjpEndPlateWeldInput(EndPlateMomentComponentResult momentComponents, EndPlateCjpFlangeDetail cjp,
            double plateHeight, double topProjection, FilletWeldSteelGrade beamGrade, FilletWeldSteelGrade endPlateGrade,
            double beamFlangeFy, double beamFlangeFu, string materialSource,
            FilletWeldEndCondition webEnds, FilletWeldEndCondition compressionFlangeEnds,
            EndPlateCompressionTransfer compressionTransfer, string filletDetailSource, string normalTransferSource,
            string shearTransferSource, string consumableCompatibilitySource, string localEccentricitySource,
            string compressionBearingSource = null)
        {
            MomentComponents = momentComponents ?? throw new ArgumentNullException(nameof(momentComponents));
            Cjp = cjp ?? throw new ArgumentNullException(nameof(cjp));
            ConnectionValidation.Positive(plateHeight, nameof(plateHeight)); ConnectionValidation.Nonnegative(topProjection, nameof(topProjection));
            ConnectionValidation.Positive(beamFlangeFy, nameof(beamFlangeFy)); ConnectionValidation.Positive(beamFlangeFu, nameof(beamFlangeFu));
            if (beamFlangeFu < beamFlangeFy) throw new ArgumentException("Flange fu must not be below fy.");
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), beamGrade) || !Enum.IsDefined(typeof(FilletWeldSteelGrade), endPlateGrade))
                throw new ArgumentOutOfRangeException(nameof(beamGrade));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), webEnds) || !Enum.IsDefined(typeof(FilletWeldEndCondition), compressionFlangeEnds))
                throw new ArgumentOutOfRangeException(nameof(webEnds));
            if (!Enum.IsDefined(typeof(EndPlateCompressionTransfer), compressionTransfer)) throw new ArgumentOutOfRangeException(nameof(compressionTransfer));
            PlateHeight = plateHeight; TopProjection = topProjection; BeamGrade = beamGrade; EndPlateGrade = endPlateGrade;
            BeamFlangeFy = beamFlangeFy; BeamFlangeFu = beamFlangeFu; MaterialSource = materialSource;
            WebEnds = webEnds; CompressionFlangeEnds = compressionFlangeEnds; CompressionTransfer = compressionTransfer;
            FilletDetailSource = filletDetailSource; NormalTransferSource = normalTransferSource; ShearTransferSource = shearTransferSource;
            ConsumableCompatibilitySource = consumableCompatibilitySource; LocalEccentricitySource = localEccentricitySource;
            CompressionBearingSource = compressionBearingSource;
        }
    }

    public sealed class TopFlushCjpEndPlateWeldResult
    {
        public ConnectionVerification<TopFlushCjpEndPlateWeldInput> WeldAssessment { get; }
        public EndPlateMomentComponentResult MomentComponents => WeldAssessment.Input.MomentComponents;
        public bool CanQualifyWholeJoint => false;
        public bool IsFullyFlushGeometry => false;
        public IReadOnlyList<string> PendingChecks { get; }
        internal TopFlushCjpEndPlateWeldResult(ConnectionVerification<TopFlushCjpEndPlateWeldInput> assessment)
        {
            WeldAssessment = assessment;
            PendingChecks = Array.AsReadOnly(new[] {
                "CJP equivalence relies on the stated penetration, preparation, matching consumable, inspection and through-thickness qualification; numerical prerequisites are not execution certification",
                "End-plate bending and local 3D force introduction remain distinct from the normal beam-flange seam section; no fictitious min(tf,tp) resistance is evaluated",
                "Real inner toe geometry and flange/web root transitions require the supplied qualification; no outer top-flange fillet exists",
                "Qualified compression bearing, if selected, remains an external contact/fit assessment",
                "Net/block failure, remaining bolt/support/member checks, full-joint resistance, rotational capacity and global model compatibility",
                "A plate flush at both upper and lower flanges needs a separately qualified weld-boundary/load-spread geometry" });
        }
    }

    /// <summary>Full-strength CJP equivalence of the upper flange plus local paired-fillet
    /// web/lower-flange checks. Top flush only, lower plate projection retained. No joint
    /// strength or rotation qualification is implied by the CJP equivalence.</summary>
    public static class TopFlushCjpEndPlateWeldComponents
    {
        public static TopFlushCjpEndPlateWeldResult Verify(TopFlushCjpEndPlateWeldInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var parent = p.MomentComponents.ComponentAssessment; var m = parent.Input;
            var beam = m.Beam; var plate = m.EndPlate; var g = m.InternalGeometry; var cjp = p.Cjp; var code = m.Policy.Code;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            void Require(bool condition, string reason) { if (!condition) issues.Add(reason); }
            void Source(string value, string reason) { Require(!string.IsNullOrWhiteSpace(value), reason); }
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + "OutsideNumericRange"); }
            Source(p.MaterialSource, "ActualThicknessMaterialSourceRequired");
            Source(cjp.PenetrationQualificationSource, "FullPenetrationQualificationRequired");
            Source(cjp.PreparationAndInspectionSource, "CjpPreparationAndInspectionQualificationRequired");
            Source(cjp.InnerToeGeometrySource, "ActualInnerToeGeometryQualificationRequired");
            Source(cjp.ThroughThicknessBehaviourSource, "ThroughThicknessBehaviourQualificationRequired");
            Source(p.FilletDetailSource, "RealWebAndLowerPairedFilletDetailRequired");
            Source(p.NormalTransferSource, "RolledRootAndTransitionNormalTransferQualificationRequired");
            Source(p.ShearTransferSource, "UniformWebOnlyShearTransferQualificationRequired");
            Source(p.ConsumableCompatibilitySource, "ConsumableCompatibilityQualificationRequired");
            Source(p.LocalEccentricitySource, "LocalEccentricityQualificationRequired");
            if (p.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing)
                Source(p.CompressionBearingSource, "CompressionBearingQualificationRequired");
            Require(parent.Status == ConnectionStatus.Passed, "PassedMomentComponentsRequiredForWeldEnvelope");
            Require(m.ExtensionGeometry == null, "TopFlushDetailRequiresNoExternalBoltRow");
            Require(p.TopProjection == 0, "ActualUpperPlateEdgeMustBeFlushWithFlangeExterior");
            Require(Equal(p.PlateHeight, beam.Height + m.CompressionEndPlateProjection + p.TopProjection), "PlateHeightDoesNotMatchParentBeamAndEdgePositions");
            Require(Equal(cjp.InnerFilletLeg, g.TensionFlangeWeldLeg), "ActualInnerToeDoesNotMatchParentTStubGeometry");
            Require(cjp.OuterFilletLeg == 0, "NoOuterTopFilletFitsAtFlushEdge");
            Require(cjp.FullPenetrationQualified, "PartialOrUnqualifiedPenetrationIsNotCjp");
            Require(!Below(cjp.FusionDepth, beam.FlangeThickness), "CjpDoesNotFuseWholeFlangeThickness");
            Require(!Below(cjp.EffectiveSeamWidth, beam.Width), "CjpDoesNotCoverWholeFlangeWidth");
            Require(!Below(plate.Width, beam.Width), "EndPlateDoesNotCoverBeamFlangeWidth");
            Require(!Below(plate.Thickness, beam.FlangeThickness), "PlateThinnerThanFlangeOutsideSimpleCjpEquivalence");
            Require(plate.Fy >= p.BeamFlangeFy && plate.Fu >= p.BeamFlangeFu, "WeakerPlateRequiresSeparateLocalResistanceQualification");
            Require(cjp.AllWeldMinimumFy >= Math.Max(p.BeamFlangeFy, plate.Fy), "AllWeldYieldStrengthBelowConnectedMaterials");
            Require(cjp.AllWeldMinimumFu >= Math.Max(p.BeamFlangeFu, plate.Fu), "AllWeldUltimateStrengthBelowConnectedMaterials");
            Require(p.BeamFlangeFy <= (int)p.BeamGrade && beam.Fy <= (int)p.BeamGrade && plate.Fy <= (int)p.EndPlateGrade,
                "YieldStrengthExceedsDeclaredNominalGrade");
            Require(Math.Min(plate.Thickness, Math.Min(beam.FlangeThickness, beam.WebThickness)) >= 4, "ParentThicknessBelowFourMillimetresScope");
            Require(m.CompressionEndPlateProjection >= g.CompressionFlangeWeldLeg, "LowerPairedFilletNeedsRealPlateProjection");
            Require(beam.Width <= 1700, "LowerFlangeOuterRunRequiresLongWeldModel");
            Value("PlateHeight", p.PlateHeight, "mm"); Value("TopProjection", p.TopProjection, "mm");
            Value("LowerProjection", m.CompressionEndPlateProjection, "mm");
            Value("MomentDemand", m.Moment, "Nmm"); Value("RetainedAxialDemand", m.Axial, "N");
            Value("WebShearTransferred", m.Shear, "N"); Value("FlangeShearTransferred", 0, "N");
            Value("Cjp.FusionDepthSupplied", cjp.FusionDepth, "mm"); Value("Cjp.SeamWidthSupplied", cjp.EffectiveSeamWidth, "mm");
            Value("Cjp.FusionDepthCredited", Math.Min(cjp.FusionDepth, beam.FlangeThickness), "mm");
            Value("Cjp.SeamWidthCredited", Math.Min(cjp.EffectiveSeamWidth, beam.Width), "mm");
            Value("Cjp.InnerFilletLegGeometryOnly", cjp.InnerFilletLeg, "mm"); Value("Cjp.OuterFilletLeg", cjp.OuterFilletLeg, "mm");
            Value("Cjp.AllWeldMinimumFy", cjp.AllWeldMinimumFy, "MPa"); Value("Cjp.AllWeldMinimumFu", cjp.AllWeldMinimumFu, "MPa");
            Value("GammaM0", code.Plates.GammaM0, "1");
            double lowerRun = (beam.Width - beam.WebThickness) / 2 - beam.RootRadius;
            double aWeb = g.WebWeldLeg / Math.Sqrt(2), aLower = g.CompressionFlangeWeldLeg / Math.Sqrt(2);
            // The single real inner fillet receives no added resistance credit, but
            // still needs a physically admissible full-size throat and straight run.
            EffectiveRun("Cjp.InnerFilletGeometry", lowerRun, cjp.InnerFilletLeg / Math.Sqrt(2), FilletWeldEndCondition.FullSize);
            double lWeb = EffectiveRun("Web", beam.ClearWeb, aWeb, p.WebEnds);
            double lLower = EffectiveRun("CompressionFlange", lowerRun, aLower, p.CompressionFlangeEnds);
            if (issues.Count != 0) return Result();
            double area = beam.Width * beam.FlangeThickness;
            double cjpResistance = area * (p.BeamFlangeFy / code.Plates.GammaM0);
            Value("Cjp.NormalFlangeArea", area, "mm2"); Value("Cjp.EquivalentFlangeResistance", cjpResistance, "N");
            if (!(cjpResistance > 0)) issues.Add("CjpEquivalentResistanceOutsideNumericRange");
            if (issues.Count != 0) return Result();
            checks.Add(new ConnectionCheck("Cjp.FullStrengthNormalEquivalence", cjpResistance, cjpResistance,
                code.IsNtc ? "NTC2018 4.2.8.2.1; qualified complete penetration, flange-normal section equivalence only"
                    : "EN1993-1-8:2005 4.7.1; qualified complete penetration, flange-normal section equivalence only"));
            double webNormal = beam.Fy / code.Plates.GammaM0 * beam.WebThickness / (2 * g.WebWeldLeg) * (beam.ClearWeb / lWeb);
            double webShear = m.Shear / (2 * aWeb * lWeb);
            double lowerNormal = p.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing ? 0
                : -p.BeamFlangeFy / code.Plates.GammaM0 * beam.FlangeThickness / (2 * g.CompressionFlangeWeldLeg) * (lowerRun / lLower);
            Value("CompressionFlange.QualifiedDirectBearing", p.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing ? 1 : 0, "1");
            if (!Finite(webNormal) || !Finite(webShear) || !Finite(lowerNormal)) { issues.Add("FilletStressOutsideNumericRange"); return Result(); }
            var plateMaterial = new FilletWeldMaterial(p.EndPlateGrade, plate.Fu, plate.Thickness);
            Evaluate("Web", new FilletWeldMaterial(p.BeamGrade, beam.Fu, beam.WebThickness), webNormal, webNormal, webShear);
            if (p.CompressionTransfer == EndPlateCompressionTransfer.FullStrengthWelds)
                Evaluate("CompressionFlange", new FilletWeldMaterial(p.BeamGrade, p.BeamFlangeFu, beam.FlangeThickness), lowerNormal, lowerNormal, 0);
            return Result();

            double EffectiveRun(string name, double nominal, double throat, FilletWeldEndCondition ends)
            {
                Require(throat >= 3, name + ":ThroatBelowThreeMillimetres");
                Require(ends != FilletWeldEndCondition.Defective, name + ":DefectiveEnds");
                double length = nominal - (!code.IsNtc && ends == FilletWeldEndCondition.NominalWithoutFullSizeEvidence ? 2 * throat : 0);
                Require(length >= Math.Max(30, 6 * throat), name + ":EffectiveLengthBelowScope");
                Require(nominal <= 1700, name + ":LongNonlapWeldRequiresSpecialModel");
                Value(name + ".NominalStraightRun", nominal, "mm"); Value(name + ".EffectiveStraightRun", length, "mm");
                Value(name + ".Throat", throat, "mm"); return length;
            }
            void Evaluate(string name, FilletWeldMaterial material, double sigma, double perpendicular, double parallel)
            {
                try
                {
                    var r = FilletWeldResistance.Verify(code, material, plateMaterial, sigma, perpendicular, parallel, FilletWeldMethod.Directional);
                    issues.AddRange(r.Diagnostics.Select(x => name + ":" + x));
                    checks.AddRange(r.Checks.Select(c => new ConnectionCheck(name + "." + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit)));
                    Value(name + ".SigmaPerpendicular", sigma, "MPa"); Value(name + ".TauPerpendicular", perpendicular, "MPa");
                    Value(name + ".TauParallel", parallel, "MPa"); Value(name + ".BetaW", r.Beta, "1"); Value(name + ".Fu", r.Fu, "MPa");
                }
                catch (ArgumentOutOfRangeException) { issues.Add(name + ":StressEvaluationOutsideNumericRange"); }
            }
            TopFlushCjpEndPlateWeldResult Result() => new TopFlushCjpEndPlateWeldResult(new ConnectionVerification<TopFlushCjpEndPlateWeldInput>(
                p, code, "Steel.Connection.EndPlate.TopFlushCjpWeldComponents",
                "Upper CJP normal-section equivalence with a real inner toe and lower plate projection; web/lower paired-fillet local envelope. No fully-flush or complete-joint qualification.",
                checks, trace, issues, new[] {
                    "CJP equivalence is conditional on stated physical qualification and stronger/equal plate material; it is not an independent full-joint capacity proof.",
                    "No min(tf,tp) plate resistance, extra inner-fillet strength or outer top-flange fillet is credited.",
                    "The actual inner toe equals the unchanged parent T-stub geometry; no parent capacity is silently recalculated.",
                    "All vertical shear is transferred by web fillets only, EN1993-1-8:2005 6.2.2(1).",
                    "Normal fillet demand is the full-yield envelope, including at zero moment; failure of that envelope is not proof of actual-joint failure at lower demand.",
                    "CJP uses normal parent-section gammaM0. Web/lower fillets retain their own EC/NTC throat criteria and effective-end policies."
                }));
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Below(double actual, double minimum) => ConnectionValidation.BelowMinimumWithRoundoff(actual, minimum);
        private static bool Equal(double a, double b) => Finite(a) && Finite(b) && !Below(a, b) && !Below(b, a);
    }
}
