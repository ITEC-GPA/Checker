
namespace GPC.Checkers.CompositeBridge;

/// <summary>The St. Venant shear flow of one phase in the closed cell of the box</summary>
/// <param name="Phase">The phase</param>
/// <param name="Kind">The kind of the phase</param>
/// <param name="TorqueKNm">The torque increment [kNm]</param>
/// <param name="CellArea">A0 [mm²]</param>
/// <param name="TorsionConstant">J = 4 A0² / Σ(ℓ/t) in steel [mm⁴] (0: open section)</param>
/// <param name="Flow">q = T / (2 A0) [N/mm = kN/m]</param>
/// <param name="Closed">False: steel phase without top bracing (open section, torsion not verified)</param>
public sealed record BridgeTorsionFlow(string Phase, string Kind, double TorqueKNm, double CellArea, double TorsionConstant, double Flow, bool Closed);
/// <summary>The distortion of the box (envelope along the span, beam on elastic foundation)</summary>
/// <param name="WarpingInertia">I_Dw [mm⁶]</param>
/// <param name="FrameStiffness">K [N]</param>
/// <param name="DiaphragmStiffness">K_D [N mm]</param>
/// <param name="TorqueLoad">The generalised load of a unit torque</param>
/// <param name="Diaphragms">The number of intermediate diaphragms</param>
/// <param name="Moment">max |E I_Dw ψ''| [N mm²]</param>
/// <param name="Amplitude">max |ψ|</param>
/// <param name="DiaphragmAmplitude">max |ψ| at the diaphragms</param>
/// <param name="WarpingStressBottom">σ_dw at the bottom flange (corners and outstands) [MPa]</param>
/// <param name="WarpingStressTop">σ_dw at the top flanges [MPa]</param>
/// <param name="CornerMomentBottom">The transverse moment at the bottom corners [N mm/mm]</param>
/// <param name="CornerMomentTop">The transverse moment at the top corners [N mm/mm]</param>
/// <param name="BendingRatio">σ_dw / σ of bending at the bottom flange</param>
/// <param name="Included">True: σ_dw added to the bottom flange (more than 10% of bending, EN 1993-2 §6.2.7(3))</param>
public sealed record BridgeDistortionResult(double WarpingInertia, double FrameStiffness, double DiaphragmStiffness, double TorqueLoad, int Diaphragms,
    double Moment, double Amplitude, double DiaphragmAmplitude, double WarpingStressBottom, double WarpingStressTop, double CornerMomentBottom, double CornerMomentTop,
    double BendingRatio, bool Included);
/// <summary>Torsion, distortion and diaphragms of the box in one situation. The flows are cumulative with sign [N/mm = kN/m]</summary>
/// <param name="Flows">The flows of the phases</param>
/// <param name="WebFlow">The flow in the webs</param>
/// <param name="BottomFlow">The flow in the bottom flange</param>
/// <param name="SlabFlow">The flow in the slab and in the connection (composite phases)</param>
/// <param name="BracingFlow">The flow in the top bracing (steel phases)</param>
/// <param name="Distortion">The distortion (null: not analysed)</param>
/// <param name="Checks">The checks of torsion, distortion and diaphragms (those of webs, studs and slab include the flows)</param>
/// <param name="Details">The values of the calculation</param>
public sealed record BridgeTorsionResult(List<BridgeTorsionFlow> Flows, double WebFlow, double BottomFlow, double SlabFlow, double BracingFlow,
    BridgeDistortionResult? Distortion, List<BridgeLocalCheck> Checks, List<BridgeDetailValue> Details);

public static partial class HBridgeSection
{
    private const double ConcretePoisson = .2, SteelPoisson = .3;

    /// <summary>Box: the half distance of the web axes at the level y (0 at the top of the steel, the webs extended beyond the flanges)</summary>
    private static double BoxHalfWidth(BridgeGeometry g, double y) =>
        g.WebSpacingTop / 2 + (y + g.TopThickness) * (g.WebSpacingTop - g.WebSpacingBottom) / 2 / g.WebHeight;

    /// <summary>The cell of the steel box closed by the top bracing on the mid-plane of the top flanges</summary>
    /// <param name="g">The geometry of a box</param>
    /// <param name="bracingThickness">The equivalent thickness of the bracing</param>
    public static BoxCell SteelCell(BridgeGeometry g, double bracingThickness)
    {
        double yb = -g.TopThickness - g.WebHeight - g.Bottom1Thickness / 2, yt = -g.TopThickness / 2;
        return new(2 * BoxHalfWidth(g, yt), 2 * BoxHalfWidth(g, yb), yt - yb, bracingThickness, g.PlateThickness, g.Bottom1Thickness);
    }

