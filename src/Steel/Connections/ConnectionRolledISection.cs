using System;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Immutable dimensions of a doubly symmetric rolled I/H section; mm and MPa.
    /// Fy/Fu describe the web at its actual thickness. Area includes root fillets. No catalogue lookup occurs.</summary>
    public sealed class ConnectionRolledISection
    {
        public double Height { get; } public double Width { get; } public double WebThickness { get; }
        public double FlangeThickness { get; } public double RootRadius { get; } public double Area { get; }
        public double Fy { get; } public double Fu { get; }
        public double ClearWeb => Height - 2 * FlangeThickness - 2 * RootRadius;
        public ConnectionRolledISection(double height, double width, double webThickness, double flangeThickness,
            double rootRadius, double area, double fy, double fu)
        {
            foreach (double value in new[] { height, width, webThickness, flangeThickness, area, fy, fu })
                ConnectionValidation.Positive(value, nameof(value));
            ConnectionValidation.Nonnegative(rootRadius, nameof(rootRadius));
            if (fu < fy || height <= 2 * (flangeThickness + rootRadius) || width <= webThickness + 2 * rootRadius)
                throw new ArgumentException("Invalid rolled I/H geometry or strengths.");
            // Rolled doubly symmetric I/H with four circular roots. Catalogue dimensions/areas
            // are rounded independently: allow +/-0.5%, not an arbitrary area inside the bounding box.
            double geometricArea = 2 * width * flangeThickness + (height - 2 * flangeThickness) * webThickness
                + 4 * (1 - Math.PI / 4) * rootRadius * rootRadius;
            ConnectionNumbers.Finite(geometricArea, nameof(area));
            if (Math.Abs(area - geometricArea) > .005 * geometricArea)
                throw new ArgumentException("Area differs by more than 0.5% from the rolled I/H dimensions and circular root fillets.");
            Height = height; Width = width; WebThickness = webThickness; FlangeThickness = flangeThickness;
            RootRadius = rootRadius; Area = area; Fy = fy; Fu = fu;
        }
    }

}
