using System.Runtime.Serialization;
using System.Xml;
using GPC.Model.Core;
using GPC.Model.Models;

namespace GPC.Checkers.CompositeBridge;

public enum BridgeWorkflowMethod { Compatible, LinearHistory }
public enum BridgeWorkflowSectionState { Steel, Composite, ConcreteExcluded }
public enum BridgeActionBehavior { Steel, LongTerm, ShortTerm, Shrinkage }

/// <summary>A verification recipe, separate from the physical model. It stores stable entity/case identities and
/// analysis provenance, never recursively embeds source models. Strains are dimensionless, contraction negative; times are days.</summary>
[DataContract]
public sealed class BridgeWorkflowDefinition
{
    [DataMember(Order=0,IsRequired=true)] private int schema=1;
    [field:DataMember(Order=1)] public Guid ModelGuid { get; set; }
    [field:DataMember(Order=2)] public string AnalysisFingerprint { get; set; } = "";
    [field:DataMember(Order=3)] public BridgeWorkflowMethod Method { get; set; }
    [field:DataMember(Order=4)] public BridgeWorkflowPhaseDefinition[] Phases { get; set; } = Array.Empty<BridgeWorkflowPhaseDefinition>();

    public void Validate(GPC.Model.Models.Model model)
    {
        if(model==null) throw new ArgumentNullException(nameof(model));
        ValidateShape();
        if(ModelGuid!=model.Guid || AnalysisFingerprint!=model.AnalysisFingerprint()) throw new ArgumentException("StaleBridgeWorkflowModel");
        // One lookup per model: the section states are checked in O(states), not O(beams × states).
        var byGuid=model.BeamElements.Values.ToDictionary(b=>b.Guid);
        var beams=new HashSet<Guid>(byGuid.Keys);
        var cases=new HashSet<Guid>(model.LoadCases.Values.Select(c=>c.Guid));
        var used=new Dictionary<Guid,HashSet<Guid>>();
        var sourceUsed=new Dictionary<(Guid Model,string Revision,Guid Case),HashSet<Guid>>();
        foreach(var p in Phases)
            foreach(var a in p.Actions)
            {
                if(a.BeamGuids.Any(id=>!beams.Contains(id)) || a.ConcreteExcludedBeamGuids.Any(id=>!beams.Contains(id))) throw new ArgumentException("UnknownBridgeWorkflowBeam");
                var selected=a.BeamGuids.Length==0 ? beams : new HashSet<Guid>(a.BeamGuids);
                if(a.ConcreteExcludedBeamGuids.Any(id=>!selected.Contains(id))) throw new ArgumentException("ConcreteExcludedBeamOutsideAction");
                if(a.ForceCaseGuid.HasValue)
                {
                    if(!cases.Contains(a.ForceCaseGuid.Value)) throw new ArgumentException("UnknownBridgeWorkflowCase");
                    if(!used.TryGetValue(a.ForceCaseGuid.Value,out var previous)) used.Add(a.ForceCaseGuid.Value,previous=new HashSet<Guid>());
                    if(previous.Overlaps(selected)) throw new ArgumentException("RepeatedBridgeForceIncrement"); previous.UnionWith(selected);
                    var source=(a.SourceModelGuid,a.SourceAnalysisFingerprint,a.SourceCaseGuid!.Value);
                    if(!sourceUsed.TryGetValue(source,out var previousSource)) sourceUsed.Add(source,previousSource=new HashSet<Guid>());
                    if(previousSource.Overlaps(selected)) throw new ArgumentException("RepeatedBridgeSourceIncrement"); previousSource.UnionWith(selected);
                }
                if(a.StrainCaseGuid.HasValue && !cases.Contains(a.StrainCaseGuid.Value)) throw new ArgumentException("UnknownBridgeWorkflowCase");
                foreach(var state in a.SectionStates)
                {
                    if(!selected.Contains(state.BeamGuid)) throw new ArgumentException("BridgeSectionStateOutsideAction");
                    if(state.Behavior!=a.Behavior) throw new ArgumentException("BridgeSectionBehaviorMismatch");
                    if(state.ClassificationCaseGuid.HasValue && !cases.Contains(state.ClassificationCaseGuid.Value)) throw new ArgumentException("UnknownClassificationCase");
                    if(state.PhysicalPropertyFingerprint!=PropertyFingerprint(byGuid[state.BeamGuid])) throw new ArgumentException("StaleBridgeSectionState");
                }
            }
    }
    /// <summary>Checks provenance against the supplied analysis models without retaining them in the recipe.</summary>
    public void ValidateSources(IEnumerable<GPC.Model.Models.Model> sources) => SourceCases(sources);

