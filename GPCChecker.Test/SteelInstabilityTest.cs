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

        /*[TestMethod]
        public void CircularSectionTestAxial1()
        {
            /*
             Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
             Units  :  N, mm, C
 
             Frame :  1        X Mid:  0.        Combo:  N_cmb           Design Type:  Beam                 
             Length:  1000.    Y Mid:  0.        Shape:  CHS100x10       Frame Type:  DCH-MRF            
             Loc   :  1000.    Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
             Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
             Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
             Consider Torsion? Yes               
 
             GammaM0=1.        GammaM1=1.        GammaM2=1.25      
             An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
             Aeff=2827.433     eNy=0.            eNz=0.            
             A=2827.433        Iyy=2898119.223   iyy=32.016        Wel,yy=57962.384    Weff,yy=57962.384 
             It=5796238.446    Izz=2898119.223   izz=32.016        Wel,zz=57962.384    Weff,zz=57962.384 
             Iw=0.             Iyz=0.            h=100.            Wpl,yy=81333.333    Av,y=1800.        
             E=210000.         fy=355.           fu=510.           Wpl,zz=81333.333    Av,z=1800.        
 
 
             STRESS CHECK FORCES & MOMENTS
                 Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
                 1000.           -100000.          0.          0.          0.          0.          0.
 
             PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.3.3(4)-6.62)
                 D/C Ratio:    0.112 = 0.112 + sqrt[(0.)^2 + (0.)^2  ] <         0.95          OK
                                    = NEd/(Chi_z NRk/GammaM1) + sqrt[(kzy (My,Ed+NEd eNy)/(Chi_LT My,Rk/GammaM1))^2
                                        + (kzz (Mz,Ed+NEd eNz)/(Mz,Rk/GammaM1))^2]       (EC3 6.3.3(4)-6.62)  
 
             BASIC FACTORS
                 Buckling Mode   K Factor    L Factor       Lcr/i
                 Major (y-y)           1.          1.      31.235
                 Major Braced          1.          1.      31.235
                 Minor (z-z)           1.          1.      31.235
                 Minor Braced          1.          1.      31.235
                 LTB                   1.          1.      31.235
 
             AXIAL FORCE DESIGN
                                      Ned       Nc,Rd       Nt,Rd
                                    Force    Capacity    Capacity
                 Axial           -100000. 1003738.853 1003738.853
 
                                   Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                              1003738.853  1038233.54 228369619.8  6006690.95          1.
 
                            Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
                 Major (y-y)    c    0.49  6006690.95       0.409       0.635       0.893  896003.838
                 MajorB(y-y)    c    0.49  6006690.95       0.409       0.635       0.893  896003.838
                 Minor (z-z)    c    0.49  6006690.95       0.409       0.635       0.893  896003.838
                 MinorB(z-z)    c    0.49  6006690.95       0.409       0.635       0.893  896003.838
                 Torsional TF   c    0.49  6006690.95       0.409       0.635       0.893  896003.838
 
             MOMENT DESIGN
                                      Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                                   Moment      Moment    Capacity    Capacity    Capacity    Capacity
                 Major (y-y)           0.          0. 28873333.33 28873333.33 28873333.33 28873333.33
                 Minor (z-z)           0.          0. 28873333.33 28873333.33 28873333.33
 
                                  Section      Flange         Web     Epsilon       Alpha         Psi
                 Compactness      Class 1     Class 1     Class 1       0.814       0.641      -0.801
 
                            Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
                 LTB            d    0.76       0.096       0.465          1.          0. 3159461645.
 
                 Factors      kw       C1          C2          C3
                               1.   1.884          0.       0.941
                               za      zs          zg          zz          zj
                              50.      0.         50.          0.          0.
 
                 Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
                           0.          0.          0.          0.          0.       0.998       0.998
 
                          nPL          wy          wz         Cyy         Cyz         Czy         Czz
                          0.1       1.403       1.403       1.054       1.063       1.063       1.054
 
                          Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
                           1.          1.          1.       0.964       0.573       0.573       0.964   
             */
        /*
            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section circularSect = new SectionCHS(100, 10, steel);

            double N = -1e5;
            double V1 = 0;
            double V2 = 0;
            double M1 = 0;
            double M2 = 0;
            double T = 0;

            double L = 1000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 1;
            double? psiz = 1;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;

            EuroCodeBeamChecker checker = new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, annex);

            Assert.AreEqual(checker.NRd, 1003738.853, 0.01);
            Assert.AreEqual(checker.MRdy / 28873333.33, 1, 0.03);
            Assert.AreEqual(checker.MRdz / 28873333.33, 1, 0.03);

            double Trd = checker.TRd;
            double Vrdy = checker.VRdy;
            double Vrdz = checker.VRdz;

            //Assert.AreEqual(checker.WRAxial, 0.112);

            double WRBending1 = checker.WRBending1;
            double WRBending2 = checker.WRBending2;
            double WRShear1 = checker.WRShear1;
            double WRShear2 = checker.WRShear2;
            double WRTorsion = checker.WRTorsion;

            checker.CheckBuckling(L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz);
            double WRBuckling1 = checker.WRBuckling1;
            double WRBuckling2 = checker.WRBuckling2;
            double WRCombined = checker.WRCombined;

            Assert.AreEqual(0.893, checker.Chiy, 0.001);
            Assert.AreEqual(0.893, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);
            Assert.AreEqual(0.964, checker.Kyy, 0.005);
            Assert.AreEqual(0.573, checker.Kyz, 0.005);
            Assert.AreEqual(0.573, checker.Kzy, 0.005);
            Assert.AreEqual(0.964, checker.Kzz, 0.005);
            Assert.AreEqual(6006690.95, checker.Ncry, 0.1);
            Assert.AreEqual(6006690.95, checker.Ncrz, 0.1);
            Assert.AreEqual(0.635, checker.Phiy, 0.001);
            Assert.AreEqual(0.635, checker.Phiz, 0.01);
            //Assert.AreEqual(0.465, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }*/

    }
}