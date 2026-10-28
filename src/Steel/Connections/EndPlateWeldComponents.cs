using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum EndPlateCompressionTransfer { QualifiedDirectBearing, FullStrengthWelds }

    /// <summary>Qualification of a continuous pair of equal-leg fillets on both faces of each beam part.
    /// The local straight-line envelope does not derive load redistribution through rolled roots,
    /// weld transitions, root gaps or eccentricities. Their treatment requires explicit detail evidence.
    /// ShearTransferSource qualifies uniform transfer of all retained V through the two web welds only.</summary>
    public sealed class EndPlateWeldDetail
    {
        public FilletWeldEndCondition TensionFlangeEnds { get; }
        public FilletWeldEndCondition WebEnds { get; }
        public FilletWeldEndCondition CompressionFlangeEnds { get; }
        public EndPlateCompressionTransfer CompressionTransfer { get; }
        public string DetailSource { get; }
        public string NormalTransferSource { get; }
        public string ShearTransferSource { get; }
        public string ConsumableCompatibilitySource { get; }
        public string LocalEccentricitySource { get; }
        public string CompressionBearingSource { get; }
        public EndPlateWeldDetail(FilletWeldEndCondition tensionFlangeEnds, FilletWeldEndCondition webEnds,
            FilletWeldEndCondition compressionFlangeEnds, EndPlateCompressionTransfer compressionTransfer,
            string detailSource, string normalTransferSource, string shearTransferSource,
            string consumableCompatibilitySource, string localEccentricitySource, string compressionBearingSource = null)
        {
            foreach (var ends in new[] { tensionFlangeEnds, webEnds, compressionFlangeEnds })
                if (!Enum.IsDefined(typeof(FilletWeldEndCondition), ends)) throw new ArgumentOutOfRangeException(nameof(ends));
            if (!Enum.IsDefined(typeof(EndPlateCompressionTransfer), compressionTransfer)) throw new ArgumentOutOfRangeException(nameof(compressionTransfer));
            TensionFlangeEnds = tensionFlangeEnds; WebEnds = webEnds; CompressionFlangeEnds = compressionFlangeEnds;
            CompressionTransfer = compressionTransfer; DetailSource = detailSource; NormalTransferSource = normalTransferSource;
            ShearTransferSource = shearTransferSource; ConsumableCompatibilitySource = consumableCompatibilitySource;
            LocalEccentricitySource = localEccentricitySource; CompressionBearingSource = compressionBearingSource;
        }
    }

    public sealed class EndPlateWeldComponentInput
    {
        public EndPlateMomentComponentResult MomentComponents { get; }
        public FilletWeldSteelGrade BeamGrade { get; }
        public FilletWeldSteelGrade EndPlateGrade { get; }
        public double BeamFlangeFy { get; }
        public double BeamFlangeFu { get; }
        public string MaterialSource { get; }
        public EndPlateWeldDetail Detail { get; }
        public EndPlateWeldComponentInput(EndPlateMomentComponentResult momentComponents,
            FilletWeldSteelGrade beamGrade, FilletWeldSteelGrade endPlateGrade, double beamFlangeFy,
            double beamFlangeFu, string materialSource, EndPlateWeldDetail detail)
        {
            MomentComponents = momentComponents ?? throw new ArgumentNullException(nameof(momentComponents));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), beamGrade)) throw new ArgumentOutOfRangeException(nameof(beamGrade));
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), endPlateGrade)) throw new ArgumentOutOfRangeException(nameof(endPlateGrade));
            ConnectionValidation.Positive(beamFlangeFy, nameof(beamFlangeFy)); ConnectionValidation.Positive(beamFlangeFu, nameof(beamFlangeFu));
            if (beamFlangeFu < beamFlangeFy) throw new ArgumentException("Flange ultimate strength must not be below yield strength.");
            BeamGrade = beamGrade; EndPlateGrade = endPlateGrade; BeamFlangeFy = beamFlangeFy; BeamFlangeFu = beamFlangeFu;
            MaterialSource = materialSource;
        }
    }

    public sealed class EndPlateWeldComponentResult
    {
        public ConnectionVerification<EndPlateWeldComponentInput> WeldAssessment { get; }
        public EndPlateMomentComponentResult MomentComponents => WeldAssessment.Input.MomentComponents;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> PendingChecks { get; }
        internal EndPlateWeldComponentResult(ConnectionVerification<EndPlateWeldComponentInput> assessment)
        {
            WeldAssessment = assessment;
            PendingChecks = Array.AsReadOnly(new[] {
                "The rolled-root and flange/web transition transfer, paired-fillet continuity and local eccentricity require the supplied independent detail qualification; they are not derived by this local envelope",
                "Direct compression bearing, when selected, is externally qualified; no contact pressure, fit tolerance or bearing-surface check is performed here",
                "Bolt components, plate/support net and block failure, local force introduction and member resistance/stability",
                "Rotation capacity and global-model compatibility; a full-strength local weld envelope proves no rotational ductility" });
        }
    }

    /// <summary>Local full-yield normal-force envelope of qualified paired fillets, with all concomitant
    /// vertical shear carried by the web welds. N, mm, MPa. Equal legs and90-degree fusion faces only;
    /// effective throat is leg/sqrt(2), with no additional penetration or root-gap allowance.</summary>
    internal static class EndPlateWeldComponents
    {
        public static EndPlateWeldComponentResult Verify(EndPlateWeldComponentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var parent = p.MomentComponents.ComponentAssessment; var m = parent.Input; var code = m.Policy.Code;
            var beam = m.Beam; var g = m.InternalGeometry; var detail = p.Detail;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            Required(p.MaterialSource, "ActualThicknessMaterialSourceRequired");
            Required(detail.DetailSource, "ContinuousPairedFilletDetailSourceRequired");
            Required(detail.NormalTransferSource, "RolledRootAndTransitionNormalTransferQualificationRequired");
            Required(detail.ShearTransferSource, "UniformWebOnlyShearTransferQualificationRequired");
            Required(detail.ConsumableCompatibilitySource, "ConsumableCompatibilityQualificationRequired");
            Required(detail.LocalEccentricitySource, "LocalEccentricityQualificationRequired");
            if (detail.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing)
                Required(detail.CompressionBearingSource, "CompressionBearingQualificationRequired");
            if (parent.Status != ConnectionStatus.Passed) issues.Add("PassedMomentComponentsRequiredForWeldEnvelope");
            if (p.BeamFlangeFy > (int)p.BeamGrade || beam.Fy > (int)p.BeamGrade || m.EndPlate.Fy > (int)p.EndPlateGrade)
                issues.Add("YieldStrengthExceedsDeclaredNominalGrade");
            if (Math.Min(m.EndPlate.Thickness, Math.Min(beam.FlangeThickness, beam.WebThickness)) < 4)
                issues.Add("ParentThicknessBelowFourMillimetresScope");
            if (m.EndPlate.Width < beam.Width)
                issues.Add("EndPlateMustCoverFullBeamFlangeWidthForPairedFillets");
            if (beam.Width > 1700)
                issues.Add("FlangeOuterRunLongNonlapWeldRequiresSpecialModel");
            if (m.ExtensionGeometry == null)
                issues.Add("FlushPlateHasNoOuterTensionFlangeFilletInThisPairedFilletModel");
            // The shortest straight flange run is the clear outstand. The outer run is longer.
            // Curved root/transition load transfer is explicitly qualified outside this local model.
            double flangeRun = (beam.Width - beam.WebThickness) / 2 - beam.RootRadius;
            double webRun = beam.ClearWeb;
            double aTension = g.TensionFlangeWeldLeg / Math.Sqrt(2);
            double aWeb = g.WebWeldLeg / Math.Sqrt(2);
            double aCompression = g.CompressionFlangeWeldLeg / Math.Sqrt(2);
            double lTension = Length("TensionFlange", flangeRun, aTension, detail.TensionFlangeEnds);
            double lWeb = Length("Web", webRun, aWeb, detail.WebEnds);
            double lCompression = Length("CompressionFlange", flangeRun, aCompression, detail.CompressionFlangeEnds);
            Value("TensionFlange.Leg", g.TensionFlangeWeldLeg, "mm"); Value("Web.Leg", g.WebWeldLeg, "mm");
            Value("CompressionFlange.Leg", g.CompressionFlangeWeldLeg, "mm");
            Value("WebShearTransferred", m.Shear, "N"); Value("FlangeShearTransferred", 0, "N");
            Value("MomentDemand", m.Moment, "Nmm");
            if (issues.Count != 0) return Result();
            var plateMaterial = new FilletWeldMaterial(p.EndPlateGrade, m.EndPlate.Fu, m.EndPlate.Thickness);
            var flangeMaterial = new FilletWeldMaterial(p.BeamGrade, p.BeamFlangeFu, beam.FlangeThickness);
            var webMaterial = new FilletWeldMaterial(p.BeamGrade, beam.Fu, beam.WebThickness);
            double fyFlange = p.BeamFlangeFy / code.Plates.GammaM0;
            double fyWeb = beam.Fy / code.Plates.GammaM0;
            // Each fillet takes half the transverse plate force fy*t per unit nominal root length.
            // At45 degrees sigma_perp=tau_perp=fy*t/(2*leg), including end-shortening amplification.
            double normalTension = fyFlange * (beam.FlangeThickness / (2 * g.TensionFlangeWeldLeg)) * (flangeRun / lTension);
            double normalWeb = fyWeb * (beam.WebThickness / (2 * g.WebWeldLeg)) * (webRun / lWeb);
            double shearWeb = m.Shear / (2 * aWeb * lWeb);
            double normalCompression = detail.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing ? 0
                : -fyFlange * (beam.FlangeThickness / (2 * g.CompressionFlangeWeldLeg)) * (flangeRun / lCompression);
            Value("TensionFlange.NormalEnvelopeDesignStress", fyFlange, "MPa");
            Value("Web.NormalEnvelopeDesignStress", fyWeb, "MPa");
            Value("CompressionFlange.QualifiedDirectBearing", detail.CompressionTransfer == EndPlateCompressionTransfer.QualifiedDirectBearing ? 1 : 0, "1");
            Value("CompressionFlange.SigmaPerpendicular", normalCompression, "MPa");
            if (!Finite(normalTension) || !Finite(normalWeb) || !Finite(shearWeb) || !Finite(normalCompression))
            { issues.Add("WeldStressOutsideNumericRange"); return Result(); }
            Evaluate("TensionFlange", flangeMaterial, normalTension, normalTension, 0);
            Evaluate("Web", webMaterial, normalWeb, normalWeb, shearWeb);
            if (detail.CompressionTransfer == EndPlateCompressionTransfer.FullStrengthWelds)
                Evaluate("CompressionFlange", flangeMaterial, normalCompression, normalCompression, 0);
            return Result();

            double Length(string name, double nominal, double throat, FilletWeldEndCondition ends)
            {
                if (throat < 3) issues.Add(name + ":ThroatBelowThreeMillimetres");
                if (ends == FilletWeldEndCondition.Defective) issues.Add(name + ":DefectiveEnds");
                // Same policy as FilletWeldGroup: EC deducts2a without full-size evidence;
                // NTC uses nominal non-defective lengths. No EC deduction is silently imposed on NTC.
                double effective = nominal - (!code.IsNtc && ends == FilletWeldEndCondition.NominalWithoutFullSizeEvidence ? 2 * throat : 0);
                if (effective < Math.Max(30, 6 * throat)) issues.Add(name + ":EffectiveLengthBelowScope");
                if (nominal > 1700) issues.Add(name + ":LongNonlapWeldRequiresSpecialModel");
                Value(name + ".NominalStraightRun", nominal, "mm"); Value(name + ".EffectiveStraightRun", effective, "mm");
                Value(name + ".Throat", throat, "mm");
                return effective;
            }
            void Evaluate(string name, FilletWeldMaterial material, double sigma, double perpendicular, double parallel)
            {
                var result = FilletWeldResistance.Verify(code, material, plateMaterial, sigma, perpendicular, parallel, FilletWeldMethod.Directional);
                issues.AddRange(result.Diagnostics.Select(d => name + ":" + d));
                checks.AddRange(result.Checks.Select(c => c.WithId(name + "." + c.Id)));
                if (name != "CompressionFlange") Value(name + ".SigmaPerpendicular", sigma, "MPa");
                Value(name + ".TauPerpendicular", perpendicular, "MPa"); Value(name + ".TauParallel", parallel, "MPa");
                Value(name + ".Fu", result.Fu, "MPa"); Value(name + ".BetaW", result.Beta, "1");
            }
            void Required(string source, string issue) { if (string.IsNullOrWhiteSpace(source)) issues.Add(issue); }
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            EndPlateWeldComponentResult Result() => new EndPlateWeldComponentResult(new ConnectionVerification<EndPlateWeldComponentInput>(
                p, code, "Steel.Connection.EndPlate.WeldComponents",
                "Local full-yield normal-stress envelope in qualified continuous paired fillets, with all concomitant vertical shear in web welds. No entire-joint or rotation qualification.",
                checks, trace, issues, new[] {
                    "EN1993-1-8:2005 6.2.2(1): beam web welds transfer all vertical shear, with no assistance from flange welds.",
                    "The full-yield normal envelope is evaluated even at zero moment; it is not a reconstructed elastic stress distribution or plastic rotation proof.",
                    "Equal-leg90-degree fillets use a=leg/sqrt(2), with no deep penetration or root-gap credit; geometry uses the same weld legs as the parent T-stubs.",
                    "This paired-flange-fillet model requires an extended end plate covering the beam flange width; a flush plate needs a different top-flange weld detail.",
                    "Normal-force transfer, continuous paired fillets and rolled-root transitions are independently qualified: " + detail.NormalTransferSource,
                    "Uniform transfer through the effective straight web welds is independently qualified: " + detail.ShearTransferSource,
                    "Local eccentricity and detailing qualification: " + detail.LocalEccentricitySource,
                    "Compression bearing, if selected, is caller-qualified and transfers no normal force to the nominal compression welds: " + detail.CompressionBearingSource,
                    "NTC uses its declared factors, directional throat-stress criterion and effective-length policy; the supplementary connection envelope remains explicitly identified.",
                    "A failed full-yield envelope does not automatically establish failure of the actual joint at a lower action; no parent resistance is silently changed."
                }));
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
