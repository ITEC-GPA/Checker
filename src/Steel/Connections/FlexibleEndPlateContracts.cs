using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Partial-depth plate, two symmetric columns. N, mm, Nmm and MPa.
    /// Component X is transverse, Y upward, Z normal to the supporting column web.
    /// Actions are at plate centre; the initial model accepts downward Fy only.
    /// Fy/Fu of each I/H snapshot describe its web. ExistingSupportActions excludes
    /// the connections being assessed and must explicitly be zero in this local model.</summary>
    public sealed class FlexibleEndPlateInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate Plate { get; }
        public ConnectionBolt Bolt { get; }
        public ConnectionRolledISection Beam { get; }
        public ConnectionRolledISection Support { get; }
        public int Rows { get; }
        public double Gauge { get; }
        public double Height => 80 + (Rows - 1) * 70;
        public double Pitch => 70;
        public double PlateTopOffset => 50;
        public double SupportTopBoltEdge { get; }
        public double SupportBottomBoltEdge { get; }
        public double WeldThroat { get; }
        public FilletWeldEndCondition WeldEnds { get; }
        public string ConsumableCompatibilitySource { get; }
        public string PinnedModelQualificationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public bool BeamLaterallyRestrained { get; }
        public bool BeamNotched { get; }
        public PlanarConnectionActions Actions { get; }
        public PlanarConnectionActions ExistingSupportActions { get; }
        public FlexibleEndPlateInput(ConnectionDesignCode code, ConnectionPlate plate, ConnectionBolt bolt,
            ConnectionRolledISection beam, ConnectionRolledISection support, int rows, double gauge,
            double supportTopBoltEdge, double supportBottomBoltEdge, double weldThroat,
            FilletWeldEndCondition weldEnds, string consumableCompatibilitySource,
            PlanarConnectionActions actions, PlanarConnectionActions existingSupportActions,
            bool beamLaterallyRestrained, string pinnedModelQualificationSource,
            string holeClearanceServiceabilitySource = null, bool beamNotched = false)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Support = support ?? throw new ArgumentNullException(nameof(support));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ExistingSupportActions = existingSupportActions ?? throw new ArgumentNullException(nameof(existingSupportActions));
            if (rows < 1 || rows > 100) throw new ArgumentOutOfRangeException(nameof(rows));
            ConnectionValidation.Positive(gauge, nameof(gauge));
            ConnectionValidation.Positive(supportTopBoltEdge, nameof(supportTopBoltEdge));
            ConnectionValidation.Positive(supportBottomBoltEdge, nameof(supportBottomBoltEdge));
            ConnectionValidation.Positive(weldThroat, nameof(weldThroat));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), weldEnds)) throw new ArgumentOutOfRangeException(nameof(weldEnds));
            if (plate.Width <= gauge || gauge <= beam.WebThickness)
                throw new ArgumentException("The bolt columns must lie outside the web and inside the plate.");
            Rows = rows; Gauge = gauge; SupportTopBoltEdge = supportTopBoltEdge;
            SupportBottomBoltEdge = supportBottomBoltEdge; WeldThroat = weldThroat; WeldEnds = weldEnds;
            ConsumableCompatibilitySource = consumableCompatibilitySource;
            BeamLaterallyRestrained = beamLaterallyRestrained; BeamNotched = beamNotched;
            PinnedModelQualificationSource = pinnedModelQualificationSource;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    public sealed class FlexibleEndPlateResult
    {
        public ConnectionVerification<FlexibleEndPlateInput> Assessment { get; }
        public FlexibleEndPlateInput Input => Assessment.Input;
        public ConnectionStatus Status => Assessment.Status;
        public bool IsComplete => Assessment.IsComplete;
        public IReadOnlyList<ConnectionCheck> Checks => Assessment.Checks;
        public IReadOnlyList<SectionResistanceValue> Trace => Assessment.Trace;
        public IReadOnlyList<string> Diagnostics => Assessment.Diagnostics;
        public double? Utilization => Assessment.Utilization;
        public ConnectionCheck GoverningCheck => Assessment.GoverningCheck;
        public ElasticBoltGroupResult BoltDistribution { get; }
        public FilletWeldGroupResult WeldVerification { get; }
        public bool ConformsToSciPinnedDetail { get; }
        public string Scope => Assessment.Scope;
        public double? LoadUtilization => IsComplete ? (double?)ConnectionOutcome.LoadChecks(Checks).Max(c => c.Utilization) : null;
        /// <summary>Typed support-web bearing resistance per bolt behind the trace entry of the same name; null when not traced.</summary>
        internal double? SupportBearingPerBolt { get; }
        internal FlexibleEndPlateResult(ConnectionVerification<FlexibleEndPlateInput> assessment,
            ElasticBoltGroupResult bolts, FilletWeldGroupResult welds, bool conforms, double? supportBearingPerBolt = null)
        { Assessment = assessment; BoltDistribution = bolts; WeldVerification = welds; ConformsToSciPinnedDetail = conforms; SupportBearingPerBolt = supportBearingPerBolt; }
    }

    /// <summary>Two opposite faces of the SAME column web, shared through-bolts,
    /// top-aligned layouts with the same gauge and pitch. Downward reactions only.
    /// ExistingSupportActions in both inputs excludes these two new reactions.</summary>
    public sealed class FlexibleEndPlatePairInput
    {
        public FlexibleEndPlateInput First { get; }
        public FlexibleEndPlateInput Second { get; }
        public FlexibleEndPlatePairInput(FlexibleEndPlateInput first, FlexibleEndPlateInput second)
        {
            First = first ?? throw new ArgumentNullException(nameof(first));
            Second = second ?? throw new ArgumentNullException(nameof(second));
            if (ReferenceEquals(first, second)) throw new ArgumentException("Two distinct beam connection inputs are required.");
            if (!ReferenceEquals(first.Code, second.Code) || !ReferenceEquals(first.Support, second.Support)
                || !ReferenceEquals(first.Bolt, second.Bolt))
                throw new ArgumentException("Shared code, column and through-bolt snapshots are required.");
            if (first.Gauge != second.Gauge || first.SupportTopBoltEdge != second.SupportTopBoltEdge
                || Math.Abs((first.Rows - second.Rows) * first.Pitch + first.SupportBottomBoltEdge - second.SupportBottomBoltEdge) > 1e-8)
                throw new ArgumentException("The bolt rows and physical column ends must coincide in the common frame.");
        }
    }
}
