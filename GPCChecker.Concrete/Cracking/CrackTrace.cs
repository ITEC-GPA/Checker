using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>
    /// Fine reason of a crack result (0.0.18.0), always set: it separates the three branches that <see cref="CrackOutcome.NoEffectiveArea"/> joins and marks the
    /// entirely compressed section, the only <see cref="CrackOutcome.Evaluated"/> branch without an entry of its own in the trace (the others have one: neutral
    /// axis in the cover <see cref="CrackTraceCodes.NearestBarDepth"/>, upper bound <see cref="CrackTraceFlags.UpperBound"/>, entirely tensile section
    /// <see cref="CrackTraceCodes.GoverningFace"/>). <see cref="None"/> for every other result, where <see cref="SectionCrackResult.Outcome"/> says it all.
    /// </summary>
    public enum CrackReason
    {
        /// <summary>No special reason: the outcome is enough.</summary>
        None,
        /// <summary>Partially compressed section with hc,eff ≤ 0 (tensile bars on or beyond the tensile edge). ANTHEA: "Area efficace nulla".</summary>
        ZeroEffectiveDepth,
        /// <summary>Partially compressed section with effective bars but Ac,eff ≤ 0. ANTHEA: "Armatura/area efficace assente".</summary>
        NoEffectiveSteelOrArea,
        /// <summary>Entirely tensile section: a face or radial band without effective area or steel. ANTHEA: "&lt;face&gt;: area o armatura efficace assente".</summary>
        FaceWithoutAreaOrSteel,
        /// <summary>
        /// Entirely compressed section (εc,max ≤ 1e-12 at the vertices, holes included): wk = 0, no k2, outcome <see cref="CrackOutcome.Evaluated"/>
        /// (ANTHEA "Sezione interamente compressa", Ntc2018Checks.cs:124). Added by the prototype cycle of ANTHEA F2.7 (0.0.18.0).
        /// </summary>
        EntirelyCompressed
    }

    /// <summary>
    /// Outcome of one region of the crack check (0.0.18.0, with <see cref="SectionCrackOptions.Trace"/>): key of <see cref="CrackRegion.Key"/>, outcome and
    /// width (mm, null when the region has no width). "InnerSurfaces" is the key of the inner surfaces that are not supported (no region).
    /// </summary>
    public sealed class CrackRegionOutcome
    {
        public string Key { get; }
        /// <summary>Evaluated, or the outcome that stopped the region: NoEffectiveArea, SpacingUndetermined, InnerSurfaceUnreinforced, InnerSurfaceNotSupported.</summary>
        public CrackOutcome Outcome { get; }
        public double? Width { get; }
        public CrackRegionOutcome(string key, CrackOutcome outcome, double? width) { Key = key; Outcome = outcome; Width = width; }
        public override string ToString() => Key + ": " + Outcome + (Width.HasValue ? " " + Width.Value.ToString("R", CultureInfo.InvariantCulture) + " mm" : "");
    }

    /// <summary>
    /// One entry of the trace of the crack check (0.0.18.0, <see cref="SectionCrackOptions.Trace"/>): a calculated value with a stable code
    /// (<see cref="CrackTraceCodes"/>), the region it belongs to, its unit, the numbers of its expression (<see cref="CrackTraceArguments"/>) and the
    /// flags of its branch (<see cref="CrackTraceFlags"/>). The texts are left to the caller.
    /// </summary>
    public sealed class CrackTraceEntry
    {
        /// <summary>Stable code, one of <see cref="CrackTraceCodes"/>.</summary>
        public string Code { get; }
        /// <summary>
        /// Key of the region (<see cref="CrackRegion.Key"/>: Face+x, Radial(angle), DsCoarseSystem, InnerWall+x, InnerRing...) for the entries of a face, of a
        /// radial band, of the DS coarse system or of an inner surface; null for the entries of the section. ANTHEA writes these entries with the name of the
        /// region as prefix ("Faccia +x · hc,eff"), except those with <see cref="CrackTraceFlags.Summary"/>, which repeat the governing region in the summary.
        /// </summary>
        public string Region { get; }
        /// <summary>Value; null where ANTHEA writes a note without value or a value it cannot compute (spacing not determined, ηw suspended).</summary>
        public double? Value { get; }
        /// <summary>Unit: −, mm, mm2, MPa, 1/mm.</summary>
        public string Unit { get; }
        /// <summary>Numbers of the expression, by name (<see cref="CrackTraceArguments"/>); <see cref="CrackTraceArguments.Bar"/> can repeat.</summary>
        public IReadOnlyList<KeyValuePair<string, double>> Arguments { get; }
        /// <summary>Flags of the branch (<see cref="CrackTraceFlags"/>).</summary>
        public IReadOnlyList<string> Flags { get; }

        public CrackTraceEntry(string code, string region, double? value, string unit, IEnumerable<KeyValuePair<string, double>> arguments, IEnumerable<string> flags)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Region = region; Value = value; Unit = unit ?? "";
            Arguments = (arguments ?? Enumerable.Empty<KeyValuePair<string, double>>()).ToArray();
            Flags = (flags ?? Enumerable.Empty<string>()).ToArray();
        }

        public bool HasFlag(string flag) => Flags.Contains(flag);

        /// <summary>First argument with the name; null when absent.</summary>
        public double? Argument(string name)
        {
            foreach (var argument in Arguments) if (argument.Key == name) return argument.Value;
            return null;
        }

        /// <summary>Every argument with the name, in order (the bars of a region).</summary>
        public IEnumerable<double> ArgumentsNamed(string name) => Arguments.Where(a => a.Key == name).Select(a => a.Value);

        internal CrackTraceEntry WithFlag(string flag) => new CrackTraceEntry(Code, Region, Value, Unit, Arguments, Flags.Concat(new[] { flag }));

        public override string ToString()
            => (Region != null ? Region + " · " : "") + Code + " = " + (Value.HasValue ? Value.Value.ToString("R", CultureInfo.InvariantCulture) : "—") + " " + Unit
            + (Arguments.Count > 0 ? " (" + string.Join(", ", Arguments.Select(a => a.Key + " = " + a.Value.ToString("R", CultureInfo.InvariantCulture))) + ")" : "")
            + (Flags.Count > 0 ? " [" + string.Join(", ", Flags) + "]" : "");
    }

    /// <summary>
    /// Stable codes of the trace of <see cref="SectionCrackCheck"/> (0.0.18.0). The trace lists the calculated entries of ANTHEA (X.Calculations at commit
    /// 2d40a95, the lines cited here: Ntc2018Checks.Cracking, ConcreteTensionCracking, ConcreteInnerCracking, ConcreteCodeChecks.CrackWidth and
    /// UnbondedCrackWidthBound, Ntc2018Checks.CalculateCrackWidth) in the same order; each code names one symbol of ANTHEA (given here), the flags tell the
    /// variants of its text.
    /// The entries that repeat the inputs (Verifica, Modello, N, Mx, My, φ, γc, γs, Normativa fessurazione, Criterio, wlim) are not traced.
    /// </summary>
    public static class CrackTraceCodes
    {
        // ---- Decompression and crack formation (Ntc2018Checks.cs:84-88)
        /// <summary>"Analisi ausiliaria", no value: uncracked homogenized section, linear, tensile concrete.</summary>
        public const string AuxiliaryAnalysis = "AuxiliaryAnalysis";
        /// <summary>"fctm", MPa.</summary>
        public const string Fctm = "Fctm";
        /// <summary>"σct,max", MPa: maximum stress of the uncracked section.</summary>
        public const string UncrackedMaximumStress = "UncrackedMaximumStress";
        /// <summary>"σct,lim", MPa: 0 (<see cref="CrackTraceFlags.Decompression"/>) or fctm/1.2 (<see cref="CrackTraceFlags.CrackFormation"/>).</summary>
        public const string UncrackedStressLimit = "UncrackedStressLimit";
        /// <summary>"Divisore formazione" = 1.2, crack formation only.</summary>
        public const string FormationDivisor = "FormationDivisor";

        // ---- Bars and k2 of the section (Ntc2018Checks.cs:102-116, :131-141; ConcreteTensionCracking.cs:16-18)
        /// <summary>"Barre compresse" (σs &lt; 0, all the ordinary bars); "Barre compresse per k₂" with <see cref="CrackTraceFlags.K2FromBars"/>.</summary>
        public const string CompressedBars = "CompressedBars";
        /// <summary>"Barre tese" (σs &gt; 0); "Barre tese per k₂" with <see cref="CrackTraceFlags.K2FromBars"/>.</summary>
        public const string TensileBars = "TensileBars";
        /// <summary>"Barre a tensione nulla" (σs = 0); "... per k₂" with <see cref="CrackTraceFlags.K2FromBars"/>.</summary>
        public const string ZeroStressBars = "ZeroStressBars";
        /// <summary>
        /// "Criterio k₂": <see cref="CrackTraceFlags.K2FromBars"/> (rule before D7-b, :108, with <see cref="CrackTraceFlags.CompressedBar"/> and
        /// <see cref="CrackTraceFlags.ZeroStressBars"/>), <see cref="CrackTraceFlags.Bending"/> (:136, with <see cref="CrackTraceFlags.NotInWidthFormula"/> for MC2010
        /// and DIN and <see cref="CrackTraceFlags.InnerBands"/> with holes), <see cref="CrackTraceFlags.PartiallyCompressed"/> (:141, rule before D7-b with a
        /// profile other than NTC), <see cref="CrackTraceFlags.EntirelyTensile"/> (ConcreteTensionCracking.cs:17, which removes the earlier entries).
        /// </summary>
        public const string K2Criterion = "K2Criterion";

        // ---- Strain plane (Ntc2018Checks.cs:120-122, :144-158)
        /// <summary>"εc,min": minimum strain at the vertices.</summary>
        public const string MinimumStrain = "MinimumStrain";
        /// <summary>"εc,max".</summary>
        public const string MaximumStrain = "MaximumStrain";
        /// <summary>"Tolleranza compressione" = 1e-12.</summary>
        public const string CompressionTolerance = "CompressionTolerance";
        /// <summary>"χx", 1/mm.</summary>
        public const string ChiX = "ChiX";
        /// <summary>"χy", 1/mm.</summary>
        public const string ChiY = "ChiY";
        /// <summary>"|∇ε|", 1/mm.</summary>
        public const string StrainGradient = "StrainGradient";
        /// <summary>"qx" = χx/|∇ε|.</summary>
        public const string DirectionX = "DirectionX";
        /// <summary>"qy" = χy/|∇ε|.</summary>
        public const string DirectionY = "DirectionY";
        /// <summary>"Qmax", mm.</summary>
        public const string Qmax = "Qmax";
        /// <summary>"Qmin", mm.</summary>
        public const string Qmin = "Qmin";
        /// <summary>"h" = Qmax − Qmin, mm.</summary>
        public const string Height = "Height";
        /// <summary>"h − x" = εc,max/|∇ε|, mm.</summary>
        public const string TensileDepth = "TensileDepth";
        /// <summary>"x" = h − (h − x), mm.</summary>
        public const string CompressedDepth = "CompressedDepth";

        // ---- Effective area of the partially compressed section (Ntc2018Checks.cs:167-189)
        /// <summary>"h − d,min": neutral axis in the cover, Qmax − Q of the bar nearest to the tensile edge, mm.</summary>
        public const string NearestBarDepth = "NearestBarDepth";
        /// <summary>"Numero barre tese" (ε &gt; 0).</summary>
        public const string TensileBarCount = "TensileBarCount";
        /// <summary>"QG,s": centroid of the tensile bars along the gradient, mm.</summary>
        public const string TensileCentroid = "TensileCentroid";
        /// <summary>"h − d" = Qmax − QG,s, mm.</summary>
        public const string CoverToCentroid = "CoverToCentroid";
        /// <summary>"d" = h − (h − d), mm.</summary>
        public const string EffectiveHeight = "EffectiveHeight";
        /// <summary>"Candidato 1 hc,eff" = 2.5 (h − d), mm.</summary>
        public const string EffectiveDepthCandidate1 = "EffectiveDepthCandidate1";
        /// <summary>"Candidato 2 hc,eff" = (h − x)/3, mm.</summary>
        public const string EffectiveDepthCandidate2 = "EffectiveDepthCandidate2";
        /// <summary>"Candidato 3 hc,eff" = h/2, mm.</summary>
        public const string EffectiveDepthCandidate3 = "EffectiveDepthCandidate3";
        /// <summary>
        /// "hc,eff", mm: section (:179, <see cref="CrackTraceFlags.MinimumOfThree"/>, <see cref="CrackTraceFlags.DinCoefficient"/> or
        /// <see cref="CrackTraceFlags.DsBand"/>), face of an entirely tensile section (ConcreteTensionCracking.cs:39, <see cref="CrackTraceFlags.EntirelyTensile"/>),
        /// inner band (ConcreteInnerCracking.cs:68, <see cref="CrackTraceFlags.InnerBand"/>).
        /// </summary>
        public const string EffectiveDepth = "EffectiveDepth";
        /// <summary>"Qtaglio" = Qmax − hc,eff, mm.</summary>
        public const string CutLevel = "CutLevel";
        /// <summary>
        /// "Ac,eff", mm2: section (:188), face (ConcreteTensionCracking.cs:40, <see cref="CrackTraceFlags.EntirelyTensile"/>), inner band
        /// (ConcreteInnerCracking.cs:82, <see cref="CrackTraceFlags.InnerBand"/>), governing region in the summary (ConcreteTensionCracking.cs:72,
        /// ConcreteInnerCracking.cs:125, <see cref="CrackTraceFlags.Summary"/>: the text is the name of <see cref="CrackTraceEntry.Region"/>).
        /// </summary>
        public const string EffectiveArea = "EffectiveArea";
        /// <summary>"Numero barre efficaci": tensile bars with Q ≥ Qtaglio − 1e-8 mm.</summary>
        public const string EffectiveBarCount = "EffectiveBarCount";

        // ---- Tensile bars of the partially compressed section, in the bar order (Ntc2018Checks.cs:190-203); argument Bar (0-based, ANTHEA "B01" = 0)
        /// <summary>"Bnn · ε": <see cref="CrackTraceFlags.Included"/> in As,eff or <see cref="CrackTraceFlags.Excluded"/>.</summary>
        public const string BarStrain = "BarStrain";
        /// <summary>"Bnn · x", mm.</summary>
        public const string BarX = "BarX";
        /// <summary>"Bnn · y", mm.</summary>
        public const string BarY = "BarY";
        /// <summary>"Bnn · Q", mm.</summary>
        public const string BarQ = "BarQ";
        /// <summary>"Bnn · Ø", mm.</summary>
        public const string BarDiameter = "BarDiameter";
        /// <summary>"Bnn · As", mm2.</summary>
        public const string BarArea = "BarArea";
        /// <summary>"Bnn · σs", MPa.</summary>
        public const string BarStress = "BarStress";

        // ---- Effective steel, cover and spacing (Ntc2018Checks.cs:209-238; ConcreteCodeChecks.cs:206-211)
        /// <summary>"Øeq", mm: tensile bars without bonded bars (:209, <see cref="CrackTraceFlags.TensileBars"/>), effective bars (:225,
        /// <see cref="CrackTraceFlags.EffectiveBars"/>), Eurocode formula (ConcreteCodeChecks.cs:209, <see cref="CrackTraceFlags.Formula"/>).</summary>
        public const string EquivalentDiameter = "EquivalentDiameter";
        /// <summary>"σs", MPa: <see cref="CrackTraceFlags.TensileBars"/> (:210), <see cref="CrackTraceFlags.EffectiveBars"/> (:226), <see cref="CrackTraceFlags.Formula"/>
        /// (ConcreteCodeChecks.cs:208).</summary>
        public const string SteelStress = "SteelStress";
        /// <summary>"As,eff", mm2: section (:222), face or inner band (ConcreteTensionCracking.cs:41, ConcreteInnerCracking.cs:83: one argument Bar per bar),
        /// governing region in the summary (<see cref="CrackTraceFlags.Summary"/>).</summary>
        public const string EffectiveSteel = "EffectiveSteel";
        /// <summary>"ΣØ²", mm2.</summary>
        public const string DiameterSquareSum = "DiameterSquareSum";
        /// <summary>"ΣØ", mm.</summary>
        public const string DiameterSum = "DiameterSum";
        /// <summary>"c", mm: section (:227, <see cref="CrackTraceFlags.Assigned"/> or <see cref="CrackTraceFlags.Nominal"/>), Eurocode formula (<see cref="CrackTraceFlags.Formula"/>).</summary>
        public const string Cover = "Cover";
        /// <summary>"s", mm: section (:232, <see cref="CrackTraceFlags.Automatic"/> or <see cref="CrackTraceFlags.Manual"/>; null value when the automatic spacing is
        /// not determined), Eurocode formula (<see cref="CrackTraceFlags.Formula"/>).</summary>
        public const string Spacing = "Spacing";
        /// <summary>"Ecls analisi", MPa: modulus of the analysis, only with <see cref="SectionCrackOptions.AnalysisConcreteModulus"/>.</summary>
        public const string AnalysisConcreteModulus = "AnalysisConcreteModulus";
        /// <summary>"n analisi" = Es (1 + φ)/Ecls, only with the analysis context.</summary>
        public const string AnalysisModularRatio = "AnalysisModularRatio";

        // ---- Width formula, NTC 2018 (Ntc2018Checks.cs:269-300) and Eurocode family (ConcreteCodeChecks.cs:206-211)
        /// <summary>"Es", MPa (NTC).</summary>
        public const string Es = "Es";
        /// <summary>"Ecm", MPa (NTC).</summary>
        public const string Ecm = "Ecm";
        /// <summary>"fct,eff = fctm", MPa (NTC).</summary>
        public const string EffectiveTensileStrength = "EffectiveTensileStrength";
        /// <summary>"ρp,eff" = As,eff/Ac,eff.</summary>
        public const string Rho = "Rho";
        /// <summary>"αe" = Es/Ecm.</summary>
        public const string AlphaE = "AlphaE";
        /// <summary>"kt": NTC with <see cref="CrackTraceFlags.ShortTerm"/> or <see cref="CrackTraceFlags.LongTerm"/>.</summary>
        public const string Kt = "Kt";
        /// <summary>"k₁" (NTC): <see cref="CrackTraceFlags.Ribbed"/> 0.8 or <see cref="CrackTraceFlags.Plain"/> 1.6.</summary>
        public const string K1 = "K1";
        /// <summary>"k₂" of the formula; Eurocode family with <see cref="CrackTraceFlags.NotInWidthFormula"/> for MC2010 and DIN (rule after D7-b).</summary>
        public const string K2 = "K2";
        /// <summary>"k₃" = 3.4 (NTC).</summary>
        public const string K3 = "K3";
        /// <summary>"k₄" = 0.425 (NTC).</summary>
        public const string K4 = "K4";
        /// <summary>"β minimo deformazione": 0.6 (NTC, Eurocode), 1 − kt (MC2010); upper bound with <see cref="CrackTraceFlags.UpperBound"/>.</summary>
        public const string BetaMinimum = "BetaMinimum";
        /// <summary>"β apertura" = 1.7 (NTC).</summary>
        public const string BetaWidth = "BetaWidth";
        /// <summary>"Coefficiente regione distante" = 0.75 (NTC).</summary>
        public const string FarRegionCoefficient = "FarRegionCoefficient";
        /// <summary>"Coefficiente soglia interasse" = 5 (NTC).</summary>
        public const string SpacingThresholdCoefficient = "SpacingThresholdCoefficient";
        /// <summary>"σs (formula)", MPa (NTC).</summary>
        public const string FormulaSteelStress = "FormulaSteelStress";
        /// <summary>"Øeq (formula)", mm (NTC).</summary>
        public const string FormulaDiameter = "FormulaDiameter";
        /// <summary>"c (formula)", mm (NTC).</summary>
        public const string FormulaCover = "FormulaCover";
        /// <summary>"s (formula)", mm (NTC).</summary>
        public const string FormulaSpacing = "FormulaSpacing";
        /// <summary>"h − x (formula)", mm (NTC).</summary>
        public const string FormulaTensileDepth = "FormulaTensileDepth";
        /// <summary>"1 + αe·ρp,eff" (NTC).</summary>
        public const string InteractionFactor = "InteractionFactor";
        /// <summary>"Δσ tension stiffening" = kt fct/ρ (1 + αe ρ), MPa (NTC).</summary>
        public const string TensionStiffening = "TensionStiffening";
        /// <summary>"Δε calcolata" = (σs − Δσ)/Es (NTC); arguments SteelStress, TensionStiffening, Es.</summary>
        public const string ComputedStrainDifference = "ComputedStrainDifference";
        /// <summary>"Δε minima" = 0.6 σs/Es (NTC); arguments SteelStress, Es.</summary>
        public const string MinimumStrainDifference = "MinimumStrainDifference";
        /// <summary>"εsm − εcm": NTC with <see cref="CrackTraceFlags.ComputedGoverns"/> or <see cref="CrackTraceFlags.MinimumGoverns"/>; Eurocode formula; upper bound
        /// with <see cref="CrackTraceFlags.UpperBound"/>.</summary>
        public const string MeanStrainDifference = "MeanStrainDifference";
        /// <summary>"Termine copriferro" = k₃ c, mm (NTC).</summary>
        public const string CoverTerm = "CoverTerm";
        /// <summary>"Termine armatura" = k₁ k₂ k₄ Øeq/ρp,eff, mm (NTC).</summary>
        public const string ReinforcementTerm = "ReinforcementTerm";
        /// <summary>"Δsm,vicino", mm (NTC).</summary>
        public const string NearSpacing = "NearSpacing";
        /// <summary>"s_lim" = 5 (c + Øeq/2), mm (NTC).</summary>
        public const string SpacingLimit = "SpacingLimit";
        /// <summary>"s − s_lim", mm (NTC): <see cref="CrackTraceFlags.CloseBars"/> or <see cref="CrackTraceFlags.SparseBars"/>.</summary>
        public const string SpacingExcess = "SpacingExcess";
        /// <summary>"Δsm,distante" = 0.75 (h − x), mm (NTC): with <see cref="CrackTraceFlags.CloseBars"/> not used, with <see cref="CrackTraceFlags.SparseBars"/> a candidate.</summary>
        public const string FarSpacing = "FarSpacing";
        /// <summary>"Δsm adottata", mm (NTC): <see cref="CrackTraceFlags.CloseBars"/> or <see cref="CrackTraceFlags.SparseBars"/>, and
        /// <see cref="CrackTraceFlags.NearGoverns"/> or <see cref="CrackTraceFlags.FarGoverns"/>.</summary>
        public const string AdoptedSpacing = "AdoptedSpacing";
        /// <summary>"sr,max", mm: variant of the formula in the flags (<see cref="CrackTraceFlags.FormulaStandard"/>, <see cref="CrackTraceFlags.FormulaModelCode2010"/>,
        /// <see cref="CrackTraceFlags.FormulaDin"/>, <see cref="CrackTraceFlags.FormulaSparseBars"/>, <see cref="CrackTraceFlags.UpperBoundNtc"/>,
        /// <see cref="CrackTraceFlags.UpperBoundEurocode"/>, <see cref="CrackTraceFlags.UpperBoundDin"/>).</summary>
        public const string MaximumCrackSpacing = "MaximumCrackSpacing";
        /// <summary>
        /// "wk", mm: NTC formula (arguments AdoptedSpacing, MeanStrainDifference), Eurocode formula, upper bound (<see cref="CrackTraceFlags.UpperBound"/>), half
        /// of the DS coarse system (ConcreteTensionCracking.cs:66, <see cref="CrackTraceFlags.DsCoarseHalf"/>), envelope of the outer and inner surfaces
        /// (ConcreteInnerCracking.cs:127, <see cref="CrackTraceFlags.Envelope"/>).
        /// </summary>
        public const string Width = "Width";
        /// <summary>"ηw" = wk/wlim (Ntc2018Checks.cs:214, :241; ConcreteTensionCracking.cs:72; ConcreteInnerCracking.cs:128 with <see cref="CrackTraceFlags.Envelope"/>,
        /// null value when a surface is left without a check and wk ≤ wlim).</summary>
        public const string WidthRatio = "WidthRatio";

        // ---- Entirely tensile section (ConcreteTensionCracking.cs)
        /// <summary>"k₂ · interamente tesa" (:18); <see cref="CrackTraceFlags.K2FromBars"/> for the note of the rule before D7-b.</summary>
        public const string EntirelyTensileK2 = "EntirelyTensileK2";
        /// <summary>Header of a face or radial band without value (:38, the symbol is the name of <see cref="CrackTraceEntry.Region"/>); radial bands with the argument Angle.</summary>
        public const string RegionCheck = "RegionCheck";
        /// <summary>"Faccia governante" = wk of the governing face (:71, the text is the name of <see cref="CrackTraceEntry.Region"/>).</summary>
        public const string GoverningFace = "GoverningFace";

        // ---- Inner surfaces of hollow sections (ConcreteInnerCracking.cs)
        /// <summary>"h − x della fascia", mm (:70-73): <see cref="CrackTraceFlags.Uniform"/> (arguments GradientTimesHeight, Tolerance) or
        /// <see cref="CrackTraceFlags.BoundedByHeight"/> (argument FromNeutralAxis); written only in these two cases.</summary>
        public const string BandTensileDepth = "BandTensileDepth";
        /// <summary>"k₂ della fascia" (:77-79): <see cref="CrackTraceFlags.Uniform"/> or <see cref="CrackTraceFlags.Local"/>; not written with the rule before D7-b.</summary>
        public const string BandK2 = "BandK2";
        /// <summary>"Superficie governante", no value (:123): an inner surface governs (<see cref="CrackTraceEntry.Region"/>).</summary>
        public const string GoverningSurface = "GoverningSurface";
        /// <summary>"Contorno interno", no value (:130): note of the inner boundary.</summary>
        public const string InnerBoundaryNote = "InnerBoundaryNote";
    }

    /// <summary>Flags of the trace entries (<see cref="CrackTraceEntry.Flags"/>): the branch that chooses the text of ANTHEA.</summary>
    public static class CrackTraceFlags
    {
        /// <summary>Entry of the governing region repeated in the summary: ANTHEA writes it without the region prefix.</summary>
        public const string Summary = "Summary";
        /// <summary>Rule of k₂ before D7-b (<see cref="SectionCrackInput.NtcK2FromCompressedBars"/>).</summary>
        public const string K2FromBars = "K2FromBars";
        /// <summary>With <see cref="K2FromBars"/>: at least one compressed ordinary bar (k₂ = 0.5; otherwise 1).</summary>
        public const string CompressedBar = "CompressedBar";
        /// <summary>With <see cref="K2FromBars"/>: some bars have exactly zero stress (not compressed).</summary>
        public const string ZeroStressBars = "ZeroStressBars";
        /// <summary>Neutral axis inside the section: k₂ = 0.5 (D7-b).</summary>
        public const string Bending = "Bending";
        /// <summary>MC2010 and DIN: k₂ does not enter sr,max (rule after D7-b).</summary>
        public const string NotInWidthFormula = "NotInWidthFormula";
        /// <summary>Hollow section: the inner bands use the k₂ of their own band.</summary>
        public const string InnerBands = "InnerBands";
        /// <summary>Rule before D7-b with a profile other than NTC: partially compressed section, k₂ = 0.5.</summary>
        public const string PartiallyCompressed = "PartiallyCompressed";
        /// <summary>Entirely tensile section: k₂ of the strains, entries of a face.</summary>
        public const string EntirelyTensile = "EntirelyTensile";
        /// <summary>Decompression (σct,lim = 0).</summary>
        public const string Decompression = "Decompression";
        /// <summary>Crack formation (σct,lim = fctm/1.2).</summary>
        public const string CrackFormation = "CrackFormation";
        /// <summary>hc,eff = min[2.5 (h − d); (h − x)/3; h/2].</summary>
        public const string MinimumOfThree = "MinimumOfThree";
        /// <summary>hc,eff of DIN NCI 7.3.2(3).</summary>
        public const string DinCoefficient = "DinCoefficient";
        /// <summary>hc,eff of DK NA Fig. 7.100: band with the centroid at the tensile steel.</summary>
        public const string DsBand = "DsBand";
        /// <summary>Entry of an inner wall or ring.</summary>
        public const string InnerBand = "InnerBand";
        /// <summary>Tensile bar inside the effective area.</summary>
        public const string Included = "Included";
        /// <summary>Tensile bar outside the effective area.</summary>
        public const string Excluded = "Excluded";
        /// <summary>No bonded bar in Ac,eff: Øeq and σs of the tensile bars.</summary>
        public const string TensileBars = "TensileBars";
        /// <summary>Øeq and σs of the effective bars.</summary>
        public const string EffectiveBars = "EffectiveBars";
        /// <summary>Entry of the Eurocode-family width formula that repeats a datum (σs, c, Øeq, s).</summary>
        public const string Formula = "Formula";
        /// <summary>Upper bound without bonded bars in Ac,eff (EC2 7.3.4(3), eq. (7.14)).</summary>
        public const string UpperBound = "UpperBound";
        /// <summary>sr,max = k3 c + k1 k2 k4 Ø/ρ.</summary>
        public const string FormulaStandard = "FormulaStandard";
        /// <summary>sr,max of MC2010: 2 [c + Ø/(4 τbm/fctm ρ)].</summary>
        public const string FormulaModelCode2010 = "FormulaModelCode2010";
        /// <summary>sr,max of DIN: min[Ø/(3.6 ρ) or 1.3 (h − x); σs Ø/(3.6 fct)].</summary>
        public const string FormulaDin = "FormulaDin";
        /// <summary>sr,max = 1.3 (h − x), sparse bars (7.14).</summary>
        public const string FormulaSparseBars = "FormulaSparseBars";
        /// <summary>Upper bound, NTC: 1.7 · 0.75 (h − x).</summary>
        public const string UpperBoundNtc = "UpperBoundNtc";
        /// <summary>Upper bound, Eurocode: 1.3 (h − x).</summary>
        public const string UpperBoundEurocode = "UpperBoundEurocode";
        /// <summary>Upper bound, DIN: min[1.3 (h − x); σs Ø/(3.6 fct)].</summary>
        public const string UpperBoundDin = "UpperBoundDin";
        /// <summary>Cover assigned (<see cref="SectionCrackInput.CoverOverride"/>).</summary>
        public const string Assigned = "Assigned";
        /// <summary>Nominal cover plus link.</summary>
        public const string Nominal = "Nominal";
        /// <summary>Automatic maximum spacing of the effective bars.</summary>
        public const string Automatic = "Automatic";
        /// <summary>Assigned maximum spacing.</summary>
        public const string Manual = "Manual";
        /// <summary>Short-term loading.</summary>
        public const string ShortTerm = "ShortTerm";
        /// <summary>Long-term loading.</summary>
        public const string LongTerm = "LongTerm";
        /// <summary>Ribbed bars.</summary>
        public const string Ribbed = "Ribbed";
        /// <summary>Plain bars.</summary>
        public const string Plain = "Plain";
        /// <summary>εsm − εcm: the computed value governs.</summary>
        public const string ComputedGoverns = "ComputedGoverns";
        /// <summary>εsm − εcm: the minimum 0.6 σs/Es governs.</summary>
        public const string MinimumGoverns = "MinimumGoverns";
        /// <summary>s ≤ s_lim: Δsm,vicino.</summary>
        public const string CloseBars = "CloseBars";
        /// <summary>s &gt; s_lim: max(Δsm,vicino; Δsm,distante).</summary>
        public const string SparseBars = "SparseBars";
        /// <summary>Δsm adottata: the region near the bars governs.</summary>
        public const string NearGoverns = "NearGoverns";
        /// <summary>Δsm adottata: the region far from the bars governs.</summary>
        public const string FarGoverns = "FarGoverns";
        /// <summary>wk of the DS coarse system: 0.5 of eq. (7.8) with the whole tensile section.</summary>
        public const string DsCoarseHalf = "DsCoarseHalf";
        /// <summary>Envelope of the outer and inner surfaces.</summary>
        public const string Envelope = "Envelope";
        /// <summary>Inner band in uniform tension (negligible gradient).</summary>
        public const string Uniform = "Uniform";
        /// <summary>h − x of an inner band bounded by the height of the section along the gradient (neutral axis outside the section).</summary>
        public const string BoundedByHeight = "BoundedByHeight";
        /// <summary>k₂ of an inner band from the strains of its tensile part.</summary>
        public const string Local = "Local";
    }

    /// <summary>Names of the arguments of the trace entries (<see cref="CrackTraceEntry.Arguments"/>).</summary>
    public static class CrackTraceArguments
    {
        /// <summary>Index of a bar in <see cref="CrackSectionGeometry.Bars"/> (0-based; ANTHEA "B01" = 0); repeated for the bars of a region.</summary>
        public const string Bar = "Bar";
        /// <summary>Angle of a radial band, degrees (unrounded: the key rounds it to 0.##).</summary>
        public const string Angle = "Angle";
        /// <summary>σs of the NTC formula, MPa.</summary>
        public const string SteelStress = "SteelStress";
        /// <summary>Δσ tension stiffening, MPa.</summary>
        public const string TensionStiffening = "TensionStiffening";
        /// <summary>Es, MPa.</summary>
        public const string Es = "Es";
        /// <summary>Δsm adottata, mm.</summary>
        public const string AdoptedSpacing = "AdoptedSpacing";
        /// <summary>εsm − εcm.</summary>
        public const string MeanStrainDifference = "MeanStrainDifference";
        /// <summary>|∇ε| · h along the gradient (uniform tension of an inner band).</summary>
        public const string GradientTimesHeight = "GradientTimesHeight";
        /// <summary><see cref="SectionCrackCheck.UniformTensionTolerance"/>.</summary>
        public const string Tolerance = "Tolerance";
        /// <summary>εmax/|∇ε| of an inner band, mm.</summary>
        public const string FromNeutralAxis = "FromNeutralAxis";
    }

    /// <summary>Collects the trace and the region outcomes of one evaluation; null when <see cref="SectionCrackOptions.Trace"/> is off.</summary>
    internal sealed class CrackTraceBuilder
    {
        internal const string Dimensionless = "−";
        private readonly List<CrackTraceEntry> entries = new List<CrackTraceEntry>();
        private readonly List<CrackRegionOutcome> outcomes = new List<CrackRegionOutcome>();

        internal int Count => entries.Count;
        internal IReadOnlyList<CrackTraceEntry> Entries => entries.ToArray();
        internal IReadOnlyList<CrackRegionOutcome> Outcomes => outcomes.ToArray();

        internal static KeyValuePair<string, double> Arg(string name, double value) => new KeyValuePair<string, double>(name, value);

        internal void Add(string code, double? value, string unit) => entries.Add(new CrackTraceEntry(code, null, value, unit, null, null));

        internal void Add(string code, double? value, string unit, string region, params string[] flags)
            => entries.Add(new CrackTraceEntry(code, region, value, unit, null, flags.Where(f => f != null)));

        internal void AddWith(string code, double? value, string unit, string region, string[] flags, params KeyValuePair<string, double>[] arguments)
            => entries.Add(new CrackTraceEntry(code, region, value, unit, arguments, (flags ?? new string[0]).Where(f => f != null)));

        /// <summary>Removes the entries with the code (ConcreteTensionCracking.cs:16 removes the "Criterio k₂" of the bars).</summary>
        internal void RemoveAll(string code) => entries.RemoveAll(e => e.Code == code);

        /// <summary>Repeats the entries [start; end) with the flag (the governing region in the summary).</summary>
        internal void Repeat(int start, int end, string flag)
        {
            for (int i = start; i < end; i++) entries.Add(entries[i].WithFlag(flag));
        }

        internal void Outcome(string key, CrackOutcome outcome, double? width) => outcomes.Add(new CrackRegionOutcome(key, outcome, width));
    }
}
