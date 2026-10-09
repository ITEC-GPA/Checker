using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;

namespace GPC.Checkers.Geotechnics.Piles
{
    /// <summary>Installation of the pile: it selects K and μ of the shaft and α of the undrained adhesion.</summary>
    public enum PileInstallation { Bored, ContinuousFlightAuger, DrivenSteelSection, DrivenClosedSteelTube, DrivenPrecastConcrete, DrivenCastInPlace }

    /// <summary>State of a layer for the shaft coefficient K: loose or dense.</summary>
    public enum SoilDensity { Loose, Dense }

    /// <summary>Method of the group efficiency of the axial capacity.</summary>
    public enum PileGroupMethod { None, ConverseLabarre, Feld, UserDefined }

    /// <summary>Parameters of a layer of the profile for the axial capacity: behaviour, density for K, Nc of the undrained base, shaft active or not.</summary>
    public sealed class AxialPileLayer
    {
        public SoilBehaviour Behaviour { get; }
        public SoilDensity Density { get; }
        public double Nc { get; }
        public bool ShaftActive { get; }
        public AxialPileLayer(SoilBehaviour behaviour, SoilDensity density, double nc = 9, bool shaftActive = true)
        {
            if (double.IsNaN(nc) || double.IsInfinity(nc) || nc < 0) throw new ArgumentOutOfRangeException(nameof(nc));
            Behaviour = behaviour; Density = density; Nc = nc; ShaftActive = shaftActive;
        }
    }

