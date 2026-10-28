using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Slenderness
{
    /// <summary>
    /// Profiles of the model column (0.27.0.0): the standard of the checker by exact type, without fallback. NTC 2018 and CNR-DT 200 (NTC member):
    /// slenderness limit [4.1.41], minimum eccentricity of §4.1.2.3.4.2, straightness defect of §4.1.2.3.9.3; the method and the imperfection by
    /// inclination are those of EN 1992-1-1, to which §4.1.2.3.9.3 refers ("documenti di comprovata validità"). EN 1992-1-1 with the recommended
    /// values and UNI EN 1992-1-1 with DM 31/07/2012 (θ0 = 1/200 of 5.2(5); the other nationally determined parameters of 5.8 with the
    /// recommended values, to be checked against the annex): slenderness limit (5.13N), minimum eccentricity of 6.1(4).
    /// </summary>
    public enum ModelColumnProfile { Ntc2018, CnrDT200, EN1992p11, UniEN1992p11 }

    /// <summary>Resolution of the profile of the model column from the standard of the checker (exact type, as the other profiles of the library).</summary>
    public static class ModelColumnProfiles
    {
        public static bool TryResolve(Standard standard, out ModelColumnProfile profile)
        {
            profile = default(ModelColumnProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = ModelColumnProfile.Ntc2018;
            else if (type == typeof(StandardCNR200)) profile = ModelColumnProfile.CnrDT200;
            else if (type == typeof(StandardEN1992p11)) profile = ModelColumnProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = ModelColumnProfile.UniEN1992p11;
            else return false;
            return true;
        }

        /// <summary>Reason of an unsupported standard, in English as the other rejections of the library.</summary>
        public static string NotSupportedReason(Standard standard)
        {
            if (standard == null) return "Model column: no standard.";
            return "Model column: no implemented profile for " + standard.GetType().Name
                + " (implemented: NTC 2018, CNR-DT 200, EN 1992-1-1 with the recommended values, UNI EN 1992-1-1 with DM 31/07/2012).";
        }

        /// <summary>True for NTC 2018 and CNR-DT 200.</summary>
        public static bool IsNtc(ModelColumnProfile profile) => profile == ModelColumnProfile.Ntc2018 || profile == ModelColumnProfile.CnrDT200;

        /// <summary>Basic inclination θ0 of EN 1992-1-1 5.2(5): 1/200 (recommended value; DM 31/07/2012 adopts it), also for NTC 2018.</summary>
        public static double BaseInclination(ModelColumnProfile profile) => 1.0 / 200;

        /// <summary>Standard of the profile, in Italian for the reports.</summary>
        public static string Reference(ModelColumnProfile profile)
        {
            switch (profile)
            {
                case ModelColumnProfile.Ntc2018: return "NTC 2018 §4.1.2.3.9 con EN 1992-1-1 5.8 (documento di comprovata validità)";
                case ModelColumnProfile.CnrDT200: return "CNR-DT 200 R1/2013 con NTC 2018 §4.1.2.3.9 ed EN 1992-1-1 5.8";
                case ModelColumnProfile.EN1992p11: return "EN 1992-1-1 5.8 (valori raccomandati)";
                case ModelColumnProfile.UniEN1992p11: return "UNI EN 1992-1-1 5.8 con DM 31/07/2012";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }

    /// <summary>Kind of the geometric imperfection of the model column.</summary>
    public enum ModelColumnImperfectionKind
    {
        /// <summary>No imperfection: ei = 0.</summary>
        None,
        /// <summary>Eccentricity assigned by the caller (mm).</summary>
        Assigned,
        /// <summary>EN 1992-1-1 5.2(5) and 5.2(7)a: ei = θi l0 / 2, θi = θ0 αh αm, αh = 2/√l (l in m, 2/3 ≤ αh ≤ 1), αm = √(0.5 (1 + 1/m)). Default.</summary>
        En1992Inclination,
        /// <summary>EN 1992-1-1 5.2(7) note: ei = l0 / 400 for walls and isolated columns of braced systems.</summary>
        En1992Simplified,
        /// <summary>NTC 2018 §4.1.2.3.9.3: straightness defect l / 300 of the height (compressed columns of braced frames without explicit bending).</summary>
        Ntc2018Straightness
    }

    /// <summary>Geometric imperfection of the model column, in the direction of the moment (unfavourable). Immutable.</summary>
    public sealed class ModelColumnImperfection
    {
        public ModelColumnImperfectionKind Kind { get; }
        /// <summary>Assigned eccentricity (mm); null for the other kinds.</summary>
        public double? Eccentricity { get; }
        /// <summary>θ0 of <see cref="ModelColumnImperfectionKind.En1992Inclination"/>; null: the value of the profile (<see cref="ModelColumnProfiles.BaseInclination"/>).</summary>
        public double? BaseInclination { get; }
        /// <summary>m of αm (5.2(6): 1 for the isolated member).</summary>
        public int Members { get; }

        private ModelColumnImperfection(ModelColumnImperfectionKind kind, double? eccentricity, double? baseInclination, int members)
        { Kind = kind; Eccentricity = eccentricity; BaseInclination = baseInclination; Members = members; }

        public static ModelColumnImperfection None { get; } = new ModelColumnImperfection(ModelColumnImperfectionKind.None, null, null, 1);
        public static ModelColumnImperfection En1992Simplified { get; } = new ModelColumnImperfection(ModelColumnImperfectionKind.En1992Simplified, null, null, 1);
        public static ModelColumnImperfection Ntc2018Straightness { get; } = new ModelColumnImperfection(ModelColumnImperfectionKind.Ntc2018Straightness, null, null, 1);

        /// <summary>Eccentricity ei ≥ 0 (mm) assigned by the caller.</summary>
        public static ModelColumnImperfection Assigned(double eccentricity) => new ModelColumnImperfection(ModelColumnImperfectionKind.Assigned, eccentricity, null, 1);

        /// <summary>EN 1992-1-1 5.2(7)a with θ0 (null: the value of the profile) and m members (1 for the isolated member, 5.2(6)).</summary>
        public static ModelColumnImperfection En1992Inclination(double? baseInclination = null, int members = 1)
            => new ModelColumnImperfection(ModelColumnImperfectionKind.En1992Inclination, null, baseInclination, members);
    }

    /// <summary>Minimum eccentricity of the first-order moment, applied as a minimum of the total M0Ed,tot (not added to the imperfection).</summary>
    public enum ModelColumnMinimumEccentricity
    {
        /// <summary>The one of the profile: NTC 2018 and CNR-DT 200 <see cref="Ntc2018"/>, EN and UNI <see cref="En1992"/>. Default.</summary>
        Standard,
        /// <summary>No minimum.</summary>
        None,
        /// <summary>NTC 2018 §4.1.2.3.4.2: e0 = max(l0 / 200; 20 mm).</summary>
        Ntc2018,
        /// <summary>EN 1992-1-1 6.1(4): e0 = max(h / 30; 20 mm), h depth of the section in the bending direction.</summary>
        En1992
    }

    /// <summary>Kind of the creep of the model column.</summary>
    public enum ModelColumnCreepKind
    {
        /// <summary>φef = 0; the conditions of EN 1992-1-1 5.8.4(4) are checked and a warning is given when they are not shown.</summary>
        Neglected,
        /// <summary>φef assigned by the caller.</summary>
        Effective,
        /// <summary>φef = φ(∞,t0) M0Eqp / M0Ed of EN 1992-1-1 (5.19), with the imperfection in both first-order moments.</summary>
        FinalCoefficient
    }

    /// <summary>Creep of the model column: the strains of the concrete law are multiplied by (1 + φef) (EN 1992-1-1 5.8.6(4)). Immutable.</summary>
    public sealed class ModelColumnCreep
    {
        public ModelColumnCreepKind Kind { get; }
        /// <summary>φef of <see cref="ModelColumnCreepKind.Effective"/>; null otherwise.</summary>
        public double? EffectiveRatio { get; }
        /// <summary>φ(∞,t0): the coefficient of (5.19), or the value known to the caller for the conditions of 5.8.4(4); null when not known.</summary>
        public double? FinalCoefficient { get; }
        /// <summary>M0Eqp (Nmm, in the direction of the moment, without the imperfection) of (5.19); null otherwise.</summary>
        public double? QuasiPermanentMoment { get; }
        /// <summary>N of the quasi-permanent combination (N, compression negative) for its imperfection moment; null otherwise.</summary>
        public double? QuasiPermanentAxialForce { get; }

        private ModelColumnCreep(ModelColumnCreepKind kind, double? effective, double? final, double? moment, double? axial)
        { Kind = kind; EffectiveRatio = effective; FinalCoefficient = final; QuasiPermanentMoment = moment; QuasiPermanentAxialForce = axial; }

        /// <summary>φef = 0, with φ(∞,t0) when known for the conditions of EN 1992-1-1 5.8.4(4).</summary>
        public static ModelColumnCreep Neglected(double? finalCoefficient = null) => new ModelColumnCreep(ModelColumnCreepKind.Neglected, null, finalCoefficient, null, null);

        /// <summary>φef ≥ 0 assigned; 0 is the same as <see cref="Neglected"/>.</summary>
        public static ModelColumnCreep Effective(double effectiveRatio, double? finalCoefficient = null)
            => new ModelColumnCreep(ModelColumnCreepKind.Effective, effectiveRatio, finalCoefficient, null, null);

        /// <summary>
        /// φef of (5.19) from φ(∞,t0), the first-order moment of the quasi-permanent combination (Nmm, direction of the moment, without imperfection)
        /// and its axial force (N, compression negative); needs the first-order moment of the request.
        /// </summary>
        public static ModelColumnCreep FromFinalCoefficient(double finalCoefficient, double quasiPermanentMoment, double quasiPermanentAxialForce)
            => new ModelColumnCreep(ModelColumnCreepKind.FinalCoefficient, null, finalCoefficient, quasiPermanentMoment, quasiPermanentAxialForce);
    }

    /// <summary>
    /// Request of the model column (0.27.0.0), units N, mm, Nmm, compression negative as <see cref="Response.MomentCurvatureRequest"/>:
    /// <list type="bullet">
    /// <item>axial force NEd ≤ 0 (tension rejected) and direction θ of the moment in degrees in the axes of the forces;</item>
    /// <item>effective length l0 and length l of the member (mm, l for αh of 5.2(5) and the straightness defect l/300);</item>
    /// <item>first-order moment M0Ed ≥ 0 in the direction θ at the critical section, without imperfection (Nmm); null: capacity only;</item>
    /// <item>factor c of the curvature distribution in [8; 10] (EN 1992-1-1 5.8.8.2(4); 10 by default, 8 for a constant first-order moment);</item>
    /// <item>imperfection (null: <see cref="ModelColumnImperfection.En1992Inclination"/> with θ0 of the profile), minimum eccentricity, creep (null:
    /// <see cref="ModelColumnCreep.Neglected"/>);</item>
    /// <item>rm = M01/M02 of the factor C of (5.13N) in [−1; 1], null when not known (C = 0.7);</item>
    /// <item>10-500 steps of curvature, tolerance on N of the resistance MRd(N) of the domain (N), largest transverse moment over the moment of the
    /// direction θ and its rejection.</item>
    /// </list>
    /// </summary>
    public sealed class ModelColumnRequest
    {
        public double AxialForce { get; }
        public double DirectionDegrees { get; }
        public double EffectiveLength { get; }
        public double MemberLength { get; }
        public double? FirstOrderMoment { get; }
        public double CurvatureFactor { get; }
        public ModelColumnImperfection Imperfection { get; }
        public ModelColumnMinimumEccentricity MinimumEccentricity { get; }
        public ModelColumnCreep Creep { get; }
        public double? EndMomentRatio { get; }
        public int Steps { get; }
        public double AxialTolerance { get; }
        public double TransverseMomentLimit { get; }
        public bool RejectTransverseMoment { get; }

        public ModelColumnRequest(double axialForce, double directionDegrees, double effectiveLength, double memberLength, double? firstOrderMoment = null,
            double curvatureFactor = 10, ModelColumnImperfection imperfection = null, ModelColumnMinimumEccentricity minimumEccentricity = ModelColumnMinimumEccentricity.Standard,
            ModelColumnCreep creep = null, double? endMomentRatio = null, int steps = 200, double axialTolerance = 1000, double transverseMomentLimit = 0.01,
            bool rejectTransverseMoment = true)
        {
            AxialForce = axialForce; DirectionDegrees = directionDegrees; EffectiveLength = effectiveLength; MemberLength = memberLength;
            FirstOrderMoment = firstOrderMoment; CurvatureFactor = curvatureFactor; Imperfection = imperfection ?? ModelColumnImperfection.En1992Inclination();
            MinimumEccentricity = minimumEccentricity; Creep = creep ?? ModelColumnCreep.Neglected(); EndMomentRatio = endMomentRatio; Steps = steps;
            AxialTolerance = axialTolerance; TransverseMomentLimit = transverseMomentLimit; RejectTransverseMoment = rejectTransverseMoment;
        }
    }
}
