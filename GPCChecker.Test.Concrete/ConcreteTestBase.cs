using System;
using System.Collections.Generic;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public abstract class ConcreteTestBase : UnitTestBase
    {
        protected ReinforcedConcreteSection GetRectangularSection1(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50, ConcreteMaterialEN1992 concreteMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);

            int i = 0;
            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(i++, rebar, new Point3d(concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(i++, rebar, new Point3d(width - concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(i++, rebar, new Point3d(width - concreteCover, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(i++, rebar, new Point3d(concreteCover, height - concreteCover, 0))
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        protected virtual void CalculateAdimensionalForces(IConcreteSection section, ResultBeamForces forces,
            out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
        {
            BoundingBox3d bBox = section.Shape.GetBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;
            double fck = section.ConcreteMaterial.StressStrainTableCompression.GetMaximumStress();

            adimAxialForce = forces.N / (b * h * fck);
            adimBendingMomentX = forces.M1 / (b * h * h * fck);
            adimBendingMomentY = forces.M2 / (b * b * h * fck);
        }

        protected virtual ForceTuple CalculateAdimensionalForces(IConcreteSection section, ForceTuple forces)
        {
            BoundingBox3d bBox = section.Shape.GetBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;
            double fck = Math.Abs(section.ConcreteMaterial.StressStrainTableCompression.GetMinimumStress());

            return new ForceTuple(forces.N / (b * h * fck), forces.Mx / (b * h * h * fck), forces.My / (b * b * h * fck));
        }

        protected virtual void CalculateAdimensionalForces(IConcreteSection section, double N, double Mx, double My,
            out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
        {
            BoundingBox3d bBox = section.Shape.GetBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;
            double fck = section.ConcreteMaterial.StressStrainTableCompression.GetMaximumStress();

            adimAxialForce = N / (b * h * fck);
            adimBendingMomentX = Mx / (b * h * h * fck);
            adimBendingMomentY = My / (b * b * h * fck);
        }

        protected bool SLSCommonAssertModelCode(StressAnalysisResult result, IConcreteSection section, ResultBeamForces forces,
            StandardModelCode2010 standard)
        {
            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);
            var adimExternalForces = solver.ConvertToAdimForces(new ForceTuple(forces.N, forces.M1, forces.M2));

            List<string> log = result.GetLog();
            foreach (string s in log)
                Console.WriteLine($"{s}");

            if (log.Count > 0)
                return false;

            if (result.StrainPlane != null)
            {
                ForceTuple calculatedForces = solver.CalculateSectionForceResultant(result.StrainPlane);

                var adimForces = solver.ConvertToAdimForces(calculatedForces);
                double tolerance = 1e-5;

                if (Math.Abs(adimForces.N - adimExternalForces.N) > tolerance ||
                    Math.Abs(adimForces.Mx - adimExternalForces.Mx) > tolerance ||
                    Math.Abs(adimForces.My - adimExternalForces.My) > tolerance)
                    return false;

                (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension();
                (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension();

                Console.WriteLine($"Tensions associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} ");

                for (int i = 0; i < rebarTensions.Length; i++)
                    Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
                        $"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

                for (int i = 0; i < concreteTensions.Length; i++)
                    Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");
            }
            else
            {
                Console.WriteLine($"Result {result.Id} associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} don't find strain plane." +
                    $"Point is external");
            }

            return true;
        }

        protected bool CommonAssertDomainPoint(IConcreteSection section, ResultBeamForces force, StandardModelCode2010 standard, double adimTolerance = 0.005,
            double[] factor = null)
        {
            if (factor == null)
                factor = new double[] { 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5 };

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);
            FailureDomain.FailureDomainPoint[] failureDomainPoints = new FailureDomain.FailureDomainPoint[factor.Length];

            int j = 0;

            try
            {
                for (j = 0; j < factor.Length; j++)
                {
                    ResultBeamForces testForce = new ResultBeamForces(factor[j] * force.N, 0, 0, 0, factor[j] * force.M1, factor[j] * force.M2, force.CoordinateSystem);
                    failureDomainPoints[j] = solver.CalculatePoint(testForce.ConvertToForceTuple(section.Centroid));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fail to calculate domain point {j}, {e.Message}");
                return false;
            }


            for (int i = 1; i < factor.Length; i++)
            {
                if (failureDomainPoints[i] != null)
                {
                    ForceTuple adimForces = solver.ConvertToAdimForces(new ForceTuple(failureDomainPoints[0].Point.Z - failureDomainPoints[i].Point.Z,
                        failureDomainPoints[0].Point.X - failureDomainPoints[i].Point.X,
                        failureDomainPoints[0].Point.Y - failureDomainPoints[i].Point.Y));

                    Assert.IsTrue(Math.Abs(adimForces.N) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.Mx) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.My) < adimTolerance);
                }
                else
                    return false;
            }

            return true;
        }

        protected Point3d[] ExportToGmsh(FailureDomain failureDomain, FailureDomain failureDomain2 = null)
        {
            GmshNet.Gmsh.Initialize();
            int horizontal = failureDomain.DomainPoints.GetUpperBound(0);
            List<Point3d> points = new List<Point3d>();


            Action<FailureDomain> action = new Action<FailureDomain>((domain) =>
            {
                for (int i = 0; i < horizontal; i++)
                {
                    int vertical = domain.DomainPoints[i].GetUpperBound(0);

                    for (int j = 0; j < vertical; j++)
                    {

                        GmshNet.Gmsh.Model.Occ.AddPoint(domain.DomainPoints[i][j].MxRd / 1000000,
                            domain.DomainPoints[i][j].MyRd / 1000000,
                            domain.DomainPoints[i][j].NRd / 1000 / 10);

                        points.Add(new Point3d(domain.DomainPoints[i][j].MxRd / 1000000,
                            domain.DomainPoints[i][j].MyRd / 1000000,
                            domain.DomainPoints[i][j].NRd / 1000 / 10));
                    }
                }

                GmshNet.Gmsh.Model.Occ.Synchronize();
            });

            action(failureDomain);

            if (failureDomain2 != null)
                action(failureDomain2);




            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();

            return points.ToArray();
        }


        protected Point3d[] ExportToGmsh(FailureDomain failureDomain)
        {
            GmshNet.Gmsh.Initialize();
            int horizontal = failureDomain.DomainPoints.GetUpperBound(0);
            List<Point3d> points = new List<Point3d>();

            for (int i = 0; i < horizontal; i++)
            {
                int vertical = failureDomain.DomainPoints[i].GetUpperBound(0);

                for (int j = 0; j < vertical; j++)
                {
                    GmshNet.Gmsh.Model.Occ.AddPoint(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                        failureDomain.DomainPoints[i][j].MyRd / 1000000,
                        failureDomain.DomainPoints[i][j].NRd / 1000 / 10);

                    points.Add(new Point3d(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                        failureDomain.DomainPoints[i][j].MyRd / 1000000,
                        failureDomain.DomainPoints[i][j].NRd / 1000 / 10));
                }
            }

            GmshNet.Gmsh.Model.Occ.Synchronize();
            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();

            return points.ToArray();
        }

        protected Point3d[] ExportToGmsh(Mesh mesh)
        {
            GmshNet.Gmsh.Initialize();
            List<Point3d> points = new List<Point3d>();

            for (int i = 1; i <= mesh.FacesCount; i++)
            {
                for (int j = 1; j <= mesh.Faces[i].GetNodes().Length; j++)
                {
                    MeshVertex[] vertices = mesh.GetFaceVertices(mesh.Faces[i]);
                    List<int> indicesV = new List<int>();
                    List<int> indicesL = new List<int>();

                    for (int k = 0; k < vertices.Length; k++)
                    {
                        indicesV.Add(GmshNet.Gmsh.Model.Occ.AddPoint(vertices[k].Point.X / 1000000,
                            vertices[k].Point.Y / 1000000,
                            vertices[k].Point.Z / 10000));
                    }

                    for (int k = 0; k < indicesV.Count; k++)
                    {
                        if (k != indicesV.Count - 1)
                            indicesL.Add(GmshNet.Gmsh.Model.Occ.AddLine(indicesV[k], indicesV[k + 1]));
                        else
                            indicesL.Add(GmshNet.Gmsh.Model.Occ.AddLine(indicesV[k], indicesV[0]));
                    }

                    int wire = GmshNet.Gmsh.Model.Occ.AddWire(indicesL.ToArray());
                    GmshNet.Gmsh.Model.Occ.AddPlaneSurface(new int[] { wire });
                }
            }

            GmshNet.Gmsh.Model.Occ.Synchronize();

            GmshNet.Gmsh.Model.Mesh.Generate(0);
            GmshNet.Gmsh.Model.Mesh.Generate(1);
            GmshNet.Gmsh.Model.Mesh.Generate(2);

            GmshNet.Gmsh.Model.Occ.Synchronize();

            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();

            return points.ToArray();
        }

        protected void ExportToGmsh(IConcreteSection section)
        {
            GmshNet.Gmsh.Initialize();

            int[] fillTag = new int[section.Shape.Fill.Count];
            for (int i = 0; i < section.Shape.Fill.Count; i++)
            {
                fillTag[i] = GmshNet.Gmsh.Model.Occ.AddPoint(section.Shape.Fill[i].X, section.Shape.Fill[i].Y, 0.0);
            }

            for (int i = 0; i < fillTag.Length; i++)
            {
                if (i != (fillTag.Length - 1))
                    GmshNet.Gmsh.Model.Occ.AddLine(fillTag[i], fillTag[i + 1]);
                else
                    GmshNet.Gmsh.Model.Occ.AddLine(fillTag[i], fillTag[0]);
            }

            if (section.Shape.HasHoles)
            {
                int[][] holesTag = new int[section.Shape.Holes.Length][];

                for (int i = 0; i < section.Shape.Holes.Length; i++)
                {
                    holesTag[i] = new int[section.Shape.Holes[i].Count];
                    for (int j = 0; j < section.Shape.Holes[i].Count; j++)
                    {
                        holesTag[i][j] = GmshNet.Gmsh.Model.Occ.AddPoint(section.Shape.Holes[i][j].X, section.Shape.Holes[i][j].Y, 0.0);
                    }
                }

                for (int i = 0; i < section.Shape.Holes.Length; i++)
                {
                    for (int j = 0; j < holesTag[i].Length; j++)
                    {
                        if (j != (holesTag[i].Length - 1))
                            GmshNet.Gmsh.Model.Occ.AddLine(holesTag[i][j], holesTag[i][j + 1]);
                        else
                            GmshNet.Gmsh.Model.Occ.AddLine(holesTag[i][j], holesTag[i][0]);
                    }
                }
            }

            foreach (var rebar in section.GetRebars())
            {
                GmshNet.Gmsh.Model.Occ.AddPoint(rebar.Position.X, rebar.Position.Y, 0.0);
            }

            GmshNet.Gmsh.Model.Occ.Synchronize();
            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();
        }

        protected void ShowDomainPoints(FailureDomain failureDomain)
        {
            for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
                for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
                    Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
                                      $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
                                      $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");
        }

        protected bool CommonAssertsModelCode(IConcreteSection section, StandardModelCode2010 standard, FailureDomain failureDomain, double errorPercentage = 5.0)
        {
            List<Point3d> failureDomainPoints = new List<Point3d>();

            Point3d NRdMin = new Point3d(double.MaxValue, double.MaxValue, double.MaxValue);
            Point3d MxRdMin = new Point3d(double.MaxValue, double.MaxValue, double.MaxValue);
            Point3d MyRdMin = new Point3d(double.MaxValue, double.MaxValue, double.MaxValue);
            Point3d NRdMax = new Point3d(double.MinValue, double.MinValue, double.MinValue);
            Point3d MxRdMax = new Point3d(double.MinValue, double.MinValue, double.MinValue);
            Point3d MyRdMax = new Point3d(double.MinValue, double.MinValue, double.MinValue);

            for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
            {
                for (int j = 0; j < failureDomain.DomainPoints[i].Length; j++)
                {
                    failureDomainPoints.Add(failureDomain.DomainPoints[i][j].Point);

                    if (failureDomain.DomainPoints[i][j].Point.Z < NRdMin.Z)
                        NRdMin = failureDomain.DomainPoints[i][j].Point;

                    if (failureDomain.DomainPoints[i][j].Point.X < MxRdMin.X)
                        MxRdMin = failureDomain.DomainPoints[i][j].Point;

                    if (failureDomain.DomainPoints[i][j].Point.Y < MyRdMin.Y)
                        MyRdMin = failureDomain.DomainPoints[i][j].Point;

                    if (failureDomain.DomainPoints[i][j].Point.Z > NRdMax.Z)
                        NRdMax = failureDomain.DomainPoints[i][j].Point;

                    if (failureDomain.DomainPoints[i][j].Point.X > MxRdMax.X)
                        MxRdMax = failureDomain.DomainPoints[i][j].Point;

                    if (failureDomain.DomainPoints[i][j].Point.Y > MyRdMax.Y)
                        MyRdMax = failureDomain.DomainPoints[i][j].Point;
                }
            }

            // valore per campo di deformazione 6 (epsilon 0.2% costante)
            double pureCompressionAxialForce = section.Shape.GetArea() * ((ConcreteMaterialModelCode2010)section.ConcreteMaterial).Fck * standard.AlphaCC / standard.GammaC;
            double pureCompressionMomentX = 0;
            double pureCompressionMomentY = 0;


            foreach (var rebar in section.GetRebars())
            {
                pureCompressionAxialForce += rebar.Area * (rebar.RebarMaterial.Fyk / standard.GammaS -
                    ((ConcreteMaterialModelCode2010)section.ConcreteMaterial).Fck * standard.AlphaCC / standard.GammaC);
                pureCompressionMomentX += rebar.Area * (rebar.RebarMaterial.Fyk / standard.GammaS) *
                    (rebar.Position.Y - section.Centroid.Y);
                pureCompressionMomentY += rebar.Area * (rebar.RebarMaterial.Fyk / standard.GammaS) *
                    (rebar.Position.X - section.Centroid.X);
            }


            if (Math.Abs((Math.Abs(NRdMin.Z) - Math.Abs(pureCompressionAxialForce)) / NRdMin.Z) * 100 > errorPercentage)
                return false;
            if (Math.Abs((Math.Abs(NRdMin.X) - Math.Abs(pureCompressionMomentX)) / NRdMin.X) * 100 > errorPercentage &&
                (Math.Abs(NRdMin.X) > 1 && Math.Abs(pureCompressionMomentX) > 1))
                return false;
            if (Math.Abs((Math.Abs(NRdMin.Y) - Math.Abs(pureCompressionMomentY)) / NRdMin.Y) * 100 > errorPercentage &&
                (Math.Abs(NRdMin.Y) > 1 && Math.Abs(pureCompressionMomentY) > 1))
                return false;

            // valore per campo di deformazione 1 (epsilon 7.5% costante)
            double pureTractionAxialForce = 0.0;
            double pureTractionMomentX = 0;
            double pureTractionMomentY = 0;


            foreach (var rebar in section.GetRebars())
            {
                pureTractionAxialForce += rebar.Area * rebar.RebarMaterial.Fyk / standard.GammaS;
                pureTractionMomentX += rebar.Area * rebar.RebarMaterial.Fyk / standard.GammaS * (rebar.Position.Y - section.Centroid.Y);
                pureTractionMomentY += rebar.Area * rebar.RebarMaterial.Fyk / standard.GammaS * (rebar.Position.X - section.Centroid.X);
            }


            if (Math.Abs((Math.Abs(NRdMax.Z) - Math.Abs(pureTractionAxialForce)) / NRdMax.Z) * 100 > errorPercentage)
                return false;
            if (Math.Abs((Math.Abs(NRdMax.X) - Math.Abs(pureTractionMomentX)) / NRdMax.X) * 100 > errorPercentage &&
                (Math.Abs(NRdMax.X) > 1 && Math.Abs(pureTractionMomentX) > 1))
                return false;
            if (Math.Abs((Math.Abs(NRdMax.Y) - Math.Abs(pureTractionMomentY)) / NRdMax.Y) * 100 > errorPercentage &&
                (Math.Abs(NRdMax.X) > 1 && Math.Abs(pureTractionMomentY) > 1))
                return false;

            return true;
        }

        internal class SectionSolverModelCode2010Test : SectionSolverModelCode2010
        {
            internal SectionSolverModelCode2010Test(IConcreteSection section, StandardModelCode2010 standard, int id = -1)
                : base(section, standard, id)
            {
            }

            internal ForceTuple CalculateSectionForceResultant(StrainPlane strainPlane)
            {
                return base.CalculateForceResultant(strainPlane);
            }

            internal double CalculateSigmaConcrete(double strain)
            {
                return base.CalculateSigmaC(strain);
            }

            internal double GetDesignUltimateStrainRebars(ReinforcedConcreteRebar rebar, double strain)
            {
                return base.CalculateStressRebar(rebar, strain);
            }

            internal ForceTuple ConvertToAdimForces(ForceTuple force)
            {
                return base.ConvertToAdimensionalForces(force);
            }

            internal override double CalculateStressRebar(ReinforcedConcreteRebar rebar, double strain)
            {
                return base.CalculateStressRebar(rebar, strain);
            }

            internal FailureDomain.FailureDomainPoint CalculatePoint(ForceTuple targetLocalForces)
            {
                return base.CalculateDomainPoint(targetLocalForces);
            }

            internal ForceTuple IntegrateSectionStressTest(StrainPlane strainPlane)
            {
                return base.IntegrateSectionStress(strainPlane);
            }
        }
    }
}
