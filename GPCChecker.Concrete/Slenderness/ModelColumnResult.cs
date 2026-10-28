using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Slenderness
{
    /// <summary>Reason of a rejected model column.</summary>
    public enum ModelColumnRejection
    {
        /// <summary>Values of the request not valid (lengths, c outside [8; 10], steps outside 10-500, negative M0Ed, imperfection or creep not valid).</summary>
        InvalidRequest,
        /// <summary>No profile for the standard of the checker (<see cref="ModelColumnProfiles"/>).</summary>
        UnsupportedStandard,
        /// <summary>Section without bars, with steel profiles, of fibre-reinforced or non-European concrete.</summary>
        UnsupportedSection,
        /// <summary>Checker with the elastic failure domain or with the concrete in tension (NTC 2018 §4.1.2.3.9.2: neglected).</summary>
        UnsupportedChecker,
        /// <summary>Tensile axial force: the model column is a compression member.</summary>
        TensionAxialForce,
        /// <summary>|NEd| beyond the resistance in centred compression of the section (with the law of the creep).</summary>
        AxialForceBeyondResistance,
        /// <summary>Transverse moment beyond <see cref="ModelColumnRequest.TransverseMomentLimit"/>: the section is not symmetric about the plane of bending.</summary>
        TransverseMomentBeyondLimit
    }

    /// <summary>Warnings of a model column; the analysis is complete.</summary>
    public enum ModelColumnWarning
    {
        /// <summary>N = 0: no second-order moment, M1Rd,max is the maximum moment of the section at N = 0.</summary>
        NoAxialForce,
        /// <summary>φef = 0 without the three conditions of EN 1992-1-1 5.8.4(4) shown (φ(∞,t0) ≤ 2, λ ≤ 75, M0Ed/NEd ≥ h).</summary>
        CreepNeglectedWithoutConditions,
        /// <summary>Transverse moment beyond the limit, accepted (<see cref="ModelColumnRequest.RejectTransverseMoment"/> false).</summary>
        TransverseMoment,
        /// <summary>Resistance MRd(N) of the failure domain not available: the comparison with the nominal curvature is incomplete.</summary>
        ResistanceNotAvailable,
        /// <summary>M0Ed,tot not above the moment of the section at zero curvature (asymmetric section): equilibrium taken at χ = 0.</summary>
        EquilibriumAtZeroCurvature
    }

    /// <summary>Failure mode of the model column.</summary>
    public enum ModelColumnMode
    {
        /// <summary>Maximum of M − M2 before the ultimate curvature: limit point of the equilibrium (instability).</summary>
        Instability,
        /// <summary>Maximum of M − M2 at the ultimate curvature: failure of the critical section.</summary>
        SectionFailure
    }

    /// <summary>
    /// Typed rejection of the model column: an <see cref="ArgumentException"/> with the reason, the axial force of the request and a value of the reason
    /// (resistance in compression, transverse ratio; NaN otherwise); the reason is also in <c>Data[</c><see cref="ModelColumnAnalysis.RejectionKey"/><c>]</c>.
    /// </summary>
    [Serializable]
    public sealed class ModelColumnException : ArgumentException
    {
        public ModelColumnRejection Reason { get; }
        public double AxialForce { get; }
        /// <summary>NRd,c (N, compression negative) for <see cref="ModelColumnRejection.AxialForceBeyondResistance"/>, the transverse ratio for
        /// <see cref="ModelColumnRejection.TransverseMomentBeyondLimit"/>, NaN otherwise.</summary>
        public double Value { get; }
        internal ModelColumnException(string message, ModelColumnRejection reason, double axialForce, double value) : base(message)
        { Reason = reason; AxialForce = axialForce; Value = value; Data[ModelColumnAnalysis.RejectionKey] = reason; }

        private ModelColumnException(SerializationInfo info, StreamingContext context) : base(info, context)
        { Reason = (ModelColumnRejection)info.GetInt32("GPC.Reason"); AxialForce = info.GetDouble("GPC.AxialForce"); Value = info.GetDouble("GPC.Value"); }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            base.GetObjectData(info, context);
            info.AddValue("GPC.Reason", (int)Reason); info.AddValue("GPC.AxialForce", AxialForce); info.AddValue("GPC.Value", Value);
        }
    }

    /// <summary>
    /// Point of the curve at imposed curvature: curvature χ (1/mm) in the direction of the moment, moment M in the direction θ and transverse moment
    /// (Nmm), second-order moment M2 = |NEd| l0²/c χ, first-order capacity M − M2, strain at the reference point of the solver, largest compression
    /// strain of the concrete (positive) and strain of the most tensile bar (tension positive), total strains (with the creep strains).
    /// </summary>
    public sealed class ModelColumnPoint
    {
        public double Curvature { get; internal set; }
        public double Moment { get; internal set; }
        public double TransverseMoment { get; internal set; }
        public double SecondOrderMoment { get; internal set; }
        public double FirstOrderCapacity { get; internal set; }
        public double ReferenceStrain { get; internal set; }
        public double ConcreteCompressionStrain { get; internal set; }
        public double SteelStrain { get; internal set; }
        /// <summary>A bar at or beyond its design yield strain, in tension or in compression.</summary>
        public bool Yielded { get; internal set; }
        /// <summary>The point at the ultimate curvature χu.</summary>
        public bool Ultimate { get; internal set; }
        /// <summary>The point of the maximum of M − M2 (χ*), inserted in the curve when it is between two samples.</summary>
        public bool Maximum { get; internal set; }
    }

    /// <summary>
    /// Properties of the section in the direction of bending: gross concrete area Ac (mm²), depth h along the curvature (mm), second moment Ic of the
    /// uncracked concrete about its centroid (mm⁴), radius of gyration i (mm), area of the bars As (mm²) and their radius of gyration is (mm), fck and
    /// fcd (MPa), Σ As fyd (N), NRd = Ac fcd + As fyd of EN 1992-1-1 5.8.9(4) (N, positive), resistance in centred compression NRd,c of the model of
    /// the analysis (N, compression negative, with the law of the creep) and design yield strain εyd of the most tensile bar.
    /// </summary>
    public sealed class ModelColumnSection
    {
        public double ConcreteArea { get; internal set; }
        public double Depth { get; internal set; }
        public double Inertia { get; internal set; }
        public double RadiusOfGyration { get; internal set; }
        public double ReinforcementArea { get; internal set; }
        public double ReinforcementRadiusOfGyration { get; internal set; }
        public double CharacteristicStrength { get; internal set; }
        public double DesignStrength { get; internal set; }
        public double ReinforcementResistance { get; internal set; }
        public double AxialResistance { get; internal set; }
        public double CompressionResistance { get; internal set; }
        public double YieldStrain { get; internal set; }
    }

    /// <summary>
    /// Slenderness, informative (the analysis is carried out anyway): λ = l0/i (NTC [4.1.42], EN (5.14)), ν = |NEd|/(Ac fcd), ω = As fyd/(Ac fcd),
    /// λlim = 25/√ν of NTC [4.1.41] and λlim = 20 A B C/√n of EN (5.13N) with A = 1/(1 + 0.2 φef), B = √(1 + 2ω), C = 1.7 − rm (0.7 when rm is
    /// not known); <see cref="Limit"/> is the one of the profile. Infinite limits at N = 0.
    /// </summary>
    public sealed class ModelColumnSlenderness
    {
        public double Slenderness { get; internal set; }
        public double RelativeAxialForce { get; internal set; }
        public double MechanicalRatio { get; internal set; }
        public double LimitNtc2018 { get; internal set; }
        public double FactorA { get; internal set; }
        public double FactorB { get; internal set; }
        public double FactorC { get; internal set; }
        public double LimitEn1992 { get; internal set; }
        public double Limit { get; internal set; }
        public string Reference { get; internal set; }
        /// <summary>λ ≤ λlim of the profile: second-order effects negligible for the standard (NTC §4.1.2.3.9.2, EN 5.8.3.1(1)).</summary>
        public bool SecondOrderEffectsNegligible { get; internal set; }
    }

    /// <summary>Imperfection: kind, θ0, αh, αm, θi (EN 5.2(5); null for the other kinds), eccentricity ei (mm), moment |NEd| ei (Nmm) and reference.</summary>
    public sealed class ModelColumnImperfectionResult
    {
        public ModelColumnImperfectionKind Kind { get; internal set; }
        public double? BaseInclination { get; internal set; }
        public double? HeightFactor { get; internal set; }
        public double? MemberFactor { get; internal set; }
        public double? Inclination { get; internal set; }
        public double Eccentricity { get; internal set; }
        public double Moment { get; internal set; }
        public string Reference { get; internal set; }
    }

    /// <summary>
    /// First-order moment at the critical section (Nmm): assigned M0Ed (null for capacity only), moment of the imperfection, minimum eccentricity e0 (mm,
    /// null without minimum) and its moment, total M0Ed,tot = max(M0Ed + |NEd| ei; |NEd| e0) (null for capacity only) and whether the minimum governs.
    /// </summary>
    public sealed class ModelColumnFirstOrder
    {
        public double? AppliedMoment { get; internal set; }
        public double ImperfectionMoment { get; internal set; }
        public ModelColumnMinimumEccentricity MinimumEccentricityRule { get; internal set; }
        public double? MinimumEccentricity { get; internal set; }
        public double? MinimumMoment { get; internal set; }
        public double? TotalMoment { get; internal set; }
        public bool MinimumGoverns { get; internal set; }
        public string MinimumReference { get; internal set; }
    }

    /// <summary>
    /// Creep: kind, φef, φ(∞,t0) when known, M0Eqp,tot/M0Ed,tot of (5.19), and the conditions of EN 5.8.4(4) for φef = 0 (null when not known):
    /// φ(∞,t0) ≤ 2, λ ≤ 75, M0Ed/NEd ≥ h; <see cref="NeglectAdmissible"/> when all three are shown.
    /// </summary>
    public sealed class ModelColumnCreepResult
    {
        public ModelColumnCreepKind Kind { get; internal set; }
        public double EffectiveRatio { get; internal set; }
        public double? FinalCoefficient { get; internal set; }
        public double? MomentRatio { get; internal set; }
        public bool? FinalCoefficientCondition { get; internal set; }
        public bool SlendernessCondition { get; internal set; }
        public bool? EccentricityCondition { get; internal set; }
        public bool NeglectAdmissible { get; internal set; }
    }

    /// <summary>
    /// Capacity: M1Rd,max = max over χ of [M(χ) − |NEd| l0²/c χ] (Nmm), its curvature χ* (1/mm), moment M(χ*) and deflection e2* = l0²/c χ* (mm),
    /// mode, ultimate curvature χu and moment M(χu), first yield (null when no bar yields before χu) and the largest assigned M0Ed with the
    /// imperfection and the minimum eccentricity, M1Rd,max − |NEd| ei (null when |NEd| e0 > M1Rd,max).
    /// </summary>
    public sealed class ModelColumnCapacity
    {
        public double MaximumFirstOrderMoment { get; internal set; }
        public double CurvatureAtMaximum { get; internal set; }
        public double MomentAtMaximum { get; internal set; }
        public double DeflectionAtMaximum { get; internal set; }
        public double SecondOrderMomentAtMaximum { get; internal set; }
        public ModelColumnMode Mode { get; internal set; }
        public double UltimateCurvature { get; internal set; }
        public double UltimateMoment { get; internal set; }
        public double? YieldCurvature { get; internal set; }
        public double? YieldMoment { get; internal set; }
        public double? MaximumAppliedMoment { get; internal set; }
    }

    /// <summary>
    /// Equilibrium for the assigned M0Ed: first stable intersection M(χ) = M0Ed,tot + |NEd| l0²/c χ on [0; χ*]. Not found when M0Ed,tot > M1Rd,max
    /// (instability, not verified); then the values are null. Ratio = M0Ed,tot / M1Rd,max.
    /// </summary>
    public sealed class ModelColumnEquilibrium
    {
        public bool Found { get; internal set; }
        public double FirstOrderMoment { get; internal set; }
        public double? Curvature { get; internal set; }
        public double? Deflection { get; internal set; }
        public double? SecondOrderMoment { get; internal set; }
        public double? DesignMoment { get; internal set; }
        /// <summary>Moment of the section at χeq, M(χeq): equal to <see cref="DesignMoment"/> within the precision of the search.</summary>
        public double? SectionMoment { get; internal set; }
        public double Ratio { get; internal set; }
        public bool Verified { get; internal set; }
    }

    /// <summary>
    /// Nominal curvature of EN 1992-1-1 5.8.8, informative: n, ω, nu = 1 + ω, nbal = 0.4, Kr (5.36), β, Kφ (5.37), d = h/2 + is (5.35), εyd,
    /// 1/r0 = εyd/(0.45 d), 1/r (5.34) (1/mm), e2 = (1/r) l0²/c (mm), M2 = |NEd| e2 (5.33), MRd(N) of the failure domain in the direction θ, the
    /// largest first-order moment MRd − M2 and, with M0Ed, MEd = M0Ed,tot + M2 (5.31) and MEd/MRd (Nmm; null when MRd(N) is not available).
    /// </summary>
    public sealed class ModelColumnNominalCurvature
    {
        public double RelativeAxialForce { get; internal set; }
        public double MechanicalRatio { get; internal set; }
        public double Nu { get; internal set; }
        public double Nbal { get; internal set; }
        public double Kr { get; internal set; }
        public double Beta { get; internal set; }
        public double Kphi { get; internal set; }
        public double EffectiveDepth { get; internal set; }
        public double YieldStrain { get; internal set; }
        public double BasicCurvature { get; internal set; }
        public double Curvature { get; internal set; }
        public double Deflection { get; internal set; }
        public double SecondOrderMoment { get; internal set; }
        public double? ResistingMoment { get; internal set; }
        public double? MaximumFirstOrderMoment { get; internal set; }
        public double? DesignMoment { get; internal set; }
        public double? Ratio { get; internal set; }
    }

    /// <summary>
    /// Step of the trace for the report: stable code (<see cref="ModelColumnTraceCodes"/>), description and formula in Italian, value (null for a step
    /// without value) in the units of the library (<see cref="Unit"/>: N, mm, mm², mm⁴, Nmm, N·mm², 1/mm, MPa, − ), reference of the standard.
    /// In the formulas NEd is the modulus of the compression.
    /// </summary>
    public sealed class ModelColumnTraceStep
    {
        public string Code { get; }
        public string Description { get; }
        public string Formula { get; }
        public double? Value { get; }
        public string Unit { get; }
        public string Reference { get; }
        internal ModelColumnTraceStep(string code, string description, string formula, double? value, string unit, string reference)
        { Code = code; Description = description; Formula = formula ?? ""; Value = value; Unit = unit ?? ""; Reference = reference ?? ""; }

        public override string ToString()
            => Code + ": " + Description + " " + Formula + " = " + (Value.HasValue ? Value.Value.ToString("G9", CultureInfo.InvariantCulture) : "—") + " " + Unit
                + (Reference.Length > 0 ? " [" + Reference + "]" : "");
    }

    /// <summary>Stable codes of the trace of the model column.</summary>
    public static class ModelColumnTraceCodes
    {
        public const string Method = "Method";
        public const string EffectiveLength = "EffectiveLength";
        public const string MemberLength = "MemberLength";
        public const string AxialForce = "AxialForce";
        public const string ConcreteArea = "ConcreteArea";
        public const string Inertia = "Inertia";
        public const string RadiusOfGyration = "RadiusOfGyration";
        public const string Slenderness = "Slenderness";
        public const string RelativeAxialForce = "RelativeAxialForce";
        public const string MechanicalRatio = "MechanicalRatio";
        public const string FactorA = "FactorA";
        public const string FactorB = "FactorB";
        public const string FactorC = "FactorC";
        public const string LimitSlenderness = "LimitSlenderness";
        public const string BaseInclination = "BaseInclination";
        public const string HeightFactor = "HeightFactor";
        public const string MemberFactor = "MemberFactor";
        public const string Inclination = "Inclination";
        public const string ImperfectionEccentricity = "ImperfectionEccentricity";
        public const string ImperfectionMoment = "ImperfectionMoment";
        public const string AppliedMoment = "AppliedMoment";
        public const string MinimumEccentricity = "MinimumEccentricity";
        public const string MinimumMoment = "MinimumMoment";
        public const string TotalFirstOrderMoment = "TotalFirstOrderMoment";
        public const string FinalCreepCoefficient = "FinalCreepCoefficient";
        public const string CreepMomentRatio = "CreepMomentRatio";
        public const string EffectiveCreepRatio = "EffectiveCreepRatio";
        public const string CreepConditions = "CreepConditions";
        public const string ConcreteLaw = "ConcreteLaw";
        public const string CurvatureFactor = "CurvatureFactor";
        public const string SecondOrderStiffness = "SecondOrderStiffness";
        public const string UltimateCurvature = "UltimateCurvature";
        public const string UltimateMoment = "UltimateMoment";
        public const string YieldCurvature = "YieldCurvature";
        public const string CurvatureAtMaximum = "CurvatureAtMaximum";
        public const string DeflectionAtMaximum = "DeflectionAtMaximum";
        public const string SecondOrderMomentAtMaximum = "SecondOrderMomentAtMaximum";
        public const string MaximumFirstOrderMoment = "MaximumFirstOrderMoment";
        public const string Mode = "Mode";
        public const string EquilibriumCurvature = "EquilibriumCurvature";
        public const string EquilibriumDeflection = "EquilibriumDeflection";
        public const string EquilibriumSecondOrderMoment = "EquilibriumSecondOrderMoment";
        public const string DesignMoment = "DesignMoment";
        public const string Ratio = "Ratio";
        public const string Outcome = "Outcome";
        public const string NominalKr = "NominalKr";
        public const string NominalBeta = "NominalBeta";
        public const string NominalKphi = "NominalKphi";
        public const string NominalDepth = "NominalDepth";
        public const string NominalBasicCurvature = "NominalBasicCurvature";
        public const string NominalCurvature = "NominalCurvature";
        public const string NominalDeflection = "NominalDeflection";
        public const string NominalSecondOrderMoment = "NominalSecondOrderMoment";
        public const string NominalResistance = "NominalResistance";
        public const string NominalMaximumFirstOrderMoment = "NominalMaximumFirstOrderMoment";
        public const string NominalDesignMoment = "NominalDesignMoment";
        public const string NominalRatio = "NominalRatio";
        public const string BiaxialSlendernessRatio = "BiaxialSlendernessRatio";
        public const string BiaxialEccentricityRatio = "BiaxialEccentricityRatio";
        public const string BiaxialRelativeAxialForce = "BiaxialRelativeAxialForce";
        public const string BiaxialExponent = "BiaxialExponent";
        public const string BiaxialUtilization = "BiaxialUtilization";
        public const string BiaxialOutcome = "BiaxialOutcome";
    }

    /// <summary>
    /// Result of the model column (0.27.0.0): method of EN 1992-1-1 5.8.6(6) with 5.8.8.2(3)-(4) (NTC 2018 §4.1.2.3.9.3, "colonna modello"), critical
    /// section only, curvature from the response of the section at constant N. <see cref="Verified"/> is null for capacity only. Units N, mm, Nmm, 1/mm.
    /// </summary>
    public sealed class ModelColumnResult
    {
        public ModelColumnProfile Profile { get; internal set; }
        public double AxialForce { get; internal set; }
        public double DirectionDegrees { get; internal set; }
        public double EffectiveLength { get; internal set; }
        public double MemberLength { get; internal set; }
        public double CurvatureFactor { get; internal set; }
        /// <summary>k = |NEd| l0²/c (N·mm²): M2 = k χ.</summary>
        public double SecondOrderStiffness { get; internal set; }
        public ModelColumnSection Section { get; internal set; }
        public ModelColumnSlenderness Slenderness { get; internal set; }
        public ModelColumnImperfectionResult Imperfection { get; internal set; }
        public ModelColumnFirstOrder FirstOrder { get; internal set; }
        public ModelColumnCreepResult Creep { get; internal set; }
        public IReadOnlyList<ModelColumnPoint> Points { get; internal set; }
        public ModelColumnCapacity Capacity { get; internal set; }
        /// <summary>Null for capacity only (no M0Ed).</summary>
        public ModelColumnEquilibrium Equilibrium { get; internal set; }
        public ModelColumnNominalCurvature NominalCurvature { get; internal set; }
        /// <summary>max |M⊥| / max |M| on the points of the curve.</summary>
        public double TransverseMomentRatio { get; internal set; }
        public IReadOnlyList<ModelColumnTraceStep> Trace { get; internal set; }
        public IReadOnlyList<ModelColumnWarning> Warnings { get; internal set; }
        public bool? Verified { get; internal set; }
        public string Status { get; internal set; }
    }
}
