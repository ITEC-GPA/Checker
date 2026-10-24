using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Minimum effective length of ONE row, individually AND in every relevant
    /// group, EN1993-1-8 Table6.11. A group total is not a row length. Supplying these
    /// lengths requires a qualified yield-line model; resistance lengths cannot simply
    /// be reused without considering the contribution of this row in each group.</summary>
    public sealed class JointStiffnessEffectiveLength
    {
        public IReadOnlyList<double> RelevantRowLengths { get; }
        public double Minimum => RelevantRowLengths.Min();
        public string DerivationSource { get; }
        public JointStiffnessEffectiveLength(IEnumerable<double> relevantRowLengths, string derivationSource)
        {
            if (relevantRowLengths == null) throw new ArgumentNullException(nameof(relevantRowLengths));
            var values = relevantRowLengths.ToArray();
            if (values.Length == 0) throw new ArgumentException("Individual and relevant group row lengths are required.");
            foreach (var value in values) ConnectionValidation.Positive(value, nameof(relevantRowLengths));
            RelevantRowLengths = Array.AsReadOnly(values); DerivationSource = derivationSource;
        }
    }

    /// <summary>One tension row of TWO identical bolts. Distances/lengths in mm and
    /// bolt stress area in mm2. LeverArm is from the actual compression centre.
    /// EndPlateM is mx for an external row; internal rows use their actual m.
    /// BoltElongationLength includes grip, washers and half the head+nut heights.</summary>
    public sealed class EndPlateStiffnessRow
    {
        public double LeverArm { get; }
        public JointStiffnessEffectiveLength ColumnLength { get; }
        public JointStiffnessEffectiveLength EndPlateLength { get; }
        public double ColumnFlangeM { get; }
        public double EndPlateM { get; }
        public double EndPlateThickness { get; }
        public double BoltStressArea { get; }
        public double BoltElongationLength { get; }
        public string GeometrySource { get; }
        public EndPlateStiffnessRow(double leverArm, JointStiffnessEffectiveLength columnLength,
            JointStiffnessEffectiveLength endPlateLength, double columnFlangeM, double endPlateM,
            double endPlateThickness, double boltStressArea, double boltElongationLength, string geometrySource)
        {
            foreach (var value in new[] { leverArm, columnFlangeM, endPlateM, endPlateThickness,
                boltStressArea, boltElongationLength }) ConnectionValidation.Positive(value, nameof(value));
            ColumnLength = columnLength ?? throw new ArgumentNullException(nameof(columnLength));
            EndPlateLength = endPlateLength ?? throw new ArgumentNullException(nameof(endPlateLength));
            LeverArm = leverArm; ColumnFlangeM = columnFlangeM; EndPlateM = endPlateM;
            EndPlateThickness = endPlateThickness; BoltStressArea = boltStressArea;
            BoltElongationLength = boltElongationLength; GeometrySource = geometrySource;
        }
    }

    /// <summary>Component-method stiffness of an unstiffened rolled I/H column-flange
    /// end-plate joint. No supplementary plates. One active bending direction; rows must
    /// decrease in lever arm and include every relevant row, or omit only the rows nearer
    /// the compression centre (6.3.1(3)). N is signed; M is the nonnegative magnitude in
    /// this direction. NplRd and MjRd are separately qualified resistances, not calculated here.
    /// Beta is the EN5.3 transformation parameter from the same concomitant action state.</summary>
    public sealed class EndPlateRotationalStiffnessInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Column { get; }
        public ColumnWebEffectiveWidth CompressionWidth { get; }
        public ColumnWebPanelConfiguration Configuration { get; }
        public double Beta { get; }
        public IReadOnlyList<EndPlateStiffnessRow> Rows { get; }
        public double AxialForce { get; }
        public double BeamPlasticAxialResistance { get; }
        public double Moment { get; }
        public double JointMomentResistance { get; }
        public string TransformationSource { get; }
        public string ResistanceAndActionSource { get; }
        public string JointGeometrySource { get; }
        public EndPlateRotationalStiffnessInput(ConnectionDesignCode code, ConnectionRolledISection column,
            ColumnWebEffectiveWidth compressionWidth, ColumnWebPanelConfiguration configuration, double beta,
            IEnumerable<EndPlateStiffnessRow> rows, double axialForce, double beamPlasticAxialResistance,
            double moment, double jointMomentResistance, string transformationSource,
            string resistanceAndActionSource, string jointGeometrySource)
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
            ConnectionNumbers.Finite(axialForce, nameof(axialForce));
            ConnectionValidation.Positive(beamPlasticAxialResistance, nameof(beamPlasticAxialResistance));
            ConnectionValidation.Nonnegative(moment, nameof(moment));
            ConnectionValidation.Positive(jointMomentResistance, nameof(jointMomentResistance));
            Configuration = configuration; Beta = beta; Rows = Array.AsReadOnly(copy);
            AxialForce = axialForce; BeamPlasticAxialResistance = beamPlasticAxialResistance;
            Moment = moment; JointMomentResistance = jointMomentResistance;
            TransformationSource = transformationSource; ResistanceAndActionSource = resistanceAndActionSource;
            JointGeometrySource = jointGeometrySource;
        }
    }

    /// <summary>Stiffness calculation, never a whole-joint resistance/rotation qualification.
    /// Null outputs on unsupported geometry, missing evidence or numeric failure.</summary>
    public sealed class EndPlateRotationalStiffnessResult
    {
        public EndPlateRotationalStiffnessInput Input { get; }
        public bool IsSupported => Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public double? InitialStiffness { get; }
        public double? SecantStiffness { get; }
        public double? ModelRotation { get; }
        public double? EquivalentLeverArm { get; }
        public double? EquivalentRowCoefficient { get; }
        public double? StiffnessRatio { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public string Reference => "EN1993-1-8:2005 6.3.1, Tables6.8/6.10/6.11, 6.3.3.1; elastic component model also used with the explicit NTC2018 design policy";
        public string Scope => "Unstiffened column-flange bolted end-plate joint, initial/secant rotational stiffness in Nmm/rad; explicit qualified row/group lengths and compression centre. Not rotation capacity, global frame classification, member resistance, weld/bolt strength or complete-joint qualification.";
        internal EndPlateRotationalStiffnessResult(EndPlateRotationalStiffnessInput p, double initial,
            double secant, double rotation, double z, double k, double mu, List<string> diagnostics,
            List<SectionResistanceValue> trace)
        {
            Input = p; Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            Trace = Array.AsReadOnly(trace.ToArray());
            if (IsSupported)
            {
                InitialStiffness = initial; SecantStiffness = secant; ModelRotation = rotation;
                EquivalentLeverArm = z; EquivalentRowCoefficient = k; StiffnessRatio = mu;
            }
        }
    }

    public static class EndPlateRotationalStiffness
    {
        public static EndPlateRotationalStiffnessResult Calculate(EndPlateRotationalStiffnessInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            const double elasticModulus = EndPlateStiffnessArithmetic.ElasticModulus;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            double initial = 0, secant = 0, rotation = 0, z = 0, keq = 0, mu = 0;
            Required(p.JointGeometrySource, "JointGeometrySourceRequired");
            Required(p.TransformationSource, "TransformationSourceRequired");
            Required(p.ResistanceAndActionSource, "ResistanceAndConcomitantActionSourceRequired");
            Required(p.CompressionWidth.Source, "CompressionWidthSourceRequired");
            if (p.CompressionWidth.DerivedColumn != null && !ReferenceEquals(p.CompressionWidth.DerivedColumn, p.Column))
                issues.Add("CompressionWidthDerivedForAnotherColumn");
            if (p.Configuration == ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths)
                issues.Add("UnequalBeamDepthsOutsidePanelModel");
            if (p.Configuration == ColumnWebPanelConfiguration.SingleSidedJoint && p.Beta != 1)
                issues.Add("SingleSidedJointRequiresBetaOneInThisField");
            if (p.Column.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            if (Math.Abs(p.AxialForce) / p.BeamPlasticAxialResistance > .05)
                issues.Add("AxialForceExceedsFivePercentOfBeamPlasticResistance");
            if (p.Moment > p.JointMomentResistance) issues.Add("MomentExceedsQualifiedJointResistance");
            EndPlateStiffnessArithmetic.Column(p.Column, p.CompressionWidth, out double av, out double k2);
            Positive("ColumnShearArea", av, "mm2"); Positive("K2", k2, "mm");
            Value("Beta", p.Beta, "1"); Value("ElasticModulus", elasticModulus, "MPa");
            Value("AxialForce", p.AxialForce, "N"); Value("Moment", p.Moment, "Nmm");
            Value("BeamPlasticAxialResistance", p.BeamPlasticAxialResistance, "N");
            Value("JointMomentResistance", p.JointMomentResistance, "Nmm");
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
                if (r.EndPlateThickness != p.Rows[0].EndPlateThickness)
                    issues.Add("OneEndPlateMustHaveOneThickness");
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
            mu = p.Moment / p.JointMomentResistance <= 2d / 3 ? 1
                : Math.Pow(1.5 * (p.Moment / p.JointMomentResistance), 2.7);
            initial = EndPlateStiffnessArithmetic.Initial(z, totalFlexibility); secant = initial / mu;
            rotation = p.Moment / secant;
            Positive("EquivalentLeverArm", z, "mm"); Positive("EquivalentRowCoefficient", keq, "mm");
            Value("PanelFlexibility", panelFlexibility, "1/mm");
            if (p.Beta > 0) Positive("K1", 1 / panelFlexibility, "mm");
            Positive("TotalFlexibility", totalFlexibility, "1/mm"); Positive("Mu", mu, "1");
            Positive("InitialStiffness", initial, "Nmm/rad"); Positive("SecantStiffness", secant, "Nmm/rad");
            Value("ModelRotation", rotation, "rad");
            return Result();

            void Required(string source, string diagnostic)
            { if (string.IsNullOrWhiteSpace(source)) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            {
                if (!Finite(value)) issues.Add(name + "OutsideNumericRange");
                else trace.Add(new SectionResistanceValue(name, value, unit));
            }
            void Positive(string name, double value, string unit)
            { if (!(value > 0)) issues.Add(name + "OutsidePositiveRange"); Value(name, value, unit); }
            EndPlateRotationalStiffnessResult Result() => new EndPlateRotationalStiffnessResult(p,
                initial, secant, rotation, z, keq, mu, issues, trace);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
