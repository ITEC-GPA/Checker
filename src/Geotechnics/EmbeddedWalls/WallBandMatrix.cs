namespace GPC.Checkers.Geotechnics.EmbeddedWalls;

/// <summary>Symmetric bandwidth-three matrix for ordered Hermite beam dofs; no artificial diagonal stabilization.</summary>
internal sealed class WallBandMatrix
{
    private readonly double[,] values;
    internal int Count { get; }
    internal WallBandMatrix(int count) { Count = count; values = new double[count, 4]; }
    internal void Add(int i, int j, double value)
    {
        if (j > i) { int swap = i; i = j; j = swap; }
        if (i - j > 3) throw new InvalidOperationException("Outside beam bandwidth.");
        values[i, i - j] += value;
    }
    internal double[] Multiply(double[] x)
    {
        var y = new double[Count];
        for (int i = 0; i < Count; i++)
            for (int d = 0; d <= Math.Min(i, 3); d++)
            { int j = i - d; y[i] += values[i, d] * x[j]; if (d > 0) y[j] += values[i, d] * x[i]; }
        return y;
    }
    internal double[] Solve(double[] rhs, bool[] fixedDofs)
    {
        var a = (double[,])values.Clone();
        var b = (double[])rhs.Clone();
        // Symmetric diagonal equilibration gives a dimensionless factorization.
        var scale = new double[Count];
        for (int i = 0; i < Count; i++)
        {
            if (fixedDofs[i]) { a[i, 0] = 1; b[i] = 0; }
            if (a[i, 0] <= 0 || double.IsNaN(a[i, 0])) throw new InvalidOperationException("Mechanism: nonpositive stiffness.");
            scale[i] = 1 / Math.Sqrt(a[i, 0]);
        }
        for (int i = 0; i < Count; i++)
        {
            b[i] *= scale[i];
            for (int d = 0; d <= Math.Min(3, i); d++)
            {
                int j = i - d;
                a[i, d] = d > 0 && (fixedDofs[i] || fixedDofs[j]) ? 0 : a[i, d] * scale[i] * scale[j];
            }
        }
        for (int i = 0; i < Count; i++)
        {
            for (int j = Math.Max(0, i - 3); j <= i; j++)
            {
                double v = a[i, i - j];
                for (int k = Math.Max(0, i - 3); k < j; k++) v -= a[i, i - k] * a[j, j - k];
                if (i == j)
                {
                    if (v <= 1e-13 || double.IsNaN(v)) throw new InvalidOperationException("Singular or ill-conditioned tangent; check constraints and exhausted resistances.");
                    a[i, 0] = Math.Sqrt(v);
                }
                else a[i, i - j] = v / a[j, 0];
            }
        }
        for (int i = 0; i < Count; i++)
        { for (int j = Math.Max(0, i - 3); j < i; j++) b[i] -= a[i, i - j] * b[j]; b[i] /= a[i, 0]; }
        for (int i = Count - 1; i >= 0; i--)
        { for (int j = i + 1; j <= Math.Min(Count - 1, i + 3); j++) b[i] -= a[j, j - i] * b[j]; b[i] /= a[i, 0]; }
        for (int i = 0; i < Count; i++) b[i] *= scale[i];
        return b;
    }
}
