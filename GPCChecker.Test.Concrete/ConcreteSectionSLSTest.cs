using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.Checkers.Concrete.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;

namespace ConcreteTests
{
	[TestClass]
	public class ConcreteSectionSLSTest
	{
		protected virtual void CalculateAdimensionalForces(IConcreteSection section, ResultBeamForces forces, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = section.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;
			double fck = section.ConcreteMaterial.StressStrainTableCompression.GetMaximumStress();

			adimAxialForce = forces.N / (b * h * fck);
			adimBendingMomentX = forces.M1 / (b * h * h * fck);
			adimBendingMomentY = forces.M2 / (b * b * h * fck);
		}

		protected virtual void CalculateAdimensionalForces(IConcreteSection section, double N, double Mx, double My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY)
		{
			BoundingBox3d bBox = section.Shape.GetBoundingBox();
			double h = bBox.Size.Y;
			double b = bBox.Size.X;
			double fck = section.ConcreteMaterial.StressStrainTableCompression.GetMaximumStress();

			adimAxialForce = N / (b * h * fck);
			adimBendingMomentX = Mx / (b * h * h * fck);
			adimBendingMomentY = My / (b * b * h * fck);
		}

		protected bool CommonAssertModelCode(StressAnalysisResult result, IConcreteSection section, ResultBeamForces forces, StrainPlane strainPlane, StandardModelCode2010 standard,
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions, double tolerance = 1e-5)
		{
			//CalculateAdimensionalForces(section, forces, out double adimExternalAxialForce, out double adimExternalendingMomentX, out double adimExternalBendingMomentY);

			//SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			//SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			//SectionSolverModelCode2010 solver = new GPC.Checkers.Concrete.SectionSolvers.SectionSolverModelCode2010(section, standard);

			//sectionChecker.CalculateForceResultant(strainPlane);
			//CalculateAdimensionalForces(section, N, Mx, My, out double adimAxialForce, out double adimBendingMomentX, out double adimBendingMomentY);
			//if (Math.Abs(adimAxialForce - adimExternalAxialForce) > tolerance ||
			//	Math.Abs(adimBendingMomentX - adimExternalendingMomentX) > tolerance ||
			//	Math.Abs(adimBendingMomentY - adimExternalBendingMomentY) > tolerance)
			//	return false;

			//double[] concreteTensions = result.GetVerticesTension();

			//for (int i = 0; i < concreteTensions.Length; i++)
			//	Console.WriteLine($"Vertices {i} = {concreteTensions[i]}");

			return true;
		}

		[TestMethod]
		public void SezioneRettangolareTest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 *  1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
			//Assert.IsTrue(CommonAssert(result, section, forces, result.StrainPlane, standard));
		}

		[TestMethod]
		public void SezioneRettangolareTest2()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 * 1000, 0, 0, 0, 0 * 1000000, 10 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SezioneRettangolareTest3()
		{
			double rebarDiameter = 18;
			double height = 800;
			double width = 400;
			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(width, 0),
																		new Point2d(width, height),
																		new Point2d(0, height) }));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialEN1992.C45_55);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,750,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200,750,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350,750,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SezioneQuadrataTest1()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 300),
																		new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(100 * 1000, 0, 0, 0, 10 * 1000000, 0 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SezioneQuadrataTest2()
		{
			double rebarDiameter = 20;

			// sezione rettangolare 300x300
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 300),
																		new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(-100 * 1000, 0, 0, 0, 20 * 1000000, 10 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}

		[TestMethod]
		public void SezioneQuadrataTest2Precompressa()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 20;

			// sezione rettangolare 300x300
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																	new Point2d(300, 0),
																	new Point2d(300, 300),
																	new Point2d(0, 300) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial(200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 250,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 250,0)),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 150, 0), 0.007045)};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ResultBeamForces forces = new ResultBeamForces(0 * 1000, 0, 0, 0, 50 * 1000000, 20 * 1000000, CoordinateSystem.Global);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, new ResultBeamForces[] { forces }, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			StressAnalysisResult[] slsResult = sectionChecker.GetStressAnalysisResult();
		}


	}
}
