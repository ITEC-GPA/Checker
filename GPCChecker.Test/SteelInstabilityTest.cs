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
    }
}