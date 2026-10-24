using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Geometry of the initial elastic component model at negligible axial load.
    /// This geometric calculation does not assess an action state; the bound adapter enforces N=0.
    /// No moment resistance, imposed action, secant law or rotation capacity is implied.
    /// All steel springs use E210000MPa; effective lengths are for EACH row in all groups.</summary>
    public sealed class EndPlateInitialStiffnessInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Column { get; }
        public ColumnWebEffectiveWidth CompressionWidth { get; }
        public ColumnWebPanelConfiguration Configuration { get; }
        public double Beta { get; }
        public IReadOnlyList<EndPlateStiffnessRow> Rows { get; }
        public string TransformationSource { get; }
        public string JointGeometrySource { get; }
        public EndPlateInitialStiffnessInput(ConnectionDesignCode code, ConnectionRolledISection column,
            ColumnWebEffectiveWidth compressionWidth, ColumnWebPanelConfiguration configuration, double beta,
            IEnumerable<EndPlateStiffnessRow> rows, string transformationSource, string jointGeometrySource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            CompressionWidth = compressionWidth ?? throw new ArgumentNullException(nameof(compressionWidth));
            if (!Enum.IsDefined(typeof(ColumnWebPanelConfiguration), configuration)) throw new ArgumentOutOfRangeException(nameof(configuration));
            ConnectionValidation.Nonnegative(beta, nameof(beta));
            if (beta > 2) throw new ArgumentOutOfRangeException(nameof(beta));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var copy = rows.ToArray();
            if (copy.Length < 1 || copy.Length > 20 || copy.Any(r => r == null)) throw new ArgumentException("One to twenty non-null rows required.", nameof(rows));
            Configuration = configuration; Beta = beta; Rows = Array.AsReadOnly(copy);
            TransformationSource = transformationSource; JointGeometrySource = jointGeometrySource;
        }
    }

    public sealed class EndPlateInitialStiffnessResult
    {
        public EndPlateInitialStiffnessInput Input { get; }
        public bool IsSupported => Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public double? InitialStiffness { get; }
        public double? EquivalentLeverArm { get; }
        public double? EquivalentRowCoefficient { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public string Reference => "EN1993-1-8:2005 6.3.1, Tables6.8/6.10/6.11, 6.3.3.1; supplementary elastic component model under the explicit NTC2018 policy";
        public string Scope => "Initial elastic component stiffness only, E210000MPa and negligible axial load. No secant stiffness, rotation, joint strength, ductility or global-model qualification.";
        internal EndPlateInitialStiffnessResult(EndPlateInitialStiffnessInput input, double initial, double z,
            double k, IEnumerable<string> diagnostics, IEnumerable<SectionResistanceValue> trace)
        {
            Input = input; Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            Trace = Array.AsReadOnly(trace.ToArray());
            if (IsSupported) { InitialStiffness = initial; EquivalentLeverArm = z; EquivalentRowCoefficient = k; }
        }
    }

    public static class EndPlateInitialStiffness
    {
        public static EndPlateInitialStiffnessResult Calculate(EndPlateInitialStiffnessInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            double initial = 0, z = 0, keq = 0;
            Required(p.JointGeometrySource, "JointGeometrySourceRequired");
            Required(p.TransformationSource, "TransformationSourceRequired");
            Required(p.CompressionWidth.Source, "CompressionWidthSourceRequired");
            if (p.CompressionWidth.DerivedColumn != null && !ReferenceEquals(p.CompressionWidth.DerivedColumn, p.Column))
                issues.Add("CompressionWidthDerivedForAnotherColumn");
            if (p.Configuration == ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths)
                issues.Add("UnequalBeamDepthsOutsidePanelModel");
            if (p.Configuration == ColumnWebPanelConfiguration.SingleSidedJoint && p.Beta != 1)
                issues.Add("SingleSidedJointRequiresBetaOneInThisField");
            if (p.Column.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            EndPlateStiffnessArithmetic.Column(p.Column, p.CompressionWidth, out double av, out double k2);
            Positive("ColumnShearArea", av, "mm2"); Positive("K2", k2, "mm");
            Value("Beta", p.Beta, "1"); Value("ElasticModulus", EndPlateStiffnessArithmetic.ElasticModulus, "MPa");
            double sumKh = 0, sumKh2 = 0;
            for (int i = 0; i < p.Rows.Count; i++)
            {
                var r = p.Rows[i]; string id = "Row" + (i + 1) + ".";
                Required(r.GeometrySource, id + "GeometrySourceRequired");
                Required(r.ColumnLength.DerivationSource, id + "ColumnRowAndGroupLengthsRequired");
                Required(r.EndPlateLength.DerivationSource, id + "EndPlateRowAndGroupLengthsRequired");
                if (i > 0 && r.LeverArm >= p.Rows[i - 1].LeverArm) issues.Add("RowsMustDecreaseInLeverArm");
                if (r.BoltElongationLength <= p.Column.FlangeThickness + r.EndPlateThickness)
                    issues.Add(id + "BoltLengthMustIncludePlatesAndAdditionalHardware");
                if (r.EndPlateThickness != p.Rows[0].EndPlateThickness) issues.Add("OneEndPlateMustHaveOneThickness");
                EndPlateStiffnessArithmetic.Row(p.Column, r, out double k3, out double k4, out double k5,
                    out double k10, out double flexibility, out double effective);
                Positive(id + "K3", k3, "mm"); Positive(id + "K4", k4, "mm");
                Positive(id + "K5", k5, "mm"); Positive(id + "K10", k10, "mm");
                Positive(id + "Keff", effective, "mm"); Positive(id + "Flexibility", flexibility, "1/mm");
                Value(id + "ColumnLength", r.ColumnLength.Minimum, "mm");
                Value(id + "EndPlateLength", r.EndPlateLength.Minimum, "mm");
                Value(id + "LeverArm", r.LeverArm, "mm");
                sumKh += effective * r.LeverArm; sumKh2 += effective * r.LeverArm * r.LeverArm;
            }
            Positive("SumKh", sumKh, "mm2"); Positive("SumKh2", sumKh2, "mm3");
            if (issues.Count != 0) return Result();
            EndPlateStiffnessArithmetic.Assemble(p.Beta, av, k2, sumKh, sumKh2,
                out z, out keq, out double panelFlexibility, out double totalFlexibility);
            initial = EndPlateStiffnessArithmetic.Initial(z, totalFlexibility);
            Positive("EquivalentLeverArm", z, "mm"); Positive("EquivalentRowCoefficient", keq, "mm");
            Value("PanelFlexibility", panelFlexibility, "1/mm");
            if (p.Beta > 0) Positive("K1", 1 / panelFlexibility, "mm");
            Positive("TotalFlexibility", totalFlexibility, "1/mm"); Positive("InitialStiffness", initial, "Nmm/rad");
            return Result();

            void Required(string source, string diagnostic)
            { if (string.IsNullOrWhiteSpace(source)) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) issues.Add(name + "OutsideNumericRange");
                else trace.Add(new SectionResistanceValue(name, value, unit));
            }
            void Positive(string name, double value, string unit)
            { if (!(value > 0)) issues.Add(name + "OutsidePositiveRange"); Value(name, value, unit); }
            EndPlateInitialStiffnessResult Result() => new EndPlateInitialStiffnessResult(p, initial, z, keq, issues, trace);
        }
    }
}
