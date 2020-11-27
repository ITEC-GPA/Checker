using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPC.Checker.Steel.EuroCode;


namespace AnnexTest
{
    [TestClass]
    public class AnnexTest
    {
        [TestMethod]
        public void AnnexTest1()
        {
            Annex annex = new Annex(1.0, 1.1, 1.25);
            annex.Gm0 = 1.05;

            annex = new ItalyAnnex();
        }
    }
}
