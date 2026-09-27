
namespace GPC.Checkers.CompositeBridge;

public sealed record BridgeBar(string Id, double X, double Y, double Diameter, double Area);
/// <summary>
/// The geometry of the composite section. For the analysis N–Mx about the horizontal axis every steel section is the equivalent H: WebThickness is
/// the total horizontal width of the webs (WebCount tw / cos α) and TopWidth the total width of the top flanges, so that area and moments about
/// the horizontal axis are the real ones. The real plates, for local buckling, shear and details, are described by the last parameters
/// </summary>
/// <param name="Width">Effective width of the slab [mm]</param>
/// <param name="SlabHeight">Thickness of the slab</param>
/// <param name="WebHeight">Vertical height of the webs between the flanges</param>
/// <param name="WebThickness">Total horizontal width of the webs of the equivalent H (WebCount tw / cos α; the H: tw)</param>
/// <param name="TopWidth">Total width of the top flanges</param>
/// <param name="TopThickness">Thickness of the top flanges</param>
/// <param name="Bottom1Width">Width of the (first) bottom plate</param>
/// <param name="Bottom1Thickness">Thickness of the (first) bottom plate</param>
/// <param name="Bottom2Width">Width of the second bottom plate (0: none)</param>
/// <param name="Bottom2Thickness">Thickness of the second bottom plate (0: none)</param>
/// <param name="BottomEquivalentWidth">Width of the equivalent bottom plate (same area and total thickness)</param>
/// <param name="BottomEquivalentThickness">Total thickness of the bottom plates</param>
/// <param name="Height">Height of the steel section</param>
/// <param name="BottomArea">Area of the bottom plates</param>
/// <param name="BottomRealCentroid">Level of the centroid of the bottom plates</param>
/// <param name="BottomRealInertia">Own moment of inertia of the bottom plates</param>
/// <param name="SteelArea">Area of the real steel section</param>
/// <param name="SteelInertia">Moment of inertia of the real steel section about the horizontal axis</param>
/// <param name="SteelCentroid">Level of the centroid of the steel (0 at the top of the steel)</param>
/// <param name="Bars">The bars of the slab</param>
/// <param name="SectionType">The type of the steel section</param>
/// <param name="WebCount">The number of webs (2 for the box)</param>
/// <param name="WebPlateThickness">The thickness of each web, normal to its plane (0: WebThickness / WebCount)</param>
/// <param name="WebLength">The length of each web along its plane between the flanges, WebHeight / cos α (0: WebHeight)</param>
/// <param name="WebAngle">The angle of the webs from the vertical (radians)</param>
/// <param name="TopFlangeCount">The number of top flanges (2 for the box)</param>
/// <param name="WebSpacingTop">Box: distance between the axes of the webs at the top flanges</param>
/// <param name="WebSpacingBottom">Box: distance between the axes of the webs at the bottom flange</param>
public sealed record BridgeGeometry(double Width, double SlabHeight, double WebHeight, double WebThickness, double TopWidth, double TopThickness,
    double Bottom1Width, double Bottom1Thickness, double Bottom2Width, double Bottom2Thickness, double BottomEquivalentWidth, double BottomEquivalentThickness,
    double Height, double BottomArea, double BottomRealCentroid, double BottomRealInertia, double SteelArea, double SteelInertia, double SteelCentroid, BridgeBar[] Bars,
    BridgeSteelSectionType SectionType = BridgeSteelSectionType.H, int WebCount = 1, double WebPlateThickness = 0, double WebLength = 0, double WebAngle = 0,
    int TopFlangeCount = 1, double WebSpacingTop = 0, double WebSpacingBottom = 0)
{
    /// <summary>The thickness of each web plate</summary>
    public double PlateThickness => WebPlateThickness > 0 ? WebPlateThickness : WebThickness / WebCount;
    /// <summary>The length of each web plate between the flanges</summary>
    public double PlateLength => WebLength > 0 ? WebLength : WebHeight;
    /// <summary>The horizontal width of each web, tw / cos α</summary>
    public double WebHorizontalThickness => WebThickness / WebCount;
    /// <summary>The width of each top flange</summary>
    public double TopFlangeWidth => TopWidth / TopFlangeCount;
    /// <summary>Box: the clear width of the bottom flange between the webs (internal plate)</summary>
    public double BottomInternalWidth => SectionType == BridgeSteelSectionType.Box ? WebSpacingBottom - WebHorizontalThickness : 0;
    /// <summary>The width of each outstand of the bottom flange (box: beyond the webs, 0 if the flange ends at the webs)</summary>
    public double BottomOutstandWidth => SectionType == BridgeSteelSectionType.Box ? Math.Max(0, (Bottom1Width - WebSpacingBottom - WebHorizontalThickness) / 2)
        : (BottomEquivalentWidth - WebHorizontalThickness) / 2;
}
public sealed record BridgeMaterialValues(string Concrete, double Fck, double Ec, string Steel, double Fy, double Ea, string Rebar, double Fys, double Es);
public sealed record BridgePlate(double Width, double Thickness, double Psi, double KSigma, double Lambda, double Rho, double CompressedWidth,
    double EffectiveAtStart, double EffectiveAtEnd, double StartStress, double EndStress);
