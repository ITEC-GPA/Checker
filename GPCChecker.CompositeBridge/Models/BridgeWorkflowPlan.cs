using System.Collections.Concurrent;
using GPC.Checkers.CompositeBridge.History;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Coordinates;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;

namespace GPC.Checkers.CompositeBridge;

/// <summary>A workflow prepared once for one model and its sources (user decision 9 of 4/11, «falli»; review L3/L10).
/// <para><see cref="Prepare"/> runs <see cref="BridgeWorkflowDefinition.Validate"/> and
/// <see cref="BridgeWorkflowDefinition.ValidateSources(GPC.Model.Models.Model, IEnumerable{GPC.Model.Models.Model})"/> once, with their
/// diagnostics (an invalid workflow, model or source fails here, as the executor's one-shot calls fail), and builds once what every
/// evaluation looks up: the beams and cases by GUID, the phases in order, the beams, section states and exclusions of each action, the
/// property fingerprint of each beam. <see cref="BuildInput(BridgeWorkflowEvaluation)"/> and <see cref="Calculate"/> then compile an
/// evaluation with the code of <see cref="BridgeWorkflowExecutor"/>: the same phases, bit for bit, without validating the workflow again
/// (the analysis fingerprints of the model and of the sources are the costly part of the validation).</para>
/// <para>The plan reads the model and the sources as they are at <see cref="Prepare"/> and keeps a copy of the workflow. A change to the
/// model or to the sources after it needs a new plan; the result selection of each evaluation (dataset, its input fingerprint, the
/// result state of the station) is still checked at every evaluation. The one-shot calls of the executor prepare at every call, so they
/// see any change. A plan is safe for concurrent evaluations.</para></summary>
public sealed class BridgeWorkflowPlan
{
    private readonly BridgeWorkflowDefinition workflow;
    private readonly GPC.Model.Models.Model model;
    private readonly Dictionary<Guid,BeamElement> beams;
    private readonly Dictionary<Guid,LoadCaseBase> cases;
    private readonly (BridgeWorkflowPhaseDefinition Phase,ActionLookup[] Actions)[] phases;
    private readonly ConcurrentDictionary<Guid,string> propertyFingerprints=new();

    /// <summary>What an evaluation needs of an action: its beams (empty: every beam), section states and concrete exclusions by beam.</summary>
    private sealed class ActionLookup
    {
        internal readonly BridgeWorkflowActionDefinition Action;
        private readonly HashSet<Guid>? beams;
        private readonly Dictionary<Guid,BridgeBeamSectionState> states;
        private readonly HashSet<Guid> excluded;
        internal ActionLookup(BridgeWorkflowActionDefinition action)
        {
            Action=action;beams=action.BeamGuids.Length==0?null:new HashSet<Guid>(action.BeamGuids);
            // Validate guarantees one state per beam and distinct GUIDs: the same answers as SingleOrDefault and Contains on the arrays.
            states=action.SectionStates.ToDictionary(s=>s.BeamGuid);excluded=new HashSet<Guid>(action.ConcreteExcludedBeamGuids);
        }
        internal bool Applies(Guid beam)=>beams==null||beams.Contains(beam);
        internal BridgeBeamSectionState? State(Guid beam)=>states.TryGetValue(beam,out var state)?state:null;
        internal bool Excluded(Guid beam)=>excluded.Contains(beam);
    }

    private BridgeWorkflowPlan(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model)
    {
        this.workflow=workflow;this.model=model;
        beams=model.BeamElements.Values.ToDictionary(b=>b.Guid);
        cases=model.LoadCases.Values.ToDictionary(c=>c.Guid);
        phases=workflow.Phases.OrderBy(p=>p.Order).Select(p=>(p,p.Actions.Select(a=>new ActionLookup(a)).ToArray())).ToArray();
    }

    /// <summary>Validates <paramref name="workflow"/> against <paramref name="model"/> and its <paramref name="sources"/> once and prepares
    /// the evaluations. The plan keeps a copy of the workflow: later changes to it do not reach the plan.</summary>
    /// <exception cref="ArgumentNullException">A null workflow or model.</exception>
    /// <exception cref="ArgumentException">The diagnostics of <see cref="BridgeWorkflowDefinition.Validate"/> and
    /// <see cref="BridgeWorkflowDefinition.ValidateSources(GPC.Model.Models.Model, IEnumerable{GPC.Model.Models.Model})"/>.</exception>
    public static BridgeWorkflowPlan Prepare(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,IEnumerable<GPC.Model.Models.Model> sources)
    {
        if(workflow==null||model==null)throw new ArgumentNullException();
        workflow.Validate(model);workflow.ValidateSources(model,sources);
        return new BridgeWorkflowPlan(workflow.Detach(),model);
    }

