using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using GPC.TestUtilities;
using GPC.Utilities.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class GeneralTest : ConcreteTestBase
    {
        private bool SerializationClassesCommonAsserts(object objToTest)
        {
            bool check = true;
            
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, objToTest);
                ms.Position = 0;

                var oggettoDeserializzato = formatter.Deserialize(ms);

                if (objToTest == oggettoDeserializzato)
                {
                    Console.WriteLine($"Class {objToTest.ToString().Replace("GPC.Checkers.Concrete.", "")} is serializable");
                }
                else
                {
                    Console.WriteLine($"Warning: Class {objToTest.GetType()} is not serializable");
                    check = false;
                }
            }
            return check;
        }

		#region Async test

		[TestMethod]
        public void AsyncTest1()
        {
            RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial("", 450));

            ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300,
                new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

            concreteSectionRectangular.AddRebar(new ReinforcedConcreteRebar(rebarPhi20, new Point3d(50, 50, 0)));

            List<ResultBeamForces> forces = new List<ResultBeamForces>();

            for (int i = 0; i < 100; i++)
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
        public void SerializableTest1()
        {
            string assemblyName = "GPCChecker.Concrete";
            string nameSpace = "GPC.Checkers.Concrete.SectionSolvers";

            var assembly = Assembly.Load(assemblyName);
            var classes = assembly.GetTypes().Where(a => a.IsClass && a.Namespace != null && a.Namespace.Contains(nameSpace)).ToList();

            foreach(var c in classes)
			{
                Assert.IsTrue(SerializationClassesCommonAsserts(c));
            }                
        }

        [TestMethod]
        public void SerializableTest2()
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
        public void SerializableTest3()
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
        public void SerializableTest4()
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

		#endregion
	}
}
