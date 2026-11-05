using System.Runtime.Serialization;
using GPC.Checkers.CompositeBridge.Layout;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// The local state of the composite section of a beam in the actions of one behaviour (BridGHe "section state": the moment sign of a
/// reference case decides whether the slab is cracked), before the verification recipe exists: an application lifts it into the section
/// states and the concrete-excluded beams of <see cref="BridgeWorkflowActionDefinition"/>. Immutable. Lane L12: the
/// GPC.Model.Structure.Bridges.BridgeSectionStateAttribute of lane L11, outside the model, with <see cref="BridgeActionBehavior"/> instead
/// of the copy of it that GPCModel had.
/// </summary>
[DataContract]
public sealed class BridgeLocalSectionState
{
    /// <summary>The behaviour of the actions the state applies to: long term, short term or shrinkage (never steel only).</summary>
    [field:DataMember(Order=0)] public BridgeActionBehavior Behavior { get; private set; }
    /// <summary>True when the concrete of the slab is excluded (cracked section).</summary>
    [field:DataMember(Order=1)] public bool ConcreteExcluded { get; private set; }
    /// <summary>The modular ratio of the state (dimensionless, finite, greater than 0).</summary>
    [field:DataMember(Order=2)] public double ModularRatio { get; private set; }
    /// <summary>The name of the load case whose results classified the state; null when none.</summary>
    [field:DataMember(Order=3)] public string? ClassificationCase { get; private set; }
    /// <summary>The fingerprint of the beam property the state was computed for (<see cref="BridgeWorkflowDefinition.PropertyFingerprint"/>,
    /// not empty), so a later edit of the property is detected as a stale state.</summary>
    [field:DataMember(Order=4)] public string PropertyFingerprint { get; private set; } = "";

    public BridgeLocalSectionState(BridgeActionBehavior behavior, bool concreteExcluded, double modularRatio, string? classificationCase, string propertyFingerprint)
    {
        Behavior = behavior; ConcreteExcluded = concreteExcluded; ModularRatio = modularRatio;
        ClassificationCase = classificationCase; PropertyFingerprint = propertyFingerprint;
        string? error = Error();
        if (error != null) throw new ArgumentException(error);
    }

    [OnDeserialized] private void OnRead(StreamingContext context) { string? error = Error(); if (error != null) throw new SerializationException(error); }

    private string? Error()
    {
        if (!Enum.IsDefined(typeof(BridgeActionBehavior), Behavior) || Behavior == BridgeActionBehavior.Steel) return "InvalidBridgeSectionBehavior";
        if (double.IsNaN(ModularRatio) || double.IsInfinity(ModularRatio) || ModularRatio <= 0) return "InvalidBridgeSectionModularRatio";
        if (ClassificationCase != null && string.IsNullOrWhiteSpace(ClassificationCase)) return "InvalidBridgeSectionClassificationCase";
        if (string.IsNullOrWhiteSpace(PropertyFingerprint)) return "MissingBridgeSectionPropertyFingerprint";
        return null;
    }
}

/// <summary>The composite sections of a girder beam (BridGHe "Composite Beam": start and end property): <see cref="End"/> is
/// <see cref="Start"/> itself for a constant section. Immutable record; the definitions are held, not copied (the rule of the definitions:
/// whoever wants another one creates it).</summary>
[DataContract]
public sealed class BridgeBeamCompositeSection
{
    /// <summary>The section at the start of the beam (not null).</summary>
    [field:DataMember(Order=0)] public BridgeCompositeSectionDefinition Start { get; private set; } = null!;
    /// <summary>The section at the end of the beam (not null; the start one for a constant section).</summary>
    [field:DataMember(Order=1)] public BridgeCompositeSectionDefinition End { get; private set; } = null!;
    /// <summary>True when the end section is the start one.</summary>
    public bool IsConstant => ReferenceEquals(Start, End);

    /// <param name="end">Null: a constant section (<see cref="End"/> = <paramref name="start"/>).</param>
    public BridgeBeamCompositeSection(BridgeCompositeSectionDefinition start, BridgeCompositeSectionDefinition? end = null)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end ?? start;
        Start.Validate(); if (!IsConstant) End.Validate();
    }

    [OnDeserialized] private void OnRead(StreamingContext context) { if (Start == null || End == null) throw new SerializationException("MissingBridgeBeamCompositeSection"); }
}

