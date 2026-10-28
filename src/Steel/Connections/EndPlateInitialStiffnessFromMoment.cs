using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class EndPlateBoltElongationDetail
    {
        public int MomentRowIndex { get; }
        public double Length { get; }
        public string Source { get; }
        public EndPlateBoltElongationDetail(int momentRowIndex,double length,string source)
        {
            if(momentRowIndex<0||momentRowIndex>19)throw new ArgumentOutOfRangeException(nameof(momentRowIndex));
            ConnectionValidation.Positive(length,nameof(length));
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Actual grip/washer/head/nut dimensions required.");
            MomentRowIndex=momentRowIndex;Length=length;Source=source;
        }
    }

    /// <summary>External detail for ONE physical tension row. Effective lengths must include
    /// that row acting individually and in every relevant group, never a group total/average.
    /// Bolt elongation length includes grip, actual washers and half head/nut heights.</summary>
    public sealed class EndPlateStiffnessRowDetail
    {
        public int MomentRowIndex { get; }
        public JointStiffnessEffectiveLength ColumnLength { get; }
        public JointStiffnessEffectiveLength EndPlateLength { get; }
        public double BoltElongationLength { get; }
        public string HardwareSource { get; }
        public EndPlateStiffnessRowDetail(int momentRowIndex, JointStiffnessEffectiveLength columnLength,
            JointStiffnessEffectiveLength endPlateLength, double boltElongationLength, string hardwareSource)
        {
            if (momentRowIndex < 0 || momentRowIndex > 19) throw new ArgumentOutOfRangeException(nameof(momentRowIndex));
            ConnectionValidation.Positive(boltElongationLength, nameof(boltElongationLength));
            ColumnLength = columnLength ?? throw new ArgumentNullException(nameof(columnLength));
            EndPlateLength = endPlateLength ?? throw new ArgumentNullException(nameof(endPlateLength));
            MomentRowIndex = momentRowIndex; BoltElongationLength = boltElongationLength; HardwareSource = hardwareSource;
        }
    }

    public sealed class EndPlateInitialStiffnessFromMomentInput
    {
        public EndPlateMomentWithBeamSectionResult Parent { get; }
        public IReadOnlyList<EndPlateStiffnessRowDetail> Rows { get; }
        public bool LengthsDerivedFromGeometry { get; internal set; }
        public EndPlateInitialStiffnessFromMomentInput(EndPlateMomentWithBeamSectionResult parent,
            IEnumerable<EndPlateStiffnessRowDetail> rows)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var copy = rows.ToArray();
            if (copy.Length < 1 || copy.Length > 20 || copy.Any(r => r == null)) throw new ArgumentException("One to twenty physical row details required.", nameof(rows));
            Rows = Array.AsReadOnly(copy);
        }
    }

    public sealed class EndPlateInitialStiffnessFromMomentResult
    {
        public EndPlateInitialStiffnessFromMomentInput Input { get; }
        public EndPlateMomentWithBeamSectionResult Parent => Input.Parent;
        public EndPlateMomentComponentResult MomentComponents => Parent.MomentComponents;
        public EndPlateInitialStiffnessResult InitialModel { get; }
        public bool IsSupported => Diagnostics.Count == 0 && InitialModel != null && InitialModel.IsSupported;
        public ConnectionStatus Status => Parent.Status == ConnectionStatus.Failed ? ConnectionStatus.Failed
            : IsSupported ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => false;
        public double? InitialStiffness => IsSupported ? InitialModel.InitialStiffness : null;
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> PendingChecks { get; }
        internal EndPlateInitialStiffnessFromMomentResult(EndPlateInitialStiffnessFromMomentInput input,
            EndPlateInitialStiffnessResult initial, IEnumerable<string> diagnostics, IEnumerable<SectionResistanceValue> trace)
        {
            Input = input; InitialModel = initial; Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            Trace = Array.AsReadOnly(trace.ToArray());
            PendingChecks = Array.AsReadOnly(new[] {
                input.LengthsDerivedFromGeometry
                    ? "Effective lengths derived for EACH row individually and in all adjacent groups within the qualified Tables6.4/6.6 geometry; chart alpha and physical detail assumptions remain supplied qualifications"
                    : "The supplied effective lengths require qualification for EACH row in every relevant group; source text is provenance, not derivation or certification",
                "Actual bolt grip/washer/head/nut elongation lengths require the supplied hardware qualification",
                "Column and plate springs use the declared carbon-steel E210000MPa policy; only the available Model beam material modulus is independently bound",
                "Secant stiffness and rotation are not evaluated: the parent moment resistance covers components, not the complete joint Mj,Rd",
                "Plate/support net and block failure, local force introduction, member stability, rotation capacity and global-model compatibility remain outside this initial stiffness adapter" });
        }
    }

    /// <summary>Binds initial stiffness to the actual homogeneous Model beam case and its
    /// moment geometry. All parent tension rows are retained; no artificial MjRd or zero load.</summary>
    internal static class EndPlateInitialStiffnessFromMoment
    {
        /// <summary>Derive every row's stiffness lengths from the SAME parent geometry.
        /// Hardware elongation lengths and the geometry's chart-alpha qualification
        /// remain explicit. No group-total averaging and no complete-joint MjRd.</summary>
        public static EndPlateInitialStiffnessFromMomentInput CreateInputFromGeometry(EndPlateMomentWithBeamSectionResult parent,
            IEnumerable<EndPlateBoltElongationDetail> hardware)
        {
            if(parent==null)throw new ArgumentNullException(nameof(parent));
            if(hardware==null)throw new ArgumentNullException(nameof(hardware));
            var details=hardware.ToArray(); var m=parent.Input;
            DeriveGeometry(m,out var column,out var plate,out var outer,out var coordinates);
            foreach(var geometry in new[]{column,plate,outer}.Where(x=>x!=null))
                if(!geometry.IsSupported)throw new NotSupportedException(string.Join("; ",geometry.Diagnostics));
            if(details.Length!=coordinates.Count||details.Any(x=>x==null))throw new ArgumentException("Actual hardware for every physical parent row required.");
            int offset=outer==null?0:1;
            var rows=new List<EndPlateStiffnessRowDetail>();
            for(int i=0;i<details.Length;i++) {
                if(details[i].MomentRowIndex!=i)throw new ArgumentException("Hardware must follow all physical parent rows exactly once.");
                var plateLength=offset==1&&i==0?outer.StiffnessLength(0):plate.StiffnessLength(i-offset);
                rows.Add(new EndPlateStiffnessRowDetail(i,column.StiffnessLength(i),plateLength,details[i].Length,details[i].Source));
            }
            return new EndPlateInitialStiffnessFromMomentInput(parent,rows){LengthsDerivedFromGeometry=true};
        }

        private static void DeriveGeometry(EndPlateMomentComponentInput m,out TStubDerivedGeometry columnGeometry,
            out TStubDerivedGeometry internalGeometry,out TStubDerivedGeometry outerGeometry,out List<double> coordinates)
        {
            var g=m.InternalGeometry;var column=m.Column;
            coordinates=new List<double>();
            if(m.ExtensionGeometry!=null)coordinates.Add(-m.ExtensionGeometry.FlangeToRow);
            coordinates.AddRange(g.OrderedInternalRows.Select(y=>y+m.Beam.FlangeThickness));
            columnGeometry=TStubEffectiveGeometry.UnstiffenedColumnFlange(new TStubUnstiffenedColumnGeometry(
                column.Width,column.WebThickness,column.RootRadius,m.EndPlate.Width,g.BoltGauge,coordinates,
                m.ColumnFirstEndContinuation,m.ColumnLastEndContinuation,g.DetailSource));
            internalGeometry=TStubInternalEndPlateLengths.Calculate(g);
            outerGeometry=m.ExtensionGeometry==null?null:TStubEffectiveGeometry.ExtendedEndPlateOuterRow(m.ExtensionGeometry);
        }

        public static EndPlateInitialStiffnessFromMomentResult Calculate(EndPlateInitialStiffnessFromMomentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var parent = p.Parent; var m = parent.Input; var b = parent.BeamSection.Input;
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            EndPlateInitialStiffnessResult initial = null;
            issues.AddRange(parent.Diagnostics.Select(d => "Parent:" + d));
            if (parent.Status != ConnectionStatus.Passed || !parent.IsComplete)
                issues.Add("PassedSameCaseBeamAndMomentAssessmentRequired");
            if (b.Section.ElasticModulus != EndPlateStiffnessArithmetic.ElasticModulus)
                issues.Add("ModelBeamElasticModulusMustEqual210000MPa");
            if (m.Axial != 0) issues.Add("ZeroAxialParentRequiredForInitialStiffnessAdapter");
            if (!ReferenceEquals(m.BeamMomentResistance, parent.BeamSection.DerivedResistance)
                || !ReferenceEquals(m.Policy.Code, b.Code) || !ReferenceEquals(m.Beam, b.Beam))
                issues.Add("SameDerivedBeamTokenCodeAndBeamRequired");
            if (m.Moment != b.Actions.M1 || m.Shear != Math.Abs(b.Actions.V2) || m.Axial != b.Actions.N)
                issues.Add("SameConcomitantActionsRequired");
            Value("ModelBeamElasticModulus", b.Section.ElasticModulus, "MPa");
            string[] actionNames = { "N", "V1", "V2", "T", "M1", "M2" };
            double[] actionValues = { b.Actions.N, b.Actions.V1, b.Actions.V2, b.Actions.T, b.Actions.M1, b.Actions.M2 };
            for (int i = 0; i < actionNames.Length; i++) Value("Actions." + actionNames[i], actionValues[i], i < 3 ? "N" : "Nmm");
            if (issues.Count != 0) return Result();
            var effectiveRows = parent.MomentComponents.EffectiveRows;
            if (p.Rows.Count != effectiveRows.Count) issues.Add("EveryPhysicalMomentRowRequiresOneStiffnessDetail");
            for (int i = 0; i < p.Rows.Count; i++)
            {
                if (p.Rows[i].MomentRowIndex != i) issues.Add("StiffnessDetailsMustFollowEveryParentRowExactlyOnce");
                if (string.IsNullOrWhiteSpace(p.Rows[i].HardwareSource)) issues.Add("Row" + (i + 1) + ":HardwareElongationSourceRequired");
            }
            if (issues.Count != 0) return Result();
            var g = m.InternalGeometry; var column = m.Column;
            int offset = m.ExtensionGeometry == null ? 0 : 1;
            DeriveGeometry(m,out var columnGeometry,out var internalGeometry,out var outerGeometry,out var coordinates);
            if(p.LengthsDerivedFromGeometry)Value("RowLengthsDerivedFromGeometry",1,"1");
            Geometry("Column", columnGeometry); Geometry("InternalPlate", internalGeometry);
            if (outerGeometry != null) Geometry("OuterPlate", outerGeometry);
            if (issues.Count != 0) return Result();
            var rows = new List<EndPlateStiffnessRow>();
            for (int i = 0; i < p.Rows.Count; i++)
            {
                var detail = p.Rows[i]; var plateGeometry = offset == 1 && i == 0 ? outerGeometry : internalGeometry;
                int plateRow = offset == 1 && i == 0 ? 0 : i - offset;
                // Table6.11 k3/k4/k5 selects the smallest ROW length, individually or in groups
                // (Tables6.4/6.6). IndividualRows.Mode1 is min(cp,nc) of that ONE row, hence
                // an upper bound on the overall minimum, not a group-total/plastic-capacity test.
                // This does NOT derive missing group contributions or certify an external minimum.
                if (detail.ColumnLength.Minimum > columnGeometry.IndividualRows[i].Mode1)
                    issues.Add("Row" + (i + 1) + ":ColumnLengthExceedsIndividualMechanism");
                if (detail.EndPlateLength.Minimum > plateGeometry.IndividualRows[plateRow].Mode1)
                    issues.Add("Row" + (i + 1) + ":PlateLengthExceedsIndividualMechanism");
                rows.Add(new EndPlateStiffnessRow(effectiveRows[i].LeverArm, detail.ColumnLength, detail.EndPlateLength,
                    columnGeometry.M, plateGeometry.M, m.EndPlate.Thickness, m.Bolt.TensileArea,
                    detail.BoltElongationLength, g.DetailSource + "; " + detail.HardwareSource));
                Value("Row" + (i + 1) + ".Y", coordinates[i], "mm");
                Value("Row" + (i + 1) + ".ColumnM", columnGeometry.M, "mm");
                Value("Row" + (i + 1) + ".PlateM", plateGeometry.M, "mm");
                Value("Row" + (i + 1) + ".PlateThickness", m.EndPlate.Thickness, "mm");
                Value("Row" + (i + 1) + ".BoltStressArea", m.Bolt.TensileArea, "mm2");
            }
            if (issues.Count != 0) return Result();
            var width = ColumnWebEffectiveWidth.FromBoltedEndPlate(column, m.Beam.FlangeThickness,
                g.CompressionFlangeWeldLeg, m.EndPlate.Thickness, m.CompressionEndPlateProjection, g.DetailSource);
            var derived = new EndPlateInitialStiffnessInput(m.Policy.Code, column, width, m.PanelConfiguration,
                m.Beta, rows, m.TransformationSource, g.DetailSource);
            initial = EndPlateInitialStiffness.Calculate(derived);
            issues.AddRange(initial.Diagnostics.Select(d => "InitialModel:" + d));
            trace.AddRange(initial.Trace.Select(v => new SectionResistanceValue("InitialModel." + v.Name, v.Value, v.Unit)));
            return Result();

            void Value(string name, double value, string unit) => trace.Add(new SectionResistanceValue(name, value, unit));
            void Geometry(string prefix, TStubDerivedGeometry geometry)
            { issues.AddRange(geometry.Diagnostics.Select(d => prefix + ":" + d)); }
            EndPlateInitialStiffnessFromMomentResult Result() => new EndPlateInitialStiffnessFromMomentResult(p, initial, issues, trace);
        }
    }
}