    /// <summary>The validation of the one-shot calls, then a plan on the caller's workflow (used at once, not kept).</summary>
    internal static BridgeWorkflowPlan PrepareCore(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,IEnumerable<GPC.Model.Models.Model> sources)
    {
        workflow.Validate(model);workflow.ValidateSources(model,sources);
        return new BridgeWorkflowPlan(workflow,model);
    }

    /// <summary>The method of the prepared workflow.</summary>
    public BridgeWorkflowMethod Method=>workflow.Method;

    /// <summary>As <see cref="BridgeWorkflowExecutor.BuildInput(BridgeWorkflowDefinition, GPC.Model.Models.Model, IEnumerable{GPC.Model.Models.Model}, BridgeWorkflowEvaluation)"/>:
    /// strict, an out-of-plane action above roundoff is rejected (BridgeSectionCannotDiscardOutOfPlaneActions).</summary>
    public HBridgeInput BuildInput(BridgeWorkflowEvaluation evaluation)
    {
        if(evaluation==null)throw new ArgumentNullException();
        return Compile(evaluation,true,out _);
    }

    /// <summary>As <see cref="BridgeWorkflowExecutor.BuildInput(BridgeWorkflowDefinition, GPC.Model.Models.Model, IEnumerable{GPC.Model.Models.Model}, BridgeWorkflowEvaluation, out IReadOnlyList{string})"/>:
    /// BridGHe's planar assumption unless <see cref="BridgeWorkflowEvaluation.StrictOutOfPlaneActions"/>, with its warnings.</summary>
    public HBridgeInput BuildInput(BridgeWorkflowEvaluation evaluation,out IReadOnlyList<string> warnings)
    {
        if(evaluation==null)throw new ArgumentNullException();
        return Compile(evaluation,null,out warnings);
    }

    /// <summary>As <see cref="BridgeWorkflowExecutor.Calculate"/>: the section engine of the method on the compiled input, with its warnings on every stage.</summary>
    public HBridgeAnalysisResult Calculate(BridgeWorkflowEvaluation evaluation,CancellationToken cancellation=default)
    {
        var input=BuildInput(evaluation,out var warnings);
        var result=workflow.Method==BridgeWorkflowMethod.Compatible ? HBridgeSection.Calculate(input,cancellation)
            : HBridgeHistoryResults.Calculate(input,new HBridgeHistoryOptions {MaterialMode=HistoryMaterialMode.Linear},cancellation);
        foreach(var stage in result.Stages) stage.Warnings.AddRange(warnings);
        return result;
    }

