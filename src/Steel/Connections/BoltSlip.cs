using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum BoltSlipLimitState { Serviceability, Ultimate }
    public enum BoltTighteningControl { Controlled, Uncontrolled }

    /// <summary>Slip factors are separate from plate/bolt strength factors.
    /// EC values and their National Annex are caller-selected; NTC values are fixed.</summary>
    public sealed class BoltSlipPolicy
    {
        public ConnectionDesignCode Code { get; }
        public double GammaM3Ultimate { get; }
        public double GammaM3Serviceability { get; }
        private BoltSlipPolicy(ConnectionDesignCode code, double ultimate, double serviceability)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            ConnectionValidation.Positive(ultimate, nameof(ultimate)); ConnectionValidation.Positive(serviceability, nameof(serviceability));
            GammaM3Ultimate = ultimate; GammaM3Serviceability = serviceability;
        }
        public static BoltSlipPolicy Eurocode3(ConnectionDesignCode code, double gammaM3Ultimate, double gammaM3Serviceability)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (code.IsNtc) throw new ArgumentException("Use the NTC policy for an NTC design code.");
            return new BoltSlipPolicy(code, gammaM3Ultimate, gammaM3Serviceability);
        }
        public static BoltSlipPolicy Ntc2018(ConnectionDesignCode code)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (!code.IsNtc) throw new ArgumentException("An NTC design code is required.");
            return new BoltSlipPolicy(code, 1.25, 1.10);
        }
    }

    /// <summary>One preloaded bolt, normal round holes. Shear/tension are nonnegative
    /// concomitant design demands in N for the specified limit state and combination.
    /// Tension includes any already-assessed prying. No implicit SLS/ULS conversion.
    /// FrictionPlanes counts interfaces, not bolts. FrictionCoefficient is qualified
    /// by the caller for the actual surface/condition; no treatment is inferred.</summary>
    internal sealed class BoltSlipInput
    {
        public BoltSlipPolicy Policy { get; }
        public ConnectionBolt Bolt { get; }
        public BoltSlipLimitState LimitState { get; }
        public double ShearForce { get; }
        public double TensionForce { get; }
        public int FrictionPlanes { get; }
        public double FrictionCoefficient { get; }
        public BoltTighteningControl TighteningControl { get; }
        public string AssemblyQualificationSource { get; }
        public string SurfaceQualificationSource { get; }
        public string ActionCombinationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public double JointLength { get; }
        public BoltSlipInput(BoltSlipPolicy policy, ConnectionBolt bolt, BoltSlipLimitState limitState,
            double shearForce, double tensionForce, int frictionPlanes, double frictionCoefficient,
            BoltTighteningControl tighteningControl, string assemblyQualificationSource, string surfaceQualificationSource,
            string actionCombinationSource, string holeClearanceServiceabilitySource = null, double jointLength = 0)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            if (!Enum.IsDefined(typeof(BoltSlipLimitState), limitState)) throw new ArgumentOutOfRangeException(nameof(limitState));
            if (!Enum.IsDefined(typeof(BoltTighteningControl), tighteningControl)) throw new ArgumentOutOfRangeException(nameof(tighteningControl));
            ConnectionValidation.Nonnegative(shearForce, nameof(shearForce)); ConnectionValidation.Nonnegative(tensionForce, nameof(tensionForce));
            ConnectionValidation.Nonnegative(jointLength, nameof(jointLength));
            if (frictionPlanes < 1) throw new ArgumentOutOfRangeException(nameof(frictionPlanes));
            ConnectionValidation.Positive(frictionCoefficient, nameof(frictionCoefficient));
            if (frictionCoefficient > 1) throw new ArgumentOutOfRangeException(nameof(frictionCoefficient));
            LimitState = limitState; ShearForce = shearForce; TensionForce = tensionForce; FrictionPlanes = frictionPlanes;
            FrictionCoefficient = frictionCoefficient; TighteningControl = tighteningControl;
            AssemblyQualificationSource = assemblyQualificationSource; SurfaceQualificationSource = surfaceQualificationSource;
            ActionCombinationSource = actionCombinationSource; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
            JointLength = jointLength;
        }
    }

    internal static class BoltSlip
    {
        public static ConnectionVerification<BoltSlipInput> Verify(BoltSlipInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            var code = p.Policy.Code;
            BoltComponentRules.RoundHole(p.Bolt, issues);
            BoltComponentRules.NtcHole(code, p.Bolt, p.HoleClearanceServiceabilitySource, issues);
            if (!code.IsNtc && p.TighteningControl != BoltTighteningControl.Controlled) issues.Add("EC_CONTROLLED_TIGHTENING_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.AssemblyQualificationSource)) issues.Add("PRELOADED_ASSEMBLY_QUALIFICATION_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.SurfaceQualificationSource)) issues.Add("FRICTION_SURFACE_QUALIFICATION_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.ActionCombinationSource)) issues.Add("CONCOMITANT_LIMIT_STATE_COMBINATION_REQUIRED");
            string reference = code.IsNtc ? "NTC2018 Table4.2.XIV and4.2.8.1.1 equations4.2.72-74"
                : "EN1993-1-8:2005 3.9.1/3.9.2 equations3.6-3.8, normal holes ks1";
            if (issues.Count == 0)
            {
                double gammaPreload = code.IsNtc && p.TighteningControl == BoltTighteningControl.Uncontrolled ? 1.10 : 1;
                double nominal = BoltSlipMath.NominalPreload(p.Bolt.Fu, p.Bolt.TensileArea);
                double preload = nominal / gammaPreload;
                double gammaSlip = p.LimitState == BoltSlipLimitState.Serviceability ? p.Policy.GammaM3Serviceability : p.Policy.GammaM3Ultimate;
                double remaining = Math.Max(0, preload - .8 * p.TensionForce);
                double betaLong = BoltLongJoint.Reduction(p.JointLength, p.Bolt.Diameter);
                double resistance = betaLong * BoltSlipMath.DesignResistance(1, p.FrictionPlanes, p.FrictionCoefficient, p.TensionForce, gammaSlip, preload);
                if (double.IsNaN(resistance) || double.IsInfinity(resistance) || double.IsInfinity(nominal))
                    issues.Add("SLIP_CAPACITY_OUTSIDE_NUMERIC_RANGE");
                else
                {
                    trace.Add(new SectionResistanceValue("NominalPreload", nominal, "N"));
                    trace.Add(new SectionResistanceValue("GammaM7", gammaPreload, "1"));
                    trace.Add(new SectionResistanceValue("PreloadForSlip", preload, "N"));
                    trace.Add(new SectionResistanceValue("GammaM3", gammaSlip, "1"));
                    trace.Add(new SectionResistanceValue("Ks", 1, "1"));
                    trace.Add(new SectionResistanceValue("FrictionPlanes", p.FrictionPlanes, "1"));
                    trace.Add(new SectionResistanceValue("Mu", p.FrictionCoefficient, "1"));
                    trace.Add(new SectionResistanceValue("ResidualClampingForce", remaining, "N"));
                    trace.Add(new SectionResistanceValue("BetaLongJoint", betaLong, "1"));
                    // The reserve check prevents zero shear masking a negative residual preload.
                    var role = p.LimitState == BoltSlipLimitState.Serviceability ? ConnectionCheckRole.Serviceability : ConnectionCheckRole.Resistance;
                    checks.Add(new ConnectionCheck("Slip.PreloadReserve", .8 * p.TensionForce, preload, reference, "N", role));
                    checks.Add(new ConnectionCheck(p.LimitState == BoltSlipLimitState.Serviceability ? "Slip.Serviceability" : "Slip.Ultimate",
                        p.ShearForce, resistance, reference + (betaLong < 1 ? "; conservative long-joint extension SCI P398 section4 Step2" : ""), "N", role));
                }
            }
            return new ConnectionVerification<BoltSlipInput>(p, code, "Steel.Connection.Bolt.Slip",
                "Slip and residual preload of one 8.8/10.9 bolt with normal round holes at the explicit limit state. "
                + "No bolt tensile/shear strength, bearing, punching, plate net/block, group/contact/prying model or whole-joint qualification. "
                + "Category B still requires ULS strength checks; category C requires the applicable plate/member checks.",
                checks, trace, issues, new[] {
                    "Preloading, assembly suitability, tightening and friction surface execution are caller-qualified; a source label is not certification.",
                    "Shear and tensile forces are concomitant at the named limit state; do not reuse ULS actions as SLS actions.",
                    "No favourable contact-force exemption of EN3.9.2(2) is invoked; tension always reduces the preload available for slip.",
                    "NTC uncontrolled tightening uses gammaM7=1.10 from Table4.2.XIV. This option is not imported into EC3 controlled-preload rules.",
                    "SCI P398 long-joint reduction is conservatively applied to slip at both limit states when JointLength>15d; no uniform-transfer exemption is inferred.",
                    "Friction planes share the specified coefficient and bolt preload. Oversize/slotted holes, relaxation not covered by the qualified surface and hybrid weld sharing are outside scope."
                });
        }
    }

    /// <summary>Arithmetic shared with the established EN bolt checker; callers own applicability and action-level checks.</summary>
    internal static class BoltSlipMath
    {
        internal static double NominalPreload(double fu, double area) => .7 * fu * area;
        internal static double DesignResistance(double ks, double planes, double mu, double tension, double gamma, double preload)
            => ks * planes * mu * Math.Max(0, preload - .8 * tension) / gamma;
    }
}
