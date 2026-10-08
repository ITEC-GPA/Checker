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
            if (!Enum.IsDefined(typeof(ServiceabilityCombination), combination)) throw new ArgumentOutOfRangeException(nameof(combination));
            if (!IsFinite(concreteLimitFactor) || concreteLimitFactor <= 0 || concreteLimitFactor > 1) throw new ArgumentOutOfRangeException(nameof(concreteLimitFactor));
            if (!(result.Standard is StandardModelCode2010 standard)) throw new NotSupportedException("Stress limits require a StandardModelCode2010-based standard.");
            if (!(result.SectionSolver.ConcreteMaterial is ConcreteMaterialEuropeanCommon material)) throw new NotSupportedException("Stress limits require a European concrete material.");
            if (result.StrainPlane == null) throw new InvalidOperationException("Stress analysis without strain plane: not converged.");
            var section = result.ConcreteSection;
            if (section.Rebars.Any(r => r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon && r.EpsilonP == 0))
                throw new NotSupportedException("Tendon with zero prestrain: the solver identifies tendons through the prestrain; limits not assigned.");

            bool linear = result.LinearElasticAnalysis;
            double psi = result.PsiRebar ?? 0, psiTendon = result.PsiTendon ?? 0;
            var vertices = linear ? result.GetConcreteVerticesTension(psi) : result.GetConcreteVerticesTension();
            var bars = linear ? result.GetRebarsTension(psi, psiTendon) : result.GetRebarsTension();
            if (vertices.Length == 0 || vertices.Any(v => !IsFinite(v.tension)) || bars.Any(b => !IsFinite(b.tension)))
                throw new InvalidOperationException("Non-finite stresses: stress analysis not converged.");

            var output = new StressLimitResult
            {
                Combination = combination, LinearAnalysis = linear, PsiRebar = psi, PsiTendon = psiTendon, ConcreteLimitFactor = concreteLimitFactor,
                ConcreteMinStress = vertices.Min(v => v.tension), SteelMaxStress = bars.Length == 0 ? 0 : bars.Max(b => Math.Abs(b.tension))
            };
            if (combination == ServiceabilityCombination.Frequent) return output;

            // Same native checks used by the application adapters; the factor reduces the concrete limit only.
            var concrete = combination == ServiceabilityCombination.Characteristic
                ? (linear ? result.ConcreteServiceabilityCharacteristicCheck(psi) : result.ConcreteServiceabilityCharacteristicCheck())
                : (linear ? result.ConcreteServiceabilityQuasiPermanentCheck(psi) : result.ConcreteServiceabilityQuasiPermanentCheck());
            double nativeLimit = Math.Abs(combination == ServiceabilityCombination.Characteristic
                ? material.GetConcreteServiceabilityCharacteristicStress(standard) : material.GetConcreteServiceabilityQuasiPermanentStress(standard));
            if (!IsFinite(nativeLimit) || nativeLimit <= 0) throw new InvalidOperationException("Invalid concrete stress limit.");
            output.ConcreteLimit = nativeLimit * concreteLimitFactor;
            var points = concrete.Select((c, i) => new { c, i }).Where(v => v.c.tension < 0)
                .Select(v => new StressLimitPoint("C" + (v.i + 1), v.c.point.X, v.c.point.Y, v.c.tension, output.ConcreteLimit.Value, v.c.workingRatio / concreteLimitFactor)).ToArray();
            output.ConcretePoints = Array.AsReadOnly(points);
            output.ConcreteGoverning = points.OrderByDescending(p => p.Ratio).FirstOrDefault();

            if (combination == ServiceabilityCombination.Characteristic && bars.Length != 0)
            {
                var steel = linear ? result.SteelServiceabilityCharacteristicCheck(psi, psiTendon) : result.SteelServiceabilityCharacteristicCheck();
                var steelPoints = steel.Select((s, i) => new StressLimitPoint((s.rebar.EpsilonP != 0 ? "P" : "B") + (i + 1), s.rebar.Position.X, s.rebar.Position.Y,
                    s.tension, Math.Abs(s.rebar.EpsilonP != 0 ? s.rebar.RebarMaterial.GetServiceabilityCharacteristicStressPrestress(standard)
                        : s.rebar.RebarMaterial.GetServiceabilityCharacteristicStress(standard)), s.workingRatio)).ToArray();
                if (steelPoints.Any(p => !IsFinite(p.Ratio) || !IsFinite(p.Limit) || p.Limit <= 0)) throw new InvalidOperationException("Invalid steel stress limit.");
                output.SteelPoints = Array.AsReadOnly(steelPoints);
                output.SteelGoverning = steelPoints.OrderByDescending(p => p.Ratio).First();
            }
            if (points.Any(p => !IsFinite(p.Ratio))) throw new InvalidOperationException("Invalid concrete stress ratio.");
            return output;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
