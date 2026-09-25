
namespace GPC.Checkers.CompositeBridge;

public sealed record BridgeBar(string Id, double X, double Y, double Diameter, double Area);
public sealed record BridgeGeometry(double Width, double SlabHeight, double WebHeight, double WebThickness, double TopWidth, double TopThickness,
    double Bottom1Width, double Bottom1Thickness, double Bottom2Width, double Bottom2Thickness, double BottomEquivalentWidth, double BottomEquivalentThickness,
    double Height, double BottomArea, double BottomRealCentroid, double BottomRealInertia, double SteelArea, double SteelInertia, double SteelCentroid, BridgeBar[] Bars);
public sealed record BridgeMaterialValues(string Concrete, double Fck, double Ec, string Steel, double Fy, double Ea, string Rebar, double Fys, double Es);
public sealed record BridgePlate(double Width, double Thickness, double Psi, double KSigma, double Lambda, double Rho, double CompressedWidth,
    double EffectiveAtStart, double EffectiveAtEnd, double StartStress, double EndStress);
public sealed record BridgeEffective(double WebTop, double WebBottom, double TopWidth, double BottomWidth, BridgePlate Web, BridgePlate Top, BridgePlate Bottom)
{
    public static BridgeEffective Full(BridgeGeometry g)
    {
        var plate = new BridgePlate(g.WebHeight, g.WebThickness, 0, 0, 0, 1, 0, g.WebHeight / 2, g.WebHeight / 2, 0, 0);
        return new(g.WebHeight / 2, g.WebHeight / 2, g.TopWidth, g.BottomEquivalentWidth, plate,
            plate with { Width = (g.TopWidth - g.WebThickness) / 2, Thickness = g.TopThickness, EffectiveAtStart = (g.TopWidth - g.WebThickness) / 2, EffectiveAtEnd = 0 },
            plate with { Width = (g.BottomEquivalentWidth - g.WebThickness) / 2, Thickness = g.BottomEquivalentThickness, EffectiveAtStart = (g.BottomEquivalentWidth - g.WebThickness) / 2, EffectiveAtEnd = 0 });
    }
    public double Distance(BridgeEffective b, BridgeGeometry g) => new[] { Math.Abs(WebTop - b.WebTop) / g.WebHeight, Math.Abs(WebBottom - b.WebBottom) / g.WebHeight,
        Math.Abs(TopWidth - b.TopWidth) / g.TopWidth, Math.Abs(BottomWidth - b.BottomWidth) / g.BottomEquivalentWidth }.Max();
    public BridgeEffective Relax(BridgeEffective b, double f) => this with { WebTop = WebTop * (1 - f) + b.WebTop * f, WebBottom = WebBottom * (1 - f) + b.WebBottom * f,
        TopWidth = TopWidth * (1 - f) + b.TopWidth * f, BottomWidth = BottomWidth * (1 - f) + b.BottomWidth * f };
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
    List<BridgeStressPoint> Points, List<string> Warnings, BridgeShearResult? Shear = null, BridgeStudResult? Studs = null)
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
