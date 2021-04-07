using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Model.Sections;
using GPC.Model.Materials;
using GPC.Checker.Steel.EuroCode;
using System.Windows;

namespace SteelTests
{
    [TestClass]
    public class Classification
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
        public void CHSClasstificationTest1()
        {
            double D = 1219;
            double t = 25;

            SectionCHS sec = new SectionCHS(D, t, new SteelMaterial("S235", 210000, 0.3, 235, 510, 7850));
            EuroCodeBeamChecker classification = new EuroCodeBeamChecker(sec, -1, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 86

            sec = new SectionCHS(D, t, new SteelMaterial("S275", 210000, 0.3, 275, 510, 7850));
            classification = new EuroCodeBeamChecker(sec, -1, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 2); //Cordova cap. 2 pag. 86

            sec = new SectionCHS(D, t, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850));
            classification = new EuroCodeBeamChecker(sec, -1, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 86

            /*sec = new SectionCHS(D, t, new SteelMaterial("S460", 210000, 0.3, 460, 510, 7850));
            classification = new EuroCodeBeamChecker(sec, -1, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 86*/
        }

        [TestMethod]
        public void RHSClasstificationTest1()
        {
            //in hot formed h = H - 2 * t - (0.5+0.5) * t
            //in favour of safety h = H - 2 t, for this reason a value of 0.95 * Ned has been used to take this tolerance
            double h = 500;
            double b = 300;
            double t = 12.5;

            SectionRHS sec = new SectionRHS(h, b, t, t, t, t, false, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850));
            EuroCodeBeamChecker classification = new EuroCodeBeamChecker(sec, -2026e3 * 0.95, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, -2859e3 * 0.95, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 2); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, -6029e3 * 0.95, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, -6029e3, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 75
            Assert.AreEqual(181.2 * 100 / classification.Aeff, 1, 0.01); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 1e6, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 75
        }

        [TestMethod]
        public void RHSClasstificationTest2()
        {
            //in hot formed h = H - 2 * t - (0.5+0.5) * t
            //in favour of safety h = H - 2 t, for this reason a value of 0.95 * Ned has been used to take this tolerance
            double h = 350;
            double b = 250;
            double t = 10;

            Annex annex = new Annex();

            SectionRHS sec = new SectionRHS(h, b, t, t, t, t, false, new SteelMaterial("S355", 210000, 0.3, 355, 510, 7850));
            EuroCodeBeamChecker classification = new EuroCodeBeamChecker(sec, -1597e3 * 0.95, 0, 0, 0, 0, 0, annex);
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, -2130e3 * 0.95, 0, 0, 0, 0, 0, annex);
            Assert.AreEqual(classification.ClassificationSection, 2); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, sec.Area * -355, 0, 0, 0, 0, 0, annex);
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 1e6, 0, annex);
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 75

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, annex);
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 75
        }

        [TestMethod]
        public void ClassificationSectionHTest1()
        {
            //HEA800
            double h = 790; //Cordova cap. 2 pag. 61
            double b = 300;
            double t = 28;
            double tw = 15;

            SectionH sec = new SectionH(h, tw, b, t, b, t, true, new SteelMaterial("S235", 200000, 0.3, 235, 510, 7850));

            EuroCodeBeamChecker classification = new EuroCodeBeamChecker(sec, -1211e3 * 0.85, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, -1699e3 * 0.85, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 2); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, -6053e3 * 0.84, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, -6053e3, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 61
            Assert.AreEqual(classification.Aeff / (277.0 * 100), 1, 0.05); //Cordova cap. 2 pag. 61

            /*classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 61 --> cordova non classifica anima perchè passa assse neutro ma per pressoflessione?*/

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 1e6, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 61
        }

        [TestMethod]
        public void ClassificationSectionHTest2()
        {
            //HEA700
            double h = 690; //Cordova cap. 2 pag. 61
            double b = 300;
            double t = 27;
            double tw = 14.5;

            SectionH sec = new SectionH(h, tw, b, t, b, t, true, new SteelMaterial("S235", 200000, 0.3, 235, 510, 7850));

            EuroCodeBeamChecker classification = new EuroCodeBeamChecker(sec, -1332e3 * 0.85, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, -1788e3 * 0.85, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 2); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, -1788e3, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 3); //Cordova cap. 2 pag. 61

            classification = new EuroCodeBeamChecker(sec, sec.Area * -235, 0, 0, 0, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 61 --> classe 3 considerando raggio curvatura interno

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 1e6, 0, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 4); //Cordova cap. 2 pag. 61 --> Cordova non classifica anima perchè passa assse neutro ma per pressoflessione?*/

            classification = new EuroCodeBeamChecker(sec, 0, 0, 0, 0, 1e6, 0, new Annex());
            Assert.AreEqual(classification.ClassificationSection, 1); //Cordova cap. 2 pag. 61
        }
    }
}