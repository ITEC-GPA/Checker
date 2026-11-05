using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Published subpanel results, not a benchmark of bolted-cover restraints.
/// JRC/ECCS EUR22898EN (2007), example16, 16.3.2 and16.4, pp189-191 (PDF203-205).
/// https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf
/// Printed values are rounded; tolerances reflect their precision.</summary>
[TestClass]
public class PlateSubpanelJrcTests
{
    [DataTestMethod]
    [DataRow(450d, 5.391, .531)]
    [DataRow(300d, 5.363, .355)]
    public void PublishedSimplySupportedSubpanelsMatchTheSharedKernel(double width, double kTau, double lambda)
    {
        var r = Ec3WebPanelShear.Calculate(width, 12, 355, 210000, 1, 1.1, 1.2, panelLength: 4000);
        Assert.AreEqual(kTau, r.KTau, .0005 + 1e-12); // half last printed digit, plus floating-point roundoff
        Assert.AreEqual(lambda, r.Slenderness, .001); // source uses rounded 37.4*epsilon
        Assert.AreEqual(1.2, r.Chi);
        double tauRd = r.BucklingResistance / (width * 12);
        Assert.AreEqual(223.6, tauRd, .1);
        Assert.AreEqual(.525, 117.5 / tauRd, .001); // published additional subpanel shear check
    }
}
