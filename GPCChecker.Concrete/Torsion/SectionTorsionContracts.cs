using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Shear;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Torsion
{
    /// <summary>Equivalent thickness of a hollow section in <see cref="TorsionGeometry"/> suggestions.</summary>
    public enum TorsionThicknessRule
    {
        /// <summary>Actual wall thickness (NTC 2018 §4.1.2.3.6, ANTHEA).</summary>
        WallThickness,
        /// <summary>A/u (A within the outer outline, holes included), not less than 2 × axis distance, at most the wall (EN 1992-1-1 6.3.2(1)).</summary>
        EquivalentLimitedByWall
    }

    /// <summary>
    /// Resisting thin-walled profile: Ak area enclosed by the centre line of the walls (holes included), mm²; uk its perimeter, mm;
    /// tef wall thickness, mm. Explicit data; the static methods are suggestions from typical outlines, never confirmations.
    /// </summary>
    public sealed class TorsionGeometry
    {
        public double EnclosedArea { get; }
        public double Perimeter { get; }
        public double Thickness { get; }

        public TorsionGeometry(double enclosedArea, double perimeter, double thickness)
        {
            if (!Positive(enclosedArea) || !Positive(perimeter) || !Positive(thickness)) throw new ArgumentException("Torsion: Ak, uk and tef must be positive.");
            EnclosedArea = enclosedArea; Perimeter = perimeter; Thickness = thickness;
        }

        /// <summary>
        /// Rectangle width × height (holes: centred inner rectangle), transferred from ANTHEA ConcreteTorsionCalculator.Geometry.
        /// Solid: t = max(Ac/u, 2a); hollow: see <paramref name="rule"/>. a = distance from the edge to the centre of the longitudinal bars.
        /// </summary>
        public static TorsionGeometry Rectangle(double width, double height, double concreteArea, double axisDistance, double? innerWidth = null, double? innerHeight = null,
            TorsionThicknessRule rule = TorsionThicknessRule.WallThickness)
        {
            if (innerWidth.HasValue != innerHeight.HasValue) throw new ArgumentException("Torsion: both inner dimensions are required for a hollow rectangle.");
            double? wall = innerWidth.HasValue ? Math.Min((width - innerWidth.Value) / 2, (height - innerHeight.Value) / 2) : (double?)null;
            return FromOutline(false, width, height, concreteArea, axisDistance, wall, rule);
        }

        /// <summary>Circle of diameter D (holes: concentric circle). Rules as <see cref="Rectangle"/>.</summary>
        public static TorsionGeometry Circle(double diameter, double concreteArea, double axisDistance, double? innerDiameter = null,
            TorsionThicknessRule rule = TorsionThicknessRule.WallThickness)
            => FromOutline(true, diameter, diameter, concreteArea, axisDistance, innerDiameter.HasValue ? (diameter - innerDiameter.Value) / 2 : (double?)null, rule);

        private static TorsionGeometry FromOutline(bool circle, double width, double height, double concreteArea, double axis, double? wall, TorsionThicknessRule rule)
        {
            if (!Positive(width) || !Positive(height) || !Positive(concreteArea) || !Positive(axis) || (wall.HasValue && !Positive(wall.Value)))
                throw new ArgumentException("Torsion: invalid outline.");
            double outerPerimeter = circle ? Math.PI * width : 2 * (width + height);
            double t = concreteArea / outerPerimeter;
            if (!wall.HasValue) t = Math.Max(t, 2 * axis);
            else if (rule == TorsionThicknessRule.WallThickness) t = wall.Value;
            else t = Math.Min(Math.Max((circle ? Math.PI * width * width / 4 : width * height) / outerPerimeter, 2 * axis), wall.Value);
            if (t < 2 * axis || t <= 0 || t >= Math.Min(width, height))
                throw new ArgumentException("Torsion: the resisting thickness cannot contain the peripheral reinforcement.");
            return circle ? new TorsionGeometry(Math.PI * Math.Pow((width - t) / 2, 2), Math.PI * (width - t), t)
                : new TorsionGeometry((width - t) * (height - t), 2 * (width + height - 2 * t), t);
        }

        internal static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
    }

    /// <summary>
    /// Shear of one direction with the cot θ of the torsion check: signed demand V and resistances VRsd (links), VRcd (strut), N;
    /// εx of Model Code 2010 (0 otherwise). Built by <see cref="SectionTorsionCalculator.Calculate"/> or given explicitly.
    /// </summary>
    public sealed class TorsionShearComponent
    {
        public double Demand { get; }
        public double VRsd { get; }
        public double VRcd { get; }
        public double CotTheta { get; }
        public double LongitudinalStrain { get; }
        /// <summary>Shear result of the direction when computed by the torsion calculator; null for given values.</summary>
        public SectionShearResult Shear { get; }

        public TorsionShearComponent(double demand, double vrsd, double vrcd, double cotTheta, double longitudinalStrain = 0)
        {
            if (new[] { demand, vrsd, vrcd, cotTheta, longitudinalStrain }.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || vrsd < 0 || vrcd < 0 || longitudinalStrain < 0)
                throw new ArgumentException("Torsion: invalid shear component.");
            Demand = demand; VRsd = vrsd; VRcd = vrcd; CotTheta = cotTheta; LongitudinalStrain = longitudinalStrain;
        }

        internal TorsionShearComponent(double demand, SectionShearResult shear) : this(demand, shear.VRsd, shear.VRcd, shear.CotTheta, shear.LongitudinalStrain) { Shear = shear; }
    }

    /// <summary>
    /// Torsion of a non-prestressed section with closed links at 90°. Units: T in Nmm (sign kept), lengths mm, areas mm², stresses MPa.
    /// Design strengths and γc are explicit. The longitudinal area ΣAsl is the one available for torsion in addition to bending
    /// (NTC 2018, EN 1992-1-1 6.3.2(3)), distributed along the profile with a bar in each corner.
    /// </summary>
    public sealed class SectionTorsionInput
    {
        public Standard Standard { get; }
        public double T { get; }
        public TorsionGeometry Geometry { get; }
        public double Fck { get; }
        public double Fcd { get; }
        public double GammaC { get; }
        /// <summary>Design yield strength of the links, MPa.</summary>
        public double LinkFyd { get; }
        /// <summary>Design yield strength of the longitudinal torsion bars, MPa.</summary>
        public double LongitudinalFyd { get; }
        /// <summary>Area of one leg of the closed links, mm²; 0 = no closed links (zero torsional resistance).</summary>
        public double LinkLegArea { get; }
        public double Spacing { get; }
        /// <summary>ΣAsl available for torsion, mm².</summary>
        public double LongitudinalArea { get; }
        /// <summary>Strut inclination shared by torsion and shear.</summary>
        public double CotTheta { get; }
        /// <summary>Inclination of the links, degrees; only 90° is implemented.</summary>
        public double LinkAngleDegrees { get; }
        /// <summary>Box section with reinforcement on both faces of the walls (DIN ν and linear interaction; Model Code 2010 linear interaction).</summary>
        public bool HollowWithReinforcementOnBothFaces { get; }

        public SectionTorsionInput(Standard standard, double t, TorsionGeometry geometry, double fck, double fcd, double gammaC, double linkFyd, double longitudinalFyd,
            double linkLegArea, double spacing, double longitudinalArea, double cotTheta, double linkAngleDegrees = 90, bool hollowWithReinforcementOnBothFaces = false)
        {
            Standard = standard ?? throw new ArgumentNullException(nameof(standard));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            T = t; Fck = fck; Fcd = fcd; GammaC = gammaC; LinkFyd = linkFyd; LongitudinalFyd = longitudinalFyd; LinkLegArea = linkLegArea; Spacing = spacing;
            LongitudinalArea = longitudinalArea; CotTheta = cotTheta; LinkAngleDegrees = linkAngleDegrees; HollowWithReinforcementOnBothFaces = hollowWithReinforcementOnBothFaces;
        }
    }

    public enum TorsionVerdict { Satisfied, NotSatisfied }

    /// <summary>
    /// Resistances in Nmm: TRcd (strut), TRsd (links), TRld (longitudinal bars), TRd = min. Ratios: torsion |T|/TRd, strut interaction with
    /// the shear of both directions, link interaction (torsion plus the governing direction). Null ratio = not defined (zero resistance):
    /// the verdict is then NotSatisfied, never a zero ratio.
    /// </summary>
    public sealed class SectionTorsionResult
    {
        public TorsionProfile Profile { get; internal set; }
        public double Demand { get; internal set; }
        public double TRcd { get; internal set; }
        public double TRsd { get; internal set; }
        public double TRld { get; internal set; }
        public double TRd { get; internal set; }
        public double? TorsionRatio { get; internal set; }
        public double? ConcreteInteraction { get; internal set; }
        public double? LinkInteraction { get; internal set; }
        /// <summary>Maximum of the three ratios; null when one of them is not defined.</summary>
        public double? Ratio { get; internal set; }
        /// <summary>ΣAsl required by torsion, mm².</summary>
        public double RequiredLongitudinalArea { get; internal set; }
        /// <summary>Area of one link leg per unit length required by torsion, mm²/mm.</summary>
        public double RequiredLinkAreaPerLength { get; internal set; }
        public double CotTheta { get; internal set; }
        /// <summary>Strut strength used in TRcd (ν fcd, f'cd or kc fck/γc), MPa.</summary>
        public double StrutStrength { get; internal set; }
        /// <summary>True when the strut interaction is quadratic (DIN and Model Code 2010 solid sections).</summary>
        public bool QuadraticInteraction { get; internal set; }
        public TorsionVerdict Verdict { get; internal set; }
        public string Status { get; internal set; }
        public string Model { get; internal set; }
        public string Reference { get; internal set; }
        public SectionShearResult Axis1Shear { get; internal set; }
        public SectionShearResult Axis2Shear { get; internal set; }
        public IReadOnlyList<ShearCalculationDetail> Details { get; internal set; } = new ShearCalculationDetail[0];
        /// <summary>Rules of the standard not included in this method (for example DK NA 6.3.2(6)).</summary>
        public IReadOnlyList<string> Limitations { get; internal set; } = new string[0];
    }
}
