using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
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

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 25, 
				ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				1.0, 1.00, 0.00195, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 
				0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(width, height, rebarDiameter, concreteCover, concreteMaterial);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ShowDomainPoints(plasticFailureDomain.Domain);

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);			

			Point3d maxTraction = new Point3d(0, 0, 464000);
			Point3d minCompression = new Point3d(0, 0, -2850000);

			BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

			var maxFT = CalculateAdimensionalForces(section,
				new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
			var minFT = CalculateAdimensionalForces(section,
				new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

			Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
			Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

			Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

			FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
				GetLocalCoordinateSystem(section), 1));

			Point3d expDomainPoint = new Point3d(106 * 1000000, 0, 0);

			Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 2.5);
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
				1.0, 1.00, 5E-5, 0.01, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ShowDomainPoints(plasticFailureDomain.Domain);

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);			

			Point3d maxTraction = new Point3d(0, 0, 166666);
			Point3d minCompression = new Point3d(0, 0, -3537000);

			BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

			var maxFT = CalculateAdimensionalForces(section,
				new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
			var minFT = CalculateAdimensionalForces(section,
				new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

			Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
			Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

			Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

			FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
				GetLocalCoordinateSystem(section), 1));

			Point3d expDomainPoint = new Point3d(40 * 1000000, 0, 0);

			Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 5);
		}

		[TestMethod]
		public void RectangularSectionTest3()
		{
			double height = 1000;
			double width = 300;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 40, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				 0.6750, 0.50, 0.00195, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = 
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ShowDomainPoints(plasticFailureDomain.Domain);

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);			

			Point3d maxTraction = new Point3d(0, 0, 100000);
			Point3d minCompression = new Point3d(0, 0, -8000000);

			BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

			var maxFT = CalculateAdimensionalForces(section,
				new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
			var minFT = CalculateAdimensionalForces(section,
				new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

			Assert.IsTrue(Math.Abs(maxFT.N) < 0.015);
			Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

			Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

			FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
				GetLocalCoordinateSystem(section), 1));

			Point3d expDomainPoint = new Point3d(54 * 1000000, 0, 0);

			Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 5);
		}

		[TestMethod]
		public void RectangularSectionTest4()
		{
			double height = 1000;
			double width = 300;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ConcreteMaterialModelCode2010FRC concreteMaterial = new ConcreteMaterialModelCode2010FRC("", 40, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.Bilinear,
				 1.0, 1.0, 0.00195, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear, 0, 0, 0, ConcreteMaterialModelCode2010.CementType.ClassN);
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ShowDomainPoints(plasticFailureDomain.Domain);

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);			

			Point3d maxTraction = new Point3d(0, 0, 200000);
			Point3d minCompression = new Point3d(0, 0, -8000000);

			BoundingBox3d bBox = GetBoundingBox(plasticFailureDomain.Domain);

			var maxFT = CalculateAdimensionalForces(section,
				new ForceTuple(minCompression.Z - bBox.Min.Z, 0, 0));
			var minFT = CalculateAdimensionalForces(section,
				new ForceTuple(maxTraction.Z - bBox.Max.Z, 0, 0));

			Assert.IsTrue(Math.Abs(maxFT.N) < 0.01);
			Assert.IsTrue(Math.Abs(maxFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(maxFT.My) < 0.01);

			Assert.IsTrue(Math.Abs(minFT.N) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.Mx) < 0.01);
			Assert.IsTrue(Math.Abs(minFT.My) < 0.01);

			FailureDomain.FailureDomainPoint domainPoint = plasticFailureDomain.AddForce(new ResultBeamForces(0, 0, 0, 0, 10 * 1000000, 0,
				GetLocalCoordinateSystem(section), 1));

			Point3d expDomainPoint = new Point3d(97 * 1000000, 0, 0);

			Assert.IsTrue(Math.Abs(expDomainPoint.X - domainPoint.MxRd) / domainPoint.MxRd * 100 < 2);
		}

		[TestMethod]
		public void RectangularSectionTest5()
		{
			double height = 600;
			double width = 300;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(0, 0),
				new Point2d(width, 0),
				new Point2d(width, height),
				new Point2d(0, height)
			}));

			ConcreteMaterialModelCode2010FRC concreteMaterial = ConcreteMaterialModelCode2010FRC.C30_37_10;
			ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			StandardEN1992p11 standard = new StandardEN1992p11();

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResult();
			//var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();

			ShowDomainPoints(plasticFailureDomain.Domain);

			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);			
		}

		[TestMethod]
		public void RectangularSectionTest6()
		{
			double height = 400;
			double width = 400;
			double rebarDiameter = 20;
			double concreteCover = 50;

			Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
			{
				new Point2d(-width / 2.0, -height / 2.0),
				new Point2d(width / 2.0, -height / 2.0),
				new Point2d(width / 2.0, height / 2.0),
				new Point2d(-width / 2.0, height / 2.0)
			}));

			ShapeEx shapeEx = new ShapeEx(shape, ConcreteMaterialModelCode2010FRC.C30_37_15);
			RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, RebarMaterial.B450C);

			ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
			{
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width / 2.0, -height / 2.0 + concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + height / 2.0)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width - concreteCover, -height / 2.0 + height - concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + width / 2.0, -height / 2.0 + height - concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + height - concreteCover)),
				new ReinforcedConcreteRebar(rebar, new Point2d(-width / 2.0 + concreteCover, -height / 2.0 + height / 2.0))
			};

			ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);
			section.AddRebars(rebars);

			StandardEN1992p11 standard = new StandardEN1992p11();
			//ExportToGmsh(section);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
				new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

			var elasticFailureDomain = sectionChecker.GetElasticFailureDomainResult();
			ShowDomainPoints(elasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain.GetMesh());

			var plasticFailureDomain = sectionChecker.GetPlasticFailureDomainResultAsync().Result;
			ShowDomainPoints(plasticFailureDomain.Domain);
			//ExportToGmsh(plasticFailureDomain.Domain.GetMesh());
		}

		[TestMethod]
		public void RectangularSectionTest10()
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
			//ExportToGmsh(plasticFailureDomain.Domain);
			//ExportToGmsh(elasticFailureDomain.Domain);
		}
	}
}
