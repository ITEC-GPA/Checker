using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Checkers.ReinforcedConcrete.Results;
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

			for (int i = 0; i < section.Shape.Fill.Count; i++)
			{
				GmshNet.Gmsh.Model.Occ.AddPoint(section.Shape.Fill[i].X, section.Shape.Fill[i].Y, section.Shape.Fill[i].Z);
			}

			if (section.Shape.HasHoles)
			{
				for (int i = 0; i < section.Shape.Holes.Length; i++)
				{
					for (int j = 0; j < section.Shape.Holes[i].Count; j++)
					{
						GmshNet.Gmsh.Model.Occ.AddPoint(section.Shape.Holes[i][j].X, section.Shape.Holes[i][j].Y, section.Shape.Holes[i][j].Z);
					}
				}
			}

			for (int i = 0; i < section.Rebars.Length; i++)
			{
				GmshNet.Gmsh.Model.Occ.AddPoint(section.Rebars[i].Position.X, section.Rebars[i].Position.Y, section.Rebars[i].Position.Z);
			}

			GmshNet.Gmsh.Model.Occ.Synchronize();
			GmshNet.Gmsh.Fltk.Run();
			GmshNet.Gmsh.Finalize();
		}

		private bool CommonAssertsEN(ReinforcedConcreteSection section, StandardEN1992p11 standard, FailureDomain failureDomain)
		{
			List<Point3d> failureDomainPoints = new List<Point3d>();
			BoundingBox3d boundingBox3D = new BoundingBox3d();

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
			{
				for (int j = 0; j < failureDomain.DomainPoints[i].Length; j++)
				{
					boundingBox3D.Update(failureDomain.DomainPoints[i][j].Point);
					failureDomainPoints.Add(failureDomain.DomainPoints[i][j].Point);
				}
			}

			// valore per campo di deformazione 6 (epsilon 0.2% costante)
			double pureCompressionAxialForce = section.ShapeEx.GetArea() * section.ConcreteMaterial.Fck * standard.AlphaCC / standard.GammaC;
			double pureCompressionMomentX = 0;
			double pureCompressionMomentY = 0;

			for (int i = 0; i < section.Rebars.Length; i++)
			{
				pureCompressionAxialForce += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS;
				pureCompressionMomentX += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS *
					(section.Rebars[i].Position.Y - section.Centroid.Y);
				pureCompressionMomentY += section.Rebars[i].Area * section.Rebars[i].RebarMaterial.Fyk / standard.GammaS *
					(section.Rebars[i].Position.X - section.Centroid.X);
			}

			if (Math.Abs(boundingBox3D.Min.X - pureCompressionAxialForce) / boundingBox3D.Min.X * 100 < 2.5)
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

			if (Math.Abs(boundingBox3D.Max.X - pureTractionAxialForce) / boundingBox3D.Max.X * 100 < 2.5)
				return false;



			return true;
		}

		[TestMethod]
        public void FailureDomainRectangularSectionTest1()
        {
            double rebarDiameter = 18;

            // sezione rettangolare 300x500
            Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
                                                                    new Point3d(300, 0, 0),
                                                                    new Point3d(300, 500, 0),
                                                                    new Point3d(0, 500, 0), }));

            ShapeEx shapeEx = new ShapeEx(shape, new ConcreteMaterialEN1992(25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new RebarMaterial(450));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[] {  new ReinforcedConcreteRebar(rebar, new Point3d(50,50,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250, 50, 0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(250,450,0)),
                                                                                new ReinforcedConcreteRebar(rebar, new Point3d(50,450,0))};

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx, rebars);
			StandardEN1992p11 standard = new StandardEN1992p11();
			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, standard);

            int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4};

            FailureDomain failureDomain = solver.CalculateFailureDomain(64, subd);
            ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[i].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd, 3)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd, 3)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd, 3)}");

			//Assert.IsTrue(CommonAssertsEN(section, standard, failureDomain));

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
		public void FailureDomainRectangularSectionTest2()
		{
			double rebarDiameter = 26;

			// sezione rettangolare 300x500
			Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
																	new Point3d(300, 0, 0),
																	new Point3d(300, 500, 0),
																	new Point3d(0, 500, 0), }));

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

			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new StandardEN1992p11());

			int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4 };

			FailureDomain failureDomain = solver.CalculateFailureDomain(64, subd);
			ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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
			Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
																	new Point3d(300, 0, 0),
																	new Point3d(300, 500, 0),
																	new Point3d(0, 500, 0), }));

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

			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new StandardEN1992p11());

			int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4 };

			FailureDomain failureDomain = solver.CalculateFailureDomain(64, subd);
			ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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
			Shape shape = new Shape(new Polygon3d(new Point3d[] {   new Point3d(0, 0, 0),
																	new Point3d(300, 0, 0),
																	new Point3d(300, 500, 0),
																	new Point3d(0, 500, 0), }));

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

			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new StandardEN1992p11());

			int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4 };

			FailureDomain failureDomain = solver.CalculateFailureDomain(64, subd);
			ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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

			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new StandardEN1992p11());

			int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4 };

			FailureDomain failureDomain = solver.CalculateFailureDomain(32, subd);
			ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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

			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);
			ConcreteSectionCHS section = new ConcreteSectionCHS(diameterExternal, thickness, ConcreteMaterialEN1992.C25_30, concreteCover, numberOfRebars, rebar);

			ConcreteSectionSolverEN1992 solver = new ConcreteSectionSolverEN1992(section, new StandardEN1992p11());

			int[] subd = new int[] { 1, 1, 1, 50, 2, 1, 4 };

			FailureDomain failureDomain = solver.CalculateFailureDomain(32, subd);
			ExportToGmsh(section);
			ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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
	}
}


































