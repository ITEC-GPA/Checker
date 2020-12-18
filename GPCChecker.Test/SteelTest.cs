using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EuroCode;
using System.Windows;

namespace SteelTests
{
    [TestClass]
    public class SteelTest
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

            EuroCodeBeamChecker checker = null; // new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz, annex);

            Assert.AreEqual(checker.NRd, 1003738.853, 0.01);
            Assert.AreEqual(checker.MRdy / 28873333.33, 1, 0.03);
            Assert.AreEqual(checker.MRdz / 28873333.33, 1, 0.03);
            double Trd = checker.TRd;
            double Vrdy = checker.VRdy;
            double Vrdz = checker.VRdz;
            double WrAxial = checker.WRAxial;
            double WRBending1 = checker.WRBending1;
            double WRBending2 = checker.WRBending2;
            double WRBuckling1 = checker.WRBuckling1;
            double WRBuckling2 = checker.WRBuckling2;
            double WRCombined = checker.WRCombined;
            double WRShear1 = checker.WRShear1;
            double WRShear2 = checker.WRShear2;
            double WRTorsion = checker.WRTorsion;
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
        }

        [TestMethod]
        public void CircularSectionTestAxial2()
        {
            /*
             Eurocode 3-2005 STEEL SECTION CHECK    (Flexural Details for Combo and Station)
             Units  :  KN, m, C
 
             Frame :  1        X Mid:  0.        Combo:  N_M1_M2         Design Type:  Beam                 
             Length:  1.       Y Mid:  0.        Shape:  CHS100x10       Frame Type:  DCH-MRF            
             Loc   :  0.       Z Mid:  0.        Class:  Class 1         Rolled : No                      
 
             Country=CEN Default                 Combination=Eq. 6.10                  Reliability=Class 2                 
             Interaction=Method 1 (Annex A)      MultiResponse=Envelopes               P-Delta Done? No                    
             Consider Torsion? Yes               
 
             GammaM0=1.        GammaM1=1.        GammaM2=1.25      
             An/Ag=1.          RLLF=1.           PLLF=0.75         D/C Lim=0.95      
 
             Aeff=0.003        eNy=0.            eNz=0.            
             A=0.003           Iyy=2.898E-06     iyy=0.032         Wel,yy=5.796E-05    Weff,yy=5.796E-05 
             It=5.796E-06      Izz=2.898E-06     izz=0.032         Wel,zz=5.796E-05    Weff,zz=5.796E-05 
             Iw=0.             Iyz=0.            h=0.1             Wpl,yy=8.133E-05    Av,y=0.002        
             E=210000000.      fy=355000.        fu=510000.        Wpl,zz=8.133E-05    Av,z=0.002        
 
 
             STRESS CHECK FORCES & MOMENTS
                 Location             Ned      Med,yy      Med,zz       Ved,z       Ved,y         Ted
                 0.                  -50.         15.         10.         15.         10.          0.
 
             PMM DEMAND/CAPACITY RATIO   (Governing Equation EC3 6.2.1(7))
                 D/C Ratio:    0.674 = 0.05 + sqrt[(0.52)^2 + (0.346)^2  ] <         0.95          OK
                                    = (NEd/NRd) + sqrt[(My,Ed/My,Rd)^2 + (Mz,Ed/Mz,Rd)^2]       (EC3 6.2.1(7))  
 
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
                 Axial               -50.    1003.739    1003.739
 
                                   Npl,Rd       Nu,Rd       Ncr,T      Ncr,TF       An/Ag
                                 1003.739    1038.234   228369.62    6006.691          1.
 
                            Curve   Alpha         Ncr   LambdaBar         Phi         Chi       Nb,Rd
                 Major (y-y)    c    0.49    6006.691       0.409       0.635       0.893     896.004
                 MajorB(y-y)    c    0.49    6006.691       0.409       0.635       0.893     896.004
                 Minor (z-z)    c    0.49    6006.691       0.409       0.635       0.893     896.004
                 MinorB(z-z)    c    0.49    6006.691       0.409       0.635       0.893     896.004
                 Torsional TF   c    0.49    6006.691       0.409       0.635       0.893     896.004
 
             MOMENT DESIGN
                                      Med    Med,span       Mc,Rd       Mv,Rd       Mn,Rd       Mb,Rd
                                   Moment      Moment    Capacity    Capacity    Capacity    Capacity
                 Major (y-y)          15.         15.      28.873      28.873      28.873      28.873
                 Minor (z-z)          10.         10.      28.873      28.873      28.873
 
                                  Section      Flange         Web     Epsilon       Alpha         Psi
                 Compactness      Class 1     Class 1     Class 1       0.814        0.57        -0.9
 
                            Curve AlphaLT LambdaBarLT       PhiLT       ChiLT          Iw         Mcr
                 LTB            d    0.76       0.096       0.465          1.          0.    3159.462
 
                 Factors      kw       C1          C2          C3
                               1.   1.884          0.       0.941
                               za      zs          zg          zz          zj
                             0.05      0.        0.05          0.          0.
 
                 Factors  aLT         bLT         cLT         dLT         eLT        MueY        MueZ
                           0.          0.          0.          0.          0.       0.999       0.999
 
                          nPL          wy          wz         Cyy         Cyz         Czy         Czz
                         0.05       1.403       1.403       1.032       1.035       1.035       1.032
 
                          Cmy         Cmz        CmLT         kyy         kyz         kzy         kzz
                        0.789       0.789          1.        0.77       0.461       0.461        0.77
             */

            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section circularSect = new SectionCHS(100, 10, steel);

            double N = -5e4;
            double V1 = 10e3;
            double V2 = 15e3;
            double M1 = 10e6;
            double M2 = 15e6;
            double T = 0;

            double L = 1000;
            double betay = 1;
            double betaz = 1;
            double betaLT = 1;

            SupportCondition supportConditiony = SupportCondition.EndsRestrained;
            LoadCondition loadConditiony = LoadCondition.NotDirectlyLoaded;
            double? psiy = 0;
            double? psiz = 0;

            SupportCondition supportConditionz = SupportCondition.EndsRestrained;
            LoadCondition loadConditionz = LoadCondition.NotDirectlyLoaded;

            EuroCodeBeamChecker checker = null;//new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz, annex);

            Assert.AreEqual(checker.NRd, 1003738.853, 0.01);
            Assert.AreEqual(checker.MRdy / 28873333.33, 1, 0.03);
            Assert.AreEqual(checker.MRdz / 28873333.33, 1, 0.03);
            double Trd = checker.TRd;
            double Vrdy = checker.VRdy;
            double Vrdz = checker.VRdz;
            double WrAxial = checker.WRAxial;
            double WRBending1 = checker.WRBending1;
            double WRBending2 = checker.WRBending2;
            double WRBuckling1 = checker.WRBuckling1;
            double WRBuckling2 = checker.WRBuckling2;
            double WRCombined = checker.WRCombined;
            double WRShear1 = checker.WRShear1;
            double WRShear2 = checker.WRShear2;
            double WRTorsion = checker.WRTorsion;
            Assert.AreEqual(0.893, checker.Chiy, 0.001);
            Assert.AreEqual(0.893, checker.Chiz, 0.001);
            Assert.AreEqual(1.0, checker.ChiLT, 0.001);
            Assert.AreEqual(0.77, checker.Kyy, 0.005);
            Assert.AreEqual(0.461, checker.Kyz, 0.005);
            Assert.AreEqual(0.461, checker.Kzy, 0.005);
            Assert.AreEqual(0.77, checker.Kzz, 0.005);
            Assert.AreEqual(6006690.95, checker.Ncry, 0.1);
            Assert.AreEqual(6006690.95, checker.Ncrz, 0.1);
            Assert.AreEqual(0.635, checker.Phiy, 0.001);
            Assert.AreEqual(0.635, checker.Phiz, 0.01);
            //Assert.AreEqual(0.465, checker.PhiLT, 0.001); //SAP calcola in modo diverso non documentato
        }

        [TestMethod]
        public void CircularSectionTestAxial3()
        {
            Annex annex = new Annex();
            annex.Gm0 = 1.0;
            annex.Gm1 = 1.0;
            annex.Gm2 = 1.25;

            double fy = 355;
            double fu = 510;

            SteelMaterial steel = new SteelMaterial("S355", 210000, 0.3, fy, fu, 7850);
            Section circularSect = new SectionCHS(400, 10, steel);

            double N = 100 * 1000;
            double V1 = 100 * 1000;
            double V2 = 1000 * 1000;
            double M1 = 20 * 1e6;
            double M2 = 400 * 1e6;
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

            EuroCodeBeamChecker checker = null; // new EuroCodeBeamChecker(circularSect, N, V1, V2, M1, M2, T, L, betay, betaz, betaLT, supportConditiony, loadConditiony, psiy, supportConditionz, loadConditionz, psiz, annex);
        }

        [TestMethod]
        public void RHSClasstificationTest1()
        {
            double h = 150;
            double b = 100;
            double t = 4;

            SectionRHS sec = new SectionRHS(h, b, t, t, t, t, false, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            EuroCodeBeamChecker axialCheck1 = null; //new EuroCodeBeamChecker(sec, -230e3, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            MessageBox.Show(axialCheck1.ClassificationSection.ToString());

            /*EuroCodeBeamChecker axialCheck2 = new EuroCodeBeamChecker(sec, -232e3, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            MessageBox.Show(axialCheck2.ClassificationSection.ToString());*/

            /*EuroCodeBeamChecker axialCheck3 = new EuroCodeBeamChecker(sec, -318e3, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            MessageBox.Show(axialCheck3.ClassificationSection.ToString());*/

            /*EuroCodeBeamChecker axialCheck4 = new EuroCodeBeamChecker(sec, -672e3, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            MessageBox.Show(axialCheck4.ClassificationSection.ToString());*/

            EuroCodeBeamChecker bendingCheck = null; //new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 1e6, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            MessageBox.Show(bendingCheck.ClassificationSection.ToString());
        }

        [TestMethod]
        public void RHSClasstificationTest2()
        {
            double h = 500;
            double b = 100;
            double t = 5;

            SectionRHS sec = new SectionRHS(h, b, t, t, t, t, false, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            double A = sec.Area;
            double J2 = sec.J22;
            double Wel = sec.Wel22Min;
            EuroCodeBeamChecker axialCheck1 = null; // new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            double Aeff = axialCheck1.Aeff;
            double J2eff = axialCheck1.J2eff;
            double Weff = axialCheck1.Weffy;
            var x = "";
        }

        [TestMethod]
        public void SectionHTest1()
        {
            double h = 1024;
            double b = 500;
            double t = 12;
            double tw = 8;

            SectionH sec = new SectionH(h, tw, b, t, b, t, true, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            double Wel = sec.Wel22Min;
            //EuroCodeBeamChecker Check1 = new EuroCodeBeamChecker(sec, -230e3, 0, 0, 0, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            //EuroCodeBeamChecker Check2 = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            EuroCodeBeamChecker Check3 = null; // new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 2600e6, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            //double Mrdy = Check1.MRdy;
            double Weff = Check3.Weffy;

            var x = "";
        }

        [TestMethod]
        public void SectionHTest2()
        {
            double h = 500;
            double bt = 500;
            double bb = 100;
            double tft = 12;
            double tfb = 5;
            double tw = 8;

            SectionH sec = new SectionH(h, tw, bt, tft, bb, tfb, true, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            double Wel = sec.Wel22Min;

            EuroCodeBeamChecker Check = null; // new EuroCodeBeamChecker(sec, 1e3, 0, 0, 0, 0 ,0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());
            
            double Weff = Check.Weffy;

            var x = "";
        }

        [TestMethod]
        public void SectionHTest3()
        {
            double h = 1060;
            double b = 400;
            double t = 30;
            double tw = 8;

            SectionH sec = new SectionH(h, tw, b, t, b, t, true, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            double Wel = sec.Wel22Min;

            EuroCodeBeamChecker Check = null; // new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 2600e6, 0, 1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, new Annex());

            double Weff = Check.Weffy;

            var x = "";
        }

        [TestMethod]
        public void SectionHTest4()
        {
            double h = 300;
            double b = 200;
            double t = 10;
            double tw = 10;

            SectionH sec = new SectionH(h, tw, b, t, b, t, true, new SteelMaterial("S355", 200000, 0.3, 355, 510, 7850));
            double Wel = sec.Wel22Min;

            EuroCodeBeamChecker Check = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, new Annex());
            Check.CheckResistance();
            Check.CheckBuckling(1000, 1, 1, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1, SupportCondition.EndsRestrained, LoadCondition.NotDirectlyLoaded, 1);

            double Weff = Check.Weffy;

            var x = "";
        }
    }
}