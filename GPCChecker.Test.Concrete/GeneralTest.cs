using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class GeneralTest : UnitTestBase
    {

        protected Point3d[] ExportToGmsh(FailureDomain failureDomain, FailureDomain failureDomain2 = null)
        {
            GmshNet.Gmsh.Initialize();
            int horizontal = failureDomain.DomainPoints.GetUpperBound(0);
            List<Point3d> points = new List<Point3d>();


            Action<FailureDomain> action = new Action<FailureDomain>((domain) =>
            {
                for (int i = 0; i < horizontal; i++)
                {
                    int vertical = domain.DomainPoints[i].GetUpperBound(0);

                    for (int j = 0; j < vertical; j++)
                    {

                        GmshNet.Gmsh.Model.Occ.AddPoint(domain.DomainPoints[i][j].MxRd / 1000000,
                            domain.DomainPoints[i][j].MyRd / 1000000,
                            domain.DomainPoints[i][j].NRd / 1000 / 10);

                        points.Add(new Point3d(domain.DomainPoints[i][j].MxRd / 1000000,
                            domain.DomainPoints[i][j].MyRd / 1000000,
                            domain.DomainPoints[i][j].NRd / 1000 / 10));
                    }
                }

                GmshNet.Gmsh.Model.Occ.Synchronize();
            });

            action(failureDomain);

            if (failureDomain2 != null)
                action(failureDomain2);




            GmshNet.Gmsh.Fltk.Run();
            GmshNet.Gmsh.Finalize();

            return points.ToArray();
        }

        [TestMethod]
        public void AsyncTest1()
        {


            RebarSectionCircular rebarPhi20 = new RebarSectionCircular(20, new RebarMaterial("", 450));

            ConcreteSectionRectangular concreteSectionRectangular = new ConcreteSectionRectangular(500, 300,
                                                                    new ConcreteMaterialEN1992("", 25,
                                                                    ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

            concreteSectionRectangular.AddRebar(new ReinforcedConcreteRebar(1, rebarPhi20, new Point3d(50, 50, 0)));

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


        [TestMethod]
        public void Test1()
        {

            RebarSectionCircular rebarPhi201 = new RebarSectionCircular(20, new RebarMaterial("", 450));
            RebarSectionCircular rebarPhi202 = new RebarSectionCircular(20, new RebarMaterial("", 300000, 450, 500));

            ConcreteSectionRectangular concreteSectionRectangular1 = new ConcreteSectionRectangular(500, 300,
                                                                    new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));

            concreteSectionRectangular1.AddRebar(new ReinforcedConcreteRebar(1, rebarPhi201, new Point3d(50, 50, 0)) );


            ConcreteSectionRectangular concreteSectionRectangular2 = new ConcreteSectionRectangular(500, 300,
                                                                    new ConcreteMaterialEN1992("", 25, ConcreteMaterialEN1992.CompressionStressStrainDiagrams.Bilinear));
            concreteSectionRectangular2.AddRebar(new ReinforcedConcreteRebar(1, rebarPhi202, new Point3d(50, 50, 0)));


            Func<ConcreteSectionRectangular, FailureDomain> func = new Func<ConcreteSectionRectangular, FailureDomain>((concreteSection) =>
            {
                SectionCheckerAttribute sectionCheckerAttribute = new SectionCheckerAttribute(concreteSection, null, null);

                SectionCheckerModelCode2010 sectionChecker = new SectionCheckerModelCode2010(sectionCheckerAttribute, new SectionCheckerModelCode2010.SectionOptionsModelCode2010(), new StandardEN1992p11());

                return sectionChecker.GetFailureDomainResult().Domain;
            });



            ExportToGmsh(func(concreteSectionRectangular1), func(concreteSectionRectangular2));

        }

    }
}