/// <summary>Effective steel geometry. With two bottom plates BottomWidth/Bottom are the first plate (welded to the web) and
/// SecondBottomWidth/SecondBottom the second one; with one plate SecondBottom is null. WebTop and WebBottom are the vertical heights of the effective
/// strips of the web (Web is the plate along its plane); TopWidth and BottomWidth are the total effective widths. Box: Bottom is the internal plate
/// of the bottom flange between the webs and BottomOutstand the outstand beyond the webs (null if there is none).</summary>
public sealed record BridgeEffective(double WebTop, double WebBottom, double TopWidth, double BottomWidth, BridgePlate Web, BridgePlate Top, BridgePlate Bottom,
    double SecondBottomWidth = 0, BridgePlate? SecondBottom = null, BridgePlate? BottomOutstand = null)
{
    public static BridgeEffective Full(BridgeGeometry g)
    {
        var plate = new BridgePlate(g.PlateLength, g.PlateThickness, 0, 0, 0, 1, 0, g.PlateLength / 2, g.PlateLength / 2, 0, 0);
        double twh = g.WebHorizontalThickness;
        BridgePlate Outstand(double width, double thickness) =>
            plate with { Width = (width - twh) / 2, Thickness = thickness, EffectiveAtStart = (width - twh) / 2, EffectiveAtEnd = 0 };
        var top = Outstand(g.TopFlangeWidth, g.TopThickness);
        if (g.SectionType == BridgeSteelSectionType.Box)
        {
            double internalWidth = g.BottomInternalWidth, outstand = g.BottomOutstandWidth;
            var internalPlate = plate with { Width = internalWidth, Thickness = g.Bottom1Thickness, EffectiveAtStart = internalWidth / 2, EffectiveAtEnd = internalWidth / 2 };
            return new(g.WebHeight / 2, g.WebHeight / 2, g.TopWidth, g.Bottom1Width, plate, top, internalPlate, 0, null,
                outstand > 0 ? plate with { Width = outstand, Thickness = g.Bottom1Thickness, EffectiveAtStart = outstand, EffectiveAtEnd = 0 } : null);
        }
        // the gross widths exactly (the H results do not change)
        bool two = g.Bottom2Thickness > 0;
        return new(g.WebHeight / 2, g.WebHeight / 2, g.TopWidth, two ? g.Bottom1Width : g.BottomEquivalentWidth, plate,
            top, two ? Outstand(g.Bottom1Width, g.Bottom1Thickness) : Outstand(g.BottomEquivalentWidth, g.BottomEquivalentThickness),
            two ? g.Bottom2Width : 0, two ? Outstand(g.Bottom2Width, g.Bottom2Thickness) : null);
    }
    public double Distance(BridgeEffective b, BridgeGeometry g) => new[] { Math.Abs(WebTop - b.WebTop) / g.WebHeight, Math.Abs(WebBottom - b.WebBottom) / g.WebHeight,
        Math.Abs(TopWidth - b.TopWidth) / g.TopWidth, Math.Abs(BottomWidth - b.BottomWidth) / (g.Bottom2Thickness > 0 ? g.Bottom1Width : g.BottomEquivalentWidth),
        g.Bottom2Thickness > 0 ? Math.Abs(SecondBottomWidth - b.SecondBottomWidth) / g.Bottom2Width : 0 }.Max();
    /// <summary>Dimensionless coordinates (the effective lengths over the gross ones), as in <see cref="Distance"/></summary>
    public double[] Coordinates(BridgeGeometry g) => new[] { WebTop / g.WebHeight, WebBottom / g.WebHeight, TopWidth / g.TopWidth,
        BottomWidth / (g.Bottom2Thickness > 0 ? g.Bottom1Width : g.BottomEquivalentWidth), g.Bottom2Thickness > 0 ? SecondBottomWidth / g.Bottom2Width : 0 };
    public BridgeEffective Relax(BridgeEffective b, double f) => this with { WebTop = WebTop * (1 - f) + b.WebTop * f, WebBottom = WebBottom * (1 - f) + b.WebBottom * f,
        TopWidth = TopWidth * (1 - f) + b.TopWidth * f, BottomWidth = BottomWidth * (1 - f) + b.BottomWidth * f,
        SecondBottomWidth = SecondBottomWidth * (1 - f) + b.SecondBottomWidth * f };
}
public sealed record BridgeContribution(string Name, string Kind, double N, double Mx, double N0, double HomogenizationN, double Phi, double EffectivePhi,
    double Area, double Centroid, double Inertia, double UniformStress, double StressSlope, double RebarRatio, double RebarArea, double WBottom, double? WTop, double SolverInertia, double EquilibriumResidual,
    double LoadY = 0, string LoadReference = HBridgeSection.CommonLoadReference,
    double V = 0, double ShrinkageStrain = 0, double ConcreteStressOffset = 0, double EquivalentN = 0, double EquivalentMomentAtInterface = 0, double ConnectionFlowExtra = 0)
{
    internal History.HistoryStressProfile? HistoryProfile;
    public History.HistoryStressProfile? GetHistoryProfile() => HistoryProfile;
    public bool IsShrinkage => Kind == HBridgeSection.ShrinkageKind;
    public bool HasConcrete => HBridgeSection.HasConcrete(Kind);
    public double MomentAtInterface => Mx - N * LoadY / 1000;
    public double SteelStress(double y) => HistoryProfile?.Stress("Acciaio", y) ?? UniformStress + StressSlope * (y - Centroid);
    public double Stress(string material, double y) => HistoryProfile?.Stress(material, y) ?? (material == "CLS" ? HasConcrete ? SteelStress(y) / HomogenizationN + ConcreteStressOffset : 0 :
        material == "Armatura" ? Kind == "Solo acciaio" ? 0 : SteelStress(y) * RebarRatio : SteelStress(y));
    public double? NeutralAxis => HistoryProfile is not null ? null : Math.Abs(StressSlope) < 1e-15 ? null : Centroid - UniformStress / StressSlope;
}
public sealed record BridgeStressPoint(string Name, string Material, double Y, double Stress, double Limit, double? Utilization, bool Active, double[] Contributions);
public sealed record BridgeSteelProperties(double Area, double Centroid, double Inertia);
public sealed record BridgeStage(string Name, int Iterations, double Residual, BridgeEffective Effective, BridgeSteelProperties EffectiveSteel, List<BridgeContribution> Contributions,
    List<BridgeStressPoint> Points, List<string> Warnings, BridgeShearResult? Shear = null, BridgeStudResult? Studs = null, BridgeTorsionResult? Torsion = null)
{
    internal History.HistoryStageView? HistoryView;
    public History.HistoryStageView? GetHistory() => HistoryView;
    public double MaxUtilization => Points.Where(p => p.Utilization.HasValue).Select(p => p.Utilization!.Value).DefaultIfEmpty().Max();
    public double? SteelNeutralAxis
    {
        get { if (HistoryView is not null) return null; double slope = Contributions.Sum(c => c.StressSlope); return Math.Abs(slope) < 1e-15 ? null : -Contributions.Sum(c => c.SteelStress(0)) / slope; }
    }
}
public sealed record HBridgeAnalysisResult(string Method, string Scope, HBridgeInput Input, BridgeGeometry Geometry, BridgeMaterialValues Materials, List<BridgeStage> Stages);
