
namespace GPC.Checkers.CompositeBridge;

public sealed record BridgeLocalCheck(string Name, double Demand, double Resistance, string Unit, double? Ratio, string Note = "")
{
    public string Status => Ratio is null ? "Da verificare" : Ratio <= 1 ? "Entro limite" : "Non verificato";
}
public sealed record BridgeShearFlow(string Phase, string Kind, double V, double StaticMoment, double Inertia, double Flow, double AdditionalFlow, string Basis = "");
public sealed record BridgeShearResult(double V, WebShearResistance Web, double TauAverage, double TauMaximum,
    double EquivalentStressEnvelope, bool UsesStiffeners, TransverseStiffenerResistance? Stiffener, List<BridgeLocalCheck> Checks,
    List<BridgeDetailValue>? Details = null, bool RigidEndPost = false);
public sealed record BridgeStudResult(bool Enabled, HeadedStudResistance? Resistance, double Flow, double ForcePerStud,
    double ResistancePerStud, double ResistancePerLength, List<BridgeShearFlow> Contributions, List<BridgeLocalCheck> Checks, List<BridgeDetailValue>? Details = null);

public static partial class HBridgeSection
{
    /// <summary>The resistance of the webs from the one of a plate in its plane: forces and areas times n cos α (nothing changes for the H)</summary>
    private static WebShearResistance ScaleWeb(WebShearResistance w, double factor) => factor == 1 ? w : new()
    {
        KTau = w.KTau, TauCritical = w.TauCritical, Slenderness = w.Slenderness, Chi = w.Chi, Area = w.Area * factor, ShearArea = w.ShearArea * factor,
        PlasticResistance = w.PlasticResistance * factor, BucklingResistance = w.BucklingResistance * factor, Resistance = w.Resistance * factor
    };