    /// <summary>The cell of the composite box closed by the slab on its mid-plane (webs extended to it)</summary>
    /// <param name="g">The geometry of a box</param>
    /// <param name="slabThickness">The thickness of the slab in steel (hc / nG)</param>
    public static BoxCell CompositeCell(BridgeGeometry g, double slabThickness)
    {
        double yb = -g.TopThickness - g.WebHeight - g.Bottom1Thickness / 2, yt = g.SlabHeight / 2;
        return new(2 * BoxHalfWidth(g, yt), 2 * BoxHalfWidth(g, yb), yt - yb, slabThickness, g.PlateThickness, g.Bottom1Thickness);
    }

    /// <summary>The modular ratio in shear nG = Ga / Gc of a phase: n (1 + νc) / (1 + νa), with the creep of the phase (EN 1994-2 §5.4.2.2(11))</summary>
    private static double ShearModularRatio(HBridgeInput d, BridgePhase phase, BridgeMaterialValues m)
    {
        double n;
        try { n = Homogenization(d, phase).N; }
        catch (ArgumentException) { n = m.Ea / m.Ec; } // phase without concrete stiffness data: short term
        return n * (1 + ConcretePoisson) / (1 + SteelPoisson);
    }

    /// <summary>The torques of the phases: only for the box with the torsion checks (the H sections are in straight bending)</summary>
    private static void ValidateTorsion(HBridgeInput d, BridgeGeometry g, BridgePhase[] phases)
    {
        bool box = g.SectionType == BridgeSteelSectionType.Box;
        bool torque = phases.Any(p => p.KindName != ShrinkageKind && BridgeNumbers.Require(p.TorsionKNm, "T", double.NegativeInfinity) != 0);
        if (torque && (!box || !d.Box.Enabled))
            throw new ArgumentException(box ? "Cassoncino: attivare le verifiche a torsione per assegnare momenti torcenti alle fasi."
                : "Momento torcente disponibile solo per il cassoncino: le sezioni ad H sono in flessione retta.");
    }

    /// <summary>The St. Venant shear flows of the phases of a situation; null for the H sections and the box without torsion checks</summary>
    private static BridgeTorsionResult? TorsionFlows(HBridgeInput d, BridgeGeometry g, BridgeMaterialValues m, BridgePhase[] phases)
    {
        ValidateTorsion(d, g, phases);
        if (g.SectionType != BridgeSteelSectionType.Box || !d.Box.Enabled) return null;
        double bracing = BridgeNumbers.Require(d.Box.BracingThickness, "t_controvento");
        var steelCell = SteelCell(g, bracing);
        var flows = new List<BridgeTorsionFlow>();
        foreach (var p in phases)
        {
            if (p.KindName == ShrinkageKind) continue;
            double t = p.TorsionKNm * 1e6;
            if (p.KindName == "Solo acciaio")
            {
                bool closed = bracing > 0;
                flows.Add(new(p.Name, p.KindName, p.TorsionKNm, steelCell.Area, closed ? steelCell.TorsionConstant : 0, closed ? steelCell.ShearFlow(t) : 0, closed));
                continue;
            }
            // the cracked slab with half its stiffness (EN 1994-2 §5.4.2.3(6)); q does not depend on the thicknesses of the single cell
            var cell = CompositeCell(g, g.SlabHeight / ShearModularRatio(d, p, m) * (p.KindName == "Soletta esclusa" ? .5 : 1));
            flows.Add(new(p.Name, p.KindName, p.TorsionKNm, cell.Area, cell.TorsionConstant, cell.ShearFlow(t), true));
        }
        double all = flows.Sum(f => f.Flow), slab = flows.Where(f => f.Kind != "Solo acciaio").Sum(f => f.Flow);
        return new(flows, all, all, slab, flows.Where(f => f.Kind == "Solo acciaio").Sum(f => f.Flow), null, [], []);
    }

