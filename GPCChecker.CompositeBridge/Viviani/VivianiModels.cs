namespace GPC.Checkers.CompositeBridge.Viviani;

/// <summary>
/// Actions of one stage in the units of the program: axial force N [kN] (tension positive), shear T [kN], bending moment M [kNm]
/// (positive compresses the slab)
/// </summary>
public sealed record VivianiActions(double N = 0, double T = 0, double M = 0);

/// <summary>
/// Data of the method of prof. Viviani (program DT-NTC2008 "SezioneComposta", decompiled and rewritten without changing the calculation). Units
/// of the program: dimensions in cm, areas in cm², actions in kN and kNm, stresses in MPa. The defaults are the ones of the program
/// </summary>
public sealed record VivianiInput
{
    /// <summary>H: height of the slab (0: steel section only)</summary>
    public double SlabHeight { get; init; }
    /// <summary>B: width of the slab (the effective width, the method does not reduce it)</summary>
    public double SlabWidth { get; init; }
    /// <summary>R: cover, distance of the reinforcement from the top of the slab</summary>
    public double RebarCover { get; init; }
    /// <summary>Af: area of the reinforcement of the slab (one layer)</summary>
    public double RebarArea { get; init; }
    /// <summary>Ts: thickness of the top flange</summary>
    public double TopFlangeThickness { get; init; }
    /// <summary>Bs: width of the top flange</summary>
    public double TopFlangeWidth { get; init; }
    /// <summary>Hw: height of the web (between the flanges)</summary>
    public double WebHeight { get; init; }
    /// <summary>Tw: thickness of the web</summary>
    public double WebThickness { get; init; }
    /// <summary>Ti: thickness of the bottom flange</summary>
    public double BottomFlangeThickness { get; init; }
    /// <summary>Bi: width of the bottom flange</summary>
    public double BottomFlangeWidth { get; init; }
    /// <summary>n0: modular ratio of the short term actions</summary>
    public double ShortTermModularRatio { get; init; } = 6.0;
    /// <summary>ninf: modular ratio of the long term actions</summary>
    public double LongTermModularRatio { get; init; } = 18.0;
    /// <summary>fyd of the reinforcement</summary>
    public double RebarFyd { get; init; } = 391.3;
    /// <summary>fcd of the concrete (used only by the plastic analysis)</summary>
    public double Fcd { get; init; } = 16.46;
    /// <summary>fyd of the top flange</summary>
    public double TopFlangeFyd { get; init; } = 338.1;
    /// <summary>fyd of the web</summary>
    public double WebFyd { get; init; } = 338.1;
    /// <summary>fyd of the bottom flange</summary>
    public double BottomFlangeFyd { get; init; } = 338.1;
    /// <summary>Actions on the steel section (n = ∞, e.g. G1)</summary>
    public VivianiActions SteelOnly { get; init; } = new();
    /// <summary>Long term actions on the composite section (n = ninf, e.g. G2)</summary>
    public VivianiActions LongTerm { get; init; } = new();
    /// <summary>Short term actions on the composite section (n = n0, e.g. Q)</summary>
    public VivianiActions ShortTerm { get; init; } = new();
    /// <summary>γ of the actions on the steel section</summary>
    public double GammaG1 { get; init; } = 1.0;
    /// <summary>γ of the long term actions</summary>
    public double GammaG2 { get; init; } = 1.0;
    /// <summary>γ of the short term actions</summary>
    public double GammaQ { get; init; } = 1.0;
    /// <summary>"Elast.": the elastic stresses with the γ of the actions (false: characteristic actions). The plastic check always uses the γ</summary>
    public bool FactoredElasticStresses { get; init; }
    /// <summary>N. iter.: iterations of the effective web (0: no classification)</summary>
    public int Iterations { get; init; } = 4;
}

/// <summary>Colour of a field of the program: it is part of the output (e.g. green class, red utilization)</summary>
public enum VivianiColor { Black, Green, Red, Blue, Aquamarine }

/// <summary>Area [cm²], centroid from the bottom of the steel [cm] and moment of inertia [cm⁴] of a section</summary>
public sealed record VivianiSectionProperties(double Area, double Centroid, double Inertia);

