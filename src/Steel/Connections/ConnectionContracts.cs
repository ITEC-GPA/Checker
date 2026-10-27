using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Plate fracture/block-tearing factors are distinct from bolt/bearing factors (e.g. SCI P358 UK NA).
    /// The source label records the caller's parameter set, not its certification.</summary>
    public sealed class ConnectionDesignCode
    {
        public SectionResistanceCode Plates { get; }
        public double GammaM2Connection { get; }
        public bool IsNtc => Plates.Standard == SectionResistanceStandard.Ntc2018;
        private ConnectionDesignCode(SectionResistanceCode plates, double gammaM2Connection)
        { ConnectionValidation.Positive(gammaM2Connection, nameof(gammaM2Connection)); Plates = plates; GammaM2Connection = gammaM2Connection; }
        public static ConnectionDesignCode Eurocode3(double gammaM0, double gammaM2Plate, double gammaM2Connection, string parameterSet)
            => new ConnectionDesignCode(SectionResistanceCode.Eurocode3(gammaM0, gammaM2Plate, parameterSet), gammaM2Connection);
        public static ConnectionDesignCode Ntc2018() => new ConnectionDesignCode(SectionResistanceCode.Ntc2018(), 1.25);
    }

    /// <summary>Rectangular carbon-steel plate, mm and MPa. Strengths are assigned for actual product/thickness.</summary>
    public sealed class ConnectionPlate
    {
        public double Width { get; }
        public double Thickness { get; }
        public double Fy { get; }
        public double Fu { get; }
        public ConnectionPlate(double width, double thickness, double fy, double fu)
        {
            ConnectionValidation.Positive(width, nameof(width)); ConnectionValidation.Positive(thickness, nameof(thickness));
            ConnectionValidation.Positive(fy, nameof(fy)); ConnectionValidation.Positive(fu, nameof(fu));
            if (fu < fy) throw new ArgumentException("fu must not be below fy.");
            Width = width; Thickness = thickness; Fy = fy; Fu = fu;
        }
    }
    public enum ConnectionBoltGrade { Class8_8, Class10_9 }

    /// <summary>Non-countersunk bolt dimensions/material in a round hole. Areas mm2, dimensions mm.
    /// Preloading/packing applicability belongs to the component input; thread condition is common to assigned shear planes.</summary>
    public sealed class ConnectionBolt
    {
        public double Diameter { get; }
        public double HoleDiameter { get; }
        public double TensileArea { get; }
        public ConnectionBoltGrade Grade { get; }
        public bool ThreadedShearPlanes { get; }
        public double Fu => Grade == ConnectionBoltGrade.Class8_8 ? 800 : 1000;
        public ConnectionBolt(double diameter, double holeDiameter, double tensileArea, ConnectionBoltGrade grade, bool threadedShearPlanes)
        {
            ConnectionValidation.Positive(diameter, nameof(diameter)); ConnectionValidation.Positive(holeDiameter, nameof(holeDiameter));
            ConnectionValidation.Positive(tensileArea, nameof(tensileArea));
            if (!Enum.IsDefined(typeof(ConnectionBoltGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            if (holeDiameter <= diameter || tensileArea > Math.PI * diameter * diameter / 4)
                throw new ArgumentException("Round clearance hole and tensile area within shank area required.");
            Diameter = diameter; HoleDiameter = holeDiameter; TensileArea = tensileArea; Grade = grade; ThreadedShearPlanes = threadedShearPlanes;
        }
    }

    /// <summary>Two identical main plates joined by two identical symmetric covers, no packing/contact bearing.
    /// Each side of the butt has Rows x 2 bolts, equal pitch and centred transverse gauge.
    /// End distances are from the main butt edge and the cover free outer edge respectively.
    /// The two groups are mirror images. N is longitudinal tension in the plate plane;
    /// V1 is transverse in-plane, V2 out-of-plane; the six actions refer to the joint centre.</summary>
    public sealed class DoubleCoverSpliceInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionPlate Main { get; }
        public ConnectionPlate Cover { get; }
        public ConnectionBolt Bolt { get; }
        public SteelActions Actions { get; }
        public int Rows { get; }
        public double Pitch { get; }
        public double Gauge { get; }
        public double MainEnd { get; }
        public double CoverEnd { get; }
        public double ButtGap { get; }
        public bool Exposed { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public DoubleCoverSpliceInput(ConnectionDesignCode code, ConnectionPlate main, ConnectionPlate cover,
            ConnectionBolt bolt, SteelActions actions, int rows, double pitch, double gauge, double mainEnd,
            double coverEnd, double buttGap, bool exposed, string holeClearanceServiceabilitySource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code)); Main = main ?? throw new ArgumentNullException(nameof(main));
            Cover = cover ?? throw new ArgumentNullException(nameof(cover)); Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (rows < 1 || rows > 1000) throw new ArgumentOutOfRangeException(nameof(rows));
            ConnectionValidation.Positive(pitch, nameof(pitch)); ConnectionValidation.Positive(gauge, nameof(gauge));
            ConnectionValidation.Positive(mainEnd, nameof(mainEnd)); ConnectionValidation.Positive(coverEnd, nameof(coverEnd));
            ConnectionValidation.Nonnegative(buttGap, nameof(buttGap));
            Rows = rows; Pitch = pitch; Gauge = gauge; MainEnd = mainEnd; CoverEnd = coverEnd;
            ButtGap = buttGap; Exposed = exposed; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    public enum ConnectionStatus { Passed, Failed, NotSupported }
    public sealed class ConnectionCheck
    {
        public string Id { get; }
        public double Demand { get; }
        public double Resistance { get; }
        public double Utilization => Resistance > 0 ? Demand / Resistance : Demand == 0 ? 0 : double.PositiveInfinity;
        public bool Passed => Utilization <= 1;
        public string Reference { get; }
        public string Unit { get; }
        internal ConnectionCheck(string id, double demand, double resistance, string reference, string unit = "N")
        {
            ConnectionValidation.Nonnegative(demand, nameof(demand)); ConnectionValidation.Nonnegative(resistance, nameof(resistance));
            Id = id; Demand = demand; Resistance = resistance; Reference = reference; Unit = unit;
        }
    }
    public sealed class DoubleCoverSpliceResult
    {
        public const string MethodId = "Steel.Connection.DoubleCoverSplice.Tension";
        public DoubleCoverSpliceInput Input { get; }
        public ConnectionStatus Status { get; }
        /// <summary>All ULS checks in Scope were evaluated; not certification of the whole structural joint.</summary>
        public bool IsComplete { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ConnectionCheck GoverningCheck => IsComplete ? Checks.OrderByDescending(c => c.Utilization).First() : null;
        public double? Utilization => GoverningCheck?.Utilization;
        public string Scope => "Static concentric tension of symmetric two-cover rectangular-plate splice; ULS only. "
            + "No welds, compression, bending, fatigue, seismic ductility or slip/deformation verification.";
        internal DoubleCoverSpliceResult(DoubleCoverSpliceInput input, IList<ConnectionCheck> checks,
            IList<SectionResistanceValue> trace, IList<string> diagnostics)
        {
            var retained = ConnectionOutcome.Retained(checks, diagnostics.Count);
            Input = input; Checks = Array.AsReadOnly(retained); Trace = Array.AsReadOnly(trace.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            IsComplete = ConnectionOutcome.IsComplete(retained, diagnostics.Count);
            Status = ConnectionOutcome.Status(retained, diagnostics.Count);
        }
    }
    internal static class ConnectionValidation
    {
        // Only absorbs floating-point subtraction/multiplication roundoff at an
        // inclusive minimum; this is not a manufacturing/detailing tolerance.
        internal static bool BelowMinimumWithRoundoff(double actual, double minimum)
            => actual < minimum && minimum - actual > 8 * 2.2204460492503131e-16
                * Math.Max(Math.Abs(actual), Math.Abs(minimum));
        internal static void Positive(double value, string name)
        { if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        internal static void Nonnegative(double value, string name)
        { if (!(value >= 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
    }
}