    /// <summary>Checks provenance and ties every force case of <paramref name="model"/> to its declared source case: the same persistent
    /// case when the source is this model (any revision), otherwise the same case GUID or name, the identity the result selectors use.
    /// A force case cannot silently carry the results of another source case.</summary>
    public void ValidateSources(GPC.Model.Models.Model model,IEnumerable<GPC.Model.Models.Model> sources)
    {
        if(model==null) throw new ArgumentNullException(nameof(model));
        var cases=model.LoadCases.Values.ToDictionary(c=>c.Guid);
        foreach(var (a,source) in SourceCases(sources))
        {
            if(!cases.TryGetValue(a.ForceCaseGuid!.Value,out var force)) throw new ArgumentException("UnknownBridgeWorkflowCase");
            bool tied=force.Guid==source.Guid || a.SourceModelGuid!=model.Guid && string.Equals(force.Name,source.Name,StringComparison.Ordinal);
            if(!tied) throw new ArgumentException("BridgeForceCaseNotFromSourceCase: "+a.Key);
        }
    }
    private List<(BridgeWorkflowActionDefinition Action,GPC.Model.LoadCases.LoadCaseBase Source)> SourceCases(IEnumerable<GPC.Model.Models.Model> sources)
    {
        ValidateShape();
        if(sources==null) throw new ArgumentNullException(nameof(sources));
        var available=sources.ToArray();
        if(available.Any(m=>m==null)) throw new ArgumentException("NullBridgeSourceModel");
        var byRevision=available.GroupBy(m=>(m.Guid,Revision:m.AnalysisFingerprint())).ToDictionary(g=>g.Key,g=>g.First());
        var caseIndex=new Dictionary<GPC.Model.Models.Model,Dictionary<Guid,GPC.Model.LoadCases.LoadCaseBase>>();
        var result=new List<(BridgeWorkflowActionDefinition,GPC.Model.LoadCases.LoadCaseBase)>();
        foreach(var a in Phases.SelectMany(p=>p.Actions).Where(a=>a.ForceCaseGuid.HasValue))
        {
            if(!byRevision.TryGetValue((a.SourceModelGuid,a.SourceAnalysisFingerprint),out var source)) throw new ArgumentException("MissingOrStaleBridgeSourceModel");
            if(!caseIndex.TryGetValue(source,out var sourceCases)) caseIndex.Add(source,sourceCases=source.LoadCases.Values.GroupBy(c=>c.Guid).ToDictionary(g=>g.Key,g=>g.First()));
            if(!sourceCases.TryGetValue(a.SourceCaseGuid!.Value,out var sourceCase)) throw new ArgumentException("UnknownBridgeSourceCase");
            result.Add((a,sourceCase));
        }
        return result;
    }
    private void ValidateShape()
    {
        if(schema!=1) throw new SerializationException("UnsupportedBridgeWorkflowSchema");
        if(ModelGuid==Guid.Empty || string.IsNullOrWhiteSpace(AnalysisFingerprint) || !Enum.IsDefined(typeof(BridgeWorkflowMethod),Method)) throw new ArgumentException("InvalidBridgeWorkflowIdentity");
        if(Phases==null || Phases.Length==0 || Phases.Any(p=>p==null)) throw new ArgumentException("MissingBridgeWorkflowPhases");
        if(Phases.Any(p=>p.Id==Guid.Empty) || Phases.Select(p=>p.Id).Distinct().Count()!=Phases.Length || Phases.Select(p=>p.Order).Distinct().Count()!=Phases.Length) throw new ArgumentException("DuplicateBridgePhaseIdentity");
        var keys=new HashSet<string>(StringComparer.Ordinal); bool composite=false; double previousDay=0;
        foreach(var p in Phases.OrderBy(p=>p.Order))
        {
            if(p.Order<0 || string.IsNullOrWhiteSpace(p.Name) || !Enum.IsDefined(typeof(BridgeWorkflowSectionState),p.State)) throw new ArgumentException("InvalidBridgePhase");
            if(p.Day.HasValue) { Finite(p.Day.Value); if(p.Day<previousDay) throw new ArgumentException("NonChronologicalBridgePhases"); previousDay=p.Day.Value; }
            if(p.State==BridgeWorkflowSectionState.Steel && composite) throw new ArgumentException("SteelAfterCompositeActivation");
            if(p.State==BridgeWorkflowSectionState.ConcreteExcluded && (!composite||Method==BridgeWorkflowMethod.Compatible)) throw new ArgumentException("ConcreteExclusionRequiresLinearHistory");
            composite|=p.State==BridgeWorkflowSectionState.Composite;
            if(p.Actions==null||p.Actions.Any(a=>a==null)) throw new ArgumentException("MissingBridgeActions");
            foreach(var a in p.Actions)
            {
                if(string.IsNullOrWhiteSpace(a.Key)||a.Key!=a.Key.Trim()||!keys.Add(a.Key)||!Enum.IsDefined(typeof(BridgeActionBehavior),a.Behavior)) throw new ArgumentException("InvalidBridgeAction");
                if((p.State==BridgeWorkflowSectionState.Steel)!=(a.Behavior==BridgeActionBehavior.Steel)) throw new ArgumentException("BridgeActionSectionMismatch");
                if(a.Strain.HasValue) { Finite(a.Strain.Value); if(a.Strain>0 || p.State!=BridgeWorkflowSectionState.Composite || a.Behavior==BridgeActionBehavior.Steel || a.Behavior==BridgeActionBehavior.LongTerm) throw new ArgumentException("InvalidBridgeShrinkageIncrement"); }
                if(a.Behavior==BridgeActionBehavior.Shrinkage && !a.Strain.HasValue) throw new ArgumentException("MissingShrinkageIncrement");
                if(a.PsiL.HasValue) { Finite(a.PsiL.Value); if(a.PsiL<=0) throw new ArgumentException("InvalidBridgeCreepMultiplier"); }
                if(a.ForceCaseGuid==Guid.Empty || a.StrainCaseGuid==Guid.Empty || a.SourceCaseGuid==Guid.Empty) throw new ArgumentException("EmptyBridgeCaseGuid");
                if(!a.ForceCaseGuid.HasValue && (!a.PrimaryOnly||!a.Strain.HasValue)) throw new ArgumentException("MissingBridgeForceCase");
                if(a.ForceCaseGuid.HasValue && (a.PrimaryOnly || !a.SourceCaseGuid.HasValue || a.SourceModelGuid==Guid.Empty || string.IsNullOrWhiteSpace(a.SourceAnalysisFingerprint))) throw new ArgumentException("MissingBridgeSourceProvenance");
                if(a.StrainCaseGuid.HasValue && !a.Strain.HasValue) throw new ArgumentException("MissingBridgeStrainIncrement");
                Ids(a.BeamGuids); Ids(a.ConcreteExcludedBeamGuids);
                if(a.SectionStates==null || a.SectionStates.Any(s=>s==null) || a.SectionStates.Select(s=>s.BeamGuid).Distinct().Count()!=a.SectionStates.Length) throw new ArgumentException("InvalidBridgeSectionStates");
                if((a.SectionStates.Length!=0||a.ConcreteExcludedBeamGuids.Length!=0) && p.State!=BridgeWorkflowSectionState.Composite) throw new ArgumentException("LocalStateRequiresCompositePhase");
                foreach(var state in a.SectionStates)
                {
                    Finite(state.ModularRatio);
                    if(state.BeamGuid==Guid.Empty || state.ModularRatio<=0 || string.IsNullOrWhiteSpace(state.PhysicalPropertyFingerprint) || !Enum.IsDefined(typeof(BridgeActionBehavior),state.Behavior)) throw new ArgumentException("InvalidBridgeSectionState");
                    if(state.ConcreteExcluded!=a.ConcreteExcludedBeamGuids.Contains(state.BeamGuid)) throw new ArgumentException("InconsistentConcreteExclusion");
                }
            }
        }
        if(keys.Count==0) throw new ArgumentException("EmptyBridgeWorkflow");
    }
    public static string PropertyFingerprint(GPC.Model.Elements.BeamElement beam) => ModelValues.Fingerprint(new object[] {beam.BeamProperty,beam.Assignments.Sections,beam.Assignments.AnalysisProfile,beam.Assignments.Physical?.Taper});
    public string Fingerprint() { ValidateShape(); return ModelValues.Fingerprint(new object[]{this}); }
    public byte[] Save()
    {
        ValidateShape(); using(var stream=new MemoryStream()) { Serializer().WriteObject(stream,this); return stream.ToArray(); }
    }
    public BridgeWorkflowDefinition Copy() => Read(Save());
    public static BridgeWorkflowDefinition Read(byte[] bytes)
    {
        if(bytes==null) throw new ArgumentNullException(nameof(bytes));
        if(bytes.Length>16*1024*1024) throw new SerializationException("BridgeWorkflowTooLarge");
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))
        {
            var value=(BridgeWorkflowDefinition)(Serializer().ReadObject(reader) ?? throw new SerializationException("MissingBridgeWorkflow")); value.ValidateShape(); return value;
        }
    }
    private static DataContractSerializer Serializer() => new(typeof(BridgeWorkflowDefinition),new DataContractSerializerSettings {MaxItemsInObjectGraph=1000000});
    private static void Finite(double value) { if(double.IsNaN(value)||double.IsInfinity(value)) throw new ArgumentException("NonFiniteBridgeWorkflowValue"); }
    private static void Ids(Guid[] values) { if(values==null || values.Any(g=>g==Guid.Empty) || values.Distinct().Count()!=values.Length) throw new ArgumentException("InvalidBridgeBeamSelection"); }
}

