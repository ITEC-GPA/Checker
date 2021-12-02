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
			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.4;
			double u = 0.2;
			double omega = 0.25;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h/10, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void VSS_AB1_2()
		{
			double b = 400;
			double h = 800;

			// dati presi dall'abaco
			double v = 0.41;
			double u = 0.25;
			double omega = 0.37;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h / 10, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void VSS_AB1_3()
		{
			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.8;
			double u = 0.15;
			double omega = 0.36;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C50_60;
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void VSS_AB2_1()
		{
			double b = 300.0;
			double h = 1000.0;

			// dati presi dall'abaco
			double v = 0.42;
			double u = 0.15;
			double omega = 0.17;


			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void VSS_AB2_2()
		{
			double b = 300.0;
			double h = 500.0;

			// dati presi dall'abaco
			double v = 0.42;
			double u = 0.15;
			double omega = 0.17;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void VSS_AB2_3()
		{
			double b = 300.0;
			double h = 500.0;

			// dati presi dall'abaco
			double v = 0.80;
			double u = 0.20;
			double omega = 0.60;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("FeB44k", 430);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double Ns = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double Ms = u * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(Ns, Ms, 0), failureDomain));
		}

		[TestMethod]
		public void CV_AB1_1()
		{
			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.40;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB1_2()
		{
			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.40;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB1_3()
		{
			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.175;
			double omega = 0.60;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB1_4()
		{
			double b = 500;
			double h = 1200;

			// dati presi dall'abaco
			double v = 0.1;
			double uy = 0.075;
			double ux = 0.175;
			double omega = 0.60;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB2_1()
		{
			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.4;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.45;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB2_2()
		{
			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.4;
			double ux = 0.1;
			double uy = 0.1;
			double omega = 0.45;

			ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.ParabolaRectangle);
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB2_3()
		{
			double b = 300;
			double h = 500;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.075;
			double omega = 0.30;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

		[TestMethod]
		public void CV_AB2_4()
		{
			double b = 500;
			double h = 1000;

			// dati presi dall'abaco
			double v = 0.1;
			double ux = 0.075;
			double uy = 0.075;
			double omega = 0.30;

			ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992.C40_50;
			RebarMaterial rebarMaterial = new RebarMaterial("", 440);
			StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

			// si calcola un diametro equivalente all'omega di input
			double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
			double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

			ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

			SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
			SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions = new SectionCheckerModelCode2010.SectionOptionsModelCode2010();
			SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard);

			FailureDomainResult failureDomain = sectionChecker.GetFailureDomainResult();
			//ExportToGmsh(failureDomain.Domain);

			// coppia Nrd/Mrd per questa combinazione di u/v
			double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
			double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
			double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

			Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
		}

	}
}
