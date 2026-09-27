using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.CompositeBridge;

// H-specific geometry and result assembly; reusable cumulative iteration and native solver live in Analysis/.
public static partial class HBridgeSection
{
    public static HBridgeAnalysisResult Calculate(HBridgeInput data, CancellationToken cancellation = default)
    {
        data = data.Snapshot(); var g = Geometry(data); var mat = Materials(data);
        if (!Standards.Contains(data.Options.StandardName)) throw new ArgumentException("Normativa non supportata.");
        if (data.Options.LimitStateName is not ("SLU" or "SLE rara" or "SLE quasi permanente")) throw new ArgumentException("Stato limite non supportato.");
        BridgeNumbers.Require(data.Options.GammaM0, "gamma_m0", strict: true); BridgeNumbers.Require(data.Options.GammaC, "gamma_c", strict: true);
        BridgeNumbers.Require(data.Options.GammaS, "gamma_s", strict: true); BridgeNumbers.Require(data.Options.AlphaCC, "alpha_cc", strict: true);
        var phases = data.Phases.Where(p => p.Active).ToArray();
        if (phases.Length == 0) throw new ArgumentException("Attivare almeno una fase.");
        bool compositeSeen = false;
        foreach (var p in phases)
        {
            if (!PhaseKinds.Contains(p.KindName)) throw new ArgumentException("Tipo di fase sconosciuto.");
            if (p.KindName == "Solo acciaio" && compositeSeen) throw new ArgumentException("Le fasi di solo acciaio devono precedere le fasi composte.");
            compositeSeen |= p.KindName is "Composta" or "Soletta esclusa";
            if (p.KindName == ShrinkageKind) Number(p, "epsilon_cs");
            else { Number(p, "N"); Number(p, "Mx"); Number(p, "V"); }
            if (!LoadReferences.Contains(LoadReference(p))) throw new ArgumentException("Riferimento di N sconosciuto.");
        }
        // null means follow the effective centroid inside Solve; fixed points are resolved once.
        var applicationPoints = phases.ToDictionary(p => p, p => p.KindName == ShrinkageKind ? null : LoadReference(p) switch
        {
            CommonLoadReference => (double?)BridgeNumbers.Require(data.Options.CommonLoadY, "y_ref", double.NegativeInfinity),
            GrossLoadReference => GrossPhaseCentroid(data, p),
            _ => null
        }, new BridgePhaseIdentity());
        var stages = new List<BridgeStage>();
        // The effective-width iterations use the transformed-section field with the Checker integration inertia, which is the Checker linear
        // field to machine precision (relative differences 1e-15); the converged situation is solved again with the Checker solver, which
        // gives the reported stresses and the equilibrium audit. Before, every iteration built a Checker solver for every composite phase
        // (about 2 ms each: 63 solutions and 115 ms for three phases, now 3 solutions).
        var situations = CumulativePhaseAnalysis.Analyze(phases, () => BridgeEffective.Full(g),
            (effective, phase) => Solve(data, g, effective, phase, applicationPoints[phase], nativeSolver: !data.Options.Class4),
            contributions => data.Options.Class4 ? EffectiveWidths(g, y => contributions.Sum(c => c.SteelStress(y)), mat.Steel.Fyk, data.Options) : BridgeEffective.Full(g),
            (current, next) => current.Distance(next, g), (current, next, factor) => current.Relax(next, factor),
            phase => phase.Name, cancellation,
            coordinates: data.Options.AcceleratedIteration ? e => e.Coordinates(g) : null, warmStart: data.Options.AcceleratedIteration);
        foreach (var situation in situations)
        {
            int end = situation.PhaseIndex, iter = situation.Iterations;
            double error = situation.Residual;
            var included = phases.Take(end + 1).ToArray();
            var effective = situation.State;
            // gross section: the only iteration already used the Checker solver
            var contributions = data.Options.Class4 ? included.Select(p => Solve(data, g, effective, p, applicationPoints[p])).ToList() : situation.Contributions.ToList();
            // Keep properties and stresses on the same converged geometry. Diagnostic plate data are recomputed on those stresses.
            var diagnostics = data.Options.Class4 ? EffectiveWidths(g, y => contributions.Sum(c => c.SteelStress(y)), mat.Steel.Fyk, data.Options) : BridgeEffective.Full(g);
            effective = effective with { Web = diagnostics.Web, Top = diagnostics.Top, Bottom = diagnostics.Bottom, SecondBottom = diagnostics.SecondBottom };
            var points = StressPoints(g, contributions, data, Math.Abs(mat.Concrete.Fck), mat.Steel.Fyk, mat.Rebar.Fyk);
            var warnings = new List<string>();
            if (points.Any(p => p.Material == "CLS" && p.Stress > 1e-6)) warnings.Add("CLS teso nel modello non fessurato: valutare una situazione con soletta esclusa; non è attestata la verifica del CLS in trazione.");
            if (!data.Options.Class4) warnings.Add("Sezione lorda: riduzioni locali disattivate. Risultato di confronto, non verifica di classe 4.");
            if (contributions.Any(c => c.IsShrinkage)) warnings.Add("Ritiro uniforme imposto al solo CLS: effetti primari autoequilibrati. Eventuali azioni secondarie da vincoli esterni vanno inserite come fasi N–Mx separate.");
            if (data.Options.Class4) warnings.AddRange(LocalBucklingWarnings(data.Options));
            warnings.AddRange(SectionTypeWarnings(g));
            if (g.Bottom2Thickness > 0) warnings.Add("Due piastre inferiori modellate con la geometria reale. Instabilità locale: ciascuna piastra come sbalzo dall'anima con il proprio spessore, senza il beneficio dell'accoppiamento (a favore di sicurezza).");
            var effectiveParts = SteelPieces(g, effective, mat.Steel);
            double effectiveArea = effectiveParts.Sum(p => p.Section.Area);
            double effectiveCentroid = effectiveParts.Sum(p => p.Section.Area * p.PositionToGlobal(p.Section.Centroid).Y) / effectiveArea;
            double effectiveInertia = effectiveParts.Sum(p => p.Section.Jxx * Math.Pow(Math.Cos(p.Rotation), 2) + p.Section.Jyy * Math.Pow(Math.Sin(p.Rotation), 2)
                + p.Section.Area * Math.Pow(p.PositionToGlobal(p.Section.Centroid).Y - effectiveCentroid, 2));
            var materialValues = new BridgeMaterialValues(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression,
                mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension, mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension);
            var accessory = CalculateShear(data, g, materialValues, effective, contributions, included, warnings);
            stages.Add(new(phases[end].Name, iter, error, effective, new(effectiveArea, effectiveCentroid, effectiveInertia), contributions, points, warnings, accessory.Shear, accessory.Studs));
        }
        return new(Method, Scope, data, g,
            new(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression, mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension,
                mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension), stages);
    }
    /// <summary>The warnings for the parts whose local buckling is excluded by the options</summary>
    public static IEnumerable<string> LocalBucklingWarnings(BridgeAnalysisOptions options)
    {
        var parts = new List<string>();
        if (!options.TopFlangeBuckling) parts.Add("piattabanda superiore");
        if (!options.BottomFlangeBuckling) parts.Add("piattabanda inferiore");
        if (!options.WebBuckling) parts.Add("anima");
        if (parts.Count > 0)
            yield return "Instabilità locale esclusa per: " + string.Join(", ", parts) + " (interamente efficaci, ρ = 1). Giustificare l'esclusione, ad esempio con classe 1–3 o con il vincolo della soletta connessa.";
    }
    private static double Number(BridgePhase p, string k) => BridgeNumbers.Require(k switch {
        "N" => p.ForceKN, "Mx" => p.MomentKNm, "V" => p.ShearKN, "q_conn" => p.AdditionalConnectionFlow,
        "epsilon_cs" => p.ShrinkageMicrostrain, _ => throw new ArgumentException(k) }, k, double.NegativeInfinity);
    /// <summary>The stress field of one load increment on the effective geometry e</summary>
    /// <param name="d">The input</param>
    /// <param name="g">The geometry</param>
    /// <param name="e">The effective steel geometry</param>
    /// <param name="p">The phase</param>
    /// <param name="applicationPoint">y of the axial force; null: the effective centroid</param>
    /// <param name="nativeSolver">False: the transformed-section field with the Checker integration inertia instead of the Checker solver
    /// (the same field to machine precision, used for the effective-width iterations)</param>
    private static BridgeContribution Solve(HBridgeInput d, BridgeGeometry g, BridgeEffective e, BridgePhase p, double? applicationPoint, bool nativeSolver = true)
    {
        if (p.KindName == ShrinkageKind) return SolveShrinkage(d, g, e, p, nativeSolver);
        var mat = Materials(d); string kind = p.KindName;
        var homo = kind == "Composta" ? Homogenization(d, p) : (0d, 0d, 0d, 0d);
        double n = homo.Item2, area, cy, inertia;
        ReinforcedConcreteSection? composite = null;
        var steel = SteelPieces(g, e, mat.Steel);
        if (kind == "Composta")
        {
            var section = composite = NativeSection(d, g); section.SteelSections.Clear();
            foreach (var part in steel) section.AddSteelSection(part);
            // Native Model transforms into concrete; divide by n to report in structural steel.
            var props = section.GetHomogeneizedMechanicalProperties(homo.Item3);
            area = props.areaH / n; cy = props.centroidH.Y; inertia = props.JxxH / n;
        }
        else
        {
            var parts = steel.Select(s => (A: s.Section.Area, Y: s.PositionToGlobal(s.Section.Centroid).Y,
                I: s.Section.Jxx * Math.Pow(Math.Cos(s.Rotation), 2) + s.Section.Jyy * Math.Pow(Math.Sin(s.Rotation), 2))).ToList();
            if (kind == "Soletta esclusa")
                parts.AddRange(g.Bars.Select(b => (b.Area * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension, b.Y,
                    Math.PI * Math.Pow(b.Diameter, 4) / 64 * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension)));
            area = parts.Sum(x => x.A); cy = parts.Sum(x => x.A * x.Y) / area;
            inertia = parts.Sum(x => x.I + x.A * Math.Pow(x.Y - cy, 2));
        }
        if (!BridgeNumbers.IsFinite(inertia) || inertia <= 0 || !BridgeNumbers.IsFinite(area) || area <= 0) throw new InvalidOperationException("Proprietà della sezione non valide.");
        double yref = applicationPoint ?? cy;
        double force = Number(p, "N") * 1000, moment = Number(p, "Mx") * 1e6;
        double mg = moment + force * (cy - yref), uniform = force / area, slope = -mg / inertia;
        double solverInertia = inertia;
        if (composite is not null)
        {
            // Independent equilibrium audit of Checker's line-wall / point-rebar integration. The integration inertia is a property of the
            // section: before, it was corrected only for a loaded phase (an unloaded composite phase reported the Model inertia)
            solverInertia -= e.TopWidth * Math.Pow(g.TopThickness, 3) / 12
                + (g.Bottom2Thickness > 0 ? e.BottomWidth * Math.Pow(g.Bottom1Thickness, 3) / 12 + e.SecondBottomWidth * Math.Pow(g.Bottom2Thickness, 3) / 12
                    : e.BottomWidth * Math.Pow(g.BottomEquivalentThickness, 3) / 12)
                + g.Bars.Sum(b => Math.PI * Math.Pow(b.Diameter, 4) / 64) * (mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension - 1 / n);
            if (!nativeSolver) slope = -mg / solverInertia;
            else if (force != 0 || moment != 0)
            {
                var field = CompositeLinearStressSolver.Solve(composite, force, moment, g.Width / 2, yref, homo.Item3, d.Options.Standard);
                slope = field.Slope;
                uniform = field.Stress(cy);
            }
        }
        double calculatedN = uniform * area, calculatedM = -slope * solverInertia - calculatedN * (cy - yref);
        double span = g.Height + g.SlabHeight;
        double equilibrium = Math.Max(Math.Abs(calculatedN - force) / Math.Max(1, Math.Max(Math.Abs(force), Math.Abs(moment) / span)),
            Math.Abs(calculatedM - moment) / Math.Max(1, Math.Max(Math.Abs(moment), Math.Abs(force) * span)));
        if (!BridgeNumbers.IsFinite(equilibrium) || equilibrium > 1e-5) throw new InvalidOperationException($"Equilibrio della fase {p.Name} non soddisfatto (residuo {equilibrium:E2}).");
        return new(p.Name, kind, force / 1000, moment / 1e6, homo.Item1, n, homo.Item4, homo.Item3,
            area, cy, inertia, uniform, slope, mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension,
            kind == "Solo acciaio" ? 0 : g.Bars.Sum(b => b.Area), inertia / Math.Abs(-g.Height - cy), Math.Abs(cy) < 1e-9 ? null : inertia / Math.Abs(cy), solverInertia, equilibrium, yref, LoadReference(p), Number(p, "V"),
            ConnectionFlowExtra: kind != "Solo acciaio" ? Number(p, "q_conn") : 0);
    }
    private static BridgeContribution SolveShrinkage(HBridgeInput d, BridgeGeometry g, BridgeEffective effective, BridgePhase phase, bool nativeSolver = true)
    {
        // Input in microstrain: contraction is negative. Bars do not receive an eigenstrain.
        double strain = Number(phase, "epsilon_cs") * 1e-6;
        double ec = Materials(d).Steel.ElasticModulusTension / Homogenization(d, phase).N;
        double ac = g.Width * g.SlabHeight - g.Bars.Sum(b => b.Area);
        double yc = (g.Width * g.SlabHeight * g.SlabHeight / 2 - g.Bars.Sum(b => b.Area * b.Y)) / ac;
        double equivalentForce = ec * ac * strain;
        var fictitious = phase with { Kind = BridgePhaseKind.Composite, ForceKN = equivalentForce / 1000, MomentKNm = 0, ShearKN = 0 };
        var response = Solve(d, g, effective, fictitious, yc, nativeSolver);
        // The equivalent force solves compatibility. Removing the free-strain stress in
        // concrete restores zero external N and M; omitting this term is NOT shrinkage.
        return response with { Kind = ShrinkageKind, N = 0, Mx = 0, V = 0, ShrinkageStrain = strain,
            ConcreteStressOffset = -ec * strain, EquivalentN = equivalentForce / 1000,
            EquivalentMomentAtInterface = -equivalentForce * yc / 1e6, LoadReference = "Ritiro · CLS netto", LoadY = yc };
    }
    private static List<SteelSectionPosition> SteelPieces(BridgeGeometry g, BridgeEffective e, SteelMaterial material)
    {
        var pieces = new List<SteelSectionPosition>();
        void Add(double width, double height, double y, bool web = false)
        {
            if (width <= 1e-8 || height <= 1e-8) return;
            // Checker integrates along the ThinWall centreline: orient vertical webs along their length,
            // never represent them as a horizontal short line with the web height as its thickness.
            var rectangle = web ? new SectionRectangular(width, height) : new SectionRectangular(height, width);
            pieces.Add(new SteelSectionPosition(new SteelSection(rectangle, material), Point2d.Origin, web ? Math.PI / 2 : 0,
                new Point2d(g.Width / 2, y + height / 2), InsertionPointType.Centroid) { IsInsideConcrete = false });
        }
        Add(e.TopWidth, g.TopThickness, -g.TopThickness);
        Add(g.WebThickness, e.WebTop, -g.TopThickness - e.WebTop, true);
        Add(g.WebThickness, e.WebBottom, -g.TopThickness - g.WebHeight, true);
        if (g.Bottom2Thickness > 0)
        {
            // the two real bottom plates (before, the equivalent rectangle with the same area and total thickness)
            Add(e.BottomWidth, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness);
            Add(e.SecondBottomWidth, g.Bottom2Thickness, -g.Height);
        }
        else Add(e.BottomWidth, g.BottomEquivalentThickness, -g.Height);
        return pieces;
    }
    private static List<BridgeStressPoint> StressPoints(BridgeGeometry g, List<BridgeContribution> c, HBridgeInput d, double fck, double fy, double fys)
    {
        var points = new List<BridgeStressPoint>();
        double lc = d.Options.LimitStateName == "SLU" ? d.Options.AlphaCC * fck / d.Options.GammaC : (d.Options.LimitStateName == "SLE rara" ? .6 : .45) * fck;
        double la = d.Options.LimitStateName == "SLU" ? fy / d.Options.GammaM0 : fy;
        double ls = d.Options.LimitStateName == "SLU" ? fys / d.Options.GammaS : .8 * fys;
        void Add(string label, string material, double y, double limit)
        {
            var values = c.Select(p => p.Stress(material, y)).ToArray(); double stress = values.Sum();
            bool active = material switch { "Acciaio" => true, "CLS" => c.Any(x => x.HasConcrete), _ => c.Any(x => x.Kind != "Solo acciaio") };
            double? utilization = !active || material == "CLS" && stress > 1e-6 ? null : Math.Abs(stress) / limit;
            points.Add(new(label, material, y, stress, limit, utilization, active, values));
        }
        Add("Soletta · estradosso", "CLS", g.SlabHeight, lc); Add("Soletta · intradosso", "CLS", 0, lc);
        foreach (var row in g.Bars.GroupBy(b => b.Y).OrderByDescending(x => x.Key)) Add("Armatura · y=" + row.Key.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "Armatura", row.Key, ls);
        Add("Acciaio · estradosso", "Acciaio", 0, la); Add("Anima · sommità", "Acciaio", -g.TopThickness, la);
        Add("Anima · piede", "Acciaio", -g.TopThickness - g.WebHeight, la); Add("Acciaio · intradosso", "Acciaio", -g.Height, la);
        return points;
    }
}
