using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One concomitant physical splice case, N/mm. No envelope of independent maxima.</summary>
    public sealed class SpliceActionCase
    {
        public string Id { get; }
        public PlanarConnectionActions Actions { get; }
        public string Source { get; }
        public SpliceActionCase(string id, PlanarConnectionActions actions, string source)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Case identifier and actual combination source are required.");
            Id = id; Actions = actions ?? throw new ArgumentNullException(nameof(actions)); Source = source;
        }
    }

    public sealed class WeldedSpliceButtCaseResult
    {
        public SpliceActionCase Case { get; }
        public WeldedIBeamButtSpliceResult Result { get; }
        internal WeldedSpliceButtCaseResult(SpliceActionCase input, WeldedIBeamButtSpliceResult result)
        { Case = input; Result = result; }
    }

    /// <summary>Application construction only: immutable Model snapshots, explicit ULS/SLS cases
    /// and existing Steel pipeline. No UI, inferred restraints, load combinations or welded execution.</summary>
    public static class BridgeSpliceApplication
    {
        /// <summary>Same positive section-face convention on both members. Physical X longitudinal,
        /// Y up, Z transverse; pole transport precedes M1=-Mz, V2=Fy. Reject unsupported actions.</summary>
        public static SteelActions ToSteelActions(PlanarConnectionActions a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.Fz != 0 || a.Mx != 0 || a.My != 0 || a.ApplicationZ != 0)
                throw new NotSupportedException("Only planar N-V-M in the physical splice frame is supported.");
            return new SteelActions(a.Fx, 0, a.Fy, 0, -(a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx), 0);
        }

        public static SteelVerificationResult EvaluateSection(SteelSectionInput section, PlanarConnectionActions actions,
            ConnectionDesignCode code, Func<SteelVerificationInput, IEnumerable<SteelShearBucklingAssessment>> shearPreparation = null)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            var input = new SteelVerificationInput(section, ToSteelActions(actions), new SteelDesignContext(code.Plates));
            // Missing preparation intentionally remains visible in the returned assessment.
            var preparation = shearPreparation == null ? Array.Empty<SteelShearBucklingAssessment>()
                : shearPreparation(input) ?? throw new InvalidOperationException("The preparation callback returned null.");
            return SteelVerificationPipeline.Execute(SteelMethodSelector.Select(input), preparation);
        }

        public static WeldedIBeamSpliceStandardInput CreateBolted(WeldedIBeamSpliceDetailInput template,
            SteelSectionInput left, SteelSectionInput right, IEnumerable<SpliceActionCase> ultimateCases,
            SpliceLongitudinalFillets welds, string continuityAndSecondOrderSource, SpliceBoltCategory category,
            Func<SteelVerificationInput, IEnumerable<SteelShearBucklingAssessment>> shearPreparation = null,
            WeldedIBeamSpliceSlipInput slipTemplate = null, IEnumerable<SpliceActionCase> serviceabilityCases = null,
            SpliceBlockVerificationMethod blockMethod = SpliceBlockVerificationMethod.FastenerHierarchy,
            SpliceFlangeFaceModel faceModel = SpliceFlangeFaceModel.BalancedEqualFaces)
            => CreateBolted(template, left, right, ultimateCases, welds, continuityAndSecondOrderSource, category,
                SplicePlateStabilityMethod.CompactCovers, shearPreparation, slipTemplate, serviceabilityCases, blockMethod, faceModel);

        public static WeldedIBeamSpliceStandardInput CreateBolted(WeldedIBeamSpliceDetailInput template,
            SteelSectionInput left, SteelSectionInput right, IEnumerable<SpliceActionCase> ultimateCases,
            SpliceLongitudinalFillets welds, string continuityAndSecondOrderSource, SpliceBoltCategory category,
            SplicePlateStabilityMethod plateStability,
            Func<SteelVerificationInput, IEnumerable<SteelShearBucklingAssessment>> shearPreparation = null,
            WeldedIBeamSpliceSlipInput slipTemplate = null, IEnumerable<SpliceActionCase> serviceabilityCases = null,
            SpliceBlockVerificationMethod blockMethod = SpliceBlockVerificationMethod.FastenerHierarchy,
            SpliceFlangeFaceModel faceModel = SpliceFlangeFaceModel.BalancedEqualFaces)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (right == null) throw new ArgumentNullException(nameof(right));
            var u = Snapshot(ultimateCases); var s = Snapshot(serviceabilityCases ?? Array.Empty<SpliceActionCase>(), false);
            if (category == SpliceBoltCategory.A && s.Length != 0)
                throw new ArgumentException("Category A does not request slip cases; select B or C explicitly.");
            if (s.Length != 0 && slipTemplate == null)
                throw new ArgumentException("Explicit SLS cases require the slip surface/assembly template.");
            var original = template.Plates.Bolts;
            if (original.Code.IsNtc && template.Plates.Policy.GammaM1 != Ntc2018DesignPolicy.GammaM1Bridges)
                throw new ArgumentException("NTC 2018 Table 4.2.VII: bridge members use gammaM1=1.10. Use IBeamSplicePlatePolicy.Ntc2018Bridges().", nameof(template));
            var shape = WeldedIBeamSpliceSection.FromModel(left, original.Detail.Section.TopWeldToe, original.Detail.Section.BottomWeldToe);
            // One preparation for the physical detail: every ULS/SLS case shares its demand-independent results.
            var prepared = PreparedWeldedIBeamSplice.Prepare(template, shape);
            var cases = u.Select(c =>
            {
                var detail = Detail(c);
                return new WeldedSpliceStandardCase(c.Id, new WeldedIBeamSpliceParentInput(detail,
                    EvaluateSection(left, c.Actions, original.Code, shearPreparation),
                    EvaluateSection(right, c.Actions, original.Code, shearPreparation), welds, continuityAndSecondOrderSource));
            }).ToArray();
            var slips = new List<WeldedSpliceSlipCase>();
            if (slipTemplate != null)
            {
                if (!SpliceIdentity.Code(slipTemplate.Policy.Code, original.Code))
                    throw new ArgumentException("Slip policy and section/bolt policy must match.");
                if (category == SpliceBoltCategory.C)
                    foreach (var c in u) slips.Add(Slip(c, BoltSlipLimitState.Ultimate, c.Id + ".slip", c.Id));
                foreach (var c in s) slips.Add(Slip(c, BoltSlipLimitState.Serviceability, c.Id, null));
            }
            // No SLS values are synthesized. Missing B/C evidence is reported by Verify.
            return new WeldedIBeamSpliceStandardInput(cases, category, slips, blockMethod, faceModel, plateStability);

            WeldedIBeamSpliceDetailInput Detail(SpliceActionCase c) => prepared.CreateCase(c.Actions, c.Source);
            WeldedSpliceSlipCase Slip(SpliceActionCase c, BoltSlipLimitState state, string id, string ulsId)
                => new WeldedSpliceSlipCase(id, new WeldedIBeamSpliceSlipInput(slipTemplate.Policy, Detail(c).Plates.Bolts, state,
                    slipTemplate.Surfaces, slipTemplate.TighteningControl, slipTemplate.AssemblyQualificationSource, c.Source), ulsId);
        }

        public static IReadOnlyList<WeldedSpliceButtCaseResult> VerifyWelded(ConnectionDesignCode code,
            SteelSectionInput left, SteelSectionInput right, IEnumerable<SpliceActionCase> ultimateCases,
            SpliceButtWeldDetail top, SpliceButtWeldDetail web, SpliceButtWeldDetail bottom, string alignmentAndContinuitySource,
            Func<SteelVerificationInput, IEnumerable<SteelShearBucklingAssessment>> shearPreparation = null)
        {
            var cases = Snapshot(ultimateCases);
            return Array.AsReadOnly(cases.Select(c => new WeldedSpliceButtCaseResult(c,
                WeldedIBeamButtSplice.Verify(new WeldedIBeamButtSpliceInput(code,
                    EvaluateSection(left, c.Actions, code, shearPreparation), EvaluateSection(right, c.Actions, code, shearPreparation),
                    top, web, bottom, c.Source, alignmentAndContinuitySource)))).ToArray());
        }
        private static SpliceActionCase[] Snapshot(IEnumerable<SpliceActionCase> cases, bool nonempty = true)
        {
            if (cases == null) throw new ArgumentNullException(nameof(cases));
            var a = cases.ToArray();
            if (nonempty && a.Length == 0 || a.Any(c => c == null) || a.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != a.Length)
                throw new ArgumentException("Non-null cases with unique identifiers required; ULS cannot be empty.");
            return a;
        }
    }
}