/// <summary>
/// Results of the method of prof. Viviani, as shown by the program (at full precision: the program rounds them only for the display). Points of the
/// stresses: 1 top of the slab (concrete), 2 reinforcement, 3 top of the steel, 4 top of the web, 5 bottom of the web, 6 bottom of the steel
/// </summary>
public sealed record VivianiResult
{
    /// <summary>Steel section, with the effective web of the last iteration (n = ∞)</summary>
    public VivianiSectionProperties SteelSection { get; init; } = null!;
    /// <summary>Section of the long term actions (the cracked one if <see cref="CrackedSlab"/>)</summary>
    public VivianiSectionProperties LongTermSection { get; init; } = null!;
    /// <summary>Section of the short term actions (the cracked one if <see cref="CrackedSlab"/>)</summary>
    public VivianiSectionProperties ShortTermSection { get; init; } = null!;
    /// <summary>The top of the slab is in tension under the long and short term actions (or there is no slab): steel and reinforcement only</summary>
    public bool CrackedSlab { get; init; }
    /// <summary>σ1: concrete at the top of the slab (0 if cracked)</summary>
    public double Sigma1 { get; init; }
    /// <summary>σ2: reinforcement</summary>
    public double Sigma2 { get; init; }
    /// <summary>σ3: top of the steel</summary>
    public double Sigma3 { get; init; }
    /// <summary>σ4: top of the web</summary>
    public double Sigma4 { get; init; }
    /// <summary>σ5: bottom of the web</summary>
    public double Sigma5 { get; init; }
    /// <summary>σ6: bottom of the steel</summary>
    public double Sigma6 { get; init; }
    /// <summary>|σ3|</summary>
    public double SigmaId3 { get; init; }
    /// <summary>sqrt(σ4² + 3 τ²)</summary>
    public double SigmaId4 { get; init; }
    /// <summary>sqrt(σ5² + 3 τ²)</summary>
    public double SigmaId5 { get; init; }
    /// <summary>|σ6|</summary>
    public double SigmaId6 { get; init; }
    /// <summary>τ in the web: |T| / ((be1 + be2) tw)</summary>
    public double Tau { get; init; }
    /// <summary>b: shear flow on the connection [kN/cm] (long and short term actions)</summary>
    public double ShearFlow { get; init; }
    /// <summary>M Ed: γG1 M1 + γG2 M2 + γQ MQ [kNm]</summary>
    public double MEd { get; init; }
    /// <summary>T Ed [kN]</summary>
    public double TEd { get; init; }
    /// <summary>M Rd: plastic moment [kNm] (0: not calculated)</summary>
    public double MRd { get; init; }
    /// <summary>η = M Ed / M Rd (1 when M Rd is not calculated)</summary>
    public double Utilization { get; init; }
    /// <summary>Yel: elastic neutral axis from the bottom of the steel, when it cuts the web (otherwise 0)</summary>
    public double ElasticNeutralAxis { get; init; }
    /// <summary>Ypl: plastic neutral axis from the bottom of the steel</summary>
    public double PlasticNeutralAxis { get; init; }
    /// <summary>c/t of the web: Hw / Tw</summary>
    public double WebSlenderness { get; init; }
    /// <summary>c/t limit of class 3 of the web (elastic analysis)</summary>
    public double ElasticLimit { get; init; }
    /// <summary>c/t limit of the web of the plastic analysis (1000: web not compressed)</summary>
    public double PlasticLimit { get; init; }
    /// <summary>Class of the elastic analysis: 3, 4 (effective web) or 0 (not classified)</summary>
    public int ElasticClass { get; init; }
    /// <summary>Class of the plastic analysis: 1, 2, 0 (class 3 or 4 of the web: M Rd not calculated), -1 (compressed web too slender)</summary>
    public int PlasticClass { get; init; }
    /// <summary>be1: effective part of the web from the top</summary>
    public double EffectiveWebTop { get; init; }
    /// <summary>be2: effective part of the web from the bottom</summary>
    public double EffectiveWebBottom { get; init; }
    /// <summary>Iterations of the effective web performed</summary>
    public int IterationsPerformed { get; init; }
    /// <summary>fyd of the web of the plastic analysis (reduced by the shear if T Ed &gt; 0.5 Vpl)</summary>
    public double WebFydForBending { get; init; }
    /// <summary>T Ed exceeds the plastic shear resistance of the web: the program shows Tw = 0 in red and does not calculate M Rd</summary>
    public bool ShearExceedsWebResistance { get; init; }
    /// <summary>Tw shown by the program at the end (0 if <see cref="ShearExceedsWebResistance"/>)</summary>
    public double ShownWebThickness { get; init; }
    /// <summary>Colour of Tw</summary>
    public VivianiColor WebThicknessColor { get; init; }
    /// <summary>Colour of η: green verified, red not verified, black not calculated</summary>
    public VivianiColor UtilizationColor { get; init; }
    /// <summary>Colour of Af</summary>
    public VivianiColor RebarAreaColor { get; init; }
    /// <summary>Colour of the fyd of the web: red if reduced by the shear</summary>
    public VivianiColor WebFydColor { get; init; }
    /// <summary>Colour of the elastic class: green class 3, blue class 4 with the top of the web compressed, aquamarine class 4 with the bottom compressed</summary>
    public VivianiColor ElasticClassColor { get; init; }
    /// <summary>Colour of the plastic class: green class 1 or 2, red otherwise</summary>
    public VivianiColor PlasticClassColor { get; init; }
}
