using System;
using GPC.Model.Core;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Owned snapshot of a registered Model steel section, including its material.
    /// Geometry remains in Model; no parallel hierarchy of section shapes is introduced.
    /// This application boundary does not qualify a geometry for a calculation method.
    /// Dimensions are mm, areas mm2, strengths/moduli MPa and angles radians.</summary>
    public sealed class SteelSectionInput
    {
        private readonly ISteelSection snapshot;
        public string Name => snapshot.Name;
        public Type GeometryType => snapshot.SectionShape.GetType();
        public double Area { get; }
        public double PrincipalAxisAngle { get; }
        public double YieldStrength { get; }
        public double UltimateStrength { get; }
        public double ElasticModulus { get; }
        public double PoissonRatio { get; }
        public bool? Unperforated { get; }
        public double? NetArea { get; }

        /// <summary>Copies the complete registered Model value. Unknown/custom Model
        /// types are rejected by ModelValues.CopyValue, never sliced into a known shape.
        /// Unperforated=null means holes have not been declared, not that none exist.</summary>
        public SteelSectionInput(ISteelSection section, bool? unperforated = null, double? netArea = null)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            snapshot = ModelValues.CopyValue(section);
            if (snapshot.SectionShape == null || snapshot.SteelMaterial == null)
                throw new ArgumentException("Section geometry and material are required.", nameof(section));
            Area = snapshot.Area; PrincipalAxisAngle = snapshot.AngleX1;
            YieldStrength = snapshot.SteelMaterial.Fyk; UltimateStrength = snapshot.SteelMaterial.Fu;
            ElasticModulus = snapshot.SteelMaterial.E; PoissonRatio = snapshot.SteelMaterial.Ni;
            SteelInputValidation.Positive(Area, nameof(section));
            SteelInputValidation.Finite(PrincipalAxisAngle, nameof(section));
            SteelInputValidation.Positive(YieldStrength, nameof(section));
            SteelInputValidation.Positive(UltimateStrength, nameof(section));
            SteelInputValidation.Positive(ElasticModulus, nameof(section));
            SteelInputValidation.Finite(PoissonRatio, nameof(section));
            if (PoissonRatio <= -1 || PoissonRatio >= .5)
                throw new ArgumentOutOfRangeException(nameof(section), "The isotropic material requires -1 < nu < 0.5.");
            SteelInputValidation.PositiveOptional(netArea, nameof(netArea));
            if (netArea.HasValue && (unperforated != false || netArea.Value > Area))
                throw new ArgumentException("Net area requires declared holes and cannot exceed gross area.", nameof(netArea));
            Unperforated = unperforated; NetArea = netArea;
        }

        /// <summary>Returns a detached, mutable Model value for existing calculators.
        /// Changes to this copy cannot change the input or subsequent copies.</summary>
        public ISteelSection CreateSectionCopy() => ModelValues.CopyValue(snapshot);

        /// <summary>The owned snapshot itself, for the calculators of this library that only READ it.
        /// It keeps Model's thread-safe lazy caches (torsion, shear centre, plastic moduli) across
        /// the checks of a cut, where every CreateSectionCopy cost an XML round trip (about 1.3 ms
        /// for an H) and rebuilt those caches. Never modify it; use CreateSectionCopy for that.</summary>
        internal ISteelSection ReadOnlySection => snapshot;
    }
}
