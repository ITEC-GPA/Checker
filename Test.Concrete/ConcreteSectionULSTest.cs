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
			double pureCompressionAxialForce = section.Shape.GetArea() * section.ConcreteMaterial.Fck * standard.AlphaCC / standard.GammaC;
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
        public void FailureDomainTest1()
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

            int[] subd = new int[] { 1, 1, 2, 50, 2, 1, 4};

            FailureDomain failureDomain = solver.CalculateFailureDomain(64, subd);
            ExportToGmsh(failureDomain);

			for (int i = 0; i < failureDomain.DomainPoints.Length; i++)
				for (int j = 0; j < failureDomain.DomainPoints[0].Length; j++)
					Console.WriteLine($"{Math.Round(failureDomain.DomainPoints[i][j].MxRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].MyRd)}, " +
									  $"{Math.Round(failureDomain.DomainPoints[i][j].NRd)}");

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
		public void FailureDomainTest2()
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

			int[] subd = new int[] { 1, 1, 2, 50, 2, 1, 4 };

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
		public void FailureDomainTest3()
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

			int[] subd = new int[] { 1, 1, 2, 50, 2, 1, 4 };

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
	}
}
