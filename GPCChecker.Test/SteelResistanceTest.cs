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
        public void CircularSectionTest1()
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
            Assert.AreEqual(checker.MRdy / (540.073 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (540.073 * 1e6), 1, 0.03);

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);
 
            Assert.AreEqual(1, checker.Chiy, 0.001);
            Assert.AreEqual(1, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);

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
            Assert.AreEqual(checker.MRdy / (615.258 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (435.088 * 1e6), 1, 0.03);

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

            Assert.AreEqual(checker.NRd / (4238.7 * 1000.0), 1, 0.01);
            Assert.AreEqual(checker.MRdy / (658.33 * 1e6), 1, 0.03);
            Assert.AreEqual(checker.MRdz / (171.135 * 1e6), 1, 0.03);

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
            //Assert.AreEqual(0.479, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }
    }
}