    /// <summary>An investigated vertical for the axial capacity: a Model <see cref="SoilProfile"/> (pile head at the ground surface) and the parameters of its layers.</summary>
    public sealed class AxialPileSurvey
    {
        public SoilProfile Profile { get; }
        public IReadOnlyList<AxialPileLayer> Layers { get; }
        public AxialPileSurvey(SoilProfile profile, IEnumerable<AxialPileLayer> layers)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray();
            if (Layers.Count != profile.Layers.Count || Layers.Any(l => l == null)) throw new ArgumentException("One parameter set per layer of the profile.");
        }
    }

    /// <summary>An investigated vertical for a micropile: a Model <see cref="SoilProfile"/> and the Bustamante-Doix soil, α and activity of its layers.</summary>
    public sealed class MicropileSurvey
    {
        public SoilProfile Profile { get; }
        public IReadOnlyList<(BustamanteDoixSoil Soil, double Alpha, bool ShaftActive)> Layers { get; }
        public MicropileSurvey(SoilProfile profile, IEnumerable<(BustamanteDoixSoil Soil, double Alpha, bool ShaftActive)> layers)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Layers = (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray();
            if (Layers.Count != profile.Layers.Count) throw new ArgumentException("One parameter set per layer of the profile.");
        }
    }

    /// <summary>
    /// Pile for the axial capacity: installation, diameter and length (mm), unit weight of the pile (N/mm³; 25 kN/m³ as ANTHEA), buoyancy below the
    /// water table, design actions in compression and tension at the head (N; null when not given) and the friction angle of the base factor Nq
    /// (the one of the layer as ANTHEA, or reduced by Kishida: see <see cref="BearingCapacityFactors.Kishida"/> for the rule of the installation).
    /// </summary>
    public sealed class AxialPile
    {
        public PileInstallation Installation { get; }
        public double Diameter { get; }
        public double Length { get; }
        public double UnitWeight { get; }
        public bool Buoyancy { get; }
        public double? CompressionAction { get; }
        public double? TensionAction { get; }
        public NqFrictionAngle BaseFrictionAngle { get; }
        public AxialPile(PileInstallation installation, double diameter, double length, double unitWeight = 25 * SoilUnits.KiloNewtonPerCubicMetre, bool buoyancy = false,
            double? compressionAction = null, double? tensionAction = null, NqFrictionAngle baseFrictionAngle = NqFrictionAngle.Layer)
        {
            Installation = installation; Diameter = diameter; Length = length; UnitWeight = unitWeight; Buoyancy = buoyancy; CompressionAction = compressionAction; TensionAction = tensionAction;
            BaseFrictionAngle = Enum.IsDefined(typeof(NqFrictionAngle), baseFrictionAngle) ? baseFrictionAngle : throw new ArgumentOutOfRangeException(nameof(baseFrictionAngle));
        }
    }

    /// <summary>
    /// Grouted micropile (Bustamante-Doix): drilling diameter D and length along the axis (mm), inclination θ from the vertical (rad), injection and
    /// pressure (MPa, taken as pl), start of the grouted length along the axis (mm), base share of the shaft resistance (0-15%, null = no base), the
    /// Model <see cref="SectionCHS"/> of the tube and its Model material (density for the weight), unit weight of the grout (N/mm³) and the design
    /// actions along the axis (N).
    /// </summary>
    public sealed class Micropile
    {
        public double Diameter { get; }
        public double Length { get; }
        public double Inclination { get; }
        public MicropileInjection Injection { get; }
        public double Pressure { get; }
        public double GroutStart { get; }
        public double? BaseShare { get; }
        public SectionCHS Tube { get; }
        public Material TubeMaterial { get; }
        public double GroutUnitWeight { get; }
        public double? CompressionAction { get; }
        public double? TensionAction { get; }
        public Micropile(double diameter, double length, double inclination, MicropileInjection injection, double pressure, double groutStart, double? baseShare, SectionCHS tube,
            Material tubeMaterial, double groutUnitWeight = 25 * SoilUnits.KiloNewtonPerCubicMetre, double? compressionAction = null, double? tensionAction = null)
        {
            Diameter = diameter; Length = length; Inclination = inclination; Injection = injection; Pressure = pressure; GroutStart = groutStart; BaseShare = baseShare;
            Tube = tube ?? throw new ArgumentNullException(nameof(tube)); TubeMaterial = tubeMaterial ?? throw new ArgumentNullException(nameof(tubeMaterial));
            GroutUnitWeight = groutUnitWeight; CompressionAction = compressionAction; TensionAction = tensionAction;
        }
    }

    /// <summary>Group efficiency ηg of the axial capacity in compression and in tension and the number of piles.</summary>
    public sealed class PileGroupEfficiency
    {
        public PileGroupMethod Method { get; }
        public double Compression { get; }
        public double Tension { get; }
        public int Piles { get; }
        private PileGroupEfficiency(PileGroupMethod method, double compression, double tension, int piles)
        {
            if (!(compression > 0)) throw new ArgumentException("The selected method gives a non positive ηg.");
            Method = method; Compression = compression; Tension = tension; Piles = piles;
        }

        public static PileGroupEfficiency None() => new PileGroupEfficiency(PileGroupMethod.None, 1, 1, 1);

        /// <summary>Converse-Labarre: η = 1 − θx − θy, θ = atan(D/s) (degrees)/90 · (n − 1)/n per direction with more than one pile; applied to compression and tension.</summary>
        public static PileGroupEfficiency ConverseLabarre(int pilesX, int pilesY, double spacingX, double spacingY, double diameter)
        {
            Count(pilesX); Count(pilesY);
            if ((pilesX > 1 || pilesY > 1) && !(diameter > 0)) throw new ArgumentException("A positive diameter is required.");
            double tx = pilesX > 1 ? Math.Atan(diameter / Spacing(spacingX)) * 180 / Math.PI / 90 * (pilesX - 1) / pilesX : 0;
            double ty = pilesY > 1 ? Math.Atan(diameter / Spacing(spacingY)) * 180 / Math.PI / 90 * (pilesY - 1) / pilesY : 0;
            return new PileGroupEfficiency(PileGroupMethod.ConverseLabarre, 1 - tx - ty, 1 - tx - ty, pilesX * pilesY);
        }

        /// <summary>Feld: each adjacent or diagonal pile reduces by 1/16: η = 1 − 2 pairs/(nx ny)/16.</summary>
        public static PileGroupEfficiency Feld(int pilesX, int pilesY)
        {
            Count(pilesX); Count(pilesY);
            double pairs = (pilesX - 1.0) * pilesY + pilesX * (pilesY - 1.0) + 2 * (pilesX - 1.0) * (pilesY - 1.0);
            double eta = 1 - (2 * pairs / (pilesX * (double)pilesY)) / 16;
            return new PileGroupEfficiency(PileGroupMethod.Feld, eta, eta, pilesX * pilesY);
        }

        /// <summary>Assigned efficiencies in compression and tension (positive).</summary>
        public static PileGroupEfficiency UserDefined(double compression, double tension)
        {
            foreach (double v in new[] { compression, tension }) if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) throw new ArgumentException("Efficiency: positive finite values are required.");
            return new PileGroupEfficiency(PileGroupMethod.UserDefined, compression, tension, 1);
        }

        private static void Count(int n) { if (n < 1) throw new ArgumentException("The number of piles must be an integer not smaller than 1."); }
        private static double Spacing(double s) => !double.IsNaN(s) && !double.IsInfinity(s) && s > 0 ? s : throw new ArgumentException("A positive spacing is required.");
    }

    /// <summary>Shaft segment of a layer: depths (mm), mean and bottom σ'v (MPa), K, μ, drained and undrained unit shaft resistance (MPa), α, lateral resistances (N).</summary>
    public sealed class AxialShaftSegment
    {
        public double Top { get; }
        public double Bottom { get; }
        public double MeanStress { get; }
        public double BottomStress { get; }
        public double? K { get; }
        public double? Mu { get; }
        public double DrainedShear { get; }
        public double UndrainedShear { get; }
        public double? Alpha { get; }
        public double DrainedLateral { get; }
        public double UndrainedLateral { get; }
        public bool ShaftActive { get; }
        internal AxialShaftSegment(double top, double bottom, double mean, double bottomStress, double? k, double? mu, double td, double tu, double? alpha, double ld, double lu, bool active)
        {
            Top = top; Bottom = bottom; MeanStress = mean; BottomStress = bottomStress; K = k; Mu = mu; DrainedShear = td; UndrainedShear = tu; Alpha = alpha;
            DrainedLateral = ld; UndrainedLateral = lu; ShaftActive = active;
        }
    }

    /// <summary>Resistances of a pile on one vertical at a depth: shaft segments, tip stresses (MPa), Nq of the tip layer, Nc, drained and undrained base and shaft (N).</summary>
    public sealed class AxialSurveyResistance
    {
        public IReadOnlyList<AxialShaftSegment> Segments { get; }
        public double TipEffectiveStress { get; }
        public double TipFrictionAngle { get; }
        public double TipTotalStress { get; }
        public NqResult? Nq { get; }
        public double? Nc { get; }
        public double DrainedBase { get; }
        public double DrainedShaft { get; }
        public double UndrainedBase { get; }
        public double UndrainedShaft { get; }
        internal AxialSurveyResistance(AxialShaftSegment[] segments, double tipStress, double tipPhi, double tipTotal, NqResult? nq, double? nc, double bd, double ld, double bu, double lu)
        {
            Segments = segments; TipEffectiveStress = tipStress; TipFrictionAngle = tipPhi; TipTotalStress = tipTotal; Nq = nq; Nc = nc; DrainedBase = bd; DrainedShaft = ld;
            UndrainedBase = bu; UndrainedShaft = lu;
        }
    }

    /// <summary>Resistances of a micropile on one vertical at a depth: Bustamante-Doix segments, shaft resistance and base share (N).</summary>
    public sealed class MicropileSurveyResistance
    {
        public IReadOnlyList<MicropileShaftSegment> Segments { get; }
        public double Shaft { get; }
        public double Base { get; }
        internal MicropileSurveyResistance(MicropileShaftSegment[] segments, double shaft, double baseResistance) { Segments = segments; Shaft = shaft; Base = baseResistance; }
    }

    /// <summary>Drained (or the only condition of a micropile) and undrained capacity conditions.</summary>
    public enum AxialCondition { Drained, Undrained }

    /// <summary>A branch of the components: computed shaft and base (N), characteristic /ξ and design η/(ξ γ).</summary>
    public sealed class AxialComponentBranch
    {
        public double Shaft { get; }
        public double Base { get; }
        public double CharacteristicShaft { get; }
        public double CharacteristicBase { get; }
        public double DesignShaft { get; }
        public double DesignBase { get; }
        internal AxialComponentBranch(double shaft, double baseResistance, double xi, double gammaShaft, double gammaBase, double eta)
        {
            Shaft = shaft; Base = baseResistance; CharacteristicShaft = shaft / xi; CharacteristicBase = baseResistance / xi;
            DesignShaft = eta * shaft / (xi * gammaShaft); DesignBase = eta * baseResistance / (xi * gammaBase);
        }
    }

    /// <summary>Components of a condition and direction at a depth: mean branch (ξ3) and minimum branch (ξ4).</summary>
    public sealed class AxialComponents
    {
        public AxialComponentBranch Mean { get; }
        public AxialComponentBranch Minimum { get; }
        internal AxialComponents(AxialComponentBranch mean, AxialComponentBranch minimum) { Mean = mean; Minimum = minimum; }
    }

    /// <summary>Results at one depth: weight of the pile (N), resistances of every vertical and the components by condition and direction.</summary>
    public sealed class AxialDepthResult<TSurvey>
    {
        public double Depth { get; }
        public double Weight { get; }
        public IReadOnlyList<TSurvey> Surveys { get; }
        public IReadOnlyDictionary<(AxialCondition Condition, bool Compression), AxialComponents> Components { get; }
        internal AxialDepthResult(double depth, double weight, TSurvey[] surveys, Dictionary<(AxialCondition, bool), AxialComponents> components)
        { Depth = depth; Weight = weight; Surveys = surveys; Components = components; }
    }

    /// <summary>Design capacity curves along the depth: mean branch, minimum branch and design = min of the two (N).</summary>
    public sealed class AxialCapacityCurve
    {
        public IReadOnlyList<(double Depth, double Value)> Mean { get; }
        public IReadOnlyList<(double Depth, double Value)> Minimum { get; }
        public IReadOnlyList<(double Depth, double Value)> Design { get; }
        internal AxialCapacityCurve(List<(double, double)> mean, List<(double, double)> minimum, List<(double, double)> design) { Mean = mean; Minimum = minimum; Design = design; }
    }

    /// <summary>
    /// Axial capacity along the depth (pile) or the axis (micropile): curves by condition and direction, actions with the factored weight, details at
    /// every depth, reached depth and coverage.
    /// </summary>
    public sealed class AxialCapacityResult<TSurvey>
    {
        public IReadOnlyDictionary<(AxialCondition Condition, bool Compression), AxialCapacityCurve> Curves { get; internal set; } = null!;
        public IReadOnlyList<AxialDepthResult<TSurvey>> Depths { get; internal set; } = null!;
        /// <summary>NEd + γG,unfav W at every depth (N); empty without the action.</summary>
        public IReadOnlyList<(double Depth, double Value)> CompressionActions { get; internal set; } = null!;
        /// <summary>max(0; NEd,t − γG,fav W) at every depth (N); empty without the action.</summary>
        public IReadOnlyList<(double Depth, double Value)> TensionActions { get; internal set; } = null!;
        public double MaximumDepth { get; internal set; }
        public double PileLength { get; internal set; }
        public bool FullCoverage { get; internal set; }
        public int SurveyCount { get; internal set; }
        public PileGroupEfficiency Efficiency { get; internal set; } = null!;
        public PileResistanceFactors Factors { get; internal set; } = null!;
        public IReadOnlyList<string> Warnings { get; internal set; } = new string[0];
    }
}
