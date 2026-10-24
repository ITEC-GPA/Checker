using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Two equal continuous 90-degree fillet welds along the full plate height,
    /// one on each beam-web face. Geometry and accidental actions are taken only from Tying.
    /// WeldDetailSource records this physical detail; no reduced/incomplete/intermittent weld is inferred.</summary>
    public sealed class FlexibleEndPlateTyingWeldInput
    {
        public FlexibleEndPlateTyingInput Tying { get; }
        public FilletWeldEndCondition Ends { get; }
        public string ConsumableCompatibilitySource { get; }
        public string WeldDetailSource { get; }
        public string ActionCombinationSource { get; }
        public FlexibleEndPlateTyingWeldInput(FlexibleEndPlateTyingInput tying,
            FilletWeldEndCondition ends, string consumableCompatibilitySource,
            string weldDetailSource, string actionCombinationSource)
        {
            Tying = tying ?? throw new ArgumentNullException(nameof(tying));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), ends)) throw new ArgumentOutOfRangeException(nameof(ends));
            Ends = ends; ConsumableCompatibilitySource = consumableCompatibilitySource;
            WeldDetailSource = weldDetailSource; ActionCombinationSource = actionCombinationSource;
        }
    }

    /// <summary>SCI accidental-tying weld component only. The C.1 throat requirement develops
    /// beam-web yielding, not the ultimate web resistance of Check12 or whole-joint ductility.</summary>
    public sealed class FlexibleEndPlateTyingWeldResult
    {
        public FlexibleEndPlateTyingWeldInput Input { get; }
        public FlexibleEndPlateTyingResult PlateAndBeamAssessment { get; }
        public PlanarConnectionActions ActionsAtWeldCentre { get; }
        public ConnectionStatus Status { get; }
        public bool IsComponentAssessmentComplete => Status != ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "SUPPORT_LOCAL_TYING_RESISTANCE_AND_CONTACT_COMPATIBILITY",
            "SUPPORT_MEMBER_ACTIONS_AND_FORCE_TRANSFER",
            "ACTUAL_HEAD_NUT_WASHER_BEARING_AND_PUNCHING",
            "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS" });
        public IReadOnlyList<string> Assumptions { get; } = Array.AsReadOnly(new[] {
            "Separate SCI P358 accidental-tying policy with gammaMu1.1; no ordinary EC3/NTC certificate.",
            "Two equal symmetric continuous full-size fillet welds, 90-degree fusion, along the complete plate height.",
            "AppendixC.1 develops tw*fy per unit length. It does not develop tw*fu/gammaMu or certify an ultimate hierarchy for the entire joint.",
            "The AppendixC.2 shear-related 0.8 reduction and the simple 0.40tw rule are not used.",
            "The plate Mode1/weld capacity ratio is reported for comparison, not as proof of postplastic local redistribution or global ductility.",
            "PlateAndBeamAssessment is retained separately. A passing weld component does not override a failed plate/bolt/beam component." });
        public ConnectionCheck GoverningCheck => IsComponentAssessmentComplete ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;
        public double? LoadUtilization => IsComponentAssessmentComplete
            ? (double?)Checks.Where(c => c.Id != "Tying.Weld.FullStrengthWebYieldThroat").Max(c => c.Utilization) : null;
        public double? WeldTyingResistance => IsComponentAssessmentComplete
            ? (double?)Trace.Single(v => v.Name == "Weld.TyingResistance").Value : null;
        public string Scope => "SCI P358(2014) AppendixC.1 accidental-tying weld resistance and beam-web yield-strength throat criterion, standard partial-depth S275 end plate. No ordinary N+V, support verification or whole-joint qualification.";
        internal FlexibleEndPlateTyingWeldResult(FlexibleEndPlateTyingWeldInput input,
            FlexibleEndPlateTyingResult component, PlanarConnectionActions actions,
            IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            Input = input; PlateAndBeamAssessment = component; ActionsAtWeldCentre = actions;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            var evaluated = Diagnostics.Count == 0 ? checks.ToArray() : Array.Empty<ConnectionCheck>();
            Checks = Array.AsReadOnly(evaluated); Trace = Array.AsReadOnly(trace.ToArray());
            Status = Diagnostics.Count != 0 || evaluated.Length == 0 ? ConnectionStatus.NotSupported
                : evaluated.All(c => c.Passed) ? ConnectionStatus.Passed : ConnectionStatus.Failed;
        }
    }

    public static class FlexibleEndPlateTyingWeld
    {
        public static FlexibleEndPlateTyingWeldResult Verify(FlexibleEndPlateTyingWeldInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var component = FlexibleEndPlateTyingComponents.Verify(p.Tying);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); PlanarConnectionActions atWeld = null;
            if (!component.IsComponentAssessmentComplete)
                issues.AddRange(component.Diagnostics.Select(d => "PlateAndBeam:" + d));
            Require(p.Ends == FilletWeldEndCondition.FullSize, "FULL_SIZE_WELD_ENDS_REQUIRED");
            Required(p.ConsumableCompatibilitySource, "CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED");
            Required(p.WeldDetailSource, "SYMMETRIC_CONTINUOUS_WELD_DETAIL_SOURCE_REQUIRED");
            Required(p.ActionCombinationSource, "ACCIDENTAL_ACTION_COMBINATION_SOURCE_REQUIRED");
            if (issues.Count != 0) return Result();

            var g = p.Tying.Geometry; var c = component.ActionsAtPlateCentre;
            double a = g.WeldThroat, length = g.Height, fu = Math.Min(g.Beam.Fu, g.Plate.Fu);
            double gamma = p.Tying.Policy.GammaMu, beta = .85;
            // The beam transmits +Fz to the plate; the weld reaction on the beam is -Fz.
            // Roots lie at Z=tp, plate actions at Z=tp/2. Translation is parallel to the
            // ONLY admitted force, so r x F is zero. The frozen component already checked all six actions.
            atWeld = new PlanarConnectionActions(c.Fx, c.Fy, c.Fz, c.Mx, c.My, c.Mz,
                g.PlateCentreX, g.PlateCentreY, g.Plate.Thickness);
            double area = 2 * a * length;
            double sigma = c.Fz / area / Math.Sqrt(2), tau = sigma;
            double equivalent = FilletWeldNumbers.Norm(sigma, Math.Sqrt(3) * tau, 0);
            double equivalentResistance = fu / (beta * gamma), normalResistance = .9 * fu / gamma;
            double required = g.Beam.WebThickness * g.Beam.Fy * beta * gamma / (Math.Sqrt(2) * fu);
            double directionalCapacity = Math.Sqrt(2) * a * length * equivalentResistance;
            double normalCapacity = 2 * Math.Sqrt(2) * a * length * normalResistance;
            double capacity = Math.Min(directionalCapacity, normalCapacity);
            double mode1 = component.Checks.Single(v => v.Id == "Tying.EndPlate.Mode1").Resistance;
            Value("Policy.GammaMu", gamma, "1"); Value("Weld.BetaW", beta, "1"); Value("Weld.Fu", fu, "MPa");
            Value("Weld.Throat", a, "mm"); Value("Weld.EffectiveLengthPerSide", length, "mm");
            Value("Weld.TotalThroatArea", area, "mm2"); Value("Weld.RequiredWebYieldThroat", required, "mm");
            Value("Weld.SigmaPerpendicular", sigma, "MPa"); Value("Weld.TauPerpendicular", tau, "MPa");
            Value("Weld.TauParallel", 0, "MPa"); Value("Weld.DirectionalResistance", directionalCapacity, "N");
            Value("Weld.NormalResistance", normalCapacity, "N"); Value("Weld.TyingResistance", capacity, "N");
            Value("BeamWeb.YieldForce", g.Beam.WebThickness * length * g.Beam.Fy, "N");
            Value("BeamWeb.UltimateForce", component.Checks.Single(v => v.Id == "Tying.BeamWeb.Tension").Resistance, "N");
            Value("Plate.Mode1Resistance", mode1, "N"); Value("Plate.Mode1ToWeldResistanceRatio", mode1 / capacity, "1");
            Value("Actions.TransmittedFz", c.Fz, "N"); Value("Actions.WeldReactionOnBeamFz", -c.Fz, "N");
            Value("Geometry.WeldRootZ", g.Plate.Thickness, "mm"); Value("Geometry.PlateCentreZ", c.ApplicationZ, "mm");
            Value("Geometry.LeftRootX", -g.Beam.WebThickness / 2, "mm");
            Value("Geometry.RightRootX", g.Beam.WebThickness / 2, "mm");
            Value("Actions.WeldCentreY", c.ApplicationY, "mm");
            Check("Tying.Weld.FullStrengthWebYieldThroat", required, a, "SCI P358(2014) AppendixC.1 p279: beam-web yielding, not ultimate strength", "mm");
            Check("Tying.Weld.DirectionalEquivalent", equivalent, equivalentResistance,
                "SCI P358(2014) AppendixC.1 p279; EN1993-1-8:2005 4.5.3.2 eq4.1 with explicit SCI gammaMu", "MPa");
            Check("Tying.Weld.NormalTension", Math.Max(0, sigma), normalResistance,
                "EN1993-1-8:2005 4.5.3.2(6) eq4.1; SCI P358 AppendixC.1 accidental gammaMu", "MPa");
            return Result();

            void Required(string source, string diagnostic) => Require(!string.IsNullOrWhiteSpace(source), diagnostic);
            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + "OutsideNumericRange"); }
            void Check(string id, double demand, double resistance, string source, string unit)
            {
                if (!Finite(demand) || !Finite(resistance) || demand < 0 || resistance <= 0) issues.Add(id + "OutsideNumericRange");
                else checks.Add(new ConnectionCheck(id, demand, resistance, source, unit));
            }
            FlexibleEndPlateTyingWeldResult Result() => new FlexibleEndPlateTyingWeldResult(p, component, atWeld, checks, trace, issues);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
