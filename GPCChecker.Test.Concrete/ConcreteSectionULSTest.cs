using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Results;
using System.Collections.Generic;
using GPC.Model.Standards;

namespace ConcreteTests
{
	[TestClass]
	public class ConcreteSectionULSTest
	{
		private Point3d[] ExportToGmsh(FailureDomain failureDomain)
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

		private void ExportToGmsh(IConcreteSection section)
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

			for (int i = 0; i < section.Rebars.Length; i++)
			{
				GmshNet.Gmsh.Model.Occ.AddPoint(section.Rebars[i].Position.X, section.Rebars[i].Position.Y, 0.0);
			}

			GmshNet.Gmsh.Model.Occ.Synchronize();
			GmshNet.Gmsh.Fltk.Run();
			GmshNet.Gmsh.Finalize();
		}

		private void ShowDomainPoints(FailureDomain failureDomain)
		{
			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");
		}

		private bool CommonAssertsModelCode(IConcreteSection section, StandardModelCode2010 standard, FailureDomain failureDomain, double errorPercentage = 5.0)
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

			for (int i = 0; i < section.Rebars.Length; i++)
			{
				pureCompressionAxialForce += section.Rebars[i].Area * (section.Rebars[i].RebarMaterial.Fyk / standard.GammaS -
					((ConcreteMaterialModelCode2010)section.ConcreteMaterial).Fck * standard.AlphaCC / standard.GammaC);
				pureCompressionMomentX += section.Rebars[i].Area * (section.Rebars[i].RebarMaterial.Fyk / standard.GammaS) * 
					(section.Rebars[i].Position.Y - section.Centroid.Y);
				pureCompressionMomentY += section.Rebars[i].Area * (section.Rebars[i].RebarMaterial.Fyk / standard.GammaS) *
					(section.Rebars[i].Position.X - section.Centroid.X);
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

			for (int i = 0; i < section.Rebars.Length; i++)
			{
				pureTractionAxialForce += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS;
				pureTractionMomentX += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS *
					(section.Rebars[i].Position.Y - section.Centroid.Y);
				pureTractionMomentY += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS *
					(section.Rebars[i].Position.X - section.Centroid.X);
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

		[TestMethod]
        public void FailureDomainRectangularSectionTest1()
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
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

            FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			ExportToGmsh(failureDomain);
			
			//Assert.IsTrue(CommonAssertsModelCode (section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-382.775	3.84247		0			0
				-351.973	11.3133		0			0
				-321.43		18.5065		0			0
				-293.415	24.8945		0			0
				-48.0393	75.0195		0			0
				396.513		159.786		0			0
				495.779		173.896		0			0
				661.413		192.136		0			0
				993.368		208.726		0			0
				1234.54		191.47		0			0
				1539.64		162.899		0			0
				1955.48		104.992		0			0
				2236.37		53.6365		0			0
				2427.3		18.3439		0			0
				2523.84		0			0			0
				2523.84		0			0			0
				2427.3		-18.3439	0			0
				2236.37		-53.6365	0			0
				1955.48		-104.992	0			0
				1539.64		-162.899	0			0
				1234.54		-191.47		0			0
				993.368		-208.726	0			0
				661.413		-192.136	0			0
				495.779		-173.896	0			0
				396.513		-159.786	0			0
				-48.0393	-75.0195	0			0
				-293.415	-24.8945	0			0
				-321.43		-18.5065	0			0
				-351.973	-11.3133	0			0
				-382.775	-3.84247	0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
				-398.343	0			0			0
			*/
		}

		[TestMethod]
		public void StrainPlaneRectangularSectionTest1()
		{
			double rebarDiameter = 18;
			double tolerance = 5;     // tolleranza percentuale

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
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			StrainPlane strainPlane = new StrainPlane(0.0, 0.0, section.Centroid, -0.0002);
			solver.CalculateForces(strainPlane, out double N, out double Mx, out double My);

			Assert.IsTrue(Math.Abs(Mx) < tolerance);
			Assert.IsTrue(Math.Abs(My) < tolerance);
			Assert.IsTrue(Math.Abs((Math.Abs(N) - 440000) / N) * 100< tolerance);	// 440 kN calcolato con VCA
		}

		[TestMethod]
		public void StrainPlaneRectangularSectionTest2()
		{
			double rebarDiameter = 18;
			double tolerance = 5;       // tolleranza percentuale

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
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			StrainPlane strainPlane = new StrainPlane(0.0, 0.0, section.Centroid, -0.0004539);
			solver.CalculateForces(strainPlane, out double N, out double Mx, out double My);

			Assert.IsTrue(Math.Abs(Mx) < tolerance);
			Assert.IsTrue(Math.Abs(My) < tolerance);
			Assert.IsTrue(Math.Abs((Math.Abs(N) - 1000000) / N) * 100 < tolerance); // 1000 kN calcolato con VCA
		}

		[TestMethod]
		public void StrainPlaneRectangularSectionTest3()
		{
			double rebarDiameter = 18;
			double tolerance = 5;     // tolleranza percentuale

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
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			StrainPlane strainPlane = new StrainPlane(0.0, 0.0000036072, section.Centroid, 0.00048156);
			solver.CalculateForces(strainPlane, out double N, out double Mx, out double My);

			Assert.IsTrue(Math.Abs((Math.Abs(Mx) - 50000000) / Mx) * 100 < tolerance);  // 50 kNm calcolato con VCA
			Assert.IsTrue(Math.Abs(My) < tolerance);
			Assert.IsTrue(Math.Abs(N) < tolerance); 
		}

		[TestMethod]
		public void FailureDomainRectangularSectionTest2()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			ExportToGmsh(failureDomain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1210.06	-161.031	0			0
				-1154.63	-147.587	0			0
				-1099.66	-134.642	0			0
				-1049.25	-123.146	0			0
				23.2394		93.24		0			0
				1553.29		391.791		0			0
				1731.92		417.183		0			0
				2029.99		450.008		0			0
				2627.37		479.864		0			0
				2998.8		461.326		0			0
				3485.28		422.426		0			0
				4171.02		330.734		0			0
				4631.74		247.27		0			0
				4930.56		192.712		0			0
				5063.07		167.946		0			0
				5063.07		167.946		0			0
				4713.18		99.7023		0			0
				4178.29		-2.06773	0			0
				3481.51		-132.744	0			0
				2465.78		-290.433	0			0
				1649.33		-395.329	0			0
				947.912		-479.864	0			0
				350.534		-450.008	0			0
				52.4623		-417.183	0			0
				-126.174	-391.791	0			0
				-755.328	-273.418	0			0
				-1049.25	-212.745	0			0
				-1099.66	-201.25		0			0
				-1154.63	-188.305	0			0
				-1210.06	-174.861	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
				-1238.07	-167.946	0			0
			*/
		}

		[TestMethod]
		public void FailureDomainRectangularSectionTest3()
		{
			double rebarDiameter = 18;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,70,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 450, 0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			// ExportToGmsh(failureDomain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1177.11	152.928		0			0
				-1146.31	160.399		0			0
				-1115.77	167.592		0			0
				-1087.75	173.98		0			0
				-842.741	224.032		0			0
				-398.608	308.714		0			0
				-299.343	322.824		0			0
				-133.709	341.065		0			0
				259.844		346.567		0			0
				804.641		272.319		0			0
				1413.36		186.756		0			0
				2132.81		71.857		0			0
				2625.26		-19.253		0			0
				3027.75		-94.3		0			0
				3318.18		-149.085	0			0
				3318.18		-149.085	0			0
				3221.74		-167.409	0			0
				3030.92		-202.679	0			0
				2750.14		-254.013	0			0
				2334.46		-311.889	0			0
				2029.51		-340.43		0			0
				1788.49		-357.655	0			0
				1456.54		-341.065	0			0
				1250.91		-315.626	0			0
				1043.42		-282.035	0			0
				-475.611	2.52598		0			0
				-1087.75	124.191		0			0
				-1115.77	130.579		0			0
				-1146.31	137.772		0			0
				-1177.11	145.243		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
				-1192.68	149.085		0			0
			*/
		}

		[TestMethod]
		public void FailureDomainRectangularSectionTest4()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,70,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 70, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,430,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 430, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 430, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 430, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 430, 0)),};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section,standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			// ExportToGmsh(failureDomain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4127.59	6.91482		0			0
				-4072.16	20.3592		0			0
				-4017.19	33.3038		0			0
				-3966.78	44.7995		0			0
				-2704.73	295.308		0			0
				403.698		877.967		0			0
				808.584		944.084		0			0
				1190.26		991.959		0			0
				1916.42		998.635		0			0
				3007.94		843.881		0			0
				4214.5		668.766		0			0
				5620.33		440.858		0			0
				6584.38		262.073		0			0
				7386.54		112.195		0			0
				7980.61		0			0			0
				7980.61		0			0			0
				7386.54		-112.195	0			0
				6584.38		-262.073	0			0
				5620.33		-440.858	0			0
				4214.5		-668.766	0			0
				3007.94		-843.881	0			0
				1916.42		-998.635	0			0
				1190.26		-991.959	0			0
				808.584		-944.084	0			0
				403.698		-877.967	0			0
				-2704.73	-295.308	0			0
				-3966.78	-44.7995	0			0
				-4017.19	-33.3038	0			0
				-4072.16	-20.3592	0			0
				-4127.59	-6.91482	0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
				-4155.61	0			0			0
			*/
		}

		[TestMethod]
		public void FailureDomainRectangularSectionPrestressedTest1()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 20;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial(200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),																				
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 0.007045)};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			//ExportToGmsh(failureDomain);

			//// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-905.882	73.2787		0			0
				-850.452	86.7231		0			0
				-795.487	99.6677		0			0
				-745.072	111.163		0			0
				-407.98		180.471		0			0
				271.13		308.835		0			0
				449.766		334.227		0			0
				747.837		367.052		0			0
				1388.85		390.363		0			0
				1821.5		361.728		0			0
				2369.18		312.73		0			0
				3116.14		210.941		0			0
				3617.17		120.777		0			0
				3956.3		59.5186		0			0
				4128.07		28.26		0			0
				4128.07		28.26		0			0
				4000.26		-1.42864	0			0
				3705.09		-61.1167	0			0
				3248.02		-149.711	0			0
				2533.25		-243.453	0			0
				2017.76		-284.403	0			0
				1617.29		-304.991	0			0
				981.689		-269.402	0			0
				645.392		-230.843	0			0
				428.53		-199.717	0			0
				-407.98		-47.7433	0			0
				-745.072	21.5644		0			0
				-795.487	33.0601		0			0
				-850.452	46.0048		0			0
				-905.882	59.4491		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
				-933.899	66.3639		0			0
			*/
		}

		[TestMethod]
		public void FailureDomainRectangularSectionPrestressedTest2()
		{
			double rebarDiameter = 20;
			double rebarDiameterPrestress = 20;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(300, 0),
																		new Point2d(300, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial(200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 100, 0), 0.007045),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(150, 400, 0), 0.007045)};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			ExportToGmsh(failureDomain);

			//// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1348.31	6.91482		0			0
				-1292.88	20.3592		0			0
				-1237.91	33.3038		0			0
				-1187.5		44.7995		0			0
				-850.406	114.107		0			0
				-139.496	247.241		0			0
				77.3662		278.367		0			0
				413.663		316.926		0			0
				1049.27		352.515		0			0
				1449.73		331.927		0			0
				1969.1		290.397		0			0
				2726.78		190.217		0			0
				3211.06		97.5407		0			0
				3533.44		33.7706		0			0
				3688.47		0			0			0
				3688.47		0			0			0
				3533.44		-33.7706	0			0
				3211.06		-97.5407	0			0
				2726.78		-190.217	0			0
				1969.1		-290.397	0			0
				1449.73		-331.927	0			0
				1049.27		-352.515	0			0
				413.663		-316.926	0			0
				77.3662		-278.367	0			0
				-139.496	-247.241	0			0
				-850.406	-114.107	0			0
				-1187.5		-44.7995	0			0
				-1237.91	-33.3038	0			0
				-1292.88	-20.3592	0			0
				-1348.31	-6.91482	0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
				-1376.33	0			0			0
			*/
		}

		[TestMethod]
		public void FailureDomainCircularSectionTest1()
		{
			// sezione circolare diametro 500
			double rebarDiameter = 16;
			double sectionDiameter = 500;

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(450, 250, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(434.77591, 326.536678, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(391.421355, 391.421357, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(326.536686, 434.775907, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(173.463322, 434.77591, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(108.578643, 391.421355, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(65.224093, 326.536686, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(50, 250, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(65.22409, 173.463322, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(108.578645, 108.578643, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(173.463314, 65.224093, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(250.0, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(326.536678, 65.22409, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(391.421357, 108.578645, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(434.775907, 173.463314, 0)) };


			ConcreteSectionCircular section = new ConcreteSectionCircular(sectionDiameter, 
				new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle), rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			// ExportToGmsh(failureDomain);

			//// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1258.81	4.84e-014	0			0
				-1251.8		1.72729		0			0
				-1230.47	6.88051		0			0
				-1200.35	13.9574		0			0
				-1166.33	21.7031		0			0
				-947.661	66.0554		0			0
				14.6662		233.636		0			0
				427.718		287.822		0			0
				1083.72		350.511		0			0
				2479.81		388.665		0			0
				3208.22		357.463		0			0
				4100.23		295.198		0			0
				5147.35		172.755		0			0
				5689.17		88.0575		0			0
				6064.06		29.8863		0			0
				6256.29		-4.84e-014	0			0
				6256.29		-5.21e-014	0			0
				6064.06		-29.8863	0			0
				5689.17		-88.0575	0			0
				5147.35		-172.755	0			0
				4100.23		-295.198	0			0
				3208.22		-357.463	0			0
				2479.81		-388.665	0			0
				1083.72		-350.511	0			0
				427.718		-287.822	0			0
				14.6662		-233.636	0			0
				-947.661	-66.0554	0			0
				-1166.33	-21.7031	0			0
				-1200.35	-13.9574	0			0
				-1230.47	-6.88051	0			0
				-1251.8		-1.72729	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
				-1258.81	5.21e-014	0			0
			*/
		}

		[TestMethod]
		public void FailureDomainCHSSectionTest1()
		{
			double rebarDiameter = 26;
			double diameterExternal = 1000;
			double thickness = 100;
			double concreteCover = 50;
			int numberOfRebars = 32;

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));
			ConcreteSectionCHS section = new ConcreteSectionCHS(diameterExternal, thickness, ConcreteMaterialEN1992.C25_30, concreteCover, numberOfRebars, rebar);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);

			FailureDomain failureDomain = solver.CalculateFailureDomain();
			ShowDomainPoints(failureDomain);
			// ExportToGmsh(failureDomain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6648.08	-8.94e-014	0			0
				-6631.33	8.24822		0			0
				-6514.99	62.2599		0			0
				-6096.85	249.778		0			0
				-5476.35	520.861		0			0
				-4308.15	1009.75		0			0
				-1593.06	1926.89		0			0
				-739.734	2119.45		0			0
				569.251		2298.39		0			0
				3195.96		2169.78		0			0
				4565.66		1864.64		0			0
				5996.74		1520.36		0			0
				7665.2		1043.25		0			0
				8887.74		612.691		0			0
				9907.84		257.905		0			0
				10647		8.94e-014	0			0
				10647		1.49e-013	0			0
				9907.84		-257.905	0			0
				8887.74		-612.691	0			0
				7665.2		-1043.25	0			0
				5996.74		-1520.36	0			0
				4565.66		-1864.64	0			0
				3195.96		-2169.78	0			0
				569.251		-2298.39	0			0
				-739.734	-2119.45	0			0
				-1593.06	-1926.89	0			0
				-4308.15	-1009.75	0			0
				-5476.35	-520.861	0			0
				-6096.85	-249.778	0			0
				-6514.99	-62.2599	0			0
				-6631.33	-8.24822	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
				-6648.08	-1.49e-013	0			0
			*/
		}

		[TestMethod]
		public void FailureDomainSectionTPrestressedTest1()
		{
			double rebarDiameter = 26;
			double rebarDiameterPrestress = 20;

			// sezion a T tovescia 
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(500, 0),
																		new Point2d(500, 500),
																		new Point2d(400, 500),
																		new Point2d(400, 1000),
																		new Point2d(100, 1000),
																		new Point2d(100, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));
			RebarSectionCircular rebarP = new RebarSectionCircular(rebarDiameterPrestress, new RebarMaterial(200000, 1620, 1800));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(400, 50, 0)),																				
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(400, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350, 950, 0)),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(250, 100, 0), 0.007045),
																				new ReinforcedConcreteRebar(rebarP, new Point3d(250, 100, 0), 0.007045) };

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);			
			FailureDomain failureDomain = solver.CalculateFailureDomain();

			ShowDomainPoints(failureDomain);
			ExportToGmsh(failureDomain);			

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3319.17	217.179		0			0
				-2941.5		414.739		0			0
				-2355.62	717.257		0			0
				-1822.17	991.042		0			0
				-1164.02	1323.95		0			0
				-209.629	1731.12		0			0
				167.497		1857.38		0			0
				1099.1		2029.31		0			0
				3190.77		2218.48		0			0
				4502.39		2106.42		0			0
				6191.82		1893.31		0			0
				8512.78		1402.98		0			0
				10248.5		882.735		0			0
				11445		521.508		0			0
				12065.4		326.229		0			0
				12065.4		326.229		0			0
				11577.1		92.8251		0			0
				10727.7		-286.162	0			0
				9554.07		-801.031	0			0
				7896.03		-1321.98	0			0
				6526.62		-1642.82	0			0
				5614.88		-1716.85	0			0
				2884.1		-1650.42	0			0
				1343.13		-1398.91	0			0
				418.76		-1156.47	0			0
				-2329.51	-230.051	0			0
				-2714.37	-88.2293	0			0
				-2891.41	-18.9784	0			0
				-3085.36	59.9323		0			0
				-3279.8		141.959		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
				-3378.22	184.358		0			0
			*/
		}

		[TestMethod]
		public void FailureDomainSectionTTest1()
		{
			double rebarDiameter = 26;

			// sezion a T tovescia 
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] {   new Point2d(0, 0),
																		new Point2d(500, 0),
																		new Point2d(500, 500),
																		new Point2d(400, 500),
																		new Point2d(400, 1000),
																		new Point2d(100, 1000),
																		new Point2d(100, 500),
																		new Point2d(0, 500) }));

			ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(45, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle));
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(100, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(400, 50, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(100, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(400, 450, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(150, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(200, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(300, 950, 0)),
																				new ReinforcedConcreteRebar(rebar, new Point3d(350, 950, 0))};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);

			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();
			SectionSolverULSModelCode2010 solver = new SectionSolverULSModelCode2010(section, standard);
			FailureDomain failureDomain = solver.CalculateFailureDomain();

			ShowDomainPoints(failureDomain);
			ExportToGmsh(failureDomain);

			// Assert.IsTrue(CommonAssertsModelCode(section, standard, failureDomain));

			/* DOMINIO DI ROTTURA CALCOLATO CON VCA
				NRd			MRd			C3			C4
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2434.32	-81.4583	0			0
				-2056.65	116.101		0			0
				-1470.77	418.62		0			0
				-937.317	692.404		0			0
				-279.167	1025.32		0			0
				675.223		1432.48		0			0
				1052.35		1558.74		0			0
				1983.95		1730.67		0			0
				4075.62		1919.84		0			0
				5387.24		1807.78		0			0
				7076.68		1594.67		0			0
				9348.02		1121.09		0			0
				11014.7		624.156		0			0
				12142.1		286.244		0			0
				12693.4		114.279		0			0
				12693.4		114.279		0			0
				12175.3		-109.057	0			0
				11296		-477.977	0			0
				10092.6		-982.778	0			0
				8455.67		-1510.86	0			0
				7107.4		-1838.84	0			0
				6216.79		-1920		0			0
				3567.49		-1881.07	0			0
				2108.01		-1657.05	0			0
				1265.12		-1442.12	0			0
				-1444.66	-528.688	0			0
				-1829.52	-386.867	0			0
				-2006.56	-317.616	0			0
				-2200.5		-238.705	0			0
				-2394.95	-156.679	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
				-2493.36	-114.279	0			0
			*/
		}
	}
}
