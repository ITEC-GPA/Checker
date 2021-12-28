

namespace GPC.Checkers.Glasses.Wrappers
{
    public interface IGlassPanelWrapper
    {

        double GetElasticModulus();

        double GetPoissonRatio();

        double GetSelfWeightPerUnitArea();

        double GetSelfWeightTotal();

        double GetTotalThickness();

        double GetDensity();

    }
}
