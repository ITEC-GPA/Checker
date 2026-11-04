using GPC.Checkers.CompositeBridge.History;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Coordinates;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;

namespace GPC.Checkers.CompositeBridge;

/// <summary>Evaluation inputs, not a second model archive. SectionInput contains geometry/materials/check options and
/// no phases. SectionAxes identifies the load reduction point in mm and the section axes (3 longitudinal, 2 height).
/// The caller declares which centroid/elevation that point represents through LoadReference.</summary>
public sealed class BridgeWorkflowEvaluation
{
    public Guid BeamGuid { get; set; }
    public string PhysicalPropertyFingerprint { get; set; } = "";
    public double Station { get; set; }
    public SectionSide Side { get; set; }
    public HBridgeInput SectionInput { get; set; } = null!;
    public CoordinateSystem SectionAxes { get; set; } = null!;
    public BridgeLoadReference LoadReference { get; set; }
    /// <summary>Explicit coefficients by persistent model case GUID. An absent case has coefficient zero.</summary>
    public IReadOnlyDictionary<Guid,double> Factors { get; set; } = new Dictionary<Guid,double>();
    /// <summary>Exact selectors for the model force cases used by the workflow. No implicit interpolation or envelope.</summary>
    public IReadOnlyDictionary<Guid,ResultSelection> Results { get; set; } = new Dictionary<Guid,ResultSelection>();
    public double LongTermPhi { get; set; }
    public double ShortTermPhi { get; set; }
    public double ShrinkagePhi { get; set; }
    /// <summary>False (default), as BridGHe's production adapter: the section engines cover N–Mx–Vy (plus T for a box with torsion checks),
    /// so V1, M2 and the other torques are neglected and the results carry <see cref="BridgeWorkflowExecutor.PlanarAssumption"/>.
    /// True: any such action above roundoff is rejected, as always by the BuildInput overload without warnings.</summary>
    public bool StrictOutOfPlaneActions { get; set; }
}

/// <summary>Compiles physical N/mm results into the existing kN/kNm section engines. Linear static primary cases are
/// independent action increments; staged/nonlinear totals and independent extrema are rejected instead of summed.</summary>
public static class BridgeWorkflowExecutor
{
    /// <summary>The warning of the neglected out-of-plane actions: the text of BridGHe's BridgeCheckAdapter.</summary>
    public const string PlanarAssumption = "Verifica nel piano N–Mx–Vy: torsione T, flessione sull'asse debole M2 e taglio trasversale V1 sono trascurati nelle verifiche di sezione e dei dettagli; rimangono nei risultati FEM. I relativi effetti e interazioni non sono verificati.";
    /// <summary>The same warning for a box with torsion checks, which keeps T.</summary>
    public const string PlanarTorsionAssumption = "Verifica nel piano N–Mx–Vy con la torsione T del cassoncino: flessione sull'asse debole M2 e taglio trasversale V1 sono trascurati nelle verifiche di sezione e dei dettagli; rimangono nei risultati FEM. I relativi effetti e interazioni non sono verificati.";

    public static HBridgeAnalysisResult Calculate(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,
        IEnumerable<GPC.Model.Models.Model> sources,BridgeWorkflowEvaluation evaluation,CancellationToken cancellation=default)
    {
        var input=BuildInput(workflow,model,sources,evaluation,out var warnings);
        var result=workflow.Method==BridgeWorkflowMethod.Compatible ? HBridgeSection.Calculate(input,cancellation)
            : HBridgeHistoryResults.Calculate(input,new HBridgeHistoryOptions {MaterialMode=HistoryMaterialMode.Linear},cancellation);
        foreach(var stage in result.Stages) stage.Warnings.AddRange(warnings);
        return result;
    }

    /// <summary>Strict compilation, for a caller that cannot receive warnings (review L4): an out-of-plane action above roundoff is rejected
    /// (BridgeSectionCannotDiscardOutOfPlaneActions) whatever <see cref="BridgeWorkflowEvaluation.StrictOutOfPlaneActions"/> says, as before
    /// the planar assumption existed, so no action is neglected without a trace. BridGHe's planar assumption needs the overload with warnings.</summary>
    public static HBridgeInput BuildInput(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,
        IEnumerable<GPC.Model.Models.Model> sources,BridgeWorkflowEvaluation evaluation) => Compile(workflow,model,sources,evaluation,true,out _);

    /// <param name="warnings">The assumptions of the compiled input, for every stage of its results: the planar assumption and the neglected
    /// out-of-plane actions unless <see cref="BridgeWorkflowEvaluation.StrictOutOfPlaneActions"/>.</param>
    public static HBridgeInput BuildInput(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,
        IEnumerable<GPC.Model.Models.Model> sources,BridgeWorkflowEvaluation evaluation,out IReadOnlyList<string> warnings)
        => Compile(workflow,model,sources,evaluation,null,out warnings);

