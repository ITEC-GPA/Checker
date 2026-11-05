using System.Runtime.Serialization;
using System.Xml;
using GPC.Geometry;
using GPC.Model.Core;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// The composite section of a bridge girder as an application defines it (BridGHe "Composite Beam Property"), before any model or
/// analysis: the steel-concrete <see cref="ReinforcedConcreteSection"/> of GPCModel (one steel H, a rectangular slab, at most two rows of
/// bars of one diameter each: the sections the H bridge engine checks), the modular ratios of the long-term, short-term and shrinkage
/// actions, the vertical offset and the Consider Concrete / Consider Rebar choices. Units: mm, MPa; the modular ratios are
/// dimensionless. The section is held, not copied: the callers do not change a section they gave to a definition.
/// <see cref="SectionInput(bool, BridgeAnalysisOptions?)"/> gives the input of the H bridge engine for the section, so the checks need not
/// rebuild the section from its dimensions.
/// </summary>
[DataContract]
public sealed class BridgeCompositeSectionDefinition
{
    [DataMember(Order=0,IsRequired=true)] private int schema=1;
    /// <summary>The steel-concrete section (not null).</summary>
    [field:DataMember(Order=1)] public ReinforcedConcreteSection Section { get; private set; } = null!;
    /// <summary>The modular ratio n∞ of the long-term actions (finite, greater than 0).</summary>
    [field:DataMember(Order=2)] public double NLongTerm { get; private set; }
    /// <summary>The modular ratio n0 of the short-term actions (finite, greater than 0).</summary>
    [field:DataMember(Order=3)] public double NShortTerm { get; private set; }
    /// <summary>The modular ratio n,sh of shrinkage (finite, greater than 0). Required: an application that defaults it to n∞ resolves it
    /// before creating the definition.</summary>
    [field:DataMember(Order=4)] public double NShrinkage { get; private set; }
    /// <summary>The vertical offset of the section [mm] (finite), as the application gave it.</summary>
    [field:DataMember(Order=5)] public double VerticalOffsetMm { get; private set; }
    /// <summary>True when the concrete of the slab is considered (BridGHe "Consider Concrete").</summary>
    [field:DataMember(Order=6)] public bool ConsiderConcrete { get; private set; }
    /// <summary>True when the reinforcing bars are considered (BridGHe "Consider Rebar").</summary>
    [field:DataMember(Order=7)] public bool ConsiderRebar { get; private set; }
    /// <summary>The name (not empty).</summary>
    [field:DataMember(Order=8)] public string Name { get; private set; } = "";
    /// <summary>The rows of bars as the application gave them (total area and cover of each row, bars per row of <see cref="Section"/>), from
    /// which <see cref="AnalysisSection"/> builds the section of the analysis properties; null for a definition created without them.</summary>
    [field:DataMember(Order=9,EmitDefaultValue=false)] public BridgeCompositeRebarRows? RebarRows { get; private set; }
    /// <summary>The steel of the analysis properties as the application gave it (BridGHe: the steel material of Composite Beam Property, with
    /// E, ν or G, density, thermal expansion, damping…): the material of the homogenised analysis properties and the steel of the shrinkage
    /// actions; null for a definition created without it. Its E is the E of the steel of <see cref="Section"/> (one input).</summary>
    [field:DataMember(Order=10,EmitDefaultValue=false)] public Material? AnalysisSteel { get; private set; }

    public BridgeCompositeSectionDefinition(string name, ReinforcedConcreteSection section, double nLongTerm, double nShortTerm, double nShrinkage,
        double verticalOffsetMm, bool considerConcrete, bool considerRebar)
    {
        Name = name; Section = section; NLongTerm = nLongTerm; NShortTerm = nShortTerm; NShrinkage = nShrinkage;
        VerticalOffsetMm = verticalOffsetMm; ConsiderConcrete = considerConcrete; ConsiderRebar = considerRebar;
        Validate();
    }

    /// <summary>A definition with the rows of bars from which the application built <paramref name="section"/>
    /// (<paramref name="rebarRows"/>: <see cref="BridgeCompositeRebarRows.BarsPerRow"/> bars in each row of non-zero area, the bars of a row of
    /// the total area of the row), so that <see cref="AnalysisSection"/> is available.</summary>
    public BridgeCompositeSectionDefinition(string name, ReinforcedConcreteSection section, BridgeCompositeRebarRows rebarRows, double nLongTerm, double nShortTerm,
        double nShrinkage, double verticalOffsetMm, bool considerConcrete, bool considerRebar)
    {
        Name = name; Section = section; NLongTerm = nLongTerm; NShortTerm = nShortTerm; NShrinkage = nShrinkage;
        VerticalOffsetMm = verticalOffsetMm; ConsiderConcrete = considerConcrete; ConsiderRebar = considerRebar;
        RebarRows = rebarRows ?? throw new ArgumentException("MissingBridgeCompositeRebarRows");
        Validate();
    }