    /// <summary>The couple of the bearings from the torque at the support, T / e_b [N] (0 without torsion)</summary>
    private static double BearingTorsionForce(HBridgeInput d, BridgeGeometry g) =>
        g.SectionType == BridgeSteelSectionType.Box && d.Box.Enabled && BridgeNumbers.Require(d.Box.SupportTorqueKNm, "T_appoggio", double.NegativeInfinity) != 0
            ? Math.Abs(d.Box.SupportTorqueKNm) * 1e6 / BridgeNumbers.Require(d.Box.BearingSpacing, "e_appoggi", strict: true) : 0;

    /// <summary>The shear resistance of a plate panel per unit area, min(plastic; buckling) / (b t), with η = 1 (flanges and diaphragms)</summary>
    private static (double Resistance, WebShearResistance Panel) PanelShear(double width, double thickness, double panel, BridgeMaterialValues m, double gm0, double gm1)
    {
        var w = BridgeShearConnection.Web(width, thickness, m.Fy, m.Ea, gm0, gm1, 1, panel);
        return (w.Resistance / w.Area, w);
    }

    /// <summary>The von Mises stress with a transverse stress of unknown sign: √(σx² + σz² + |σx σz| + 3τ²)</summary>
    private static double VonMises(double sx, double sz, double tau) => Math.Sqrt(sx * sx + sz * sz + Math.Abs(sx * sz) + 3 * tau * tau);

