using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EuroCode;
using System.Windows;

namespace SteelTests
{
    [TestClass]
    public class SteelResistanceChecks
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section circularSect = new SectionCHS(400, 10, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0.5;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;
            double? psiz = 0.5;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd / (4349.535 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (540.073 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (540.073 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);
 
            Assert.AreEqual(1, checker.Chiy, 0.001);
            Assert.AreEqual(1, checker.Chiz, 0.001);
            Assert.AreEqual(1, checker.ChiLT, 0.001);

            Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.537, checker.Kyz, 0.005);
            Assert.AreEqual(0.537, checker.Kzy, 0.005);
            Assert.AreEqual(0.895, checker.Kzz, 0.005);

            Assert.AreEqual(483123.525 * 1000, checker.Ncry, 0.1);
            Assert.AreEqual(483123.525 * 1000, checker.Ncrz, 0.1);

            Assert.AreEqual(0.479, checker.Phiy, 0.001);
            Assert.AreEqual(0.479, checker.Phiz, 0.01);
            //Assert.AreEqual(0.479, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }

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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionRHS(400, 200, 8, 8, 15, 15, false, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = 50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(1, checker.Chiy, 0.001);
            Assert.AreEqual(1, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);

            Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.496, checker.Kyz, 0.005);
            Assert.AreEqual(0.584, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(548219.667 * 1000 / checker.Ncry, 1, 0.01);
            Assert.AreEqual(226849.304 * 1000 / checker.Ncrz, 1, 0.01);

            Assert.AreEqual(0.48, checker.Phiy, 0.001);
            Assert.AreEqual(0.5, checker.Phiz, 0.01);
            //Assert.AreEqual(0.479, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }

        [TestMethod]
        public void HSectionTest1()
        {
            /*
             Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  4        X Mid:  0.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  3.        Shape:  H Symmetric     Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
     1.                  100.        100.       -100.        -50.         50.          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.806 = 0. + 0.075 + 0.731   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       6.027
     Major Braced          1.          1.       6.027
     Minor (z-z)           1.          1.      17.471
     Minor Braced          1.          1.      17.471
     LTB                   1.          1.      17.471
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      4238.7      4238.7
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4238.7    4384.368   99485.127   99485.127          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34  681301.309       0.079       0.483          1.      4238.7
     MajorB(y-y)    b    0.34  681301.309       0.079       0.483          1.      4238.7
     Minor (z-z)    c    0.49   81072.028       0.229       0.533       0.985    4176.867
     MinorB(z-z)    c    0.49   81072.028       0.229       0.533       0.985    4176.867
     Torsional TF   c    0.49   99485.127       0.206       0.523       0.997    4224.835
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      658.33      658.33      658.33      658.33
     Minor (z-z)        -100.       -100.     171.135     171.135     171.135
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.468      -1.047
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.178        0.51          1.   1.449E-06   20835.089
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                  0.2      0.         0.2          0.          0.
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.998       0.002       0.014       0.438        0.57          1.          1.
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.       1.128         1.5          1.       0.993       0.944       0.715
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.623       0.493       1.252
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 250, 15, 250, 15, true, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(1, checker.Chiy, 0.001);
            Assert.AreEqual(0.985, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);

            Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.623, checker.Kyz, 0.005);
            Assert.AreEqual(0.493, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(681301.309 * 1000 / checker.Ncry, 1, 0.01);
            Assert.AreEqual(81072.028 * 1000 / checker.Ncrz, 1, 0.01);

            Assert.AreEqual(0.483, checker.Phiy, 0.001);
            Assert.AreEqual(0.533, checker.Phiz, 0.01);
            //Assert.AreEqual(0.479, checker.PhiLT, 0.001); //SAP calcola Mcr in modo non ben documentato
        }

        [TestMethod]
        public void HSectionTest2()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  7        X Mid:  2.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  2.        Shape:  H               Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 3         Rolled : No                      
 
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
     1.                  100.        100.       -100.        -50.         50.          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7), 6.2.9.2(1))
     D/C Ratio:     0.93 = 0.02 + 0.239 + 0.671   <         0.95          OK
                        = (NEd/NRd) + (My,Ed/My,Rd) + (Mz,Ed/Mz,Rd)       (EC3 6.2.1(7), 6.2.9.2(1))  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       6.593
     Major Braced          1.          1.       6.593
     Minor (z-z)           1.          1.      14.847
     Minor Braced          1.          1.      14.847
     LTB                   1.          1.      14.847
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      4927.4      4927.4
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       4927.4    5096.736   86106.214   71230.316          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34  661845.933       0.086       0.484          1.      4927.4
     MajorB(y-y)    b    0.34  661845.933       0.086       0.484          1.      4927.4
     Minor (z-z)    c    0.49  130511.085       0.194       0.517          1.      4927.4
     MinorB(z-z)    c    0.49  130511.085       0.194       0.517          1.      4927.4
     Torsional TF   c    0.49   71230.316       0.263        0.55       0.968    4769.566
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.      418.54      418.54      418.54      418.54
     Minor (z-z)        -100.       -100.     149.027     149.027     149.027
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 3     Class 3     Class 1       0.814       0.469      -1.041
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.135       0.493          1.   1.317E-06   22923.568
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.271  -0.076       0.347      -0.054      -0.022
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.995   8.559E-04       0.009       0.272       0.497          1.          1.
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.       1.384         1.5          1.       0.995       0.895       0.751
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.895       0.895       0.895
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(400, 12, 200, 10, 300, 25, true, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(checker.NRd / (4927.4 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (418.54 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (149.027 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(1.0, checker.Chiy, 0.001);
            Assert.AreEqual(1.0, checker.Chiz, 0.001);
            //Assert.AreEqual(1.0, checker.ChiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT

            //NB: in SAP wy > 1 e wz > 1 anche se sezione è classificata come classe 3!!!!
            /*Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.895, checker.Kyz, 0.005);
            Assert.AreEqual(0.895, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(0.484, checker.Phiy, 0.001);
            Assert.AreEqual(0.517, checker.Phiz, 0.01);
            Assert.AreEqual(0.493, checker.PhiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT
            */
        }

        [TestMethod]
        public void HSectionTest3()
        {
            /*
            Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
 Units  :  KN, m, C
 
 Frame :  23       X Mid:  2.        Combo:  N+M1+M2 SLU TRACDesign Type:  Beam                 
 Length:  1.       Y Mid:  -1.       Shape:  H Asym2         Frame Type:  DCH-MRF            
 Loc   :  1.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
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
     1.                  100.        100.       -100.        -50.         50.          0.
 
 PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
     D/C Ratio:    0.545 = 0. + 0.09 + 0.454   <         0.95          OK
                        = NEd/(Chi_z NRk/GammaM1) + kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1)
                            + kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1)       (EC3 6.3.3(4)-6.62)  
 
 BASIC FACTORS
     Buckling Mode   K Factor    L Factor       Lcr/i
     Major (y-y)           1.          1.       8.711
     Major Braced          1.          1.       8.711
     Minor (z-z)           1.          1.      16.587
     Minor Braced          1.          1.      16.587
     LTB                   1.          1.      16.587
 
 AXIAL FORCE DESIGN
                          Ned       Nc,Rd       Nt,Rd
                        Force    Capacity    Capacity
     Axial               100.      6212.5      6212.5
 
                       Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                       6212.5       6426.   54968.312   47810.622          1.
 
                Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
     Major (y-y)    b    0.34  478030.048       0.114       0.492          1.      6212.5
     MajorB(y-y)    b    0.34  478030.048       0.114       0.492          1.      6212.5
     Minor (z-z)    c    0.49   131832.47       0.217       0.528       0.991     6158.43
     MinorB(z-z)    c    0.49   131832.47       0.217       0.528       0.991     6158.43
     Torsional TF   c    0.49   47810.622        0.36       0.604       0.918    5703.301
 
 MOMENT DESIGN
                          Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                       Moment      Moment    Capacity    Capacity    Capacity    Capacity
     Major (y-y)         100.        100.     637.891     637.891     637.891     637.891
     Minor (z-z)        -100.       -100.     263.477     263.477     263.477
 
                      Section      Flange         Web     Epsilon       Alpha         Psi
     Compactness      Class 1     Class 1     Class 1       0.814       0.477      -1.032
 
                Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
     LTB            c    0.49       0.154         0.5          1.          0.   26997.251
 
     Factors      kw       C1          C2          C3
                   1.   1.322          0.       0.728
                   za      zs          zg          zz          zj
                0.121   0.077       0.043       0.013       0.064
 
     Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
            0.985   9.073E-04       0.011       0.252       0.505          1.          1.
 
              nPL          wy          wz         Cyy         Cyz         Czy         Czz
               0.       1.398         1.5          1.       0.995         0.9       0.748
 
              Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
            0.895       0.895          1.       0.895       0.559       0.576       1.197
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section sect = new SectionH(300, 25, 300, 25, 150, 25, true, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(checker.NRd / (6212.5 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (637.891 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (263.477 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(1.0, checker.Chiy, 0.001);
            Assert.AreEqual(0.991, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT

            Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.559, checker.Kyz, 0.005);
            Assert.AreEqual(0.576, checker.Kzy, 0.02);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(0.492, checker.Phiy, 0.001);
            Assert.AreEqual(0.528, checker.Phiz, 0.01);
            //Assert.AreEqual(0.5, checker.PhiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT
        }

        [TestMethod]
        public void CSectionTest()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionC sect = new SectionC(400, 15, 200, 25, 200, 25, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(checker.NRd / (5413.75 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (828.703 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (300.973 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(1.0, checker.Chiy, 0.001);
            Assert.AreEqual(0.998, checker.Chiz, 0.001);
            Assert.AreEqual(0.956, checker.ChiT, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);

            Assert.AreEqual(0.895, checker.Kyy, 0.005);
            Assert.AreEqual(0.616, checker.Kyz, 0.005);
            Assert.AreEqual(0.484, checker.Kzy, 0.005);
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(0.474, checker.Phiy, 0.001);
            Assert.AreEqual(0.522, checker.Phiz, 0.01);
            Assert.AreEqual(0.504, checker.PhiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT
        }

        [TestMethod]
        public void TSectionTest()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionT sect = new SectionT(400, 200, 40, 50, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(checker.NRd / (8520 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdNy / (923.0 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdNz / (227.3 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);

            Assert.AreEqual(1.0, checker.Chiy, 0.001);
            Assert.AreEqual(0.928, checker.Chiz, 0.001);
            Assert.AreEqual(0.84, checker.ChiT, 0.01);
            //Assert.AreEqual(0.987, checker.ChiLT, 0.001);

            //Assert.AreEqual(1.001, checker.Kyy, 0.005);// --> difference start in Cmy = 1 instead of 0.9 (seems that lambda0 not reported is greater than limit of 0.2*(C1)^0.5*(...) see table A1 EN 1993-1-1
            Assert.AreEqual(0.54, checker.Kyz, 0.005);
            //Assert.AreEqual(0.676, checker.Kzy, 0.005);// --> difference start in Cmy
            //Assert.AreEqual(0.901, checker.Kzz, 0.005); //in SAP 2000 there is a WRONG version of czz -> kzz(Cmz, muz, Ncrz, Czz) -> check Cmz, muz, Ncrz
            Assert.AreEqual(0.895, checker.Cmz, 0.005);
            Assert.AreEqual(1.0, checker.Muz, 0.005);

            Assert.AreEqual(0.482, checker.Phiy, 0.001);
            Assert.AreEqual(0.593, checker.Phiz, 0.01);
            //Assert.AreEqual(0.530, checker.PhiLT, 0.001); //SAP calcola Mcr in modo diverso non documentato -> lamba -> phi -> ChiLT
        }

        [TestMethod]
        public void LSectionTest()
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

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            SectionL sect = new SectionL(350, 80, 350, 40, steel);

            double N = 100 * 1000;
            double V1 = 50 * 1000;
            double V2 = -50 * 1000;
            double M1 = -100 * 1e6;
            double M2 = 100 * 1e6;
            double T = 0;

            double L = 1000;
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

            Assert.AreEqual(checker.NRd / (13774.0 * 1000.0), 1, 0.01);
            //Assert.AreEqual(checker.MRdNy / (772.216 * 1e6), 1, 0.03); //how calulate Wpl of L section?! -> use elastic wel
            //Assert.AreEqual(checker.MRdNz / (433.721 * 1e6), 1, 0.03); //how calulate Wpl of L section?! -> use elastic Wel
        }
    }
}