    /// <summary>A definition with the rows of bars (as the constructor with <paramref name="rebarRows"/>) and the steel of the analysis
    /// properties <paramref name="analysisSteel"/> (not null, held: the callers do not change it), whose E must be that of the steel of
    /// <paramref name="section"/>.</summary>
    public BridgeCompositeSectionDefinition(string name, ReinforcedConcreteSection section, BridgeCompositeRebarRows rebarRows, Material analysisSteel,
        double nLongTerm, double nShortTerm, double nShrinkage, double verticalOffsetMm, bool considerConcrete, bool considerRebar)
    {
        Name = name; Section = section; NLongTerm = nLongTerm; NShortTerm = nShortTerm; NShrinkage = nShrinkage;
        VerticalOffsetMm = verticalOffsetMm; ConsiderConcrete = considerConcrete; ConsiderRebar = considerRebar;
        RebarRows = rebarRows ?? throw new ArgumentException("MissingBridgeCompositeRebarRows");
        AnalysisSteel = analysisSteel ?? throw new ArgumentException("MissingBridgeCompositeAnalysisSteel");
        Validate();
    }

    /// <summary>Checks the values and the shape of the section: <see cref="ArgumentException"/> for an invalid value, <see cref="NotSupportedException"/>
    /// for a section the H bridge engine does not check (the limits of the section input).</summary>
    public void Validate()
    {
        if(schema!=1) throw new SerializationException("UnsupportedBridgeCompositeSectionSchema");
        if(string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("InvalidBridgeCompositeSectionName");
        if(Section==null) throw new ArgumentException("MissingBridgeCompositeSection");
        foreach(var n in new[]{NLongTerm,NShortTerm,NShrinkage})
            if(double.IsNaN(n)||double.IsInfinity(n)||n<=0) throw new ArgumentException("InvalidBridgeModularRatio");
        if(double.IsNaN(VerticalOffsetMm)||double.IsInfinity(VerticalOffsetMm)) throw new ArgumentException("InvalidBridgeVerticalOffset");
        if(!(Section.SectionShape is SectionRectangular)) throw new NotSupportedException("BridgeCompositeSectionRequiresRectangularSlab");
        if(!(Section.ConcreteMaterial is ConcreteMaterialEN1992)) throw new NotSupportedException("BridgeCompositeSectionRequiresConcreteEN1992");
        if(Section.SteelSections.Count!=1 || !(Section.SteelSections[0].Section.SectionShape is SectionH)) throw new NotSupportedException("BridgeCompositeSectionRequiresOneSteelH");
        var positioned=Section.SteelSections[0];
        if(Math.Abs(positioned.Rotation)>1e-10 || Math.Abs(Section.Jxy)>1e-10*(Section.Jxx+Section.Jyy)) throw new NotSupportedException("BridgeCompositeSectionRotatedOrUnsymmetric");
        var rows=Rows();
        if(rows.Length>2) throw new NotSupportedException("BridgeCompositeSectionWithMoreThanTwoRebarRows");
        foreach(var row in rows)
            if(row.Any(b=>Math.Abs(b.RebarSection.Diameter-row[0].RebarSection.Diameter)>1e-8)) throw new NotSupportedException("BridgeCompositeSectionRowWithDifferentDiameters");
        if(RebarRows!=null)
        {
            RebarRows.Validate();
            // The rows given describe the bars of the section: BarsPerRow bars in each row of non-zero area, the total area of the rows.
            double given=RebarRows.TopAreaMm2+RebarRows.BottomAreaMm2, bars=Section.AreaRebars;
            int count=RebarRows.BarsPerRow*((RebarRows.TopAreaMm2>0 ? 1 : 0)+(RebarRows.BottomAreaMm2>0 ? 1 : 0));
            if(Section.Rebars.Count()!=count || Math.Abs(bars-given)>1e-9*Math.Max(1,given)) throw new ArgumentException("BridgeCompositeRebarRowsDoNotMatchSection");
        }
        if(AnalysisSteel!=null)
        {
            // One input: the steel of the section (E, fy, fu for the checks) and the steel of the analysis come from the same material.
            double analysis=AnalysisSteel.ElasticModulusTension, steel=positioned.Section.SteelMaterial.ElasticModulusTension;
            if(!(analysis==steel || Math.Abs(analysis-steel)<=1e-12*Math.Max(Math.Abs(analysis),Math.Abs(steel))))
                throw new ArgumentException(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "BridgeCompositeAnalysisSteelModulusMismatch: the elastic modulus of the analysis steel '{0}' ({1:R} MPa) differs from that of the steel of the section '{2}' ({3:R} MPa); both come from the steel material of the composite section",
                    AnalysisSteel.Name, analysis, positioned.Section.SteelMaterial.Name, steel));
        }
    }

