using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Serviceability
{
    /// <summary>Straight wall of a thin-walled shear-flow model: midline from (X0, Y0) along the unit direction
    /// (Ex, Ey), length L, thickness T; centroidal section axes, mm. Flow q (N/mm) in the +s direction:
    /// q(s) = q(0) - Vy/Ixx int(y t ds) - Vx/Iyy int(x t ds), i.e. tau = V S/(I t) (EN 1993-1-1:2005 6.2.6(4)
    /// eq. 6.20) for both shear components at once, with S and I of the same midline model so that the flows
    /// balance Vx and Vy exactly. Positive face, V along +X/+Y, N tension positive.</summary>
    internal sealed class ShearWall
    {
        internal string Id { get; }
        internal double X0 { get; }
        internal double Y0 { get; }
        internal double Ex { get; }
        internal double Ey { get; }
        internal double Length { get; }
        internal double Thickness { get; }
        internal StressPolynomial Flow { get; }

        private ShearWall(string id, double x0, double y0, double ex, double ey, double length, double t, StressPolynomial flow)
        { Id = id; X0 = x0; Y0 = y0; Ex = ex; Ey = ey; Length = length; Thickness = t; Flow = flow; }

        internal static ShearWall Create(string id, double x0, double y0, double ex, double ey, double length, double t,
            double q0, double vx, double vy, double ixx, double iyy)
            => new ShearWall(id, x0, y0, ex, ey, length, t, new StressPolynomial(q0,
                -t * (vy * y0 / ixx + vx * x0 / iyy), -t * (vy * ey / ixx + vx * ex / iyy) / 2));

        internal double End => Flow.At(Length);
        internal ShearWall WithCirculation(double q) => new ShearWall(Id, X0, Y0, Ex, Ey, Length, Thickness, Flow + new StressPolynomial(q));
    }

    /// <summary>Straight line of material fibres where sigma is evaluated: (X0, Y0) at u=0, (X1, Y1) at u=1, with
    /// the wall shear stress Tau(u) in MPa (signed along the wall). A zero-length line is a single point.</summary>
    internal sealed class StressLine
    {
        internal string Id { get; }
        internal double X0 { get; }
        internal double Y0 { get; }
        internal double X1 { get; }
        internal double Y1 { get; }
        internal StressPolynomial Tau { get; }

        internal StressLine(string id, double x0, double y0, double x1, double y1, StressPolynomial tau)
        { Id = id; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Tau = tau; }

        /// <summary>Face of a wall: the midline shifted by (dx, dy), s from sa to sb; tau = q/t, constant across the thickness.</summary>
        internal static StressLine OnWall(string id, ShearWall w, double dx, double dy, double sa, double sb)
            => new StressLine(id, w.X0 + w.Ex * sa + dx, w.Y0 + w.Ey * sa + dy, w.X0 + w.Ex * sb + dx, w.Y0 + w.Ey * sb + dy,
                (1 / w.Thickness) * w.Flow.Compose(sa, sb - sa));

        internal static StressLine Point(string id, double x, double y, double tau) => new StressLine(id, x, y, x, y, new StressPolynomial(tau));
    }

    /// <summary>Qualified shear-stress models of the service-stress check, one per supported exact Model type:
    /// doubly symmetric H (open thin-walled), RHS with symmetric walls (single closed cell, shear centre at the
    /// centroid), CHS (thin-walled circle) and solid rectangles (Jourawski) in the narrow range only. Normal
    /// stresses always use the gross Model properties; these models give the shear stresses and the lines or
    /// arc where both are evaluated. Coordinates: section geometry axes X/Y through the centroid, mm.</summary>
    internal sealed class ServiceStressModel
    {
        internal string Family { get; private set; }
        internal string ShearModel { get; private set; }
        internal double ShearIxx { get; private set; }
        internal double ShearIyy { get; private set; }
        internal IReadOnlyList<StressLine> Lines { get; private set; }
        /// <summary>Midline walls of the thin-walled models (H, RHS), with their final flows; empty otherwise.</summary>
        internal IReadOnlyList<ShearWall> Walls { get; private set; } = new ShearWall[0];
        /// <summary>CHS only: outer radius for sigma, midline radius and thickness for tau(theta).</summary>
        internal double OuterRadius { get; private set; }
        internal double MidRadius { get; private set; }
        internal double WallThickness { get; private set; }
        internal bool IsCircle => Lines == null;

        /// <summary>The model for the shear forces Vx/Vy (section geometry axes, N), or null with the refusal code.</summary>
        internal static ServiceStressModel Create(ISteelSection section, double vx, double vy, out string code, out string message)
        {
            code = null; message = null;
            var shape = section.SectionShape; var type = shape.GetType();
            if (type == typeof(SectionH))
            {
                var h = (SectionH)shape;
                if (section.FormedType == Section.FormedTypes.ColdFormed)
                    return Refuse("ColdFormedOpenSection.NotQualified", "Cold-formed open sections require their own qualified design route.", out code, out message);
                if (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange)
                    return Refuse("Geometry.NotDoublySymmetric", "Monosymmetric H: the shear centre is off the centroid and the cut shears would twist the section.", out code, out message);
                return H(h.Height, h.LenghtTopFlange, h.ThicknessTopFlange, h.ThicknessWeb, vx, vy);
            }
            if (type == typeof(SectionRHS) || type == typeof(SectionRHSRoundedCorners))
            {
                var r = (SectionRHS)shape;
                if (type == typeof(SectionRHS) && r.R != 0)
                    return Refuse("Geometry.NotQualified", "Model's SectionRHS radius is not part of its section properties: sharp corners only.", out code, out message);
                if (r.ThicknessTop != r.ThicknessBottom || r.ThicknessWebLeft != r.ThicknessWebRight)
                    return Refuse("Geometry.NotDoublySymmetric", "RHS walls must be symmetric (top=bottom, left=right) for the shear centre to lie at the centroid.", out code, out message);
                return Box(r.Base, r.Height, r.ThicknessTop, r.ThicknessWebLeft, type == typeof(SectionRHSRoundedCorners), vx, vy);
            }
            if (type == typeof(SectionCHS))
            {
                var chs = (SectionCHS)shape;
                return Circle(chs.Diameter, chs.Thickness);
            }
            if (type == typeof(SectionRectangular))
            {
                var r = (SectionRectangular)shape;
                if (r.Angle != 0)
                    return Refuse("Geometry.NotQualified", "Rotated rectangles are not mapped.", out code, out message);
                return Rectangle(r.Width, r.Height, vx, vy, out code, out message);
            }
            return Refuse("Geometry.NotQualified", "Service stresses are qualified for exact doubly symmetric H, RHS, CHS and solid rectangles only; "
                + type.Name + " has no qualified shear-stress distribution.", out code, out message);
        }

        private static ServiceStressModel Refuse(string c, string m, out string code, out string message)
        { code = c; message = m; return null; }

        private static void Positive(double value, string name)
        {
            if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentException("Invalid section dimension: " + name + ".");
        }

        // Midline model: flanges are lines at +-yf, the web a line from yf to -yf; flange own inertia neglected.
        // Fillets are part of the gross properties of sigma but not of the shear flow.
        private static ServiceStressModel H(double height, double b, double tf, double tw, double vx, double vy)
        {
            Positive(height - 2 * tf, "web height"); Positive(b - tw, "flange outstand"); Positive(tf, "flange"); Positive(tw, "web");
            double yf = (height - tf) / 2, ixx = 2 * b * tf * yf * yf + tw * Math.Pow(2 * yf, 3) / 12, iyy = tf * b * b * b / 6;
            var tl = ShearWall.Create("TopFlange.Left", -b / 2, yf, 1, 0, b / 2, tf, 0, vx, vy, ixx, iyy);
            var tr = ShearWall.Create("TopFlange.Right", b / 2, yf, -1, 0, b / 2, tf, 0, vx, vy, ixx, iyy);
            var web = ShearWall.Create("Web", 0, yf, 0, -1, 2 * yf, tw, tl.End + tr.End, vx, vy, ixx, iyy);
            var bl = ShearWall.Create("BottomFlange.Left", -b / 2, -yf, 1, 0, b / 2, tf, 0, vx, vy, ixx, iyy);
            var br = ShearWall.Create("BottomFlange.Right", b / 2, -yf, -1, 0, b / 2, tf, 0, vx, vy, ixx, iyy);
            var lines = new List<StressLine>();
            foreach (var flange in new[] { tl, tr, bl, br })
            {
                double outward = flange.Y0 > 0 ? tf / 2 : -tf / 2;
                lines.Add(StressLine.OnWall(flange.Id + ".Outer", flange, 0, outward, 0, b / 2));
                lines.Add(StressLine.OnWall(flange.Id + ".Inner", flange, 0, -outward, 0, b / 2 - tw / 2));
            }
            lines.Add(StressLine.OnWall("Web.Left", web, -tw / 2, 0, tf / 2, 2 * yf - tf / 2));
            lines.Add(StressLine.OnWall("Web.Right", web, tw / 2, 0, tf / 2, 2 * yf - tf / 2));
            return new ServiceStressModel { Family = "H", Lines = lines, Walls = new[] { tl, tr, web, bl, br }, ShearIxx = ixx, ShearIyy = iyy,
                ShearModel = "Open thin-walled midline model, tau = V S/(I t) from the flange tips; fillets neglected in tau" };
        }

        // Single closed cell, cut at the top-flange centre and closed with the constant flow of zero twist,
        // -sum(int q/t ds)/sum(L/t): the cut shears pass through the shear centre (the centroid).
        private static ServiceStressModel Box(double width, double height, double tf, double tw, bool rounded, double vx, double vy)
        {
            Positive(tf, "flange"); Positive(tw, "web"); Positive(width - 2 * tw, "inner width"); Positive(height - 2 * tf, "inner height");
            double bm = width - tw, hm = height - tf;
            double ixx = 2 * bm * tf * hm * hm / 4 + 2 * tw * hm * hm * hm / 12, iyy = 2 * hm * tw * bm * bm / 4 + 2 * tf * bm * bm * bm / 12;
            var open = new List<ShearWall>();
            open.Add(ShearWall.Create("Top.Left", 0, hm / 2, -1, 0, bm / 2, tf, 0, vx, vy, ixx, iyy));
            open.Add(ShearWall.Create("Web.Left", -bm / 2, hm / 2, 0, -1, hm, tw, open[0].End, vx, vy, ixx, iyy));
            open.Add(ShearWall.Create("Bottom", -bm / 2, -hm / 2, 1, 0, bm, tf, open[1].End, vx, vy, ixx, iyy));
            open.Add(ShearWall.Create("Web.Right", bm / 2, -hm / 2, 0, 1, hm, tw, open[2].End, vx, vy, ixx, iyy));
            open.Add(ShearWall.Create("Top.Right", bm / 2, hm / 2, -1, 0, bm / 2, tf, open[3].End, vx, vy, ixx, iyy));
            double twist = open.Sum(w => w.Flow.Integral().At(w.Length) / w.Thickness), compliance = open.Sum(w => w.Length / w.Thickness);
            var walls = open.Select(w => w.WithCirculation(-twist / compliance)).ToArray();
            var lines = new List<StressLine>();
            var top = walls[0]; var right = walls[4];
            lines.Add(StressLine.OnWall("Top.Left.Outer", top, 0, tf / 2, 0, bm / 2));
            lines.Add(StressLine.OnWall("Top.Right.Outer", right, 0, tf / 2, 0, bm / 2));
            lines.Add(StressLine.OnWall("Top.Left.Inner", top, 0, -tf / 2, 0, bm / 2 - tw / 2));
            lines.Add(StressLine.OnWall("Top.Right.Inner", right, 0, -tf / 2, tw / 2, bm / 2));
            lines.Add(StressLine.OnWall("Web.Left.Outer", walls[1], -tw / 2, 0, 0, hm));
            lines.Add(StressLine.OnWall("Web.Left.Inner", walls[1], tw / 2, 0, tf / 2, hm - tf / 2));
            lines.Add(StressLine.OnWall("Bottom.Outer", walls[2], 0, -tf / 2, 0, bm));
            lines.Add(StressLine.OnWall("Bottom.Inner", walls[2], 0, tf / 2, tw / 2, bm - tw / 2));
            lines.Add(StressLine.OnWall("Web.Right.Outer", walls[3], tw / 2, 0, 0, hm));
            lines.Add(StressLine.OnWall("Web.Right.Inner", walls[3], -tw / 2, 0, tf / 2, hm - tf / 2));
            // Outer corners: the midline corner flow over the thinner wall (conservative); with rounded corners the
            // point lies outside the material, a conservative bound of sigma.
            double t = Math.Min(tf, tw);
            lines.Add(StressLine.Point("Corner.TopLeft", -width / 2, height / 2, walls[0].End / t));
            lines.Add(StressLine.Point("Corner.BottomLeft", -width / 2, -height / 2, walls[1].End / t));
            lines.Add(StressLine.Point("Corner.BottomRight", width / 2, -height / 2, walls[2].End / t));
            lines.Add(StressLine.Point("Corner.TopRight", width / 2, height / 2, walls[3].End / t));
            return new ServiceStressModel { Family = rounded ? "RHSRounded" : "RHS", Lines = lines, Walls = walls, ShearIxx = ixx, ShearIyy = iyy,
                ShearModel = "Closed single-cell thin-walled midline model (sharp corners), tau = V S/(I t) plus the zero-twist circulating flow"
                    + (rounded ? "; outer corner points outside the rounded material (conservative)" : "") };
        }

        private static ServiceStressModel Circle(double diameter, double t)
        {
            Positive(t, "wall"); Positive(diameter - 2 * t, "inner diameter");
            double rm = (diameter - t) / 2, i = Math.PI * rm * rm * rm * t;
            return new ServiceStressModel { Family = "CHS", OuterRadius = diameter / 2, MidRadius = rm, WallThickness = t, ShearIxx = i, ShearIyy = i,
                ShearModel = "Thin-walled circle, tau(theta) = (Vy cos(theta) - Vx sin(theta))/(pi rm t), maximum 2V/A; sigma on the outer surface" };
        }

        // Jourawski on a solid rectangle assumes tau uniform across the width b: GPC admits it only for b <= h/2
        // along the shear (classical elasticity puts the edge peak a few percent above it there; it grows quickly
        // for wider sections), and for one shear component.
        private static ServiceStressModel Rectangle(double width, double height, double vx, double vy, out string code, out string message)
        {
            code = null; message = null;
            Positive(width, "width"); Positive(height, "height");
            if (vx != 0 && vy != 0)
                return Refuse("SolidShear.NotQualified", "Solid rectangle with both shear components: the two-dimensional shear field is not qualified.", out code, out message);
            if (vy != 0 && width > height / 2 || vx != 0 && height > width / 2)
                return Refuse("SolidShear.NotQualified", "Jourawski's uniform shear across a solid rectangle is admitted only for a width across the shear at most half the depth along it.", out code, out message);
            double area = width * height;
            var lines = new List<StressLine>();
            if (vx != 0)
            {
                // tau(x) = 1.5 Vx/A (1 - 4x^2/b^2) along both long edges, u from x=-b/2 to x=b/2.
                var tau = new StressPolynomial(0, 6 * vx / area, -6 * vx / area);
                lines.Add(new StressLine("Edge.Top", -width / 2, height / 2, width / 2, height / 2, tau));
                lines.Add(new StressLine("Edge.Bottom", -width / 2, -height / 2, width / 2, -height / 2, tau));
            }
            else
            {
                // tau(y) = 1.5 Vy/A (1 - 4y^2/h^2), uniform across the width: the maximum of sigma at given y is on a side.
                var tau = new StressPolynomial(0, 6 * vy / area, -6 * vy / area);
                lines.Add(new StressLine("Side.Left", -width / 2, height / 2, -width / 2, -height / 2, tau));
                lines.Add(new StressLine("Side.Right", width / 2, height / 2, width / 2, -height / 2, tau));
            }
            return new ServiceStressModel { Family = "Rectangular", Lines = lines, ShearIxx = width * height * height * height / 12, ShearIyy = height * width * width * width / 12,
                ShearModel = vx == 0 && vy == 0 ? "No shear" : "Solid rectangle, Jourawski tau = 1.5 V/A (1 - 4z^2/d^2), uniform across the width" };
        }
    }
}
