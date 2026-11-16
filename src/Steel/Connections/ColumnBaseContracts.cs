using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Declared typology of the base. Only the unstiffened exposed base plate is computed;
    /// the other values exist so that a refusal is explicit and testable, never a silent pass.</summary>
    public enum ColumnBaseDetail { UnstiffenedExposedPlate, StiffenedPlate, EmbeddedColumn, HollowSectionColumn }

    /// <summary>Design situation of the declared actions. Only ordinary static ULS actions are computed.</summary>
    public enum ColumnBaseDesignSituation { OrdinaryStatic, SeismicCapacityDesign, Fatigue }

    /// <summary>OutsideFlanges: one row of two anchors beyond each flange, four anchors, rows at
    /// y=+/-RowDistance outside the column depth. BetweenFlanges: anchors between the flanges, one on
    /// each side of the web, two rows at y=+/-RowDistance, or one row on the major axis when
    /// RowDistance is zero; this layout is computed only in states without anchor tension.</summary>
    public enum ColumnBaseAnchorLayout { OutsideFlanges, BetweenFlanges }

    /// <summary>EN1993-1-8 6.2.6.12(4) fixing of the anchor into the foundation.</summary>
    public enum ColumnBaseAnchorage { Hook, WasherPlate, OtherQualifiedDevice }

    /// <summary>EN1993-1-8 6.2.2(6): sand-cement mortar has Cf,d=0.20; other grouts need a tested value.</summary>
    public enum ColumnBaseGrout { SandCementMortar, OtherTested }

    /// <summary>One anchor-bolt type; all anchors are identical. mm, mm2, MPa. TensileArea is the stress area
    /// As; HoleDiameter is the hole in the base plate. WasherThickness and NutHeight enter the EN anchor
    /// elongation length; MeanNutDiameter is dm of EN1993-1-8 Table3.4 for punching of the plate under the nut.
    /// Fyb/Fub are the nominal strengths of the actual product (bolt class or bar grade) recorded in MaterialSource.</summary>
    public sealed class ColumnBaseAnchor
    {
        public double Diameter { get; }
        public double TensileArea { get; }
        public double HoleDiameter { get; }
        public double Fyb { get; }
        public double Fub { get; }
        public bool ThreadsComplyWithEn1090 { get; }
        public double WasherThickness { get; }
        public double NutHeight { get; }
        public double MeanNutDiameter { get; }
        public ColumnBaseAnchorage Anchorage { get; }
        public string MaterialSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public ColumnBaseAnchor(double diameter, double tensileArea, double holeDiameter, double fyb, double fub,
            bool threadsComplyWithEn1090, double washerThickness, double nutHeight, double meanNutDiameter,
            ColumnBaseAnchorage anchorage, string materialSource, string holeClearanceServiceabilitySource = null)
        {
            foreach (double v in new[] { diameter, tensileArea, holeDiameter, fyb, fub, nutHeight, meanNutDiameter })
                ConnectionValidation.Positive(v, nameof(v));
            ConnectionValidation.Nonnegative(washerThickness, nameof(washerThickness));
            if (!Enum.IsDefined(typeof(ColumnBaseAnchorage), anchorage)) throw new ArgumentOutOfRangeException(nameof(anchorage));
            if (fub < fyb) throw new ArgumentException("fub must not be below fyb.");
            if (holeDiameter <= diameter || tensileArea > Math.PI * diameter * diameter / 4)
                throw new ArgumentException("Round clearance hole and tensile area within shank area required.");
            if (meanNutDiameter <= holeDiameter) throw new ArgumentException("The nut must cover the hole.");
            Diameter = diameter; TensileArea = tensileArea; HoleDiameter = holeDiameter; Fyb = fyb; Fub = fub;
            ThreadsComplyWithEn1090 = threadsComplyWithEn1090; WasherThickness = washerThickness; NutHeight = nutHeight;
            MeanNutDiameter = meanNutDiameter; Anchorage = anchorage; MaterialSource = materialSource;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>Doubly symmetric anchor pattern about both column axes, mm. X runs along the flanges,
    /// Y along the web, origin on the column axis. Gauge is the X distance between the two anchors of a row;
    /// RowDistance is |y| of each row measured from the major axis.</summary>
    public sealed class ColumnBaseAnchorPattern
    {
        public ColumnBaseAnchorLayout Layout { get; }
        public double Gauge { get; }
        public double RowDistance { get; }
        public string DetailSource { get; }
        public int AnchorCount => Layout == ColumnBaseAnchorLayout.BetweenFlanges && RowDistance == 0 ? 2 : 4;
        public ColumnBaseAnchorPattern(ColumnBaseAnchorLayout layout, double gauge, double rowDistance, string detailSource)
        {
            if (!Enum.IsDefined(typeof(ColumnBaseAnchorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            ConnectionValidation.Positive(gauge, nameof(gauge)); ConnectionValidation.Nonnegative(rowDistance, nameof(rowDistance));
            if (layout == ColumnBaseAnchorLayout.OutsideFlanges && !(rowDistance > 0))
                throw new ArgumentOutOfRangeException(nameof(rowDistance), "Rows outside the flanges need a positive distance.");
            Layout = layout; Gauge = gauge; RowDistance = rowDistance; DetailSource = detailSource;
        }
    }

    /// <summary>Concrete foundation and grout under the plate. Fck/GroutFck MPa, areas mm2, thickness mm.
    /// DesignDistributionArea is Ac1 of EN1992-1-1 6.7(2)-(3) derived by the caller for the WHOLE plate
    /// footprint as loaded area (similar shape, concentric, foundation edges and depth): equal to the
    /// footprint means no spreading. ConcentratedResistanceSource records that derivation and that no
    /// reduction for non-uniform load or high shear applies. FrictionCoefficient is used only for
    /// OtherTested grout, with its EN1990 Annex D test source.</summary>
    public sealed class ColumnBaseFoundation
    {
        public double Fck { get; }
        public double AlphaCC { get; }
        public double GammaC { get; }
        public double DesignDistributionArea { get; }
        public double GroutThickness { get; }
        public double GroutFck { get; }
        public ColumnBaseGrout Grout { get; }
        public double? FrictionCoefficient { get; }
        public string ConcentratedResistanceSource { get; }
        public string FrictionSource { get; }
        public ColumnBaseFoundation(double fck, double alphaCC, double gammaC, double designDistributionArea,
            double groutThickness, double groutFck, ColumnBaseGrout grout, string concentratedResistanceSource,
            double? frictionCoefficient = null, string frictionSource = null)
        {
            foreach (double v in new[] { fck, alphaCC, gammaC, designDistributionArea, groutThickness, groutFck })
                ConnectionValidation.Positive(v, nameof(v));
            if (!Enum.IsDefined(typeof(ColumnBaseGrout), grout)) throw new ArgumentOutOfRangeException(nameof(grout));
            if (frictionCoefficient.HasValue) ConnectionValidation.Positive(frictionCoefficient.Value, nameof(frictionCoefficient));
            Fck = fck; AlphaCC = alphaCC; GammaC = gammaC; DesignDistributionArea = designDistributionArea;
            GroutThickness = groutThickness; GroutFck = groutFck; Grout = grout; FrictionCoefficient = frictionCoefficient;
            ConcentratedResistanceSource = concentratedResistanceSource; FrictionSource = frictionSource;
        }
    }

    /// <summary>Design resistances per anchor of the CONCRETE failure modes (EN1992-4 or a product
    /// assessment: pull-out, cone, splitting, edge, pry-out, bond), for the actual group, edges and
    /// reinforcement, N. They are not EC3 quantities and are never computed or assumed here: a null value
    /// means not declared. Source identifies the calculation.</summary>
    public sealed class ColumnBaseAnchorConcreteResistance
    {
        public double? TensionPerAnchor { get; }
        public double? ShearPerAnchor { get; }
        public string Source { get; }
        public ColumnBaseAnchorConcreteResistance(double? tensionPerAnchor, double? shearPerAnchor, string source)
        {
            if (tensionPerAnchor.HasValue) ConnectionValidation.Positive(tensionPerAnchor.Value, nameof(tensionPerAnchor));
            if (shearPerAnchor.HasValue) ConnectionValidation.Positive(shearPerAnchor.Value, nameof(shearPerAnchor));
            TensionPerAnchor = tensionPerAnchor; ShearPerAnchor = shearPerAnchor; Source = source;
        }
    }

    /// <summary>Continuous equal-leg fillets all round the column on the plate: outer and inner flange
    /// faces (inner runs stop at the root radius) and both web faces over the clear web. Legs in mm,
    /// effective throat leg/sqrt(2). Grades are the nominal EN grades of the column and of the plate.</summary>
    public sealed class ColumnBaseWelds
    {
        public double FlangeLeg { get; }
        public double WebLeg { get; }
        public FilletWeldEndCondition FlangeEnds { get; }
        public FilletWeldEndCondition WebEnds { get; }
        public FilletWeldSteelGrade ColumnGrade { get; }
        public FilletWeldSteelGrade PlateGrade { get; }
        public string ConsumableCompatibilitySource { get; }
        public string LocalEccentricitySource { get; }
        public ColumnBaseWelds(double flangeLeg, double webLeg, FilletWeldEndCondition flangeEnds, FilletWeldEndCondition webEnds,
            FilletWeldSteelGrade columnGrade, FilletWeldSteelGrade plateGrade, string consumableCompatibilitySource, string localEccentricitySource)
        {
            ConnectionValidation.Positive(flangeLeg, nameof(flangeLeg)); ConnectionValidation.Positive(webLeg, nameof(webLeg));
            foreach (var ends in new[] { flangeEnds, webEnds })
                if (!Enum.IsDefined(typeof(FilletWeldEndCondition), ends)) throw new ArgumentOutOfRangeException(nameof(ends));
            foreach (var grade in new[] { columnGrade, plateGrade })
                if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            FlangeLeg = flangeLeg; WebLeg = webLeg; FlangeEnds = flangeEnds; WebEnds = webEnds;
            ColumnGrade = columnGrade; PlateGrade = plateGrade;
            ConsumableCompatibilitySource = consumableCompatibilitySource; LocalEccentricitySource = localEccentricitySource;
        }
    }

    /// <summary>Declared data of the rotational stiffness (EN1993-1-8 6.3.4, Table6.11 k13): elastic modulus of the
    /// foundation concrete Ec, MPa (e.g. Ecm of EN1992-1-1 Table3.1 or NTC2018 (11.2.5)), never assumed, with its source.</summary>
    public sealed class ColumnBaseStiffnessDeclaration
    {
        public double ConcreteModulus { get; }
        public string Source { get; }
        public ColumnBaseStiffnessDeclaration(double concreteModulus, string source)
        {
            ConnectionValidation.Positive(concreteModulus, nameof(concreteModulus));
            ConcreteModulus = concreteModulus; Source = source;
        }
    }

    /// <summary>Rotational stiffness of the base at the actual actions, EN1993-1-8 6.3.4 Table6.12 with 6.3.1(4)-(6):
    /// Sj = e/(e+ek) E z^2/(mu sum 1/ki). Nmm/rad; stiffness coefficients and eccentricities in mm.
    /// The value belongs to ONE row of Table6.12 (which sides are in tension), i.e. to the eccentricity interval
    /// [EccentricityMin, EccentricityMax] of that row (null = unbounded), under proportional loading. Outside it another row
    /// applies; with HingeBeyondRange (no anchor tension capacity) the base offers no rotational restraint beyond it.</summary>
    public sealed class ColumnBaseRotationalStiffness
    {
        public double InitialStiffness { get; }
        public double Stiffness { get; }
        public double Mu { get; }
        public double LeverArm { get; }
        /// <summary>e = M1/N of the actual state, mm; null for N = 0 (pure moment, the limit e/(e+ek) = 1 is used).</summary>
        public double? Eccentricity { get; }
        public double Ek { get; }
        public double SideAStiffnessCoefficient { get; }
        public double SideBStiffnessCoefficient { get; }
        /// <summary>Lower bound of e = M1/N for the Table6.12 row of this value, mm; null when unbounded.</summary>
        public double? EccentricityMin { get; }
        /// <summary>Upper bound of e = M1/N for the Table6.12 row of this value, mm; null when unbounded.</summary>
        public double? EccentricityMax { get; }
        /// <summary>Moments M1 = e N at the bounds of the interval with the actual N, Nmm (ascending); null when N = 0 or unbounded.</summary>
        public double? MomentAtLowerLimit { get; }
        public double? MomentAtUpperLimit { get; }
        /// <summary>True when the base has no anchor tension capacity (anchors between the flanges without the opt-in): beyond the
        /// both-compression interval it is a hinge for M1, not a semi-rigid joint.</summary>
        public bool HingeBeyondRange { get; }
        internal ColumnBaseRotationalStiffness(double initial, double stiffness, double mu, double z, double? e, double ek, double ka, double kb,
            double? eMin, double? eMax, double? mLow, double? mHigh, bool hinge)
        {
            InitialStiffness = initial; Stiffness = stiffness; Mu = mu; LeverArm = z; Eccentricity = e; Ek = ek; SideAStiffnessCoefficient = ka; SideBStiffnessCoefficient = kb;
            EccentricityMin = eMin; EccentricityMax = eMax; MomentAtLowerLimit = mLow; MomentAtUpperLimit = mHigh; HingeBeyondRange = hinge;
        }
    }

    /// <summary>Qualified design moment resistance Mc,Rd of the column cross-section at the base, Nmm,
    /// already reduced for the concomitant shear V2 (and for the axial force where the caller's section
    /// policy requires it). Used only for Fc,fc,Rd=Mc,Rd/(h-tf), EN1993-1-8 6.2.6.7.</summary>
    public sealed class ColumnBaseColumnMomentResistance
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Column { get; }
        public double Value { get; }
        public double ConcomitantShear { get; }
        public string Source { get; }
        public ColumnBaseColumnMomentResistance(ConnectionDesignCode code, ConnectionRolledISection column,
            double value, double concomitantShear, string source)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            ConnectionValidation.Positive(value, nameof(value)); ConnectionValidation.Nonnegative(concomitantShear, nameof(concomitantShear));
            Value = value; ConcomitantShear = concomitantShear; Source = source;
        }
    }

    /// <summary>Base of one I/H column (rolled, or welded with RootRadius=0) welded concentrically to a
    /// rectangular unstiffened plate on grout and concrete. Plate.Width is bp along the flanges (X),
    /// PlateLength is hp along the web (Y). Actions are the column internal forces at the base cut in the
    /// SteelActions convention: N&gt;0 tension; M1 major axis, M1&gt;0 tensions the +Y flange (side A);
    /// V2 along the web (Y), V1 along the flanges (X). M2 and T must be zero in this field.
    /// ColumnFlangeFu is the flange ultimate strength for the weld material.</summary>
    public sealed class ColumnBaseInput
    {
        public ConnectionDesignCode Code { get; }
        public ColumnBaseDetail Detail { get; }
        public ColumnBaseDesignSituation Situation { get; }
        public ConnectionRolledISection Column { get; }
        public double ColumnFlangeFu { get; }
        public ConnectionPlate Plate { get; }
        public double PlateLength { get; }
        public ColumnBaseAnchor Anchor { get; }
        public ColumnBaseAnchorPattern Pattern { get; }
        public ColumnBaseFoundation Foundation { get; }
        public ColumnBaseAnchorConcreteResistance AnchorConcrete { get; }
        public ColumnBaseWelds Welds { get; }
        public ColumnBaseColumnMomentResistance ColumnMomentResistance { get; }
        public SteelActions Actions { get; }
        public string ActionCombinationSource { get; }
        /// <summary>Opt-in anchor tension between the flanges (BetweenFlanges, RowDistance &gt; 0): alpha of the bolt row
        /// next to the tension flange read from EN1993-1-8 Figure 6.11 for this geometry, with its source. Null keeps the
        /// refusal ANCHOR_TENSION_BETWEEN_FLANGES_NOT_IMPLEMENTED.</summary>
        public TStubChartAlpha InternalRowAlpha { get; private set; }
        /// <summary>Opt-in alternative to InternalRowAlpha: alpha from the SCI P398 Appendix G inversion of Figure 6.11,
        /// chosen explicitly with this source; never substituted silently.</summary>
        public string InternalRowAlphaSciP398Source { get; private set; }
        /// <summary>Opt-in rotational stiffness (EN1993-1-8 6.3.4): declared concrete modulus and its source.</summary>
        public ColumnBaseStiffnessDeclaration Stiffness { get; private set; }
        /// <summary>Opt-in: anchors in tension are credited in shear with the EN1993-1-8 Table3.4 combined rule when no
        /// prying develops; the source states that the declared concrete resistances hold for the combined tension and
        /// shear of the same state (EN1992-4 interaction, outside this method). Null keeps tensioned anchors out of shear.</summary>
        public string TensionShearInteractionSource { get; private set; }

        public ColumnBaseInput WithInternalRowAlpha(TStubChartAlpha alpha)
        { var c = Copy(); c.InternalRowAlpha = alpha ?? throw new ArgumentNullException(nameof(alpha)); return c; }
        public ColumnBaseInput WithSciP398InternalRowAlpha(string source)
        { var c = Copy(); c.InternalRowAlphaSciP398Source = source ?? throw new ArgumentNullException(nameof(source)); return c; }
        public ColumnBaseInput WithStiffness(ColumnBaseStiffnessDeclaration stiffness)
        { var c = Copy(); c.Stiffness = stiffness ?? throw new ArgumentNullException(nameof(stiffness)); return c; }
        public ColumnBaseInput WithTensionShearInteraction(string concreteInteractionSource)
        { var c = Copy(); c.TensionShearInteractionSource = concreteInteractionSource ?? throw new ArgumentNullException(nameof(concreteInteractionSource)); return c; }
        private ColumnBaseInput Copy() => new ColumnBaseInput(Code, Detail, Situation, Column, ColumnFlangeFu, Plate, PlateLength, Anchor, Pattern,
            Foundation, AnchorConcrete, Welds, ColumnMomentResistance, Actions, ActionCombinationSource)
        { InternalRowAlpha = InternalRowAlpha, InternalRowAlphaSciP398Source = InternalRowAlphaSciP398Source, Stiffness = Stiffness,
          TensionShearInteractionSource = TensionShearInteractionSource };

        public ColumnBaseInput(ConnectionDesignCode code, ColumnBaseDetail detail, ColumnBaseDesignSituation situation,
            ConnectionRolledISection column, double columnFlangeFu, ConnectionPlate plate, double plateLength,
            ColumnBaseAnchor anchor, ColumnBaseAnchorPattern pattern, ColumnBaseFoundation foundation,
            ColumnBaseAnchorConcreteResistance anchorConcrete, ColumnBaseWelds welds,
            ColumnBaseColumnMomentResistance columnMomentResistance, SteelActions actions, string actionCombinationSource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (!Enum.IsDefined(typeof(ColumnBaseDetail), detail)) throw new ArgumentOutOfRangeException(nameof(detail));
            if (!Enum.IsDefined(typeof(ColumnBaseDesignSituation), situation)) throw new ArgumentOutOfRangeException(nameof(situation));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            Plate = plate ?? throw new ArgumentNullException(nameof(plate));
            Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
            AnchorConcrete = anchorConcrete ?? throw new ArgumentNullException(nameof(anchorConcrete));
            Welds = welds ?? throw new ArgumentNullException(nameof(welds));
            ColumnMomentResistance = columnMomentResistance ?? throw new ArgumentNullException(nameof(columnMomentResistance));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ConnectionValidation.Positive(columnFlangeFu, nameof(columnFlangeFu)); ConnectionValidation.Positive(plateLength, nameof(plateLength));
            Detail = detail; Situation = situation; ColumnFlangeFu = columnFlangeFu; PlateLength = plateLength;
            ActionCombinationSource = actionCombinationSource;
        }
    }
}
