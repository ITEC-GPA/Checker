using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Euler-Bernoulli plane beam, two degrees of freedom per node (v, theta = dv/dx), consistent nodal loads of a uniform
/// load: with uniform elements the nodal values are exact, so the FE nodes reproduce the closed forms. Prescribed values model
/// supports, settlements and clamps. N, mm.</summary>
internal static class PlaneBeamFe
{
    internal static (double[] V, double[] Theta) Solve(double[] x, double ei, double q, IReadOnlyDictionary<int, double> v, IReadOnlyDictionary<int, double> theta,
        IReadOnlyDictionary<int, double>? force = null)
    {
        int n = x.Length, size = 2 * n; var k = new double[size, size]; var f = new double[size];
        for (int e = 0; e < n - 1; e++)
        {
            double l = x[e + 1] - x[e], c = ei / (l * l * l);
            double[,] ke = { { 12, 6 * l, -12, 6 * l }, { 6 * l, 4 * l * l, -6 * l, 2 * l * l }, { -12, -6 * l, 12, -6 * l }, { 6 * l, 2 * l * l, -6 * l, 4 * l * l } };
            double[] fe = { q * l / 2, q * l * l / 12, q * l / 2, -q * l * l / 12 };
            for (int i = 0; i < 4; i++) { f[2 * e + i] += fe[i]; for (int j = 0; j < 4; j++) k[2 * e + i, 2 * e + j] += c * ke[i, j]; }
        }
        foreach (var p in force ?? new Dictionary<int, double>()) f[2 * p.Key] += p.Value;
        var known = new Dictionary<int, double>();
        foreach (var p in v) known[2 * p.Key] = p.Value;
        foreach (var p in theta) known[2 * p.Key + 1] = p.Value;
        var free = Enumerable.Range(0, size).Where(d => !known.ContainsKey(d)).ToArray();
        int m = free.Length; var a = new double[m, m + 1];
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < m; j++) a[i, j] = k[free[i], free[j]];
            a[i, m] = f[free[i]] - known.Sum(p => k[free[i], p.Key] * p.Value);
        }
        for (int col = 0; col < m; col++)
        {
            int pivot = Enumerable.Range(col, m - col).OrderByDescending(r => Math.Abs(a[r, col])).First();
            for (int j = 0; j <= m; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
            for (int r = 0; r < m; r++)
            {
                if (r == col) continue;
                double factor = a[r, col] / a[col, col];
                for (int j = col; j <= m; j++) a[r, j] -= factor * a[col, j];
            }
        }
        var u = new double[size];
        foreach (var p in known) u[p.Key] = p.Value;
        for (int i = 0; i < m; i++) u[free[i]] = a[i, m] / a[i, i];
        return (Enumerable.Range(0, n).Select(i => u[2 * i]).ToArray(), Enumerable.Range(0, n).Select(i => u[2 * i + 1]).ToArray());
    }
}

/// <summary>Steel.Serviceability.Member end to end: FE nodal displacements of straight steel members imported as Model results,
/// deformation tasks with declared categories, the engine with declared limits (EC3 2005 and NTC 2018), archives and currency.
/// E = 210000 MPa, I1 = 8.356e7 mm^4 (IPE 300 major axis), I2 = 6.04e6 mm^4; expected values are the closed forms in the comments.</summary>
[TestClass]
public class SteelMemberServiceabilityTest
{
    private const double E = 210000, I1 = 8.356e7, I2 = 6.04e6;
    private static double[] Stations(double length, int elements) => Enumerable.Range(0, elements + 1).Select(k => length * k / elements).ToArray();
    private static readonly Dictionary<int, double> None = new();