    /// <summary>The rows of bars from the top, as the H bridge engine reads them.</summary>
    private ReinforcedConcreteRebar[][] Rows() => Section.Rebars.GroupBy(b => Math.Round(b.Position.Y, 8)).OrderByDescending(g => g.Key).Select(g => g.ToArray()).ToArray();

    /// <summary>The input of the H bridge engine for the section, without phases, with the default options of the checks
    /// (<see cref="BridgeStandard.Eurocode4"/>, <see cref="BridgeLimitState.Rare"/>, the other options at their defaults).</summary>
    public HBridgeInput SectionInput(bool rebars) => SectionInput(rebars, null);

    /// <summary>
    /// The input of the H bridge engine for the section, without phases (the workflow executor or the caller adds them): the input the
    /// adapter of BridGHe (BridgeCheckAdapter.SectionInput) gives for the same section with the reference system of the Super Element Check
    /// (origin at the centroid of the section, axes -X and -Y) and the same options, bit for bit. <paramref name="rebars"/> false leaves the
    /// rows of bars out. The options are those given (null: the default options of <see cref="SectionInput(bool)"/>) with the common load
    /// elevation of that reference system. Materials: the instances of the section.
    /// </summary>
    public HBridgeInput SectionInput(bool rebars, BridgeAnalysisOptions? options)
    {
        Validate();
        var section=Section;
        var positioned=section.SteelSections[0]; var h=(SectionH)positioned.Section.SectionShape;
        var rows=Rows();
        BridgeRebarRow Row(int index)
        {
            if(!rebars || index>=rows.Length) return new BridgeRebarRow();
            var bars=rows[index];
            return new BridgeRebarRow
            {
                Enabled=true,
                Diameter=bars[0].RebarSection.Diameter,
                Pitch=section.Width/bars.Length,
                AxisDistance=index==0 ? section.Height-bars[0].Position.Y : bars[0].Position.Y
            };
        }
        var steel=positioned.Section.SteelMaterial;
        var rebar=section.Rebars.FirstOrDefault()?.RebarSection.RebarMaterial ?? steel;
        var reference=new CoordinateSystem(section.Centroid,new Vector3d(-1,0,0),new Vector3d(0,-1,0));
        var used=options ?? new BridgeAnalysisOptions { Standard=BridgeStandard.Eurocode4, LimitState=BridgeLimitState.Rare };
        return new HBridgeInput
        {
            Materials=new BridgeMaterialSet((ConcreteMaterialEN1992)section.ConcreteMaterial,steel,rebar),
            Geometry=new HSectionDimensions
            {
                SlabWidth=section.Width,
                SlabHeight=section.Height,
                WebHeight=h.HeightWeb,
                WebThickness=h.ThicknessWeb,
                TopWidth=h.LenghtTopFlange,
                TopThickness=h.ThicknessTopFlange,
                BottomWidth=h.LenghtBottomFlange,
                BottomThickness=h.ThicknessBottomFlange
            },
            TopRebars=Row(0),
            BottomRebars=Row(1),
            Options=used with { CommonLoadY=reference.Origin.Y }
        };
    }

    /// <summary>
    /// The section of the homogenised analysis properties of the girder (BridGHe 0.3 and 0.4: the generic FEM properties of Composite Beam
    /// Property, Bridge State Override and the other composite components), which differs from <see cref="Section"/> (the section of the
    /// checks) in the bars: one bar per row of <see cref="RebarRows"/> with the whole area A of the row (diameter √(4A/π)), at the pitch of the
    /// slab width (one bar at the middle of the slab), at the cover given (0 for the bottom row: the top one), with the slab, the steel H,
    /// the materials and the name of <see cref="Section"/> and no horizontal eccentricity. It is the section of BridGHe
    /// CompositeBeamPropertyModel.GetReinforcedConcreteSection bit for bit. The homogenised values depend on the bars (I22 by a few per cent,
    /// A and I11 below 1e-4 against the bars of <see cref="Section"/>): the two layouts are kept as they are until they are unified.
    /// A new section at every call (the steel shape and the materials are the instances of <see cref="Section"/>); a definition without
    /// <see cref="RebarRows"/> gives <see cref="InvalidOperationException"/>.
    /// </summary>
    public ReinforcedConcreteSection AnalysisSection()
    {
        Validate();
        var rows=RebarRows ?? throw new InvalidOperationException("BridgeCompositeSectionWithoutRebarRows");
        var section=Section; var positioned=section.SteelSections[0];
        var rebar=section.Rebars.FirstOrDefault()?.RebarSection.RebarMaterial;
        RebarSectionCircular? Bar(double area)
        {
            double diameter=Math.Sqrt(area*4/Math.PI);
            return diameter!=0 ? new RebarSectionCircular("",diameter,rebar) : null;
        }
        double width=section.Width;
        return ReinforcedConcreteSection.CreateBridgeSection(width,section.Height,section.ConcreteMaterial,Bar(rows.TopAreaMm2),width/1.0,rows.TopCoverMm,
            Bar(rows.BottomAreaMm2),width/1.0,(GPC.Model.Sections.Section)positioned.Section.SectionShape,positioned.Section.SteelMaterial,rows.BottomCoverMm,0,section.Name);
    }

