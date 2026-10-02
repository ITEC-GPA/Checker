using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>Weight per unit length of a grouted micropile (N/mm): steel tube, grout in the rest of the borehole and their sum; areas mm².</summary>
    public sealed class MicropileWeight
    {
        public double SteelArea { get; }
        public double GroutArea { get; }
        public double Steel { get; }
        public double Grout { get; }
        public double Total => Steel + Grout;
        internal MicropileWeight(double steelArea, double groutArea, double steel, double grout) { SteelArea = steelArea; GroutArea = groutArea; Steel = steel; Grout = grout; }
    }

    /// <summary>
    /// Resistance of the steel tube of a micropile for the horizontal check: class from D/t with ε² = 235/fy (EN 1993-1-1 Table 5.2: 50, 70, 90),
    /// Npl = A fyd, Mpl = Wpl fyd and My = Mpl (1 − |N|/Npl), a conservative linear N-M interaction without the grout. Units mm, N, N·mm.
    /// </summary>
    public sealed class MicropileTubeResistance
    {
        public SectionCHS Tube { get; }
        public double DesignYieldStrength { get; }
        public int SectionClass { get; }
        public double PlasticAxial { get; }
        public double PlasticMoment { get; }
        public double Axial { get; }
        /// <summary>Resisting moment for the horizontal capacity, N·mm.</summary>
        public double ResistingMoment { get; }
        public const string Model = "Solo acciaio CHS, classe 1 secondo limiti D/t con ε²=235/fy. My = Wpl·fy/γM0·(1−|N|/Npl). Interazione lineare conservativa N–M; riempimento escluso. Non verifica instabilità globale, taglio, giunti, corrosione o capacità di rotazione delle connessioni. fy deve essere appropriato a materiale e spessore.";
        internal MicropileTubeResistance(SectionCHS tube, double fyd, int cls, double npl, double mpl, double axial, double moment)
        { Tube = tube; DesignYieldStrength = fyd; SectionClass = cls; PlasticAxial = npl; PlasticMoment = mpl; Axial = axial; ResistingMoment = moment; }
    }

    /// <summary>
    /// The steel tube (CHS) of a micropile: a Model <see cref="SectionCHS"/> (from the ModelData catalogues EN 10210-2, Celsius EN 10210 and
    /// EN 10219-2, or by D and t), the Model steel (density and fyk) and the γM0 of the Model steel standard. Transferred from ANTHEA (Chs.Peso, GeometriaMicropalo.Coseno, MicropaloOrizzontale,
    /// commit fe4652c).
    /// </summary>
    public static class MicropileTube
    {
        /// <summary>
        /// Weight per unit length: steel tube and grout filling the borehole of diameter D (mm); the tube must be smaller than the borehole. The unit
        /// weight of the steel is the one of the Model material, ρ (t/mm³) · <see cref="SoilUnits.Gravity"/> (7850 kg/m³ · 9.81 m/s² for the steels of
        /// Model, as the legacy calculation); grout γ in N/mm³.
        /// </summary>
        public static MicropileWeight Weight(SectionCHS tube, Material material, double drillDiameter, double groutUnitWeight)
        {
            if (tube == null) throw new ArgumentNullException(nameof(tube));
            if (material == null) throw new ArgumentNullException(nameof(material));
            double steelUnitWeight = material.GetUnitWeight(SoilUnits.Gravity);
            if (!Positive(drillDiameter)) throw new ArgumentException("Invalid drilling diameter.");
            if (!Positive(groutUnitWeight) || !Positive(steelUnitWeight)) throw new ArgumentException("Invalid unit weight of the grout or of the steel.");
            if (tube.Diameter >= drillDiameter) throw new ArgumentException("The outer diameter of the CHS must be smaller than the drilling diameter.");
            double d = tube.Diameter, t = tube.Thickness;
            double steel = Math.PI * (d * d - Math.Pow(d - 2 * t, 2)) / 4, grout = Math.PI * drillDiameter * drillDiameter / 4 - steel;
            return new MicropileWeight(steel, grout, steelUnitWeight * steel, groutUnitWeight * grout);
        }

        /// <summary>cos θ of the axis inclined by θ (rad) from the vertical, 0 ≤ θ &lt; π/2.</summary>
        public static double AxisCosine(double inclination)
        {
            if (double.IsNaN(inclination) || double.IsInfinity(inclination) || inclination < 0 || inclination >= Math.PI / 2)
                throw new ArgumentException("Inclination θ: between 0 included and 90° excluded.");
            return Math.Cos(inclination);
        }

        /// <summary>Section class of a CHS: D/t / (235/fy) ≤ 50, 70, 90 (classes 1, 2, 3), otherwise 4.</summary>
        public static int SectionClass(SectionCHS tube, double fy)
        {
            double slenderness = tube.Diameter / tube.Thickness / (235 / fy);
            return slenderness <= 50 ? 1 : slenderness <= 70 ? 2 : slenderness <= 90 ? 3 : 4;
        }

        /// <summary>
        /// Resisting moment of the tube under the axial force N (N, sign ignored) for the horizontal capacity: class 1 only (no ductility is
        /// attributed otherwise), |N| &lt; Npl. The tube must be smaller than the borehole (mm).
        /// </summary>
        public static MicropileTubeResistance LateralResistance(SectionCHS tube, SteelMaterial steel, StandardEN1993p11 standard, double axial, double drillDiameter)
        {
            if (tube == null) throw new ArgumentNullException(nameof(tube));
            if (steel == null) throw new ArgumentNullException(nameof(steel));
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            double d = tube.Diameter, t = tube.Thickness;
            if (!Positive(d) || !Positive(t) || 2 * t >= d) throw new ArgumentException("CHS: the thickness must be smaller than half the diameter.");
            if (!Positive(drillDiameter) || d >= drillDiameter) throw new ArgumentException("The CHS diameter must be smaller than the geotechnical diameter.");
            if (!Positive(steel.Fyk)) throw new ArgumentException("Invalid yield strength of the steel.");
            if (double.IsNaN(standard.GammaM0) || standard.GammaM0 < 1) throw new ArgumentException("γM0 not smaller than 1.");
            if (double.IsNaN(axial) || double.IsInfinity(axial)) throw new ArgumentException("Invalid axial force.");
            double fyd = steel.CalculateFyd(standard), inner = d - 2 * t;
            double area = Math.PI / 4 * (d * d - inner * inner), wpl = (Math.Pow(d, 3) - Math.Pow(inner, 3)) / 6;
            int cls = SectionClass(tube, steel.Fyk);
            if (cls != 1) throw new ArgumentException("CHS not of class 1: the moment of the plastic mechanism is not available automatically; no ductility is attributed.");
            double npl = area * fyd, mpl = wpl * fyd, ratio = Math.Abs(axial) / npl;
            if (ratio >= 1) throw new ArgumentException("The axial force reaches or exceeds the resistance of the CHS.");
            return new MicropileTubeResistance(tube, fyd, cls, npl, mpl, axial, mpl * (1 - ratio));
        }

        private static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
    }
}
