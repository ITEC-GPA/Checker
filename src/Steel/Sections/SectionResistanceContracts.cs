using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GPC.Checkers.Steel.Sections
{
    public enum SectionResistanceStandard { Eurocode3_2005, Ntc2018 }
    public enum SectionResistanceMechanism { Tension, Compression, Bending, Shear }
    public enum SectionResistanceStatus
    {
        Passed, Failed, NotSupported, MissingInput, InvalidInput, CalculationError
    }

    /// <summary>Immutable code context for the four section primitives only.
    /// No implicit national annex selection and no member-stability parameters.</summary>
    public sealed class SectionResistanceCode
    {
        internal const double NtcGammaM0 = 1.05;
        internal const double NtcGammaM2 = 1.25;
        public SectionResistanceStandard Standard { get; }
        public string Name { get; }
        public string Edition { get; }
        public string ParameterSet { get; }
        public double GammaM0 { get; }
        public double GammaM2 { get; }

        private SectionResistanceCode(SectionResistanceStandard standard, string name, string edition,
            string parameterSet, double gammaM0, double gammaM2)
        {
            if (string.IsNullOrWhiteSpace(parameterSet)) throw new ArgumentException("An explicit parameter-set label is required.", nameof(parameterSet));
            if (!(gammaM0 > 0) || double.IsInfinity(gammaM0)) throw new ArgumentOutOfRangeException(nameof(gammaM0));
            if (!(gammaM2 > 0) || double.IsInfinity(gammaM2)) throw new ArgumentOutOfRangeException(nameof(gammaM2));
            Standard = standard; Name = name; Edition = edition; ParameterSet = parameterSet;
            GammaM0 = gammaM0; GammaM2 = gammaM2;
        }

        /// <summary>The parameter-set label records provenance; it does not certify
        /// that the supplied factors match the named national annex.</summary>
        public static SectionResistanceCode Eurocode3(double gammaM0, double gammaM2, string parameterSet)
            => new SectionResistanceCode(SectionResistanceStandard.Eurocode3_2005, "EN 1993-1-1", "2005",
                parameterSet, gammaM0, gammaM2);

        public static SectionResistanceCode Ntc2018()
            => new SectionResistanceCode(SectionResistanceStandard.Ntc2018, "NTC", "2018", "D.M. 17/01/2018",
                NtcGammaM0, NtcGammaM2);
    }

    /// <summary>One finite scalar used or obtained in the calculation; units are N, mm, MPa or Nmm.</summary>
    public sealed class SectionResistanceValue
    {
        public string Name { get; }
        public double Value { get; }
        public string Unit { get; }
        internal SectionResistanceValue(string name, double value, string unit)
        { Name = name; Value = value; Unit = unit; }
    }

    /// <summary>Decision for one isolated section mechanism, not a complete member check.
    /// Resistance and Utilization exist only for Passed/Failed. An unavailable or erroneous
    /// calculation cannot carry a successful numerical result. Trace values are snapshots.</summary>
    public sealed class SectionResistanceResult
    {
        public SectionResistanceMechanism Mechanism { get; }
        public SectionResistanceStatus Status { get; }
        public SectionResistanceCode Code { get; }
        public string Reference { get; }
        public string Scope { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }
        public double? Resistance { get; }
        public double? Utilization { get; }
        public string ResistanceUnit { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public bool IsEvaluated => Status == SectionResistanceStatus.Passed || Status == SectionResistanceStatus.Failed;

        internal SectionResistanceResult(SectionResistanceMechanism mechanism, SectionResistanceStatus status,
            SectionResistanceCode code, string reference, string scope, string diagnosticCode, string message,
            double? resistance, double? utilization, IList<SectionResistanceValue> trace)
        {
            Mechanism = mechanism; Status = status; Code = code; Reference = reference; Scope = scope;
            DiagnosticCode = diagnosticCode; Message = message; Resistance = resistance; Utilization = utilization;
            ResistanceUnit = mechanism == SectionResistanceMechanism.Bending ? "Nmm" : "N";
            Trace = new ReadOnlyCollection<SectionResistanceValue>(new List<SectionResistanceValue>(trace));
        }
    }
}
