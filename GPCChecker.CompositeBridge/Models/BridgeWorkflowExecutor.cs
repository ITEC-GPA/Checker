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

    /// <summary>Validates the workflow against the model and its sources once and returns the plan that compiles every evaluation without
    /// validating again (user decision 9 of 4/11): for the many sections of a bridge. Same diagnostics as the one-shot calls, at preparation.</summary>
    public static BridgeWorkflowPlan Prepare(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,IEnumerable<GPC.Model.Models.Model> sources)
        => BridgeWorkflowPlan.Prepare(workflow,model,sources);

    // One-shot calls: the plan prepared at every call (Validate, ValidateSources), then the same compilation.
    private static HBridgeInput Compile(BridgeWorkflowDefinition workflow,GPC.Model.Models.Model model,
        IEnumerable<GPC.Model.Models.Model> sources,BridgeWorkflowEvaluation evaluation,bool? strictOverride,out IReadOnlyList<string> warnings)
    {
        if(workflow==null||model==null||evaluation==null) throw new ArgumentNullException();
        return BridgeWorkflowPlan.PrepareCore(workflow,model,sources).Compile(evaluation,strictOverride,out warnings);
    }
}
