using System;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Results;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Explicit bridge from Model action conventions to the common Steel input.
    /// ModelChecker preparation remains responsible for units, concomitance, positive face,
    /// centroid reduction and revision validity. This bridge does not infer them.</summary>
    public static class ModelSteelInput
    {
        /// <summary>The caller declares that forces are already in the section geometry's
        /// local X/Y axes, at its centroid, in N/Nmm. Rotates force and moment components
        /// by AngleX1 to the principal section axes. Does not use global coordinates,
        /// move the reduction point, apply factors or change the axial-force sign.</summary>
        public static SteelVerificationInput FromSectionAxes(SteelSectionInput section,
            ResultBeamForces forces, SteelDesignContext code, SteelMemberInput member = null)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (forces == null) throw new ArgumentNullException(nameof(forces));
            // Validate before rotating too: a finite input may overflow during rotation.
            var local = new SteelActions(forces.N, forces.V1, forces.V2, forces.T, forces.M1, forces.M2);
            double c = Math.Cos(section.PrincipalAxisAngle), s = Math.Sin(section.PrincipalAxisAngle);
            var principal = new SteelActions(local.N, c * local.V1 + s * local.V2,
                -s * local.V1 + c * local.V2, local.T,
                c * local.M1 + s * local.M2, -s * local.M1 + c * local.M2);
            return new SteelVerificationInput(section, principal, code, member);
        }
    }
}
