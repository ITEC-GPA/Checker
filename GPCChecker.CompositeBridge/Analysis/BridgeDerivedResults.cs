namespace GPC.Checkers.CompositeBridge;

/// <summary>Mechanical quantities shared by reports and views, without adding fields to the archive result schema.</summary>
public static class BridgeDerivedResults
{
    /// <summary>Curvature in 1/mm, with the same sign as positive Mx.</summary>
    public static double Curvature(this BridgeContribution phase, double steelElasticModulus) =>
        -phase.StressSlope / steelElasticModulus;

    /// <summary>Effective concrete modulus in MPa, from the structural-steel modular ratio.</summary>
    public static double EffectiveConcreteModulus(double steelElasticModulus, double modularRatio) =>
        steelElasticModulus / modularRatio;

    /// <summary>Reinforcing-steel / effective-concrete modulus ratio.</summary>
    public static double RebarModularRatio(double structuralModularRatio, double rebarElasticModulus, double steelElasticModulus) =>
        structuralModularRatio * rebarElasticModulus / steelElasticModulus;

    public static BridgeSectionProperties EquivalentBottomPlate(this BridgeGeometry geometry) =>
        CompositeSectionProperties.RectangleProperties("Piattabanda inferiore equivalente", geometry.BottomEquivalentWidth,
            geometry.BottomEquivalentThickness, -geometry.Height, geometry.Width / 2);
}
