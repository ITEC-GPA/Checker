using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Serviceability
{
    /// <summary>Serviceability combination of the stress state being checked.</summary>
    public enum ServiceabilityCombination { Characteristic, Frequent, QuasiPermanent }

    /// <summary>Stress at a section point (MPa, compression negative), absolute limit (MPa) and ratio |σ|/limit.</summary>
    public sealed class StressLimitPoint
    {
        public string Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Stress { get; }
        public double Limit { get; }
        public double Ratio { get; }
        internal StressLimitPoint(string id, double x, double y, double stress, double limit, double ratio)
        { Id = id; X = x; Y = y; Stress = stress; Limit = limit; Ratio = ratio; }
    }

    /// <summary>
    /// Stress limits of a calculated stress state. Stresses MPa, compression negative; coordinates mm in the section plane.
    /// A null ratio means that this implementation has no limit for that material and combination.
    /// </summary>
    public sealed class StressLimitResult
    {
        public ServiceabilityCombination Combination { get; internal set; }
        public bool LinearAnalysis { get; internal set; }
        public double PsiRebar { get; internal set; }
        public double PsiTendon { get; internal set; }
        /// <summary>Factor on the concrete limit (1 = none; 0.8 for thin castings where the chosen standard requires it).</summary>
        public double ConcreteLimitFactor { get; internal set; }
        /// <summary>Minimum concrete stress at the vertices of the concrete shape (holes included).</summary>
        public double ConcreteMinStress { get; internal set; }
        /// <summary>Absolute concrete limit, including <see cref="ConcreteLimitFactor"/>; null when the combination has none.</summary>
        public double? ConcreteLimit { get; internal set; }
        /// <summary>Most stressed compressed vertex; null when no vertex is compressed or no limit applies.</summary>
        public StressLimitPoint ConcreteGoverning { get; internal set; }
        public double? ConcreteRatio => ConcreteLimit.HasValue ? ConcreteGoverning?.Ratio ?? 0 : (double?)null;
        /// <summary>Maximum absolute stress of bars and tendons.</summary>
        public double SteelMaxStress { get; internal set; }
        public StressLimitPoint SteelGoverning { get; internal set; }
        public double? SteelRatio => SteelGoverning?.Ratio;
        public double? Ratio => ConcreteRatio.HasValue || SteelRatio.HasValue ? Math.Max(ConcreteRatio ?? 0, SteelRatio ?? 0) : (double?)null;
        /// <summary>
        /// True when <see cref="Ratio"/> ≤ 1 (within the limits, the limit itself included), false when it is greater; null
        /// without a ratio, when no limit applies to the combination (frequent): the stress state is only calculated.
        /// </summary>
        public bool? Satisfied => Ratio.HasValue ? Ratio.Value <= 1 : (bool?)null;
        public IReadOnlyList<StressLimitPoint> ConcretePoints { get; internal set; } = new StressLimitPoint[0];
        public IReadOnlyList<StressLimitPoint> SteelPoints { get; internal set; } = new StressLimitPoint[0];
    }

    /// <summary>
    /// Stress-limit check of a stress state already calculated by the section solver; equilibrium is not solved again.
    /// Characteristic: concrete compression ≤ k1·fck at the vertices and bars ≤ k3·fyk, tendons ≤ prestress limit.
    /// Quasi-permanent: concrete compression ≤ k2·fck. Frequent: no stress limit (null ratios).
    /// Coefficients come from the standard of the stress analysis (ServiceabilityStress…Coefficient…).
    /// </summary>
    public static class StressLimitCheck
    {
        public const string MethodId = "Concrete.ServiceabilityStressLimits";

        /// <summary>
        /// Reason why the standard does not require serviceability stress limits; null otherwise. The other MC2010-based classes
        /// (MC2010, EN 1992-1-1 and annexes, NTC 2018, CNR-DT 204, CNR-DT 200) use the coefficients of the class.
        /// </summary>
        public static string NotApplicableReason(Standard standard)
            => standard != null && standard.GetType() == typeof(StandardCSTR34)
                ? "CS-TR34 (ground-supported floor slabs) does not set serviceability stress limits for the section." : null;

        /// <summary>
        /// Absolute steel stress limit k3·fyk (MPa) of a bar material, |<see cref="SteelMaterial.GetServiceabilityCharacteristicStress"/>|,
        /// with the coefficient of <paramref name="standard"/> (custom coefficients included). It does not depend on the combination:
        /// callers that show the steel limit for every serviceability state use it also for the quasi-permanent and frequent ones,
        /// where <see cref="Evaluate"/> sets no steel limit. No prestressing rule: for a tendon material it is still k3·fyk.
        /// </summary>
        public static double SteelLimit(StandardModelCode2010 standard, SteelMaterial material)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (material == null) throw new ArgumentNullException(nameof(material));
            return Math.Abs(material.GetServiceabilityCharacteristicStress(standard));
        }

        public static StressLimitResult Evaluate(StressAnalysisResult result, ServiceabilityCombination combination, double concreteLimitFactor = 1)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!(result.Standard is StandardModelCode2010 standard)) throw new NotSupportedException("Stress limits require a StandardModelCode2010-based standard.");
            return Evaluate(Analysis.LegacySectionCalculation.Capture(result), StressLimitContext.Resolve(standard, result.ConcreteSection), combination, concreteLimitFactor);
        }

        /// <summary>The same normative method with a detached numerical response and explicit resolved limits.</summary>
        public static StressLimitResult Evaluate(Analysis.SectionResponse response, StressLimitContext context, ServiceabilityCombination combination, double concreteLimitFactor = 1)
        {
            if (response == null || context == null) throw new ArgumentNullException();
            if (!Enum.IsDefined(typeof(ServiceabilityCombination), combination)) throw new ArgumentOutOfRangeException(nameof(combination));
            if (!IsFinite(concreteLimitFactor) || concreteLimitFactor <= 0 || concreteLimitFactor > 1) throw new ArgumentOutOfRangeException(nameof(concreteLimitFactor));
            if (response.Diagnostics.Status != Analysis.CalculationStatus.Completed) throw new InvalidOperationException("Numerical analysis not completed: " + response.Diagnostics.Message);
            if (!response.Bars.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(context.SteelCharacteristic.Keys.OrderBy(id => id, StringComparer.Ordinal)))
                throw new ArgumentException("Stress response and normative bar layout differ.");
            var output = new StressLimitResult { Combination = combination, LinearAnalysis = response.Linear, PsiRebar = response.PsiRebar,
                PsiTendon = response.PsiTendon, ConcreteLimitFactor = concreteLimitFactor,
                ConcreteMinStress = response.Concrete.Min(p => p.Stress), SteelMaxStress = response.Bars.Count == 0 ? 0 : response.Bars.Max(p => Math.Abs(p.Stress)) };
            if (combination == ServiceabilityCombination.Frequent) return output;
            double limit = combination == ServiceabilityCombination.Characteristic ? context.ConcreteCharacteristic : context.ConcreteQuasiPermanent;
            if (!IsFinite(limit) || limit <= 0) throw new InvalidOperationException("Invalid concrete stress limit.");
            output.ConcreteLimit = limit * concreteLimitFactor;
            var points = response.Concrete.Where(p => p.Stress < 0).Select(p => new StressLimitPoint(p.Id, p.X, p.Y, p.Stress,
                output.ConcreteLimit.Value, Math.Abs(p.Stress / limit) / concreteLimitFactor)).ToArray();
            output.ConcretePoints = Array.AsReadOnly(points); output.ConcreteGoverning = points.OrderByDescending(p => p.Ratio).FirstOrDefault();
            if (combination == ServiceabilityCombination.Characteristic && response.Bars.Count != 0)
            {
                var steel = response.Bars.Select(p => new StressLimitPoint(p.Id, p.X, p.Y, p.Stress, context.SteelCharacteristic[p.Id], Math.Abs(p.Stress / context.SteelCharacteristic[p.Id]))).ToArray();
                if (steel.Any(p => !IsFinite(p.Ratio) || !IsFinite(p.Limit) || p.Limit <= 0)) throw new InvalidOperationException("Invalid steel stress limit.");
                output.SteelPoints = Array.AsReadOnly(steel); output.SteelGoverning = steel.OrderByDescending(p => p.Ratio).First();
            }
            if (points.Any(p => !IsFinite(p.Ratio))) throw new InvalidOperationException("Invalid concrete stress ratio.");
            return output;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
