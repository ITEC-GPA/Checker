using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    public abstract partial class SectionSolver
    {
        // Per-call data only: no phase history is attached to the section or its materials.
        internal sealed class PhaseSection
        {
            private readonly SectionSolver solver;
            private readonly bool linear;
            private readonly ReinforcedConcreteRebar[] bars;
            private readonly Dictionary<int, bool> inside;
            private readonly Point2d[] vertices;
            private readonly StressAnalysisTension tension;
            private readonly CoordinateSystem outputAxes, axes;
            private readonly bool analyticParabola;
            private readonly ParabolaRectangleLaw parabola;
            private readonly StressBlockIntegration analyticIntegration;
            private readonly Sample[] concreteSamples;
            private readonly ForceTuple outputN, outputMx, outputMy;
            private readonly struct Sample
            {
                internal readonly string Id;
                internal readonly double X, Y, Weight;
                internal Sample(string id, double x, double y, double weight) { Id = id; X = x; Y = y; Weight = weight; }
            }
            internal double Length { get; }
            internal int Evaluations { get; private set; }
            internal void ResetEvaluations() => Evaluations = 0;

            internal PhaseSection(SectionSolver solver)
            {
                this.solver = solver;
                var options = solver.SectionOption;
                linear = options.StressAnalysisType == StressAnalysisTypes.Linear;
                if (!linear && options.StressAnalysisType != StressAnalysisTypes.NonLinear)
                    throw new NotSupportedException("Unsupported phase stress analysis type.");
                if (options.PsiCoefficientRebar != 0 || options.PsiCoefficientTendon != 0)
                    throw new NotSupportedException("Memoryless phase analysis requires zero psi coefficients; creep is not accumulated.");
                if (!linear && solver.ConcreteMaterial.CompressionStressStrainDiagram == ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock)
                    throw new NotSupportedException("Phase equilibrium requires a continuous constitutive law; stress block is a resistance model.");
                if (!linear && options.StressAnalysisConcreteTension == ConcreteMaterial.TensionResponseDiagrams.TensionChord)
                    throw new NotSupportedException("TensionChord coupling with imposed shrinkage is not supported by phase analysis.");
                tension = linear ? null : solver.GetStressAnalysisTension();
                bars = solver.ConcreteSection.GetRebars();
                inside = solver.ConcreteSection.GetRebarIsInsideAssociation();
                vertices = solver.ConcreteSection.ConcreteShape.GetPoints2d().ToArray();
                if (vertices.Length < 3) throw new ArgumentException("A nonempty concrete section is required.");
                outputAxes = (CoordinateSystem)options.ForceReferenceCoordinateSystem.Clone();
                ConcretePhase.ValidateAxes(outputAxes);
                axes = (CoordinateSystem)solver.SolverAxes.Clone();
                var reference = solver.IntegrationReferencePoint;
                Length = Math.Max(1, vertices.Max(p => Math.Max(Math.Abs(p.X - reference.X), Math.Abs(p.Y - reference.Y))));
                // Material/mesh configuration is fixed for the call (or for this batch worker).
                // Recognizing the parabola at every sampled point cloned two material arrays each time.
                analyticParabola = !linear && solver.TryGetParabolaLaw(out parabola);
                if (analyticParabola && tension == null) analyticIntegration = solver.GetStressBlockIntegration();
                var samples = new List<Sample>();
                foreach (var element in solver._globalCoordinateGaussPointsMesh)
                    foreach (var p in element)
                        samples.Add(new Sample("G" + samples.Count, p.GpX, p.GpY, p.EvaluateFunction((x, y) => 1.0)));
                concreteSamples = samples.ToArray();
                outputN = Project(new ForceTuple(1, 0, 0)); outputMx = Project(new ForceTuple(0, 1, 0)); outputMy = Project(new ForceTuple(0, 0, 1));
            }

            internal StrainPlane Plane(double[] q, double shrinkage = 0)
                => new StrainPlane(q[1] / Length, q[2] / Length, solver.IntegrationReferencePoint, q[0] - shrinkage);

            // Solve about the integration point. Input axes may have a different origin and in-plane rotation.
            internal ForceTuple Load(ResultBeamForces force)
            {
                var f = force.ConvertToForceTuple(axes);
                var r = solver.IntegrationReferencePoint;
                var result = new ForceTuple(f.N, f.Mx - f.N * (axes.Origin.Y - r.Y), f.My + f.N * (axes.Origin.X - r.X));
                if (!ConcretePhase.Finite(result.N) || !ConcretePhase.Finite(result.Mx) || !ConcretePhase.Finite(result.My))
                    throw new ArgumentException("Non-finite transformed phase actions.");
                return result;
            }

            private ForceTuple Project(ForceTuple f)
            {
                f = solver.GetExternalForces(f, axes);
                var output = new ResultBeamForces(f.N, 0, 0, 0, f.Mx, f.My, axes).ToCoordinateSystem(outputAxes);
                return new ForceTuple(output.N, output.M1, output.M2);
            }

            internal ForceTuple OutputForces(ForceTuple f) => outputN * f.N + outputMx * f.Mx + outputMy * f.My;
            internal ResultBeamForces External(ForceTuple f)
            {
                f = OutputForces(f);
                return new ResultBeamForces(f.N, 0, 0, 0, f.Mx, f.My, outputAxes);
            }

            private double ConcreteStress(double e) => linear ? solver.CalculateElasticSigmaC(e)
                : e > 0 && tension != null ? tension.GetConcreteStress(e) : analyticParabola ? parabola.At(e) : solver.CalculateSigmaC(e);

            private double BarStress(ReinforcedConcreteRebar bar, double e)
            {
                if (!linear) return solver.CalculateStressRebar(bar, e);
                // Choose the modulus from the mechanical strain, including the fixed prestrain.
                double mechanical = e + bar.EpsilonP;
                return mechanical * (mechanical < 0 ? bar.RebarMaterial.ElasticModulusCompression : bar.RebarMaterial.ElasticModulusTension);
            }

            internal bool Evaluate(double[] q, double shrinkage, out ForceTuple force)
            {
                Evaluations++;
                force = default;
                if (!ConcretePhase.Finite(q[0]) || !ConcretePhase.Finite(q[1]) || !ConcretePhase.Finite(q[2]) || !ConcretePhase.Finite(shrinkage)) return false;
                var total = Plane(q);
                var mechanical = Plane(q, shrinkage);
                if (!linear)
                {
                    // Check the actual fibres before calling tables that return zero outside their ultimate interval.
                    double ultimate = solver.GetUltimateStrainConcreteCompression();
                    foreach (var p in vertices) if (mechanical.GetStrain(p) < ultimate) return false;
                    foreach (var bar in bars)
                    {
                        double e = total.GetStrain(bar.Position) + bar.EpsilonP;
                        if (e < solver.CalculateDesignUltimateStrainCompressionRebar(bar.RebarMaterial)
                            || e > solver.CalculateDesignUltimateStrainTensionRebar(bar.RebarMaterial)) return false;
                    }
                }
                double n = 0, mx = 0, my = 0;
                var r = solver.IntegrationReferencePoint;
                void Add(double x, double y, double value)
                { n += value; mx -= value * (y - r.Y); my += value * (x - r.X); }
                if (analyticIntegration != null)
                {
                    var concrete = analyticIntegration.Integrate(mechanical, parabola);
                    n = concrete.N; mx = concrete.Mx; my = concrete.My;
                }
                else
                    foreach (var p in concreteSamples)
                        Add(p.X, p.Y, p.Weight * ConcreteStress(mechanical.GetStrain(p.X, p.Y)));
                for (int i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    double sigma = BarStress(b, total.GetStrain(b.Position));
                    if (inside[i]) sigma -= ConcreteStress(mechanical.GetStrain(b.Position));
                    Add(b.Position.X, b.Position.Y, b.Area * sigma);
                }
                for (int i = 0; i < solver.ConcreteSection.SteelSections.Count; i++)
                {
                    var steel = solver.ConcreteSection.SteelSections[i].Section;
                    for (int j = 0; j < solver._steelSectionsThinWallsBreaked[i].Length; j++)
                    {
                        var wall = solver._steelSectionsThinWallsBreaked[i][j];
                        if (!linear)
                        {
                            double lower = solver._standardStructuralSteel == null ? steel.SteelMaterial.StrainUCompression : solver.CalculateDesignUltimateStrainCompressionStructuralSteel(steel.SteelMaterial);
                            double upper = solver._standardStructuralSteel == null ? steel.SteelMaterial.StrainUTension : solver.CalculateDesignUltimateStrainTensionStructuralSteel(steel.SteelMaterial);
                            // Affine strain extrema occur at the wall perimeter, not at interior Gauss samples.
                            double c = Math.Cos(wall.Angle), s = Math.Sin(wall.Angle);
                            double center = total.GetStrain(wall.Point);
                            double range = .5 * (wall.L * Math.Abs(total.ChiX * c + total.ChiY * s)
                                + wall.T * Math.Abs(-total.ChiX * s + total.ChiY * c));
                            if (center - range < lower || center + range > upper) return false;
                        }
                        foreach (var p in solver._globalCoordinateGaussPointsThinWalls[i][j])
                        {
                            double e = total.GetStrain(p.GpX, p.GpY);
                            if (!linear && (e < steel.SteelMaterial.StrainUCompression || e > steel.SteelMaterial.StrainUTension)) return false;
                            double sigma = linear ? solver.CalculateElasticSigmaS(0, steel, e)
                                : solver._standardStructuralSteel == null ? steel.SteelMaterial.GetStress(e) : solver.CalculateStressStructuralSteel(steel, e);
                            if (wall.IsInsideConcrete) sigma -= ConcreteStress(mechanical.GetStrain(p.GpX, p.GpY));
                            Add(p.GpX, p.GpY, sigma * wall.T * p.EvaluateFunction((x, y) => 1.0));
                        }
                    }
                }
                force = new ForceTuple(n, mx, my);
                return ConcretePhase.Finite(n) && ConcretePhase.Finite(mx) && ConcretePhase.Finite(my);
            }

            internal ConcretePhaseState State(double[] q, double shrinkage, ForceTuple target, ForceTuple force, double[,] scaledTangent, bool integrationPoints, double normalizedResidual)
            {
                var total = Plane(q);
                var points = new List<ConcretePhasePoint>(vertices.Length + bars.Length + (integrationPoints ? concreteSamples.Length : 0));
                void AddConcrete(string id, ConcretePhasePointKind kind, double x, double y)
                {
                    double e = total.GetStrain(x, y);
                    points.Add(new ConcretePhasePoint(id, kind, x, y, e, shrinkage, ConcreteStress(e - shrinkage)));
                }
                for (int i = 0; i < vertices.Length; i++) AddConcrete("C" + i, ConcretePhasePointKind.ConcreteVertex, vertices[i].X, vertices[i].Y);
                if (integrationPoints)
                {
                    foreach (var p in concreteSamples) AddConcrete(p.Id, ConcretePhasePointKind.ConcreteIntegrationPoint, p.X, p.Y);
                }
                for (int i = 0; i < bars.Length; i++)
                {
                    var b = bars[i]; double e = total.GetStrain(b.Position);
                    points.Add(new ConcretePhasePoint("R" + i, ConcretePhasePointKind.Rebar, b.Position.X, b.Position.Y, e, -b.EpsilonP, BarStress(b, e)));
                }
                for (int i = 0; i < solver.ConcreteSection.SteelSections.Count; i++)
                {
                    var steel = solver.ConcreteSection.SteelSections[i].Section;
                    for (int j = 0; j < solver._globalCoordinateGaussPointsThinWalls[i].Length; j++)
                    {
                        int k = 0;
                        foreach (var p in solver._globalCoordinateGaussPointsThinWalls[i][j])
                        {
                            double e = total.GetStrain(p.GpX, p.GpY);
                            double sigma = linear ? solver.CalculateElasticSigmaS(0, steel, e)
                                : solver._standardStructuralSteel == null ? steel.SteelMaterial.GetStress(e) : solver.CalculateStressStructuralSteel(steel, e);
                            points.Add(new ConcretePhasePoint("S" + i + ":" + j + ":" + k++, ConcretePhasePointKind.StructuralSteel, p.GpX, p.GpY, e, 0, sigma));
                        }
                    }
                }
                double[,] tangent = null;
                if (scaledTangent != null)
                {
                    tangent = new double[3, 3];
                    for (int j = 0; j < 3; j++)
                    {
                        double column = j == 0 ? 1 : Length;
                        var f = External(new ForceTuple(scaledTangent[0, j] * column, scaledTangent[1, j] * Length * column, scaledTangent[2, j] * Length * column));
                        tangent[0, j] = f.N; tangent[1, j] = f.M1; tangent[2, j] = f.M2;
                    }
                }
                return new ConcretePhaseState(External(target), External(force), shrinkage, SectionStrain.From(total), tangent, points, normalizedResidual);
            }
        }
    }
}
