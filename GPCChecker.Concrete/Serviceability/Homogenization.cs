using System;

namespace GPC.Checkers.Concrete.Serviceability
{
    /// <summary>
    /// Reason why <see cref="Homogenization.Resolve"/> rejected its arguments. The exception is always an
    /// <see cref="ArgumentException"/> (exact type); the reason is stored in its <see cref="Exception.Data"/> under
    /// <see cref="Homogenization.RejectionKey"/>, so that callers can map it without reading the message.
    /// </summary>
    public enum HomogenizationRejection
    {
        /// <summary>A modulus is not finite or not positive, or the value is not a finite number.</summary>
        InvalidInput = 0,
        /// <summary>φ, given or derived from n, is not finite or is below −<see cref="Homogenization.CreepTolerance"/> (n &lt; Es/Ec).</summary>
        NegativeCreep = 1,
        /// <summary>The modular ratio n is not finite.</summary>
        RatioOutOfRange = 2
    }

    /// <summary>
    /// Modular ratio n = Es·(1 + φ)/Ec of the bars (or tendons) homogenized to concrete, and the creep coefficient φ that gives
    /// a chosen n. Moduli in any consistent unit (MPa in this library); n and φ are dimensionless.
    /// </summary>
    /// <remarks>
    /// Moved from ANTHEA, where the same arithmetic appears in three forms: Anthea.Calculations.Homogenization.Resolve
    /// (X.Calculations/ConcreteSectionProperties.cs:44-53, with checks), the synchronization of n and φ in the stress tab
    /// (X.Desktop/Wpf/ConcreteStress.cs:178 and :183, without checks) and the modular ratio of the short report and of the
    /// crack trace (X.Core/ReportConcreteShort.cs:78, X.Calculations/Ntc2018Checks.cs:238). The expressions keep the order of
    /// the operations of ANTHEA, so the results are bit for bit the same. The form Es/(Ec/(1 + φ)) of Model
    /// (ConcreteSectionHelper) is a different expression and can differ in the last digit.
    /// </remarks>
    public static class Homogenization
    {
        /// <summary>
        /// Tolerance of <see cref="Resolve"/> on a negative φ: values down to −1e-12 (rounding of φ derived from n = Es/Ec)
        /// are accepted and set to 0, lower values are rejected.
        /// </summary>
        public const double CreepTolerance = 1e-12;

        /// <summary>Key of <see cref="Exception.Data"/> that holds the <see cref="HomogenizationRejection"/> of a rejection of <see cref="Resolve"/>.</summary>
        public const string RejectionKey = "GPC.Checkers.Concrete.Serviceability.HomogenizationRejection";

        /// <summary>
        /// n = es·(1 + phi)/ec, evaluated as <c>es * (1 + phi) / ec</c>. No checks: a negative phi gives n &lt; es/ec (the stress
        /// tab of ANTHEA accepts it), NaN and infinities propagate.
        /// </summary>
        public static double ModularRatio(double es, double ec, double phi) => es * (1 + phi) / ec;

        /// <summary>
        /// φ = n·ec/es − 1, evaluated as <c>n * ec / es - 1</c>, the inverse of <see cref="ModularRatio"/>. No checks: n &lt; es/ec
        /// gives a negative φ, NaN and infinities propagate.
        /// </summary>
        public static double CreepFromModularRatio(double n, double es, double ec) => n * ec / es - 1;

        /// <summary>
        /// φ and n from φ (<paramref name="fromN"/> false) or from n (<paramref name="fromN"/> true), with the checks of ANTHEA:
        /// <list type="number">
        /// <item>finite positive moduli and a finite value, otherwise <see cref="HomogenizationRejection.InvalidInput"/>;</item>
        /// <item>φ = <paramref name="value"/>, or <see cref="CreepFromModularRatio"/> of it; a φ that is not finite or is below
        /// −<see cref="CreepTolerance"/> gives <see cref="HomogenizationRejection.NegativeCreep"/>;</item>
        /// <item>φ = max(0, φ), so the tolerance band gives 0;</item>
        /// <item>n = <see cref="ModularRatio"/>; a non-finite n gives <see cref="HomogenizationRejection.RatioOutOfRange"/>.</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Rejected arguments; exact type <see cref="ArgumentException"/>, reason in <see cref="Exception.Data"/>[<see cref="RejectionKey"/>].
        /// </exception>
        public static (double Phi, double N) Resolve(double es, double ec, bool fromN, double value)
        {
            if (!IsFinite(es) || es <= 0 || !IsFinite(ec) || ec <= 0 || !IsFinite(value))
                throw Rejection(HomogenizationRejection.InvalidInput,
                    "Homogenization: the elastic moduli must be finite and positive and the value a finite number.");
            double phi = fromN ? CreepFromModularRatio(value, es, ec) : value;
            if (!IsFinite(phi) || phi < -CreepTolerance)
                throw Rejection(HomogenizationRejection.NegativeCreep, "Homogenization: φ must be ≥ 0, that is n ≥ Es/Ec.");
            phi = Math.Max(0, phi);
            double n = ModularRatio(es, ec, phi);
            if (!IsFinite(n)) throw Rejection(HomogenizationRejection.RatioOutOfRange, "Homogenization: the modular ratio is outside the numeric range.");
            return (phi, n);
        }

        private static ArgumentException Rejection(HomogenizationRejection reason, string message)
        {
            var exception = new ArgumentException(message);
            exception.Data[RejectionKey] = reason;
            return exception;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
