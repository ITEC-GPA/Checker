using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Steel.EuroCode;


namespace SteelTests
{
    [TestClass]
    public class AnnexTest
    {
        [TestMethod]
        public void AnnexTest1()
        {
            Annex annex = new Annex();
            annex.Gm0 = 1.05;

            annex = new ItalyAnnex();
        }
    }
}