    /// <summary>
    /// The checks of torsion, distortion and diaphragms of the box in one situation (those of the webs, of the studs and of the slab surfaces
    /// include the flows in <see cref="CalculateShear"/>)
    /// </summary>
    /// <param name="d">The input</param>
    /// <param name="g">The geometry</param>
    /// <param name="m">The materials</param>
    /// <param name="torsion">The flows</param>
    /// <param name="contributions">The contributions of the situation</param>
    /// <param name="sigma">The normal stress of the steel at y</param>
    /// <param name="tauTop">τ of bending at the top of the webs</param>
    /// <param name="tauBottom">τ of bending at the bottom of the webs</param>
    /// <param name="bottomShear">τ of bending in the bottom flange at the webs</param>
    /// <param name="warnings">The warnings</param>
    private static BridgeTorsionResult CheckTorsion(HBridgeInput d, BridgeGeometry g, BridgeMaterialValues m, BridgeTorsionResult torsion,
        List<BridgeContribution> contributions, Func<double, double> sigma, double tauTop, double tauBottom, double bottomShear, List<string> warnings)
    {
        var o = d.Box; var checks = torsion.Checks; var details = torsion.Details;
        bool ultimate = d.Options.LimitStateName == "SLU";
        double gm0 = d.Options.GammaM0, gm1 = d.Options.GammaM1, fy = m.Fy, ea = m.Ea, limit = fy / (ultimate ? gm0 : 1);
        double tw = g.PlateThickness, tb = g.Bottom1Thickness, hc = g.SlabHeight, top = -g.TopThickness, bottom = -g.TopThickness - g.WebHeight;
        double tauWebT = Math.Abs(torsion.WebFlow) / tw, tauBottomT = Math.Abs(torsion.BottomFlow) / tb;
        void Check(string name, double demand, double resistance, string unit, double? ratio, string note, bool always = false) =>
            checks.Add(new(name, demand, resistance, unit, ultimate || always ? ratio : null, ultimate || always ? note : "Esito richiede azioni SLU; " + note));

        foreach (var f in torsion.Flows)
        {
            string name = "Torsione · " + f.Phase;
            details.Add(new(name + " · T", f.TorqueKNm, "kNm"));
            details.Add(new(name + " · A0", f.CellArea / 1e6, "m²"));
            details.Add(new(name + " · J (acciaio)", f.TorsionConstant / 1e12, "m⁴"));
            details.Add(new(name + " · q = T/(2A0)", f.Flow, "kN/m"));
        }
        details.AddRange(new[] { new BridgeDetailValue("Torsione · q anime e fondo", torsion.WebFlow, "kN/m"), new("Torsione · q soletta e connessione", torsion.SlabFlow, "kN/m"),
            new("Torsione · τ anime", tauWebT, "MPa"), new("Torsione · τ fondo", tauBottomT, "MPa") });
        var open = torsion.Flows.Where(f => !f.Closed && f.TorqueKNm != 0).ToList();
        foreach (var f in open)
            checks.Add(new($"Torsione · {f.Phase} · cassone aperto", Math.Abs(f.TorqueKNm), 0, "kNm", null,
                "Fase di solo acciaio senza controvento superiore (t* = 0): torsione della sezione aperta per ingobbamento non verificata"));
        if (open.Count > 0) warnings.Add("Cassoncino aperto nelle fasi di solo acciaio: assegnare lo spessore equivalente t* del controvento superiore oppure verificare separatamente la torsione non uniforme del cassone aperto.");
        if (torsion.BracingFlow != 0)
        {
            details.Add(new("Controvento superiore · q", torsion.BracingFlow, "kN/m"));
            warnings.Add("Controvento superiore: le aste vanno verificate con il flusso q delle fasi di solo acciaio (taglio del campo di controvento = q × lunghezza del campo); qui è solo riportato.");
        }

        // distortion: beam on elastic foundation on the span, composite cell with the short term slab
        BridgeDistortionResult? distortion = null;
        double span = BridgeNumbers.Require(o.SpanLength, "L_campata"), spacing = BridgeNumbers.Require(o.DiaphragmSpacing, "passo_diaframmi");
        double mt = BridgeNumbers.Require(o.DistributedTorque, "m_t", double.NegativeInfinity), tc = BridgeNumbers.Require(o.ConcentratedTorque, "T_c", double.NegativeInfinity);
        if (span == 0 && (mt != 0 || tc != 0)) throw new ArgumentException("Distorsione: assegnare la luce della campata.");
        if (spacing > 0 && span > 0 && spacing >= span) throw new ArgumentException("Diaframmi intermedi: passo minore della luce (0 se assenti).");
        double sigmaBottom = Math.Max(Math.Abs(sigma(-g.Height)), Math.Abs(sigma(bottom)));
        double sigmaDwBottom = 0, sigmaDwTop = 0, cornerBottom = 0, cornerTop = 0;
        bool concrete = contributions.Any(c => c.Kind is "Composta" or "Soletta esclusa");
        if (span > 0 && !concrete) warnings.Add("Distorsione analizzata solo nelle situazioni con la soletta: cella chiusa dalla soletta.");
        if (span > 0 && concrete)
        {
            double n0 = ea / m.Ec, yb = bottom - tb / 2, yt = hc / 2, xt = BoxHalfWidth(g, yt), xb = BoxHalfWidth(g, yb);
            var section = new BoxDistortionSection(2 * xt, 2 * xb, yt - yb, hc / n0, tw, tb,
                m.Ec * Math.Pow(hc, 3) / (12 * (1 - ConcretePoisson * ConcretePoisson)), ea * Math.Pow(tw, 3) / (12 * (1 - SteelPoisson * SteelPoisson)),
                ea * Math.Pow(tb, 3) / (12 * (1 - SteelPoisson * SteelPoisson)), Math.Max(0, g.Width / 2 - xt), Math.Max(0, g.Bottom1Width / 2 - xb), g.TopFlangeWidth * g.TopThickness);
            var mode = BoxDistortion.Mode(section);
            bool plate = o.DiaphragmKind == BridgeDiaphragmKind.Plate;
            if (!plate && o.DiaphragmKind != BridgeDiaphragmKind.CrossBracing) throw new ArgumentException("Tipo di diaframma sconosciuto.");
            double kd = spacing == 0 ? 0 : plate ? BoxDistortion.PlateDiaphragmStiffness(mode, BridgeNumbers.Require(o.DiaphragmThickness, "t_diaframma", strict: true), ea, SteelPoisson)
                : BoxDistortion.BracingStiffness(mode, ea * BridgeNumbers.Require(o.BracingArea, "A_diagonale", strict: true));
            double load = Math.Abs(mode.TorqueLoad);
            var envelope = BoxDistortion.Envelope(span, spacing, ea * mode.WarpingInertia, mode.FrameStiffness, kd, Math.Abs(mt) * 1e3 * load, Math.Abs(tc) * 1e6 * load);
            sigmaDwBottom = envelope.MaxMoment * new[] { mode.Warping[2], mode.Warping[3], mode.OutstandWarping[2], mode.OutstandWarping[3] }.Max(Math.Abs) / mode.WarpingInertia;
            sigmaDwTop = envelope.MaxMoment * Math.Max(Math.Abs(mode.Warping[0]), Math.Abs(mode.Warping[1])) / mode.WarpingInertia;
            cornerBottom = envelope.MaxAmplitude * Math.Max(mode.CornerMoments[2], mode.CornerMoments[3]);
            cornerTop = envelope.MaxAmplitude * Math.Max(mode.CornerMoments[0], mode.CornerMoments[1]);
            double ratio = sigmaBottom > 1e-9 ? sigmaDwBottom / sigmaBottom : sigmaDwBottom > 0 ? double.PositiveInfinity : 0;
            distortion = new(mode.WarpingInertia, mode.FrameStiffness, kd, mode.TorqueLoad, envelope.Diaphragms, envelope.MaxMoment, envelope.MaxAmplitude,
                envelope.MaxDiaphragmAmplitude, sigmaDwBottom, sigmaDwTop, cornerBottom, cornerTop, ratio, ratio > .1);
            double lambda = Math.Pow(mode.FrameStiffness / (4 * ea * mode.WarpingInertia), .25);
            details.AddRange(new[] { new BridgeDetailValue("Distorsione · I_Dw", mode.WarpingInertia / 1e18, "m⁶"), new("Distorsione · K telaio", mode.FrameStiffness / 1000, "kN·m/m"),
                new("Distorsione · K_D diaframma", kd / 1e9, "MN·m"), new("Distorsione · carico generalizzato di T unitario", load, "—"),
                new("Distorsione · diaframmi intermedi", envelope.Diaphragms, "—"), new("Distorsione · λ·L_D", lambda * (spacing > 0 ? spacing : span), "—"),
                new("Distorsione · ψ massimo", envelope.MaxAmplitude, "rad"), new("Distorsione · σdw fondo", sigmaDwBottom, "MPa"),
                new("Distorsione · σdw piattabande superiori", sigmaDwTop, "MPa"), new("Distorsione · σdw soletta", sigmaDwTop / n0, "MPa"),
                new("Distorsione · momento trasversale nodi inferiori", cornerBottom / 1000, "kNm/m"), new("Distorsione · momento trasversale nodi superiori", cornerTop / 1000, "kNm/m"),
                new("Distorsione · σdw / σ flessione fondo", ratio, "—") });
            // EN 1993-2 §6.2.7(3): distortional effects up to 10% of the bending effects may be disregarded in the bottom flange checks;
            // the corners, with the transverse bending of the frame, always include them
            warnings.Add($"Distorsione: σdw = {BridgeNumericFormat.Number(ratio * 100)}% della σ di flessione del fondo; " + (ratio > .1
                ? "oltre il 10% è sommata nelle verifiche del fondo (EN 1993-2 §6.2.7(3))." : "entro il 10% è trascurata nelle verifiche del fondo (EN 1993-2 §6.2.7(3)); i nodi la includono."));
            double webFoot = VonMises(Math.Abs(sigma(bottom)) + sigmaDwBottom, 6 * cornerBottom / (tw * tw), Math.Abs(tauBottom) + tauWebT);
            double flangeFoot = VonMises(sigmaBottom + sigmaDwBottom, 6 * cornerBottom / (tb * tb), tauBottomT + bottomShear);
            double webHead = VonMises(Math.Abs(sigma(top)) + sigmaDwTop, 6 * cornerTop / (tw * tw), Math.Abs(tauTop) + tauWebT);
            Check("Distorsione · nodo anima–fondo (σx, σz, τ)", Math.Max(webFoot, flangeFoot), limit, "MPa", Math.Max(webFoot, flangeFoot) / limit,
                "EN 1993-1-1 §6.2.1(5): σx con σdw, flessione trasversale σz = 6m/t² del telaio distorto (segno sfavorevole), τ di taglio e torsione; anima e fondo", true);
            Check("Distorsione · nodo anima–piattabanda superiore", webHead, limit, "MPa", webHead / limit,
                "Anima sotto la piattabanda superiore: σx con σdw, σz = 6m/tw² con il momento d'angolo al piano medio della soletta (cautelativo)", true);
            if (spacing > 0)
            {
                double amplitude = envelope.MaxDiaphragmAmplitude;
                double clear = (g.WebSpacingTop + g.WebSpacingBottom) / 2 - g.WebHorizontalThickness;
                if (plate)
                {
                    double t = o.DiaphragmThickness;
                    var (tau, vm) = BoxDistortion.PlateDiaphragmStresses(mode, t, ea, amplitude, SteelPoisson);
                    var (resistance, panel) = PanelShear(g.WebHeight, t, clear, m, gm0, gm1);
                    Check("Diaframma intermedio · taglio e imbozzamento", tau, resistance, "MPa", tau / resistance,
                        $"Piastra tra le anime (hw × {BridgeNumericFormat.Number(clear)} mm), EN 1993-1-5 §5 con η = 1, χw = {BridgeNumericFormat.Number(panel.Chi)}; aperture non considerate");
                    Check("Diaframma intermedio · tensione equivalente", vm, limit, "MPa", vm / limit, "Stato piano di tensione della piastra al ψ del diaframma", true);
                    details.Add(new("Diaframma intermedio · τ", tau, "MPa"));
                }
                else
                {
                    double area = o.BracingArea, radius = BridgeNumbers.Require(o.BracingRadius, "i_diagonale", strict: true), beta = BridgeNumbers.Require(o.BracingBucklingFactor, "beta_diagonale", strict: true);
                    var forces = BoxDistortion.BracingForces(mode, ea * area, amplitude);
                    double force = forces.Max(Math.Abs), length = BoxDistortion.BracingLengths(mode).Max();
                    double slenderness = beta * length / radius / (Math.PI * Math.Sqrt(ea / fy)), phi = .5 * (1 + .49 * (slenderness - .2) + slenderness * slenderness);
                    double chi = Math.Min(1, 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - slenderness * slenderness)))), nb = chi * area * fy / gm1;
                    Check("Diaframma intermedio · diagonale compressa", force / 1000, nb / 1000, "kN", force / nb,
                        $"EN 1993-1-1 §6.3.1, curva c, Lcr = {BridgeNumericFormat.Number(beta)}·L = {BridgeNumericFormat.Number(beta * length)} mm, λ̄ = {BridgeNumericFormat.Number(slenderness)}; collegamenti non verificati");
                    details.Add(new("Diaframma intermedio · N diagonale", force / 1000, "kN"));
                }
                details.Add(new("Diaframma intermedio · ψ", amplitude, "rad"));
            }
            else warnings.Add("Distorsione senza diaframmi intermedi: la sola rigidezza a telaio della sezione trasversale si oppone alla distorsione.");
            warnings.Add("Distorsione: analogia della trave su suolo elastico (Wright et al., 1968) sulla campata semplicemente appoggiata con diaframmi d'estremità rigidi; " +
                "m_t su tutta la luce e T_c nella posizione più sfavorevole, dello stesso segno, applicati come coppie verticali alla sommità delle anime. " +
                "Cella composta con la soletta a breve termine; accoppiamento con l'ingobbamento torsionale trascurato; inviluppi lungo la campata, indipendenti dalle fasi.");
        }
        else if (span == 0) checks.Add(new("Distorsione", 0, 0, "—", null, "Non analizzata: assegnare luce, diaframmi e torcenti dei carichi eccentrici"));

        // bottom flange between the webs: bending, torsion and shear (EN 1993-1-1 §6.2.1(5); EN 1993-1-5 §5, §7.1(5))
        double sx = sigmaBottom + (distortion?.Included == true ? sigmaDwBottom : 0);
        double tauFlange = tauBottomT + bottomShear, eq = Math.Sqrt(sx * sx + 3 * tauFlange * tauFlange);
        Check("Fondo · tensione equivalente (σ, τ torsione + taglio)", eq, limit, "MPa", eq / limit,
            "√(σ² + 3τ²) al nodo con le anime: τ = q/tb + τ di taglio del fondo" + (distortion?.Included == true ? "; σ con σdw" : ""), true);
        var (flangeResistance, flangePanel) = PanelShear(g.BottomInternalWidth, tb, spacing, m, gm0, gm1);
        double tauMean = tauBottomT + bottomShear / 2;
        Check("Fondo · imbozzamento a taglio", tauMean, flangeResistance, "MPa", tauMean / flangeResistance,
            $"EN 1993-1-5 §5 e §7.1(5): τ medio del pannello (≥ metà del massimo), η = 1, a = {(spacing > 0 ? BridgeNumericFormat.Number(spacing) + " mm" : "∞")}, χw = {BridgeNumericFormat.Number(flangePanel.Chi)}");
        double interaction = BridgeBendingShear.ElasticInteraction(sx / (fy / gm0), tauMean, flangeResistance);
        Check("Fondo · interazione σ–τ", interaction, 1, "—", interaction, "EN 1993-1-5 §7.1(5): η1 + (2η3 − 1)² con Mf,Rd = 0, η1 = σ/(fy/γM0) sulla sezione efficace");

        // slab: longitudinal reinforcement of the torsion (the transverse one and the struts are in the surfaces a–a / b–b)
        if (torsion.SlabFlow != 0)
        {
            double cot = 1.25;
            if (d.Transverse.Enabled)
            {
                cot = BridgeNumbers.Require(d.Transverse.CotTheta, "cot_trasv", strict: true);
                if (cot < 1 || cot > 1.25) throw new ArgumentException("Soletta: cot θ tra 1 e 1,25.");
            }
            double fyd = m.Fys / BridgeNumbers.Require(d.Options.GammaS, "gamma_s", strict: true), force = Math.Abs(torsion.SlabFlow) * cot;
            double concreteMean = (contributions.Sum(c => c.Stress("CLS", 0)) + contributions.Sum(c => c.Stress("CLS", hc))) / 2;
            double available = Math.Max(0, -concreteMean) * hc;
            foreach (var (row, y) in new[] { (d.TopRebars, hc - d.TopRebars.AxisDistance), (d.BottomRebars, d.BottomRebars.AxisDistance) })
                if (row.Enabled) available += Math.PI * row.Diameter * row.Diameter / 4 / row.Pitch * Math.Max(0, fyd - Math.Max(0, contributions.Sum(c => c.Stress("Armatura", y))));
            Check("Soletta · armatura longitudinale per torsione", force, available, "kN/m", available > 0 ? force / available : 2,
                $"EN 1992-1-1 §6.3.2(3): trazione q·cotθ (cotθ = {BridgeNumericFormat.Number(cot)}) contro la compressione disponibile del CLS e la capacità residua delle barre oltre la flessione");
        }
        if (torsion.SlabFlow != 0 && !d.Transverse.Enabled)
            checks.Add(new("Soletta · taglio da torsione", Math.Abs(torsion.SlabFlow), 0, "kN/m", null, "Attivare l’armatura trasversale: le superfici a–a e b–b sommano il flusso torsionale"));

        // support diaphragm: the torque to the bearings
        double supportTorque = BridgeNumbers.Require(o.SupportTorqueKNm, "T_appoggio", double.NegativeInfinity);
        if (supportTorque != 0)
        {
            double eb = BridgeNumbers.Require(o.BearingSpacing, "e_appoggi", strict: true), couple = Math.Abs(supportTorque) * 1e6 / eb;
            details.Add(new("Appoggio · coppia degli apparecchi T/e_b", couple / 1000, "kN"));
            if (Math.Abs(eb - g.WebSpacingBottom) > .1 * g.WebSpacingBottom)
                warnings.Add("Apparecchi d'appoggio non allineati alle anime: flessione del diaframma d'appoggio tra apparecchio e anima non verificata.");
            double ts = BridgeNumbers.Require(o.SupportDiaphragmThickness, "t_diaframma_appoggio");
            if (ts > 0)
            {
                var cell = SteelCell(g, 1);
                double tau = Math.Abs(supportTorque) * 1e6 / (2 * cell.Area * ts), clear = (g.WebSpacingTop + g.WebSpacingBottom) / 2 - g.WebHorizontalThickness;
                var (resistance, panel) = PanelShear(g.WebHeight, ts, clear, m, gm0, gm1);
                Check("Diaframma d'appoggio · taglio da torsione", tau, resistance, "MPa", tau / resistance,
                    $"τ = T/(2 A0 tD), A0 della cella in acciaio ({BridgeNumericFormat.Number(cell.Area / 1e6)} m²); EN 1993-1-5 §5 con η = 1, χw = {BridgeNumericFormat.Number(panel.Chi)}; aperture non considerate");
            }
            else checks.Add(new("Diaframma d'appoggio", Math.Abs(supportTorque), 0, "kNm", null, "Spessore non assegnato: diaframma d'appoggio non verificato"));
        }
        warnings.Add("Torsione del cassoncino: cella chiusa di Bredt, q = T/(2A0) tra i piani medi della soletta (del controvento nelle fasi di solo acciaio) e del fondo, " +
            "indipendente dalla rigidezza delle pareti; J con la soletta hc/nG, dimezzata se fessurata (EN 1994-2 §§5.4.2.2(11), 5.4.2.3(6)). q si somma al taglio nelle anime " +
            "(EN 1993-1-1 §6.2.7(9)), nel fondo, nei pioli e nelle superfici a–a/b–b interne alla cella (EN 1994-2 §6.6.2.1). Torsione non uniforme e distorsione della sezione aperta esclusa.");
        return torsion with { Distortion = distortion };
    }
}
