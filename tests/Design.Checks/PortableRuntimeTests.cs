using GPC.Design.Actions;
using GPC.Design.Compatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.Versioning;
using Collections = GPC.Design.Compatibility.CollectionExtensions;

namespace Design.Checks;

[TestClass]
public sealed class PortableRuntimeTests
{
    [TestMethod]
    public void BothConsumersLoadTheNetStandardAssembly()
    {
        var assembly = typeof(NtcSpectra).Assembly;
        var target = (TargetFrameworkAttribute)Attribute.GetCustomAttribute(assembly, typeof(TargetFrameworkAttribute))!;
        Assert.AreEqual(".NETStandard,Version=v2.0", target.FrameworkName);
        Assert.IsFalse(assembly.GetExportedTypes().Any(t => t.Namespace == "GPC.Design.Compatibility"));
    }

    [TestMethod]
    public void FloatingPointBoundaryContractsArePreserved()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
        Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits(PortableMath.CopySign(0, -1)));
        Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(PortableMath.CopySign(negativeZero, 1)));
        Assert.AreEqual(-double.Epsilon, PortableMath.BitDecrement(0));
        Assert.AreEqual(-double.Epsilon, PortableMath.BitDecrement(negativeZero));
        Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(PortableMath.BitDecrement(double.Epsilon)));
        Assert.AreEqual(double.MaxValue, PortableMath.BitDecrement(double.PositiveInfinity));
        Assert.AreEqual(double.NegativeInfinity, PortableMath.BitDecrement(-double.MaxValue));
        Assert.AreEqual(double.NegativeInfinity, PortableMath.BitDecrement(double.NegativeInfinity));
        Assert.IsTrue(double.IsNaN(PortableMath.BitDecrement(double.NaN)));
        Assert.AreEqual(5d, PortableMath.Hypot(3, -4));
        Assert.AreEqual(5e-300, PortableMath.Hypot(3e-300, 4e-300), 1e-315);
        Assert.AreEqual(5e300, PortableMath.Hypot(3e300, 4e300), 1e285);
        Assert.AreEqual(double.PositiveInfinity, PortableMath.Hypot(double.NaN, double.NegativeInfinity));
        Assert.IsTrue(double.IsNaN(PortableMath.Hypot(1, double.NaN)));
        Assert.AreEqual(double.MaxValue, PortableMath.Hypot(double.MaxValue, double.Epsilon));
        Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits(PortableMath.Asinh(negativeZero)));
        Assert.AreEqual(double.Epsilon, PortableMath.Asinh(double.Epsilon));
        Assert.AreEqual(0.88137358701954302523, PortableMath.Asinh(1), 3e-16);
        Assert.AreEqual(710.47586007394394204, PortableMath.Asinh(double.MaxValue), 2e-13);
        Assert.AreEqual(double.NegativeInfinity, PortableMath.Asinh(double.NegativeInfinity));
        Assert.IsTrue(double.IsNaN(PortableMath.Asinh(double.NaN)));
        Assert.IsTrue(PortableMath.IsFinite(double.Epsilon));
        Assert.IsFalse(PortableMath.IsFinite(double.NaN));
        Assert.IsFalse(PortableMath.IsFinite(double.NegativeInfinity));
        Assert.IsTrue(double.IsNaN(PortableMath.Clamp(double.NaN, 0, 1)));
        Assert.ThrowsException<ArgumentException>(() => PortableMath.Clamp(1, 2, 0));
    }

    [TestMethod]
    public void GuardsKeepArgumentNamesAndEnumValidation()
    {
        object? missing = null;
        Assert.AreEqual("missing", Assert.ThrowsException<ArgumentNullException>(() => Guard.NotNull(missing)).ParamName);
        Assert.IsTrue(PortableEnum.IsDefined(DayOfWeek.Monday));
        Assert.IsFalse(PortableEnum.IsDefined((DayOfWeek)100));
        CollectionAssert.AreEqual((DayOfWeek[])Enum.GetValues(typeof(DayOfWeek)), PortableEnum.GetValues<DayOfWeek>());
    }

    [TestMethod]
    public void SelectionKeepsFirstTieAndHandlesEmptyNullAndNaNKeys()
    {
        string?[] values = [null, "bb", "aa", null];
        Assert.AreEqual("aa", Collections.MinBy(values, s => s));
        Assert.AreEqual("bb", Collections.MaxBy(values, s => s));
        Assert.AreEqual("bb", Collections.MinBy(values, s => s?.Length));
        Assert.AreEqual("bb", Collections.MaxBy(values, s => s?.Length));
        Assert.IsNull(Collections.MinBy(Array.Empty<string>(), s => s));
        Assert.IsNull(Collections.MaxBy(new string?[] { null, null }, s => s));
        Assert.ThrowsException<InvalidOperationException>(() => Collections.MinBy(Array.Empty<int>(), i => i));
        Assert.ThrowsException<InvalidOperationException>(() => Collections.MaxBy(Array.Empty<int>(), i => i));
        double[] numbers = [1, double.NaN, 2];
        Assert.IsTrue(double.IsNaN(Collections.MinBy(numbers, n => n)));
        Assert.AreEqual(2d, Collections.MaxBy(numbers, n => n));
        int visits = 0;
        Assert.AreEqual(0, Collections.MinBy(Enumerable.Range(0, 4000), i => { visits++; return i; }));
        Assert.AreEqual(4000, visits, "Selection must evaluate each candidate once.");
    }

    [TestMethod]
    public void DistinctAndSkipLastStayDeferredAndRetainOrder()
    {
        int visits = 0;
        var distinct = Collections.DistinctBy(new[] { "bb", "aa", "c", "dd" }, s => { visits++; return s.Length; });
        Assert.AreEqual(0, visits);
        CollectionAssert.AreEqual(new[] { "bb", "c" }, distinct.ToArray());
        Assert.AreEqual(4, visits);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Collections.SkipLast(Enumerable.Range(0, 5), 2).ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), Collections.SkipLast(Enumerable.Range(0, 5), 8).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, Collections.SkipLast(Enumerable.Range(0, 2), -1).ToArray());
        var map = new Dictionary<int, string> { [1] = "old" };
        Assert.IsFalse(Collections.TryAdd(map, 1, "new"));
        Assert.AreEqual("old", map[1]);
        Assert.IsTrue(Collections.TryAdd(map, 2, "new"));
        Assert.IsNull(Collections.GetValueOrDefault(map, 3));
        Assert.AreEqual("fallback", Collections.GetValueOrDefault(map, 3, "fallback"));
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public void PortablePrimitivesMatchNativeNet8AcrossTheDoubleRange()
    {
        var random = new Random(47821);
        var bytes = new byte[8];
        long asinhUlp = 0, hypotUlp = 0;
        double[] edges = [0, -0.0, double.Epsilon, -double.Epsilon, 1e-8, 2, 1e154,
            double.MaxValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN];
        var values = edges.Concat(Enumerable.Range(0, 25000).Select(_ =>
        {
            random.NextBytes(bytes); return BitConverter.ToDouble(bytes, 0);
        })).ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            double x = values[i], y = values[(i + 107) % values.Length];
            EqualBits(Math.CopySign(x, y), PortableMath.CopySign(x, y));
            EqualBits(Math.BitDecrement(x), PortableMath.BitDecrement(x));
            EqualBits(Math.Clamp(x, -2, 2), PortableMath.Clamp(x, -2, 2));
            Assert.AreEqual(double.IsFinite(x), PortableMath.IsFinite(x));
            asinhUlp = Math.Max(asinhUlp, ErrorUlp(Math.Asinh(x), PortableMath.Asinh(x)));
            hypotUlp = Math.Max(hypotUlp, ErrorUlp(double.Hypot(x, y), PortableMath.Hypot(x, y)));
            // Similar magnitudes exercise addition/rounding; random bit patterns mostly have distant exponents.
            hypotUlp = Math.Max(hypotUlp, ErrorUlp(double.Hypot(x, x * .75), PortableMath.Hypot(x, x * .75)));
        }
        Console.WriteLine($"Native .NET 8 comparison: {values.Length} values; max Asinh {asinhUlp} ULP, Hypot {hypotUlp} ULP.");
        Assert.IsTrue(asinhUlp <= 4, $"Asinh: {asinhUlp} ULP");
        Assert.IsTrue(hypotUlp <= 4, $"Hypot: {hypotUlp} ULP");
    }

    private static void EqualBits(double expected, double actual)
    {
        if (double.IsNaN(expected)) Assert.IsTrue(double.IsNaN(actual));
        else Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    private static long ErrorUlp(double expected, double actual)
    {
        if (!double.IsFinite(expected)) { EqualBits(expected, actual); return 0; }
        Assert.IsTrue(double.IsFinite(actual), $"Expected {expected:R}, actual {actual:R}");
        Assert.AreEqual(Math.Sign(expected), Math.Sign(actual));
        return Math.Abs(BitConverter.DoubleToInt64Bits(Math.Abs(expected)) - BitConverter.DoubleToInt64Bits(Math.Abs(actual)));
    }
#endif
}