    /// <summary>The compilation of <see cref="BridgeWorkflowExecutor"/> (same checks of the evaluation, same arithmetic, same order).</summary>
    internal HBridgeInput Compile(BridgeWorkflowEvaluation evaluation,bool? strictOverride,out IReadOnlyList<string> warnings)
    {
        bool strict=strictOverride??evaluation.StrictOutOfPlaneActions;
        var notes=new List<string>();warnings=notes;
        var e=evaluation;
        if(!beams.TryGetValue(e.BeamGuid,out var beam)) throw new ArgumentException("UnknownBridgeEvaluationBeam");
        if(e.PhysicalPropertyFingerprint!=propertyFingerprints.GetOrAdd(beam.Guid,_=>BridgeWorkflowDefinition.PropertyFingerprint(beam))) throw new ArgumentException("StaleBridgeEvaluationSection");
        Finite(e.Station);if(e.Station<0||e.Station>1||!Enum.IsDefined(typeof(SectionSide),e.Side)
            ||!Enum.IsDefined(typeof(BridgeLoadReference),e.LoadReference)) throw new ArgumentException("InvalidBridgeEvaluationLocation");
        Axes.Validate(e.SectionAxes);
        if(beam.NodeI==null||beam.NodeJ==null)throw new ArgumentException("BridgeWorkflowRequiresConnectedBeam");
        var tangent=beam.NodeJ.Position-beam.NodeI.Position;double length=Axes.Length(tangent);
        if(!(length>1e-9)||Math.Abs(Axes.Dot(tangent/length,e.SectionAxes.V3)-1)>1e-8
            ||Math.Abs(Axes.Dot(e.SectionAxes.Origin-beam.NodeI.Position,tangent/length)-e.Station*length)>1e-7)
            throw new ArgumentException("BridgeReductionAxesMustMatchStationAndConnectivity");
        if(e.SectionInput==null||e.SectionInput.Phases==null||e.SectionInput.Phases.Length!=0)
            throw new ArgumentException("BridgeSectionInputMustHaveNoPhases");
        foreach(double phi in new[]{e.LongTermPhi,e.ShortTermPhi,e.ShrinkagePhi}) {Finite(phi);if(phi<0)throw new ArgumentException("NegativeBridgeCreepCoefficient");}
        if(e.Factors==null||e.Results==null)throw new ArgumentException("MissingBridgeEvaluationBindings");
        foreach(var pair in e.Factors) {Finite(pair.Value);if(!cases.ContainsKey(pair.Key))throw new ArgumentException("UnknownBridgeFactorCase");}
        foreach(var pair in e.Results)
            if(!cases.TryGetValue(pair.Key,out var load)||pair.Value==null||pair.Value.Case!=load.Name)
                throw new ArgumentException("BridgeResultCaseBindingMismatch");
        double Factor(Guid? id)=>id.HasValue&&e.Factors.TryGetValue(id.Value,out double f)?f:0;
        // The H engines verify uniaxial bending/shear; a box with its torsion checks also verifies torque.
        bool torsion=e.SectionInput.Geometry.SectionType==BridgeSteelSectionType.Box&&e.SectionInput.Box.Enabled;
        int neglected=0;double maxV1=0,maxM2=0,maxT=0;
        var compiled=new List<BridgePhase>();
        foreach(var (phase,actions) in phases)
        {
            int before=compiled.Count;
            // The state of the last unloaded increment of a local state in this phase (see below).
            (BridgePhaseKind Kind,double Phi,BridgeHomogenizationSource Source,double N)? unloaded=null;
            var baseKind=phase.State==BridgeWorkflowSectionState.Steel?BridgePhaseKind.SteelOnly:
                phase.State==BridgeWorkflowSectionState.Composite?BridgePhaseKind.Composite:BridgePhaseKind.ConcreteExcluded;
            foreach(var lookup in actions)
            {
                if(!lookup.Applies(beam.Guid))continue;
                var action=lookup.Action;
                double forceFactor=Factor(action.ForceCaseGuid);
                // Coefficients come only from cases (contract of Factors). A primary strain has its strain case: Validate rejects one without it
                // (review L4, BridgePrimaryStrainRequiresStrainCase), so a coupled strain takes the coefficient of its force case.
                double strainFactor=action.StrainCaseGuid.HasValue?Factor(action.StrainCaseGuid):forceFactor;
                if(action.ForceCaseGuid.HasValue&&action.StrainCaseGuid.HasValue&&forceFactor!=strainFactor)
                    throw new ArgumentException("BridgeForceAndStrainFactorsDiffer: "+action.Key);
                var local=lookup.State(beam.Guid);
                bool excluded=lookup.Excluded(beam.Guid);
                // A beam whose concrete is excluded in this action (the cracked region of a local FEM state): its increment on the
                // concrete-excluded section, with both methods. Compatible sums it with the others, as BridGHe's checks (examples 50:
                // 48 beams near the supports in the long- and short-term actions). A whole phase excluded still needs LinearHistory (Validate).
                var kind=excluded?BridgePhaseKind.ConcreteExcluded:baseKind;
                double phi=action.Behavior==BridgeActionBehavior.LongTerm?e.LongTermPhi:
                    action.Behavior==BridgeActionBehavior.ShortTerm?e.ShortTermPhi:action.Behavior==BridgeActionBehavior.Shrinkage?e.ShrinkagePhi:0;
                var increment=new BridgePhase {Name=phase.Order+" / "+phase.Name+" / "+action.Key,Kind=kind,Reference=e.LoadReference,
                    Phi=phi,PsiL=action.PsiL??1,HomogenizationSource=local==null?BridgeHomogenizationSource.Phi:BridgeHomogenizationSource.ModularRatio,
                    N=local?.ModularRatio??double.NaN};
                // An unloaded increment of a local state (coefficient zero: an alternative moving position, for example) only activates
                // its stiffness: one per run of identical states, as BridGHe's checks, or the 31 positions of example 50 would exceed the
                // 20 phases of the section engine on the concrete-excluded beams. Changes of state and loaded increments are kept.
                var state=(kind,phi,increment.HomogenizationSource,increment.N);
                bool repeated=forceFactor==0&&unloaded.HasValue&&unloaded.Value.Equals(state);
                if(action.ForceCaseGuid.HasValue)unloaded=forceFactor==0&&(local!=null||excluded)?state:null;
                if(action.ForceCaseGuid.HasValue&&(forceFactor!=0||(local!=null||excluded)&&!repeated))
                {
                    var force=forceFactor==0?new ResultBeamForces(0,0,0,0,0,0,e.SectionAxes):Read(action.ForceCaseGuid.Value);
                    // Only roundoff from orthonormal rotations counts as in-plane (N / Nmm).
                    double forceTolerance=Math.Max(1e-8,Math.Max(Math.Abs(force.N),Math.Abs(force.V2))*1e-12);
                    double momentTolerance=Math.Max(1e-8,Math.Abs(force.M1)*1e-12);
                    double v1=Math.Abs(force.V1*forceFactor),m2=Math.Abs(force.M2*forceFactor),t=torsion?0:Math.Abs(force.T*forceFactor);
                    if(Math.Abs(force.V1)>forceTolerance||Math.Abs(force.M2)>momentTolerance||(!torsion&&Math.Abs(force.T)>momentTolerance))
                    {
                        if(strict)throw new NotSupportedException("BridgeSectionCannotDiscardOutOfPlaneActions: "+action.Key);
                        neglected++;maxV1=Math.Max(maxV1,v1);maxM2=Math.Max(maxM2,m2);maxT=Math.Max(maxT,t);
                    }
                    compiled.Add(increment with {ForceKN=force.N*forceFactor/1000,MomentKNm=force.M1*forceFactor/1e6,
                        ShearKN=force.V2*forceFactor/1000,TorsionKNm=torsion?force.T*forceFactor/1e6:0});
                }
                if(action.Strain.HasValue&&action.Strain.Value*strainFactor!=0)
                {
                    if(kind!=BridgePhaseKind.Composite)throw new NotSupportedException("ShrinkageRequiresActiveConcrete");
                    compiled.Add(increment with {Name=increment.Name+" / strain",Kind=BridgePhaseKind.Shrinkage,
                        ShrinkageMicrostrain=action.Strain.Value*strainFactor*1e6});
                    unloaded=null;
                }
            }
            // Activation remains a construction event even when all action coefficients vanish.
            if(compiled.Count==before)compiled.Add(new BridgePhase {Name=phase.Order+" / "+phase.Name,Kind=baseKind,
                Reference=e.LoadReference,Phi=baseKind==BridgePhaseKind.SteelOnly?0:e.ShortTermPhi});
        }
        foreach(var p in compiled)foreach(double value in new[]{p.ForceKN,p.MomentKNm,p.ShearKN,p.TorsionKNm,p.ShrinkageMicrostrain})Finite(value);
        if(!strict)
        {
            notes.Add(torsion?BridgeWorkflowExecutor.PlanarTorsionAssumption:BridgeWorkflowExecutor.PlanarAssumption);
            var invariant=System.Globalization.CultureInfo.InvariantCulture;
            if(neglected!=0)notes.Add(string.Format(invariant,"Azioni fuori dal piano trascurate in {0} incrementi: |V1| fino a {1:G6} N, |M2| fino a {2:G6} N mm, |T| fino a {3:G6} N mm.",
                neglected,maxV1,maxM2,maxT));
        }
        return e.SectionInput with {Phases=compiled.ToArray()};

        ResultBeamForces Read(Guid id)
        {
            if(!e.Results.TryGetValue(id,out var selection))throw new ArgumentException("MissingBridgeResultSelection");
            if(!model.Datasets.TryGetValue(selection.Dataset,out var dataset)||dataset.InputFingerprint!=workflow.AnalysisFingerprint)
                throw new ArgumentException("MissingOrStaleBridgeResultDataset");
            if(dataset.NormalizedUnits!="N,mm,rad")throw new NotSupportedException("BridgeResultsRequireNormalizedNmmUnits");
            var sample=ResultQueries.BeamStation(beam,selection,e.Station,e.Side)??throw new ArgumentException("MissingBridgeResultStation");
            var state=sample.State;
            if(state==null||state.InputFingerprint!=workflow.AnalysisFingerprint
                ||!(sample.Case is GPC.Model.LoadCases.LoadCaseBase sampleCase)||sampleCase.Guid!=id)
                throw new ArgumentException("StaleBridgeResultState");
            if(dataset.Semantics!=AnalysisSemantics.LinearStatic||state.Semantics!=AnalysisSemantics.LinearStatic
                ||state.IsCombined!=false||state.Phase!=null||state.Step!=null||state.Mode.HasValue)
                throw new NotSupportedException("BridgeWorkflowRequiresIndependentLinearStaticCases");
            if(sample.Body!=ActionBody.PositiveSectionFace)throw new NotSupportedException("BridgeResultRequiresPositiveSectionFace");
            return ActionTransformations.TransportBeam(sample,e.SectionAxes).ResultBeamForces;
        }
    }
    private static void Finite(double value) {if(double.IsNaN(value)||double.IsInfinity(value))throw new ArgumentException("NonFiniteBridgeEvaluationValue");}
}
