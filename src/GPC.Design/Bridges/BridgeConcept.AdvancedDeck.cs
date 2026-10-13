
namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    private sealed record AdvancedDeck(double Slab, double Spacing, int Girders, double ConcreteArea, double SteelArea,
        double SteelMass, double Inertia, double Web, double Bottom, TechnicalItem[] Dimensions);
    private static AdvancedDeck ExtendedDeck(BridgeInput i, Family family, double width, double length, double depth, bool ortho)
    {
        var dimensions = new List<TechnicalItem>();
        void Dim(string c, string s, double v, string u, string note) => dimensions.Add(new(c, s, v, u, "Adottato", note));
        double ec = ConcreteModulus(i.Fc), n = 200000 / ec;
        var parts = new List<(double A, double Y, double I, double N)>();
        void Rect(double b, double h, double y, int count, double modular)
        { if (b <= 0 || h <= 0) throw new ArgumentException("Dimensioni delle lamiere incompatibili con l’altezza disponibile."); parts.Add((b * h * count, y, b * h * h * h / 12 * count, modular)); }
        double tf = i.FlangeMm / 1000, tw = i.WebMm / 1000, bf = i.FlangeWidth;
        double spacing = Auto(i.Spacing, family.Spacing > 0 ? family.Spacing : 3), slab, ac, steel;
        int girders; double bottom;
        if (family.Id == "filler_beam")
        {
            slab = depth; bottom = i.EmbeddedCover; double h = depth - 2 * bottom;
            girders = Math.Max(2, (int)Math.Floor(width / spacing));
            if (girders > 64 || bf >= width / girders - .05 || h <= 2 * tf + .1)
                throw new ArgumentException("Travi incorporate: controllare copriferro, interasse, piattabande e altezza netta.");
            steel = girders * (2 * bf * tf + tw * (h - 2 * tf)); ac = width * depth - steel;
            if (ac <= 0) throw new ArgumentException("Travi incorporate: il volume di acciaio supera l’ingombro della soletta.");
            // Concrete gross rectangle + (n-1) steel replaces, rather than duplicates, the displaced concrete.
            Rect(width, depth, depth / 2, 1, 1);
            Rect(bf, tf, bottom + tf / 2, girders, n - 1); Rect(bf, tf, depth - bottom - tf / 2, girders, n - 1);
            Rect(tw, h - 2 * tf, depth / 2, girders, n - 1);
            Dim("Trave incorporata", "c", bottom * 1000, "mm", "Cls sopra e sotto le piattabande, nel modello ideale completamente inglobato");
            Dim("Trave incorporata", "h", h, "m", "Altezza del profilo a I equivalente; non un profilo di catalogo");
            Dim("Trave incorporata", "b_f", bf * 1000, "mm", "Larghezza piattabanda");
            Dim("Trave incorporata", "t_f", tf * 1000, "mm", "Spessore piattabande");
            Dim("Trave incorporata", "t_w", tw * 1000, "mm", "Spessore anima");
        }
        else if (ortho)
        {
            slab = i.DeckPlateMm / 1000; bottom = tf;
            girders = (int)Auto(i.Boxes, Math.Max(1, Math.Round(width / 10))); spacing = width / girders;
            double rh = i.RibHeight, rt = i.RibMm / 1000, rb = i.RibBottom, ru = i.RibTop;
            int ribs = Math.Max(1, (int)Math.Floor(width / i.RibSpacing));
            if (ru <= rb || ru >= width / ribs || rh + slab + tf >= depth || ru <= 2 * rt || rb <= 2 * rt)
                throw new ArgumentException("Piastra ortotropa: canalette incompatibili con interasse, larghezze, spessori o altezza del cassone.");
            double webH = depth - slab - tf, boxB = width * .55 / girders;
            double developed = Math.Sqrt(rh * rh + Math.Pow((ru - rb) / 2, 2));
            steel = width * slab + ribs * rt * (rb + 2 * developed) + girders * (boxB * tf + 2 * tw * webH); ac = 0;
            Rect(width, slab, depth - slab / 2, 1, n); Rect(boxB, tf, tf / 2, girders, n);
            Rect(tw, webH, tf + webH / 2, 2 * girders, n);
            Rect(rb, rt, depth - slab - rh + rt / 2, ribs, n);
            // Sloping thin walls: area uses developed length; vertical inertia uses actual rise.
            parts.Add((2 * ribs * rt * developed, depth - slab - rh / 2, 2 * ribs * rt * developed * rh * rh / 12, n));
            Dim("Piastra ortotropa", "t_p", slab * 1000, "mm", "Lamiera carrabile; assenza di soletta in cls");
            Dim("Canalette", "n_r", ribs, "n.", "Canalette longitudinali per sezione");
            Dim("Canalette", "s_r", width / ribs, "m", "Interasse adottato W/n");
            Dim("Canalette", "h_r", rh, "m", "Altezza della canaletta");
            Dim("Canalette", "b_sup", ru, "m", "Larghezza alla lamiera carrabile");
            Dim("Canalette", "b_inf", rb, "m", "Larghezza inferiore");
            Dim("Canalette", "t_r", rt * 1000, "mm", "Spessore delle tre pareti");
            Dim("Cassone", "b_inf", boxB, "m", "Fondo per cassone = 0,55 W/n");
            Dim("Cassone", "t_inf", tf * 1000, "mm", "Spessore lamiera inferiore");
            Dim("Cassone", "t_w", tw * 1000, "mm", "Due anime verticali per cassone");
        }
        else
        {
            slab = Auto(i.Slab, .25); bottom = tf;
            girders = Math.Max(2, (int)Math.Floor(width / spacing));
            double h = depth - slab;
            if (girders > 64 || h <= 2 * tf + .1 || bf >= width / girders)
                throw new ArgumentException("Impalcato misto: altezza, interasse o piattabande incompatibili.");
            steel = girders * (2 * bf * tf + tw * (h - 2 * tf)); ac = width * slab;
            Rect(width, slab, depth - slab / 2, 1, 1); Rect(bf, tf, tf / 2, girders, n);
            Rect(bf, tf, h - tf / 2, girders, n); Rect(tw, h - 2 * tf, h / 2, girders, n);
            Dim("Travi impalcato", "h", h, "m", "Travi longitudinali secondarie sotto la soletta");
            Dim("Travi impalcato", "b_f", bf * 1000, "mm", "Larghezza piattabanda");
            Dim("Travi impalcato", "t_f", tf * 1000, "mm", "Spessore piattabande");
            Dim("Travi impalcato", "t_w", tw * 1000, "mm", "Spessore anima");
        }
        Dim("Impalcato", "n", girders, "n.", "Travi o cassoni longitudinali");
        Dim("Impalcato", "s", spacing, "m", "Interasse di riferimento");
        Dim("Impalcato", "A_cls", ac, "m²", "Area netta di calcestruzzo usata nelle quantità");
        Dim("Impalcato", "A_acc", steel, "m²", "Area di acciaio longitudinale prima dell’aggiunta per collegamenti e traversi");
        double ybar = parts.Sum(p => p.A * p.N * p.Y) / parts.Sum(p => p.A * p.N);
        double inertia = parts.Sum(p => p.N * (p.I + p.A * Math.Pow(p.Y - ybar, 2)));
        double factor = family.Id == "filler_beam" ? 1.05 : 1.15;
        Dim("Impalcato", "k_acc", factor, "—", "Maggiorazione di massa per accessori / traversi; esclusa dalla rigidezza");
        return new(slab, spacing, girders, ac, steel, steel * length * SteelTonnesPerCubicMetre * factor, inertia, tw, bottom, dimensions.ToArray());
    }
}