/// <summary>
/// The bridge data of one beam, by the GUID of the beam: its physical bridge member (<see cref="BridgeMemberMetadata"/>), its super
/// element (the beams Super Element Check checks together), its local section state and its composite sections. Each is optional; an
/// empty record is no data. Immutable: the With methods give a new record. Lane L12: the bridge attributes of lane L11, outside the model.
/// </summary>
[DataContract]
public sealed class BridgeBeamData
{
    /// <summary>The GUID of the beam (<see cref="Element.Guid"/>, not empty).</summary>
    [field:DataMember(Order=0)] public Guid BeamGuid { get; private set; }
    /// <summary>The physical bridge member of the beam, or null.</summary>
    [field:DataMember(Order=1)] public BridgeMemberMetadata? Member { get; private set; }
    /// <summary>The name of the super element of the beam (not empty, without leading or trailing spaces), or null.</summary>
    [field:DataMember(Order=2)] public string? SuperElement { get; private set; }
    /// <summary>The local section state of the beam, or null.</summary>
    [field:DataMember(Order=3)] public BridgeLocalSectionState? SectionState { get; private set; }
    /// <summary>The composite sections of the beam, or null.</summary>
    [field:DataMember(Order=4)] public BridgeBeamCompositeSection? CompositeSection { get; private set; }

    public BridgeBeamData(Guid beamGuid, BridgeMemberMetadata? member = null, string? superElement = null, BridgeLocalSectionState? sectionState = null,
        BridgeBeamCompositeSection? compositeSection = null)
    {
        BeamGuid = beamGuid; Member = member; SuperElement = superElement; SectionState = sectionState; CompositeSection = compositeSection;
        string? error = Error();
        if (error != null) throw new ArgumentException(error);
    }

    /// <summary>True when the record carries no data.</summary>
    public bool IsEmpty => Member == null && SuperElement == null && SectionState == null && CompositeSection == null;

    public BridgeBeamData WithMember(BridgeMemberMetadata? member) => new(BeamGuid, member, SuperElement, SectionState, CompositeSection);
    public BridgeBeamData WithSuperElement(string? superElement) => new(BeamGuid, Member, superElement, SectionState, CompositeSection);
    public BridgeBeamData WithSectionState(BridgeLocalSectionState? sectionState) => new(BeamGuid, Member, SuperElement, sectionState, CompositeSection);
    public BridgeBeamData WithCompositeSection(BridgeBeamCompositeSection? compositeSection) => new(BeamGuid, Member, SuperElement, SectionState, compositeSection);

    /// <summary>A valid super element name: not empty, without leading or trailing spaces.</summary>
    public static bool ValidSuperElementName(string? name) => !string.IsNullOrWhiteSpace(name) && name!.Trim() == name;

    [OnDeserialized] private void OnRead(StreamingContext context)
    {
        string? error = Error() ?? (IsEmpty ? "EmptyBridgeBeamData" : null);
        if (error != null) throw new SerializationException(error);
    }

    private string? Error()
    {
        if (BeamGuid == Guid.Empty) return "EmptyBridgeBeamGuid";
        if (SuperElement != null && !ValidSuperElementName(SuperElement)) return "InvalidSuperElementName";
        return null;
    }
}

/// <summary>
/// The bridge data of a GPC model (lane L12, user decision of 5/11: composition, the bridge vocabulary outside GPCModel): per beam, by the
/// GUID of the element, a <see cref="BridgeBeamData"/>; and the verification recipe of the model (<see cref="Workflow"/>, D3). The model
/// does not know them, so the Straus7 writer and the analysis fingerprint never see them. <see cref="BridgeModel"/> pairs them with their
/// model; <see cref="Validate"/> checks them against a model with the rules of lane L11.
/// <para>Persistence: <see cref="Save"/>/<see cref="Read"/> (XML of the data contracts, as <see cref="BridgeWorkflowDefinition"/>), or
/// with the model in one package (<see cref="BridgeModel.Save(Stream)"/>).</para>
/// </summary>
[DataContract]
public sealed class BridgeModelData
{
    [DataMember(Order=0,IsRequired=true)] private int schema=1;
    /// <summary>The records in GUID order: written only in the snapshots that are saved or fingerprinted (<see cref="Snapshot"/>), read
    /// into <see cref="index"/>.</summary>
    [DataMember(Order=1)] private BridgeBeamData[] beams = Array.Empty<BridgeBeamData>();
    /// <summary>The verification recipe of the model (D3), or null.</summary>
    [field:DataMember(Order=2,EmitDefaultValue=false)] public BridgeWorkflowDefinition? Workflow { get; set; }
    [NonSerialized] private SortedDictionary<Guid, BridgeBeamData> index = new();

    public BridgeModelData() { }

