using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Results.Storage;
using GPC.Model.Results.Locations;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace SteelTests
{
    [TestClass]
    public class EN1993p11Test
    {
        #region Section RHS

        [TestMethod]
        public void Sap_SectionRHSTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  2        X Mid:  0.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  1.        Shape:  RHS             Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 1         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.015        eNy=0.            eNz=0.            
 A=0.015           Iyy=2.645E-04     iyy=0.134         Wel,yy=0.001        Weff,yy=0.001     
 It=2.135E-04      Izz=1.095E-04     izz=0.086         Wel,zz=0.001        Weff,zz=0.001     
 Iw=0.             Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.003        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=0.001        Av,z=0.014        


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     1.                  100.        100.       -100.        -50.         50.          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.302 = 0. + 0.095 + 0.207   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.        7.46
     Major Braced          1.          1.        7.46
     Minor (z-z)           1.          1.      11.597
     Minor Braced          1.          1.      11.597
     LTB                   1.          1.      11.597

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      5225.6      5225.6

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       5225.6    5405.184  678888.292  226849.304          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49  548219.667       0.098        0.48          1.      5225.6
     MajorB(y-y)    c    0.49  548219.667       0.098        0.48          1.      5225.6
     Minor (z-z)    c    0.49  226849.304       0.152         0.5          1.      5225.6
     MinorB(z-z)    c    0.49  226849.304       0.152         0.5          1.      5225.6
     Torsional TF   c    0.49  226849.304       0.152         0.5          1.      5225.6

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     615.258     615.258     615.258     615.258
     Minor (z-z)        -100.       -100.     435.088     435.088     435.088

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.498      -1.038

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.086       0.461          1.          0.    82676.72

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.193   3.509E-05   6.823E-04       0.018       0.058          1.          1.

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.        1.31        1.12          1.          1.       0.995       0.993

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.496       0.584       0.901
             */

            double L = 1000;

            // The embedded SAP reference explicitly uses curve c (alpha=0.49).
            // In this API, Rolled + ColdFormed selects the cold-formed hollow-section branch;
            // Welded selects a fabricated box, so it is not a direct translation of SAP "Rolled: No".
            var sectionRHS = new SteelSection(new SectionRHS(400, 200, 8, 8, 15, 15, string.Empty),
                SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.ColdFormed);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(100 * 1e3, 50 * 1e3, 50 * 1e3, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0.5;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionRHS);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity - 5225.6 * 1e3) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity * 100 < 0.1);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - 5225.6 * 1e3) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity * 100 < 0.1);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity - 615.258 * 1e6) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity * 100 < 0.1);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity - 435.088 * 1e6) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity * 100 < 0.1);


            //Assert.AreEqual(1, EN1993P11Checker.EN1993p11BeamStationResults[0].Chiy, 0.001);
            //Assert.AreEqual(1, checker.Chiz, 0.001);
            //Assert.AreEqual(1.0, checker.ChiLT, 0.001);

            Assert.AreEqual(0.895, EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 0.011);
            Assert.AreEqual(0.496, EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 0.005);
            Assert.AreEqual(0.584, EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 0.011);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, EN1993P11Checker.EN1993p11BeamStationResults[0].CmY0, 0.005);
            //Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(548219.667 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr1, 1, 0.01);
            Assert.AreEqual(226849.304 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr2, 1, 0.01);

            Assert.AreEqual(0.48, EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 0.001);
            Assert.AreEqual(0.5, EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 0.01);
            //Assert.AreEqual(0.479, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }

        [TestMethod]
        public void Sap_SectionRHSTest2()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  2        X Mid:  0.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  1.        Shape:  RHS             Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 1         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.015        eNy=0.            eNz=0.            
 A=0.015           Iyy=2.645E-04     iyy=0.134         Wel,yy=0.001        Weff,yy=0.001     
 It=2.135E-04      Izz=1.095E-04     izz=0.086         Wel,zz=0.001        Weff,zz=0.001     
 Iw=0.             Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.003        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=0.001        Av,z=0.014        


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     1.                  100.        100.       -100.        -50.         50.          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.302 = 0. + 0.095 + 0.207   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.        7.46
     Major Braced          1.          1.        7.46
     Minor (z-z)           1.          1.      11.597
     Minor Braced          1.          1.      11.597
     LTB                   1.          1.      11.597

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      5225.6      5225.6

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       5225.6    5405.184  678888.292  226849.304          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49  548219.667       0.098        0.48          1.      5225.6
     MajorB(y-y)    c    0.49  548219.667       0.098        0.48          1.      5225.6
     Minor (z-z)    c    0.49  226849.304       0.152         0.5          1.      5225.6
     MinorB(z-z)    c    0.49  226849.304       0.152         0.5          1.      5225.6
     Torsional TF   c    0.49  226849.304       0.152         0.5          1.      5225.6

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     615.258     615.258     615.258     615.258
     Minor (z-z)        -100.       -100.     435.088     435.088     435.088

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.498      -1.038

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.086       0.461          1.          0.    82676.72

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.193   3.509E-05   6.823E-04       0.018       0.058          1.          1.

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.        1.31        1.12          1.          1.       0.995       0.993

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.496       0.584       0.901
             */



            // Reproduce the embedded SAP curve c; do not inherit the HotFinished default (curve a).
            var section = new SteelSection(new SectionRHS(400, 200, 8, 8, 15, 15, ""),
                SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.ColdFormed);

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity / (5225.6 * 1000.0), 1, 0.01);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity / (615.258 * 1e6), 1, 0.03);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity / (435.088 * 1e6), 1, 0.03);


            Assert.AreEqual(1.0, EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 0.001);
            Assert.AreEqual(1.0, EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 0.001);
            Assert.AreEqual(1.0, EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 0.001);

            Assert.AreEqual(0.895 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.0107);
            Assert.AreEqual(0.496 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.01);
            Assert.AreEqual(0.584 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.013);
            //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.0105);

            Assert.AreEqual(548219.667 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr1, 1, 0.01);
            Assert.AreEqual(226849.304 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr2, 1, 0.01);

            Assert.AreEqual(0.48 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.5 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 1, 0.01);
            //Assert.AreEqual(0.479, EN1993P11Checker.EN1993p11BeamStationResults[0].PhiLTBuckling, 0.001); //SAP calcola in modo diverso non documentato
        }

        [TestMethod]
        public void SectionRHSClassification()
        {
            // Nominal classification at fy, sharp corners, EN 1993-1-1:2005 Table 5.2.
            // epsilon=sqrt(235/355); web c/t=38, flange c/t=22.
            // Uniform compression: 4; pure M1: 1; pure M2: 4.
            // See tools/Steel.LegacyDiagnostics/RHS_CLASSIFICATION_STUDY.txt.
            double L = 1000;
            double h = 500;
            double b = 300;
            double t = 12.5;

            var section355 = new SteelSection(new SectionRHS(h, b, t, t, t, t, string.Empty), new SteelMaterial("S355", 210000, 355, 510));

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1500e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-2859e3 * 0.95, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-6029e3 * 0.95, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-6029e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 0, 1e6, CoordinateSystem.Global),
            };
            AssertRhsClassificationCases(section355, L, resultBeamForces,
                EN1993p11Checker.SectionClass.Class4,
                new[] { EN1993p11Checker.SectionClass.Class4, EN1993p11Checker.SectionClass.Class4, EN1993p11Checker.SectionClass.Class4, EN1993p11Checker.SectionClass.Class4, EN1993p11Checker.SectionClass.Class1, EN1993p11Checker.SectionClass.Class4 });
        }

        [TestMethod]
        public void SectionRHSClasstification2()
        {
            // Historical reference: Cordova chapter 2, page 75; original unavailable.
            // This test qualifies the sharp-corner inputs, not the original book example.
            // Table 5.2: web c/t=33, flange c/t=23, fy=355 MPa.
            // Uniform compression: 3; pure M1: 1; pure M2: 3.
            double h = 350;
            double b = 250;
            double t = 10;
            double L = 500;

            var section355 = new SteelSection(new SectionRHS(h, b, t, t, t, t, string.Empty), SteelMaterialEN1993Data.S355);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1300e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-1700e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-2160e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 0, 1e6, CoordinateSystem.Global),
            };
            AssertRhsClassificationCases(section355, L, resultBeamForces,
                EN1993p11Checker.SectionClass.Class3,
                new[] { EN1993p11Checker.SectionClass.Class3, EN1993p11Checker.SectionClass.Class3, EN1993p11Checker.SectionClass.Class3, EN1993p11Checker.SectionClass.Class1, EN1993p11Checker.SectionClass.Class3 });
        }

        private static void AssertRhsClassificationCases(SteelSection section, double length,
            ResultBeamForces[] forces, EN1993p11Checker.SectionClass compressionClass,
            EN1993p11Checker.SectionClass[] expectedClasses)
        {
            Assert.IsTrue(forces.Length == expectedClasses.Length);
            var beamElement = new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), section);
            var stations = new StationResultBeamForces[forces.Length];
            for (int i = 0; i < forces.Length; i++)
            {
                // Independent load cases, not a force diagram invented along one member.
                var loadCase = new LoadCase("RHS classification " + i, LoadCase.LoadCaseTypes.SelfWeight);
                stations[i] = new StationResultBeamForces(loadCase, forces[i], 0.0);
                beamElement.Results.Add(new BeamResult(new[] { stations[i] }));
            }
            var options = new EN1993p11Checker.EN1993p11Options(
                EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);
            var checker355 = new EN1993p11Checker(beamElement, options, new StandardEN1993p11());
            checker355.PerformCheck();

            var results = checker355.EN1993p11BeamStationResults;
            Assert.IsTrue(results.Length == forces.Length);
            int unsupportedCases = 0;
            for (int i = 0; i < forces.Length; i++)
            {
                var result = results[i];
                Assert.IsTrue(result != null);
                Assert.IsTrue(ReferenceEquals(result.StationResultBeamForces, stations[i]));
                Assert.IsTrue(result.AxialCompressionClass == compressionClass);
                // Cases contain either N alone or one pure bending moment.
                var actualClass = forces[i].N < 0
                    ? result.AxialCompressionClass : result.BendingCompressionClass;
                Assert.IsTrue(actualClass == expectedClasses[i]);
                if (expectedClasses[i] == EN1993p11Checker.SectionClass.Class4)
                {
                    unsupportedCases++;
                    Assert.IsTrue(double.IsNaN(result.WorkingRatio));
                    Assert.IsTrue(double.IsNaN(result.AxialCompressionCapacity));
                    Assert.IsTrue(double.IsNaN(result.BendingMoment1Capacity));
                    Assert.IsTrue(double.IsNaN(result.BendingMoment2Capacity));
                }
                else
                {
                    // Evaluation must continue after a rejected class-4 case.
                    Assert.IsTrue(!double.IsNaN(result.WorkingRatio) && !double.IsInfinity(result.WorkingRatio));
                }
            }
            Assert.IsTrue(checker355.GetErrorLog().Count == unsupportedCases);
            foreach (string error in checker355.GetErrorLog())
                Assert.IsTrue(error.Contains("Class 4 requires verified effective section properties"));
        }

        #endregion

        #region Section H

        [TestMethod]
        public void SectionHBuckling1()
        {
            // Requalified for the declared S355 inputs; not a reproduction of Cordova p.149.
            // Independent reference and unchanged historical test:
            // tools/Steel.LegacyDiagnostics/LEGACY_CLOSURE_01_HEA300.txt.
            // Effective lengths are explicit inputs (13000/6500 mm), not inferred restraints.
            double L = 6500;

            double h = 290.0;
            double width = 300.0;
            double flangeThickness = 14.0;
            double webThickness = 8.5;
            double r = 27.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1320 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0.5;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            // Decimal reference: exact rectangle/fillet integration and bisection of
            // lambda^2*chi^2 - 2*Phi*chi + 1 = 0, independently of GPC.
            // EN 1993-1-1:2005 + AC:2006/2009, 6.3.1.1-3, tables 5.2, 6.1, 6.2.
            Assert.AreEqual(355.0, EN1993P11Checker.Fy);
            Assert.AreEqual(1.0, standardEN1993P11.GammaM1);
            Assert.AreEqual(11252.778955533041, sectionH.Area, 11252.778955533041 * 1e-10);
            Assert.AreEqual(182634978.73102460, sectionH.J11, 182634978.73102460 * 1e-10);
            Assert.AreEqual(63095590.87174197, sectionH.J22, 63095590.87174197 * 1e-10);
            Assert.AreEqual(0, EN1993P11Checker.GetErrorLog().Count);
            Assert.AreEqual(1, EN1993P11Checker.EN1993p11BeamStationResults.Length);
            var result = EN1993P11Checker.EN1993p11BeamStationResults[0];
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class3, result.AxialCompressionClass);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.b, result.BuckingCurve1);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.c, result.BuckingCurve2);

            // Rows: principal axes 1 (y, strong) and 2 (z, weak).
            // Columns: Lcr [mm], alpha, Ncr [N], lambda_bar, Phi, chi, Nb,Rd [N], NEd/Nb,Rd.
            double[][] expected = {
                new[] { 13000.0, .34, 2239836.377953149, 1.335475495222930,
                    1.584778233358363, .410168341836943, 1638514.458263264, .805607782917661 },
                new[] { 6500.0, .49, 3095218.686035181, 1.136052498513295,
                    1.374640501824907, .465418233803958, 1859223.219939041, .709973921282717 }
            };
            double[][] actual = {
                new[] { result.LengthAxialBuckling1, result.AlphaAxialBuckling1, result.NCr1,
                    result.LambdaSignedAxialBuckling1, result.PhiAxialBuckling1, result.ChiAxialBuckling1,
                    result.AxialBuckling1Capacity, result.AxialBuckling1WorkingRatio },
                new[] { result.LengthAxialBuckling2, result.AlphaAxialBuckling2, result.NCr2,
                    result.LambdaSignedAxialBuckling2, result.PhiAxialBuckling2, result.ChiAxialBuckling2,
                    result.AxialBuckling2Capacity, result.AxialBuckling2WorkingRatio }
            };
            string[] quantities = { "Lcr", "alpha", "Ncr", "lambda_bar", "Phi", "chi", "Nb,Rd", "NEd/Nb,Rd" };
            for (int axis = 0; axis < 2; axis++)
                for (int quantity = 0; quantity < quantities.Length; quantity++)
                    Assert.AreEqual(expected[axis][quantity], actual[axis][quantity],
                        Math.Abs(expected[axis][quantity]) * 1e-10,
                        "Axis " + (axis + 1) + ": " + quantities[quantity]);
        }

        [TestMethod]
        public void SectionHBucklingExample1()
        {
            double L = 3000;

            double h = 206.2;
            double width = 204.3;
            double flangeThickness = 12.5;
            double webThickness = 7.9;
            double r = 10.2;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expNbRd = 1605190; // lui usa gammaM1 = 1.05
            double expWR = 0.62;

            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - expNbRd) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity * 100 < 1.0);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2WorkingRatio - expWR) < 0.01);
        }

        [TestMethod]
        public void SectionHBucklingExample2()
        {
            double L = 2800;

            double h = 161.8;
            double width = 154.4;
            double flangeThickness = 11.5;
            double webThickness = 8.0;
            double r = 7.6;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400 * 1000, 0, 0, 0, 32.36 * 1e6, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.OneSideRestrained_OneSideHinged, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 0.85, 1, 0.85, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expNbRd = 924.81 * 1e3;
            double expWRAB = 0.43;
            double expMRd = 79.79 * 1e6;
            double expWRM = 0.41;
            double expWR = 0.84;

            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - expNbRd) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity * 100 < 1.0);

            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2WorkingRatio - expWRAB) < 0.06);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity - expMRd) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity * 100 < 4.0);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTorsionalBucklingWorkingRatio - expWRM) < 0.02);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].WorkingRatio - expWR) < 0.02);
        }

        [TestMethod]
        public void SectionHLateralTorsionalBuckling1()
        {
            // Cordova Costruzioni in acciaio pagina 190
            double L = 5000;

            double h = 300.0;
            double width = 150.0;
            double flangeThickness = 10.7;
            double webThickness = 7.1;
            double r = 15.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.4 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                null, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            // Requalified arithmetic for unchanged S355 inputs; Cordova p.190
            // is not reproduced. See LEGACY_CLOSURE_04.txt and archived original.
            Assert.AreEqual(355.0, EN1993P11Checker.Fy);
            Assert.AreEqual(1.0, standardEN1993P11.GammaM0);
            Assert.AreEqual(1.0, standardEN1993P11.GammaM1);
            Assert.AreEqual(0, EN1993P11Checker.GetErrorLog().Count, string.Join("; ", EN1993P11Checker.GetErrorLog()));
            Assert.AreEqual(1, EN1993P11Checker.EN1993p11BeamStationResults.Length);
            var result = EN1993P11Checker.EN1993p11BeamStationResults[0];
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, result.BendingCompressionClass);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.LateralTorsionalBuckingCurves.b, result.LateraltorsionalBucklingCurve);
            Assert.AreEqual(5000.0, EN1993P11Checker.GetLengthCriticalMoment1());
            Assert.AreEqual(5000.0, EN1993P11Checker.GetLengthLatTorsBuckling());
            // Analytic geometry; pinned torsion properties are explicit Model inputs.
            Assert.AreEqual(5381.2016529422963d, sectionH.Area, 5381.2016529422963d * 1e-10, "Area");
            Assert.AreEqual(83561091.858479753d, sectionH.J11, 83561091.858479753d * 1e-10, "J11");
            Assert.AreEqual(6037784.2439929144d, sectionH.J22, 6037784.2439929144d * 1e-10, "J22");
            Assert.AreEqual(557073.94572319835d, sectionH.Wel1, 557073.94572319835d * 1e-10, "Wel1");
            Assert.AreEqual(80503.789919905525d, sectionH.Wel2, 80503.789919905525d * 1e-10, "Wel2");
            Assert.AreEqual(628365.31263994751d, sectionH.Wpl1, 628365.31263994751d * 1e-10, "Wpl1");
            Assert.AreEqual(199186.91786049944d, sectionH.Jt, 199186.91786049944d * 1e-10, "Jt");
            Assert.AreEqual(125760427779.96912d, sectionH.Jw, 125760427779.96912d * 1e-10, "Jw");
            // Four fillets, 64 chords each: geometric bound independent of observed error.
            double areaExcess = 4 * r * r / 2 * (Math.PI / 2 - 64 * Math.Sin(Math.PI / 128));
            Assert.AreEqual(628355.88646072743d, sectionH.Wpl1, areaExcess * h / 2, "Wpl1 bound");
            var expected = new[] {
                new { Name = nameof(result.BendingMoment1Capacity), Value = 223069685.98718137d },
                new { Name = nameof(result.MCr), Value = 96982272.846838504d },
                new { Name = nameof(result.LambdaSignedLTBuckling), Value = 1.5166106417506757d },
                new { Name = nameof(result.AlphaLTBuckling), Value = 0.34000000000000002d },
                new { Name = nameof(result.PhiLTBuckling), Value = 1.5523642485993885d },
                new { Name = nameof(result.ChiLTBuckling), Value = 0.42019228700876371d },
                new { Name = nameof(result.LateralTosionalBucklingCapacity), Value = 93732161.517280504d },
            };
            foreach (var item in expected)
            {
                double actual = (double)result.GetType().GetProperty(item.Name).GetValue(result);
                Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), item.Name);
                Assert.AreEqual(item.Value, actual, 1e-10 * Math.Max(1, Math.Abs(item.Value)), item.Name);
            }
        }

        [TestMethod]
        public void SectionHLateralTorsionalBuckling2()
        {
            // Cordova Costruzioni in acciaio pagina 190
            double L = 8000;

            double h = 300.0;
            double width = 150.0;
            double flangeThickness = 10.7;
            double webThickness = 7.1;
            double r = 15.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 66.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;
            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.SingleForce,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 0.5, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.TopSection);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            // Requalified arithmetic for unchanged S355 inputs; Cordova p.190
            // is not reproduced. See LEGACY_CLOSURE_05.txt and archived original.
            Assert.AreEqual(355.0, EN1993P11Checker.Fy);
            Assert.AreEqual(1.0, standardEN1993P11.GammaM0);
            Assert.AreEqual(1.0, standardEN1993P11.GammaM1);
            Assert.AreEqual(0, EN1993P11Checker.GetErrorLog().Count, string.Join("; ", EN1993P11Checker.GetErrorLog()));
            Assert.AreEqual(1, EN1993P11Checker.EN1993p11BeamStationResults.Length);
            var result = EN1993P11Checker.EN1993p11BeamStationResults[0];
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, result.BendingCompressionClass);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.LateralTorsionalBuckingCurves.b, result.LateraltorsionalBucklingCurve);
            Assert.AreEqual(4000.0, EN1993P11Checker.GetLengthCriticalMoment1());
            Assert.AreEqual(4000.0, EN1993P11Checker.GetLengthLatTorsBuckling());
            // Analytic geometry; pinned torsion properties are explicit Model inputs.
            Assert.AreEqual(5381.2016529422963d, sectionH.Area, 5381.2016529422963d * 1e-10, "Area");
            Assert.AreEqual(83561091.858479753d, sectionH.J11, 83561091.858479753d * 1e-10, "J11");
            Assert.AreEqual(6037784.2439929144d, sectionH.J22, 6037784.2439929144d * 1e-10, "J22");
            Assert.AreEqual(557073.94572319835d, sectionH.Wel1, 557073.94572319835d * 1e-10, "Wel1");
            Assert.AreEqual(80503.789919905525d, sectionH.Wel2, 80503.789919905525d * 1e-10, "Wel2");
            Assert.AreEqual(628365.31263994751d, sectionH.Wpl1, 628365.31263994751d * 1e-10, "Wpl1");
            Assert.AreEqual(199186.91786049944d, sectionH.Jt, 199186.91786049944d * 1e-10, "Jt");
            Assert.AreEqual(125760427779.96912d, sectionH.Jw, 125760427779.96912d * 1e-10, "Jw");
            // Four fillets, 64 chords each: geometric bound independent of observed error.
            double areaExcess = 4 * r * r / 2 * (Math.PI / 2 - 64 * Math.Sin(Math.PI / 128));
            Assert.AreEqual(628355.88646072743d, sectionH.Wpl1, areaExcess * h / 2, "Wpl1 bound");
            var expected = new[] {
                new { Name = nameof(result.BendingMoment1Capacity), Value = 223069685.98718137d },
                new { Name = nameof(result.MCr), Value = 136892269.90632012d },
                new { Name = nameof(result.LambdaSignedLTBuckling), Value = 1.2765293821886032d },
                new { Name = nameof(result.AlphaLTBuckling), Value = 0.34000000000000002d },
                new { Name = nameof(result.PhiLTBuckling), Value = 1.260082718818619d },
                new { Name = nameof(result.ChiLTBuckling), Value = 0.55755892462217771d },
                new { Name = nameof(result.LateralTosionalBucklingCapacity), Value = 124374494.23481971d },
            };
            foreach (var item in expected)
            {
                double actual = (double)result.GetType().GetProperty(item.Name).GetValue(result);
                Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), item.Name);
                Assert.AreEqual(item.Value, actual, 1e-10 * Math.Max(1, Math.Abs(item.Value)), item.Name);
            }
        }

        [TestMethod]
        public void SectionCLateralTorsionalBuckling2()
        {
            // Cordova Costruzioni in acciaio pagina 211
            double L = 5000;

            double h = 300.0;
            double width = 100.0;
            double flangeThickness = 16.0;
            double webThickness = 10.0;
            double r = 16.0;

            var sectionC = new SteelSection(new SectionC(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.40 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionC);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expMbRd = 95.6 * 1.05; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1
            double expMcRd = 165.5 * 1.05;

            UnitsSystem units = new UnitsSystem(GPC.Utilities.Units.UnitsConvert.LengthUnits.m, GPC.Utilities.Units.UnitsConvert.ForceUnits.kN,
                GPC.Utilities.Units.UnitsConvert.MassUnits.kg, GPC.Utilities.Units.UnitsConvert.PressureUnits.kPa, GPC.Utilities.Units.UnitsConvert.TemperatureUnits.C);

            Assert.IsTrue((Math.Abs(Units.ConverMomentFromDefault(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity, units) - expMbRd) / expMbRd * 100) < 1);
            Assert.IsTrue((Math.Abs(Units.ConverMomentFromDefault(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, units) - expMcRd) / expMcRd * 100) < 1);
        }

        [TestMethod]
        public void SectionHInteraction1()
        {
            // Cordova Costruzioni in acciaio pagina 228
            // NOTA: i risultati dei tassi di lavoro delle interazioni che si usano per confronto sono presi dal Cordova.
            // Lui usa un metodo semplificato, per questo i valori che ottengo col metodo esatto sono distanti quel 2/3 %
            double L = 5000;

            double h = 300.0;
            double width = 150.0;
            double flangeThickness = 10.7;
            double webThickness = 7.1;
            double r = 15.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness,
                string.Empty, r), SteelMaterialEN1993Data.S235, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 0.25, 0.25, 1, 1, 1, 0.1, 0.1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expInteractionWR = 1.00 / 1.05; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].BucklingInteraction1Axis - expInteractionWR) * 100) < 1.5);
        }

        [TestMethod]
        public void SectionHInteraction2()
        {
            // Annex A, Method 1, for the unchanged declared inputs. The former
            // comparison with an unidentified simplified method is archived, not reproduced.
            // See tools/Steel.LegacyDiagnostics/LEGACY_CLOSURE_02_IPE300.txt.
            double L = 5000;

            double h = 300.0;
            double width = 150.0;
            double flangeThickness = 10.7;
            double webThickness = 7.1;
            double r = 15.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r),
                SteelMaterialEN1993Data.S235, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-190.0 * 1000, 0, 0, 0, 78.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            // Preserve this benchmark's gammaM0=gammaM1=1.05. This is an explicit
            // arithmetic configuration, not qualification of a current National Annex.
            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11 { GammaM1 = 1.05 };
            EN1993p11Checker Checker = new EN1993p11Checker(beamElement, options, standard);

            Checker.PerformCheck();

            Assert.AreEqual(235.0, Checker.Fy);
            Assert.AreEqual(1.05, standard.GammaM0);
            Assert.AreEqual(1.05, standard.GammaM1);
            Assert.AreEqual(0, Checker.GetErrorLog().Count, string.Join("; ", Checker.GetErrorLog()));
            Assert.AreEqual(1, Checker.EN1993p11BeamStationResults.Length);
            var result = Checker.EN1993p11BeamStationResults[0];
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, result.BendingCompressionClass);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.a, result.BuckingCurve1);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.b, result.BuckingCurve2);
            Assert.AreEqual(5000.0, result.LengthAxialBuckling1);
            Assert.AreEqual(5000.0, result.LengthAxialBuckling2);

            // A/I/Wel: independent rectangle/fillet integrals. Wpl/It/Iw: declared
            // Model inputs to the interaction benchmark, not independent torsion validation.
            Assert.AreEqual(5381.2016529422963d, sectionH.Area, 5381.2016529422963d * 1e-10, "Area");
            Assert.AreEqual(83561091.858479753d, sectionH.J11, 83561091.858479753d * 1e-10, "J11");
            Assert.AreEqual(6037784.2439929144d, sectionH.J22, 6037784.2439929144d * 1e-10, "J22");
            Assert.AreEqual(557073.94572319835d, sectionH.Wel1, 557073.94572319835d * 1e-10, "Wel1");
            Assert.AreEqual(80503.789919905525d, sectionH.Wel2, 80503.789919905525d * 1e-10, "Wel2");
            Assert.AreEqual(199186.91786049944d, sectionH.Jt, 199186.91786049944d * 1e-10, "Jt");
            Assert.AreEqual(125760427779.96912d, sectionH.Jw, 125760427779.96912d * 1e-10, "Jw");
            Assert.AreEqual(628365.31263994751d, sectionH.Wpl1, 628365.31263994751d * 1e-10, "Wpl1");
            Assert.AreEqual(125219.46804033165d, sectionH.Wpl2, 125219.46804033165d * 1e-10, "Wpl2");

            // Frozen Decimal reference from ipe300_annex_a_reference.py; no SUT outputs
            // were used as interaction expectations. Wpl/It/Iw remain declared inputs.
            // The separate axial capacity may use 6.3.1.2(4) when N/Ncr <= .04;
            // Annex A interaction denominators retain the full buckling reductions.
            var expected = new[] {
                new { Name = nameof(result.NCr1), Value = 6927605.3277167855d },
                new { Name = nameof(result.NCr2), Value = 500560.55235765979d },
                new { Name = nameof(result.NcrTorsional), Value = 1592417.3465935264d },
                new { Name = nameof(result.NcrFlexuralTorsional), Value = 1592417.3465935264d },
                new { Name = nameof(result.LambdaSignedAxialBuckling1), Value = 0.42724992565953912d },
                new { Name = nameof(result.LambdaSignedAxialBuckling2), Value = 1.5894440831975896d },
                new { Name = nameof(result.ChiAxialBuckling1), Value = 0.94546838903303043d },
                new { Name = nameof(result.ChiAxialBuckling2), Value = 0.31130502928139725d },
                new { Name = nameof(result.MCr), Value = 203911478.89732334d },
                new { Name = nameof(result.Lambda0LTBuckling), Value = 1.1321548426377908d },
                new { Name = nameof(result.LambdaSignedLTBuckling), Value = 0.85097969055286327d },
                new { Name = nameof(result.ChiLTBuckling), Value = 0.89954248930128566d },
                new { Name = nameof(result.LateralTosionalBucklingCapacity), Value = 126506385.6360371d },
                new { Name = nameof(result.CmX0), Value = 0.78674173124301827d },
                new { Name = nameof(result.CmY0), Value = 0.74490655447440879d },
                new { Name = nameof(result.Cmx), Value = 0.92859571204491853d },
                new { Name = nameof(result.Cmy), Value = 0.74490655447440879d },
                new { Name = nameof(result.CmLT), Value = 1.1637557769200739d },
                new { Name = nameof(result.Mux), Value = 0.99846457353649998d },
                new { Name = nameof(result.Muy), Value = 0.70356069267877419d },
                new { Name = nameof(result.Wx), Value = 1.127974692523446d },
                new { Name = nameof(result.Wy), Value = 1.5d },
                new { Name = nameof(result.EpsilonX), Value = 3.9655864469387834d },
                new { Name = nameof(result.BLT), Value = 0d },
                new { Name = nameof(result.CLT), Value = 0.74593073599183501d },
                new { Name = nameof(result.DLT), Value = 0d },
                new { Name = nameof(result.ELT), Value = 0.19667125351543499d },
                new { Name = nameof(result.Cxx), Value = 0.93874292261700043d },
                new { Name = nameof(result.Cxy), Value = 0.58093441279657043d },
                new { Name = nameof(result.Cyx), Value = 0.70317271530203174d },
                new { Name = nameof(result.Cyy), Value = 0.95009200252252168d },
                new { Name = nameof(result.Kxx), Value = 1.1818220219494955d },
                new { Name = nameof(result.Kxy), Value = 1.4277918057622121d },
                new { Name = nameof(result.Kyx), Value = 0.57844350580181003d },
                new { Name = nameof(result.Kyy), Value = 0.8890947213803142d },
                new { Name = nameof(result.BucklingInteraction1Axis), Value = 0.89553427227618121d },
                new { Name = nameof(result.BucklingInteraction2Axis), Value = 0.86341924503317513d },
                new { Name = nameof(result.AxialBuckling1Capacity), Value = 1204364.1794680378d },
                new { Name = nameof(result.AxialBuckling2Capacity), Value = 374924.62615476345d },
            };
            foreach (var item in expected)
            {
                var property = result.GetType().GetProperty(item.Name);
                double actual = (double)property.GetValue(result);
                Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), item.Name);
                Assert.AreEqual(item.Value, actual, 1e-10 * Math.Max(1, Math.Abs(item.Value)), item.Name);
            }
        }

        [TestMethod]
        public void SectionHInteraction3()
        {
            // Requalified Annex A biaxial benchmark for the unchanged declared inputs.
            // The original Cordova p.237 comparison is archived, not reproduced.
            // See tools/Steel.LegacyDiagnostics/LEGACY_CLOSURE_03_HEA260.txt.
            double L = 4000;

            double h = 250.0;
            double width = 260;
            double flangeThickness = 12.5;
            double webThickness = 7.5;
            double r = 24.0;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r),
                SteelMaterialEN1993Data.S235, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400.0 * 1000, 0, 0, 0, 71.0 * 1000000, 30 * 1000000, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamElement, options, standard);

            Checker.PerformCheck();

            Assert.AreEqual(235.0, Checker.Fy);
            Assert.AreEqual(1.0, standard.GammaM0);
            Assert.AreEqual(1.0, standard.GammaM1);
            Assert.AreEqual(0, Checker.GetErrorLog().Count, string.Join("; ", Checker.GetErrorLog()));
            Assert.AreEqual(1, Checker.EN1993p11BeamStationResults.Length);
            Assert.IsTrue(sectionH.IsDoubleSymmetric);
            var result = Checker.EN1993p11BeamStationResults[0];
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, result.AxialCompressionClass);
            Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, result.BendingCompressionClass);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.b, result.BuckingCurve1);
            Assert.AreEqual(EN1993p11Checker.EN1993p11Options.AxialBuckingCurves.c, result.BuckingCurve2);
            Assert.AreEqual(4000.0, result.LengthAxialBuckling1);
            Assert.AreEqual(4000.0, result.LengthAxialBuckling2);

            // A/I/Wel are independently integrated. It/Iw remain declared Model inputs.
            // Wpl values used in the chain are pinned and also checked against the
            // independent exact integrals with a geometric discretization bound below.
            Assert.AreEqual(8681.9426315322798d, sectionH.Area, 8681.9426315322798d * 1e-10, "Area");
            Assert.AreEqual(104549554.33142601d, sectionH.J11, 104549554.33142601d * 1e-10, "J11");
            Assert.AreEqual(36675632.290801637d, sectionH.J22, 36675632.290801637d * 1e-10, "J22");
            Assert.AreEqual(836396.43465140811d, sectionH.Wel1, 836396.43465140811d * 1e-10, "Wel1");
            Assert.AreEqual(282120.24839078187d, sectionH.Wel2, 282120.24839078187d * 1e-10, "Wel2");
            Assert.AreEqual(541937.28763577028d, sectionH.Jt, 541937.28763577028d * 1e-10, "Jt");
            Assert.AreEqual(516240692070.47272d, sectionH.Jw, 516240692070.47272d * 1e-10, "Jw");
            Assert.AreEqual(919789.9015433467d, sectionH.Wpl1, 919789.9015433467d * 1e-10, "Wpl1");
            Assert.AreEqual(430171.11123419553d, sectionH.Wpl2, 430171.11123419553d * 1e-10, "Wpl2");

            // Four quarter-circle fillets, each replaced by 64 chords. Missing segment
            // area times the maximum lever arm bounds the plastic modulus error.
            double areaExcess = 4 * r * r / 2 * (Math.PI / 2 - 64 * Math.Sin(Math.PI / 128));
            Assert.AreEqual(919771.04789060669d, sectionH.Wpl1, areaExcess * h / 2, "Wpl1 geometric bound");
            Assert.AreEqual(430168.84552502073d, sectionH.Wpl2, areaExcess * width / 2, "Wpl2 geometric bound");

            // Independent Decimal reference: full N/My/Mz Annex A chain, including
            // nonzero bLT/dLT. Separate axial checks may use the 6.3.1.2(4) exemption;
            // interaction denominators retain full buckling reductions.
            var expected = new[] {
                new { Name = nameof(result.NCr1), Value = 13543198.482993022d },
                new { Name = nameof(result.NCr2), Value = 4750908.512044155d },
                new { Name = nameof(result.NcrTorsional), Value = 6801994.6010343712d },
                new { Name = nameof(result.NcrFlexuralTorsional), Value = 6801994.6010343712d },
                new { Name = nameof(result.LambdaSignedAxialBuckling1), Value = 0.38813406913755449d },
                new { Name = nameof(result.LambdaSignedAxialBuckling2), Value = 0.65532095181324068d },
                new { Name = nameof(result.ChiAxialBuckling1), Value = 0.93075438615487527d },
                new { Name = nameof(result.ChiAxialBuckling2), Value = 0.7521325820671303d },
                new { Name = nameof(result.MCr), Value = 817105274.46936047d },
                new { Name = nameof(result.Lambda0LTBuckling), Value = 0.54601075938846644d },
                new { Name = nameof(result.LambdaSignedLTBuckling), Value = 0.51432690429251449d },
                new { Name = nameof(result.ChiLTBuckling), Value = 0.97881668146255096d },
                new { Name = nameof(result.LateralTosionalBucklingCapacity), Value = 211571839.28178489d },
                new { Name = nameof(result.CmX0), Value = 1.0008860536168815d },
                new { Name = nameof(result.CmY0), Value = 1.0025258326843336d },
                new { Name = nameof(result.Cmx), Value = 1.0003769888629279d },
                new { Name = nameof(result.Cmy), Value = 1.0025258326843336d },
                new { Name = nameof(result.CmLT), Value = 1.072331823387348d },
                new { Name = nameof(result.Mux), Value = 0.99789701141016096d },
                new { Name = nameof(result.Muy), Value = 0.97772006042706994d },
                new { Name = nameof(result.Wx), Value = 1.0997056699873371d },
                new { Name = nameof(result.Wy), Value = 1.5d },
                new { Name = nameof(result.EpsilonX), Value = 1.8424813321200415d },
                new { Name = nameof(result.BLT), Value = 0.014768214936021942d },
                new { Name = nameof(result.CLT), Value = 0.19190296585538072d },
                new { Name = nameof(result.DLT), Value = 0.37928186969109473d },
                new { Name = nameof(result.ELT), Value = 1.0890916540935902d },
                new { Name = nameof(result.Cxx), Value = 1.0067482076784615d },
                new { Name = nameof(result.Cxy), Value = 1.0220984144395637d },
                new { Name = nameof(result.Cyx), Value = 0.92815204228664516d },
                new { Name = nameof(result.Cyy), Value = 0.97529442941831157d },
                new { Name = nameof(result.Kxx), Value = 1.0956653354560697d },
                new { Name = nameof(result.Kxy), Value = 0.74893468331944246d },
                new { Name = nameof(result.Kyx), Value = 0.59820814113468201d },
                new { Name = nameof(result.Kyy), Value = 1.097415450709013d },
                new { Name = nameof(result.BucklingInteraction1Axis), Value = 0.80058409463551239d },
                new { Name = nameof(result.BucklingInteraction2Axis), Value = 0.78708668535197812d },
                new { Name = nameof(result.AxialBuckling1Capacity), Value = 2040256.5184100857d },
                new { Name = nameof(result.AxialBuckling2Capacity), Value = 1534543.4032710711d },
            };
            foreach (var item in expected)
            {
                var property = result.GetType().GetProperty(item.Name);
                double actual = (double)property.GetValue(result);
                Assert.IsFalse(double.IsNaN(actual) || double.IsInfinity(actual), item.Name);
                Assert.AreEqual(item.Value, actual, 1e-10 * Math.Max(1, Math.Abs(item.Value)), item.Name);
            }
        }

        [TestMethod]
        public void SectionHLateralTorsionalBucklingExample1()
        {
            double L = 5000;

            double h = 259.6;
            double width = 147.30;
            double flangeThickness = 12.7;
            double webThickness = 7.2;
            double r = 7.6;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expLTB = 97.90 * 1e6;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity - expLTB) / expLTB * 100) < 0.5);
        }

        [TestMethod]
        public void SectionHLateralTorsionalBucklingExample2()
        {
            double L = 6000;

            double h = 310.4;
            double width = 166.9;
            double flangeThickness = 13.7;
            double webThickness = 7.9;
            double r = 8.9;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                0, 0, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expLTB = 232.65 * 1e6;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity - expLTB) / expLTB * 100) < 0.5);
        }

        [TestMethod]
        public void SectionHLateralTorsionalBucklingExample3()
        {
            double L = 6000;

            double h = 259.6;
            double width = 147.30;
            double flangeThickness = 12.7;
            double webThickness = 7.2;
            double r = 7.6;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expLTB = 94.17 * 1e6;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity - expLTB) / expLTB * 100) < 0.5);
        }

        [TestMethod]
        public void SectionHShearExample1()
        {
            double L = 6000;

            double h = 259.6;
            double width = 147.30;
            double flangeThickness = 12.7;
            double webThickness = 7.2;
            double r = 7.6;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 90000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expShear2Cap = 320.72 * 1e3;
            double expShear2WR = 0.28;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2Capacity - expShear2Cap) / expShear2Cap * 100) < 0.5);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2WorkingRatio - expShear2WR) < 0.5);
        }

        [TestMethod]
        public void SectionHShearExample2()
        {
            double L = 6000;

            double h = 254.0;
            double width = 101.6;
            double flangeThickness = 6.8;
            double webThickness = 5.7;
            double r = 7.6;

            var sectionH = new SteelSection(new SectionH(h, webThickness, width, flangeThickness, width, flangeThickness, string.Empty, r), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 100000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), sectionH);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expShear2Cap = 247.68 * 1e3;
            double expShear2WR = 0.40;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2Capacity - expShear2Cap) / expShear2Cap * 100) < 0.5);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2WorkingRatio - expShear2WR) < 0.5);
        }

        [TestMethod]
        public void Sap_SectionHAsymmetricTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  3        X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  2.        Shape:  H Asymmetric    Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 3         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.014        eNy=0.            eNz=0.            
 A=0.014           Iyy=3.193E-04     iyy=0.152         Wel,yy=0.001        Weff,yy=0.001     
 It=1.751E-06      Izz=6.297E-05     izz=0.067         Wel,zz=4.198E-04    Weff,zz=4.198E-04 
 Iw=1.317E-06      Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.009        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=6.756E-04    Av,z=0.005        


 DESIGN MESSAGES
     Error: Section overstressed

 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    1.127 = 0.135 + 0.377 + 0.615   >         0.95  Overstress
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      79.115
     Major Braced          1.          1.      79.115
     Minor (z-z)           1.          1.     178.161
     Minor Braced          1.          1.     178.161
     LTB                   1.          1.     178.161

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4927.4      4927.4

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4927.4    5096.736    4810.607     872.711          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    4596.152       1.035       1.178       0.575    2831.897
     MajorB(y-y)    b    0.34    4596.152       1.035       1.178       0.575    2831.897
     Minor (z-z)    c    0.49     906.327       2.332       3.741        0.15     739.234
     MinorB(z-z)    c    0.49     906.327       2.332       3.741        0.15     739.234
     Torsional TF   c    0.49     872.711       2.376       3.856       0.145     714.807

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      418.54      418.54      418.54     243.592
     Minor (z-z)        -100.       -100.     149.027     149.027     149.027

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 1       0.814       0.531      -0.959

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.929        1.11       0.582   1.317E-06     484.759

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.271  -0.076       0.347      -0.054      -0.022

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.995        0.07       0.099        0.01       0.018       0.991       0.905

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
             0.02       1.384         1.5       0.922       0.888         0.9       0.943

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.977       0.902       1.016       1.005       1.004       0.918       0.917
             */

            var section = new SteelSection(new SectionH(400, 12, 200, 10, 300, 25, string.Empty), SteelMaterialEN1993Data.S355);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(4927.4 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialTensionCapacity, 1, 0.01);
            Assert.AreEqual(418.54 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.01);
            Assert.AreEqual(149.027 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.01);
            Assert.AreEqual(484.759 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity, 1, 0.01);

            Assert.AreEqual(0.575 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.15 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.775 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.01);

            Assert.AreEqual(1.005 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.01);
            Assert.AreEqual(1.004 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.01);
            Assert.AreEqual(0.918 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.01);
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.01);
        }

        [TestMethod]
        public void Sap_SectionHAsymmetricTest2()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  10       X Mid:  -19.5     Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  2.        Shape:  H Asymmetric    Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 3         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.014        eNy=0.            eNz=0.            
 A=0.014           Iyy=3.193E-04     iyy=0.152         Wel,yy=0.001        Weff,yy=0.001     
 It=1.751E-06      Izz=6.297E-05     izz=0.067         Wel,zz=4.198E-04    Weff,zz=4.198E-04 
 Iw=1.317E-06      Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.009        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=6.756E-04    Av,z=0.005        


 DESIGN MESSAGES
     Error: Section overstressed

 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.       -100.       -100.       4.167       4.167          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    1.114 = 0.135 + 0.363 + 0.615   >         0.95  Overstress
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      79.115
     Major Braced          1.          1.      79.115
     Minor (z-z)           1.          1.     178.161
     Minor Braced          1.          1.     178.161
     LTB                   1.          1.     178.161

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4927.4      4927.4

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4927.4    5096.736    4810.607     872.711          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    4596.152       1.035       1.178       0.575    2831.897
     MajorB(y-y)    b    0.34    4596.152       1.035       1.178       0.575    2831.897
     Minor (z-z)    c    0.49     906.327       2.332       3.741        0.15     739.234
     MinorB(z-z)    c    0.49     906.327       2.332       3.741        0.15     739.234
     Torsional TF   c    0.49     872.711       2.376       3.856       0.145     714.807

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)        -100.       -100.      418.54      418.54      418.54     252.637
     Minor (z-z)        -100.       -100.     149.027     149.027     149.027

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 1       0.814       0.531      -0.959

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.894       1.069       0.604   1.317E-06      523.85

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.271  -0.076       0.347      -0.054       0.022

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.995       0.062       0.088       0.009       0.017       0.991       0.905

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
             0.02       1.384         1.5       0.925       0.893       0.901       0.943

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.977       0.902       1.016       1.005       1.004       0.918       0.917
             */

            var section = new SteelSection(new SectionH(300, 25, 300, 25, 150, 25, string.Empty), SteelMaterialEN1993Data.S355);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(6212.5 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialTensionCapacity, 1, 0.01);
            Assert.AreEqual(637.891 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.01);
            Assert.AreEqual(263.477 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.01);

            Assert.AreEqual(0.396 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.123 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.121 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.01);

            Assert.AreEqual(1.073 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.01);
            Assert.AreEqual(0.683 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.01);
            Assert.AreEqual(0.586 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.01);
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.01);
        }

        [TestMethod]
        public void SectionHClasstification1()
        {
            var section275 = new SteelSection(new SectionH(270, 8.0, 280, 13.0, 280, 13.0,
                string.Empty, 24), SteelMaterialEN1993Data.S420, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1000e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + 5000, 0, 0), section275);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamElement, options, standard);

            checker355.PerformCheck();

            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void SectionHClasstification2()
        {
            var section275 = new SteelSection(new SectionH(550, 11.1, 210, 17.2, 210, 17.2,
                string.Empty, 24), SteelMaterialEN1993Data.S275, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1000e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + 5000, 0, 0), section275);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamElement, options, standard);

            checker355.PerformCheck();

            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class4);
        }

        #endregion

        #region Section CHS

        [TestMethod]
        public void Sap_SectionCHSTest1()
        {
            /*
			 Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  1        X Mid:  0.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  0.        Shape:  CHS             Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 2         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.012        eNy=0.            eNz=0.            
 A=0.012           Iyy=2.331E-04     iyy=0.138         Wel,yy=0.001        Weff,yy=0.001     
 It=4.662E-04      Izz=2.331E-04     izz=0.138         Wel,zz=0.001        Weff,zz=0.001     
 Iw=0.             Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.008        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=0.002        Av,z=0.008        


 STRESS CHECK FORCES & MOMENTS
	 Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
	 1.                  100.        100.       -100.        -50.         50.          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
	 D/C Ratio:    0.285 = 0.023 + sqrt[(0.185)^2 + (0.185)^2  ] <         0.95          OK
						= (NEd/NRd) + sqrt[(My,Ed/My,Rd)^2 + (Mz,Ed/Mz,Rd)^2]       (EC3 6.2.1(7))  

 BASIC FACTORS
	 Buckling Mode   K Factor    L Factor       Lcr/i
	 Major (y-y)           1.          1.        7.25
	 Major Braced          1.          1.        7.25
	 Minor (z-z)           1.          1.        7.25
	 Minor Braced          1.          1.        7.25
	 LTB                   1.          1.        7.25

 AXIAL FORCE DESIGN
						  Ned       Nc,Rd       Nt,Rd
						Force    Capacity    Capacity
	 Axial               100.    4349.535    4349.535

					   Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
					 4349.535    4499.012  989601.686  483123.525          1.

				Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
	 Major (y-y)    c    0.49  483123.525       0.095       0.479          1.    4349.535
	 MajorB(y-y)    c    0.49  483123.525       0.095       0.479          1.    4349.535
	 Minor (z-z)    c    0.49  483123.525       0.095       0.479          1.    4349.535
	 MinorB(z-z)    c    0.49  483123.525       0.095       0.479          1.    4349.535
	 Torsional TF   c    0.49  483123.525       0.095       0.479          1.    4349.535

 MOMENT DESIGN
						  Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
					   Moment      Moment    Capacity    Capacity    Capacity    Capacity
	 Major (y-y)         100.        100.     540.073     540.073     540.073     540.073
	 Minor (z-z)        -100.       -100.     540.073     540.073     540.073

					  Section      Flange         Web     Epsilon       Alpha         Psi
	 Compactness      Class 2     Class 2     Class 2       0.814       0.465      -1.046

				Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
	 LTB            d    0.76       0.055       0.446          1.          0.  178276.967

	 Factors      kw       C1          C2          C3
				   1.   1.322          0.       0.728
				   za      zs          zg          zz          zj
				  0.2      0.         0.2          0.          0.

	 Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
			   0.          0.          0.          0.          0.          1.          1.

			  nPL          wy          wz         Cyy         Cyz         Czy         Czz
			   0.       1.305       1.305          1.          1.          1.          1.

			  Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
			0.895       0.895          1.       0.895       0.537       0.537       0.895  
			 */

            double L = 1000;
            double diameter = 400;
            double thickness = 10;
            double? psiz = 0.5;

            // The embedded SAP reference uses curve c (alpha=0.49), hence ColdFormed here.
            var circularSect = new SteelSection(new SectionCHS(diameter, thickness, string.Empty),
                SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.ColdFormed);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), circularSect);
            beamElement.Results.AddRange(beamResults);

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiz, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity / (4349.535 * 1000.0), 1, 0.01);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity / (540.073 * 1e6), 1, 0.01);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity / (540.073 * 1e6), 1, 0.01);


            Assert.AreEqual(1 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(1 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(1 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.01);

            Assert.AreEqual(0.895 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.0135);
            Assert.AreEqual(0.537 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.0137);
            Assert.AreEqual(0.537 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.0137);
            Assert.AreEqual(0.895 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.0131);

            Assert.AreEqual(483123.525 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr1, 1, 0.01);
            Assert.AreEqual(483123.525 * 1000 / EN1993P11Checker.EN1993p11BeamStationResults[0].NCr2, 1, 0.01);

            Assert.AreEqual(0.479 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.479 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.446 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiLTBuckling, 1, 0.015);
        }

        [TestMethod]
        public void SectionCHSInteraction1()
        {
            double L = 6000;
            double diameter = 400;
            double thickness = 10;
            double psix = 0.5;
            double psiy = 0.5;

            var section = new SteelSection(new SectionCHS(diameter, thickness,
                string.Empty), SteelMaterialEN1993Data.S355, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psix, psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamElement, options, standard);

            Checker.PerformCheck();

            double expAxialBuckling1 = 4349535;
            double expAxialBuckling2 = 4349535;

            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity - expAxialBuckling1) / expAxialBuckling1 * 100) < 1);
            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - expAxialBuckling2) / expAxialBuckling2 * 100) < 1);

        }

        [TestMethod]
        public void SectionCHSClassification()
        {
            double L = 1000;

            double diameter = 1219;
            double thickness = 25;

            var section235 = new SteelSection(new SectionCHS(diameter, thickness, string.Empty), new SteelMaterial("S235", 210000, 235, 360), Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            var section275 = new SteelSection(new SectionCHS(diameter, thickness, string.Empty), new SteelMaterial("S275", 210000, 275, 430), Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            var section355 = new SteelSection(new SectionCHS(diameter, thickness, string.Empty), new SteelMaterial("S355", 210000, 355, 510), Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
            var section460 = new SteelSection(new SectionCHS(diameter, thickness, string.Empty), new SteelMaterial("S460", 210000, 440, 550), Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-10, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamElement beamElement235 = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section235);
            BeamElement beamElement275 = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section275);
            BeamElement beamElement355 = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section355);
            BeamElement beamElement460 = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section460);
            beamElement235.Results.AddRange(beamResults);
            beamElement275.Results.AddRange(beamResults);
            beamElement355.Results.AddRange(beamResults);
            beamElement460.Results.AddRange(beamResults);

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker235 = new EN1993p11Checker(beamElement235, options, standard);
            EN1993p11Checker checker237 = new EN1993p11Checker(beamElement275, options, standard);
            EN1993p11Checker checker355 = new EN1993p11Checker(beamElement355, options, standard);
            EN1993p11Checker checker460 = new EN1993p11Checker(beamElement460, options, standard);

            checker235.PerformCheck();
            checker237.PerformCheck();
            checker355.PerformCheck();
            checker460.PerformCheck();

            Assert.IsTrue(checker235.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class1);
            Assert.IsTrue(checker237.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class2);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);
            Assert.IsTrue(checker460.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class4);
        }

        #endregion

        #region Section C

        [TestMethod]
        public void Sap_SectionCTest()
        {
            /*
             Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  N, mm, C

 Frame :  11       X Mid:  2000.     Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1000.    Y Mid:  4000.     Shape:  C               Frame Type:  DCH-MRF            
 Loc   :  1000.    Z Mid:  0.        Class:  Class 1         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=15250.       eNy=0.            eNz=0.            
 A=15250.          Iyy=405677083.    iyy=163.101       Wel,yy=2028385.417  Weff,yy=2028385.41
 It=2302389.583    Izz=62887713.5    izz=64.217        Wel,zz=476984.833   Weff,zz=476984.833
 Iw=1.565E+12      Iyz=0.            h=400.            Wpl,yy=2334375.     Av,y=10000.       
 E=210000.         fy=355.           fu=510.           Wpl,zz=847812.5     Av,z=6300.        


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     1000.            100000.  100000000. -100000000.     -50000.      50000.  -7040843.1

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
     D/C Ratio:    0.471 = 0.018 + 0.121 + 0.332   <         0.95          OK
                        = (NEd/NRd) + (My,Ed/My,Rd) + (Mz,Ed/Mz,Rd)       (EC3 6.2.1(7))  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       6.131
     Major Braced          1.          1.       6.131
     Minor (z-z)           1.          1.      15.572
     Minor Braced          1.          1.      15.572
     LTB                   1.          1.      15.572

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial            100000.    5413750.    5413750.

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                     5413750.    5599800.  67792369.1  65611886.1          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49  840813189.        0.08       0.474          1.    5413750.
     MajorB(y-y)    c    0.49  840813189.        0.08       0.474          1.    5413750.
     Minor (z-z)    c    0.49 130342139.2       0.204       0.522       0.998 5403250.808
     MinorB(z-z)    c    0.49 130342139.2       0.204       0.522       0.998 5403250.808
     Torsional TF   c    0.49  65611886.1       0.287       0.563       0.956 5173634.263

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)   100000000.  100000000.  828703125.  828703125.  828703125.  828703125.
     Minor (z-z)  -100000000. -100000000. 300973437.5 300973437.5 300973437.5

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.473      -1.037

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.172       0.504          1.   1.565E+12   2.795E+10

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                 200.      0.        200.          0.          0.

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.994   7.743E-04        0.01       0.193       0.442          1.          1.

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.       1.151         1.5          1.       0.995       0.971       0.779

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.616       0.484       1.149
             */


            var section = new SteelSection(new SectionC(400, 15, 200, 25, 200, 25, ""), SteelMaterialEN1993Data.S355);

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity / (5413.75 * 1000.0), 1, 0.01);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity / (828.703 * 1e6), 1, 0.03);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity / (300.973 * 1e6), 1, 0.03);

            Assert.AreEqual(1.000 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.001);
            Assert.AreEqual(0.998 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.001);
            Assert.AreEqual(1.000 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.001);

            // Historical SAP kij were 0.895/0.616/0.484/0.901.
            // They cannot qualify EC3:2005 6.3.3 for this mono-symmetric C.
            // Retain the independent component comparisons above/below.
            Assert.IsTrue(double.IsNaN(EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx));
            Assert.IsTrue(double.IsNaN(EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy));
            Assert.IsTrue(double.IsNaN(EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx));
            Assert.IsTrue(double.IsNaN(EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy));
            Assert.IsTrue(double.IsNaN(EN1993P11Checker.EN1993p11BeamStationResults[0].WorkingRatio));
            Assert.IsTrue(EN1993P11Checker.GetErrorLog().Count == 1 && EN1993P11Checker.GetErrorLog()[0].Contains("6.3.3"));

            Assert.AreEqual(0.474 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.522 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.504 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiLTBuckling, 1, 0.1); //SAP calcola in modo diverso non documentato
        }

        #endregion

        #region Section T

        [TestMethod]
        public void Sap_SectionTTest()
        {
            /*
             Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  19       X Mid:  2.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  6.        Shape:  T               Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 1         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.024        eNy=0.            eNz=0.            
 A=0.024           Iyy=3.783E-04     iyy=0.126         Wel,yy=0.001        Weff,yy=0.001     
 It=1.497E-05      Izz=3.520E-05     izz=0.038         Wel,zz=3.520E-04    Weff,zz=3.520E-04 
 Iw=0.             Iyz=0.            h=0.4             Wpl,yy=0.003        Av,y=0.01         
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=6.400E-04    Av,z=0.013        


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     1.                  100.        100.       -100.        -50.         50.          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.564 = 0. + 0.074 + 0.49   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       7.965
     Major Braced          1.          1.       7.965
     Minor (z-z)           1.          1.      26.112
     Minor Braced          1.          1.      26.112
     LTB                   1.          1.      26.112

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.       8520.       8520.

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                        8520.      8812.8   45967.405     33458.8          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49   784140.07       0.104       0.482          1.       8520.
     MajorB(y-y)    c    0.49   784140.07       0.104       0.482          1.       8520.
     Minor (z-z)    c    0.49   72956.116       0.342       0.593       0.928    7904.341
     MinorB(z-z)    c    0.49   72956.116       0.342       0.593       0.928    7904.341
     Torsional TF   c    0.49     33458.8       0.505       0.702        0.84    7160.244

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.        923.        923.        923.     911.071
     Minor (z-z)        -100.       -100.       227.2       227.2       227.2

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.133      -1.023

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.216        0.53       0.987          0.   19704.284

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.142   0.106       0.036       0.031       0.075

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
             0.96       0.001       0.013       0.226       0.391          1.          1.

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.         1.5         1.5       0.999       0.994       0.887       0.805

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
               1.       0.895          1.       1.001        0.54       0.676       1.112
             */

            var section = new SteelSection(new SectionT(400, 200, 40, 50, ""), SteelMaterialEN1993Data.S355);

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(8520 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity, 1, 0.01);
            Assert.AreEqual(923.0 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.03);
            Assert.AreEqual(227.3 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.03);

            Assert.AreEqual(1.000 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.001);
            Assert.AreEqual(0.928 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.001);
            Assert.AreEqual(0.987 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.001);

            Assert.AreEqual(1.001 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.01);
            Assert.AreEqual(0.54 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.0206);
            Assert.AreEqual(0.676 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.019);
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.0205);

            Assert.AreEqual(0.482 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.593 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.530 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiLTBuckling, 1, 0.1);
        }

        [TestMethod]
        public void Sap_SectionTTest2()
        {
            /*
           Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  18       X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  6.        Shape:  T               Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.026        eNy=0.            eNz=0.            
 A=0.026           Iyy=4.075E-04     iyy=0.126         Wel,yy=0.002        Weff,yy=0.002     
 It=1.807E-05      Izz=3.599E-05     izz=0.037         Wel,zz=3.599E-04    Weff,zz=3.599E-04 
 Iw=0.             Iyz=0.            h=0.4             Wpl,yy=0.003        Av,y=0.01         
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=6.772E-04    Av,z=0.014        


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.752 = 0.216 + 0.115 + 0.421   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      95.388
     Major Braced          1.          1.      95.388
     Minor (z-z)           1.          1.     320.976
     Minor Braced          1.          1.     320.976
     LTB                   1.          1.     320.976

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.     9141.25     9141.25

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                      9141.25      9455.4   45393.097     515.276          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49    5865.575       1.248       1.536       0.411    3760.099
     MajorB(y-y)    c    0.49    5865.575       1.248       1.536       0.411    3760.099
     Minor (z-z)    c    0.49     518.027       4.201      10.303       0.051     463.754
     MinorB(z-z)    c    0.49     518.027       4.201      10.303       0.051     463.754
     Torsional TF   c    0.49     515.276       4.212      10.353        0.05     461.427

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.    1002.012    1002.012    1002.012     512.151
     Minor (z-z)        -100.       -100.     240.402     240.402     240.402

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.191      -0.978

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.917       1.193       0.511          0.    1190.881

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.147   0.108        0.04       0.027       0.081

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.956       0.043       0.007   5.897E-04       0.001        0.99       0.815

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.011         1.5         1.5       0.868       0.861        0.84       0.906

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.978       0.907        1.02       1.158       0.775       0.591       1.011
             */

            var section = new SteelSection(new SectionT(400, 200, 45, 50, string.Empty), SteelMaterialEN1993Data.S355);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(9141.25 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity, 1, 0.01);
            Assert.AreEqual(1002.012 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.01);
            Assert.AreEqual(240.402 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.01);

            Assert.AreEqual(0.411 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(0.051 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(0.511 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.001);

            Assert.AreEqual(1.158 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.01);
            Assert.AreEqual(0.775 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.01);
            Assert.AreEqual(0.591 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.01);
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.01);

            Assert.AreEqual(1.536 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling1, 1, 0.01);
            Assert.AreEqual(10.303 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiAxialBuckling2, 1, 0.01);
            Assert.AreEqual(1.193 / EN1993P11Checker.EN1993p11BeamStationResults[0].PhiLTBuckling, 1, 0.1);
        }

        #endregion

        #region Section L

        [TestMethod]
        public void Sap_SectionLTest()
        {
            /*
           Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C

 Frame :  13       X Mid:  2.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  5.        Shape:  L               Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 3         Rolled : No                      

 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                

 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      

 Aeff=0.039        eNy=0.            eNz=0.            
 A=0.039           Iyy=3.192E-04     iyy=0.091         Wel,yy=0.001        Weff,yy=0.001     
 It=5.806E-05      Izz=4.745E-04     izz=0.111         Wel,zz=0.002        Weff,zz=0.002     
 Iw=0.             Iyz=-2.114E-04    h=0.35            Wpl,yy=0.002        Av,y=0.028        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=0.004        Av,z=0.014        

 Iyz=-2.114E-04    Imax=6.221E-04    imax=0.127        Wel,zz,maj=0.003       
 Rot= 55. deg      Imin=1.717E-04    imin=0.067        Wel,zz,min=0.001       


 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     1.                  100.        100.       -100.        -50.         50.          0.

 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
     D/C Ratio:    0.367 = 0.007 + 0.231 + 0.129   <         0.95          OK
                        = (NEd/NRd) + (My,Ed/My,Rd) + (Mz,Ed/Mz,Rd)       (EC3 6.2.1(7))  

 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       7.897
     Major Braced          1.          1.       7.897
     Minor (z-z)           1.          1.      15.034
     Minor Braced          1.          1.      15.034
     LTB                   1.          1.       9.043

 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      13774.      13774.

                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       13774.    14247.36  139533.046  133082.101          1.

                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34 1289352.953       0.103       0.489          1.      13774.
     MajorB(y-y)    b    0.34 1289352.953       0.103       0.489          1.      13774.
     Minor (z-z)    b    0.34  355783.882       0.197       0.519          1.      13774.
     MinorB(z-z)    b    0.34  355783.882       0.197       0.519          1.      13774.
     Torsional TF   b    0.34  133082.101       0.322       0.572       0.956   13169.172

 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     433.721     433.721     433.721     433.721
     Minor (z-z)        -100.       -100.     772.216     772.216     772.216

                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 3       0.814        0.49      -1.015

                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.059       0.448          1.          0.  126415.596

        ***Warning: The equation to calculate Mcr is not applicable to Angle section***
        ***Please be aware of the assumptions made by the program                   ***

     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.261  -0.091       0.352      -0.019       0.031

     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.818   1.252E-05   7.464E-04       0.011       0.107          1.          1.

              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.         1.5         1.5          1.          1.       0.995       0.947

              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.895       0.895       0.895
             */

            var section = new SteelSection(new SectionL(350, 80, 350, 40, string.Empty), SteelMaterialEN1993Data.S355);

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            StationResultBeamForces[] resultLocationStations = new StationResultBeamForces[] { new StationResultBeamForces(loadCase, resultBeamForces[0], 0.0) };
            BeamResult[] beamResults = new BeamResult[1] { new BeamResult(resultLocationStations) };

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            BeamElement beamElement = new BeamElement(new Point3d(0, 0, 0), new Point3d(0 + L, 0, 0), section);
            beamElement.Results.AddRange(beamResults);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamElement, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(13774.0 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialTensionCapacity, 1, 0.01);
            Assert.AreEqual(772.216 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.03);
            Assert.AreEqual(433.721 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.03);
        }

        #endregion
    }
}
