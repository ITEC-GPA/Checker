namespace GPC.Checkers.CompositeBridge.Viviani;

/// <summary>A stress of the comparison [MPa]: the method of prof. Viviani and the linear calculation of the library at the same point</summary>
/// <param name="Point">The point of the method (1 ... 6)</param>
/// <param name="Name">The name of the point</param>
/// <param name="Y">Level in the system of the library [mm] (0 at the bottom of the slab)</param>
/// <param name="Viviani">Stress of the method</param>
/// <param name="Library">Stress of the library (cumulative of all the phases)</param>
public sealed record VivianiComparisonPoint(int Point, string Name, double Y, double Viviani, double Library)
{
    /// <summary>Library - Viviani</summary>
    public double Difference => Library - Viviani;
}

/// <summary>Comparison of the method of prof. Viviani with the linear calculation of the library on the same data</summary>
/// <param name="Input">The data of the method, converted from the data of the library</param>
/// <param name="Viviani">The results of the method</param>
/// <param name="Library">The results of the library</param>
/// <param name="Points">The stresses at the 6 points of the method</param>
/// <param name="LibraryPlasticMoment">Plastic moment [kNm] with the exact integration of the library (<see cref="BridgeBendingShear.PlasticMoment"/>) on the
/// data of the method, with the fyd of the web of the method (reduced by the shear); signed as M Ed</param>
/// <param name="Notes">The differences of the hypotheses between the two calculations for these data</param>
public sealed record VivianiComparison(VivianiInput Input, VivianiResult Viviani, HBridgeAnalysisResult Library, IReadOnlyList<VivianiComparisonPoint> Points,
    double LibraryPlasticMoment, IReadOnlyList<string> Notes);

/// <summary>
/// Converts the data of the library into the data of the method of prof. Viviani and compares the results. The phases of solo acciaio are G1, the
/// composite phases with φ = 0 (n = n0 = Ea / Ecm) are Q and the other composite phases are G2 (they must have the same n); the loads are the
/// design ones of the phases (γ = 1). The two rows of reinforcement become one layer at their centroid; two bottom plates become the equivalent
/// plate of the library
/// </summary>
public static class VivianiComparisons
{
    /// <summary>The data of the method from the data of the library</summary>
    /// <param name="data">The data of the library</param>
    /// <param name="notes">The approximations of the conversion</param>
    /// <returns>The data of the method (cm, kN, kNm, MPa)</returns>
    /// <exception cref="ArgumentException">For phases the method does not have (shrinkage, excluded slab, long term phases with different n)</exception>
    public static VivianiInput ToViviani(HBridgeInput data, List<string>? notes = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        notes ??= new List<string>();
        if (data.Geometry.SectionType != BridgeSteelSectionType.H)
            throw new ArgumentException("Il metodo Viviani calcola solo la sezione ad H con anima verticale.");
        BridgeGeometry g = HBridgeSection.Geometry(data);
        var materials = data.Materials;
        double n0 = materials.Steel.ElasticModulusTension / materials.Concrete.ElasticModulusCompression;
        double g1N = 0, g1T = 0, g1M = 0, g2N = 0, g2T = 0, g2M = 0, qN = 0, qT = 0, qM = 0, nLong = double.NaN;
        foreach (BridgePhase p in data.Phases.Where(p => p.Active))
        {
            switch (p.Kind)
            {
                case BridgePhaseKind.SteelOnly:
                    g1N += p.ForceKN; g1T += p.ShearKN; g1M += p.MomentKNm;
                    break;
                case BridgePhaseKind.Composite:
                    double n = HBridgeSection.Homogenization(data, p).N;
                    if (Math.Abs(n - n0) <= 1e-9 * n0)
                    {
                        qN += p.ForceKN; qT += p.ShearKN; qM += p.MomentKNm;
                        break;
                    }
                    if (!double.IsNaN(nLong) && Math.Abs(n - nLong) > 1e-9 * n)
                        throw new ArgumentException("Il metodo Viviani ammette un solo coefficiente n per le azioni di lunga durata.");
                    nLong = n;
                    g2N += p.ForceKN; g2T += p.ShearKN; g2M += p.MomentKNm;
                    break;
                default:
                    throw new ArgumentException($"Il metodo Viviani non ha fasi di tipo {p.KindName}.");
            }
            if (p.Reference != BridgeLoadReference.GrossCentroid && p.ForceKN != 0)
                notes.Add($"{p.Name}: N applicato nel baricentro della sezione nel metodo Viviani (nella libreria: {p.ReferenceName}).");
        }
        double barArea = g.Bars.Sum(b => b.Area);
        double barY = barArea > 0 ? g.Bars.Sum(b => b.Area * b.Y) / barArea : 0;
        if (g.Bars.Select(b => b.Y).Distinct().Count() > 1)
            notes.Add("Le due file di armature sono un solo strato nel loro baricentro.");
        if (g.Bottom2Thickness > 0)
            notes.Add("Le due piastre inferiori sono la piastra equivalente (stessa area e spessore totale).");
        if (Math.Abs(materials.Rebar.ElasticModulusTension - materials.Steel.ElasticModulusTension) > 1e-9)
            notes.Add($"Il metodo Viviani usa per le armature il modulo dell'acciaio da carpenteria (Es/Ea = {materials.Rebar.ElasticModulusTension / materials.Steel.ElasticModulusTension:0.###} nella libreria).");
        notes.Add("Il metodo Viviani somma l'area delle armature a quella della soletta (la libreria toglie il calcestruzzo occupato dalle barre).");
        var options = data.Options;
        double fyd = materials.Steel.Fyk / options.GammaM0;
        return new VivianiInput
        {
            SlabHeight = g.SlabHeight / 10, SlabWidth = g.Width / 10, RebarCover = barArea > 0 ? (g.SlabHeight - barY) / 10 : 0, RebarArea = barArea / 100,
            TopFlangeThickness = g.TopThickness / 10, TopFlangeWidth = g.TopWidth / 10, WebHeight = g.WebHeight / 10, WebThickness = g.WebThickness / 10,
            BottomFlangeThickness = g.BottomEquivalentThickness / 10, BottomFlangeWidth = g.BottomEquivalentWidth / 10,
            ShortTermModularRatio = n0, LongTermModularRatio = double.IsNaN(nLong) ? n0 : nLong,
            RebarFyd = materials.Rebar.Fyk / options.GammaS, Fcd = options.AlphaCC * Math.Abs(materials.Concrete.Fck) / options.GammaC,
            TopFlangeFyd = fyd, WebFyd = fyd, BottomFlangeFyd = fyd,
            SteelOnly = new(g1N, g1T, g1M), LongTerm = new(g2N, g2T, g2M), ShortTerm = new(qN, qT, qM),
            GammaG1 = 1, GammaG2 = 1, GammaQ = 1, FactoredElasticStresses = false, Iterations = options.Class4 ? 4 : 0
        };
    }

