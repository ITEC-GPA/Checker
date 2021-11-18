using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Checkers;
using System.Collections.Generic;
using GPC.Model.Standards;
using GPC.Model.Sections;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model.Results;
using GPC.TestUtilities;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Helper;

namespace ConcreteTests
{
	[TestClass]
	public class SectionIntegrationTest : ConcreteTest
	{
		[TestMethod]
		public void RectangularSectionIntegration1()
		{
			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = -0.002;
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ConcreteMaterial concreteMaterial = ConcreteMaterialEN1992.C25_30;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
			ForceTuple expForceTupleNumerics = new ForceTuple(-2500000, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

			Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration2()
		{
			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = -0.002;
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
			ForceTuple expForceTupleNumerics = new ForceTuple(-2500000, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

			Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration3()
		{
			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = -0.000875;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
			ForceTuple expForceTupleNumerics = new ForceTuple(-1250000, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

			Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration4()
		{
			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = +0.0001;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
			ForceTuple expForceTupleNumerics = new ForceTuple(0, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

			Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration5()
		{
			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = +0.000;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] { };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(shapeEx.GetArea() * solver.CalculateSigmaConcrete(strainRefPoint), 0, 0);
			ForceTuple expForceTupleNumerics = new ForceTuple(0, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);

			Assert.IsTrue(Math.Abs(force.N - expForceTupleNumerics.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTupleNumerics.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTupleNumerics.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration6()
		{
			double rebarDiameter = 18;

			double chiX = 0.0;
			double chiY = 0.0;
			double strainRefPoint = +0.01;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.StressBlock));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, section.Centroid, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			double axialForce = 0;
			for (int i = 0; i < rebars.Length; i++)
				axialForce += solver.CalculateStressRebar(rebars[i], strainRefPoint) * rebars[i].Area;

			ForceTuple expForceTuple = new ForceTuple(axialForce, 0, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) < 1);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) < 1);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) < 1);
		}

		[TestMethod]
		public void RectangularSectionIntegration7()
		{
			double chiX = 0.0;
			double chiY = -0.000004;
			double strainRefPoint = +0.0;
			Point2d referencePoint = new Point2d(0,0);

			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("", 450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
				new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			StrainPlane strainPlane = new StrainPlane(chiX, chiY, referencePoint, strainRefPoint);

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			ForceTuple force = solver.CalculateSectionForceResultant(strainPlane);

			ForceTuple expForceTuple = new ForceTuple(-1400 * 1000, 130 * 1000000, 0);

			Assert.IsTrue(Math.Abs(force.N - expForceTuple.N) / Math.Abs(force.N)< 0.01);
			Assert.IsTrue(Math.Abs(force.Mx - expForceTuple.Mx) / Math.Abs(force.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(force.My - expForceTuple.My) / Math.Abs(force.My) < 0.01);
		}

		[TestMethod]
		public void DesignStressSteel()
		{	
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {
				new Point2d(0, 0),
				new Point2d(300, 0),
				new Point2d(300, 500),
				new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial("",200000, 450, 510));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {
				new ReinforcedConcreteRebar(rebar, new Point3d(150,50,0)) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionSolverModelCode2010Test solver = new SectionSolverModelCode2010Test(section, standard);

			List<double> stresses = new List<double>();

			for (double i = -67.5; i <= 67.5; i++)			
				stresses.Add(solver.CalculateStressRebar(rebars[0], i / 1000));
			
			foreach(double stress in stresses)
				Console.WriteLine(stress);

			for (int i = 0; i < stresses.Count - 1; i++)
			{
				Assert.IsTrue(stresses[i] < stresses[i + 1]);
				Assert.IsTrue(Math.Abs(stresses[i]) < rebars[0].RebarMaterial.Fu);
			}
				
			
		}
	}
}
