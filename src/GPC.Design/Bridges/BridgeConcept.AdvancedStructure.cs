
namespace GPC.Design.Bridges;

public static partial class BridgeConcept
{
    private sealed record UpperStructure(StructuralLine[] Members, double Steel, double Cables, double Rise, double Tower, double Horizontal);
    private static UpperStructure ExtendedMembers(BridgeInput i, BridgeAssumptions a, string id, double[] spans, double q)
    {
        var lines = new List<StructuralLine>(); double rise = 0, tower = 0, horizontal = 0;
        double tension = a.ConceptTension * 1000, compression = a.ConceptCompression * 1000, cable = a.ConceptCable * 1000;
        void Member(string kind, double x1, double z1, double x2, double z2, int count, double force, double stress) =>
            lines.Add(new(kind, x1, z1, x2, z2, count, Math.Max(1e-5, Math.Abs(force) / stress), force));
        double offset = 0;
        if (id is "tied_arch" or "truss") foreach (double l in spans)
        {
            double f = l * (id == "tied_arch" ? i.ArchRise : i.TrussRatio); rise = Math.Max(rise, f);
            int panels = Math.Max(4, (int)Math.Ceiling(l / i.SuspenderSpacing)); double panel = l / panels;
            double H = q * l * l / (8 * f) / 2; horizontal = Math.Max(horizontal, 2 * H);
            if (id == "tied_arch")
            {
                double force = Math.Sqrt(H * H + Math.Pow(q * l / 4, 2));
                double Z(double x) => 4 * f * x / l * (1 - x / l);
                for (int k = 0; k < 80; k++) Member("Arco", offset + l * k / 80, Z(l * k / 80), offset + l * (k + 1) / 80, Z(l * (k + 1) / 80), 2, -force, compression);
                Member("Catena", offset, 0, offset + l, 0, 2, H, tension);
                for (int k = 1; k < panels; k++) Member("Pendino", offset + k * panel, 0, offset + k * panel, Z(k * panel), 2, q * panel / 2, cable);
            }
            else
            {
                Member("Corrente inferiore", offset, 0, offset + l, 0, 2, H, tension);
                Member("Corrente superiore", offset, f, offset + l, f, 2, -H, compression);
                double diagonal = Math.Sqrt(panel * panel + f * f), force = q * l / 4 * diagonal / f;
                for (int k = 0; k < panels; k++)
                {
                    Member("Diagonale", offset + k * panel, k % 2 == 0 ? 0 : f, offset + (k + 1) * panel, k % 2 == 0 ? f : 0, 2, -force, compression);
                    Member("Montante", offset + k * panel, 0, offset + k * panel, f, 2, -q * panel / 2, compression);
                }
                Member("Montante", offset + l, 0, offset + l, f, 2, -q * panel / 2, compression);
            }
            offset += l;
        }
        if (HasTowers(id))
        {
            double l = spans[1], side = spans[0], length = spans.Sum();
            tower = Auto(i.TowerHeight, Math.Max(10, l * .2));
            if (id == "cable_stayed")
            {
                foreach (var (start, end, xTower) in new[] { (0d, side, side), (side, length / 2, side), (length / 2, side + l, side + l), (side + l, length, side + l) })
                {
                    int panels = Math.Max(2, (int)Math.Ceiling((end - start) / i.SuspenderSpacing));
                    double step = (end - start) / panels, fanH = 0;
                    for (int k = 0; k < panels; k++)
                    {
                        double x = start + (k + .5) * step, cableLength = Math.Sqrt(Math.Pow(x - xTower, 2) + tower * tower);
                        double force = q * step / 2 * cableLength / tower;
                        Member("Strallo", x, 0, xTower, tower, 2, force, cable); fanH += q * step * Math.Abs(x - xTower) / tower;
                    }
                    horizontal = Math.Max(horizontal, fanH);
                }
            }
            else
            {
                rise = l * i.CableSag;
                if (tower <= rise + 2) throw new ArgumentException("Ponte sospeso: l’antenna deve superare la freccia del cavo di almeno 2 m.");
                double H = q * l * l / (8 * rise) / 2; horizontal = 2 * H;
                double t = Math.Sqrt(H * H + Math.Pow(q * l / 4, 2));
                double Z(double x) => tower - 4 * rise * x / l * (1 - x / l);
                for (int k = 0; k < 100; k++) Member("Cavo principale", side + l * k / 100, Z(l * k / 100), side + l * (k + 1) / 100, Z(l * (k + 1) / 100), 2, t, cable);
                // Side spans are also suspended: sag relative to the inclined chord = f/4.
                // Using straight unloaded backstays would omit support for the side deck.
                double back = Math.Sqrt(H * H + Math.Pow(H * tower / side + q * side / 4, 2));
                double SideZ(double x) => tower * x / side - rise * x / side * (1 - x / side);
                for (int k = 0; k < 50; k++)
                {
                    double x1 = side * k / 50, x2 = side * (k + 1) / 50;
                    Member("Cavo di riva", x1, SideZ(x1), x2, SideZ(x2), 2, back, cable);
                    Member("Cavo di riva", length - x2, SideZ(x2), length - x1, SideZ(x1), 2, back, cable);
                }
                int sidePanels = Math.Max(2, (int)Math.Ceiling(side / i.SuspenderSpacing));
                for (int k = 1; k < sidePanels; k++)
                {
                    double x = side * k / sidePanels;
                    Member("Pendino", x, 0, x, SideZ(x), 2, q * side / sidePanels / 2, cable);
                    Member("Pendino", length - x, 0, length - x, SideZ(x), 2, q * side / sidePanels / 2, cable);
                }
                int panels = Math.Max(4, (int)Math.Ceiling(l / i.SuspenderSpacing)); double step = l / panels;
                for (int k = 1; k < panels; k++) Member("Pendino", side + k * step, 0, side + k * step, Z(k * step), 2, q * step / 2, cable);
            }
        }
        bool IsCable(StructuralLine m) => m.Kind is "Pendino" or "Strallo" or "Cavo principale" or "Cavo di riva";
        return new(lines.ToArray(), lines.Where(m => !IsCable(m)).Sum(m => m.Mass) * (1 + a.SpecialConnections / 100),
            lines.Where(IsCable).Sum(m => m.Mass), rise, tower, horizontal);
    }
    private static double[] ExtendedReactions(string id, double[] spans, double q, UpperStructure structure)
    {
        if (id == "cable_stayed") return [0, q * spans.Sum() / 2, q * spans.Sum() / 2, 0];
        if (id == "suspension")
        {
            double H = q * spans[1] * spans[1] / (8 * structure.Rise), backVertical = H * structure.Tower / spans[0];
            double end = q * spans[0] / 2 - backVertical, tower = q * (spans[0] + spans[1]) / 2 + backVertical;
            return [end, tower, tower, end];
        }
        var reactions = new double[spans.Length + 1];
        for (int k = 0; k < spans.Length; k++) { reactions[k] += q * spans[k] / 2; reactions[k + 1] += q * spans[k] / 2; }
        return reactions;
    }
}