    private static HBridgeInput Compile(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,
        IEnumerable<GPC.Model.Models.Model> sources,BridgeWorkflowEvaluation evaluation,bool? strictOverride,out IReadOnlyList<string> warnings)
    {
        if(workflow==null||model==null||evaluation==null) throw new ArgumentNullException();
        workflow.Validate(model);workflow.ValidateSources(model,sources);
        bool strict=strictOverride??evaluation.StrictOutOfPlaneActions;
        var notes=new List<string>();warnings=notes;
        var e=evaluation;var beam=model.BeamElements.Values.SingleOrDefault(b=>b.Guid==e.BeamGuid)
            ?? throw new ArgumentException("UnknownBridgeEvaluationBeam");
        if(e.PhysicalPropertyFingerprint!=BridgeWorkflowDefinition.PropertyFingerprint(beam)) throw new ArgumentException("StaleBridgeEvaluationSection");
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
        var cases=model.LoadCases.Values.ToDictionary(c=>c.Guid);
        foreach(var pair in e.Factors) {Finite(pair.Value);if(!cases.ContainsKey(pair.Key))throw new ArgumentException("UnknownBridgeFactorCase");}
        foreach(var pair in e.Results)
            if(!cases.TryGetValue(pair.Key,out var load)||pair.Value==null||pair.Value.Case!=load.Name)
                throw new ArgumentException("BridgeResultCaseBindingMismatch");
        double Factor(Guid? id)=>id.HasValue&&e.Factors.TryGetValue(id.Value,out double f)?f:0;
        // The H engines verify uniaxial bending/shear; a box with its torsion checks also verifies torque.
        bool torsion=e.SectionInput.Geometry.SectionType==BridgeSteelSectionType.Box&&e.SectionInput.Box.Enabled;
        int neglected=0;double maxV1=0,maxM2=0,maxT=0;
        var phases=new List<BridgePhase>();
        foreach(var phase in workflow.Phases.OrderBy(p=>p.Order))
        {
            int before=phases.Count;
            // The state of the last unloaded increment of a local state in this phase (see below).
            (BridgePhaseKind Kind,double Phi,BridgeHomogenizationSource Source,double N)? unloaded=null;
            var baseKind=phase.State==BridgeWorkflowSectionState.Steel?BridgePhaseKind.SteelOnly:
                phase.State==BridgeWorkflowSectionState.Composite?BridgePhaseKind.Composite:BridgePhaseKind.ConcreteExcluded;
            foreach(var action in phase.Actions.Where(a=>a.BeamGuids.Length==0||a.BeamGuids.Contains(beam.Guid)))
            {
                double forceFactor=Factor(action.ForceCaseGuid);
                // Coefficients come only from cases (contract of Factors). A primary strain has its strain case: Validate rejects one without it
                // (review L4, BridgePrimaryStrainRequiresStrainCase), so a coupled strain takes the coefficient of its force case.
                double strainFactor=action.StrainCaseGuid.HasValue?Factor(action.StrainCaseGuid):forceFactor;
                if(action.ForceCaseGuid.HasValue&&action.StrainCaseGuid.HasValue&&forceFactor!=strainFactor)
                    throw new ArgumentException("BridgeForceAndStrainFactorsDiffer: "+action.Key);
                var local=action.SectionStates.SingleOrDefault(s=>s.BeamGuid==beam.Guid);
                bool excluded=action.ConcreteExcludedBeamGuids.Contains(beam.Guid);
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
                    phases.Add(increment with {ForceKN=force.N*forceFactor/1000,MomentKNm=force.M1*forceFactor/1e6,
                        ShearKN=force.V2*forceFactor/1000,TorsionKNm=torsion?force.T*forceFactor/1e6:0});
                }
                if(action.Strain.HasValue&&action.Strain.Value*strainFactor!=0)
                {
                    if(kind!=BridgePhaseKind.Composite)throw new NotSupportedException("ShrinkageRequiresActiveConcrete");
                    phases.Add(increment with {Name=increment.Name+" / strain",Kind=BridgePhaseKind.Shrinkage,
                        ShrinkageMicrostrain=action.Strain.Value*strainFactor*1e6});
                    unloaded=null;
                }
            }
            // Activation remains a construction event even when all action coefficients vanish.
            if(phases.Count==before)phases.Add(new BridgePhase {Name=phase.Order+" / "+phase.Name,Kind=baseKind,
                Reference=e.LoadReference,Phi=baseKind==BridgePhaseKind.SteelOnly?0:e.ShortTermPhi});
        }
        foreach(var p in phases)foreach(double value in new[]{p.ForceKN,p.MomentKNm,p.ShearKN,p.TorsionKNm,p.ShrinkageMicrostrain})Finite(value);
        if(!strict)
        {
            notes.Add(torsion?PlanarTorsionAssumption:PlanarAssumption);
            var invariant=System.Globalization.CultureInfo.InvariantCulture;
            if(neglected!=0)notes.Add(string.Format(invariant,"Azioni fuori dal piano trascurate in {0} incrementi: |V1| fino a {1:G6} N, |M2| fino a {2:G6} N mm, |T| fino a {3:G6} N mm.",
                neglected,maxV1,maxM2,maxT));
        }
        return e.SectionInput with {Phases=phases.ToArray()};

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
