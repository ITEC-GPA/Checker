using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
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
    public class GeneralTest : UnitTestBase
    {

        [TestMethod]
        public void AsyncTest1()
        {


            RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial(450));

            ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300,
                                                                    new ConcreteMaterialEN1992(25,
                                                                    ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear),
                                                                    new ReinforcedConcreteRebar[] { new ReinforcedConcreteRebar(rebarPhi20, new Point3d(50, 50, 0)) }
                                                                    );

            List<ResultBeamForces> forces = new List<ResultBeamForces>();

            for (int i = 0; i < 10000; i++)
            {
                forces.Add(new ResultBeamForces(10, 20, 30, 40, 50, 60, new CoordinateSystem(concreteSectionRectangular.Centroid, Vector3d.XAxis, Vector3d.YAxis)));
            }


            SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSectionRectangular, forces.ToArray(), null);


            SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardEN1992p11());

            var stressResult = sectionChecker.GetStressAnalysisResultAsync();

            Task.WaitAll(new[] {stressResult}); 

            Assert.IsTrue(stressResult.Result.Length == forces.Count);
        }
    }
}
