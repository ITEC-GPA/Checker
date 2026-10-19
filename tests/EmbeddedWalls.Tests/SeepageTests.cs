using GPC.Model.Geotechnics.Seepage;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.EmbeddedWalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public class SeepageTests
{
    // Expected solutions come from Darcy's law and 1D layers in series, not another FE run.
    internal static SeepageModel Rectangle(int nx, int nz, double width = 1000, double height = 1000)
    {
        var nodes = new List<SeepageNode>(); var triangles = new List<SeepageTriangle>();
        for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++) nodes.Add(new(i * width / nx, j * height / nz));
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
        {
            int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
            triangles.Add(new(a, b, d)); triangles.Add(new(a, d, c));
        }
        return new() { CaseId = "independent", Nodes = nodes, Triangles = triangles,
            Materials = [new("given", .01, .01, 0, "synthetic benchmark")] };
    }

    [TestMethod]
    public void ConfinedDarcyLinearHeadAndFluxAreExact()
    {
        var m = Rectangle(8, 6); m.Heads = m.Nodes.Select((p, i) => (p, i)).Where(x => x.p.X == 0 || x.p.X == 1000)
            .Select(x => new SeepageHead(x.i, 2000 - x.p.X)).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        foreach (var n in r.Nodes) Assert.AreEqual(2000 - n.X, n.Head, 1e-6);
        foreach (var e in r.Elements) { Assert.AreEqual(.01, e.FluxX, 1e-9); Assert.AreEqual(0, e.FluxZ, 1e-9); }
        Assert.AreEqual(10, r.Inflow, 1e-7); Assert.AreEqual(10, r.Outflow, 1e-7); Assert.AreEqual(0, r.Balance, 1e-7);
    }

    [TestMethod]
    public void RotatedAnisotropyPassesLinearPatch()
    {
        var m = Rectangle(7, 5); m.Materials = [new("tensor", .04, .02, .01, "given tensor")];
        m.Heads = m.Nodes.Select((p, i) => new SeepageHead(i, 3000 - .5 * p.X - .2 * p.Z))
            .Where(h => m.Nodes[h.Node].X is 0 or 1000 || m.Nodes[h.Node].Z is 0 or 1000).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        foreach (var e in r.Elements) { Assert.AreEqual(.022, e.FluxX, 1e-9); Assert.AreEqual(.009, e.FluxZ, 1e-9); }
    }

    [TestMethod]
    public void LayersInSeriesUseHarmonicConductivity()
    {
        var m = Rectangle(6, 8); m.Materials = [new("lower", .01, .01, 0, "given"), new("upper", .04, .04, 0, "given")];
        m.Triangles = m.Triangles.Select(t => new SeepageTriangle(t.A, t.B, t.C,
            (m.Nodes[t.A].Z + m.Nodes[t.B].Z + m.Nodes[t.C].Z) / 3 < 500 ? 0 : 1)).ToArray();
        m.Heads = m.Nodes.Select((p, i) => (p, i)).Where(x => x.p.Z is 0 or 1000).Select(x => new SeepageHead(x.i, x.p.Z == 0 ? 2000 : 1000)).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        // q=Δh/(L1/k1+L2/k2)=1000/(50000+12500)=0.016 mm/s upwards.
        foreach (var e in r.Elements) Assert.AreEqual(.016, e.FluxZ, 1e-8);
        foreach (var n in r.Nodes.Where(n => n.Z == 500)) Assert.AreEqual(1200, n.Head, 1e-5);
        Assert.AreEqual(16, r.Outflow, 1e-6);
    }

    [TestMethod]
    public void NeumannBoundaryUsesEdgeLengthAndNodeWeights()
    {
        var m = Rectangle(4, 4); m.Heads = m.Nodes.Select((p, i) => (p, i)).Where(x => x.p.X == 1000).Select(x => new SeepageHead(x.i, 1000)).ToArray();
        m.Fluxes = Enumerable.Range(0, 4).Select(j => new SeepageFlux(j * 5, (j + 1) * 5, .005)).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        foreach (var n in r.Nodes) Assert.AreEqual(1500 - .5 * n.X, n.Head, 1e-6);
        Assert.AreEqual(5, r.Inflow, 1e-7); Assert.AreEqual(5, r.Outflow, 1e-7);
    }

    [TestMethod]
    public void HydrostaticUnsaturatedDomainHasZeroFlowAndNegativePressure()
    {
        var m = Rectangle(6, 8); m.Materials = [new("vg", .01, .01, 0, "given VG", new(.002, 2))];
        m.Heads = Enumerable.Range(0, 7).Select(i => new SeepageHead(i, 250)).ToArray();
        m.InitialHeads = m.Nodes.Select(p => 250.0).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        foreach (var n in r.Nodes) Assert.AreEqual(250, n.Head, 1e-7);
        Assert.AreEqual(0, r.Inflow, 1e-8);
        Assert.AreEqual(-.0073575, r.Nodes.Last().Pressure, 1e-10);
        // α|ψ|=1, n=2: Se=1/√2, kr=√Se (1−1/√2)^2.
        var c = UnsaturatedConductivity.At(new(.002, 2), -500);
        Assert.AreEqual(1 / Math.Sqrt(2), c.EffectiveSaturation, 1e-12);
        Assert.AreEqual(Math.Pow(2, -.25) * Math.Pow(1 - 1 / Math.Sqrt(2), 2), c.RelativeConductivity, 1e-12);
    }

    [TestMethod]
    public void NonlinearUniformPressureHasAnalyticalGravityFlow()
    {
        var m = Rectangle(4, 12); m.Materials = [new("vg", .01, .01, 0, "given VG", new(.002, 2))];
        m.Heads = m.Nodes.Select((p, i) => (p, i)).Where(x => x.p.Z is 0 or 1000).Select(x => new SeepageHead(x.i, x.p.Z - 500)).ToArray();
        m.InitialHeads = m.Nodes.Select(p => p.Z - 450).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        double expectedFlux = -.01 * Math.Pow(2, -.25) * Math.Pow(1 - 1 / Math.Sqrt(2), 2);
        foreach (var n in r.Nodes) Assert.AreEqual(n.Z - 500, n.Head, 2e-4);
        foreach (var e in r.Elements) Assert.AreEqual(expectedFlux, e.FluxZ, 1e-8);
        Assert.AreEqual(0, r.Balance, 1e-8);
    }

    [TestMethod]
    public void AtmosphericFaceDischargesButCannotInjectWater()
    {
        var m = Rectangle(4, 4); m.Heads = Enumerable.Range(0, 5).Select(i => new SeepageHead(i, 2000)).ToArray();
        m.SeepageFaceNodes = Enumerable.Range(20, 5).ToArray();
        var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        Assert.AreEqual(10, r.Outflow, 1e-6);
        Assert.IsTrue(r.Nodes.Where(n => n.Z == 1000).All(n => n.Draining && n.BoundaryReaction <= 1e-9));
        m.Heads = Enumerable.Range(0, 5).Select(i => new SeepageHead(i, -1000)).ToArray();
        var dry = SeepageAnalysis.Calculate(m); Assert.IsTrue(dry.IsComplete, dry.Diagnostic);
        Assert.IsTrue(dry.Nodes.All(n => !n.Draining)); Assert.AreEqual(0, dry.Inflow, 1e-8);
        Assert.AreEqual(2000, r.Nodes[0].Head, 1e-8); // A second analysis cannot mutate the first one.
    }

    [TestMethod]
    public void IterationExhaustionDoesNotPublishAPressureField()
    {
        var m = Rectangle(4, 4); m.Heads = [new(0, 1000), new(24, 2000)];
        var r = SeepageAnalysis.Calculate(m, new() { MaximumIterations = 1 });
        Assert.AreEqual(SeepageStatus.NonConvergent, r.Status); Assert.AreEqual(0, r.Nodes.Count); Assert.IsFalse(r.IsComplete);
    }

    [TestMethod]
    public void NonAffineHarmonicFieldConvergesWithMeshRefinement()
    {
        double Error(int divisions)
        {
            var m = Rectangle(divisions, divisions);
            double Head(SeepageNode p) => 3000 + (p.X * p.X - p.Z * p.Z) / 1000;
            m.Heads = m.Nodes.Select((p, i) => (p, i)).Where(x => x.p.X is 0 or 1000 || x.p.Z is 0 or 1000).Select(x => new SeepageHead(x.i, Head(x.p))).ToArray();
            var r = SeepageAnalysis.Calculate(m); Assert.IsTrue(r.IsComplete, r.Diagnostic);
            double squared = 0;
            foreach (var e in r.Elements)
            {
                double x = (m.Nodes[e.A].X + m.Nodes[e.B].X + m.Nodes[e.C].X) / 3, z = (m.Nodes[e.A].Z + m.Nodes[e.B].Z + m.Nodes[e.C].Z) / 3;
                squared += Math.Pow(e.FluxX + .00002 * x, 2) + Math.Pow(e.FluxZ - .00002 * z, 2);
            }
            Assert.AreEqual(0, r.Balance, 1e-6); return Math.Sqrt(squared / r.Elements.Count);
        }
        double coarse = Error(8), medium = Error(16), fine = Error(32);
        Assert.IsTrue(medium < .55 * coarse && fine < .55 * medium, "Il gradiente P1 deve convergere al primo ordine.");
        Assert.IsTrue(fine < .00016, "Errore RMS sulle velocità rispetto alla soluzione armonica.");
    }

    [TestMethod]
    public void HydraulicPressuresTransferToIndependentMechanicalStages()
    {
        var hydraulic = Rectangle(2, 12, 1000, 6000); hydraulic.Heads = [new(0, 6000)];
        var flooded = SeepageAnalysis.Calculate(hydraulic); Assert.IsTrue(flooded.IsComplete, flooded.Diagnostic);
        hydraulic.Heads = [new(0, 3000)];
        var drained = SeepageAnalysis.Calculate(hydraulic); Assert.IsTrue(drained.IsComplete, drained.Diagnostic);
        Assert.ThrowsException<ArgumentException>(() => SeepageWallCoupling.Profile(drained, 500, 6000, 0, false));
        var profile1 = SeepageWallCoupling.Profile(flooded, 500, 6000, 0, true);
        var profile2 = SeepageWallCoupling.Profile(drained, 500, 6000, 0, true);
        var wall = new EmbeddedWallModel { Top = 6000, Bottom = 0, MaximumElementLength = 500, FixBottomDisplacement = true, FixBottomRotation = true,
            Segments = [new() { Top = 6000, Bottom = 0, FlexuralRigidity = 2e14, SectionId = "S1", Source = "analytical" }],
            Stages = [new() { Id = "Ponded", Left = new() { Elevation = 6000, WaterElevation = 6000, PorePressures = profile1, PorePressureSource = "FEM 1" }, Right = new() { Elevation = 0 } },
                new() { Id = "Drawdown", Left = new() { Elevation = 6000, WaterElevation = 3000, PorePressures = profile2, PorePressureSource = "FEM 2" }, Right = new() { Elevation = 0 } }] };
        var r = EmbeddedWallAnalysis.Calculate(wall); Assert.IsTrue(r.IsComplete, r.Diagnostic);
        // Water triangle: Mtoe=γw b L³/6, uhead=γw b L⁵/(30EI).
        Assert.AreEqual(353160000, r.Stages[0].Points.Last().Moment, .1);
        Assert.AreEqual(12.71376, r.Stages[0].Points[0].Displacement, 1e-7);
        Assert.AreEqual(44145000, r.Stages[1].Points.Last().Moment, .1);
        Assert.AreEqual(6000, flooded.Nodes[0].Head, 1e-8);
        Assert.ThrowsException<ArgumentException>(() => SeepageWallCoupling.PressureAt(drained, 1200, 1000));
    }

    [TestMethod]
    public void CutoffDomainConservesFlowAndLongerEmbedmentReducesDischarge()
    {
        var soil = new SeepageMaterial("sand", .01, .01, 0, "given");
        var d = new EmbeddedWallSeepageDomain { CaseId = "cutoff", LeftExtent = 6000, RightExtent = 6000, Bottom = -6000,
            WallTop = 0, WallBottom = -2000, WallThickness = 400, MaximumElementLength = 500,
            LeftGround = 0, RightGround = 0, LeftHead = 1000, RightHead = 0,
            LeftLayers = [new(0, -6000, soil)], RightLayers = [new(0, -6000, soil)] };
        var shallow = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(shallow.IsComplete, shallow.Diagnostic);
        d.WallBottom = -4000;
        var deep = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(deep.IsComplete, deep.Diagnostic);
        Assert.IsTrue(deep.Inflow < shallow.Inflow && deep.Inflow > 0, "La barriera più profonda deve ridurre la conduttanza.");
        Assert.AreEqual(0, deep.Balance, 1e-7);
        Assert.IsTrue(SeepageWallCoupling.PressureAt(deep, -200, -2000) > SeepageWallCoupling.PressureAt(deep, 200, -2000));
        Assert.ThrowsException<ArgumentException>(() => SeepageWallCoupling.PressureAt(deep, 0, -2000));
        d.RightHead = d.LeftHead;
        var still = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(d)); Assert.IsTrue(still.IsComplete, still.Diagnostic);
        Assert.AreEqual(0, still.Inflow, 1e-8);
    }

    [TestMethod]
    public void InvalidTensorFloatingDomainAndInteriorFluxAreRejected()
    {
        var m = Rectangle(2, 2); m.Heads = [new(0, 2000)]; m.Materials = [new("bad", .01, .01, .02, "given")];
        Assert.ThrowsException<ArgumentException>(() => SeepageAnalysis.Calculate(m));
        m.Materials = [new("good", .01, .01, 0, "given")]; m.Heads = [];
        Assert.ThrowsException<ArgumentException>(() => SeepageAnalysis.Calculate(m));
        m.Heads = [new(0, 2000)]; m.Fluxes = [new(0, 4, .1)];
        Assert.ThrowsException<ArgumentException>(() => SeepageAnalysis.Calculate(m));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => SeepageAnalysis.Calculate(Rectangle(2, 2), cancellation: cts.Token));
    }
}
