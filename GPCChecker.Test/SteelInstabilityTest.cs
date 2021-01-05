using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EuroCode;
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
        public void HSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  4        X Mid:  -2.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  3.        Shape:  H Symmetric     Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     6.                 -100.        100.       -100.      -8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.779 = 0.066 + 0.136 + 0.577   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      36.161
     Major Braced          1.          1.      36.161
     Minor (z-z)           1.          1.     104.828
     Minor Braced          1.          1.     104.828
     LTB                   1.          1.     104.828
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4238.7      4238.7
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4238.7    4384.368    4675.221    4675.221          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34   18925.036       0.473       0.658       0.896    3797.338
     MajorB(y-y)    b    0.34   18925.036       0.473       0.658       0.896    3797.338
     Minor (z-z)    c    0.49    2252.001       1.372       1.728        0.36    1525.154
     MinorB(z-z)    c    0.49    2252.001       1.372       1.728        0.36    1525.154
     Torsional TF   c    0.49    4675.221       0.952       1.138       0.568    2408.229
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      658.33      658.33      658.33     380.769
     Minor (z-z)        -100.       -100.     171.135     171.135     171.135
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.532      -0.953
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.935       1.117       0.578   1.449E-06     752.777
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.998       0.088       0.362       0.103       0.135       0.999       0.971
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.024       1.128         1.5       0.982        0.81       0.952       0.923
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.972       0.898          1.       0.995       0.802       0.519       0.988
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 250, 15, 250, 15, true, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 6000;
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
            Assert.AreEqual(checker.MRdy / (658.33 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (171.135 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.896, checker.Chiy, 0.001);
            Assert.AreEqual(0.36, checker.Chiz, 0.001);
            Assert.AreEqual(0.578 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.995, checker.Kyy, 0.005);
            Assert.AreEqual(0.802, checker.Kyz, 0.005);
            Assert.AreEqual(0.519, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.898, checker.Cmz, 0.005);
            Assert.AreEqual(0.971, checker.Muz, 0.005);
        }

        [TestMethod]
        public void RHSSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  2        X Mid:  -2.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  1.        Shape:  RHS             Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
     6.                 -100.        100.       -100.      -8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.341 = 0.032 + 0.1 + 0.208   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       44.76
     Major Braced          1.          1.       44.76
     Minor (z-z)           1.          1.      69.582
     Minor Braced          1.          1.      69.582
     LTB                   1.          1.      69.582
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      5225.6      5225.6
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       5225.6    5405.184  678888.292     6301.37          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49   15228.324       0.586       0.766       0.794    4147.949
     MajorB(y-y)    c    0.49   15228.324       0.586       0.766       0.794    4147.949
     Minor (z-z)    c    0.49     6301.37       0.911       1.089       0.593    3100.354
     MinorB(z-z)    c    0.49     6301.37       0.911       1.089       0.593    3100.354
     Torsional TF   c    0.49     6301.37       0.911       1.089       0.593    3100.354
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     615.258     615.258     615.258     615.258
     Minor (z-z)        -100.       -100.     435.088     435.088     435.088
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.523      -0.962
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.211       0.527       0.991          0.   13779.453
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.193   2.124E-04       0.003       0.005       0.018       0.999       0.993
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.019        1.31        1.12       1.001       0.992       0.995       0.998
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.936       0.896          1.       0.941       0.508       0.611       0.906
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionRHS(400, 200, 8, 8, 15, 15, false, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 6000;
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
            Assert.AreEqual(checker.MRdy / (615.258 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (435.088 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.794, checker.Chiy, 0.001);
            Assert.AreEqual(0.593, checker.Chiz, 0.001);
            Assert.AreEqual(0.991 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.941, checker.Kyy, 0.005);
            Assert.AreEqual(0.508, checker.Kyz, 0.005);
            Assert.AreEqual(0.611, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.896, checker.Cmz, 0.005);
            Assert.AreEqual(0.993, checker.Muz, 0.005);
        }

        [TestMethod]
        public void CHSSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  1        X Mid:  -2.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  0.        Shape:  CHS             Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 2         Rolled : No                      
 
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
     6.                 -100.        100.       -100.      -8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
     D/C Ratio:    0.285 = 0.023 + sqrt[(0.185)^2 + (0.185)^2  ] <         0.95          OK
                        = (NEd/NRd) + sqrt[(My,Ed/My,Rd)^2 + (Mz,Ed/Mz,Rd)^2]       (EC3 6.2.1(7))  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.        43.5
     Major Braced          1.          1.        43.5
     Minor (z-z)           1.          1.        43.5
     Minor Braced          1.          1.        43.5
     LTB                   1.          1.        43.5
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.    4349.535    4349.535
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                     4349.535    4499.012  989601.686   13420.098          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    c    0.49   13420.098       0.569       0.753       0.803    3494.531
     MajorB(y-y)    c    0.49   13420.098       0.569       0.753       0.803    3494.531
     Minor (z-z)    c    0.49   13420.098       0.569       0.753       0.803    3494.531
     MinorB(z-z)    c    0.49   13420.098       0.569       0.753       0.803    3494.531
     Torsional TF   c    0.49   13420.098       0.569       0.753       0.803    3494.531
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     540.073     540.073     540.073     540.073
     Minor (z-z)        -100.       -100.     540.073     540.073     540.073
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 2     Class 2     Class 2       0.814       0.535      -0.954
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            d    0.76       0.135       0.484          1.          0.   29712.828
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
               0.          0.          0.          0.          0.       0.999       0.999
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.023       1.305       1.305       1.008       1.007       1.007       1.008
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.894       0.537       0.537       0.894
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionCHS(400, 10, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 6000;
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
            Assert.AreEqual(checker.MRdy / (540.073 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (540.073 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.803, checker.Chiy, 0.001);
            Assert.AreEqual(0.803, checker.Chiz, 0.001);
            Assert.AreEqual(1 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.894, checker.Kyy, 0.005);
            Assert.AreEqual(0.537, checker.Kyz, 0.005);
            Assert.AreEqual(0.537, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(0.999, checker.Muz, 0.005);
        }

        [TestMethod]
        public void HSectionTest2()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  4        X Mid:  -2.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  3.        Shape:  H Symmetric     Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     6.                 -100.        100.       -100.      -8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.781 = 0.066 + 0.138 + 0.577   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           2.          1.      72.322
     Major Braced          2.          1.      72.322
     Minor (z-z)           1.          1.     104.828
     Minor Braced          1.          1.     104.828
     LTB                   1.          1.     104.828
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4238.7      4238.7
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4238.7    4384.368    4675.221    4675.221          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34    4731.259       0.947       1.075       0.631    2675.634
     MajorB(y-y)    b    0.34    4731.259       0.947       1.075       0.631    2675.634
     Minor (z-z)    c    0.49    2252.001       1.372       1.728        0.36    1525.154
     MinorB(z-z)    c    0.49    2252.001       1.372       1.728        0.36    1525.154
     Torsional TF   c    0.49    4675.221       0.952       1.138       0.568    2408.229
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      658.33      658.33      658.33     380.769
     Minor (z-z)        -100.       -100.     171.135     171.135     171.135
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.532      -0.953
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.935       1.117       0.578   1.449E-06     752.777
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.998       0.088       0.362       0.103       0.135       0.992       0.971
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
            0.024       1.128         1.5       0.982        0.81       0.952       0.923
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.972       0.898          1.       1.003       0.796       0.527       0.988
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 250, 15, 250, 15, true, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 6000;
            double betay = 2.0;
            double betaz = 1.0;
            double betaLT = 1.0;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(sect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4238.7 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdy / (658.33 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (171.135 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(0.631, checker.Chiy, 0.001);
            Assert.AreEqual(0.36, checker.Chiz, 0.001);
            Assert.AreEqual(0.578 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(1.003, checker.Kyy, 0.005);
            Assert.AreEqual(0.796, checker.Kyz, 0.005);
            Assert.AreEqual(0.527, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.898, checker.Cmz, 0.005);
            Assert.AreEqual(0.971, checker.Muz, 0.005);
        }

        [TestMethod]
        public void AsymmetricHSectionTest1()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  3        X Mid:  -2.5      Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  2.        Shape:  H               Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 3         Rolled : No                      
 
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
     6.                 -100.        100.       -100.      -8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.952 = 0.045 + 0.298 + 0.609   >         0.95  Overstress
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      39.557
     Major Braced          1.          1.      39.557
     Minor (z-z)           1.          1.       89.08
     Minor Braced          1.          1.       89.08
     LTB                   1.          1.       89.08
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4927.4      4927.4
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4927.4    5096.736    6516.109    3124.852          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34   18384.609       0.518       0.688       0.876    4317.826
     MajorB(y-y)    b    0.34   18384.609       0.518       0.688       0.876    4317.826
     Minor (z-z)    c    0.49    3625.308       1.166       1.416        0.45     2219.29
     MinorB(z-z)    c    0.49    3625.308       1.166       1.416        0.45     2219.29
     Torsional TF   c    0.49    3124.852       1.256       1.547       0.408    2010.585
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      418.54      418.54      418.54     324.336
     Minor (z-z)        -100.       -100.     149.027     149.027     149.027
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 1       0.814       0.531      -0.959
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.618       0.793       0.775   1.317E-06      1097.4
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.271  -0.076       0.347      -0.054      -0.022
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.995       0.023       0.166       0.077        0.14       0.999       0.985
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
             0.02       1.384         1.5       0.985       0.917       0.958       0.928
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.976       0.897          1.       0.981       0.922       0.967       0.908
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 200, 10, 300, 25, true, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 6000;
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
            Assert.AreEqual(checker.MRdy / (418.54 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdz / (149.027 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(checker.McrLateralTorsional / (643.01*1e6) , 1, 0.1); //643 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.876, checker.Chiy, 0.001);
            Assert.AreEqual(0.45, checker.Chiz, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.981, checker.Kyy, 0.005);
            Assert.AreEqual(0.922, checker.Kyz, 0.005);
            Assert.AreEqual(0.967, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.897, checker.Cmz, 0.005);
            Assert.AreEqual(0.985, checker.Muz, 0.005);
        }

        [TestMethod]
        public void AsymmetricHSectionTest2()
        {
            /*
             * Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  10       X Mid:  -10.5     Combo:  N+M1+M2 SLU COMPDesign Type:  Beam                 
 Length:  6.       Y Mid:  2.        Shape:  H               Frame Type:  DCH-MRF            
 Loc   :  6.       Z Mid:  0.        Class:  Class 3         Rolled : No                      
 
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
 
 
 STRESS CHECK FORCES & MOMENTS
     Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
     6.                 -100.       -100.       -100.       8.333       8.333          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.944 = 0.045 + 0.289 + 0.609   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.      39.557
     Major Braced          1.          1.      39.557
     Minor (z-z)           1.          1.       89.08
     Minor Braced          1.          1.       89.08
     LTB                   1.          1.       89.08
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial              -100.      4927.4      4927.4
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4927.4    5096.736    6516.109    3124.852          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34   18384.609       0.518       0.688       0.876    4317.826
     MajorB(y-y)    b    0.34   18384.609       0.518       0.688       0.876    4317.826
     Minor (z-z)    c    0.49    3625.308       1.166       1.416        0.45     2219.29
     MinorB(z-z)    c    0.49    3625.308       1.166       1.416        0.45     2219.29
     Torsional TF   c    0.49    3124.852       1.256       1.547       0.408    2010.585
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)        -100.       -100.      418.54      418.54      418.54     334.195
     Minor (z-z)        -100.       -100.     149.027     149.027     149.027
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 1       0.814       0.531      -0.959
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.578       0.759       0.798   1.317E-06    1253.766
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.271  -0.076       0.347      -0.054       0.022
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.995        0.02       0.141        0.07       0.127       0.999       0.985
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
             0.02       1.384         1.5       0.986        0.93       0.961       0.935
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.976       0.897          1.       0.981       0.922       0.967       0.908
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 200, 10, 300, 25, true, steel);

            double N = -100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = -100 * 1e6;
            double T = 0;

            double L = 6000;
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
            Assert.AreEqual(checker.MRdy / (418.54 * 1e6), 1, 0.03); //to checked
            Assert.AreEqual(checker.MRdz / (149.027 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(checker.McrLateralTorsional / (1881 * 1e6), 1, 0.1); //1881 kNm coming from LTBeam -> Sap is Wrong!
            Assert.AreEqual(0.876, checker.Chiy, 0.001);
            Assert.AreEqual(0.45, checker.Chiz, 0.001);
            //Assert.AreEqual(0.775 / checker.ChiLT, 1, 0.01);

            Assert.AreEqual(0.981, checker.Kyy, 0.005);
            Assert.AreEqual(0.922, checker.Kyz, 0.005);
            Assert.AreEqual(0.967, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.897, checker.Cmz, 0.005);
            Assert.AreEqual(0.985, checker.Muz, 0.005);
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

            double C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.0, C1Value, 0.01);

            //end moments - psi = 1
            M1 = -1;
            M2 = -1;
            M3 = -1;
            M4 = -1;
            M5 = -1;
            MMax = -1;

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(2.45, C1Value, 0.01);

            //end moments - psi = -1
            M1 = 1;
            M2 = 0.5;
            M3 = 0;
            M4 = -0.5;
            M5 = -1;
            MMax = -1;

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
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

            C1Value = EuroCodeBeamChecker.C1(k, kw, MMax, M1, M2, M3, M4, M5);
            Assert.AreEqual(1.78, C1Value, 0.01);
        }
    }
}