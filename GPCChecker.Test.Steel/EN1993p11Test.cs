using GPC.Geometry;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model;

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

            SteelSectionRHS sectionRHS = new SteelSectionRHS(400, 200, 8, 8, 15, 15, SteelMaterial.S355, string.Empty);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(100 * 1e3, 50 * 1e3, 50 * 1e3, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0.5;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 1, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionRHS, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded, 
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds, 
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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



            SteelSectionRHS section = new SteelSectionRHS(400, 200, 8, 8, 15, 15, SteelMaterial.S355, "");

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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
            double L = 1000;
            double h = 500;
            double b = 300;
            double t = 12.5;

            SteelSectionRHS section355 = new SteelSectionRHS(h, b, t, t, t, t, new SteelMaterial("S355", 210000, 355, 510), string.Empty);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1500e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-2859e3 * 0.95, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-6029e3 * 0.95, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-6029e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 0, 1e6, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamCheckerAttributes beamCheckerAttributes355 = new BeamCheckerAttributes(section355, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamCheckerAttributes355, options, standard);

            checker355.PerformCheck();

            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class1);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[1].AxialCompressionClass == EN1993p11Checker.SectionClass.Class2);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[2].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[3].AxialCompressionClass == EN1993p11Checker.SectionClass.Class4);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[4].AxialCompressionClass == EN1993p11Checker.SectionClass.Class4);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[4].AxialCompressionClass == EN1993p11Checker.SectionClass.Class1);
        }

        [TestMethod]
        public void SectionRHSClasstification2()
        {
            //Cordova cap. 2 pag. 75
            //in hot formed h = H - 2 * t - (0.5+0.5) * t
            //in favour of safety h = H - 2 t, for this reason a value of 0.95 * Ned has been used to take this tolerance
            double h = 350;
            double b = 250;
            double t = 10;

            SteelSectionRHS section355 = new SteelSectionRHS(h, b, t, t, t, t, SteelMaterial.S355, string.Empty);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1300e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-1700e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(-2160e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global),
                new ResultBeamForces(0, 0, 0, 0, 0, 1e6, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, 5000) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamCheckerAttributes beamCheckerAttributes355 = new BeamCheckerAttributes(section355, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamCheckerAttributes355, options, standard);

            checker355.PerformCheck();

            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class1);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[1].AxialCompressionClass == EN1993p11Checker.SectionClass.Class2);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[2].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[3].AxialCompressionClass == EN1993p11Checker.SectionClass.Class1);
            Assert.IsTrue(checker355.EN1993p11BeamStationResults[4].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);            
        }

        #endregion

        #region Section H

        [TestMethod]
        public void SectionHBuckling1()
        {
            // Cordova Costruzioni in acciaio pagina 149
            double L = 6500;

            double h = 290.0;
            double width = 300.0;
            double flangeThickness = 14.0;
            double webThickness = 8.5;
            double r = 27.0;

            SteelSectionH sectionH = new SteelSectionH(h, webThickness,width, flangeThickness, width, flangeThickness, SteelMaterial.S355, 
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1320 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0.5;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expNbRd1 = 1450000.0 * 1.05; // lui usa gammaM1 = 1.05
            double expNbRd2 = 1588000.0 * 1.05;
            //double expWR = 0.910/1.05;

            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity - expNbRd1) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity * 100 < 0.5);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - expNbRd2) /
                EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity * 100 < 0.5);
            //Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].WorkingRatio - expWR) < 0.01);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S355,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400 * 1000, 0, 0, 0, 32.36 * 1e6, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.OneSideRestrained_OneSideHinged, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                1, 1, 1, 0.85, 1, 0.85, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S355,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.4 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                null, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expMbRd = 74.7 * 1.05; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1
            double expMcRd = 164.6 * 1.05;

            UnitsSystem units = new UnitsSystem(GPC.Utilities.Units.UnitsConvert.LengthUnits.m, GPC.Utilities.Units.UnitsConvert.ForceUnits.kN,
                GPC.Utilities.Units.UnitsConvert.MassUnits.kg, GPC.Utilities.Units.UnitsConvert.PressureUnits.kPa, GPC.Utilities.Units.UnitsConvert.TemperatureUnits.C);

            Assert.IsTrue((Math.Abs(Units.ConverMomentFromDefault(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, units) - expMcRd) / expMcRd) < 0.01);
            Assert.IsTrue((Math.Abs(Units.ConverMomentFromDefault(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity, units) - expMbRd) / expMbRd * 100) < 1);            
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S355,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 66.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.SingleForce,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 0.5, 1, 1, 1, 
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.TopSection);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expMbRd = 101.8 * 1.05; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1

            UnitsSystem units = new UnitsSystem(GPC.Utilities.Units.UnitsConvert.LengthUnits.m, GPC.Utilities.Units.UnitsConvert.ForceUnits.kN,
                GPC.Utilities.Units.UnitsConvert.MassUnits.kg, GPC.Utilities.Units.UnitsConvert.PressureUnits.kPa, GPC.Utilities.Units.UnitsConvert.TemperatureUnits.C);

            Assert.IsTrue((Math.Abs(Units.ConverMomentFromDefault(EN1993P11Checker.EN1993p11BeamStationResults[0].LateralTosionalBucklingCapacity, units) - expMbRd) / expMbRd * 100) < 1);
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

            SteelSectionC sectionC = new SteelSectionC(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S355,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.40 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionC, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S235,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380*1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 0, 1, 1, 0.25, 0.25, 1, 1, 1, 0.1, 0.1, 1, 1, 1, 1);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expInteractionWR = 1.00 / 1.05; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].BucklingInteraction1Axis - expInteractionWR) * 100) < 1.5);
        }

        [TestMethod]
        public void SectionHInteraction2()
        {
            // Cordova Costruzioni in acciaio pagina 232
            // NOTA: i risultati dei tassi di lavoro delle interazioni che si usano per confronto sono presi dal Cordova.
            // Lui usa un metodo semplificato, per questo i valori che ottengo col metodo esatto sono distanti quel 2/3 %
            double L = 5000;

            double h = 300.0;
            double width = 150.0;
            double flangeThickness = 10.7;
            double webThickness = 7.1;
            double r = 15.0;

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S235,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-190.0 * 1000, 0, 0, 0, 78.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamCheckerAttributes, options, standard);

            Checker.PerformCheck();

            double expInteraction1WR = 1.00; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1
            double expInteraction2WR = 0.92; // lui usa gammaM1 = 1.05 => UNIEN1993-1-1

            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction1Axis - expInteraction1WR) * 100) < 3.0);
            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction2Axis - expInteraction2WR) * 100) < 2.5);
        }

        [TestMethod]
        public void SectionHInteraction3()
        {
            // Cordova Costruzioni in acciaio pagina 237
            // NOTA: in questo esempio utilizza il metodo esatto
            double L = 4000;

            double h = 250.0;
            double width = 260;
            double flangeThickness = 12.5;
            double webThickness = 7.5;
            double r = 24.0;

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S235,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400.0 * 1000, 0, 0, 0, 71.0 * 1000000, 30 * 1000000, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 0;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamCheckerAttributes, options, standard);

            Checker.PerformCheck();

            double expInteraction1WR = 0.77; 
            double expInteraction2WR = 0.73; 

            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction1Axis - expInteraction1WR) * 100) < 2.5);
            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction2Axis - expInteraction2WR) * 100) < 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                0, 0, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            double psiy = 1;

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 90000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, SteelMaterial.S275,
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 100000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH section = new SteelSectionH(400, 12, 200, 10, 300, 25, SteelMaterial.S355, string.Empty);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionH section = new SteelSectionH(300, 25, 300, 25, 150, 25, SteelMaterial.S355, string.Empty);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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
            SteelSectionH section275 = new SteelSectionH(270, 8.0, 280, 13.0, 280, 13.0, SteelMaterial.S420, string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, 24);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1000e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, 5000) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamCheckerAttributes beamCheckerAttributes355 = new BeamCheckerAttributes(section275, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamCheckerAttributes355, options, standard);

            checker355.PerformCheck();

            Assert.IsTrue(checker355.EN1993p11BeamStationResults[0].AxialCompressionClass == EN1993p11Checker.SectionClass.Class3);
        }

        [TestMethod]
        public void SectionHClasstification2()
        {
            SteelSectionH section275 = new SteelSectionH(550,11.1,210, 17.2, 210, 17.2, SteelMaterial.S275, string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, 24);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(-1000e3, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, 5000) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamCheckerAttributes beamCheckerAttributes355 = new BeamCheckerAttributes(section275, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker355 = new EN1993p11Checker(beamCheckerAttributes355, options, standard);

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

            SteelSectionCHS circularSect = new SteelSectionCHS(diameter, thickness, SteelMaterial.S355, string.Empty);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { 
                new ResultBeamForces(100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(circularSect, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiz, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionCHS section = new SteelSectionCHS(diameter, thickness, SteelMaterial.S355,
                string.Empty, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-100 * 1000, 50 * 1000, 50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds,
                psix, psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standard = new StandardEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamCheckerAttributes, options, standard);

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

            SteelSectionCHS section235 = new SteelSectionCHS(diameter, thickness, new SteelMaterial("S235", 210000, 235, 360), string.Empty, Section.FormedTypes.HotFinished);
            SteelSectionCHS section275 = new SteelSectionCHS(diameter, thickness, new SteelMaterial("S275", 210000, 275, 430), string.Empty, Section.FormedTypes.HotFinished);
            SteelSectionCHS section355 = new SteelSectionCHS(diameter, thickness, new SteelMaterial("S355", 210000, 355, 510), string.Empty, Section.FormedTypes.HotFinished);
            SteelSectionCHS section460 = new SteelSectionCHS(diameter, thickness, new SteelMaterial("S460", 210000, 440, 550), string.Empty, Section.FormedTypes.HotFinished);

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { 
                new ResultBeamForces(-10, 0, 0, 0, 0, 0, CoordinateSystem.Global),
            };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1);

            BeamCheckerAttributes beamCheckerAttributes235 = new BeamCheckerAttributes(section235, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            BeamCheckerAttributes beamCheckerAttributes275 = new BeamCheckerAttributes(section275, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            BeamCheckerAttributes beamCheckerAttributes355 = new BeamCheckerAttributes(section355, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });
            BeamCheckerAttributes beamCheckerAttributes460 = new BeamCheckerAttributes(section460, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker checker235 = new EN1993p11Checker(beamCheckerAttributes235, options, standard);
            EN1993p11Checker checker237 = new EN1993p11Checker(beamCheckerAttributes275, options, standard);
            EN1993p11Checker checker355 = new EN1993p11Checker(beamCheckerAttributes355, options, standard);
            EN1993p11Checker checker460 = new EN1993p11Checker(beamCheckerAttributes460, options, standard);

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


            SteelSectionC section = new SteelSectionC(400, 15, 200, 25, 200, 25, SteelMaterial.S355, "");

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity / (5413.75 * 1000.0), 1, 0.01);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity / (828.703 * 1e6), 1, 0.03);
            Assert.AreEqual(EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity / (300.973 * 1e6), 1, 0.03);

            Assert.AreEqual(1.000 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling1, 1, 0.001);
            Assert.AreEqual(0.998 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiAxialBuckling2, 1, 0.001);
            Assert.AreEqual(1.000 / EN1993P11Checker.EN1993p11BeamStationResults[0].ChiLTBuckling, 1, 0.001);

            Assert.AreEqual(0.895 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxx, 1, 0.01);
            Assert.AreEqual(0.616 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kxy, 1, 0.0206);
            Assert.AreEqual(0.484 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyx, 1, 0.019);
            Assert.AreEqual(0.901 / EN1993P11Checker.EN1993p11BeamStationResults[0].Kyy, 1, 0.0205);

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

            SteelSectionT section = new SteelSectionT(400, 200, 40, 50, SteelMaterial.S355, "");

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionT section = new SteelSectionT(400, 200, 45, 50, SteelMaterial.S355, string.Empty);

            double L = 12000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

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

            SteelSectionL section = new SteelSectionL(350, 80, 350, 40, SteelMaterial.S355, string.Empty);

            double L = 1000;
            double? psiX = 0.5;
            double? psiY = 0.5;

            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] {
                new ResultBeamForces(100 * 1000, 50 * 1000, -50 * 1000, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);

            ResultLocationStation[] resultLocationStations = new ResultLocationStation[] { new ResultLocationStation(resultBeamForces, 0.0, L) };
            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultLocationStations) });

            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.EndsRestrained,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.EndsRestrained,
                psiX, psiY, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            Assert.AreEqual(13774.0 * 1000.0 / EN1993P11Checker.EN1993p11BeamStationResults[0].AxialTensionCapacity, 1, 0.01);
            Assert.AreEqual(772.216 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment1Capacity, 1, 0.03);
            Assert.AreEqual(433.721 * 1e6 / EN1993P11Checker.EN1993p11BeamStationResults[0].BendingMoment2Capacity, 1, 0.03);
        }

        #endregion
    }
}
