using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
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
        #region Section Construction Methods

        protected ReinforcedConcreteSection GetRectangularSection4Rebars(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50,
            ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {
            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, height - concreteCover, 0))
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        protected ReinforcedConcreteSection GetRectangularSection8Rebars(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50,
            ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width / 2.0, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, height / 2.0, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(width / 2.0, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, height / 2.0, 0))
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        protected ReinforcedConcreteSection GetRectangularSection2SideRebars(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50,
            int numberOfRebars = 4, ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);


            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[2 * numberOfRebars];

            for (int j = 0; j < numberOfRebars; j++)
            {
                rebars[j] = new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + j * (width - 2.0 * concreteCover) / (numberOfRebars - 1), concreteCover));
                rebars[2 * numberOfRebars - 1 - j] = new ReinforcedConcreteRebar(rebar,
                    new Point2d(concreteCover + j * (width - 2.0 * concreteCover) / (numberOfRebars - 1), height - concreteCover));
            }

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        protected ReinforcedConcreteSection GetRectangularSection4SideRebars(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50,
            int numberOfRebarsTopBottomSide = 4, int numberOfRebarsLateralSide = 4, ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);


            List<ReinforcedConcreteRebar> rebars = new List<ReinforcedConcreteRebar>();

            for (int j = 0; j < numberOfRebarsLateralSide; j++)
            {
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + j * (width - 2.0 * concreteCover) /
                    (numberOfRebarsLateralSide - 1), concreteCover)));
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + j * (width - 2.0 * concreteCover) /
                    (numberOfRebarsLateralSide - 1), height - concreteCover)));
            }

            for (int j = 1; j < numberOfRebarsTopBottomSide - 1; j++)
            {
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover, concreteCover + j * (height - 2.0 * concreteCover) /
                    (numberOfRebarsTopBottomSide - 1))));
                rebars.Add(new ReinforcedConcreteRebar(rebar, new Point2d(width - concreteCover, concreteCover + j * (height - 2.0 * concreteCover) /
                    (numberOfRebarsTopBottomSide - 1))));
            }

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        protected ReinforcedConcreteSection GetRectangularSectionBottomSideRebars(double width = 300, double height = 500, double rebarDiameter = 18, double concreteCover = 50,
            int numberOfRebars = 4, ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);


            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[numberOfRebars];

            for (int j = 0; j < numberOfRebars; j++)
            {
                rebars[j] = new ReinforcedConcreteRebar(rebar, new Point2d(concreteCover + j * (width - 2.0 * concreteCover) / (numberOfRebars - 1), concreteCover));
            }

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        #endregion

        #region Test Utilities Methods


        protected ReinforcedConcreteSection GetCircularSection(double diameter = 300, double rebarDiameter = 18, double concreteCover = 50,
            int numberOfRebars = 16, ConcreteMaterial concreteMaterial = null, SteelMaterial rebarMaterial = null)
        {

            if (concreteMaterial == null)
                concreteMaterial = ConcreteMaterialEN1992.C25_30;

            if (rebarMaterial == null)
                rebarMaterial = SteelMaterial.B450C;

            Shape2d shape = new Shape2d(new Polygon2d(diameter));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);


            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[numberOfRebars];

            var rebarPerimeter = new Polygon2d(diameter - concreteCover*2, numberOfRebars);
            for (int j = 0; j < rebarPerimeter.Count; j++)
            {
                rebars[j] = new ReinforcedConcreteRebar(rebar, rebarPerimeter[j]);
            }

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
            section.AddRebars(rebars);

            return section;
        }

        #endregion

        #region Test Utilities Methods

        protected CoordinateSystem GetLocalCoordinateSystem(IConcreteSection section)
		{
            return new CoordinateSystem(section.Centroid, Vector3d.XAxis, Vector3d.YAxis);
        }

        protected ConcreteMaterialEN1992 GetLinearConcreteMaterial(double elasticModulus)
        {
            double elasticModulusFactor = 0.85 / 1.5;

            return new ConcreteMaterialEN1992("", -0.002, 0.0,
                new StressStrainTable(new double[] { 0, -elasticModulus / elasticModulusFactor, -2.0 * elasticModulus / elasticModulusFactor }, new double[] { 0, -0.001, -0.002 }),
                new StressStrainTable(new double[] { 0, 0 }, new double[] { 0, 0.001 }));
        }

        protected ConcreteMaterialEN1992 GetLinearConcreteMaterialTensile(double elasticModulus)
        {
            double elasticModulusFactor = 0.85 / 1.5;
            double elasticModulusFactorTens = 1.0 / 1.5;

            return new ConcreteMaterialEN1992("", -0.002, 0.0,
                new StressStrainTable(new double[] { 0, -elasticModulus / elasticModulusFactor, -2.0 * elasticModulus / elasticModulusFactor }, new double[] { 0, -0.001, -0.002 }),
                new StressStrainTable(new double[] { 0, elasticModulus / elasticModulusFactorTens }, new double[] { 0, 0.001 }));
        }

        protected virtual ForceTuple CalculateAdimensionalForces(IConcreteSection section, ForceTuple forces)
        {
            BoundingBox3d bBox = section.Shape.GetBoundingBox();
            double h = bBox.Size.Y;
            double b = bBox.Size.X;
            double fck = Math.Abs(section.ConcreteMaterial.StressStrainTableCompression.GetMinimumStress());

            return new ForceTuple(forces.N / (b * h * fck), forces.Mx / (b * h * h * fck), forces.My / (b * b * h * fck));
        }

        protected virtual BoundingBox3d GetBoundingBox(FailureDomain failureDomain)
        {
            BoundingBox3d boundingBox = new BoundingBox3d();

            for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
            {
                for (int j = 0; j < failureDomain.DomainPoints[i].Length; j++)
                {
                    boundingBox.Update(failureDomain.DomainPoints[i][j].Point);
                }
            }

            return boundingBox;
        }

        protected bool SerializationClassesCommonAsserts(object objToTest)
        {
            bool check = true;

            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, objToTest);
                ms.Position = 0;

                var oggettoDeserializzato = formatter.Deserialize(ms);

                if (objToTest == oggettoDeserializzato)
                {
                    Console.WriteLine($"Class {objToTest.ToString().Replace("GPC.Checkers.Concrete.", "")} is serializable");
                }
                else
                {
                    Console.WriteLine($"Warning: Class {objToTest.GetType()} is not serializable");
                    check = false;
                }
            }
            return check;
        }

        #endregion

        #region Common Asserts

        protected bool TensionAnalysisCommonAssertModelCode(StressAnalysisResult result, IConcreteSection section, ResultBeamForces forces,
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
                (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension();
                (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension();

                Console.WriteLine($"Tensions associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} ");

                for (int i = 0; i < rebarTensions.Length; i++)
                    Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
                        $"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

                for (int i = 0; i < concreteTensions.Length; i++)
                    Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

                ForceTuple calculatedForces = solver.CalculateSectionForceResultant(result.StrainPlane);

                var adimForces = solver.ConvertToAdimForces(calculatedForces);
                double tolerance = 1e-5;

                if (Math.Abs(adimForces.N - adimExternalForces.N) > tolerance ||
                    Math.Abs(adimForces.Mx - adimExternalForces.Mx) > tolerance ||
                    Math.Abs(adimForces.My - adimExternalForces.My) > tolerance)
                    return false;
            }
            else
            {
                Console.WriteLine($"Result {result.Id} associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} don't find strain plane." +
                    $"Point is external");
            }

            return true;
        }

        protected bool LinearAnalysisCommonAssertModelCode(double phi, StressAnalysisResult result)
        {
            List<string> log = result.GetLog();
            foreach (string s in log)
                Console.WriteLine($"{s}");

            if (log.Count > 0)
                return false;

            if (result.StrainPlane != null)
            {
                (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension(phi);
                (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension(phi);

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

        protected bool CommonAssertDomainPointMethod(IConcreteSection section, ResultBeamForces force, StandardModelCode2010 standard,
            GPC.Checkers.Concrete.Checkers.SectionChecker.SectionOptions options, double adimTolerance = 0.005,
            double[] factor = null)
        {
            if (factor == null)
                factor = new double[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 };

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);
            FailureDomain.FailureDomainPoint[] failureDomainPoints = new FailureDomain.FailureDomainPoint[factor.Length];
            ResultBeamForces[] testForces = new ResultBeamForces[factor.Length];
            int j = 0;

            try
            {
                for (j = 0; j < factor.Length; j++)
                {
                    if (options.FailureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
                        testForces[j] = new ResultBeamForces(factor[j] * force.N, 0, 0, 0, factor[j] * force.M1, factor[j] * force.M2, force.CoordinateSystem);
                    else if (options.FailureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantN)
                        testForces[j] = new ResultBeamForces(force.N, 0, 0, 0, factor[j] * force.M1, factor[j] * force.M2, force.CoordinateSystem);
                    else if (options.FailureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantMxMy)
                        testForces[j] = new ResultBeamForces(factor[j] * force.N, 0, 0, 0, force.M1, force.M2, force.CoordinateSystem);
                    else if (options.FailureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantNMx)
                        testForces[j] = new ResultBeamForces(force.N, 0, 0, 0, force.M1, factor[j] * force.M2, force.CoordinateSystem);
                    else if (options.FailureAnalysisType == SectionSolver.FailureAnalysisTypes.ConstantNMy)
                        testForces[j] = new ResultBeamForces(force.N, 0, 0, 0, factor[j] * force.M1, force.M2, force.CoordinateSystem);

                    failureDomainPoints[j] = solver.CalculatePlasticDomainPointTest(testForces[j].ConvertToForceTuple(options.ForceReferenceCoordinateSystem),
                        options.ForceReferenceCoordinateSystem, options.FailureAnalysisType);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fail to calculate domain point {j}, {e.Message}");
                return false;
            }


            for (int i = 0; i < factor.Length; i++)
            {
                Console.WriteLine($"External Force = {Math.Round(testForces[i].M1 / 1000000)}, " +
                    $"{Math.Round(testForces[i].M2 / 1000000)}, " +
                    $"{Math.Round(testForces[i].N / 1000)}");

                if (failureDomainPoints[i] != null)
                {
                    ForceTuple adimForces = solver.ConvertToAdimForces(new ForceTuple(failureDomainPoints[0].Point.Z - failureDomainPoints[i].Point.Z,
                        failureDomainPoints[0].Point.X - failureDomainPoints[i].Point.X,
                        failureDomainPoints[0].Point.Y - failureDomainPoints[i].Point.Y));

                    Assert.IsTrue(Math.Abs(adimForces.N) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");
                    Assert.IsTrue(Math.Abs(adimForces.Mx) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");
                    Assert.IsTrue(Math.Abs(adimForces.My) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");

                    Console.WriteLine($"Point {i} = {Math.Round(failureDomainPoints[i].Point.X / 1000000, 2)}, " +
                        $"{Math.Round(failureDomainPoints[i].Point.Y / 1000000, 2)}, " +
                        $"{Math.Round(failureDomainPoints[i].Point.Z / 1000, 2)}");
                    Console.WriteLine($"Number of iteraction: {failureDomainPoints[i].StrainPlane.Id} \n");


                    Assert.IsTrue(Math.Abs(adimForces.N) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.Mx) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.My) < adimTolerance);

                    if (force.N != 0)
                        Assert.IsTrue(Math.Sign(force.N) == Math.Sign(failureDomainPoints[i].Point.Z));
                    if (force.M1 != 0)
                        Assert.IsTrue(Math.Sign(force.M1) == Math.Sign(failureDomainPoints[i].Point.X));
                    if (force.M2 != 0)
                        Assert.IsTrue(Math.Sign(force.M2) == Math.Sign(failureDomainPoints[i].Point.Y));
                }
                else
                {
                    Console.WriteLine($"Fail to calculate strain plane for force {i} = {Math.Round(testForces[i].M1 / 1000000)}, " +
                    $"{Math.Round(testForces[i].M2 / 1000000)}, " +
                    $"{Math.Round(testForces[i].N / 1000)}");
                    return false;
                }

            }

            return true;
        }

        protected bool CommonAssertDomainPointMethodFRC(IConcreteSection section, ResultBeamForces force, StandardModelCode2010 standard,
            CoordinateSystem coordinateSystem, double adimTolerance = 0.005, double[] factor = null)
        {
            if (factor == null)
                factor = new double[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5 };

            SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard, true);
            FailureDomain.FailureDomainPoint[] failureDomainPoints = new FailureDomain.FailureDomainPoint[factor.Length];
            ResultBeamForces[] testForces = new ResultBeamForces[factor.Length];
            int j = 0;

            try
            {
                for (j = 0; j < factor.Length; j++)
                {
                    testForces[j] = new ResultBeamForces(factor[j] * force.N, 0, 0, 0, factor[j] * force.M1, factor[j] * force.M2, force.CoordinateSystem);
                    failureDomainPoints[j] = solver.CalculatePlasticDomainPointTest(testForces[j].ConvertToForceTuple(coordinateSystem), coordinateSystem);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fail to calculate domain point {j}, {e.Message}");
                return false;
            }


            for (int i = 0; i < factor.Length; i++)
            {
                Console.WriteLine($"External Force = {Math.Round(testForces[i].M1 / 1000000)}, " +
                    $"{Math.Round(testForces[i].M2 / 1000000)}, " +
                    $"{Math.Round(testForces[i].N / 1000)}");

                if (failureDomainPoints[i] != null)
                {
                    ForceTuple adimForces = solver.ConvertToAdimForces(new ForceTuple(failureDomainPoints[0].Point.Z - failureDomainPoints[i].Point.Z,
                        failureDomainPoints[0].Point.X - failureDomainPoints[i].Point.X,
                        failureDomainPoints[0].Point.Y - failureDomainPoints[i].Point.Y));

                    Assert.IsTrue(Math.Abs(adimForces.N) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");
                    Assert.IsTrue(Math.Abs(adimForces.Mx) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");
                    Assert.IsTrue(Math.Abs(adimForces.My) < adimTolerance,
                        $"Force {Math.Round(force.N / 1000)}, {Math.Round(force.M1 / 1000000)}, {Math.Round(force.M2 / 1000000)} fail");

                    Console.WriteLine($"Point {i} = {Math.Round(failureDomainPoints[i].Point.X / 1000000)}, " +
                        $"{Math.Round(failureDomainPoints[i].Point.Y / 1000000)}, " +
                        $"{Math.Round(failureDomainPoints[i].Point.Z / 1000)}");
                    Console.WriteLine($"Number of iteraction: {failureDomainPoints[i].StrainPlane.Id} \n");

                    Assert.IsTrue(Math.Abs(adimForces.N) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.Mx) < adimTolerance);
                    Assert.IsTrue(Math.Abs(adimForces.My) < adimTolerance);

                    if (force.N != 0)
                        Assert.IsTrue(Math.Sign(force.N) == Math.Sign(failureDomainPoints[i].Point.Z));
                    if (force.M1 != 0)
                        Assert.IsTrue(Math.Sign(force.M1) == Math.Sign(failureDomainPoints[i].Point.X));
                    if (force.M2 != 0)
                        Assert.IsTrue(Math.Sign(force.M2) == Math.Sign(failureDomainPoints[i].Point.Y));
                }
                else
                {
                    Console.WriteLine($"Fail to calculate strain plane for force {i} = {Math.Round(testForces[i].M1 / 1000000)}, " +
                    $"{Math.Round(testForces[i].M2 / 1000000)}, " +
                    $"{Math.Round(testForces[i].N / 1000)}");
                    return false;
                }

            }

            return true;
        }

        protected void ShowDomainPoints(FailureDomain failureDomain)
        {
            for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
                for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
                    Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd / 1000000)}, " +
                                      $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd / 1000000)}, " +
                                      $"{Math.Round(failureDomain.DomainPoints[i][j].NRd / 1000)}");
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

        protected bool CommonAssertsVCA(StressAnalysisResult result, IConcreteSection section, (Point2d rebar, double tension)[] concreteTensionsCalculate,
            (ReinforcedConcreteRebar rebar, double tension)[] rebarTensionsCalculate, double tolerance = 0.05)
        {
            (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension();
            (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension();

            Console.WriteLine($"Tensions associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} ");

            for (int i = 0; i < rebarTensions.Length; i++)
                Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
                    $"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

            for (int i = 0; i < concreteTensions.Length; i++)
                Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

            for (int i = 0; i < section.Shape.Fill.Count; i++)
                if (concreteTensions[i].tension != 0)
                    Assert.IsTrue(Math.Abs((concreteTensions[i].tension - concreteTensionsCalculate[i].tension) / concreteTensions[i].tension) < tolerance);

            for (int i = 0; i < rebarTensions.Length; i++)
                if (rebarTensions[i].tension != 0)
                    Assert.IsTrue(Math.Abs((rebarTensions[i].tension - rebarTensionsCalculate[i].tension) / rebarTensions[i].tension) < tolerance);

            return true;
        }

        protected bool CommonAssertsVCA(double psi, StressAnalysisResult result, IConcreteSection section, (Point2d rebar, double tension)[] concreteTensionsCalculate,
            (ReinforcedConcreteRebar rebar, double tension)[] rebarTensionsCalculate, double tolerance = 0.05)
        {
            (Point2d point, double tension)[] concreteTensions = result.GetConcreteVerticesTension(psi);
            (ReinforcedConcreteRebar rebar, double tension)[] rebarTensions = result.GetRebarsTension(psi);

            Console.WriteLine($"Tensions associated with force {result.Force.N}, {result.Force.M1}, {result.Force.M2} ");

            for (int i = 0; i < rebarTensions.Length; i++)
                Console.WriteLine($"Rebar {i}: {rebarTensions[i].rebar.Position.X}, {rebarTensions[i].rebar.Position.Y}. " +
                    $"Tension = {Math.Round(rebarTensions[i].tension, 2)}");

            for (int i = 0; i < concreteTensions.Length; i++)
                Console.WriteLine($"Vertices {i}: {concreteTensions[i].point}. Tension = {Math.Round(concreteTensions[i].tension, 2)}");

            for (int i = 0; i < section.Shape.Fill.Count; i++)
                if (concreteTensions[i].tension != 0)
                    Assert.IsTrue(Math.Abs((concreteTensions[i].tension - concreteTensionsCalculate[i].tension) / concreteTensions[i].tension) < tolerance);

            for (int i = 0; i < rebarTensions.Length; i++)
                if (rebarTensions[i].tension != 0)
                    Assert.IsTrue(Math.Abs((rebarTensions[i].tension - rebarTensionsCalculate[i].tension) / rebarTensions[i].tension) < tolerance);

            return true;
        }

        protected bool CommonAssertsAbacus(IConcreteSection section, ForceTuple expForce, FailureDomainResult failureDomain, double adimTolerance = 0.05)
        {
            bool check = false;
            double distance = double.MaxValue;
            Point3d nearestPoint = new Point3d();

            for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
            {
                for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
                {
                    double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
                        failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(expForce.Mx / 1000000, expForce.My / 1000000, expForce.N / 1000));

                    Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(expForce);
                    ForceTuple f = new ForceTuple(dist.Z, dist.X, dist.Y);
                    ForceTuple adimForces = CalculateAdimensionalForces(section, f);

                    if (Math.Abs(adimForces.N) < adimTolerance && Math.Abs(adimForces.Mx) < adimTolerance && Math.Abs(adimForces.My) < adimTolerance)
                        check = true;

                    if (d < distance)
                    {
                        nearestPoint = failureDomain.Domain.DomainPoints[i][j].Point;
                        distance = d;
                    }
                }
            }

            Assert.IsTrue(check);

            Console.WriteLine($"Point calculated = {Math.Round(expForce.Mx / 1000000, 1)} KNm, " +
                $"{Math.Round(expForce.My / 1000000, 1)} KNm, {Math.Round(expForce.N / 1000, 1)} KN");

            Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
                $"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");

            return check;
        }

        protected bool CommonAssertsDomainCheck(IConcreteSection section, ForceTuple forceEd, ForceTuple expForce,
            FailureDomain.FailureDomainForce failureDomainForce, double expWR, double errorPercentage = 3)
        {
            Console.WriteLine("Design force");

            Console.WriteLine($"\t Ned = {Math.Round(forceEd.N / 1000, 2)}, " +
                $"MXed = {Math.Round(forceEd.Mx / 1000000, 2)}, " +
                $"MYed = {Math.Round(forceEd.My / 1000000, 2)}");

            Console.WriteLine("Expected force");

            Console.WriteLine($"\t Ned = {Math.Round(expForce.N / 1000, 2)}, " +
                $"MXed = {Math.Round(expForce.Mx / 1000000, 2)}, " +
                $"MYed = {Math.Round(expForce.My / 1000000, 2)}");

            Console.WriteLine("Calculated force on domain");

            Console.WriteLine($"\t NRD = {Math.Round(failureDomainForce.FailureDomainPoint.NRd / 1000, 2)}, " +
                $"MXRD = {Math.Round(failureDomainForce.FailureDomainPoint.MxRd / 1000000, 2)}, " +
                $"MYRD = {Math.Round(failureDomainForce.FailureDomainPoint.MyRd / 1000000, 2)}");

            Vector3d vRd = new Vector3d(failureDomainForce.FailureDomainPoint.Point.X, failureDomainForce.FailureDomainPoint.Point.Y,
                failureDomainForce.FailureDomainPoint.Point.Z);
            Vector3d eRd = new Vector3d(new Point3d(forceEd.Mx, forceEd.My, forceEd.N));


            double wr = eRd.Length / vRd.Length;


            ForceTuple adimForce = CalculateAdimensionalForces(section, new ForceTuple(expForce.N - failureDomainForce.FailureDomainPoint.NRd,
                expForce.Mx - failureDomainForce.FailureDomainPoint.MxRd, expForce.My - failureDomainForce.FailureDomainPoint.MyRd));


            if (Math.Abs(expForce.N - failureDomainForce.FailureDomainPoint.NRd) > 1000000 * errorPercentage && adimForce.N > 0.001 * errorPercentage)
                Assert.IsTrue(Math.Abs((expForce.N - failureDomainForce.FailureDomainPoint.NRd) / failureDomainForce.FailureDomainPoint.NRd) * 100 < errorPercentage);


            if (Math.Abs(expForce.Mx - failureDomainForce.FailureDomainPoint.MxRd) > 1000000 * errorPercentage && adimForce.Mx > 0.001 * errorPercentage)
                Assert.IsTrue(Math.Abs((expForce.Mx - failureDomainForce.FailureDomainPoint.MxRd) / failureDomainForce.FailureDomainPoint.MxRd) * 100 < errorPercentage);


            if (Math.Abs(expForce.My - failureDomainForce.FailureDomainPoint.MyRd) > 1000000 * errorPercentage && adimForce.My > 0.001 * errorPercentage)
                Assert.IsTrue(Math.Abs((expForce.My - failureDomainForce.FailureDomainPoint.MyRd) / failureDomainForce.FailureDomainPoint.MyRd) * 100 < errorPercentage);

            Assert.IsTrue(Math.Abs(wr - expWR) / expWR * 100 < errorPercentage);

            return true;
        }

        protected bool CommonAssertsDomainBoundingBox(IConcreteSection section, FailureDomain failureDomain, Point3d max, Point3d min)
        {
            BoundingBox3d bBox = GetBoundingBox(failureDomain);

            var maxFT = CalculateAdimensionalForces(section,
                new ForceTuple(max.Z + bBox.Min.Z, max.X - bBox.Max.X, 0));
            var minFT = CalculateAdimensionalForces(section,
                new ForceTuple(min.Z + bBox.Max.Z, min.X - bBox.Min.X, 0));

            Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
            Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

            Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
            Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

            return true;
        }

        #endregion

        #region Export To Gmsh

        protected Point3d[] ExportToGmsh(FailureDomain failureDomain, FailureDomain failureDomain2)
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

        protected Point3d[] ExportToGmsh(FailureDomain failureDomain, Line3d[] lines = null, Point3d[] pointsToTest = null)
        {
            GmshNet.Gmsh.Initialize();
            int horizontal = failureDomain.DomainPoints.Length;
            List<Point3d> points = new List<Point3d>();

            for (int i = 0; i < horizontal; i++)
            {
                int vertical = failureDomain.DomainPoints[i].Length;

                for (int j = 0; j < vertical; j++)
                {
                    if (failureDomain.DomainPoints[i][j] != null)
                    {
                        GmshNet.Gmsh.Model.Occ.AddPoint(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                            failureDomain.DomainPoints[i][j].MyRd / 1000000,
                            failureDomain.DomainPoints[i][j].NRd / 1000 / 10);

                        points.Add(new Point3d(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                            failureDomain.DomainPoints[i][j].MyRd / 1000000,
                            failureDomain.DomainPoints[i][j].NRd / 1000 / 10));
                    }

                    points.Add(new Point3d(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                        failureDomain.DomainPoints[i][j].MyRd / 1000000,
                        failureDomain.DomainPoints[i][j].NRd / 1000 / 10));
                }
            }

            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    int t1 = GmshNet.Gmsh.Model.Occ.AddPoint(lines[i].Start.X / 1000000,
                        lines[i].Start.Y / 1000000,
                        lines[i].Start.Z / 1000 / 10);

                    int t2 = GmshNet.Gmsh.Model.Occ.AddPoint(lines[i].End.X / 1000000,
                        lines[i].End.Y / 1000000,
                        lines[i].End.Z / 1000 / 10);

                    GmshNet.Gmsh.Model.Occ.AddLine(t1, t2);
                }
            }

            if (pointsToTest != null)
            {
                for (int i = 0; i < pointsToTest.Length; i++)
                {
                    GmshNet.Gmsh.Model.Occ.AddPoint(pointsToTest[i].X / 1000000,
                        pointsToTest[i].Y / 1000000,
                        pointsToTest[i].Z / 1000 / 10);
                }
            }

            GmshNet.Gmsh.Model.Occ.Synchronize();
            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();

            return points.ToArray();
        }

        protected Point3d[] ExportToGmsh(FailureDomain2d failureDomain, Line3d[] lines = null, Point3d[] pointsToTest = null)
        {
            GmshNet.Gmsh.Initialize();
            List<Point3d> points = new List<Point3d>();

            for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
            {
                if (failureDomain.DomainPoints[i] != null)
                {
                    GmshNet.Gmsh.Model.Occ.AddPoint(failureDomain.DomainPoints[i].MxRd / 1000000,
                        failureDomain.DomainPoints[i].MyRd / 1000000,
                        failureDomain.DomainPoints[i].NRd / 1000 / 10);

                    points.Add(new Point3d(failureDomain.DomainPoints[i].MxRd / 1000000,
                        failureDomain.DomainPoints[i].MyRd / 1000000,
                        failureDomain.DomainPoints[i].NRd / 1000 / 10));
                }

                points.Add(new Point3d(failureDomain.DomainPoints[i].MxRd / 1000000,
                    failureDomain.DomainPoints[i].MyRd / 1000000,
                    failureDomain.DomainPoints[i].NRd / 1000 / 10));
            }

            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    int t1 = GmshNet.Gmsh.Model.Occ.AddPoint(lines[i].Start.X / 1000000,
                        lines[i].Start.Y / 1000000,
                        lines[i].Start.Z / 1000 / 10);

                    int t2 = GmshNet.Gmsh.Model.Occ.AddPoint(lines[i].End.X / 1000000,
                        lines[i].End.Y / 1000000,
                        lines[i].End.Z / 1000 / 10);

                    GmshNet.Gmsh.Model.Occ.AddLine(t1, t2);
                }
            }

            if (pointsToTest != null)
            {
                for (int i = 0; i < pointsToTest.Length; i++)
                {
                    GmshNet.Gmsh.Model.Occ.AddPoint(pointsToTest[i].X / 1000000,
                        pointsToTest[i].Y / 1000000,
                        pointsToTest[i].Z / 1000 / 10);
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
                MeshVertex[] vertices = mesh.GetFaceVertices(mesh.Faces[i]);
                List<int> indicesV = new List<int>();
                List<int> indicesL = new List<int>();

                for (int k = 0; k < vertices.Length; k++)
                {
                    try
                    {
                        indicesV.Add(GmshNet.Gmsh.Model.Occ.AddPoint(vertices[k].Point.X / 1000000,
                            vertices[k].Point.Y / 1000000,
                            vertices[k].Point.Z / 10000));
                    }
                    catch { }
                }

                for (int k = 0; k < indicesV.Count; k++)
                {
                    try
                    {
                        if (k != indicesV.Count - 1)
                            indicesL.Add(GmshNet.Gmsh.Model.Occ.AddLine(indicesV[k], indicesV[k + 1]));
                        else
                            indicesL.Add(GmshNet.Gmsh.Model.Occ.AddLine(indicesV[k], indicesV[0]));
                    }
                    catch { }
                }

                try
                {
                    int wire = GmshNet.Gmsh.Model.Occ.AddWire(indicesL.ToArray());
                    GmshNet.Gmsh.Model.Occ.AddPlaneSurface(new int[] { wire });
                }
                catch { }
            }

            GmshNet.Gmsh.Model.Occ.Synchronize();

            GmshNet.Gmsh.Model.Mesh.Generate(0);
            GmshNet.Gmsh.Model.Mesh.Generate(1);
            //GmshNet.Gmsh.Model.Mesh.Generate(2);

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

        protected Point3d[] ExportToGmsh(FailureDomain[] failureDomains)
        {
            GmshNet.Gmsh.Initialize();
            List<Point3d> points = new List<Point3d>();
            for (int f = 0; f < failureDomains.Length; f++)
            {
                FailureDomain failureDomain = failureDomains[f];
                int horizontal = failureDomain.DomainPoints.Length;

                for (int i = 0; i < horizontal; i++)
                {
                    int vertical = failureDomain.DomainPoints[i].Length;

                    for (int j = 0; j < vertical; j++)
                    {
                        if (failureDomain.DomainPoints[i][j] != null)
                        {
                            GmshNet.Gmsh.Model.Occ.AddPoint(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                                failureDomain.DomainPoints[i][j].MyRd / 1000000,
                                failureDomain.DomainPoints[i][j].NRd / 1000 / 10);

                            points.Add(new Point3d(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                                failureDomain.DomainPoints[i][j].MyRd / 1000000,
                                failureDomain.DomainPoints[i][j].NRd / 1000 / 10));
                        }

                        points.Add(new Point3d(failureDomain.DomainPoints[i][j].MxRd / 1000000,
                            failureDomain.DomainPoints[i][j].MyRd / 1000000,
                            failureDomain.DomainPoints[i][j].NRd / 1000 / 10));
                    }
                }

                GmshNet.Gmsh.Model.Occ.Synchronize();

            }
            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();
            return points.ToArray();
        }

        #endregion

        internal class SectionSolverModelCode2010Test : SectionSolverModelCode2010
        {
            internal SectionSolverModelCode2010Test(IConcreteSection section, StandardModelCode2010 standard,
                bool considerTensileConcrete = false, int id = -1)
                : base(section, standard, considerTensileConcrete, id)
            {
            }

            internal ForceTuple CalculateSectionForceResultant(StrainPlane strainPlane)
            {
                return base.CalculateForceResultant(strainPlane, GetRebarIsInsideAssociation());
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

            internal FailureDomain.FailureDomainPoint CalculatePlasticDomainPointTest(ForceTuple targetLocalForces, CoordinateSystem coordinateSystem,
                FailureAnalysisTypes failureAnalysisType = FailureAnalysisTypes.ConstantEccentricity)
            {
                return base.CalculatePlasticDomainPoint(targetLocalForces, coordinateSystem, failureAnalysisType);
            }

            internal ForceTuple IntegrateSectionStressTest(StrainPlane strainPlane)
            {
                return base.IntegrateSectionStress(strainPlane);
            }
        }
    }
}