    /// <summary>The revision fingerprint of the definition (section, ratios, offset, choices, name).</summary>
    public string Fingerprint() { Validate(); return ModelValues.Fingerprint(new object[]{this}); }

    /// <summary>The XML of the definition (data contracts, the section with the closed vocabulary of GPCModel).</summary>
    public byte[] Save()
    {
        Validate(); using(var stream=new MemoryStream()) { Serializer().WriteObject(stream,this); return stream.ToArray(); }
    }
    /// <summary>A detached copy (through <see cref="Save"/>): the section is copied too.</summary>
    public BridgeCompositeSectionDefinition Copy() => Read(Save());
    /// <summary>Reads <see cref="Save"/> (at most 16 MiB) and validates the definition.</summary>
    public static BridgeCompositeSectionDefinition Read(byte[] bytes)
    {
        if(bytes==null) throw new ArgumentNullException(nameof(bytes));
        if(bytes.Length>16*1024*1024) throw new SerializationException("BridgeCompositeSectionTooLarge");
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))
        {
            var value=(BridgeCompositeSectionDefinition)(Serializer().ReadObject(reader) ?? throw new SerializationException("MissingBridgeCompositeSection"));
            value.Validate(); return value;
        }
    }
    private static DataContractSerializer Serializer() => new(typeof(BridgeCompositeSectionDefinition),new DataContractSerializerSettings
        { KnownTypes=ModelValues.DataContracts, PreserveObjectReferences=true, MaxItemsInObjectGraph=1000000 });
}

/// <summary>
/// The rows of bars of a <see cref="BridgeCompositeSectionDefinition"/> as the application gave them (BridGHe "Composite Beam Property"): the
/// total area of the top and bottom rows, their covers (to the centre of the bars, as ReinforcedConcreteSection.CreateBridgeSection reads
/// them; a bottom cover of 0 is the top one) and the number of bars per row of the section of the checks. Units: mm², mm. Immutable.
/// </summary>
[DataContract]
public sealed class BridgeCompositeRebarRows
{
    /// <summary>The total area of the top row [mm²] (finite, not negative; 0: no row).</summary>
    [field:DataMember(Order=0)] public double TopAreaMm2 { get; private set; }
    /// <summary>The cover of the top row, from the top face to the centre of the bars [mm] (finite).</summary>
    [field:DataMember(Order=1)] public double TopCoverMm { get; private set; }
    /// <summary>The total area of the bottom row [mm²] (finite, not negative; 0: no row).</summary>
    [field:DataMember(Order=2)] public double BottomAreaMm2 { get; private set; }
    /// <summary>The cover of the bottom row, from the bottom face to the centre of the bars [mm] (finite; 0: the top cover).</summary>
    [field:DataMember(Order=3)] public double BottomCoverMm { get; private set; }
    /// <summary>The number of bars in each row of the section of the checks (at least 1).</summary>
    [field:DataMember(Order=4)] public int BarsPerRow { get; private set; }

    public BridgeCompositeRebarRows(double topAreaMm2, double topCoverMm, double bottomAreaMm2, double bottomCoverMm, int barsPerRow)
    {
        TopAreaMm2 = topAreaMm2; TopCoverMm = topCoverMm; BottomAreaMm2 = bottomAreaMm2; BottomCoverMm = bottomCoverMm; BarsPerRow = barsPerRow;
        Validate();
    }

    /// <summary>Checks the values: <see cref="ArgumentException"/> "InvalidBridgeCompositeRebarRows" for an invalid value.</summary>
    public void Validate()
    {
        bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        if(!Finite(TopAreaMm2) || TopAreaMm2<0 || !Finite(BottomAreaMm2) || BottomAreaMm2<0 || !Finite(TopCoverMm) || !Finite(BottomCoverMm) || BarsPerRow<1)
            throw new ArgumentException("InvalidBridgeCompositeRebarRows");
    }
}
