using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C40_50;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C50_60;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("FeB44k", 200000, 430, 430, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, u, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C40_50;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C40_50;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C25_30;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992 concreteMaterial = ConcreteMaterialEN1992Data.C40_50;
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial, rebarMaterial, standard);

            ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial, standard);
            CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
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

            ConcreteMaterialEN1992[] concreteMaterial = new ConcreteMaterialEN1992[] { ConcreteMaterialEN1992Data.C25_30, ConcreteMaterialEN1992Data.C32_40,
                ConcreteMaterialEN1992Data.C40_50, ConcreteMaterialEN1992Data.C45_55, ConcreteMaterialEN1992Data.C55_67 };
            SteelMaterial rebarMaterial = new SteelMaterial("", 200000, 440, 440, 0.1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            StandardNTC2018Concrete standard = new StandardNTC2018Concrete();

            for (int i = 0; i < concreteMaterial.Length; i++)
            {
                (FailureDomainResult failureDomainResult, ReinforcedConcreteSection section) result = GetAbacusFailureDomainResult(omega, b, h, concreteMaterial[i], rebarMaterial, standard);
                ForceTuple forceTuple = GetAbacusForceTuple(b, h, v, ux, uy, concreteMaterial[i], standard);
                CommonAssertsAbacus(result.section, forceTuple, result.failureDomainResult);
            }
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

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(GetRectangularShape(width, height), concreteMaterial);

            RebarSectionCircular rebar = new RebarSectionCircular(rebarDiameter, new SteelMaterial("RebarMat", 200000, 534, 630, 0.1, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Rebar));
            RebarSectionCircular rebarComp = new RebarSectionCircular(10, new SteelMaterial("RebarMat", 200000, 534, 630, 0.1, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Rebar));

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
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);
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
            ReinforcedConcreteSection sectionFRC = new ReinforcedConcreteSection(GetRectangularShape(width, height), concreteMaterialFRC);

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(GetRectangularShape(width, height), concreteMaterial);

            RebarSectionCircular rebar20 = new RebarSectionCircular(rebarDiameterP20, new SteelMaterialEN1992("B450C", 200000, 450, 540, 0.075, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Rebar));
            RebarSectionCircular rebar16 = new RebarSectionCircular(rebarDiameterP16, new SteelMaterialEN1992("Y1620", 195000, 1420, 1620, 0.075, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Tendon));

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
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(sectionFRC), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);
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
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);
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
            Console.WriteLine(Math.Round(tensionResultFRC.StrainPlane.GetStrain(section.ConcreteShape.Fill[0]), 6));

            Console.WriteLine(concreteMaterial.Name + " h/w = " + Math.Round(height, 2));
            Console.WriteLine(Math.Round(height, 2));
            Console.WriteLine(Math.Round(height / width, 2));

            Console.WriteLine(Math.Round(section.Area, 2));
            Console.WriteLine(Math.Round(section.AreaRebars, 2));
            Console.WriteLine(Math.Round(section.AreaRebars / sectionFRC.Area * 100, 3));

            Console.WriteLine(Math.Round(tensionResult.GetRebarsTension().Select(i => i.tension).Max(), 2));
            Console.WriteLine(Math.Round(tensionResult.GetConcreteVerticesTension().Select(i => i.tension).Min(), 2));
            Console.WriteLine(Math.Round(tensionResult.StrainPlane.GetStrain(section.ConcreteShape.Fill[0]), 6));
        }

        [TestMethod]
        [TestCategory("Sap Validation")]
        public void SapValidationACI318p08Example001()
        {
            //ACI 318-08 Example 001

            double rebarDiameter = 25.47;
            double height = 406.4;
            double width = 254;
            double copriferro = 63.5;
            bool haveSpiral = false;

            Shape2d shape = GetRectangularShape(width, height);

            RebarSectionCircular rebarSection = new RebarSectionCircular(rebarDiameter, SteelMaterialACI318Data.Grade60);

            ReinforcedConcreteRebar[] rebars = new ReinforcedConcreteRebar[]
            {
                new ReinforcedConcreteRebar(rebarSection, new Point3d(0, copriferro, 0)),
                new ReinforcedConcreteRebar(rebarSection, new Point3d(width / 2.0, copriferro, 0)),
                new ReinforcedConcreteRebar(rebarSection, new Point3d(width, copriferro, 0)),
            };

            ReinforcedConcreteSection section = new ReinforcedConcreteSection(shape, new ConcreteMaterialACI318("fc' 4000", 27.579, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear));
            section.AddRebars(rebars);

            ResultBeamForces force = new ResultBeamForces(0 * 1000, 0, 0, 0, 10 * 1000000, 0, GetLocalCoordinateSystem(section));
            SectionCheckerACI318.SectionOptionsStandardACI318 options =
                new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            var solver = new SectionSolverACI318(section, options, new StandardACI318p08(), haveSpiral, section.Centroid);
            FailureDomain.FailureDomainPoint result = solver.CalculateDomainPoint(force, options.FailureAnalysisType);

            // Da documentazione SAP ACI 318-08 Example 001, usano stress block.
            // L'errore aumenta usando ParabolaRectangle.
            double expMxRd1 = 164.95 * 1000000;
            Assert.IsTrue(Math.Abs(result.MxRd - expMxRd1) / expMxRd1 * 100 < 2.5);
        }

        [TestMethod]
        [TestCategory("Sap Validation")]
        public void SapValidationACI318p08Example002()
        {
            //ACI 318-08 Example 002

            double rebarDiameter = 28.6608;
            double height = 558.8;
            double width = 355.6;
            double copriferro = 63.5;
            bool haveSpiral = false;

            ReinforcedConcreteSection section = GetRectangularSection2SideRebars(width, height, rebarDiameter, copriferro, 4,
                new ConcreteMaterialACI318("fc' 4000", 27.579, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear), SteelMaterialACI318Data.Grade60);

            ResultBeamForces force = new ResultBeamForces(-1772.17 * 1000, 0, 0, 0, 100 * 1000000, 0, GetLocalCoordinateSystem(section));
            SectionCheckerACI318.SectionOptionsStandardACI318 options =
                new SectionCheckerACI318.SectionOptionsStandardACI318(GetLocalCoordinateSystem(section), SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 64);

            var solver = new SectionSolverACI318(section, options, new StandardACI318p08(), haveSpiral, section.Centroid);
            FailureDomain.FailureDomainPoint result = solver.CalculateDomainPoint(force, options.FailureAnalysisType);

            // Da documentazione SAP ACI 318-08 Example 002, usano stress block.
            // L'errore aumenta usando ParabolaRectangle.
            double expNrd = -1772.17 * 1000;
            double expMxRd = 450.13 * 1000000;
            Assert.IsTrue(Math.Abs(result.NRd - expNrd) / expNrd * 100 < 1.0);
            Assert.IsTrue(Math.Abs(result.MxRd - expMxRd) / expMxRd * 100 < 2.5);
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
