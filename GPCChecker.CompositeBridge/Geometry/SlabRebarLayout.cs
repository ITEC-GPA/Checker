namespace GPC.Checkers.CompositeBridge;

public enum SlabBottomReference { SlabSoffit, PredalleTop }

/// <summary>Geometric rebar axis references in mm. Predalle is contained in the total homogeneous slab;
/// this contract introduces no extra material, weight, reinforcement or construction stage.</summary>
public sealed class SlabRebarLayout
{
    public bool HasPredalle { get; }
    public double PredalleThickness { get; }
    public double Height { get; }
    public SlabBottomReference BottomReference { get; }
    public double TopAxisY { get; }
    public double BottomAxisY { get; }
    public SlabRebarLayout(bool hasPredalle, double predalleThickness, double height, SlabBottomReference reference,
        double topAxisDistance, double bottomAxisDistance, bool bottomBars, double bottomDiameter)
    {
        if (!Finite(height) || height <= 0 || !Finite(topAxisDistance) || !Finite(bottomAxisDistance))
            throw new ArgumentException("Slab height and rebar axis distances must be finite; height must be positive.");
        if (hasPredalle && (!Finite(predalleThickness) || predalleThickness <= 0 || predalleThickness >= height))
            throw new ArgumentException("Predalle: inserire uno spessore maggiore di zero e minore dello spessore totale della soletta.");
        if (!Enum.IsDefined(typeof(SlabBottomReference), reference)) throw new ArgumentException("Selezionare il riferimento geometrico dei ferri inferiori.");
        if (hasPredalle && bottomBars && reference == SlabBottomReference.PredalleTop &&
            (!Finite(bottomDiameter) || bottomDiameter <= 0 || bottomAxisDistance <= 0 || bottomAxisDistance < bottomDiameter / 2))
            throw new ArgumentException("Ferri inferiori: la distanza dall'estradosso predalle all'asse deve essere positiva e almeno pari al raggio della barra.");
        HasPredalle = hasPredalle; PredalleThickness = hasPredalle ? predalleThickness : 0; Height = height;
        BottomReference = hasPredalle ? reference : SlabBottomReference.SlabSoffit;
        TopAxisY = height - topAxisDistance;
        BottomAxisY = bottomAxisDistance + (hasPredalle && reference == SlabBottomReference.PredalleTop ? predalleThickness : 0);
    }
    static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