    private static (BridgeShearResult Shear, BridgeStudResult Studs, BridgeTorsionResult? Torsion) CalculateShear(HBridgeInput source, BridgeGeometry g,
        BridgeMaterialValues m, BridgeEffective effective, List<BridgeContribution> contributions, BridgePhase[] phases, List<string> warnings,
        BridgeTorsionResult? torsion = null)
    {
        var d = source;
        double fy = m.Fy, ea = m.Ea, gm0 = BridgeNumbers.Require(d.Options.GammaM0, "gamma_m0", strict: true), gm1 = BridgeNumbers.Require(d.Options.GammaM1, "gamma_m1", strict: true);
        double eta = BridgeNumbers.Require(d.Options.ShearEta, "eta_taglio", strict: true), v = contributions.Sum(c => c.V), av = g.WebHeight * g.WebCount * g.PlateThickness;
        bool ultimate = d.Options.LimitStateName == "SLU", stiffened = d.Intermediate.Enabled, ntc = d.Options.StandardName.StartsWith("NTC");
        double bottom = -g.TopThickness - g.WebHeight, top = -g.TopThickness;
        double Sigma(double y) => contributions.Sum(c => c.SteelStress(y));
        double CompressionIntegral(double a, double b)
        {
            double sa = -Sigma(a), sb = -Sigma(b);
            if (sa <= 0 && sb <= 0) return 0;
            if (sa >= 0 && sb >= 0) return (sa + sb) / 2 * (b - a);
            return Math.Pow(Math.Max(sa, sb), 2) / (2 * Math.Abs(sb - sa)) * (b - a);
        }
        var checks = new List<BridgeLocalCheck>();
        var detailValues = new List<BridgeDetailValue>();
        // each web carries V / (n cos α) in its plane; the resistance of one plate is brought back to the total vertical one
        double webShare = g.WebCount * Math.Cos(g.WebAngle);
        // box with torsion: the more loaded web carries also the torsional flow q over its length (EN 1993-1-1 §6.2.7(9)); vEff is the equivalent
        // total vertical shear (the same v without torsion)
        double webFlow = torsion?.WebFlow ?? 0;
        double vEff = webFlow == 0 ? v : (v < 0 ? -1 : 1) * (Math.Abs(v) / webShare + Math.Abs(webFlow) * g.PlateLength / 1000) * webShare;
        var web = ScaleWeb(CheckStiffenerDetails(d, g, m, vEff / webShare, CompressionIntegral(bottom, top) * g.WebHorizontalThickness,
            checks, detailValues, warnings, out stiffened, out bool rigidEnd), webShare);
        TransverseStiffenerResistance? stiffener = null;
        checks.Insert(0, new("Anima · resistenza a taglio", Math.Abs(vEff), web.Resistance / 1000, "kN", ultimate ? Math.Abs(vEff) * 1000 / web.Resistance : null,
            (ultimate ? "min(Vpl,Rd; Vbw,Rd), Av = " + (ntc ? "hw·tw (NTC §4.2.4.1.2.4)" : "η·hw·tw (EN 1993-1-1 §6.2.6(3))") + "; flange omesse; montante terminale " + (rigidEnd ? "rigido verificato" : "non rigido") : "Resistenza SLU di confronto: assegnare la combinazione SLU per l’esito")
            + (webFlow == 0 ? "" : $"; anima più caricata con la torsione: V/(2 cos α) + q·hw/cos α, q = {BridgeNumericFormat.Number(webFlow)} kN/m (EN 1993-1-1 §6.2.7(9))")));
        if (!d.Support.Enabled && g.PlateLength / g.PlateThickness > 72 * Math.Sqrt(235 / fy) / eta)
            warnings.Add("EN 1993-1-5 §5.1: per questa anima snella sono necessari irrigidimenti trasversali agli appoggi, da dimensionare separatamente. L’opzione del pannello riguarda quelli intermedi.");

        var shearData = new List<(BridgeContribution C, BridgeSectionProperties P, double N)>();
        var flows = new List<BridgeShearFlow>();
        for (int i = 0; i < contributions.Count; i++)
        {
            var c = contributions[i]; var phase = phases[i];
            if (!c.IsShrinkage)
            {
                var p = GrossPhaseProperties(d, phase); double n = c.HasConcrete ? c.HomogenizationN : 0;
                shearData.Add((c, p, n));
            }
            double q = 0, s = 0, inertia = 0; string basis = "Nessuno scorrimento da V";
            if (c.Kind != "Solo acciaio" && !c.IsShrinkage && (d.Studs.Enabled || c.HasConcrete || ntc))
            {
                // NTC 4.3.4.3.3: same static properties as normal stresses.
                // EC4-2 6.6.2.1(2): uncracked concrete; keep the effective steel geometry.
                var reference = c;
                if (!ntc && !c.HasConcrete)
                {
                    var uncracked = phase with { Kind = BridgePhaseKind.Composite, ForceKN = 0, MomentKNm = 0 };
                    reference = Solve(d, g, effective, uncracked, 0);
                }
                double invN = reference.HasConcrete ? 1 / reference.HomogenizationN : 0;
                s = g.Width * g.SlabHeight * invN * (g.SlabHeight / 2 - reference.Centroid)
                    + g.Bars.Sum(b => b.Area * (m.Es / ea - invN) * (b.Y - reference.Centroid));
                inertia = reference.Inertia; q = c.V * 1000 * s / inertia;
                basis = ntc ? "NTC: proprietà della fase tensionale" : "EC4: CLS non fessurato, acciaio efficace";
            }
            flows.Add(new(c.Name, c.Kind, c.V, s, inertia, q, c.ConnectionFlowExtra, basis));
        }
        var steelParts = SteelPartProperties(g); // real plates, as the gross phase properties
        double Tau(double y)
        {
            double total = 0;
            foreach (var (c, p, n) in shearData)
            {
                double firstMoment = steelParts.Sum(part => {
                    double low = Math.Max(y, part.Bottom), height = Math.Max(0, part.Top - low);
                    return part.Width * height * ((part.Top + low) / 2 - p.Y); });
                if (c.HasConcrete) firstMoment += g.Width * g.SlabHeight / n * (g.SlabHeight / 2 - p.Y);
                if (c.Kind != "Solo acciaio") firstMoment += g.Bars.Sum(b => b.Area * (m.Es / ea - (c.HasConcrete ? 1 / n : 0)) * (b.Y - p.Y));
                total += c.V * 1000 * firstMoment / (p.Ix * g.WebCount * g.PlateThickness);
            }
            return total;
        }
        double t0 = Tau(bottom), t1 = Tau(top), tm = Tau((bottom + top) / 2);
        double qa = 2 * (t0 + t1 - 2 * tm), qb = t1 - t0 - qa;
        double tauMax = Math.Max(Math.Abs(t0), Math.Abs(t1));
        if (Math.Abs(qa) > 1e-14) { double x = -qb / (2 * qa); if (x > 0 && x < 1) tauMax = Math.Max(tauMax, Math.Abs(t0 + qb * x + qa * x * x)); }
        // the uniform τ of torsion in the webs adds to the envelope of bending shear
        if (webFlow != 0) tauMax += Math.Abs(webFlow) / g.PlateThickness;
        double sigmaMax = Math.Max(Math.Abs(Sigma(top)), Math.Abs(Sigma(bottom)));
        double eq = Math.Sqrt(sigmaMax * sigmaMax + 3 * tauMax * tauMax), steelLimit = fy / (ultimate ? gm0 : 1);
        checks.Add(new("Anima · tensione equivalente elastica (inviluppo)", eq, steelLimit, "MPa", eq / steelLimit, "√(max|σ|² + 3 max|τ|²); τ sulla sezione lorda omogeneizzata"
            + (webFlow == 0 ? "" : " più q/tw della torsione")));
        if (torsion is not null)
        {
            // bending shear in the bottom flange at the webs: first moment of half the internal plate
            double tb = g.Bottom1Thickness, yb = bottom - tb / 2, half = (g.WebSpacingBottom - g.WebHorizontalThickness) / 2;
            double bottomShear = Math.Abs(shearData.Sum(x => x.C.V * 1000 * half * tb * (yb - x.P.Y) / (x.P.Ix * tb)));
            torsion = CheckTorsion(d, g, m, torsion, contributions, Sigma, t1, t0, bottomShear, warnings);
        }
        if (ultimate && Math.Abs(vEff) * 1000 > .5 * web.Resistance)
        {
            double axial = contributions.Sum(c => c.N), moment = contributions.Sum(c => c.MomentAtInterface) * 1e6;
            bool fullyCompressedWeb = Sigma(bottom) < 0 && Sigma(top) < 0;
            if (Math.Abs(axial) < 1e-9 && fy <= 355 && !fullyCompressedWeb)
            {
                double fyd = fy / gm0;
                var flanges = new List<BridgeBendingShear.Block> { new(-g.TopThickness, 0, effective.TopWidth, fyd, fyd) };
                if (g.Bottom2Thickness > 0)
                {
                    flanges.Add(new(bottom - g.Bottom1Thickness, bottom, effective.BottomWidth, fyd, fyd));
                    flanges.Add(new(-g.Height, bottom - g.Bottom1Thickness, effective.SecondBottomWidth, fyd, fyd));
                }
                else flanges.Add(new(-g.Height, bottom, effective.BottomWidth, fyd, fyd));
                if (contributions.Any(c => c.Kind != "Solo acciaio"))
                    flanges.Add(new(0, g.SlabHeight, g.Width, .85 * BridgeNumbers.Require(d.Options.AlphaCC, "alpha_cc", strict: true) * m.Fck / BridgeNumbers.Require(d.Options.GammaC, "gamma_c", strict: true), 0));
                var full = flanges.Append(new BridgeBendingShear.Block(bottom, top, g.WebThickness, fyd, fyd));
                double mpl = BridgeBendingShear.PlasticMoment(full, moment >= 0), mf = BridgeBendingShear.PlasticMoment(flanges, moment >= 0);
                double interaction = BridgeBendingShear.Interaction(moment, mpl, mf, vEff * 1000, web.Resistance);
                checks.Add(new("Interazione M–V · classe 4", interaction, 1, "—", interaction,
                    $"EC4 §6.2.2.4(3), EC3 §7.1: Mpl={BridgeNumericFormat.Number(mpl / 1e6)} kNm; Mf={BridgeNumericFormat.Number(mf / 1e6)} kNm. Sezione composta; armature omesse a favore di sicurezza; anima intera. N=0, fy≤355."));
            }
            else
            {
                // Conservative elastic alternative: no flange reserve and no plastic redistribution.
                // EC3 7.1(5) also uses the elastic N-M utilization for a fully compressed web.
                double fyd = fy / gm0;
                double steelUsage = Math.Max(Math.Abs(Sigma(-g.Height)), Math.Abs(Sigma(0))) / fyd;
                double concreteUsage = 0;
                if (contributions.Any(c => c.HasConcrete))
                {
                    double fcd = .85 * BridgeNumbers.Require(d.Options.AlphaCC, "alpha_cc", strict: true) * m.Fck / BridgeNumbers.Require(d.Options.GammaC, "gamma_c", strict: true);
                    concreteUsage = new[] { 0d, g.SlabHeight }.Max(y => Math.Max(0, -contributions.Sum(c => c.Stress("CLS", y)))) / fcd;
                }
                double barUsage = g.Bars.Select(b => Math.Abs(contributions.Sum(c => c.Stress("Armatura", b.Y)))
                    / (m.Fys / BridgeNumbers.Require(d.Options.GammaS, "gamma_s", strict: true))).DefaultIfEmpty().Max();
                double etaNormal = Math.Max(steelUsage, Math.Max(concreteUsage, barUsage));
                double penalty = Math.Pow(2 * Math.Abs(vEff) * 1000 / web.Resistance - 1, 2);
                double ratio = BridgeBendingShear.ElasticInteraction(etaNormal, vEff * 1000, web.Resistance);
                checks.Add(new("Interazione N–M–V · inviluppo elastico", ratio, 1, "—", ratio,
                    "EN 1993-1-5 §7.1: inviluppo cautelativo, Mf=0 e utilizzo elastico N–M della sezione efficace; CLS compresso limitato a 0,85 fcd. Nessuna capacità plastica accreditata, anche per S420/S460. Non sostituisce i controlli tensionali."));
                detailValues.AddRange(new[] { new BridgeDetailValue("N–M–V · utilizzo normale elastico", etaNormal, "—"), new("N–M–V · termine di taglio", penalty, "—") });
            }
        }
        var shear = new BridgeShearResult(v, web, v * 1000 / av, tauMax, eq, stiffened, stiffener, checks, detailValues, rigidEnd);
        double totalFlow = flows.Sum(f => f.Flow + f.AdditionalFlow);
        if (!d.Studs.Enabled || contributions.All(c => c.Kind == "Solo acciaio")) return (shear, new(false, null, totalFlow, 0, 0, 0, flows, []), torsion);
        // box with torsion: on one top flange the flow of the slab adds to half the bending flow
        double slabFlow = torsion?.SlabFlow ?? 0;
        int rows = (int)BridgeNumbers.Require(d.Studs.CountPerRow, "n_pioli", 1);
        if (rows != d.Studs.CountPerRow || rows > 20) throw new ArgumentException("Numero di pioli per fila: intero da 1 a 20.");
        double diameter = BridgeNumbers.Require(d.Studs.Diameter, "d_pioli", strict: true), heightStud = BridgeNumbers.Require(d.Studs.Height, "h_pioli", strict: true), pitch = BridgeNumbers.Require(d.Studs.LongitudinalPitch, "passo_pioli", strict: true);
        double transversePitch = rows > 1 ? BridgeNumbers.Require(d.Studs.TransversePitch, "passo_trasv_pioli", strict: true) : 0;
        var stud = BridgeShearConnection.Stud(diameter, heightStud, BridgeNumbers.Require(d.Studs.Fu, "fu_pioli", strict: true), m.Fck, m.Ec, BridgeNumbers.Require(d.Studs.GammaV, "gamma_v", strict: true));
        double prd = stud.Resistance * (ultimate ? 1 : .75), ped = slabFlow == 0 ? Math.Abs(totalFlow) * pitch / (rows * g.TopFlangeCount)
            : (Math.Abs(totalFlow) / g.TopFlangeCount + Math.Abs(slabFlow)) * pitch / rows;
        bool validCombination = ultimate || d.Options.LimitStateName == "SLE rara";
        double edgeClear = (g.TopFlangeWidth - (rows - 1) * transversePitch - diameter) / 2;
        double edgeMin = ntc ? 20 : 25;
        double cover = g.SlabHeight - heightStud;
        double requiredCover = Math.Max(ntc ? 20 : 0, BridgeNumbers.Require(d.Studs.RequiredCover, "copriferro_pioli"));
        if (!ntc && d.TopRebars.Enabled) requiredCover = Math.Max(requiredCover, d.TopRebars.AxisDistance - d.TopRebars.Diameter / 2);
        var studChecks = new List<BridgeLocalCheck> {
            new("Piolo · taglio longitudinale", ped / 1000, prd / 1000, "kN", validCombination ? ped / prd : null, (ultimate ? "EN 1994-2 §6.6.3.1 / NTC §4.3.4.3.1" : "0,75 PRd; esito disponibile soltanto con la combinazione caratteristica/rara")
                + (slabFlow == 0 ? "" : $"; piattabanda più caricata: metà del flusso di flessione + q di torsione = {BridgeNumericFormat.Number(slabFlow)} kN/m")),
            new("Pioli · passo longitudinale minimo", 5 * diameter, pitch, "mm", 5 * diameter / pitch),
            new("Pioli · passo longitudinale massimo", pitch, Math.Min(800, 4 * g.SlabHeight), "mm", pitch / Math.Min(800, 4 * g.SlabHeight)),
            new("Pioli · bordo libero piattabanda", edgeMin, edgeClear, "mm", edgeClear > 0 ? edgeMin / edgeClear : null),
            new("Pioli · copriferro superiore", requiredCover, cover, "mm", cover > 0 ? requiredCover / cover : null),
            new("Pioli · diametro testa", 1.5 * diameter, BridgeNumbers.Require(d.Studs.HeadDiameter, "d_testa_pioli", strict: true), "mm", 1.5 * diameter / BridgeNumbers.Require(d.Studs.HeadDiameter, "d_testa_pioli", strict: true)),
            new("Pioli · spessore testa", .4 * diameter, BridgeNumbers.Require(d.Studs.HeadThickness, "t_testa_pioli", strict: true), "mm", .4 * diameter / BridgeNumbers.Require(d.Studs.HeadThickness, "t_testa_pioli", strict: true)),
            new("Pioli · diametro/spessore piattabanda", diameter, (d.Studs.RepeatedActionDetail || d.Fatigue.Enabled ? 1.5 : 2.5) * g.TopThickness, "mm", diameter / ((d.Studs.RepeatedActionDetail || d.Fatigue.Enabled ? 1.5 : 2.5) * g.TopThickness),
                d.Studs.RepeatedActionDetail || d.Fatigue.Enabled ? "Dettaglio per azioni ripetute, obbligatorio anche con verifica resistente a fatica attiva" : "Limite 2,5 tf adottato anche sopra l’anima") };
        if (rows > 1) studChecks.Add(new("Pioli · passo trasversale minimo", 2.5 * diameter, transversePitch, "mm", 2.5 * diameter / transversePitch));
        if (d.BottomRebars.Enabled)
        {
            double clearance = heightStud - d.Studs.HeadThickness - d.BottomRebars.AxisDistance - d.BottomRebars.Diameter / 2;
            studChecks.Add(new("Pioli · testa sopra armatura inferiore", 30, clearance, "mm", clearance > 0 ? 30 / clearance : null, "EN 1994-2 §6.6.5.1"));
        }
        if (contributions.Any(c => c.IsShrinkage)) warnings.Add("Pioli: il ritiro uniforme non genera un gradiente longitudinale nel tratto uniforme. Assegnare Δq aggiuntivo per effetti di estremità/vincolo ricavati dal modello globale.");
        var connectionDetails = new List<BridgeDetailValue>();
        // box: the connection is on the two top flanges, each with its rows of studs and its surfaces of the slab
        CheckSlabAndFatigue(d, g, m, totalFlow / g.TopFlangeCount, studChecks, connectionDetails, warnings, slabFlow);
        if (g.SectionType == BridgeSteelSectionType.Box)
            warnings.Add("Cassoncino: pioli e superfici della soletta per ciascuna delle due piattabande superiori, con metà del flusso di scorrimento" +
                (slabFlow == 0 ? "" : " più il flusso torsionale q (le superfici a–a interne alla cella e b–b)") + "; la fatica dei pioli usa i flussi assegnati per piattabanda.");
        return (shear, new(true, stud, totalFlow, ped / 1000, prd / 1000, rows * g.TopFlangeCount * prd / pitch, flows, studChecks, connectionDetails), torsion);
    }
}