[DataContract]
public sealed class BridgeWorkflowPhaseDefinition
{
    [field:DataMember(Order=0)] public Guid Id { get; set; }
    [field:DataMember(Order=1)] public int Order { get; set; }
    [field:DataMember(Order=2)] public string Name { get; set; } = "";
    [field:DataMember(Order=3)] public BridgeWorkflowSectionState State { get; set; }
    [field:DataMember(Order=4)] public double? Day { get; set; }
    [field:DataMember(Order=5)] public BridgeWorkflowActionDefinition[] Actions { get; set; } = Array.Empty<BridgeWorkflowActionDefinition>();
}

[DataContract]
public sealed class BridgeWorkflowActionDefinition
{
    [field:DataMember(Order=0)] public string Key { get; set; } = "";
    [field:DataMember(Order=1)] public Guid SourceModelGuid { get; set; }
    [field:DataMember(Order=2)] public string SourceAnalysisFingerprint { get; set; } = "";
    [field:DataMember(Order=3)] public Guid? SourceCaseGuid { get; set; }
    [field:DataMember(Order=4)] public string SourceName { get; set; } = "";
    [field:DataMember(Order=5)] public BridgeActionBehavior Behavior { get; set; }
    [field:DataMember(Order=6)] public Guid? ForceCaseGuid { get; set; }
    [field:DataMember(Order=7)] public Guid? StrainCaseGuid { get; set; }
    [field:DataMember(Order=8)] public double? Strain { get; set; }
    [field:DataMember(Order=9)] public Guid[] BeamGuids { get; set; } = Array.Empty<Guid>();
    [field:DataMember(Order=10)] public bool PrimaryOnly { get; set; }
    [field:DataMember(Order=11)] public Guid[] ConcreteExcludedBeamGuids { get; set; } = Array.Empty<Guid>();
    [field:DataMember(Order=12)] public BridgeBeamSectionState[] SectionStates { get; set; } = Array.Empty<BridgeBeamSectionState>();
    /// <summary>Creep multiplier ψL of this action (EN 1994-2 5.4.2.2(2): 1.1 permanent loads, 0.55 shrinkage, 1.5 imposed
    /// deformations), so different actions of one workflow keep their own value. Null: 1, the value of recipes saved before it.</summary>
    [field:DataMember(Order=13,EmitDefaultValue=false)] public double? PsiL { get; set; }
}

[DataContract]
public sealed class BridgeBeamSectionState
{
    [field:DataMember(Order=0)] public Guid BeamGuid { get; set; }
    [field:DataMember(Order=1)] public BridgeActionBehavior Behavior { get; set; }
    [field:DataMember(Order=2)] public bool ConcreteExcluded { get; set; }
    [field:DataMember(Order=3)] public double ModularRatio { get; set; }
    [field:DataMember(Order=4)] public Guid? ClassificationCaseGuid { get; set; }
    [field:DataMember(Order=5)] public string PhysicalPropertyFingerprint { get; set; } = "";
}
