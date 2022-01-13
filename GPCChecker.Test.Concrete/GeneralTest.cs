using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class GeneralTest : ConcreteTestBase
    {
		#region Async test

		[TestMethod]
        public void AsyncTest1()
        {
            RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial("", 450));

            ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300,
                new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

            concreteSectionRectangular.AddRebar(new ReinforcedConcreteRebar(rebarPhi20, new Point3d(50, 50, 0)));

            List<ResultBeamForces> forces = new List<ResultBeamForces>();

            for (int i = 0; i < 10; i++)
            {
                forces.Add(new ResultBeamForces(-100 * 1000, 20, 30, 40, 50 * 1000000, 10 * 1000000, GetLocalCoordinateSystem(concreteSectionRectangular)));
            }
            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSectionRectangular, forces.ToArray(), null);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute,
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(concreteSectionRectangular)), new StandardEN1992p11());

            var stressResult = sectionChecker.GetStressAnalysisResultAsync();

            Task.WaitAll(new[] { stressResult });

            Assert.IsTrue(stressResult.Result.Length == forces.Count);
        }

        [TestMethod]
        public void AsyncTest2()
        {
            RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial("", 450));
            ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300, ConcreteMaterialEN1992.C25_30);
            concreteSectionRectangular.AddRebar(new ReinforcedConcreteRebar(rebarPhi20, new Point3d(50, 50, 0)));

            ResultBeamForces force = new ResultBeamForces(-100 * 1000, 20, 30, 40, 3 * 1000000, 2 * 1000000, GetLocalCoordinateSystem(concreteSectionRectangular));

            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSectionRectangular);
            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute,
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(GetLocalCoordinateSystem(concreteSectionRectangular)), new StandardEN1992p11());

            var stressResult = sectionChecker.GetStressAnalysisResultAsync(force);

            Task.WaitAll(new[] { stressResult });

            Assert.IsTrue(stressResult.Result.Force.N == force.N);
            Assert.IsTrue(stressResult.Result.Force.M1 == force.M1);
            Assert.IsTrue(stressResult.Result.Force.M2 == force.M2);
        }

		#endregion

		#region Serializable Test

		[TestMethod]
        public void SerializableNameSpaceAttributes()
        {
            string assemblyName = "GPCChecker.Concrete";
            string nameSpace = "GPC.Checkers.Concrete.Attributes";

            var assembly = Assembly.Load(assemblyName);
            var classes = assembly.GetTypes().Where(a => a.IsClass && a.Namespace != null && a.Namespace.Contains(nameSpace)).ToList();

            foreach(var c in classes)
			{
                Assert.IsTrue(SerializationClassesCommonAsserts(c));
            }                
        }

        [TestMethod]
        public void SerializableNameSpaceCheckers()
        {
            string assemblyName = "GPCChecker.Concrete";
            string nameSpace = "GPC.Checkers.Concrete.Checkers";

            var assembly = Assembly.Load(assemblyName);
            var classes = assembly.GetTypes().Where(a => a.IsClass && a.Namespace != null && a.Namespace.Contains(nameSpace)).ToList();

            foreach (var c in classes)
            {
                Assert.IsTrue(SerializationClassesCommonAsserts(c));
            }
        }

        [TestMethod]
        public void SerializableNameSpaceResults()
        {
            string assemblyName = "GPCChecker.Concrete";
            string nameSpace = "GPC.Checkers.Concrete.Results";

            var assembly = Assembly.Load(assemblyName);
            var classes = assembly.GetTypes().Where(a => a.IsClass && a.Namespace != null && a.Namespace.Contains(nameSpace)).ToList();

            foreach (var c in classes)
            {
                Assert.IsTrue(SerializationClassesCommonAsserts(c));
            }
        }

        [TestMethod]
        public void SerializableNameSpaceSectionSolvers()
        {
            string assemblyName = "GPCChecker.Concrete";
            string nameSpace = "GPC.Checkers.Concrete.SectionSolvers";

            var assembly = Assembly.Load(assemblyName);
            var classes = assembly.GetTypes().Where(a => a.IsClass && a.Namespace != null && a.Namespace.Contains(nameSpace)).ToList();

            foreach (var c in classes)
            {
                Assert.IsTrue(SerializationClassesCommonAsserts(c));
            }
        }

        [TestMethod]
        public void SerializationSectionSolverModelCode2010Test()
        {
            bool check = true;

            SectionSolverModelCode2010 s = new SectionSolverModelCode2010(GetRectangularSection4Rebars(), new StandardEN1992p11());

            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, s);
                ms.Position = 0;

                var casted = formatter.Deserialize(ms);
                SectionSolverModelCode2010 oggettoDeserializzato = (SectionSolverModelCode2010)casted;

                if (s.Equals(oggettoDeserializzato))
                {
                    if(s.ConcreteMaterial != oggettoDeserializzato.ConcreteMaterial ||
                        s.ConcreteMaterialModelCode2010 != oggettoDeserializzato.ConcreteMaterialModelCode2010 ||
                        s.ConcreteSection.Shape != oggettoDeserializzato.ConcreteSection.Shape ||
                        s.ConsiderTensileConcrete != oggettoDeserializzato.ConsiderTensileConcrete)
                        check = false;
                    for (int i = 0; i < s.ConcreteSection.Rebars.Count(); i++)
                        if (s.ConcreteSection.Rebars.ToArray()[i] != oggettoDeserializzato.ConcreteSection.Rebars.ToArray()[i])
                            check = false;
                }
                else
                {
                    check = false;
                }
            }

            if (check)
                Console.WriteLine($"Class {s} is serializable");
            else
                Console.WriteLine($"Warning: Class {s} is not serializable");

            Assert.IsTrue(check);
        }               

        #endregion
    }
}
