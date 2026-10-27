using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>The safeguarded Newton solver must reach the same unilateral equilibrium as the
/// former nested bisections (frozen below, Steel 2.122.0.1), within their own tolerances, on a
/// grid that crosses every contact/row switch, and always satisfy the result's convergence test.</summary>
[TestClass]
public class EndPlateNmvFrozenBisectionTests
{
    [TestMethod]
    public void NewtonSolverMatchesTheFrozenNestedBisectionsAcrossContactSwitches()
    {
        var layouts = new[] {
            (new[] { new EndPlateNmvSpringRow("r1", 250, 4e5), new EndPlateNmvSpringRow("r2", 160, 3e5), new EndPlateNmvSpringRow("r3", 60, 2e5), new EndPlateNmvSpringRow("r4", -60, 2e5) },
             new[] { new EndPlateNmvContactBand("bottom", -290, -270, 3e6), new EndPlateNmvContactBand("top", 270, 290, 3e6) }),
            (new[] { new EndPlateNmvSpringRow("single", 0, 6e5) },
             new[] { new EndPlateNmvContactBand("bottom", -200, -180, 2e6), new EndPlateNmvContactBand("top", 180, 200, 1e6) }),
            (new[] { new EndPlateNmvSpringRow("r1", 330, 9e5), new EndPlateNmvSpringRow("r2", 230, 5e5) },
             new[] { new EndPlateNmvContactBand("bottom", -340, -300, 8e6), new EndPlateNmvContactBand("top", 300, 340, 8e6) }) };
        int compared = 0;
        foreach (var (rows, bands) in layouts)
        foreach (double n in new[] { -8e5, -2e5, -1e4, 0, 1e4, 2e5, 6e5 })
        foreach (double m in new[] { -2e8, -5e7, -1e6, 0, 1e6, 5e7, 2e8 })
        {
            if (n == 0 && m == 0) continue;
            var actual = EndPlateNmvEquilibrium.Solve(rows, bands, n, m);
            var (u, theta) = Frozen.Solve(rows, bands, n, m);
            Assert.IsTrue(actual.IsConverged, $"n={n} m={m}");
            double length = Math.Max(1, rows.Select(r => Math.Abs(r.Z)).Concat(bands.SelectMany(c => new[] { Math.Abs(c.LowerZ), Math.Abs(c.UpperZ) })).Max());
            double scale = Math.Abs(u) + Math.Abs(theta) * length + 1e-12;
            Assert.AreEqual(u, actual.AxialOpening, 1e-8 * scale, $"u n={n} m={m}");
            Assert.AreEqual(theta, actual.Rotation, 1e-8 * scale / length, $"theta n={n} m={m}");
            compared++;
        }
        Assert.AreEqual(3 * 48, compared);
    }

    // Frozen copy of the former nested bisections of EndPlateNmvEquilibrium.Solve.
    private static class Frozen
    {
        internal static (double U, double Theta) Solve(EndPlateNmvSpringRow[] rr, EndPlateNmvContactBand[] cc, double axialForce, double moment)
        {
            double length = Math.Max(1, rr.Select(r => Math.Abs(r.Z)).Concat(cc.SelectMany(c => new[] { Math.Abs(c.LowerZ), Math.Abs(c.UpperZ) })).Max());
            double kt = rr.Sum(r => r.Stiffness), kc = cc.Sum(c => c.Stiffness);
            double scale = Math.Max(1, Math.Max(Math.Abs(axialForce), Math.Abs(moment) / length));
            double u = 0, theta = 0;
            double step = Math.Max(1e-12, scale / Math.Min(kt, kc) / length);
            double lower = -step, upper = step;
            for (int i = 0; i < 100 && Response(lower).Moment > moment; i++) lower *= 2;
            for (int i = 0; i < 100 && Response(upper).Moment < moment; i++) upper *= 2;
            for (int i = 0; i < 90; i++)
            {
                theta = (lower + upper) / 2;
                var response = Response(theta); u = response.Opening;
                if (Math.Abs(response.Moment - moment) <= 1e-11 * scale * length) break;
                if (response.Moment < moment) lower = theta; else upper = theta;
            }
            return (u, theta);

            (double Opening, double Moment) Response(double rotation)
            {
                double lo = -Math.Abs(axialForce) / kc - Math.Abs(rotation) * length - 1;
                double hi = Math.Abs(axialForce) / kt + Math.Abs(rotation) * length + 1;
                double opening = 0, mm = 0;
                for (int j = 0; j < 90; j++)
                {
                    opening = (lo + hi) / 2;
                    double nn = 0; mm = 0;
                    foreach (var row in rr) { double f = row.Stiffness * Math.Max(0, opening + rotation * row.Z); nn += f; mm += f * row.Z; }
                    foreach (var band in cc) { var v = Band(band, opening, rotation); nn += v.Force; mm += v.Moment; }
                    if (Math.Abs(nn - axialForce) <= 1e-12 * scale) break;
                    if (nn < axialForce) lo = opening; else hi = opening;
                }
                return (opening, mm);
            }
        }
        // Exact band integral (independent copy of EndPlateNmvEquilibrium.Integrate force/moment).
        private static (double Force, double Moment) Band(EndPlateNmvContactBand band, double u, double theta)
        {
            double a = band.LowerZ, b = band.UpperZ;
            if (theta > 0) b = Math.Min(b, -u / theta);
            else if (theta < 0) a = Math.Max(a, -u / theta);
            else if (u >= 0) return (0, 0);
            if (!(b > a)) return (0, 0);
            double width = b - a, mid = (a + b) / 2, k = band.Stiffness / (band.UpperZ - band.LowerZ);
            double force = k * width * (u + theta * mid);
            return (force, force * mid + k * theta * width * width * width / 12);
        }
    }
}
