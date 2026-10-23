using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    public enum JointResistanceEvidenceScope { CompleteJoint, ComponentSubset }
    public enum EndPlateGoverningMechanism
    {
        ColumnWebPanelShear, ColumnWebCompression, ColumnWebTension, ColumnFlangeBending,
        EndPlateBending, BoltShear, BoltTension, BeamCompression, BeamWebTension,
        WeldFailure, PlateFracture, Other
    }
    public enum JointNominalSteelGrade { S235, S275, S355, S460, Other }

    /// <summary>Externally established design resistances, N and Nmm, for ONE bolted
    /// beam-to-column-flange end-plate detail, bending axis/direction and concomitant action case.
    /// This records caller-supplied evidence, not an independent certification. CompleteJoint
    /// means that ALL failure mechanisms were assessed, including bolts, welds, plates and
    /// members; a component subset is deliberately not promoted to complete resistance.
    /// Plastic member resistances must be applicable to the actual sections and actions.
    /// GoverningMechanisms includes ALL co-governing mechanisms at the ultimate resistance,
    /// not checks ranked by utilization at zero load. Column.Fy applies to its WEB; flange
    /// Fy is separate for product/thickness dependence. The connected member is the beam.</summary>
    public sealed class EndPlateJointResistanceEvidence
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Column { get; }
        public ConnectionPlate EndPlate { get; }
        public ConnectionBolt Bolt { get; }
        public double ColumnFlangeFy { get; }
        public double JointMomentResistance { get; }
        public double BeamPlasticMomentResistance { get; }
        public double ColumnPlasticMomentResistance { get; }
        public double BeamPlasticAxialResistance { get; }
        public double AxialForce { get; }
        public JointResistanceEvidenceScope Scope { get; }
        public IReadOnlyList<EndPlateGoverningMechanism> GoverningMechanisms { get; }
        public string DetailAndDirectionSource { get; }
        public string ResistanceAndActionSource { get; }
        public string GoverningMechanismsSource { get; }

        public EndPlateJointResistanceEvidence(ConnectionDesignCode code, ConnectionRolledISection column,
            ConnectionPlate endPlate, ConnectionBolt bolt, double columnFlangeFy,
            double jointMomentResistance, double beamPlasticMomentResistance,
            double columnPlasticMomentResistance, double beamPlasticAxialResistance, double axialForce,
            JointResistanceEvidenceScope scope, IEnumerable<EndPlateGoverningMechanism> governingMechanisms,
            string detailAndDirectionSource, string resistanceAndActionSource, string governingMechanismsSource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            EndPlate = endPlate ?? throw new ArgumentNullException(nameof(endPlate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            ConnectionValidation.Positive(columnFlangeFy, nameof(columnFlangeFy));
            ConnectionValidation.Nonnegative(jointMomentResistance, nameof(jointMomentResistance));
            ConnectionValidation.Positive(beamPlasticMomentResistance, nameof(beamPlasticMomentResistance));
            ConnectionValidation.Positive(columnPlasticMomentResistance, nameof(columnPlasticMomentResistance));
            ConnectionValidation.Positive(beamPlasticAxialResistance, nameof(beamPlasticAxialResistance));
            ConnectionNumbers.Finite(axialForce, nameof(axialForce));
            if (!Enum.IsDefined(typeof(JointResistanceEvidenceScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (governingMechanisms == null) throw new ArgumentNullException(nameof(governingMechanisms));
            var modes = governingMechanisms.Distinct().ToArray();
            if (modes.Any(m => !Enum.IsDefined(typeof(EndPlateGoverningMechanism), m)))
                throw new ArgumentOutOfRangeException(nameof(governingMechanisms));
            ColumnFlangeFy = columnFlangeFy; JointMomentResistance = jointMomentResistance;
            BeamPlasticMomentResistance = beamPlasticMomentResistance;
            ColumnPlasticMomentResistance = columnPlasticMomentResistance;
            BeamPlasticAxialResistance = beamPlasticAxialResistance; AxialForce = axialForce;
            Scope = scope; GoverningMechanisms = Array.AsReadOnly(modes);
            DetailAndDirectionSource = detailAndDirectionSource;
            ResistanceAndActionSource = resistanceAndActionSource;
            GoverningMechanismsSource = governingMechanismsSource;
        }

        internal void ValidateResistanceEvidence(ICollection<string> issues)
        {
            if (Scope != JointResistanceEvidenceScope.CompleteJoint) issues.Add("CompleteJointResistanceEvidenceRequired");
            if (string.IsNullOrWhiteSpace(DetailAndDirectionSource)) issues.Add("DetailAndBendingDirectionEvidenceRequired");
            if (string.IsNullOrWhiteSpace(ResistanceAndActionSource)) issues.Add("ResistanceAndConcomitantActionEvidenceRequired");
        }
    }

    /// <summary>I in mm4 about the frame bending axis, length in mm. For a beam use
    /// centre-to-centre column spacing, for a column use the storey height (Figure5.4).</summary>
    public sealed class JointFrameMemberStiffness
    {
        public double SecondMoment { get; }
        public double Length { get; }
        public JointFrameMemberStiffness(double secondMoment, double length)
        {
            ConnectionValidation.Positive(secondMoment, nameof(secondMoment));
            ConnectionValidation.Positive(length, nameof(length));
            SecondMoment = secondMoment; Length = length;
        }
    }

    /// <summary>ALL beams at the top of one storey and ALL columns of that storey.
    /// Each K is the arithmetic mean of individual I/L, not a sum or global ratio.</summary>
    public sealed class JointFrameStoreyStiffness
    {
        public IReadOnlyList<JointFrameMemberStiffness> BeamsAtTop { get; }
        public IReadOnlyList<JointFrameMemberStiffness> Columns { get; }
        public string StoreySource { get; }
        public JointFrameStoreyStiffness(IEnumerable<JointFrameMemberStiffness> beamsAtTop,
            IEnumerable<JointFrameMemberStiffness> columns, string storeySource)
        {
            BeamsAtTop = Copy(beamsAtTop, nameof(beamsAtTop));
            Columns = Copy(columns, nameof(columns)); StoreySource = storeySource;
        }
        private static IReadOnlyList<JointFrameMemberStiffness> Copy(IEnumerable<JointFrameMemberStiffness> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = values.ToArray();
            if (copy.Length == 0 || copy.Any(x => x == null)) throw new ArgumentException("Nonempty complete member collection required.", name);
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>Global evidence for the SAME frame and bending plane. Reduction is
    /// (displacement without bracing - displacement with bracing)/displacement without
    /// bracing under the same lateral action. A value >=0.8 permits the Figure5.4 kb=8
    /// branch. Other frames require every storey, not only the connected node's storey.</summary>
    public sealed class JointFrameClassificationContext
    {
        public double BracingDisplacementReduction { get; }
        public IReadOnlyList<JointFrameStoreyStiffness> Storeys { get; }
        public bool IncludesEveryStorey { get; }
        public string GlobalAnalysisSource { get; }
        public JointFrameClassificationContext(double bracingDisplacementReduction,
            IEnumerable<JointFrameStoreyStiffness> storeys, bool includesEveryStorey, string globalAnalysisSource)
        {
            ConnectionValidation.Nonnegative(bracingDisplacementReduction, nameof(bracingDisplacementReduction));
            if (bracingDisplacementReduction > 1) throw new ArgumentOutOfRangeException(nameof(bracingDisplacementReduction));
            if (storeys == null) throw new ArgumentNullException(nameof(storeys));
            var copy = storeys.ToArray();
            if (copy.Any(s => s == null)) throw new ArgumentException("Null storey.", nameof(storeys));
            BracingDisplacementReduction = bracingDisplacementReduction;
            Storeys = Array.AsReadOnly(copy); IncludesEveryStorey = includesEveryStorey;
            GlobalAnalysisSource = globalAnalysisSource;
        }
    }
}
