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
using System.Diagnostics;
using GPC.Checkers.Concrete.Helper;

namespace ConcreteTests
{
	[TestClass]
	public class ValidationTestFailureDomain : ConcreteTestBase
	{

		[TestMethod]
		public void VSS_AB1_1()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.4;
			double u = 0.2;
			double omega = 0.25;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[] 
			{   
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h) 
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, h - h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, h - h/10, 0))
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = - v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void VSS_AB1_2()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 400;
			double h = 800;

			// dati presi dall'abaco
			double v = 0.41;
			double u = 0.25;
			double omega = 0.37;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, h - h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, h - h/10, 0))
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void VSS_AB1_3()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.8;
			double u = 0.15;
			double omega = 0.36;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C50_60;
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, 0 + h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h/10, h - h/10, 0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h/10, h - h/10, 0))
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");

			Assert.IsTrue(check);
		}

		[TestMethod]
		public void VSS_AB2_1()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300.0;
			double h = 1000.0;

			// dati presi dall'abaco
			double v = 0.42;
			double u = 0.15;
			double omega = 0.17;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,		0 + h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,				0 + h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,		0 + h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,		h - h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,				h - h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,		h - h / 10.0,		0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,		h / 2.0,			0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,		h / 2.0,			0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void VSS_AB2_2()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300.0;
			double h = 500.0;

			// dati presi dall'abaco
			double v = 0.42;
			double u = 0.15;
			double omega = 0.17;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void VSS_AB2_3()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300.0;
			double h = 500.0;

			// dati presi dall'abaco
			double v = 0.80;
			double u = 0.20;
			double omega = 0.60;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(Ms / 1000000, 0, Ns / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(Ms, 0, Ns);
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

			Console.WriteLine($"Point calculated = {Math.Round(Ms / 1000000, 1)} KNm, " +
				$"0.0 KNm, {Math.Round(Ns / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB1_1()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.40;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB1_2()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.40;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB1_3()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.175;
			double omega = 0.60;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB1_4()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 500;
			double h = 1200;

			// dati presi dall'abaco
			double v = 0.1;
			double uy = 0.075;
			double ux = 0.175;
			double omega = 0.60;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB2_1()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.4;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.45;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB2_2()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.4;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.45;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB2_3()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.075;
			double omega = 0.30;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

		[TestMethod]
		public void CV_AB2_4()
		{
			double adimTolerance = 0.05;
			bool check = false;

			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.075;
			double omega = 0.30;

			// sezione rettangolare 300x500
			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(b, 0),
				new Point2d(b, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);


			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, rebarMaterial);

			int k = 0;
			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        0 + h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b / 2.0,             h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h - h / 10.0,       0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(0 + h / 10.0,        h / 2.0,            0)),
				new ReinforcedConcreteRebar(k++, rebar, new Point3d(b - h / 10.0,        h / 2.0,            0)),
			};


			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			double distance = double.MaxValue;
			Point3d nearestPoint = new Point3d();

			for (int i = 0; i < failureDomain.Domain.DomainPoints.GetLength(0); i++)
			{
				for (int j = 0; j < failureDomain.Domain.DomainPoints[i].GetLength(0); j++)
				{
					double d = new Point3d(failureDomain.Domain.DomainPoints[i][j].Point.X / 1000000, failureDomain.Domain.DomainPoints[i][j].Point.Y / 1000000,
						failureDomain.Domain.DomainPoints[i][j].Point.Z / 1000).DistanceTo(new Point3d(MxRd / 1000000, 0, NRd / 1000));

					Point3d dist = failureDomain.Domain.DomainPoints[i][j].Point - new Point3d(MxRd, MyRd, NRd);
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

			Console.WriteLine($"Point calculated = {Math.Round(MxRd / 1000000, 1)} KNm, " +
				$"{Math.Round(MyRd / 1000000, 1)} KNm, {Math.Round(NRd / 1000, 1)} KN");

			Console.WriteLine($"Nearest point = {Math.Round(nearestPoint.X / 1000000, 1)} KNm, " +
				$"{Math.Round(nearestPoint.Y / 1000000, 1)} KNm, {Math.Round(nearestPoint.Z / 1000, 1)} KN");
		}

	}
}
