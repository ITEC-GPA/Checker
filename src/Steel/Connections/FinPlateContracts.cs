using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Joint axes: X follows the beam away from its support, Y is transverse, Z is upward.
    /// Factored actions at the support face at plate mid-height; N and Nmm, right-handed moments.
    /// Component bolt/weld coordinate frames are different and are constructed by FinPlate.Verify.</summary>
    public sealed class FinPlateActions
    {
        public double Fx { get; } public double Fy { get; } public double Fz { get; }
        public double Mx { get; } public double My { get; } public double Mz { get; }
        public FinPlateActions(double fx, double fy, double fz, double mx, double my, double mz)
        {
            foreach (double value in new[] { fx, fy, fz, mx, my, mz }) ConnectionNumbers.Finite(value, nameof(value));
            Fx = fx; Fy = fy; Fz = fz; Mx = mx; My = my; Mz = mz;
        }
        public static FinPlateActions DownwardShear(double force)
        { ConnectionValidation.Nonnegative(force, nameof(force)); return new FinPlateActions(0, 0, -force, 0, 0, 0); }
        internal bool IsZero => Fx == 0 && Fy == 0 && Fz == 0 && Mx == 0 && My == 0 && Mz == 0;
    }

    /// <summary>Physical rectangular layout; mm. Top/bottom refer to the plate, BeamTopOffset to the beam top.
    /// Rows run vertically; Columns run along the beam axis. Only the documented standard single-column layout is qualified.</summary>
    public sealed class FinPlateLayout
    {
        public int Rows { get; } public int Columns { get; }
        public double Height { get; } public double Pitch { get; } public double Gauge { get; }
        public double TopEdge { get; } public double BottomEdge { get; } public double FreeEdge { get; }
        public double BeamTopOffset { get; } public double BeamEndEdge { get; } public double Gap { get; }
        public double FirstBoltProjection => BeamEndEdge + Gap;
        public double CentroidProjection => FirstBoltProjection + (Columns - 1) * Gauge / 2;
        public FinPlateLayout(int rows, int columns, double height, double pitch, double gauge,
            double topEdge, double bottomEdge, double freeEdge, double beamTopOffset, double beamEndEdge, double gap)
        {
            if (rows < 1 || rows > 100 || columns < 1 || columns > 10) throw new ArgumentOutOfRangeException(nameof(rows));
            foreach (double value in new[] { height, pitch, topEdge, bottomEdge, freeEdge, beamEndEdge, gap })
                ConnectionValidation.Positive(value, nameof(value));
            ConnectionValidation.Nonnegative(gauge, nameof(gauge)); ConnectionValidation.Nonnegative(beamTopOffset, nameof(beamTopOffset));
            if (columns > 1 && gauge == 0) throw new ArgumentException("Distinct bolt columns require a positive gauge.");
            double expectedHeight = topEdge + (rows - 1) * pitch + bottomEdge;
            if (Math.Abs(height - expectedHeight) > 1e-10 * Math.Max(height, expectedHeight))
                throw new ArgumentException("Plate height must equal the end distances plus row spacings.");
            Rows = rows; Columns = columns; Height = height; Pitch = pitch; Gauge = gauge;
            TopEdge = topEdge; BottomEdge = bottomEdge; FreeEdge = freeEdge; BeamTopOffset = beamTopOffset;
            BeamEndEdge = beamEndEdge; Gap = gap;
        }
    }

    public enum FinPlateSupportFace { ColumnWeb, ColumnFlange, BeamWeb, HollowWall }

    /// <summary>Two identical continuous, symmetric 90-degree fillet welds along the full plate height.
    /// Throat is the supplied effective throat; the consumable source is caller evidence, not certification.</summary>
    public sealed class FinPlateWeldDetail
    {
        public double Throat { get; }
        public FilletWeldEndCondition EndCondition { get; }
        public string ConsumableCompatibilitySource { get; }
        public FinPlateWeldDetail(double throat, FilletWeldEndCondition endCondition, string consumableCompatibilitySource)
        {
            ConnectionValidation.Positive(throat, nameof(throat));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), endCondition)) throw new ArgumentOutOfRangeException(nameof(endCondition));
            Throat = throat; EndCondition = endCondition; ConsumableCompatibilitySource = consumableCompatibilitySource;
        }
    }

    /// <summary>Standard SCI short fin plate, one loaded side of an I/H column web, ordinary vertical static ULS.
    /// No notches, packing, preloading, slots, stiffeners, frame moments, tying, fire or fatigue are represented.
    /// ExistingSupportActions is mandatory: the initial local support model accepts only an explicitly zero state.</summary>
    public sealed class FinPlateInput
    {
        public ConnectionDesignCode Code { get; } public ConnectionPlate Plate { get; } public ConnectionBolt Bolt { get; }
        public FinPlateLayout Layout { get; } public ConnectionRolledISection Beam { get; } public ConnectionRolledISection Support { get; }
        public FinPlateActions Actions { get; } public FinPlateActions ExistingSupportActions { get; }
        public FinPlateWeldDetail Weld { get; } public FinPlateSupportFace SupportFace { get; }
        public int SupportLoadedSides { get; } public bool BeamLaterallyRestrained { get; } public bool BeamNotched { get; }
        public bool ExposedToWeather { get; }
        public string PinnedModelQualificationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public FinPlateInput(ConnectionDesignCode code, ConnectionPlate plate, ConnectionBolt bolt,
            FinPlateLayout layout, ConnectionRolledISection beam, ConnectionRolledISection support, FinPlateActions actions,
            FinPlateActions existingSupportActions, FinPlateWeldDetail weld, bool beamLaterallyRestrained,
            string pinnedModelQualificationSource, string holeClearanceServiceabilitySource = null,
            FinPlateSupportFace supportFace = FinPlateSupportFace.ColumnWeb, int supportLoadedSides = 1,
            bool beamNotched = false, bool exposedToWeather = false)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code)); Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt)); Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam)); Support = support ?? throw new ArgumentNullException(nameof(support));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ExistingSupportActions = existingSupportActions ?? throw new ArgumentNullException(nameof(existingSupportActions));
            Weld = weld ?? throw new ArgumentNullException(nameof(weld));
            if (!Enum.IsDefined(typeof(FinPlateSupportFace), supportFace)) throw new ArgumentOutOfRangeException(nameof(supportFace));
            if (supportLoadedSides < 1) throw new ArgumentOutOfRangeException(nameof(supportLoadedSides));
            if (Math.Abs(plate.Width - (layout.FirstBoltProjection + (layout.Columns - 1) * layout.Gauge + layout.FreeEdge)) > 1e-10 * plate.Width)
                throw new ArgumentException("Plate width does not match bolt projection, gauge and free edge.");
            BeamLaterallyRestrained = beamLaterallyRestrained; PinnedModelQualificationSource = pinnedModelQualificationSource;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource; SupportFace = supportFace;
            SupportLoadedSides = supportLoadedSides; BeamNotched = beamNotched; ExposedToWeather = exposedToWeather;
        }
    }

    public sealed class FinPlateResult
    {
        public const string MethodId = "Steel.Connection.FinPlate.StandardShort.SingleColumn";
        public ConnectionVerification<FinPlateInput> Assessment { get; }
        public FinPlateInput Input => Assessment.Input;
        public ConnectionStatus Status => Assessment.Status;
        public bool IsComplete => Assessment.IsComplete;
        public IReadOnlyList<ConnectionCheck> Checks => Assessment.Checks;
        public IReadOnlyList<SectionResistanceValue> Trace => Assessment.Trace;
        public IReadOnlyList<string> Diagnostics => Assessment.Diagnostics;
        public IReadOnlyList<string> Assumptions => Assessment.Assumptions;
        public ConnectionCheck GoverningCheck => Assessment.GoverningCheck;
        public double? Utilization => Assessment.Utilization;
        /// <summary>Load-dependent resistance check only. Overall GoverningCheck also includes
        /// minimum geometry and full-strength weld hierarchy, whose ratios do not scale with V.</summary>
        public ConnectionCheck LoadGoverningCheck => IsComplete ? Checks.Where(c => !c.Id.StartsWith("Detail.")
            && c.Id != "Weld.FullStrengthThroat").OrderByDescending(c => c.Utilization).First() : null;
        public double? LoadUtilization => LoadGoverningCheck?.Utilization;
        public ElasticBoltGroupResult BoltDistribution { get; }
        public FilletWeldGroupResult WeldVerification { get; }
        /// <summary>Geometrical conformity only; not a numerical rotational stiffness/capacity verification.</summary>
        public bool ConformsToSciPinnedDetail { get; }
        /// <summary>Caller evidence only. The method does not certify a frame model or generate its qualification.</summary>
        public string PinnedModelQualificationSource => Input.PinnedModelQualificationSource;
        public string Scope => Assessment.Scope;
        internal FinPlateResult(ConnectionVerification<FinPlateInput> assessment, ElasticBoltGroupResult bolts,
            FilletWeldGroupResult welds, bool conforms)
        { Assessment = assessment; BoltDistribution = bolts; WeldVerification = welds; ConformsToSciPinnedDetail = conforms; }
    }
}
