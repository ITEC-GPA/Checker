using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
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
	public class FailureDomainFRCTest : ConcreteTestBase
	{
		[TestMethod]
		public void RectangularSectionTest1()
		{
			double rebarDiameter = 18;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				1.0, 1.50, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, concreteMaterial);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ShowDomainPoints(plasticFailureDomain.Domain);
			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);			
		}

		[TestMethod]
		public void RectangularSectionTest2()
		{
			double h = 500;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(h, 0),
				new Point2d(h, h),
				new Point2d(0, h)
			}));

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				1.0, 1.50, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ShowDomainPoints(plasticFailureDomain.Domain);
			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double rebarDiameter = 26;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 25, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				1.0, 1.50, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, concreteMaterial);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ShowDomainPoints(plasticFailureDomain.Domain);
			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double rebarDiameter = 20;
			double height = 500;
			double width = 300;
			double concreteCover = 50;

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 30, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.ParabolaRectangle,
				1.55, 1.80, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(width, height, rebarDiameter, concreteCover, concreteMaterial);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			//ShowDomainPoints(plasticFailureDomain.Domain);
			ExportToGmsh(plasticFailureDomain.Domain);
			ExportToGmsh(elasticFailureDomain.Domain);
		}
	}
}
