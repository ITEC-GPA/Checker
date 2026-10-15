using GPC.Checkers.Concrete.Shear;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>
    /// Bug TAG-1 of 15/10/2026 (finding taglio-torsione F1), library side: the sign of Δe of Model Code 2010 (T.6) was not declared.
    /// <see cref="SectionShearInput.AxialEccentricity"/> is the distance of the line of action of N from the mid-depth of z, positive towards the
    /// chord in tension under bending: by the statics of the two chords the tension chord takes M/z + N (1/2 + Δe/z), the "+" of the formula.
    /// The numbers of the library do not change; the existing tests all had Δe = 0.
    /// Scenario of the finding: 300 × 500, d = 460 mm, z = 0.9 d = 414 mm, Asl = 1256.64 mm², C30, γc = 1.5, dg = 20 mm, Es = 200 GPa, no shear
    /// reinforcement, N = -500 kN, M = 100 kNm, V = 100 kN. Expected (verifier, scratchpad/bug/verA-mc-deltae/statica.py, and by hand):
    /// Δe = -50 mm (resultant 50 mm towards the compressed chord): εx = [241 546 + 100 000 - 500 000 (0.5 - 50/414)] / (2 · 200 000 · 1256.64)
    /// = 3.0226e-4, kv = 0.4 / (1 + 1500 εx) · 1300 / (1000 + 0.8889 · 414) = 0.26154, VRd,c = kv √30 / 1.5 · 414 · 300 = 118 611 N;
    /// Δe = +50 mm: εx = 6.199e-5, VRd,c = 157 723 N; Δe = 0: εx = 1.821e-4, VRd,c = 135 399 N.
    /// </summary>
    [TestClass]
    public class ShearAxialEccentricityTests
    {
        private static SectionShearResult Calculate(double axialEccentricity) =>
            SectionShearCalculator.Calculate(new SectionShearInput(new StandardModelCode2010(), -500e3, 100e3, 100e6, 300 * 500, 300, 460, 1256.64,
                30, 0.85 * 30 / 1.5, 450 / 1.15, 1.5, 200000, 0, 150, 90, null, 0.9, 20, axialEccentricity));

        [TestMethod]
        public void CompressionWithEccentricityOfBothSigns()
        {
            var towardsCompressedChord = Calculate(-50);
            Assert.AreEqual(3.0226e-4, towardsCompressedChord.LongitudinalStrain, 0.0005e-4);
            Assert.AreEqual(118611, towardsCompressedChord.VRd, 2);
            var towardsTensionChord = Calculate(50);
            Assert.AreEqual(6.199e-5, towardsTensionChord.LongitudinalStrain, 0.001e-5);
            Assert.AreEqual(157723, towardsTensionChord.VRd, 2);
            var centred = Calculate(0);
            Assert.AreEqual(1.821e-4, centred.LongitudinalStrain, 0.001e-4);
            Assert.AreEqual(135399, centred.VRd, 2);
        }
    }
}
