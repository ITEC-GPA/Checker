using System;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Shared arithmetic only; callers retain their geometry, source and action guards.
    /// Order of operations is the existing EN6.3 kernel order. ki in mm, stiffness in Nmm/rad.</summary>
    internal static class EndPlateStiffnessArithmetic
    {
        internal const double ElasticModulus = 210000;

        internal static void Column(ConnectionRolledISection column, ColumnWebEffectiveWidth width,
            out double av, out double k2)
        {
            av = SectionPropertyArithmetic.RolledWebArea(column.Area, column.Width, column.FlangeThickness,
                column.Width, column.FlangeThickness, column.WebThickness, 2 * column.RootRadius);
            k2 = .7 * width.Width * column.WebThickness / column.ClearWeb;
        }

        internal static void Row(ConnectionRolledISection column, EndPlateStiffnessRow row,
            out double k3, out double k4, out double k5, out double k10,
            out double flexibility, out double effective)
        {
            k3 = .7 * row.ColumnLength.Minimum * column.WebThickness / column.ClearWeb;
            k4 = .9 * row.ColumnLength.Minimum * Math.Pow(column.FlangeThickness / row.ColumnFlangeM, 3);
            k5 = .9 * row.EndPlateLength.Minimum * Math.Pow(row.EndPlateThickness / row.EndPlateM, 3);
            // Table6.11 coefficient is for one TWO-bolt row; As belongs to ONE bolt.
            k10 = 1.6 * row.BoltStressArea / row.BoltElongationLength;
            flexibility = 1 / k3 + 1 / k4 + 1 / k5 + 1 / k10;
            effective = 1 / flexibility;
        }

        internal static void Assemble(double beta, double av, double k2, double sumKh, double sumKh2,
            out double z, out double keq, out double panelFlexibility, out double totalFlexibility)
        {
            z = sumKh2 / sumKh; keq = sumKh / z;
            // k2 is outside keq. beta0 gives zero panel flexibility without Infinity.
            panelFlexibility = beta * z / (.38 * av);
            totalFlexibility = panelFlexibility + 1 / k2 + 1 / keq;
        }

        internal static double Initial(double z, double totalFlexibility)
            => ElasticModulus * z * z / totalFlexibility;
    }
}
