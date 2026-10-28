using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class ColumnWebPolicy
    {
        public ConnectionDesignCode Code { get; }
        public double GammaM1 { get; }
        public double ElasticModulus => 210000;
        private ColumnWebPolicy(ConnectionDesignCode code, double gammaM1)
        { Code = code; ConnectionValidation.Positive(gammaM1, nameof(gammaM1)); GammaM1 = gammaM1; }
        public static ColumnWebPolicy Eurocode3(ConnectionDesignCode code, double gammaM1)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (code.IsNtc) throw new ArgumentException("Use the NTC factory for NTC factors.", nameof(code));
            return new ColumnWebPolicy(code, gammaM1);
        }
        public static ColumnWebPolicy Ntc2018(ConnectionDesignCode code)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (!code.IsNtc) throw new ArgumentException("The NTC factory requires the national policy.", nameof(code));
            return new ColumnWebPolicy(code, Ntc2018DesignPolicy.GammaM1);
        }
    }

    /// <summary>Verified effective loaded length along the column axis; mm.
    /// Source identifies the load spread or relevant row/group T-stub mechanism, including
    /// end/stiffener boundaries. This is not the clear depth of the web across the section.</summary>
    public sealed class ColumnWebEffectiveWidth
    {
        public double Width { get; }
        public string Source { get; }
        public IReadOnlyList<SectionResistanceValue> DerivationTrace { get; }
        internal ConnectionRolledISection DerivedColumn { get; }
        public ColumnWebEffectiveWidth(double width, string source)
            : this(width, source, null, Array.Empty<SectionResistanceValue>()) { }
        private ColumnWebEffectiveWidth(double width, string source, ConnectionRolledISection derivedColumn,
            SectionResistanceValue[] derivationTrace)
        {
            ConnectionValidation.Positive(width, nameof(width)); Width = width; Source = source;
            DerivedColumn = derivedColumn; DerivationTrace = Array.AsReadOnly((SectionResistanceValue[])derivationTrace.Clone());
        }

        /// <summary>EN1993-1-8 6.2.6.2(1), bolted rectangular end plate on a rolled column.
        /// Equal-leg compression-flange weld: WeldLeg is leg, not throat. Projection is
        /// measured from the beam compression-flange exterior face to the end-plate end.
        /// The plate must extend beyond the weld toe. Free column ends must not truncate beff.</summary>
        public static ColumnWebEffectiveWidth FromBoltedEndPlate(ConnectionRolledISection column,
            double beamFlangeThickness, double compressionWeldLeg, double endPlateThickness,
            double endPlateProjectionBeyondFlange, string detailSource)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            ConnectionValidation.Positive(beamFlangeThickness, nameof(beamFlangeThickness));
            ConnectionValidation.Positive(compressionWeldLeg, nameof(compressionWeldLeg));
            ConnectionValidation.Positive(endPlateThickness, nameof(endPlateThickness));
            ConnectionValidation.Nonnegative(endPlateProjectionBeyondFlange, nameof(endPlateProjectionBeyondFlange));
            if (endPlateProjectionBeyondFlange < compressionWeldLeg)
                throw new ArgumentException("The end plate must include the full compression-flange weld leg.");
            if (string.IsNullOrWhiteSpace(detailSource)) throw new ArgumentException("The physical detail source is required.", nameof(detailSource));
            double sp = endPlateThickness + Math.Min(endPlateThickness, endPlateProjectionBeyondFlange - compressionWeldLeg);
            return new ColumnWebEffectiveWidth(beamFlangeThickness + 2 * compressionWeldLeg
                + 5 * (column.FlangeThickness + column.RootRadius) + sp,
                "EN1993-1-8:2005 6.2.6.2(1), (6.11); 45-degree end-plate dispersion; " + detailSource,
                column, new[] { new SectionResistanceValue("EndPlate.BeamFlangeThickness", beamFlangeThickness, "mm"),
                    new SectionResistanceValue("EndPlate.CompressionWeldLeg", compressionWeldLeg, "mm"),
                    new SectionResistanceValue("EndPlate.Thickness", endPlateThickness, "mm"),
                    new SectionResistanceValue("EndPlate.ProjectionBeyondFlange", endPlateProjectionBeyondFlange, "mm"),
                    new SectionResistanceValue("EndPlate.ColumnFlangeThickness", column.FlangeThickness, "mm"),
                    new SectionResistanceValue("EndPlate.ColumnRootRadius", column.RootRadius, "mm"),
                    new SectionResistanceValue("EndPlate.Sp", sp, "mm") });
        }
    }

    public enum ColumnWebPanelConfiguration { SingleSidedJoint, DoubleSidedSimilarBeamDepths, DoubleSidedUnequalBeamDepths }

    internal sealed class ColumnWebPanelInput
    {
        public ColumnWebPolicy Policy { get; }
        public ConnectionRolledISection Column { get; }
        public double ShearForce { get; }
        public ColumnWebPanelConfiguration Configuration { get; }
        public string PanelEquilibriumSource { get; }
        public ColumnWebPanelInput(ColumnWebPolicy policy, ConnectionRolledISection column, double shearForce,
            ColumnWebPanelConfiguration configuration, string panelEquilibriumSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            ConnectionValidation.Nonnegative(shearForce, nameof(shearForce));
            if (!Enum.IsDefined(typeof(ColumnWebPanelConfiguration), configuration)) throw new ArgumentOutOfRangeException(nameof(configuration));
            ShearForce = shearForce; Configuration = configuration; PanelEquilibriumSource = panelEquilibriumSource;
        }
    }

    /// <summary>Transverse web tension under one concomitant row/group action.
    /// Beta is the independently determined EN5.3 transformation parameter, 0..2;
    /// it is not computed from isolated beam moments by this component.</summary>
    internal sealed class ColumnWebTensionInput
    {
        public ColumnWebPolicy Policy { get; }
        public ConnectionRolledISection Column { get; }
        public ColumnWebEffectiveWidth EffectiveWidth { get; }
        public double TensionForce { get; }
        public double Beta { get; }
        public string TransformationSource { get; }
        public string ActionCombinationSource { get; }
        public ColumnWebTensionInput(ColumnWebPolicy policy, ConnectionRolledISection column,
            ColumnWebEffectiveWidth effectiveWidth, double tensionForce, double beta,
            string transformationSource, string actionCombinationSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            EffectiveWidth = effectiveWidth ?? throw new ArgumentNullException(nameof(effectiveWidth));
            ConnectionValidation.Nonnegative(tensionForce, nameof(tensionForce));
            ConnectionValidation.Nonnegative(beta, nameof(beta));
            if (beta > 2) throw new ArgumentOutOfRangeException(nameof(beta));
            TensionForce = tensionForce; Beta = beta; TransformationSource = transformationSource;
            ActionCombinationSource = actionCombinationSource;
        }
    }

    /// <summary>Transverse compression of an unstiffened rolled web. LongitudinalCompressionStress
    /// is the maximum coexisting compressive stress from column N and M at the web root, MPa;
    /// positive compression, zero only if justified by the supplied stress source. Unequal
    /// stress across the web is represented by its maximum, not its average. No plastic
    /// redistribution of this longitudinal stress or column-sway restraint is inferred.</summary>
    internal sealed class ColumnWebCompressionInput
    {
        public ColumnWebPolicy Policy { get; }
        public ConnectionRolledISection Column { get; }
        public ColumnWebEffectiveWidth EffectiveWidth { get; }
        public double CompressionForce { get; }
        public double Beta { get; }
        public double LongitudinalCompressionStress { get; }
        public string TransformationSource { get; }
        public string LongitudinalStressSource { get; }
        public string ColumnSwayRestraintSource { get; }
        public string ActionCombinationSource { get; }
        public ColumnWebCompressionInput(ColumnWebPolicy policy, ConnectionRolledISection column,
            ColumnWebEffectiveWidth effectiveWidth, double compressionForce, double beta,
            double longitudinalCompressionStress, string transformationSource, string longitudinalStressSource,
            string columnSwayRestraintSource, string actionCombinationSource)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            EffectiveWidth = effectiveWidth ?? throw new ArgumentNullException(nameof(effectiveWidth));
            ConnectionValidation.Nonnegative(compressionForce, nameof(compressionForce));
            ConnectionValidation.Nonnegative(beta, nameof(beta));
            ConnectionValidation.Nonnegative(longitudinalCompressionStress, nameof(longitudinalCompressionStress));
            if (beta > 2) throw new ArgumentOutOfRangeException(nameof(beta));
            CompressionForce = compressionForce; Beta = beta; LongitudinalCompressionStress = longitudinalCompressionStress;
            TransformationSource = transformationSource; LongitudinalStressSource = longitudinalStressSource;
            ColumnSwayRestraintSource = columnSwayRestraintSource; ActionCombinationSource = actionCombinationSource;
        }
    }

    internal static class ColumnWebComponents
    {
        public static ConnectionVerification<ColumnWebPanelInput> VerifyPanelShear(ColumnWebPanelInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            double av = Prepare(p.Policy, p.Column, true, trace, issues);
            Required(p.PanelEquilibriumSource, "PanelEquilibriumSourceRequired", issues);
            if (p.Configuration == ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths)
                issues.Add("UnequalBeamDepthsRequireDifferentPanelModel");
            double resistance = .9 * SectionResistanceArithmetic.Shear(av, p.Column.Fy, p.Policy.Code.Plates.GammaM0);
            Check(checks, issues, "ColumnWeb.PanelShear", p.ShearForce, resistance, Reference(p.Policy, "6.2.6.1(1-2), (6.7)"));
            return new ConnectionVerification<ColumnWebPanelInput>(p, p.Policy.Code, "Steel.Connection.ColumnWeb.PanelShear",
                "Plastic shear of one unstiffened rolled I/H column web panel; single-sided or double-sided similar-depth beams. "
                + "The demand is actual panel shear from joint equilibrium, not automatically the beam end shear.", checks, trace, issues, Assumptions());
        }

        public static ConnectionVerification<ColumnWebTensionInput> VerifyTransverseTension(ColumnWebTensionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            double av = Prepare(p.Policy, p.Column, p.Beta > 0, trace, issues);
            WidthDerivation(p.EffectiveWidth, p.Column, false, trace, issues);
            Required(p.EffectiveWidth.Source, "EffectiveWidthDerivationRequired", issues);
            Required(p.TransformationSource, "TransformationDerivationRequired", issues);
            Required(p.ActionCombinationSource, "ConcomitantActionSourceRequired", issues);
            double omega = Interaction(p.EffectiveWidth.Width, p.Column.WebThickness, av, p.Beta, trace, issues);
            double resistance = omega * p.EffectiveWidth.Width * p.Column.WebThickness * p.Column.Fy / p.Policy.Code.Plates.GammaM0;
            Check(checks, issues, "ColumnWeb.TransverseTension", p.TensionForce, resistance, Reference(p.Policy, "6.2.6.3, (6.15), Table6.3"));
            return new ConnectionVerification<ColumnWebTensionInput>(p, p.Policy.Code, "Steel.Connection.ColumnWeb.TransverseTension",
                "Unstiffened rolled I/H web transverse tension for one explicitly derived effective row/group width. "
                + "No flange bending, bolt, weld, column member or complete-joint qualification.", checks, trace, issues,
                Assumptions(p.EffectiveWidth.Source, p.TransformationSource));
        }

        public static ConnectionVerification<ColumnWebCompressionInput> VerifyTransverseCompression(ColumnWebCompressionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            double av = Prepare(p.Policy, p.Column, p.Beta > 0, trace, issues);
            WidthDerivation(p.EffectiveWidth, p.Column, true, trace, issues);
            Required(p.EffectiveWidth.Source, "EffectiveWidthDerivationRequired", issues);
            Required(p.TransformationSource, "TransformationDerivationRequired", issues);
            Required(p.ActionCombinationSource, "ConcomitantActionSourceRequired", issues);
            Required(p.LongitudinalStressSource, "LongitudinalStressDerivationRequired", issues);
            Required(p.ColumnSwayRestraintSource, "ColumnSwayRestraintRequired", issues);
            if (p.LongitudinalCompressionStress > p.Column.Fy) issues.Add("LongitudinalStressAboveYieldOutsideQualifiedRange");
            double omega = Interaction(p.EffectiveWidth.Width, p.Column.WebThickness, av, p.Beta, trace, issues);
            double stressRatio = p.LongitudinalCompressionStress / p.Column.Fy;
            double kw = stressRatio <= .7 ? 1 : 1.7 - stressRatio;
            double lambda = .932 * Math.Sqrt(p.EffectiveWidth.Width * p.Column.ClearWeb * p.Column.Fy
                / (p.Policy.ElasticModulus * p.Column.WebThickness * p.Column.WebThickness));
            // Rho is a reduction. Clamp the small >1 interval due to the rounded 0.72 threshold.
            double rho = lambda <= .72 ? 1 : Math.Min(1, (lambda - .2) / (lambda * lambda));
            double baseForce = omega * kw * p.EffectiveWidth.Width * p.Column.WebThickness * p.Column.Fy;
            Value(trace, issues, "LongitudinalCompressionStress", p.LongitudinalCompressionStress, "MPa");
            Value(trace, issues, "LongitudinalStressRatio", stressRatio, "1"); Value(trace, issues, "Kw", kw, "1");
            Value(trace, issues, "PlateSlenderness", lambda, "1"); Value(trace, issues, "Rho", rho, "1");
            Check(checks, issues, "ColumnWeb.CompressionYielding", p.CompressionForce, baseForce / p.Policy.Code.Plates.GammaM0,
                Reference(p.Policy, "6.2.6.2(1-3), (6.9-6.14), Table6.3"));
            Check(checks, issues, "ColumnWeb.CompressionBuckling", p.CompressionForce, rho * baseForce / p.Policy.GammaM1,
                Reference(p.Policy, "6.2.6.2(1-3), (6.9-6.14), Table6.3"));
            return new ConnectionVerification<ColumnWebCompressionInput>(p, p.Policy.Code, "Steel.Connection.ColumnWeb.TransverseCompression",
                "Unstiffened rolled I/H web transverse compression: local yielding and plate buckling, coexisting longitudinal elastic compression and shear interaction. "
                + "Column-sway restraint is externally qualified; no complete-column or joint qualification.", checks, trace, issues,
                Assumptions(p.EffectiveWidth.Source, p.TransformationSource, p.LongitudinalStressSource, p.ColumnSwayRestraintSource));
        }

        private static double Prepare(ColumnWebPolicy policy, ConnectionRolledISection column,
            bool shearInteraction, IList<SectionResistanceValue> trace, IList<string> issues)
        {
            if (column.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            double av = SectionPropertyArithmetic.RolledWebArea(column.Area, column.Width, column.FlangeThickness,
                column.Width, column.FlangeThickness, column.WebThickness, 2 * column.RootRadius);
            double epsilon = Math.Sqrt(235 / column.Fy), slenderness = column.ClearWeb / column.WebThickness;
            if (!(av > 0) || double.IsInfinity(av)) issues.Add("ColumnShearAreaOutsideNumericRange");
            if (shearInteraction && slenderness > 69 * epsilon) issues.Add("ColumnWebPanelSlendernessAbove69Epsilon");
            Value(trace, issues, "ColumnShearArea", av, "mm2"); Value(trace, issues, "ColumnClearWeb", column.ClearWeb, "mm");
            Value(trace, issues, "ColumnWebSlenderness", slenderness, "1"); Value(trace, issues, "Epsilon", epsilon, "1");
            Value(trace, issues, "GammaM0", policy.Code.Plates.GammaM0, "1"); Value(trace, issues, "GammaM1", policy.GammaM1, "1");
            return av;
        }
        private static double Interaction(double width, double thickness, double av, double beta,
            IList<SectionResistanceValue> trace, IList<string> issues)
        {
            double x = width * thickness / av;
            double omega1 = 1 / Math.Sqrt(1 + 1.3 * x * x), omega2 = 1 / Math.Sqrt(1 + 5.2 * x * x);
            double omega = beta <= .5 ? 1 : beta < 1 ? omega1 + 2 * (1 - beta) * (1 - omega1)
                : omega1 + (beta - 1) * (omega2 - omega1);
            Value(trace, issues, "EffectiveWidth", width, "mm"); Value(trace, issues, "Beta", beta, "1");
            Value(trace, issues, "Omega1", omega1, "1"); Value(trace, issues, "Omega2", omega2, "1");
            Value(trace, issues, "Omega", omega, "1");
            if (!(omega > 0)) issues.Add("ShearInteractionOutsideNumericRange");
            return omega;
        }
        private static void WidthDerivation(ColumnWebEffectiveWidth width, ConnectionRolledISection column,
            bool compression, IList<SectionResistanceValue> trace, IList<string> issues)
        {
            if (width.DerivedColumn != null && !ReferenceEquals(width.DerivedColumn, column))
                issues.Add("EffectiveWidthColumnSnapshotMismatch");
            if (width.DerivedColumn != null && !compression)
                issues.Add("CompressionSpreadCannotRepresentTensionTStub");
            foreach (var value in width.DerivationTrace) trace.Add(value);
        }
        private static string Reference(ColumnWebPolicy policy, string clauses) => "EN1993-1-8:2005 " + clauses
            + (policy.Code.IsNtc ? "; supplementary component model with NTC2018 gammaM0/gammaM1; not a literal national component equation" : "");
        private static void Required(string source, string message, IList<string> issues)
        { if (string.IsNullOrWhiteSpace(source)) issues.Add(message); }
        private static void Value(IList<SectionResistanceValue> trace, IList<string> issues, string name, double value, string unit)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
        private static void Check(IList<ConnectionCheck> checks, IList<string> issues, string id, double force, double resistance, string reference)
        { if (!(resistance > 0) || double.IsInfinity(resistance)) issues.Add(id + "OutsideNumericRange"); else checks.Add(new ConnectionCheck(id, force, resistance, reference)); }
        private static string[] Assumptions(params string[] derivations) => new[] {
            "All force/stress inputs are concomitant at the same ultimate limit state; no favourable omission of existing column stress is inferred.",
            "Rolled, doubly symmetric I/H column without web reinforcing plates, transverse/diagonal stiffeners, notches or interfering loads.",
            "Effective load spread is not truncated by a free column end; neighbouring loaded zones and their combined widths require a parent-joint/group check.",
            "The beta transformation and any required similar beam depths are externally established; no unchecked beta=0 default is supplied.",
            "Shear area is derived from the rolled dimensions and catalogue area using the shared section-property kernel; E=210000 MPa.",
            "When beta>0 the web-panel shear slenderness limit is also required. A beta=0 transverse component does not qualify the shear panel.",
            "NTC uses its own resistance factors with the explicitly supplementary EN component model.",
            "Component derivations: " + string.Join("; ", derivations) };
    }
}