    /// <summary>The number of beams with data.</summary>
    public int Count => index.Count;
    /// <summary>True when there is no beam data and no recipe.</summary>
    public bool IsEmpty => index.Count == 0 && Workflow == null;
    /// <summary>The records, in GUID order.</summary>
    public IEnumerable<BridgeBeamData> Beams => index.Values;

    /// <summary>The data of a beam, or null.</summary>
    public BridgeBeamData? Beam(Guid beam) => index.TryGetValue(beam, out var data) ? data : null;
    public BridgeMemberMetadata? Member(Guid beam) => Beam(beam)?.Member;
    public string? SuperElement(Guid beam) => Beam(beam)?.SuperElement;
    public BridgeLocalSectionState? SectionState(Guid beam) => Beam(beam)?.SectionState;
    public BridgeBeamCompositeSection? CompositeSection(Guid beam) => Beam(beam)?.CompositeSection;

    /// <summary>Replaces the data of the beam of <paramref name="data"/>; an empty record removes them.</summary>
    public void Set(BridgeBeamData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.IsEmpty) index.Remove(data.BeamGuid); else index[data.BeamGuid] = data;
    }
    /// <summary>Sets (or, with null, removes) one datum of a beam, keeping the others.</summary>
    public void SetMember(Guid beam, BridgeMemberMetadata? member) => Set((Beam(beam) ?? new BridgeBeamData(beam)).WithMember(member));
    public void SetSuperElement(Guid beam, string? superElement) => Set((Beam(beam) ?? new BridgeBeamData(beam)).WithSuperElement(superElement));
    public void SetSectionState(Guid beam, BridgeLocalSectionState? state) => Set((Beam(beam) ?? new BridgeBeamData(beam)).WithSectionState(state));
    public void SetCompositeSection(Guid beam, BridgeBeamCompositeSection? sections) => Set((Beam(beam) ?? new BridgeBeamData(beam)).WithCompositeSection(sections));
    /// <summary>Removes all the data of a beam.</summary>
    public bool Remove(Guid beam) => index.Remove(beam);

    /// <summary>
    /// The diagnostics of the data against <paramref name="model"/>, with the rules of the domain checks of lane L11 (no exception; see
    /// <see cref="BridgeModel.RequireValid"/>): a key that is no element of the model (UnknownBridgeElement), data on an element that is not a
    /// beam (BridgeDataOnWrongElement), a recipe that does not hold for the model (InvalidBridgeWorkflow: another model or revision, unknown
    /// beams or cases, stale states); a warning for a section state computed for another property of its beam (StaleBridgeSectionState).
    /// One record per beam and one datum per kind: the duplicates of lane L11 cannot exist (they are refused when read).
    /// </summary>
    public IReadOnlyList<ModelDiagnostic> Validate(GPC.Model.Models.Model model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        ValidateShape();
        var elements = new Dictionary<Guid, Element>();
        foreach (var element in model.AllElements) elements[element.Guid] = element;
        var result = new List<ModelDiagnostic>();
        foreach (var data in index.Values)
        {
            if (!elements.TryGetValue(data.BeamGuid, out var element))
            { result.Add(ModelDiagnostic.Error("UnknownBridgeElement", null, "UnknownBridgeElement: " + data.BeamGuid.ToString("D") + " is not an element of the model")); continue; }
            if (element is not BeamElement beam)
            {
                result.Add(ModelDiagnostic.Error("BridgeDataOnWrongElement", element,
                    "BridgeDataOnWrongElement: bridge member, super element, section state and composite section data belong to beams"));
                continue;
            }
            if (data.SectionState != null && data.SectionState.PropertyFingerprint != BridgeWorkflowDefinition.PropertyFingerprint(beam))
            {
                var stale = ModelDiagnostic.Error("StaleBridgeSectionState", beam, "StaleBridgeSectionState: the section state was computed for another property of the beam; reapply it");
                stale.Severity = DiagnosticSeverity.Warning; result.Add(stale);
            }
        }
        if (Workflow != null)
            try { Workflow.Validate(model); }
            catch (Exception ex) when (ex is ArgumentException || ex is SerializationException || ex is InvalidOperationException || ex is NotSupportedException)
            { result.Add(ModelDiagnostic.Error("InvalidBridgeWorkflow", null, "InvalidBridgeWorkflow: " + ex.Message)); }
        return result.AsReadOnly();
    }

    /// <summary>Checks the form of the data (not the model): schema, records, recipe.</summary>
    public void ValidateShape()
    {
        if (schema != 1) throw new SerializationException("UnsupportedBridgeModelDataSchema");
        Workflow?.ValidateShapeOnly();
    }

    /// <summary>The revision fingerprint of the data (records in GUID order, recipe).</summary>
    public string Fingerprint() { ValidateShape(); return ModelValues.Fingerprint(new object[] { Snapshot() }); }

    /// <summary>The XML of the data (data contracts; sections and materials with the vocabulary of GPCModel, shared references kept).</summary>
    public byte[] Save() { ValidateShape(); return LayoutContracts.Save(Snapshot()); }

    /// <summary>Reads <see cref="Save"/> (at most 256 MiB): the form of the data is checked (a record twice or for an empty GUID, an empty
    /// record, invalid values), not the model they belong to (<see cref="Validate"/>).</summary>
    public static BridgeModelData Read(byte[] bytes)
    {
        var data = LayoutContracts.Read<BridgeModelData>(bytes, "MissingBridgeModelData");
        data.ValidateShape();
        // The composite sections once the whole graph is read (their sections are complete only then).
        foreach (var record in data.index.Values)
            if (record.CompositeSection != null)
                try { record.CompositeSection.Start.Validate(); record.CompositeSection.End.Validate(); }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                { throw new SerializationException("InvalidBridgeCompositeSection: " + record.BeamGuid.ToString("D") + ": " + ex.Message, ex); }
        return data;
    }

    /// <summary>A complete, independent copy (<see cref="Copy(ModelCopyOptions)"/> with <see cref="ModelCopyOptions.None"/>).</summary>
    public BridgeModelData Copy() => Copy(ModelCopyOptions.None);

    /// <summary>
    /// A copy for a copy of the model (<see cref="BridgeModel.Copy(ModelCopyOptions)"/>): the immutable records (member metadata, section
    /// states, names) are shared; the recipe is copied; the composite section definitions are shared with
    /// <see cref="ModelCopyOptions.ShareProperties"/> (they are definitions, as the properties), otherwise copied together, so the definitions
    /// and sections that several beams share stay shared in the copy.
    /// </summary>
    public BridgeModelData Copy(ModelCopyOptions options)
    {
        options ??= ModelCopyOptions.None;
        var copy = new BridgeModelData { Workflow = Workflow?.Detach() };
        Func<BridgeCompositeSectionDefinition, BridgeCompositeSectionDefinition> map = d => d;
        if (!options.SharesProperties)
        {
            var distinct = new List<BridgeCompositeSectionDefinition>(); var seen = new Dictionary<BridgeCompositeSectionDefinition, int>(Identity.Instance);
            foreach (var data in index.Values)
                if (data.CompositeSection != null)
                    foreach (var d in new[] { data.CompositeSection.Start, data.CompositeSection.End })
                        if (!seen.ContainsKey(d)) { seen.Add(d, distinct.Count); distinct.Add(d); }
            if (distinct.Count != 0)
            {
                var copied = LayoutContracts.Copy(distinct.ToArray());
                map = d => copied[seen[d]];
            }
        }
        foreach (var data in index.Values)
        {
            var sections = data.CompositeSection;
            if (sections != null && !options.SharesProperties)
                sections = new BridgeBeamCompositeSection(map(sections.Start), sections.IsConstant ? null : map(sections.End));
            copy.index.Add(data.BeamGuid, ReferenceEquals(sections, data.CompositeSection) ? data : data.WithCompositeSection(sections));
        }
        return copy;
    }

    /// <summary>The records as they are saved and fingerprinted: in GUID order, in a separate instance (this one is not changed).</summary>
    private BridgeModelData Snapshot() => new() { schema = schema, beams = index.Values.ToArray(), Workflow = Workflow };

    [OnDeserialized] private void OnRead(StreamingContext context)
    {
        index = new SortedDictionary<Guid, BridgeBeamData>();
        foreach (var data in beams ?? Array.Empty<BridgeBeamData>())
        {
            if (data == null) throw new SerializationException("MissingBridgeBeamData");
            if (index.ContainsKey(data.BeamGuid)) throw new SerializationException("DuplicateBridgeBeamData: " + data.BeamGuid.ToString("D"));
            index.Add(data.BeamGuid, data);
        }
        beams = Array.Empty<BridgeBeamData>();
    }

    private sealed class Identity : IEqualityComparer<BridgeCompositeSectionDefinition>
    {
        internal static readonly Identity Instance = new();
        public bool Equals(BridgeCompositeSectionDefinition? x, BridgeCompositeSectionDefinition? y) => ReferenceEquals(x, y);
        public int GetHashCode(BridgeCompositeSectionDefinition obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
