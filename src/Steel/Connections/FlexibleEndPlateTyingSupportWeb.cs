using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One plate on one face of a rolled I/H column web. Frame is Tying's frame:
    /// column longitudinal +Y, section depth X, web thickness Z; contact face Z=0.
    /// End distances run from extreme bolt rows to the actual nearest longitudinal end/interruption.
    /// ExistingSupportActions is the same concomitant accidental state, excluding this connection.
    /// All presence flags are mandatory. A description of an interior model is not a finite-boundary certificate.</summary>
    public sealed class FlexibleEndPlateTyingSupportWebInput
    {
        public FlexibleEndPlateTyingInput Tying { get; }
        public ConnectionRolledISection Support { get; }
        public double SupportTopBoltEdge { get; }
        public double SupportBottomBoltEdge { get; }
        public PlanarConnectionActions ExistingSupportActions { get; }
        public bool OppositeFaceConnected { get; }
        public bool SupportHasAdditionalHoles { get; }
        public bool SupportHasOpenings { get; }
        public bool SupportHasStiffeners { get; }
        public string ActionCombinationSource { get; }
        public string SupportGeometrySource { get; }
        public string SupportInteriorPanelModelSource { get; }
        public FlexibleEndPlateTyingSupportWebInput(FlexibleEndPlateTyingInput tying,
            ConnectionRolledISection support, double supportTopBoltEdge, double supportBottomBoltEdge,
            PlanarConnectionActions existingSupportActions, bool oppositeFaceConnected,
            bool supportHasAdditionalHoles, bool supportHasOpenings, bool supportHasStiffeners,
            string actionCombinationSource, string supportGeometrySource, string supportInteriorPanelModelSource)
        {
            Tying = tying ?? throw new ArgumentNullException(nameof(tying));
            Support = support ?? throw new ArgumentNullException(nameof(support));
            ExistingSupportActions = existingSupportActions ?? throw new ArgumentNullException(nameof(existingSupportActions));
            ConnectionValidation.Positive(supportTopBoltEdge, nameof(supportTopBoltEdge));
            ConnectionValidation.Positive(supportBottomBoltEdge, nameof(supportBottomBoltEdge));
            SupportTopBoltEdge = supportTopBoltEdge; SupportBottomBoltEdge = supportBottomBoltEdge;
            OppositeFaceConnected = oppositeFaceConnected; SupportHasAdditionalHoles = supportHasAdditionalHoles;
            SupportHasOpenings = supportHasOpenings; SupportHasStiffeners = supportHasStiffeners;
            ActionCombinationSource = actionCombinationSource; SupportGeometrySource = supportGeometrySource;
            SupportInteriorPanelModelSource = supportInteriorPanelModelSource;
        }
    }

    /// <summary>Check14 resistance of the ideal continuous interior-web mechanism only.
    /// LocalModelStatus never qualifies actual finite boundaries or the supporting member.</summary>
    public sealed class FlexibleEndPlateTyingSupportWebResult
    {
        public FlexibleEndPlateTyingSupportWebInput Input { get; }
        public FlexibleEndPlateTyingResult PlateAndBeamAssessment { get; }
        public PlanarConnectionActions ActionsAtSupportWebMidplane { get; }
        public ConnectionStatus LocalModelStatus { get; }
        public bool IsLocalModelCalculated { get; }
        public bool IsSupportBoundaryCompatibilityVerified => false;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> PendingChecks { get; } = Array.AsReadOnly(new[] {
            "SUPPORT_FINITE_BOUNDARIES_AND_YIELD_LINE_COMPATIBILITY",
            "SUPPORT_MEMBER_ACTIONS_AND_FORCE_TRANSFER",
            "ACTUAL_HEAD_NUT_WASHER_BEARING_AND_PUNCHING",
            "WELDS_UNDER_ACCIDENTAL_TYING_AND_REQUIRED_FAILURE_HIERARCHY",
            "GLOBAL_ROBUSTNESS_AND_DEFORMATION_REQUIREMENTS" });
        public IReadOnlyList<string> Assumptions { get; } = Array.AsReadOnly(new[] {
            "SCI P358 Check14 local continuous-web yield-line mechanism, one face only; no two-face resultant substitution.",
            "The plate and its hardware lie in the planar web between the rolled roots. Declared openings, stiffeners and additional holes are excluded.",
            "The minimum longitudinal distance40mm retains the physical plate footprint only; it is not a yield-line end-distance criterion.",
            "No finite-boundary compatibility is verified; SupportInteriorPanelModelSource is caller evidence, not an automatic certificate.",
            "The source's factor1.5 allows for column axial compression but supplies no general N/V/M interaction. The first software field requires all six pre-existing support actions explicitly zero.",
            "Unit plastic moment is Nmm/mm=N. The printed kNm/mm unit beside17.7 in P358 p49 is not used.",
            "A passing local model does not close Support or the whole joint, and does not override PlateAndBeamAssessment." });
        public double? LocalUtilization => IsLocalModelCalculated ? (double?)Checks.Max(c => c.Utilization) : null;
        public double? LocalTyingResistance => IsLocalModelCalculated ? (double?)Checks.Single().Resistance : null;
        public string Scope => "SCI P358(2014) Check14 ideal interior supporting rolled-column web under one centred accidental tying force. S275 fy275/fu410. Finite-boundary compatibility, two-face connections and member interactions remain unverified.";
        internal FlexibleEndPlateTyingSupportWebResult(FlexibleEndPlateTyingSupportWebInput input,
            FlexibleEndPlateTyingResult component, PlanarConnectionActions actions,
            IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            Input = input; PlateAndBeamAssessment = component; ActionsAtSupportWebMidplane = actions;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            var evaluated = ConnectionOutcome.Retained(checks, Diagnostics.Count);
            Checks = Array.AsReadOnly(evaluated); Trace = Array.AsReadOnly(trace.ToArray());
            IsLocalModelCalculated = ConnectionOutcome.IsComplete(evaluated, Diagnostics.Count);
            LocalModelStatus = ConnectionOutcome.Status(evaluated, Diagnostics.Count);
        }
    }

    public static class FlexibleEndPlateTyingSupportWeb
    {
        public static FlexibleEndPlateTyingSupportWebResult Verify(FlexibleEndPlateTyingSupportWebInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var component = FlexibleEndPlateTyingComponents.Verify(p.Tying);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); PlanarConnectionActions atSupport = null;
            var g = p.Tying.Geometry; var s = p.Support; var existing = p.ExistingSupportActions;
            if (!component.IsComponentAssessmentComplete)
                issues.AddRange(component.Diagnostics.Select(d => "PlateAndBeam:" + d));
            Required(p.ActionCombinationSource, "ACCIDENTAL_ACTION_COMBINATION_SOURCE_REQUIRED");
            Required(p.SupportGeometrySource, "SUPPORT_GEOMETRY_SOURCE_REQUIRED");
            Required(p.SupportInteriorPanelModelSource, "INTERIOR_WEB_MODEL_SOURCE_REQUIRED");
            Require(!p.OppositeFaceConnected, "TWO_FACE_TYING_REQUIRES_SEPARATE_MODEL");
            Require(!p.SupportHasAdditionalHoles && !p.SupportHasOpenings && !p.SupportHasStiffeners,
                "SUPPORT_WEB_INTERFERENCE_REQUIRES_SEPARATE_MODEL");
            Require(s.Fy == 275 && s.Fu == 410, "INITIAL_SUPPORT_S275_FY275_FU410_SCOPE");
            Require(p.SupportTopBoltEdge >= g.EndDistance && p.SupportBottomBoltEdge >= g.EndDistance,
                "SUPPORT_END_INTERSECTS_PLATE_FOOTPRINT");
            Require(g.Plate.Width <= s.ClearWeb && g.Gauge + g.ContactWidth <= s.ClearWeb,
                "PLATE_OR_HARDWARE_INTERSECTS_SUPPORT_ROOTS");
            Require(existing.Fx == 0 && existing.Fy == 0 && existing.Fz == 0
                && existing.Mx == 0 && existing.My == 0 && existing.Mz == 0,
                "PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");
            Value("Support.ClearWeb", s.ClearWeb, "mm"); Value("Support.WebThickness", s.WebThickness, "mm");
            Value("Support.TopBoltEdge", p.SupportTopBoltEdge, "mm"); Value("Support.BottomBoltEdge", p.SupportBottomBoltEdge, "mm");
            Value("ExistingSupport.Fx", existing.Fx, "N"); Value("ExistingSupport.Fy", existing.Fy, "N");
            Value("ExistingSupport.Fz", existing.Fz, "N"); Value("ExistingSupport.Mx", existing.Mx, "Nmm");
            Value("ExistingSupport.My", existing.My, "Nmm"); Value("ExistingSupport.Mz", existing.Mz, "Nmm");
            Value("ExistingSupport.ApplicationX", existing.ApplicationX, "mm");
            Value("ExistingSupport.ApplicationY", existing.ApplicationY, "mm");
            Value("ExistingSupport.ApplicationZ", existing.ApplicationZ, "mm");
            if (issues.Count != 0) return Result();

            double beta = g.Gauge / s.ClearWeb, gamma = g.Bolt.HoleDiameter / s.ClearWeb;
            double eta = ((g.Rows - 1) * g.Pitch - g.Rows * g.Bolt.HoleDiameter / 2) / s.ClearWeb;
            Require(beta > 0 && beta < 1 && gamma > 0 && gamma < 1 && eta > 0
                && g.Gauge > g.Bolt.HoleDiameter, "SUPPORT_YIELD_LINE_GEOMETRY_OUTSIDE_SCOPE");
            Value("Support.Beta1", beta, "1"); Value("Support.Gamma1", gamma, "1"); Value("Support.Eta1", eta, "1");
            if (issues.Count != 0) return Result();
            double mu = p.Tying.Policy.GammaMu;
            double unitMoment = s.Fu * s.WebThickness * s.WebThickness / (4 * mu);
            double resistance = 8 * unitMoment / (1 - beta) * (eta + 1.5 * Math.Sqrt(1 - beta) * Math.Sqrt(1 - gamma));
            var c = component.ActionsAtPlateCentre;
            // The beam's outward force is transferred as +Fz onto the support. Moving
            // from the plate centre to Z=-tw/2 is parallel to Fz and introduces no couple.
            atSupport = new PlanarConnectionActions(c.Fx, c.Fy, c.Fz, c.Mx, c.My, c.Mz,
                g.PlateCentreX, g.PlateCentreY, -s.WebThickness / 2);
            Value("Policy.GammaMu", mu, "1"); Value("Support.UltimateStrength", s.Fu, "MPa");
            Value("Support.UnitPlasticMoment", unitMoment, "Nmm/mm"); Value("Support.LocalTyingResistance", resistance, "N");
            Value("Actions.SupportFz", c.Fz, "N"); Value("Actions.SupportMidplaneZ", atSupport.ApplicationZ, "mm");
            Value("Actions.SupportApplicationY", atSupport.ApplicationY, "mm");
            if (!Finite(resistance) || resistance <= 0) issues.Add("SUPPORT_RESISTANCE_OUTSIDE_NUMERIC_RANGE");
            else checks.Add(new ConnectionCheck("Tying.SupportWeb.InteriorYieldLines", c.Fz, resistance,
                "SCI P358(2014) Check14 p29/example2 pp48-49; ideal continuous interior web, finite boundaries unverified"));
            return Result();

            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Required(string source, string diagnostic) => Require(!string.IsNullOrWhiteSpace(source), diagnostic);
            void Value(string name, double value, string unit)
            { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add(name + "OutsideNumericRange"); }
            FlexibleEndPlateTyingSupportWebResult Result() => new FlexibleEndPlateTyingSupportWebResult(p, component, atSupport, checks, trace, issues);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
