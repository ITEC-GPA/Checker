using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checkers.Steel.EuroCode;
using System.Windows;

namespace SteelTests
{
    [TestClass]
    public class SteelInstabilityChecks
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Nothing
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Nothing
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Nothing
        }

        [TestMethod]
        public void CHSSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  1        X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  0.        Shape:  CHS             Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 2         Rolled : No                      
 
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
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
     D/C Ratio:    0.285 = 0.023 + sqrt[(0.185)^2 + (0.185)^2  ] <         0.95          OK
                        = (NEd/NRd) + sqrt[(My,Ed/My,Rd)^2 + (Mz,Ed/Mz,Rd)^2]       (EC3 6.2.1(7))  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.         87.
     Major Braced          1.          1.         87.
     Minor (z-z)           1.          1.         87.
     Minor Braced          1.          1.         87.
     LTB                   1.          1.         87.
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.    4349.535    4349.535
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                     4349.535    4499.012  989601.686    3355.024          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49    3355.024       1.139       1.378       0.464    2018.668
     MajorB(y-y)    c    0.49    3355.024       1.139       1.378       0.464    2018.668
     Minor (z-z)    c    0.49    3355.024       1.139       1.378       0.464    2018.668
     MinorB(z-z)    c    0.49    3355.024       1.139       1.378       0.464    2018.668
     Torsional TF   c    0.49    3355.024       1.139       1.378       0.464    2018.668
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     540.073     540.073     540.073     540.073
     Minor (z-z)        -100.       -100.     540.073     540.073     540.073
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 2     Class 2     Class 2       0.814       0.535      -0.954
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.191       0.515          1.          0.   14856.414
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
               0.          0.          0.          0.          0.       0.984       0.984
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.023       1.305       1.305       0.997       0.987       0.987       0.997
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.897       0.897          1.       0.912       0.553       0.553       0.912
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionCHS(400, 10, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4349.535 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (540.073 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (540.073 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.464, checker.Chiy, 0.001);
            Assert.AreEqual(0.464, checker.Chiz, 0.001);
            Assert.AreEqual(1 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.912, checker.Kyy, 0.005);
            Assert.AreEqual(0.553, checker.Kyz, 0.005);
            Assert.AreEqual(0.553, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.897, checker.Cmz, 0.005);
            Assert.AreEqual(0.984, checker.Muz, 0.005);
        }

        [TestMethod]
        public void RHSSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  2        X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  1.        Shape:  RHS             Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.405 = 0.083 + 0.11 + 0.212   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      89.519
     Major Braced          1.          1.      89.519
     Minor (z-z)           1.          1.     139.164
     Minor Braced          1.          1.     139.164
     LTB                   1.          1.     139.164
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      5225.6      5225.6
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       5225.6    5405.184  678888.292    1575.342          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49    3807.081       1.172       1.424       0.448    2338.746
     MajorB(y-y)    c    0.49    3807.081       1.172       1.424       0.448    2338.746
     Minor (z-z)    c    0.49    1575.342       1.821       2.556        0.23    1201.623
     MinorB(z-z)    c    0.49    1575.342       1.821       2.556        0.23    1201.623
     Torsional TF   c    0.49    1575.342       1.821       2.556        0.23    1201.623
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     615.258     615.258     615.258     615.258
     Minor (z-z)        -100.       -100.     435.088     435.088     435.088
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.523      -0.962
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.299       0.582       0.924          0.    6889.727
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.193   4.556E-04       0.003   5.697E-04       0.002       0.985        0.95
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.019        1.31        1.12       0.979       0.955       0.949       0.991
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.937       0.899          1.       0.969       0.549       0.626       0.921
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionRHS(400, 200, 8, 8, 15, 15, false, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (5225.6 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (615.258 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (435.088 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.448, checker.Chiy, 0.001);
            Assert.AreEqual(0.23, checker.Chiz, 0.001);
            Assert.AreEqual(0.924 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.969, checker.Kyy, 0.005);
            Assert.AreEqual(0.549, checker.Kyz, 0.005);
            Assert.AreEqual(0.626, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.899, checker.Cmz, 0.005);
            Assert.AreEqual(0.95, checker.Muz, 0.005);
        }

        [TestMethod]
        public void HSectionSymmetricTest1()
        {
            /*
         Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  4        X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  3.        Shape:  H Symmetric     Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                
 
 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
 Aeff=0.012        eNy=0.            eNz=0.            
 A=0.012           Iyy=3.287E-04     iyy=0.166         Wel,yy=0.002        Weff,yy=0.002     
 It=0.             Izz=3.912E-05     izz=0.057         Wel,zz=3.129E-04    Weff,zz=3.129E-04 
 Iw=1.449E-06      Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.008        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=4.821E-04    Av,z=0.005        
 
 
 DESIGN MESSAGES
     Error: Section overstressed
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.61)
     D/C Ratio:    1.186 = 0.037 + 0.57 + 0.579   >         0.95  Overstress
                        = NEd/(Chi_y NRk/GammaM1) + kyy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kyz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)     (EC3 6.3.3(4)-6.61)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      72.322
     Major Braced          1.          1.      72.322
     Minor (z-z)           1.          1.     209.656
     Minor Braced          1.          1.     209.656
     LTB                   1.          1.     209.656
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4238.7      4238.7
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4238.7    4384.368    2643.581    2643.581          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    4731.259       0.947       1.075       0.631    2675.634
     MajorB(y-y)    b    0.34    4731.259       0.947       1.075       0.631    2675.634
     Minor (z-z)    c    0.49        563.       2.744       4.888       0.112     474.531
     MinorB(z-z)    c    0.49        563.       2.744       4.888       0.112     474.531
     Torsional TF   c    0.49    2643.581       1.266       1.563       0.403    1709.781
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      658.33      658.33      658.33     201.797
     Minor (z-z)        -100.       -100.     171.135     171.135     171.135
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.532      -0.953
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       1.525       1.988       0.307   1.449E-06     283.029
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.998        0.44       0.251        0.02       0.027       0.992       0.839
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.024       1.128         1.5       0.908       0.764       0.839       0.904
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.972       0.906       1.059       1.149        0.99       0.548       1.022
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 250, 15, 250, 15, true, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4238.7 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (658.33 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (171.135 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.631, checker.Chiy, 0.001);
            Assert.AreEqual(0.112, checker.Chiz, 0.001);
            //Assert.AreEqual(0.403, checker.ChiT, 0.001); --> NcrTF < Ncz ??
            Assert.AreEqual(0.307 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.149, checker.Kyy, 0.005);
            Assert.AreEqual(0.99, checker.Kyz, 0.005);
            Assert.AreEqual(0.548, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.906, checker.Cmz, 0.005);
            Assert.AreEqual(0.839, checker.Muz, 0.005);
        }

        [TestMethod]
        public void AsymmetricHSectionTest1()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 200, 10, 300, 25, true, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4927.4 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (418.54 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdNz / (149.027 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (643.01*1e6) , 1, 0.1); //643 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.575, checker.Chiy, 0.001);
            Assert.AreEqual(0.15, checker.Chiz, 0.001);
            Assert.AreEqual(0.145, checker.ChiT, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.005, checker.Kyy, 0.005);
            Assert.AreEqual(1.004, checker.Kyz, 0.005);
            Assert.AreEqual(0.918, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.005);
            Assert.AreEqual(0.905, checker.Muz, 0.005);
        }

        [TestMethod]
        public void AsymmetricHSectionTest2()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 200, 10, 300, 25, true, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = -100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4927.4 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (418.54 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdNz / (149.027 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.575, checker.Chiy, 0.001);
            Assert.AreEqual(0.15, checker.Chiz, 0.001);
            Assert.AreEqual(0.145, checker.ChiT, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.005, checker.Kyy, 0.01);
            Assert.AreEqual(1.004, checker.Kyz, 0.01);
            Assert.AreEqual(0.918, checker.Kzy, 0.01);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.01);
            Assert.AreEqual(0.905, checker.Muz, 0.01);
        }

        [TestMethod]
        public void AsymmetricHSectionTest3()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  22       X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  -1.       Shape:  H Asym2         Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                
 
 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
 Aeff=0.018        eNy=0.            eNz=0.            
 A=0.018           Iyy=2.306E-04     iyy=0.115         Wel,yy=0.001        Weff,yy=0.001     
 It=3.400E-06      Izz=6.361E-05     izz=0.06          Wel,zz=4.240E-04    Weff,zz=4.240E-04 
 Iw=0.             Iyz=0.            h=0.3             Wpl,yy=0.002        Av,y=0.011        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=7.422E-04    Av,z=0.008        
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.657 = 0.131 + 0.159 + 0.367   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.     104.528
     Major Braced          1.          1.     104.528
     Minor (z-z)           1.          1.     199.044
     Minor Braced          1.          1.     199.044
     LTB                   1.          1.     199.044
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      6212.5      6212.5
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       6212.5       6426.    12332.62     896.992          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    3319.653       1.368       1.634       0.396    2457.105
     MajorB(y-y)    b    0.34    3319.653       1.368       1.634       0.396    2457.105
     Minor (z-z)    c    0.49     915.503       2.605       4.482       0.123     764.182
     MinorB(z-z)    c    0.49     915.503       2.605       4.482       0.123     764.182
     Torsional TF   c    0.49     896.992       2.632       4.559       0.121     750.201
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     637.891     637.891     637.891     369.059
     Minor (z-z)        -100.       -100.     263.477     263.477     263.477
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.523      -0.968
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.935       1.117       0.579          0.     729.854
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.121   0.077       0.043       0.013       0.064
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.985       0.058       0.061       0.005       0.011       0.982       0.903
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.016       1.398         1.5       0.924       0.904       0.902       0.945
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.978       0.902       1.002       1.073       0.683       0.586       0.967
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(300, 25, 300, 25, 150, 25, true, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (6212.5 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (637.891 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdNz / (263.477 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.396, checker.Chiy, 0.001);
            Assert.AreEqual(0.123, checker.Chiz, 0.001);
            Assert.AreEqual(0.121, checker.ChiT, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.073, checker.Kyy, 0.005);
            Assert.AreEqual(0.683, checker.Kyz, 0.005);
            Assert.AreEqual(0.586, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.005);
            Assert.AreEqual(0.903, checker.Muz, 0.005);
        }

        [TestMethod]
        public void AsymmetricHSectionTest4()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  22       X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  -1.       Shape:  H Asym2         Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                
 
 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
 Aeff=0.018        eNy=0.            eNz=0.            
 A=0.018           Iyy=2.306E-04     iyy=0.115         Wel,yy=0.001        Weff,yy=0.001     
 It=3.400E-06      Izz=6.361E-05     izz=0.06          Wel,zz=4.240E-04    Weff,zz=4.240E-04 
 Iw=0.             Iyz=0.            h=0.3             Wpl,yy=0.002        Av,y=0.011        
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=7.422E-04    Av,z=0.008        
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.657 = 0.131 + 0.159 + 0.367   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.     104.528
     Major Braced          1.          1.     104.528
     Minor (z-z)           1.          1.     199.044
     Minor Braced          1.          1.     199.044
     LTB                   1.          1.     199.044
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      6212.5      6212.5
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       6212.5       6426.    12332.62     896.992          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    3319.653       1.368       1.634       0.396    2457.105
     MajorB(y-y)    b    0.34    3319.653       1.368       1.634       0.396    2457.105
     Minor (z-z)    c    0.49     915.503       2.605       4.482       0.123     764.182
     MinorB(z-z)    c    0.49     915.503       2.605       4.482       0.123     764.182
     Torsional TF   c    0.49     896.992       2.632       4.559       0.121     750.201
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     637.891     637.891     637.891     369.059
     Minor (z-z)        -100.       -100.     263.477     263.477     263.477
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.523      -0.968
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.935       1.117       0.579          0.     729.854
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.121   0.077       0.043       0.013       0.064
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.985       0.058       0.061       0.005       0.011       0.982       0.903
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.016       1.398         1.5       0.924       0.904       0.902       0.945
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.978       0.902       1.002       1.073       0.683       0.586       0.967
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(300, 25, 300, 25, 150, 25, true, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = -100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (6212.5 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (637.891 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdNz / (263.477 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.396, checker.Chiy, 0.001);
            Assert.AreEqual(0.123, checker.Chiz, 0.001);
            Assert.AreEqual(0.121, checker.ChiT, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.081, checker.Kyy, 0.011);
            Assert.AreEqual(0.69, checker.Kyz, 0.01);
            Assert.AreEqual(0.586, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.005);
            Assert.AreEqual(0.903, checker.Muz, 0.005);
        }

        [TestMethod]
        public void CSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  9        X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  4.        Shape:  C               Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
 Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
 Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
 Consider Torsion? No                
 
 GammaM0=1.        GammaM1=1.        GammaM2=1.25      
 An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
 Aeff=0.015        eNy=0.            eNz=0.            
 A=0.015           Iyy=4.057E-04     iyy=0.163         Wel,yy=0.002        Weff,yy=0.002     
 It=2.302E-06      Izz=6.289E-05     izz=0.064         Wel,zz=4.770E-04    Weff,zz=4.770E-04 
 Iw=1.565E-06      Iyz=0.            h=0.4             Wpl,yy=0.002        Av,y=0.01         
 E=210000000.      fy=355000.        fu=510000.        Wpl,zz=8.478E-04    Av,z=0.006        
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     12.                -100.        100.       -100.      -4.167       4.167      -0.587
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.61)
     D/C Ratio:    0.631 = 0.033 + 0.331 + 0.266   <         0.95          OK
                        = NEd/(Chi_y NRk/GammaM1) + kyy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kyz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)     (EC3 6.3.3(4)-6.61)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      73.574
     Major Braced          1.          1.      73.574
     Minor (z-z)           1.          1.     186.867
     Minor Braced          1.          1.     186.867
     LTB                   1.          1.     186.867
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.     5413.75     5413.75
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                      5413.75      5599.8    4121.174    2944.748          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49     5838.98       0.963        1.15       0.562    3041.167
     MajorB(y-y)    c    0.49     5838.98       0.963        1.15       0.562    3041.167
     Minor (z-z)    c    0.49     905.154       2.446       4.041       0.138      745.98
     MinorB(z-z)    c    0.49     905.154       2.446       4.041       0.138      745.98
     Torsional TF   c    0.49    2944.748       1.356       1.702       0.366    1981.703
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     828.703     828.703     828.703     311.293
     Minor (z-z)        -100.       -100.     300.973     300.973     300.973
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.527      -0.963
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       1.201       1.602       0.376   1.565E-06     574.195
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.994         0.1       0.152       0.009       0.021       0.992       0.903
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.018       1.151         1.5        0.96       0.859       0.895        0.94
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.972       0.902       1.009       1.032       0.802       0.529       0.974
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionC sect = new SectionC(400, 15, 200, 25, 200, 25, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (5413.75 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (828.703 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdNz / (300.973 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.562, checker.Chiy, 0.001);
            Assert.AreEqual(0.138, checker.Chiz, 0.001);
            //Assert.AreEqual(0.366, checker.ChiT, 0.001); --> NcrTF in SAP >> Ncrz .... strano..essendo flesso - torsionale al limite dovrebbero coincidere!
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.032, checker.Kyy, 0.005);
            Assert.AreEqual(0.802, checker.Kyz, 0.005);
            Assert.AreEqual(0.529, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.005);
            Assert.AreEqual(0.903, checker.Muz, 0.005);
        }

        [TestMethod]
        public void LSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  12       X Mid:  -5.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  12.      Y Mid:  5.        Shape:  L               Frame Type:  DCH-MRF            
 Loc   :  12.      Z Mid:  0.        Class:  Class 3         Rolled : No                      
 
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
     12.                -100.        100.       -100.      -4.167       4.167          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.391 = 0.047 + 0.228 + 0.117   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       94.77
     Major Braced          1.          1.       94.77
     Minor (z-z)           1.          1.     180.411
     Minor Braced          1.          1.     180.411
     LTB                   1.          1.      108.51
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      13774.      13774.
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       13774.    14247.36  132728.012    8697.006          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34     8953.84        1.24       1.446       0.457    6291.193
     MajorB(y-y)    b    0.34     8953.84        1.24       1.446       0.457    6291.193
     Minor (z-z)    b    0.34    2470.721       2.361       3.655       0.155    2137.285
     MinorB(z-z)    b    0.34    2470.721       2.361       3.655       0.155    2137.285
     Torsional TF   b    0.34    8697.006       1.258       1.472       0.447    6162.756
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     433.721     433.721     433.721     421.033
     Minor (z-z)        -100.       -100.     772.216     772.216     772.216
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 3       0.814        0.51      -0.985
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.237       0.542       0.971          0.    7690.577
 
        ***Warning: The equation to calculate Mcr is not applicable to Angle section***
        ***Please be aware of the assumptions made by the program                   ***
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.261  -0.091       0.352      -0.019       0.031
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.818   2.119E-04       0.002   1.300E-04       0.001       0.994       0.966
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.007         1.5         1.5       0.978       0.976       0.971       0.982
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.981       0.897          1.       0.986        0.93       0.958       0.903
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionL sect = new SectionL(350, 80, 350, 40, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            /*double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;*/

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (13774.0 * 1000.0), 1, 0.01);
            //Assert.AreEqual(checker.MRdNy / (828.703 * 1e6), 1, 0.03); //to checked
            //Assert.AreEqual(checker.MRdNz / (300.973 * 1e6), 1, 0.03);
            /*
            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.562, checker.Chiy, 0.001);
            Assert.AreEqual(0.138, checker.Chiz, 0.001);
            Assert.AreEqual(0.366, checker.ChiT, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.032, checker.Kyy, 0.005);
            Assert.AreEqual(0.802, checker.Kyz, 0.005);
            Assert.AreEqual(0.529, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.902, checker.Cmz, 0.005);
            Assert.AreEqual(0.903, checker.Muz, 0.005);*/
        }

        [TestMethod]
        public void TSectionTest1()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionT sect = new SectionT(400, 200, 45, 50, steel, string.Empty);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 12000;
            double betay = 1.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (9141.25 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (1002.012 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (240.402 * 1e6), 1, 0.03);
            
            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            //Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.411, checker.Chiy, 0.001);
            Assert.AreEqual(0.051, checker.Chiz, 0.001);
            Assert.AreEqual(0.05, checker.ChiT, 0.001);
            //Assert.AreEqual(0.511 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.158 / checker.Kyy, 1, 0.03);
            Assert.AreEqual(0.775, checker.Kyz, 0.005);
            Assert.AreEqual(0.591, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.907, checker.Cmz, 0.005);
            Assert.AreEqual(0.815, checker.Muz, 0.005);
        }

        [TestMethod]
        public void C1McrTest()
        {
            //single force simply supported beam 
            double F = 1;
            double L = 1000;
            double R = F / 2.0;
            
            double M1 = 0;
            double M2 = R * L / 4.0;
            double M3 = R * L / 2.0;
            double M4 = M2;
            double M5 = 0;
            double MMax = M3;

            double k = 1;
            double kw = 1;

            double C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.25, C1Value, 0.01);

            //distribuited force simply supported beam 
            double q = 1;
            R = q * L / 2.0;

            M1 = 0;
            M2 = R * L / 4.0 - q * Math.Pow(L / 4.0, 2.0) / 2;
            M3 = R * L / 2.0 - q * Math.Pow(L / 2.0, 2.0) / 2;
            M4 = M2;
            M5 = 0;
            MMax = M3;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.12, C1Value, 0.01);

            //single force restrained beam 
            F = 1;
            L = 1000;
            R = F / 2.0;

            M1 = -1.0 / 8.0 * F * L;
            M2 = -1.0 / 8.0 * F * L + R * L / 4.0;
            M3 = -1.0 / 8.0 * F * L + R * L / 2.0;
            M4 = M2;
            M5 = 0;
            MMax = M3;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.38, C1Value, 0.01);

            //ditribuited force restrained beam 
            q = 1;
            L = 1000;
            R = q * L / 2.0;

            M1 = -1.0 / 12.0 * q * Math.Pow(L, 2.0);
            M2 = -1.0 / 12.0 * q * Math.Pow(L, 2.0) - q * Math.Pow(L / 4.0, 2.0) / 2.0 + R * L / 4.0;
            M3 = -1.0 / 12.0 * q * Math.Pow(L, 2.0) - q * Math.Pow(L / 2.0, 2.0) / 2.0 + R * L / 2.0;
            M4 = M2;
            M5 = 0;
            MMax = M1;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(2.40, C1Value, 0.01);

            //end moments - psi = 1
            M1 = 1;
            M2 = 1;
            M3 = 1;
            M4 = 1;
            M5 = 1;
            MMax = 1;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.0, C1Value, 0.01);

            //end moments - psi = 1
            M1 = -1;
            M2 = -1;
            M3 = -1;
            M4 = -1;
            M5 = -1;
            MMax = -1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.0, C1Value, 0.01);

            //end moments - psi = -1
            M1 = -1;
            M2 = -0.5;
            M3 = 0;
            M4 = 0.5;
            M5 = 1;
            MMax = 1;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(2.45, C1Value, 0.01);

            //end moments - psi = -1
            M1 = 1;
            M2 = 0.5;
            M3 = 0;
            M4 = -0.5;
            M5 = -1;
            MMax = -1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(2.45, C1Value, 0.01);

            //end moments - psi = 0
            M1 = -1;
            M2 = -0.75;
            M3 = -0.5;
            M4 = -0.25;
            M5 = 0;
            MMax = -1;

            k = 1;
            kw = 1;

            C1Value = EuroCodeBeamChecker.getC1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.78, C1Value, 0.01);
        }
    }
}