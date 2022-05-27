using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h / 10, concreteMaterial, rebarMaterial);
            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h / 10, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4 * (Atot / 4.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection4Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

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
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            // si calcola un diametro equivalente all'omega di input
            double Atot = (omega * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))) / (rebarMaterial.Fyk / standard.GammaS);
            double rebarDiameter = Math.Sqrt(4.0 * (Atot / 8.0) / Math.PI);

            ReinforcedConcreteSection section = GetRectangularSection8Rebars(b, h, rebarDiameter, h / 10.0, concreteMaterial, rebarMaterial);

            SectionCheckerModelCode2010 sectionChecker = GetSectionCheckerModelCode2010(section, standard);
            sectionChecker.SectionSolver.SetTetaDiscretization(32);
            FailureDomainResult failureDomain = sectionChecker.GetPlasticFailureDomainResult();

            // coppia Nrd/Mrd per questa combinazione di u/v
            double NRd = -v * (b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC))); ;
            double MxRd = ux * (b * h * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));
            double MyRd = uy * (b * b * h * Math.Abs((concreteMaterial.Fck / standard.GammaC * standard.AlphaCC)));

            Assert.IsTrue(CommonAssertsAbacus(section, new ForceTuple(NRd, MxRd, MyRd), failureDomain));
        }

        [TestMethod]
        public void Paper_ValidationTest_1()
        {
            // Reference: Flexural behaviour of RC beams in fibre reinforced concrete
            // Alberto Meda, Fausto Minelli, Giovanni A. Plizzari

            double rebarDiameter = 16;
            double height = 300;
            double width = 200;
            double concreteCover = 41;

            //ConcreteMaterialModelCode2010 concreteMaterial = new ConcreteMaterialModelCode2010("", 45,
            //	ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.NonLinear,
            //	1.386, 0.926, 0.00285, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear);

            ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 49.7, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.NonLinear);

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("RebarMat", 200000, 534, 630, 0.1, SteelMaterial.SteelTypes.Rebar));
            RebarSectionCircular rebarComp = new RebarSectionCircular(10, new SteelMaterial("RebarMat", 200000, 534, 630, 0.1, SteelMaterial.SteelTypes.Rebar));

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar, new Point3d(concreteCover, concreteCover, 0)),
				//new ReinforcedConcreteRebar(rebar, new Point3d(width / 3.0, concreteCover, 0)),
				//new ReinforcedConcreteRebar(rebar, new Point3d(width *2.0/3.0, concreteCover, 0)),
				new ReinforcedConcreteRebar(rebar, new Point3d(width - concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebarComp, new Point3d(concreteCover, height - concreteCover + 3, 0)),
                new ReinforcedConcreteRebar(rebarComp, new Point3d(width - concreteCover, height - concreteCover + 3, 0)),
            };

            section.AddRebars(rebars);

            //ExportToGmsh(section);
            StandardEN1992p11Override standard = new StandardEN1992p11Override();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomainResult domainResult = sectionChecker.GetPlasticFailureDomainResult();

            double[] axialForces = new double[domainResult.Domain.DomainPoints[0].Length];
            double[] bendingMomentX = new double[domainResult.Domain.DomainPoints[0].Length];
            for (int i = 0; i < axialForces.Length; i++)
                axialForces[i] = domainResult.Domain.DomainPoints[0][i].NRd;
            for (int i = 0; i < bendingMomentX.Length; i++)
                bendingMomentX[i] = domainResult.Domain.DomainPoints[0][i].MxRd;

            double expAxialForce = 0;
            double MxRd = 0;
            for (int i = 0; i < axialForces.Length - 1; i++)
            {
                if (axialForces[i] >= expAxialForce && axialForces[i + 1] < expAxialForce)
                {
                    MxRd = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(axialForces[i], axialForces[i + 1],
                        bendingMomentX[i], bendingMomentX[i + 1], expAxialForce);
                }
            }

            Console.WriteLine($"Calculated MxRd = {Math.Round(MxRd / 1000000, 2)} KNm");
        }

        [TestMethod]
        public void Paper_ValidationTest_2()
        {
            // Reference: Structural design according to fib MC2010: comparison between RC and FRC elements
            // Prisco, Plizzari, Vandewalle

            double rebarDiameterP20 = 20;
            double rebarDiameterP16 = 20;
            double height = 300;
            double width = 800;
            double concreteCover = 45;

            ConcreteMaterialModelCode2010 concreteMaterialFRC = new ConcreteMaterialModelCode2010("C30/37 - 30kg/m^3", 30,
                ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.NonLinear,
                1.11, 0.89, 0.00285, 0.02, ConcreteMaterialModelCode2010.TensionStressStrainDiagrams.Bilinear);

            ConcreteMaterialEN1992 concreteMaterial = new ConcreteMaterialEN1992("", 30, ConcreteMaterialModelCode2010.CompressionStressStrainDiagrams.NonLinear);

            Shape2d shape = new Shape2d(new Polygon2d(new Point2d[]
            {
                new Point2d(0, 0),
                new Point2d(width, 0),
                new Point2d(width, height),
                new Point2d(0, height)
            }));

            ShapeEx shapeExFRC = new ShapeEx(shape, concreteMaterialFRC);
            ReinforcedConcreteSection sectionFRC = new ReinforcedConcreteSection(shapeExFRC);

            ShapeEx shapeEx = new ShapeEx(shape, concreteMaterial);
            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shapeEx);

            RebarSectionCircular rebar20 = new RebarSectionCircular(rebarDiameterP20, SteelMaterial.B450CHardening);
            RebarSectionCircular rebar16 = new RebarSectionCircular(rebarDiameterP16, SteelMaterial.B450CHardening);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebar20, new Point3d(concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(width/3.0, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar16, new Point3d(width/2.0, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(2.0*width/3.0, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(width - concreteCover, concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(concreteCover, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(width/3.0, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(2.0*width/3.0, height - concreteCover, 0)),
                new ReinforcedConcreteRebar(rebar20, new Point3d(width - concreteCover, height - concreteCover, 0)),
            };

            sectionFRC.AddRebars(rebars);
            section.AddRebars(rebars);

            StandardEN1992p11Override standard = new StandardEN1992p11Override();

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(sectionFRC, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(sectionFRC));
            SectionCheckerModelCode2010 sectionCheckerFRC = new SectionCheckerModelCode2010(sectionCheckerAttribute, sectionOptions, standard, true);

            FailureDomainResult domainResultFRC = sectionCheckerFRC.GetPlasticFailureDomainResult();

            double[] axialForces = new double[domainResultFRC.Domain.DomainPoints[0].Length];
            double[] bendingMomentX = new double[domainResultFRC.Domain.DomainPoints[0].Length];
            for (int i = 0; i < axialForces.Length; i++)
                axialForces[i] = domainResultFRC.Domain.DomainPoints[0][i].NRd;
            for (int i = 0; i < bendingMomentX.Length; i++)
                bendingMomentX[i] = domainResultFRC.Domain.DomainPoints[0][i].MxRd;

            double expAxialForce = 0;
            double MxRdFRC = 0;
            for (int i = 0; i < axialForces.Length - 1; i++)
            {
                if (axialForces[i] >= expAxialForce && axialForces[i + 1] < expAxialForce)
                {
                    MxRdFRC = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(axialForces[i], axialForces[i + 1],
                        bendingMomentX[i], bendingMomentX[i + 1], expAxialForce);
                }
            }

            SectionCheckerAttribute sectionCheckerAttribute2 = new SectionCheckerAttribute(section, null, null);
            SectionCheckerModelCode2010.SectionOptionsModelCode2010 sectionOptions2 =
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section));
            SectionCheckerModelCode2010 sectionChecker2 = new SectionCheckerModelCode2010(sectionCheckerAttribute2, sectionOptions2, standard, true);

            FailureDomainResult domainResult2 = sectionChecker2.GetPlasticFailureDomainResult();

            axialForces = new double[domainResult2.Domain.DomainPoints[0].Length];
            bendingMomentX = new double[domainResult2.Domain.DomainPoints[0].Length];
            for (int i = 0; i < axialForces.Length; i++)
                axialForces[i] = domainResult2.Domain.DomainPoints[0][i].NRd;
            for (int i = 0; i < bendingMomentX.Length; i++)
                bendingMomentX[i] = domainResult2.Domain.DomainPoints[0][i].MxRd;

            double MxRd = 0;
            for (int i = 0; i < axialForces.Length - 1; i++)
            {
                if (axialForces[i] >= expAxialForce && axialForces[i + 1] < expAxialForce)
                {
                    MxRd = GPC.Utilities.Maths.Interpolation.GetLinearInterpolation(axialForces[i], axialForces[i + 1],
                        bendingMomentX[i], bendingMomentX[i + 1], expAxialForce);
                }
            }

            Console.WriteLine($"Calculated MxRd FRC = {Math.Round(MxRdFRC / 1000000, 2)} KNm");
            Console.WriteLine($"Calculated MxRd RC = {Math.Round(MxRd / 1000000, 2)} KNm");
            Console.WriteLine($"Increment = {Math.Round((MxRdFRC - MxRd) / MxRd * 100, 2)} %");

            double slsMx = 104 * 1000000;

            StressAnalysisResult tensionResultFRC = sectionCheckerFRC.GetStressAnalysisResult(
                new ResultBeamForces(0, 0, 0, 0, slsMx, 0, GetLocalCoordinateSystem(sectionFRC)));

            StressAnalysisResult tensionResult = sectionChecker2.GetStressAnalysisResult(
                new ResultBeamForces(0, 0, 0, 0, slsMx, 0, GetLocalCoordinateSystem(section)));

            Console.WriteLine(concreteMaterialFRC.Name + " h/w = " + Math.Round(height, 2));
            Console.WriteLine(Math.Round(height, 2));
            Console.WriteLine(Math.Round(height / width, 2));

            Console.WriteLine(Math.Round(sectionFRC.Area, 2));
            Console.WriteLine(Math.Round(sectionFRC.AreaRebars, 2));
            Console.WriteLine(Math.Round(sectionFRC.AreaRebars / sectionFRC.Area * 100, 3));

            Console.WriteLine(Math.Round(tensionResultFRC.GetRebarsTension().Select(i => i.tension).Max(), 2));
            Console.WriteLine(Math.Round(tensionResultFRC.GetConcreteVerticesTension().Select(i => i.tension).Min(), 2));
            Console.WriteLine(Math.Round(tensionResultFRC.StrainPlane.GetStrain(shape.Fill[0]), 6));

            Console.WriteLine(concreteMaterial.Name + " h/w = " + Math.Round(height, 2));
            Console.WriteLine(Math.Round(height, 2));
            Console.WriteLine(Math.Round(height / width, 2));

            Console.WriteLine(Math.Round(section.Area, 2));
            Console.WriteLine(Math.Round(section.AreaRebars, 2));
            Console.WriteLine(Math.Round(section.AreaRebars / sectionFRC.Area * 100, 3));

            Console.WriteLine(Math.Round(tensionResult.GetRebarsTension().Select(i => i.tension).Max(), 2));
            Console.WriteLine(Math.Round(tensionResult.GetConcreteVerticesTension().Select(i => i.tension).Min(), 2));
            Console.WriteLine(Math.Round(tensionResult.StrainPlane.GetStrain(shape.Fill[0]), 6));
        }

        public class StandardEN1992p11Override : StandardEN1992p11
        {
            public StandardEN1992p11Override()
            {
                _gammaC = 1.0;
                _gammaCAccidental = 1.0;
                _gammaCE = 1.0;
                _gammaS = 1.0;
                _gammaSAccidental = 1.0;
                _gammaSPrestress = 1.0;
                _gammaSPrestressAccidental = 1.0;
                _alphaCC = 1.0;
                _alphaCT = 1.0;
                _gammaF = 1.0;
                _steelCoefficientStrainTension = 1.0;
            }
        }
    }
}
