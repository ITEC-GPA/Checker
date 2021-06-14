

namespace GPC.Checkers.Glasses.Wrappers
{
    public interface IGlassPanelWrapper
    {

        double GetElasticModulus();

        double GetPoissonRatios();

        double GetSelfWeightPerUnitArea();

        double GetSelfWeightTotal();

        double GetTotalThickness();

        double GetDensity();

    }
}
