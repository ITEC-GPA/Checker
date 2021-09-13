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
        [TestMethod]
        public void RHSSectionTest1()
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

            SteelSectionRHS sectionRHS = new SteelSectionRHS(400, 200, 8, 8, 15, 15, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850), string.Empty);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(100 * 1e3, 50 * 1e3, 50 * 1e3, 0, -100 * 1e6, 100 * 1e6, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 0.5;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionRHS, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded, 
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds, 
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1);
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
        public void SectionHBuckling1()
        {
            // Cordova Costruzioni in acciaio pagina 149
            double L = 6500;

            double h = 290.0;
            double width = 300.0;
            double flangeThickness = 14.0;
            double webThickness = 8.5;
            double r = 27.0;

            SteelSectionH sectionH = new SteelSectionH(h, webThickness,width, flangeThickness, width, flangeThickness, new SteelMaterial("S355", 210000, 0.3, 275, 430, 7850), 
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1320 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 0.5;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-1000 * 1000, 0, 0, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400 * 1000, 0, 0, 0, 32.36 * 1e6, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.OneSideRestrained_OneSideHinged, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1, 1, 0.85, 1, 0.85, 1, 1, 1, 1, 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S355", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.4 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, null, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S355", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 66.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 1;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.SingleForce,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 0.5, 1, 1, 1, 
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

            SteelSectionC sectionC = new SteelSectionC(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S355", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 0, 0, 64.40 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 1;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionC, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S235", 210000, 0.3, 235, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380*1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 0;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 0, 1, 1, 0.25, 0.25, 1, 1, 1, 0.1, 0.1, 1, 1, 1, 1);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S235", 210000, 0.3, 235, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-190.0 * 1000, 0, 0, 0, 78.0 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 0;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S235", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-400.0 * 1000, 0, 0, 0, 71.0 * 1000000, 30 * 1000000, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 0;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamCheckerAttributes, options, standard);

            Checker.PerformCheck();

            double expInteraction1WR = 0.77; 
            double expInteraction2WR = 0.73; 

            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction1Axis - expInteraction1WR) * 100) < 2.5);
            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].BucklingInteraction2Axis - expInteraction2WR) * 100) < 1);
        }

        [TestMethod]
        public void SectionCHSInteraction1()
        {
            double L = 1000;

            double diameter = 400;
            double thickness = 10;

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double psix = 0.5;
            double psiy = 0.5;

            SteelSectionCHS section = new SteelSectionCHS(diameter, thickness, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850),
                string.Empty, Section.FormedTypes.HotFinished);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(N, V1, V2, T, M1, M2, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(section, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.EndsRestrained, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psix, psiy, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
                EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardUNIEN1993p11 standard = new StandardUNIEN1993p11();
            EN1993p11Checker Checker = new EN1993p11Checker(beamCheckerAttributes, options, standard);

            Checker.PerformCheck();

            double expAxialBuckling1 = 483123.525 * 1000;
            double expAxialBuckling2 = 483123.525 * 1000;

            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].AxialBuckling1Capacity - expAxialBuckling1) / expAxialBuckling1 * 100) < 1);
            Assert.IsTrue((Math.Abs(Checker.EN1993p11BeamStationResults[0].AxialBuckling2Capacity - expAxialBuckling2) / expAxialBuckling2 * 100) < 1);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 1;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.NotDirectlyLoaded,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 0, 0, 1, 1, 1, 1, 1, 1, 0.5, 1, 0.5, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(-380 * 1000, 0, 0, 0, 120 * 1000000, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            double psiy = 1;

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, psiy, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);
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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 90000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

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

            SteelSectionH sectionH = new SteelSectionH(h, webThickness, width, flangeThickness, width, flangeThickness, new SteelMaterial("S275", 210000, 0.3, 275, 430, 7850),
                string.Empty, Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished, r);
            ResultBeamForces[] resultBeamForces = new ResultBeamForces[] { new ResultBeamForces(0, 0, 100000, 0, 0, 0, CoordinateSystem.Global) };
            LoadCase loadCase = new LoadCase("SelfWeight", LoadCase.LoadCaseTypes.SelfWeight);
            ResultStation[] resultStation = new ResultStation[] { new ResultStation(1, 0, L) };

            BeamCheckerAttributes beamCheckerAttributes = new BeamCheckerAttributes(sectionH, new BeamResult[] { new BeamResult(loadCase, resultBeamForces, resultStation, CoordinateSystem.Global) });
            EN1993p11Checker.EN1993p11Options options = new EN1993p11Checker.EN1993p11Options(EN1993p11Checker.EN1993p11Options.LoadConditions.Constant,
                EN1993p11Checker.EN1993p11Options.SupportConditions.HingesAtEnds, EN1993p11Checker.EN1993p11Options.LateralSupportConditions.HingesAtEnds,
                EN1993p11Checker.EN1993p11Options.LateralWarpingConditions.HingesAtEnds, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, EN1993p11Checker.EN1993p11Options.LoadApplicationPoints.ShearCenter);

            StandardEN1993p11 standardEN1993P11 = new StandardEN1993p11();
            EN1993p11Checker EN1993P11Checker = new EN1993p11Checker(beamCheckerAttributes, options, standardEN1993P11);

            EN1993P11Checker.PerformCheck();

            double expShear2Cap = 247.68 * 1e3;
            double expShear2WR = 0.40;

            Assert.IsTrue((Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2Capacity - expShear2Cap) / expShear2Cap * 100) < 0.5);
            Assert.IsTrue(Math.Abs(EN1993P11Checker.EN1993p11BeamStationResults[0].Shear2WorkingRatio - expShear2WR) < 0.5);
        }
    }
}