    /// <summary>One straight member "M" of equal elements from the origin along <paramref name="along"/>, section axis 1
    /// <paramref name="axis1"/>; states: case name -> global (dx, dy, dz, rx, ry, rz) per node, referred to the node.</summary>
    private static GPC.Model.Models.Model Frame(double length, int elements, Vector3d along, Vector3d axis1, IReadOnlyDictionary<string, double[][]> states,
        bool reverseSecond = false, bool concrete = false)
    {
        var model = new GPC.Model.Models.Model("Steel serviceability: FE nodal displacements of the plane beam solver");
        for (int k = 0; k <= elements; k++) model.NodesElements.Add(new NodeElement(new Point3d(along.X * length * k / elements, along.Y * length * k / elements, along.Z * length * k / elements), id: k + 1));
        BeamProperty section = concrete ? new ReinforcedConcreteSection(new SectionRectangular(500, 300, "RC"), ConcreteMaterialEN1992Data.C25_30)
            : new SteelSection(new SectionH(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15), SteelMaterialEN1993Data.S355);
        model.AddProperty(section);
        var parts = new List<BeamMemberPart>();
        for (int e = 0; e < elements; e++)
        {
            bool alongIJ = !(reverseSecond && e == 1);
            var beam = new BeamElement(null, null, section, id: 100 + e); model.BeamElements.Add(beam);
            model.ConnectBeam(beam.Id, alongIJ ? e + 1 : e + 2, alongIJ ? e + 2 : e + 1);
            beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; beam.Assignments.StationDomain = "NodeToNode";
            beam.Assignments.SectionAxes = Axes.Beam(beam.StartPoint, beam.EndPoint, axis1);
            beam.Assignments.SectionGeometryAxes = beam.Assignments.SectionAxes; beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
            beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
            parts.Add(new BeamMemberPart(beam.Id, alongIJ));
        }
        model.PhysicalMembers.Add("M", new PhysicalMemberDefinition("M", parts, "NodeToNode", source: "Test: explicit straight member"));
        foreach (var name in states.Keys) model.LoadCases.Add(new LoadCaseBase(name));
        model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Plane Euler-Bernoulli FE solution of the test.");
        string fp = model.AnalysisFingerprint();
        model.Datasets.Add("fe", new AnalysisDataset { Id = "fe", Program = "PlaneBeamFe (test)", ModelRevision = "r1", InputFingerprint = fp, NormalizedUnits = "N,mm,rad", IsSynthetic = true, Semantics = AnalysisSemantics.LinearStatic });
        foreach (var lc in model.LoadCases.Values)
            foreach (var node in model.NodesElements.Values)
            {
                var d = states[lc.Name][node.Id - 1];
                node.AddResult(new NodeResult(new List<INodeResultLocation> { new NodeResultDisplacement(lc,
                    new ResultDisplacement(ActionTransformations.AtPoint(CoordinateSystem.Global, node.Position), d[0], d[1], d[2], d[3], d[4], d[5])) {
                    State = new ResultState { DatasetId = "fe", ModelRevision = "r1", InputFingerprint = fp, Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(),
                        Semantics = AnalysisSemantics.LinearStatic, ConcomitantStateId = lc.Name, IsCumulative = true, IsSynthetic = true } } }));
            }
        return model;
    }
    /// <summary>Beam along X, vertical Z (member axis 2 with section axis 1 = Y): dz = v, ry = -dv/dx.</summary>
    private static double[][] Horizontal((double[] V, double[] Theta) fe) => fe.V.Select((v, i) => new[] { 0, 0, v, 0, -fe.Theta[i], 0 }).ToArray();
    private static ResultSelection State(string name, CombinationCategory? category = null) => new() { Dataset = "fe", Case = name, ConcomitantState = name, Category = category };
    private static MemberCheckSpecification Task(MemberDeformationCriterion criterion, MemberDeformationDirection direction, CombinationCategory category,
        MemberVariableDeflectionBasis basis = MemberVariableDeflectionBasis.None, string? variable = null, double start = 0, double? end = null)
        => new() { MemberId = "M", MethodId = SteelMemberServiceabilityChecker.EngineId, Mechanism = CheckMechanism.Serviceability, Category = category, Start = start, End = end,
            Context = new MemberDesignContext(globalStateConfirmed: true),
            Deformation = new MemberDeformationSpecification(criterion, direction, basis, variable == null ? null : State(variable)) };
    private static Dictionary<string, string> DeflectionParameters(string scheme = "SupportedEnds") => new() {
        ["Deflection.Scheme"] = scheme, ["Deflection.SchemeSource"] = "Test: pinned supports at both span ends",
        ["Deformation.CoverageSource"] = "Test: eight equal elements, node at mid-span (symmetric load)",
        ["Deflection.MaxRatio"] = "250", ["Deflection.MaxSource"] = "Project agreement: floors in general L/250",
        ["Deflection.VariableRatio"] = "300", ["Deflection.VariableSource"] = "Project agreement: variable actions L/300",
        ["Deflection.Precamber"] = "0" };
    private static VerificationConfiguration Configure(BeamCheckPlanRequest plan, Dictionary<string, string> parameters, bool ntc, string? edition = null)
        => new() {
            Contexts = new[] { new DesignContextDefinition { Id = "code", Code = ntc ? new StandardNTC2018Steel() : new StandardEN1993p11(), Edition = edition ?? (ntc ? "2018" : "2005") } },
            Engines = new[] { new EngineDefinition { Id = "sle", Kind = SteelMemberServiceabilityChecker.EngineId, ContextId = "code", Parameters = parameters } },
            Jobs = new[] { new ConfiguredCheckJob { Name = "SLE", Plan = plan, Routes = new[] { new CheckRouteDefinition { EngineId = "sle", Mechanisms = new[] { CheckMechanism.Serviceability } } } } } };
    private static BeamCheckPlanRequest Plan(MemberCheckSpecification spec, params ResultSelection[] results)
        => new() { MemberIds = new[] { "M" }, SectionMechanisms = Array.Empty<CheckMechanism>(), Results = results, MemberChecks = new[] { spec } };
    private static (ModelCheckReport Report, CheckResult Row, MultiMaterialCheckRequest Request) Run(GPC.Model.Models.Model model, VerificationConfiguration config)
    {
        var request = EngineCatalog.BuiltIn().Compile(config); var report = new Service().Verify(model, request);
        return (report, report.Jobs.Single().Results.Single(), request);
    }
    private static string Codes(CheckResult r) => string.Join(";", r.Diagnostics.Select(d => d.Code + (d.Message == null ? "" : ":" + d.Message)));
    private static CheckMetric Metric(CheckResult r, string key) => r.Details.Metrics.Single(m => m.Key == key);
    private static GPC.Model.Models.Model SimplySupported(double l, int elements, double total, double permanent, double settleStart = 0, double settleEnd = 0)
    {
        var x = Stations(l, elements); var ends = new Dictionary<int, double> { [0] = settleStart, [elements] = settleEnd };
        return Frame(l, elements, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["SLE-R"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, total, ends, None)),
            ["G"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, permanent, ends, None)),
            ["Q"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, total - permanent, new Dictionary<int, double> { [0] = 0, [elements] = 0 }, None)) });
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SimplySupportedFeBeamGivesFiveOver384AndStaysCurrentUntilADisplacementChanges(bool ntc)
    {
        // q = 10 N/mm (G 6 + Q 4), L = 6000: delta = 5 q L^4 / (384 EI) = 6.48e16 / 6.7382784e15 = 9.61670 mm; L/250 = 24 -> u = 0.400696.
        // delta_2 = total minus G = 0.4 delta = 3.84668 mm; L/300 = 20 -> u = 0.192334.
        double l = 6000, delta = 5 * 10 * Math.Pow(l, 4) / (384 * E * I1);
        Assert.AreEqual(9.61670, delta, 5e-6);
        var model = SimplySupported(l, 8, -10, -6);
        var plan = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic,
            MemberVariableDeflectionBasis.Difference, "G"), State("SLE-R", CombinationCategory.Characteristic));
        var (report, row, request) = Run(model, Configure(plan, DeflectionParameters(), ntc));
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        Assert.AreEqual(CheckScope.PhysicalMember, row.Scope); Assert.AreEqual(CheckMechanism.Serviceability, row.Mechanism);
        Assert.AreEqual(SteelMemberServiceabilityChecker.EngineId, row.MethodId);
        Assert.AreEqual(delta / 24, row.Utilization!.Value, 1e-11);
        var max = Metric(row, "Deflection.Max");
        Assert.AreEqual(delta, max.Demand!.Value, 1e-10); Assert.AreEqual(24, max.Capacity!.Value, 1e-12); Assert.AreEqual("mm", max.Unit);
        StringAssert.Contains(max.Reference, ntc ? "NTC 2018 4.2.4.2.1" : "EN 1993-1-1:2005 7.2.1"); StringAssert.Contains(max.Reference, "L/250");
        var variable = Metric(row, "Deflection.Variable");
        Assert.AreEqual(.4 * delta, variable.Demand!.Value, 1e-10); Assert.AreEqual(20, variable.Capacity!.Value, 1e-12);
        Assert.AreEqual(3000, row.Details.Points.Single(p => p.Key == "Deflection.Max").Z, 1e-12);
        Assert.AreEqual("Node 5", row.Details.Trace.Single(t => t.Key == "Deflection.Total.Location").Expression);
        Assert.AreEqual("Characteristic", ((ServiceabilityCheckDetails)row.Details).CombinationCategory);
        Assert.AreEqual(9, row.MemberInput.Deformation.Total.Count); Assert.AreEqual(9, row.MemberInput.Deformation.Variable.Count);
        Assert.AreEqual(ntc ? "2018" : "2005", row.Standard.Edition);
        // Archive and currency: a displacement-only change of the task state invalidates the stored outcome, its restoration does not.
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var loaded = CheckReportArchive.Load(stream);
        Assert.IsTrue(loaded.Single().Results.Single().HasUnchangedEvidence);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(loaded, model, request));
        var sample = ResultQueries.Samples<NodeResultDisplacement>(model.NodesElements[5], State("SLE-R")).Single();
        double old = sample.ResultDisplacement.D3; sample.ResultDisplacement.D3 = old - .1;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(loaded, model, request));
        sample.ResultDisplacement.D3 = old;
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(loaded, model, request));
        // The deducted state is an input too.
        var g = ResultQueries.Samples<NodeResultDisplacement>(model.NodesElements[4], State("G")).Single(); g.ResultDisplacement.D3 += .1;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(loaded, model, request));
    }

    [TestMethod]
    public void SettledSupportsUseTheChordAndThePrecamberActsAtTheGoverningNode()
    {
        // Supports settle -4 and -10 mm (statically determinate: no internal forces); the chord removes the rigid-body line.
        // Precamber +5 mm (upward, member axis 2 = +Z): delta_max = |-9.61670 + 5| = 4.61670 mm.
        double l = 6000, delta = 5 * 10 * Math.Pow(l, 4) / (384 * E * I1);
        var model = SimplySupported(l, 8, -10, -6, -4, -10);
        var parameters = DeflectionParameters(); parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        parameters["Deflection.Precamber"] = "5"; parameters["Deflection.PrecamberSource"] = "Test: fabrication camber 5 mm at mid-span";
        var row = Run(model, Configure(Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic),
            State("SLE-R", CombinationCategory.Characteristic)), parameters, false)).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        Assert.AreEqual(delta - 5, Metric(row, "Deflection.Max").Demand!.Value, 1e-9);
        Assert.AreEqual(-delta, row.Details.Trace.Single(t => t.Key == "Deflection.Total").Value!.Value, 1e-9);
        Assert.AreEqual(-4, row.Details.Trace.Single(t => t.Key == "Deflection.ReferenceAtStart").Value!.Value, 1e-9);
        Assert.AreEqual(-10, row.Details.Trace.Single(t => t.Key == "Deflection.ReferenceAtEnd").Value!.Value, 1e-9);
        Assert.AreEqual(1, row.Details.Metrics.Count);
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void CantileverTipIsCheckedAgainstTheDeclaredReferenceLength(bool supportAtStart)
    {
        // Lc = 2000, q = 5 N/mm, clamp settled by 3 mm: tip = q L^4 / (8 EI) = 8e13 / 1.403808e14 = 0.5698785 mm relative to the support;
        // reference length 2 Lc (declared): 4000/250 = 16 mm -> u = 0.0356174.
        double l = 2000, tip = 5 * Math.Pow(l, 4) / (8 * E * I1);
        Assert.AreEqual(.5698785, tip, 5e-8);
        int support = supportAtStart ? 0 : 4;
        var fe = PlaneBeamFe.Solve(Stations(l, 4), E * I1, -5, new Dictionary<int, double> { [support] = -3 }, new Dictionary<int, double> { [support] = 0 });
        var model = Frame(l, 4, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> { ["SLE-R"] = Horizontal(fe) }, reverseSecond: true);
        var parameters = DeflectionParameters(supportAtStart ? "CantileverAtStart" : "CantileverAtEnd");
        parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        var plan = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic), State("SLE-R", CombinationCategory.Characteristic));
        var missing = Run(model, Configure(plan, parameters, true)).Row;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, missing.Outcome); Assert.AreEqual(DataStatus.Insufficient, missing.Data);
        Assert.IsTrue(missing.Diagnostics.Any(d => d.Code == "SteelServiceability.MissingCantileverReference"), Codes(missing));
        parameters["Deflection.CantileverLengthFactor"] = "2"; parameters["Deflection.CantileverLengthSource"] = "Declared: limit of a cantilever referred to twice its length";
        var row = Run(model, Configure(plan, parameters, true)).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        var metric = Metric(row, "Deflection.Max");
        Assert.AreEqual(tip, metric.Demand!.Value, 1e-11); Assert.AreEqual(16, metric.Capacity!.Value, 1e-12);
        Assert.AreEqual(supportAtStart ? l : 0, row.Details.Points.Single().Z, 1e-12);
        Assert.AreEqual(4000, row.Details.Trace.Single(t => t.Key == "Deflection.ReferenceLength").Value!.Value, 1e-12);
    }

    [TestMethod]
    public void ColumnDriftIsTheRelativeDisplacementOfItsEnds()
    {
        // Column H = 3500 along Z, base clamped and translated by 2 mm (X) and 1 mm (Y); top loads 10 kN along X and 1 kN along Y.
        // Axis 1 = X: Delta1 = P H^3 / (3 E I1) = 4.2875e14 / 5.26428e13 = 8.14451 mm; axis 2 = Y: 1e3 H^3 / (3 E I2) = 4.2875e13 / 3.8052e12 = 11.26748 mm.
        // h/300 = 11.66667 mm: axis 1 u = 0.698101; magnitude sqrt(8.14451^2 + 11.26748^2) = 13.90285 mm, u = 1.191673 (not satisfied).
        double h = 3500, d1 = 1e4 * Math.Pow(h, 3) / (3 * E * I1), d2 = 1e3 * Math.Pow(h, 3) / (3 * E * I2);
        Assert.AreEqual(8.14451, d1, 5e-6); Assert.AreEqual(11.26748, d2, 5e-6);
        var x = Stations(h, 2); var clamp = new Dictionary<int, double> { [0] = 0 };
        var u1 = PlaneBeamFe.Solve(x, E * I1, 0, new Dictionary<int, double> { [0] = 2 }, clamp, new Dictionary<int, double> { [2] = 1e4 });
        var u2 = PlaneBeamFe.Solve(x, E * I2, 0, new Dictionary<int, double> { [0] = 1 }, clamp, new Dictionary<int, double> { [2] = 1e3 });
        // Column along Z with section axis 1 = X: member V1 = X, V2 = Y; rotations ry = du1/dz, rx = -du2/dz.
        var states = new Dictionary<string, double[][]> { ["SLE-R"] = Enumerable.Range(0, 3).Select(i => new[] { u1.V[i], u2.V[i], 0, -u2.Theta[i], u1.Theta[i], 0 }).ToArray() };
        var model = Frame(h, 2, new Vector3d(0, 0, 1), new Vector3d(1, 0, 0), states);
        var parameters = new Dictionary<string, string> { ["Drift.Ratio"] = "300", ["Drift.Source"] = "Declared storey drift h/300" };
        foreach (var (direction, demand, outcome) in new[] { (MemberDeformationDirection.Axis1, d1, EngineeringOutcome.Satisfied),
            (MemberDeformationDirection.Axis2, d2, EngineeringOutcome.Satisfied), (MemberDeformationDirection.Transverse, Math.Sqrt(d1 * d1 + d2 * d2), EngineeringOutcome.NotSatisfied) })
        {
            var row = Run(model, Configure(Plan(Task(MemberDeformationCriterion.HorizontalDisplacement, direction, CombinationCategory.Characteristic), State("SLE-R", CombinationCategory.Characteristic)), parameters, direction == MemberDeformationDirection.Axis2)).Row;
            Assert.AreEqual(outcome, row.Outcome, direction + " " + Codes(row));
            var metric = Metric(row, "Drift");
            Assert.AreEqual(demand, metric.Demand!.Value, 1e-9, direction.ToString()); Assert.AreEqual(h / 300, metric.Capacity!.Value, 1e-12);
            Assert.AreEqual(demand / (h / 300), row.Utilization!.Value, 1e-11);
            StringAssert.Contains(metric.Reference, direction == MemberDeformationDirection.Axis2 ? "NTC 2018 4.2.4.2.2" : "EN 1993-1-1:2005 7.2.2");
            Assert.AreEqual(0, row.Details.Points.Count);
        }
        Assert.AreEqual(13.90285, Math.Sqrt(d1 * d1 + d2 * d2), 5e-6);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void NaturalFrequencyFollowsTheDeclaredMassStateAndDeflectionBasis(bool ntc)
    {
        // Mass state 4 N/mm (weight), L = 6000: delta = 3.84668 mm, f = 17.75/sqrt(delta) = 9.05014 Hz, equal to (pi/2) sqrt(EI g/(q L^4)) with
        // g = 9806.65 mm/s^2 within 3e-6. f_min = 3 Hz: u = 0.331486. Supports settle 1 mm: Total delta = 4.84668, f = 17.75/2.201518 = 8.06262 Hz.
        double l = 6000, delta = 5 * 4 * Math.Pow(l, 4) / (384 * E * I1), exact = Math.PI / 2 * Math.Sqrt(E * I1 * 9806.65 / (4 * Math.Pow(l, 4)));
        var x = Stations(l, 8); var settled = new Dictionary<int, double> { [0] = -1, [8] = -1 };
        var model = Frame(l, 8, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["MASS"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, -4, settled, None)) });
        var parameters = new Dictionary<string, string> { ["Vibration.MinimumFrequency"] = "3", ["Vibration.Source"] = "Project agreement: floors 3 Hz",
            ["Vibration.Scheme"] = "SimplySupported", ["Vibration.SchemeSource"] = "Test: pinned ends, uniform mass", ["Vibration.MassStateSource"] = "Test: MASS = G1 + G2 + psi2 Q as gravity loads",
            ["Vibration.Deflection"] = "RelativeToChord", ["Deformation.CoverageSource"] = "Test: node at mid-span" };
        var plan = Plan(Task(MemberDeformationCriterion.Vibration, MemberDeformationDirection.Axis2, CombinationCategory.QuasiPermanent), State("MASS", CombinationCategory.QuasiPermanent));
        var row = Run(model, Configure(plan, parameters, ntc)).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        var metric = Metric(row, "Vibration.Frequency");
        Assert.AreEqual(17.75 / Math.Sqrt(delta), metric.Capacity!.Value, 1e-9); Assert.AreEqual(exact, metric.Capacity!.Value, 3e-6 * exact);
        Assert.AreEqual(9.05014, metric.Capacity!.Value, 5e-6); Assert.AreEqual(3, metric.Demand!.Value); Assert.AreEqual("Hz", metric.Unit);
        Assert.AreEqual(3 / metric.Capacity!.Value, row.Utilization!.Value, 1e-12);
        Assert.AreEqual(delta, row.Details.Trace.Single(t => t.Key == "Vibration.Deflection").Value!.Value, 1e-9);
        StringAssert.Contains(metric.Reference, ntc ? "NTC 2018 4.2.4.2.3" : "EN 1993-1-1:2005 7.2.3");
        parameters["Vibration.Deflection"] = "Total";
        Assert.AreEqual(17.75 / Math.Sqrt(delta + 1), Metric(Run(model, Configure(plan, parameters, ntc)).Row, "Vibration.Frequency").Capacity!.Value, 1e-9);
        Assert.AreEqual(8.06262, 17.75 / Math.Sqrt(delta + 1), 5e-6);
        // Another scheme only with a qualified coefficient.
        parameters["Vibration.Scheme"] = "DeclaredCoefficient";
        Assert.IsTrue(Run(model, Configure(plan, parameters, ntc)).Row.Diagnostics.Any(d => d.Code == "SteelServiceability.MissingFrequencyCoefficient"));
        parameters["Vibration.Coefficient"] = "18"; parameters["Vibration.CoefficientSource"] = "Test: qualified K for the declared scheme";
        Assert.AreEqual(18 / Math.Sqrt(delta + 1), Metric(Run(model, Configure(plan, parameters, ntc)).Row, "Vibration.Frequency").Capacity!.Value, 1e-9);
        parameters["Vibration.Scheme"] = "Cantilever";
        Assert.ThrowsException<ArgumentException>(() => EngineCatalog.BuiltIn().Compile(Configure(plan, parameters, ntc)));
    }

    [TestMethod]
    public void DirectVariableStateAndSubdividedReversedMembers()
    {
        // Same beam, Q state alone (4 N/mm): delta_2 = 3.84668 mm with the Direct basis; the reversed second element does not change the path.
        double l = 6000, delta = 5 * 10 * Math.Pow(l, 4) / (384 * E * I1);
        var x = Stations(l, 8); var pins = new Dictionary<int, double> { [0] = 0, [8] = 0 };
        var model = Frame(l, 8, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["SLE-R"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, -10, pins, None)), ["Q"] = Horizontal(PlaneBeamFe.Solve(x, E * I1, -4, pins, None)) }, reverseSecond: true);
        var row = Run(model, Configure(Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic,
            MemberVariableDeflectionBasis.Direct, "Q"), State("SLE-R", CombinationCategory.Characteristic)), DeflectionParameters(), false)).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        Assert.AreEqual(.4 * delta, Metric(row, "Deflection.Variable").Demand!.Value, 1e-10);
        CollectionAssert.AreEqual(Enumerable.Range(1, 9).ToArray(), row.MemberInput.Deformation.Total.Select(s => s.NodeId).ToArray());
    }

    [DataTestMethod]
    [DataRow("scheme", "SteelServiceability.MissingDeflectionScheme")]
    [DataRow("coverage", "SteelServiceability.MissingCoverageSource")]
    [DataRow("limits", "SteelServiceability.MissingDeflectionLimit")]
    [DataRow("precamber", "SteelServiceability.MissingPrecamber")]
    [DataRow("precamber source", "SteelServiceability.MissingPrecamberSource")]
    [DataRow("variable limit", "SteelServiceability.MissingVariableDeflectionLimit")]
    [DataRow("variable state", "SteelServiceability.MissingVariableDeflectionState")]
    [DataRow("drift limit", "SteelServiceability.MissingDriftLimit")]
    [DataRow("frequency limit", "SteelServiceability.MissingFrequencyLimit")]
    public void MissingDeclarationsAreMissingInputsNeverDefaultLimits(string problem, string code)
    {
        var model = SimplySupported(6000, 8, -10, -6); var parameters = DeflectionParameters();
        var basis = MemberVariableDeflectionBasis.Difference; var criterion = MemberDeformationCriterion.VerticalDeflection;
        switch (problem)
        {
            case "scheme": parameters.Remove("Deflection.Scheme"); parameters.Remove("Deflection.SchemeSource"); break;
            case "coverage": parameters.Remove("Deformation.CoverageSource"); break;
            case "limits": foreach (var k in new[] { "Deflection.MaxRatio", "Deflection.MaxSource", "Deflection.VariableRatio", "Deflection.VariableSource", "Deflection.Precamber" }) parameters.Remove(k); break;
            case "precamber": parameters.Remove("Deflection.Precamber"); break;
            case "precamber source": parameters["Deflection.Precamber"] = "3"; break;
            case "variable limit": parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource"); break;
            case "variable state": basis = MemberVariableDeflectionBasis.None; break;
            case "drift limit": criterion = MemberDeformationCriterion.HorizontalDisplacement; basis = MemberVariableDeflectionBasis.None; break;
            case "frequency limit": criterion = MemberDeformationCriterion.Vibration; basis = MemberVariableDeflectionBasis.None; break;
        }
        var row = Run(model, Configure(Plan(Task(criterion, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic, basis, basis == MemberVariableDeflectionBasis.None ? null : "G"),
            State("SLE-R", CombinationCategory.Characteristic)), parameters, false)).Row;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, row.Outcome); Assert.AreEqual(DataStatus.Insufficient, row.Data); Assert.IsNull(row.Utilization);
        Assert.IsTrue(row.Diagnostics.Any(d => d.Code == code), Codes(row));
    }

    [TestMethod]
    public void UnqualifiedInputsAreRefused()
    {
        var plan = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic), State("SLE-R", CombinationCategory.Characteristic));
        var parameters = DeflectionParameters(); parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        // One element per span: two end nodes give no interior deflection, which is refused rather than reported as zero.
        var single = Frame(6000, 1, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["SLE-R"] = Horizontal(PlaneBeamFe.Solve(Stations(6000, 1), E * I1, -10, new Dictionary<int, double> { [0] = 0, [1] = 0 }, None)) });
        var row = Run(single, Configure(plan, parameters, false)).Row;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, row.Outcome);
        Assert.IsTrue(row.Diagnostics.Any(d => d.Code == "SteelServiceability.InputDetail" && d.Message == "InteriorDisplacementPointRequired"), Codes(row));
        // Unqualified edition: the engine does not support the task.
        var model = SimplySupported(6000, 8, -10, -6);
        var wrong = Run(model, Configure(plan, parameters, false, "2014")).Row;
        Assert.AreEqual(DataStatus.NotSupported, wrong.Data); Assert.IsTrue(wrong.Diagnostics.Any(d => d.Code == "MemberCheckerUnavailable"));
        // A concrete member is not routed to the steel engine.
        var concrete = Frame(6000, 8, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["SLE-R"] = Horizontal(PlaneBeamFe.Solve(Stations(6000, 8), E * I1, -10, new Dictionary<int, double> { [0] = 0, [8] = 0 }, None)) }, concrete: true);
        Assert.IsTrue(Run(concrete, Configure(plan, parameters, false)).Row.Diagnostics.Any(d => d.Code == "MaterialGlobalCheckerUnavailable"));
        // Missing node results leave the task insufficient before the engine.
        var missing = SimplySupported(6000, 8, -10, -6);
        var registered = missing.NodesElements[3].Results.SelectMany(r => r.Results).OfType<NodeResultDisplacement>().Single(s => s.Case.Name == "SLE-R");
        missing.NodesElements[3].Results.Single(r => r.Results.Contains(registered)).Results.Remove(registered);
        var insufficient = Run(missing, Configure(plan, parameters, false)).Row;
        Assert.AreEqual(DataStatus.Insufficient, insufficient.Data); Assert.IsTrue(insufficient.Diagnostics.Any(d => d.Code == "MissingMemberNodeDisplacement"), Codes(insufficient));
        Assert.AreEqual(8, insufficient.CoverageAssessment.AvailableLocations);
    }

    /// <summary>Rows of engine-only declarations (no member limits, no cantilever reference option): the scenarios of the tests
    /// above. Digest of details (metrics, trace, points), standard context, utilization, statuses and diagnostic codes.</summary>
    private static IEnumerable<(string Name, CheckResult Row)> EngineOnlyRows()
    {
        var supported = SimplySupported(6000, 8, -10, -6);
        var difference = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic,
            MemberVariableDeflectionBasis.Difference, "G"), State("SLE-R", CombinationCategory.Characteristic));
        yield return ("supported-ec3", Run(supported, Configure(difference, DeflectionParameters(), false)).Row);
        yield return ("supported-ntc", Run(supported, Configure(difference, DeflectionParameters(), true)).Row);
        var single = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic), State("SLE-R", CombinationCategory.Characteristic));
        var parameters = DeflectionParameters(); parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        parameters["Deflection.Precamber"] = "5"; parameters["Deflection.PrecamberSource"] = "Test: fabrication camber 5 mm at mid-span";
        yield return ("settled-precamber", Run(SimplySupported(6000, 8, -10, -6, -4, -10), Configure(single, parameters, false)).Row);
        foreach (bool atStart in new[] { true, false })
        {
            var fe = PlaneBeamFe.Solve(Stations(2000, 4), E * I1, -5, new Dictionary<int, double> { [atStart ? 0 : 4] = -3 }, new Dictionary<int, double> { [atStart ? 0 : 4] = 0 });
            var cantilever = Frame(2000, 4, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> { ["SLE-R"] = Horizontal(fe) }, reverseSecond: true);
            parameters = DeflectionParameters(atStart ? "CantileverAtStart" : "CantileverAtEnd"); parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
            if (atStart) yield return ("cantilever-missing", Run(cantilever, Configure(single, parameters, true)).Row);
            parameters["Deflection.CantileverLengthFactor"] = "2"; parameters["Deflection.CantileverLengthSource"] = "Declared: limit of a cantilever referred to twice its length";
            yield return (atStart ? "cantilever-start" : "cantilever-end", Run(cantilever, Configure(single, parameters, true)).Row);
        }
        double h = 3500; var x = Stations(h, 2); var clamp = new Dictionary<int, double> { [0] = 0 };
        var u1 = PlaneBeamFe.Solve(x, E * I1, 0, new Dictionary<int, double> { [0] = 2 }, clamp, new Dictionary<int, double> { [2] = 1e4 });
        var u2 = PlaneBeamFe.Solve(x, E * I2, 0, new Dictionary<int, double> { [0] = 1 }, clamp, new Dictionary<int, double> { [2] = 1e3 });
        var column = Frame(h, 2, new Vector3d(0, 0, 1), new Vector3d(1, 0, 0), new Dictionary<string, double[][]> {
            ["SLE-R"] = Enumerable.Range(0, 3).Select(i => new[] { u1.V[i], u2.V[i], 0, -u2.Theta[i], u1.Theta[i], 0 }).ToArray() });
        var drift = new Dictionary<string, string> { ["Drift.Ratio"] = "300", ["Drift.Source"] = "Declared storey drift h/300" };
        foreach (var direction in new[] { MemberDeformationDirection.Axis1, MemberDeformationDirection.Axis2, MemberDeformationDirection.Transverse })
            yield return ("drift-" + direction, Run(column, Configure(Plan(Task(MemberDeformationCriterion.HorizontalDisplacement, direction, CombinationCategory.Characteristic),
                State("SLE-R", CombinationCategory.Characteristic)), drift, direction == MemberDeformationDirection.Axis2)).Row);
        var mass = Frame(6000, 8, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["MASS"] = Horizontal(PlaneBeamFe.Solve(Stations(6000, 8), E * I1, -4, new Dictionary<int, double> { [0] = -1, [8] = -1 }, None)) });
        var vibration = new Dictionary<string, string> { ["Vibration.MinimumFrequency"] = "3", ["Vibration.Source"] = "Project agreement: floors 3 Hz",
            ["Vibration.Scheme"] = "SimplySupported", ["Vibration.SchemeSource"] = "Test: pinned ends, uniform mass", ["Vibration.MassStateSource"] = "Test: MASS = G1 + G2 + psi2 Q as gravity loads",
            ["Vibration.Deflection"] = "RelativeToChord", ["Deformation.CoverageSource"] = "Test: node at mid-span" };
        var frequency = Plan(Task(MemberDeformationCriterion.Vibration, MemberDeformationDirection.Axis2, CombinationCategory.QuasiPermanent), State("MASS", CombinationCategory.QuasiPermanent));
        yield return ("vibration-chord-ntc", Run(mass, Configure(frequency, vibration, true)).Row);
        vibration["Vibration.Deflection"] = "Total";
        yield return ("vibration-total", Run(mass, Configure(frequency, vibration, false)).Row);
        vibration["Vibration.Scheme"] = "DeclaredCoefficient"; vibration["Vibration.Coefficient"] = "18"; vibration["Vibration.CoefficientSource"] = "Test: qualified K for the declared scheme";
        yield return ("vibration-coefficient", Run(mass, Configure(frequency, vibration, false)).Row);
        parameters = DeflectionParameters(); parameters.Remove("Deflection.Scheme"); parameters.Remove("Deflection.SchemeSource");
        yield return ("missing-scheme", Run(supported, Configure(difference, parameters, false)).Row);
        parameters = DeflectionParameters(); parameters.Remove("Deflection.Precamber");
        yield return ("missing-precamber", Run(supported, Configure(difference, parameters, false)).Row);
    }
    private static string Digest(CheckResult r) => ModelValues.Fingerprint(new object?[] { r.Details, r.Standard, r.Utilization, r.Outcome, r.Data, r.Execution,
        r.Diagnostics.Select(d => d.Code + ":" + d.Message).ToArray() });

    /// <summary>Digests captured on master bc2b3435 (Model 9f40d98a) before the member limits and the cantilever reference option:
    /// without them the rows are bit-identical.</summary>
    [TestMethod]
    public void EngineOnlyDeclarationsKeepTheRowsOfMaster()
    {
        var master = new Dictionary<string, string> {
            ["supported-ec3"] = "599B14F9BCA28B6503FE262B3CB207FDE09A0C87B9AE1E775D9B9CFCC842EE6C",
            ["supported-ntc"] = "ADCA3CFB09A251F7986BB35AF61EC79DB2E331CF581C4BF7BBDF83374AC30710",
            ["settled-precamber"] = "42B35D524F4BD936E19CA561FAC6D850EB82CF032631FC22F4F2899DCE62038F",
            ["cantilever-missing"] = "F598EB4F8E56621A8A45ECCBA14C15BFBC703484E4DAC12C6706220DC93EEB78",
            ["cantilever-start"] = "71EF96482B6BDE8B93F645C9180D6E2C54E6184A3BA8DAFDF483EC4B96387373",
            ["cantilever-end"] = "F4E829FE8306BC5BDCD2C5A0A05E45DB8999CE49F00BD58286FB1CAE75D59E9D",
            ["drift-Axis1"] = "8179A99DD9FE12B97A2E74A0BBAD530CE22E26EA57005066DD92A565EB8CD242",
            ["drift-Axis2"] = "AB0DF75CAE30EBBDD0F8722D16AB2A80EA424C9DDE91011675D4BE3A53F7C927",
            ["drift-Transverse"] = "BA0AA83CD8E90D23E8E37591870EDA6A9E7BB8CA6A161AEC03E9DAA6C0F9A40A",
            ["vibration-chord-ntc"] = "330A997FE9AFFDDC73CEB2055B41A0BE270D46021AC93152615E3642DC9AD570",
            ["vibration-total"] = "0303E67B9C6AA03C9862EBE35276464538AFCADC7FAFC8B1E9EA1970DC45903C",
            ["vibration-coefficient"] = "5346586AF15566A9B01D663C41516859F62ABE6A53E5CA185B3771B8C394176E",
            ["missing-scheme"] = "6BF17DC731156590EE1BFBF4D24781AEF31CFEF6CA8F56B6CB46EC526B1CFF98",
            ["missing-precamber"] = "07D07AAA33CD1C4E0F78FC51D73CE3E7C15991FD6B9F68BB1954A3DBB5258452" };
        var rows = EngineOnlyRows().ToArray();
        Assert.AreEqual(master.Count, rows.Length);
        foreach (var (name, row) in rows) Assert.AreEqual(master[name], Digest(row), name + " " + Codes(row));
    }

    /// <summary>Several straight members in one model, one case SLE-R with the given global nodal displacements (dx, dy, dz, rx, ry,
    /// rz per node, referred to the node). Node and element numbers run over the members.</summary>
    private static GPC.Model.Models.Model Members(params (string Id, Point3d Origin, Vector3d Along, Vector3d Axis1, double Length, int Elements, double[][] Displacements, bool ReverseSecond)[] members)
    {
        var model = new GPC.Model.Models.Model("Steel serviceability: several members, one job");
        var section = new SteelSection(new SectionH(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15), SteelMaterialEN1993Data.S355);
        model.AddProperty(section);
        var nodes = new List<(NodeElement Node, double[] D)>(); int id = 1, element = 100;
        foreach (var m in members)
        {
            int first = id;
            for (int k = 0; k <= m.Elements; k++, id++)
            {
                double s = m.Length * k / m.Elements;
                var node = new NodeElement(new Point3d(m.Origin.X + m.Along.X * s, m.Origin.Y + m.Along.Y * s, m.Origin.Z + m.Along.Z * s), id: id);
                model.NodesElements.Add(node); nodes.Add((node, m.Displacements[k]));
            }
            var parts = new List<BeamMemberPart>();
            for (int e = 0; e < m.Elements; e++, element++)
            {
                bool alongIJ = !(m.ReverseSecond && e == 1);
                var beam = new BeamElement(null, null, section, id: element); model.BeamElements.Add(beam);
                model.ConnectBeam(beam.Id, alongIJ ? first + e : first + e + 1, alongIJ ? first + e + 1 : first + e);
                beam.Assignments.Formulation = BeamFormulation.StraightTwoNode; beam.Assignments.StationDomain = "NodeToNode";
                beam.Assignments.SectionAxes = Axes.Beam(beam.StartPoint, beam.EndPoint, m.Axis1);
                beam.Assignments.SectionGeometryAxes = beam.Assignments.SectionAxes; beam.Assignments.ActionsAtSectionCentroidConfirmed = true;
                beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = section });
                parts.Add(new BeamMemberPart(beam.Id, alongIJ));
            }
            model.PhysicalMembers.Add(m.Id, new PhysicalMemberDefinition(m.Id, parts, "NodeToNode", source: "Test: explicit straight member " + m.Id));
        }
        model.LoadCases.Add(new LoadCaseBase("SLE-R")); var lc = model.LoadCases.Values.Single();
        model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis, "Plane Euler-Bernoulli FE solutions of the test.");
        string fp = model.AnalysisFingerprint();
        model.Datasets.Add("fe", new AnalysisDataset { Id = "fe", Program = "PlaneBeamFe (test)", ModelRevision = "r1", InputFingerprint = fp, NormalizedUnits = "N,mm,rad", IsSynthetic = true, Semantics = AnalysisSemantics.LinearStatic });
        foreach (var (node, d) in nodes)
            node.AddResult(new NodeResult(new List<INodeResultLocation> { new NodeResultDisplacement(lc,
                new ResultDisplacement(ActionTransformations.AtPoint(CoordinateSystem.Global, node.Position), d[0], d[1], d[2], d[3], d[4], d[5])) {
                State = new ResultState { DatasetId = "fe", ModelRevision = "r1", InputFingerprint = fp, Components = Enumerable.Repeat(ComponentAvailability.Available, 6).ToArray(),
                    Semantics = AnalysisSemantics.LinearStatic, ConcomitantStateId = lc.Name, IsCumulative = true, IsSynthetic = true } } }));
        return model;
    }
    private static MemberCheckSpecification MemberTask(string id, MemberDeformationCriterion criterion, MemberDeformationDirection direction)
    { var task = Task(criterion, direction, CombinationCategory.Characteristic); task.MemberId = id; return task; }
    private static void Declare(GPC.Model.Models.Model model, string id, MemberServiceabilityLimits? limits) => model.PhysicalMembers[id] = model.PhysicalMembers[id].WithServiceabilityLimits(limits);
    private static string? Origin(CheckResult row, string key) => row.Details.Trace.SingleOrDefault(t => t.Key == key + ".Origin")?.Expression;
    private static MemberDeclaredValue Value(double value, string source) => new(value, source);

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void MemberValuesOverrideTheEngineAndRecordTheirOrigin(bool ntc)
    {
        // Beam of the first test: delta = 9.61670 mm, delta_2 = 0.4 delta = 3.84668 mm, L = 6000. The member declares L/500 for
        // delta_max: 12 mm, u = 0.801392; delta_2 falls back to the engine L/300: 20 mm, u = 0.192334; scheme and precamber from the engine.
        double l = 6000, delta = 5 * 10 * Math.Pow(l, 4) / (384 * E * I1);
        Assert.AreEqual(.801392, delta / 12, 5e-7);
        var model = SimplySupported(l, 8, -10, -6);
        var plan = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic,
            MemberVariableDeflectionBasis.Difference, "G"), State("SLE-R", CombinationCategory.Characteristic));
        var config = Configure(plan, DeflectionParameters(), ntc);
        Declare(model, "M", new MemberServiceabilityLimits(maxDeflectionRatio: Value(500, "Project agreement: floors under brittle partitions L/500")));
        var (report, row, request) = Run(model, config);
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        var max = Metric(row, "Deflection.Max");
        Assert.AreEqual(delta, max.Demand!.Value, 1e-10); Assert.AreEqual(12, max.Capacity!.Value, 1e-12); Assert.AreEqual(delta / 12, row.Utilization!.Value, 1e-11);
        StringAssert.Contains(max.Reference, "limit: Project agreement: floors under brittle partitions L/500");
        var variable = Metric(row, "Deflection.Variable");
        Assert.AreEqual(.4 * delta, variable.Demand!.Value, 1e-10); Assert.AreEqual(20, variable.Capacity!.Value, 1e-12);
        StringAssert.Contains(variable.Reference, "limit: Project agreement: variable actions L/300");
        Assert.AreEqual(500, row.Details.Trace.Single(t => t.Key == "Deflection.MaxRatio").Value!.Value);
        Assert.AreEqual("Member", Origin(row, "Deflection.MaxRatio")); Assert.AreEqual("Engine", Origin(row, "Deflection.VariableRatio"));
        Assert.AreEqual("Engine", Origin(row, "Deflection.Scheme")); Assert.AreEqual("Engine", Origin(row, "Deflection.Precamber"));
        Assert.AreEqual(500, row.MemberInput.Deformation.ServiceabilityLimits.MaxDeflectionRatio.Value);
        Assert.IsNull(row.MemberInput.Definition.ServiceabilityLimits);
        // Archive and currency: the declaration is evidence of the row; a new value on the member makes the stored outcome NotEvaluated.
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream); stream.Position = 0;
        var loaded = CheckReportArchive.Load(stream);
        Assert.IsTrue(loaded.Single().Results.Single().HasUnchangedEvidence);
        Assert.AreEqual(500, loaded.Single().Results.Single().MemberInput.Deformation.ServiceabilityLimits.MaxDeflectionRatio.Value);
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(loaded, model, request));
        Declare(model, "M", new MemberServiceabilityLimits(maxDeflectionRatio: Value(1000, "Test: L/1000")));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(loaded, model, request));
        // L/1000 = 6 mm: u = 1.602783, not satisfied.
        var stricter = Run(model, config).Row;
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, stricter.Outcome); Assert.AreEqual(delta / 6, stricter.Utilization!.Value, 1e-11);
        // Own scheme and precamber: delta_max = |-9.61670 + 5| = 4.61670 mm against L/500.
        Declare(model, "M", new MemberServiceabilityLimits(MemberDeflectionScheme.SupportedEnds, "Test: pinned to the girders",
            maxDeflectionRatio: Value(500, "Test: L/500"), precamber: Value(5, "Test: camber 5 mm at mid-span")));
        var cambered = Run(model, config).Row;
        Assert.AreEqual(delta - 5, Metric(cambered, "Deflection.Max").Demand!.Value, 1e-9);
        Assert.AreEqual("Member", Origin(cambered, "Deflection.Scheme")); Assert.AreEqual("Member", Origin(cambered, "Deflection.Precamber"));
        Assert.AreEqual("SupportedEnds: Test: pinned to the girders", cambered.Details.Trace.Single(t => t.Key == "Deflection.Scheme").Expression);
        Assert.AreEqual("Test: camber 5 mm at mid-span", cambered.Details.Trace.Single(t => t.Key == "Deflection.PrecamberSource").Expression);
        // Without the member declarations the row is the engine-only row again: no origin entries.
        Declare(model, "M", null);
        var engine = Run(model, config).Row;
        Assert.AreEqual(24, Metric(engine, "Deflection.Max").Capacity!.Value, 1e-12); Assert.IsFalse(engine.Details.Trace.Any(t => t.Key.EndsWith(".Origin")));
        Assert.AreEqual(Digest(EngineOnlyRows().Single(r => r.Name == (ntc ? "supported-ntc" : "supported-ec3")).Row), Digest(engine));
    }

    [TestMethod]
    public void MissingEngineValuesComeFromTheMemberOrStayMissing()
    {
        // The engine declares scheme and coverage only: without member values the limit is a missing input (as before); the member
        // supplies L/250 with an explicit zero precamber: delta = 9.61670 mm, 24 mm, u = 0.400696.
        double delta = 5 * 10 * Math.Pow(6000, 4) / (384 * E * I1);
        var model = SimplySupported(6000, 8, -10, -6);
        var parameters = DeflectionParameters();
        foreach (var k in new[] { "Deflection.MaxRatio", "Deflection.MaxSource", "Deflection.VariableRatio", "Deflection.VariableSource", "Deflection.Precamber" }) parameters.Remove(k);
        var config = Configure(Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic),
            State("SLE-R", CombinationCategory.Characteristic)), parameters, true);
        Assert.IsTrue(Run(model, config).Row.Diagnostics.Any(d => d.Code == "SteelServiceability.MissingDeflectionLimit"));
        Declare(model, "M", new MemberServiceabilityLimits(maxDeflectionRatio: Value(250, "NTC 2018 Tab. 4.2.XII, solai in generale (declared)")));
        Assert.IsTrue(Run(model, config).Row.Diagnostics.Any(d => d.Code == "SteelServiceability.MissingPrecamber"), "the engine has no precamber either");
        Declare(model, "M", new MemberServiceabilityLimits(maxDeflectionRatio: Value(250, "NTC 2018 Tab. 4.2.XII, solai in generale (declared)"), precamber: Value(0, "Test: no camber")));
        var row = Run(model, config).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        Assert.AreEqual(delta / 24, row.Utilization!.Value, 1e-11); Assert.AreEqual(1, row.Details.Metrics.Count);
        Assert.AreEqual("Member", Origin(row, "Deflection.MaxRatio")); Assert.AreEqual("Member", Origin(row, "Deflection.Precamber")); Assert.AreEqual("Engine", Origin(row, "Deflection.Scheme"));
        // Incoherent member values are refused, not ignored.
        Declare(model, "M", new MemberServiceabilityLimits(cantileverLengthFactor: Value(2, "Test"), maxDeflectionRatio: Value(250, "Test"), precamber: Value(0, "Test")));
        var supported = Run(model, config).Row;
        Assert.IsTrue(supported.Diagnostics.Any(d => d.Code == "SteelServiceability.InputDetail" && d.Message == "CantileverReferenceOnSupportedSpan: MemberServiceabilityLimits.CantileverLengthFactor"), Codes(supported));
        var variableOnly = new Dictionary<string, string>(parameters) { ["Deflection.VariableRatio"] = "300", ["Deflection.VariableSource"] = "Test: L/300" };
        Declare(model, "M", new MemberServiceabilityLimits(precamber: Value(5, "Test: camber")));
        var precamber = Run(model, Configure(Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic,
            MemberVariableDeflectionBasis.Difference, "G"), State("SLE-R", CombinationCategory.Characteristic)), variableOnly, true)).Row;
        Assert.IsTrue(precamber.Diagnostics.Any(d => d.Code == "SteelServiceability.InputDetail" && d.Message == "PrecamberWithoutMaximumDeflection: MemberServiceabilityLimits.Precamber"), Codes(precamber));
    }

    [TestMethod]
    public void MixedMembersShareOneEngineAndOneJob()
    {
        // NTC 2018, one engine: SupportedEnds with L/250 and zero precamber, the NTC cantilever reference, drift h/300.
        // F floor beam, no declarations: delta = 9.61670 mm, L/250 = 24, u = 0.400696.
        // R roof beam, member L/200: 30 mm, u = 0.320557.
        // C cantilever clamped at its end (member scheme), Lc = 2000, q = 5 N/mm: tip = q Lc^4/(8 EI) = 0.5698785 mm relative to the
        //   settled clamp; NTC reference 2 Lc = 4000, /250 = 16 mm, u = 0.0356174.
        // P1 column, no declarations: H = 3500, 10 kN at the top: Delta1 = P H^3/(3 E I1) = 8.14451 mm, h/300 = 11.66667, u = 0.698101.
        // P2 column, member h/500 = 7 mm: u = 8.14451/7 = 1.163502, not satisfied.
        double l = 6000, delta = 5 * 10 * Math.Pow(l, 4) / (384 * E * I1), tip = 5 * Math.Pow(2000, 4) / (8 * E * I1), h = 3500, d1 = 1e4 * Math.Pow(h, 3) / (3 * E * I1);
        Assert.AreEqual(.320557, delta / 30, 5e-7); Assert.AreEqual(.0356174, tip / 16, 5e-8); Assert.AreEqual(1.163502, d1 / 7, 5e-7);
        var span = Horizontal(PlaneBeamFe.Solve(Stations(l, 8), E * I1, -10, new Dictionary<int, double> { [0] = 0, [8] = 0 }, None));
        var cantilever = Horizontal(PlaneBeamFe.Solve(Stations(2000, 4), E * I1, -5, new Dictionary<int, double> { [4] = -3 }, new Dictionary<int, double> { [4] = 0 }));
        var u1 = PlaneBeamFe.Solve(Stations(h, 2), E * I1, 0, new Dictionary<int, double> { [0] = 2 }, new Dictionary<int, double> { [0] = 0 }, new Dictionary<int, double> { [2] = 1e4 });
        var column = Enumerable.Range(0, 3).Select(i => new[] { u1.V[i], 0, 0, 0, u1.Theta[i], 0 }).ToArray();
        Vector3d x = new(1, 0, 0), y = new(0, 1, 0), z = new(0, 0, 1);
        var model = Members(("F", new Point3d(0, 0, 0), x, y, l, 8, span, false), ("R", new Point3d(0, 5000, 0), x, y, l, 8, span, true),
            ("C", new Point3d(0, 10000, 0), x, y, 2000, 4, cantilever, true), ("P1", new Point3d(0, 20000, 0), z, x, h, 2, column, false),
            ("P2", new Point3d(5000, 20000, 0), z, x, h, 2, column, false));
        Declare(model, "R", new MemberServiceabilityLimits(maxDeflectionRatio: Value(200, "NTC 2018 Tab. 4.2.XII, coperture in generale (declared)")));
        Declare(model, "C", new MemberServiceabilityLimits(MemberDeflectionScheme.CantileverAtEnd, "Test: clamped at the column, free at the span start"));
        Declare(model, "P2", new MemberServiceabilityLimits(driftRatio: Value(500, "Project agreement: storeys with brittle partitions h/500")));
        var parameters = DeflectionParameters(); parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        parameters["Deflection.CantileverReference"] = "Ntc2018"; parameters["Drift.Ratio"] = "300"; parameters["Drift.Source"] = "Project agreement: storeys h/300";
        Assert.AreEqual(0, SteelParameterSchemas.MemberServiceability.Validate(parameters).Count);
        var plan = new BeamCheckPlanRequest { MemberIds = new[] { "F", "R", "C", "P1", "P2" }, SectionMechanisms = Array.Empty<CheckMechanism>(),
            Results = new[] { State("SLE-R", CombinationCategory.Characteristic) },
            MemberChecks = new[] { "F", "R", "C" }.Select(m => MemberTask(m, MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2))
                .Concat(new[] { "P1", "P2" }.Select(m => MemberTask(m, MemberDeformationCriterion.HorizontalDisplacement, MemberDeformationDirection.Axis1))).ToArray() };
        var request = EngineCatalog.BuiltIn().Compile(Configure(plan, parameters, true));
        var report = new Service().Verify(model, request);
        var rows = report.Jobs.Single().Results.ToDictionary(r => r.Target.MemberId);
        Assert.AreEqual(5, rows.Count);
        foreach (var (member, metric, demand, capacity) in new[] { ("F", "Deflection.Max", delta, 24d), ("R", "Deflection.Max", delta, 30d), ("C", "Deflection.Max", tip, 16d),
            ("P1", "Drift", d1, h / 300), ("P2", "Drift", d1, 7d) })
        {
            var row = rows[member];
            Assert.AreEqual(demand / capacity > 1 ? EngineeringOutcome.NotSatisfied : EngineeringOutcome.Satisfied, row.Outcome, member + " " + Codes(row));
            Assert.AreEqual(demand, Metric(row, metric).Demand!.Value, 1e-9, member); Assert.AreEqual(capacity, Metric(row, metric).Capacity!.Value, 1e-9, member);
            Assert.AreEqual(demand / capacity, row.Utilization!.Value, 1e-9, member);
        }
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, rows["P2"].Outcome);
        // Origins: none for the members without declarations, member or engine value by value for the others.
        Assert.IsFalse(rows["F"].Details.Trace.Concat(rows["P1"].Details.Trace).Any(t => t.Key.EndsWith(".Origin")));
        Assert.AreEqual("Member", Origin(rows["R"], "Deflection.MaxRatio")); Assert.AreEqual("Engine", Origin(rows["R"], "Deflection.Scheme"));
        Assert.AreEqual("Member", Origin(rows["C"], "Deflection.Scheme")); Assert.AreEqual("Engine", Origin(rows["C"], "Deflection.CantileverLengthFactor"));
        Assert.AreEqual("Engine", Origin(rows["C"], "Deflection.MaxRatio"));
        var factor = rows["C"].Details.Trace.Single(t => t.Key == "Deflection.CantileverLengthFactor");
        Assert.AreEqual(2, factor.Value!.Value); StringAssert.Contains(factor.Expression, "NTC 2018 4.2.4.2.1"); StringAssert.Contains(factor.Expression, "il doppio dello sbalzo");
        Assert.AreEqual(4000, rows["C"].Details.Trace.Single(t => t.Key == "Deflection.ReferenceLength").Value!.Value, 1e-12);
        Assert.AreEqual(0, rows["C"].Details.Points.Single().Z, 1e-12);
        Assert.AreEqual("Member", Origin(rows["P2"], "Drift.Ratio")); StringAssert.Contains(Metric(rows["P2"], "Drift").Reference, "limit: Project agreement: storeys with brittle partitions h/500");
        StringAssert.Contains(Metric(rows["P1"], "Drift").Reference, "limit: Project agreement: storeys h/300");
        Assert.IsFalse(rows["R"].Details.Trace.Any(t => t.Key == "Deflection.CantileverLengthFactor"));
        // Editing the declarations of one member makes the stored job NotEvaluated; the values it was checked with restore its outcome.
        Declare(model, "R", new MemberServiceabilityLimits(maxDeflectionRatio: Value(250, "Test: L/250")));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report.Jobs, model, request));
        Declare(model, "R", new MemberServiceabilityLimits(maxDeflectionRatio: Value(200, "NTC 2018 Tab. 4.2.XII, coperture in generale (declared)")));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, Service.CurrentOutcome(report.Jobs, model, request));
    }

    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void CantileverReferenceOptionCitesNtc2018AndIsNeverAssumed(bool supportAtStart)
    {
        // Cantilever of CantileverTipIsCheckedAgainstTheDeclaredReferenceLength: tip = 0.5698785 mm. Deflection.CantileverReference =
        // Ntc2018: L = 2 Lc = 4000, L/250 = 16 mm, u = 0.0356174 (NTC 2018 4.2.4.2.1, "nel caso di mensole, il doppio dello sbalzo").
        // A member factor 1 wins: L = Lc = 2000, 8 mm, u = 0.0712348.
        double l = 2000, tip = 5 * Math.Pow(l, 4) / (8 * E * I1);
        Assert.AreEqual(.0712348, tip / 8, 5e-8);
        int support = supportAtStart ? 0 : 4;
        var fe = PlaneBeamFe.Solve(Stations(l, 4), E * I1, -5, new Dictionary<int, double> { [support] = -3 }, new Dictionary<int, double> { [support] = 0 });
        var model = Frame(l, 4, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> { ["SLE-R"] = Horizontal(fe) }, reverseSecond: true);
        var parameters = DeflectionParameters(supportAtStart ? "CantileverAtStart" : "CantileverAtEnd");
        parameters.Remove("Deflection.VariableRatio"); parameters.Remove("Deflection.VariableSource");
        var plan = Plan(Task(MemberDeformationCriterion.VerticalDeflection, MemberDeformationDirection.Axis2, CombinationCategory.Characteristic), State("SLE-R", CombinationCategory.Characteristic));
        // Not declared: the cantilever stays a missing input, as before.
        Assert.IsTrue(Run(model, Configure(plan, parameters, true)).Row.Diagnostics.Any(d => d.Code == "SteelServiceability.MissingCantileverReference"));
        parameters["Deflection.CantileverReference"] = "Ntc2018";
        var schema = SteelParameterSchemas.MemberServiceability;
        Assert.AreEqual(0, schema.Validate(parameters).Count, string.Join(";", schema.Validate(parameters).Select(i => i.Code)));
        var row = Run(model, Configure(plan, parameters, true)).Row;
        Assert.AreEqual(EngineeringOutcome.Satisfied, row.Outcome, Codes(row));
        Assert.AreEqual(tip, Metric(row, "Deflection.Max").Demand!.Value, 1e-11); Assert.AreEqual(16, Metric(row, "Deflection.Max").Capacity!.Value, 1e-12);
        Assert.AreEqual(4000, row.Details.Trace.Single(t => t.Key == "Deflection.ReferenceLength").Value!.Value, 1e-12);
        var factor = row.Details.Trace.Single(t => t.Key == "Deflection.CantileverLengthFactor");
        Assert.AreEqual(2, factor.Value!.Value); StringAssert.StartsWith(factor.Expression, "NTC 2018 4.2.4.2.1"); StringAssert.Contains(factor.Expression, "S.O. n. 8");
        Assert.IsFalse(row.Details.Trace.Any(t => t.Key.EndsWith(".Origin")));
        // A factor declared on the member wins over the option.
        Declare(model, "M", new MemberServiceabilityLimits(cantileverLengthFactor: Value(1, "Test: limit referred to the cantilever length")));
        var own = Run(model, Configure(plan, parameters, true)).Row;
        Assert.AreEqual(8, Metric(own, "Deflection.Max").Capacity!.Value, 1e-12); Assert.AreEqual(tip / 8, own.Utilization!.Value, 1e-11);
        Assert.AreEqual("Member", Origin(own, "Deflection.CantileverLengthFactor"));
        Declare(model, "M", null);
        // The option cites NTC 2018: refused for EN 1993-1-1 engines; it excludes an engine factor.
        StringAssert.StartsWith(Assert.ThrowsException<ArgumentException>(() => EngineCatalog.BuiltIn().Compile(Configure(plan, parameters, false))).Message, "CantileverReferenceIsNtc2018");
        parameters["Deflection.CantileverLengthFactor"] = "2"; parameters["Deflection.CantileverLengthSource"] = "Test";
        Assert.IsTrue(schema.Validate(parameters).Any(i => i.Code == "CantileverReferenceConflict"));
        var conflict = Run(model, Configure(plan, parameters, true)).Row;
        Assert.IsTrue(conflict.Diagnostics.Any(d => d.Code == "SteelServiceability.InputDetail" && d.Message!.StartsWith("CantileverReferenceConflict")), Codes(conflict));
        Assert.ThrowsException<ArgumentException>(() => new SteelMemberServiceabilityChecker(new StandardNTC2018Steel(), "2018", parameters: new Dictionary<string, string> { ["Deflection.CantileverReference"] = "2" }));
    }

    [TestMethod]
    public void MemberFrequencyLimitOverridesTheEngine()
    {
        // Mass state of the frequency test: f = 17.75/sqrt(3.84668) = 9.05014 Hz. The member declares 10 Hz: u = 10/9.05014 = 1.104955.
        double delta = 5 * 4 * Math.Pow(6000, 4) / (384 * E * I1), f = 17.75 / Math.Sqrt(delta);
        Assert.AreEqual(1.104955, 10 / f, 5e-7);
        var model = Frame(6000, 8, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Dictionary<string, double[][]> {
            ["MASS"] = Horizontal(PlaneBeamFe.Solve(Stations(6000, 8), E * I1, -4, new Dictionary<int, double> { [0] = 0, [8] = 0 }, None)) });
        var parameters = new Dictionary<string, string> { ["Vibration.MinimumFrequency"] = "3", ["Vibration.Source"] = "Project agreement: floors 3 Hz",
            ["Vibration.Scheme"] = "SimplySupported", ["Vibration.SchemeSource"] = "Test: pinned ends, uniform mass", ["Vibration.MassStateSource"] = "Test: MASS = G1 + G2 + psi2 Q as gravity loads",
            ["Vibration.Deflection"] = "RelativeToChord", ["Deformation.CoverageSource"] = "Test: node at mid-span" };
        var config = Configure(Plan(Task(MemberDeformationCriterion.Vibration, MemberDeformationDirection.Axis2, CombinationCategory.QuasiPermanent),
            State("MASS", CombinationCategory.QuasiPermanent)), parameters, true);
        // Deflection and drift declarations of the member do not concern a frequency task.
        Declare(model, "M", new MemberServiceabilityLimits(maxDeflectionRatio: Value(500, "Test"), driftRatio: Value(500, "Test")));
        var engine = Run(model, config).Row;
        Assert.AreEqual(3 / f, engine.Utilization!.Value, 1e-11); Assert.AreEqual("Engine", Origin(engine, "Vibration.MinimumFrequency"));
        Declare(model, "M", new MemberServiceabilityLimits(minimumFrequency: Value(10, "Project agreement: gymnasium floor 10 Hz")));
        var row = Run(model, config).Row;
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, row.Outcome, Codes(row));
        var metric = Metric(row, "Vibration.Frequency");
        Assert.AreEqual(10, metric.Demand!.Value); Assert.AreEqual(f, metric.Capacity!.Value, 1e-9); Assert.AreEqual(10 / f, row.Utilization!.Value, 1e-11);
        StringAssert.Contains(metric.Reference, "Project agreement: gymnasium floor 10 Hz"); Assert.AreEqual("Member", Origin(row, "Vibration.MinimumFrequency"));
    }

    [TestMethod]
    public void ParameterSchemaDescribesTheDeclaredVocabulary()
    {
        var catalog = EngineCatalog.BuiltIn();
        Assert.AreSame(SteelParameterSchemas.MemberServiceability, catalog.Capabilities.Single(c => c.Kind == SteelMemberServiceabilityChecker.EngineId).ParameterSchema);
        var schema = SteelParameterSchemas.MemberServiceability;
        Assert.AreEqual(0, schema.Validate(DeflectionParameters()).Count, string.Join(";", schema.Validate(DeflectionParameters()).Select(i => i.Code + ":" + i.Key)));
        Assert.IsTrue(schema.ExternalRequirements.Any(r => r.Contains("Tab. 4.2.XII")));
        bool Issue(Dictionary<string, string> values, string code) => schema.Validate(values).Any(i => i.Code == code);
        Assert.IsTrue(Issue(new Dictionary<string, string>(), "ServiceabilityCriterionRequired"));
        var values = DeflectionParameters(); values.Remove("Deflection.Precamber"); Assert.IsTrue(Issue(values, "PrecamberRequired"));
        values = DeflectionParameters(); values["Deflection.Precamber"] = "4"; Assert.IsTrue(Issue(values, "PrecamberSourceRequired"));
        values = DeflectionParameters("CantileverAtStart"); Assert.IsTrue(Issue(values, "CantileverReferenceRequired"));
        values = DeflectionParameters(); values["Deflection.CantileverLengthFactor"] = "2"; values["Deflection.CantileverLengthSource"] = "s"; Assert.IsTrue(Issue(values, "CantileverReferenceOnSupportedSpan"));
        values = DeflectionParameters(); values.Remove("Deformation.CoverageSource"); Assert.IsTrue(Issue(values, "DeflectionSchemeRequired"));
        values = new Dictionary<string, string> { ["Vibration.MinimumFrequency"] = "3", ["Vibration.Source"] = "s", ["Vibration.Scheme"] = "SimplySupported", ["Vibration.Coefficient"] = "18" };
        Assert.IsTrue(Issue(values, "VibrationModelRequired")); Assert.IsTrue(Issue(values, "CoefficientOnSimplySupported")); Assert.IsTrue(Issue(values, "VibrationCoefficientPair"));
        values = new Dictionary<string, string> { ["Drift.Ratio"] = "-300" }; Assert.IsTrue(schema.Validate(values).Any(i => i.Key == "Drift.Ratio" && i.Code == "InvalidParameterValue"));
        Assert.IsTrue(Issue(values, "DriftPair"));
        Assert.ThrowsException<ArgumentException>(() => new SteelMemberServiceabilityChecker(new StandardEN1993p11(), "2005", parameters: new Dictionary<string, string> { ["Deflection.MaxRatio"] = "L/250" }));
        Assert.ThrowsException<ArgumentException>(() => new SteelMemberServiceabilityChecker(new StandardEN1993p11(), "2005", parameters: new Dictionary<string, string> { ["AnalysisMode"] = "Bending" }));
    }
}
