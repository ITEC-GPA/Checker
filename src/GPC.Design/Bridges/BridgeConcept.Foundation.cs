
namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    // Centred service load only. Include the cap weight in the automatic sizing loop.
    private static (int Piles, double Length, double Width, double Thickness, double Ratio, double Grid) SizeFoundation(
        BridgeInput input, double axial, double pressure, double diameter, double resistance, double minimumLength, double minimumWidth)
    {
        bool autoLength = input.FootingSize == 0, autoPiles = input.PileCount == 0;
        int piles = diameter == 0 ? 0 : (int)Auto(input.PileCount, Math.Max(4, 2 * Math.Ceiling(axial / resistance / 2)));
        double grid = diameter == 0 ? 0 : (Math.Ceiling(Math.Sqrt(piles)) - 1) * 3 * diameter + 2 * diameter;
        double length = Auto(input.FootingSize, Math.Ceiling(Math.Max(minimumLength, diameter == 0 ? Math.Sqrt(axial / (.85 * pressure)) : grid) * 4) / 4);
        for (int attempt = 0; attempt < 1024; attempt++)
        {
            grid = diameter == 0 ? 0 : (Math.Ceiling(Math.Sqrt(piles)) - 1) * 3 * diameter + 2 * diameter;
            if (autoLength) length = Math.Max(length, Math.Ceiling(grid * 4) / 4);
            double width = Math.Max(length, minimumWidth), thickness = diameter == 0 ? Math.Max(.6, length / 6) : 1.5 * diameter;
            double load = axial + 25 * length * width * thickness;
            double ratio = load / (diameter == 0 ? length * width * pressure : piles * resistance);
            if (ratio <= 1 + 1e-10 || (diameter == 0 ? !autoLength : !autoPiles))
                return (piles, length, width, thickness, ratio, grid);
            if (diameter == 0) length += .25;
            else piles = Math.Max(piles + 2, (int)(2 * Math.Ceiling(load / resistance / 2)));
            if (piles > 10000) break;
        }
        throw new ArgumentException("Fondazione automatica non convergente: capacità del terreno / palo insufficiente rispetto ai pesi propri. Rivedere i parametri geotecnici.");
    }

    private static double AutoPierSize(BridgeInput input, double height, double reaction, double capVolume, double width, int columns, bool wall)
    {
        if (input.PierSize > 0) return input.PierSize;
        double available = .3 * input.FcSub * 1000 - 25 * height;
        if (available <= 0) throw new ArgumentException("Altezza pila incompatibile con la soglia di compressione indicativa.");
        double area = Math.Max(0, reaction + 25 * capVolume) / available;
        double size = wall ? Math.Max(Math.Max(1, 2 * height * Math.Sqrt(12) / 90), area / Math.Max(1, width - 2))
            : Math.Max(Math.Max(1.2, 8 * height / 90), Math.Sqrt(4 * area / (Math.PI * columns)));
        return Math.Ceiling(size * 20) / 20;
    }
}