    /// <summary>Compares the method with the linear calculation of the library on the same data</summary>
    /// <param name="data">The data of the library</param>
    /// <returns>The comparison</returns>
    public static VivianiComparison Compare(HBridgeInput data)
    {
        var notes = new List<string>();
        VivianiInput input = ToViviani(data, notes);
        VivianiResult viviani = VivianiMethod.Calculate(input);
        HBridgeAnalysisResult library = HBridgeSection.Calculate(data);
        BridgeGeometry g = library.Geometry;
        List<BridgeContribution> contributions = library.Stages.Last().Contributions;
        double Stress(string material, double y) => contributions.Sum(c => c.Stress(material, y));
        double yRebar = g.SlabHeight - input.RebarCover * 10;
        var points = new List<VivianiComparisonPoint>
        {
            new(1, "Soletta · estradosso (CLS)", g.SlabHeight, viviani.Sigma1, Stress("CLS", g.SlabHeight)),
            new(2, "Armatura", yRebar, viviani.Sigma2, input.RebarArea > 0 ? Stress("Armatura", yRebar) : 0),
            new(3, "Acciaio · estradosso", 0, viviani.Sigma3, Stress("Acciaio", 0)),
            new(4, "Anima · sommità", -g.TopThickness, viviani.Sigma4, Stress("Acciaio", -g.TopThickness)),
            new(5, "Anima · piede", -g.TopThickness - g.WebHeight, viviani.Sigma5, Stress("Acciaio", -g.TopThickness - g.WebHeight)),
            new(6, "Acciaio · intradosso", -g.Height, viviani.Sigma6, Stress("Acciaio", -g.Height))
        };
        if (viviani.CrackedSlab && contributions.Any(c => c.HasConcrete))
            notes.Add("Il metodo Viviani considera la soletta fessurata (estradosso teso) per tutte le azioni sulla sezione composta; la libreria segue il tipo di fase.");
        if (data.Options.Class4)
            notes.Add("Classe 4: il metodo Viviani riduce solo l'anima (4 iterazioni fisse, ε con fyd); la libreria anche le piattabande, fino a convergenza, ε con fy.");
        return new VivianiComparison(input, viviani, library, points, PlasticMoment(input, viviani), notes);
    }

    /// <summary>
    /// Plastic moment [kNm] of the data of the method with the exact integration of rectangular blocks of the library (<see cref="BridgeBendingShear.PlasticMoment"/>):
    /// concrete only in compression and only with the positive moment (as the method), reinforcement as a thin layer, fyd of the web of the method
    /// </summary>
    /// <param name="input">The data of the method</param>
    /// <param name="result">The results of the method (sign of M Ed and fyd of the web reduced by the shear)</param>
    /// <returns>The plastic moment, signed as M Ed</returns>
    public static double PlasticMoment(VivianiInput input, VivianiResult result)
    {
        double tpi = input.BottomFlangeThickness, haw = input.WebHeight, tps = input.TopFlangeThickness, hs = input.SlabHeight;
        var blocks = new List<BridgeBendingShear.Block>
        {
            new(0, tpi, input.BottomFlangeWidth, input.BottomFlangeFyd, input.BottomFlangeFyd),
            new(tpi, tpi + haw, input.WebThickness, result.WebFydForBending, result.WebFydForBending),
            new(tpi + haw, tpi + haw + tps, input.TopFlangeWidth, input.TopFlangeFyd, input.TopFlangeFyd)
        };
        double top = tpi + haw + tps + hs;
        // the method counts the concrete only with the positive moment
        if (hs > 0 && input.SlabWidth > 0 && result.MEd >= 0)
            blocks.Add(new(tpi + haw + tps, top, input.SlabWidth, input.Fcd, 0));
        if (input.RebarArea > 0)
        {
            // thin layer at the level of the reinforcement
            const double layer = 1e-6;
            double y = top - input.RebarCover;
            blocks.Add(new(y - layer / 2, y + layer / 2, input.RebarArea / layer, input.RebarFyd, input.RebarFyd));
        }
        bool positive = result.MEd >= 0;
        // MPa × cm³ = 1e-3 kNm
        double moment = BridgeBendingShear.PlasticMoment(blocks, positive) / 1000;
        return positive ? moment : -moment;
    }
}
