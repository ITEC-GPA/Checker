using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        /// <summary>SwayBucklingPlanes=1|2|1,2 with SwayModeSource: principal bending planes whose relevant flexural
        /// buckling mode is a sway mode (1: M1 with Ncr1, 2: M2 with Ncr2). An explicit caller declaration, never
        /// inferred from the mesh, the restraints or the effective lengths.</summary>
        private bool SwayPlane(int plane)
        {
            if (!_parameters.TryGetValue("SwayBucklingPlanes", out var planes))
            {
                if (_parameters.ContainsKey("SwayModeSource")) throw new ArgumentException("SwayModeSourceWithoutSwayBucklingPlanes");
                return false;
            }
            if (planes != "1" && planes != "2" && planes != "1,2") throw new ArgumentException("InvalidSwayBucklingPlanes: " + planes);
            Required("SwayModeSource");
            return planes == "1,2" || planes == (plane == 1 ? "1" : "2");
        }

        /// <summary>EN 1993-1-1:2005 Table B.3 note (Circolare 2019 Table C4.2.VI, see SteelSwayMomentFactor): a declared
        /// sway plane takes Cm=0.9 whatever its diagram. The other plane keeps LinearDiagrams, ConservativeEnvelope or its
        /// explicit factor; CmLT belongs to the lateral-torsional segment and keeps its own preparation. An explicit Cm of a
        /// sway plane conflicts with the computed value. Each factor and its basis are traced per plane.</summary>
        private double[] PrepareSwayMomentFactors(PhysicalMemberCheckInput member, SteelVerificationInput input,
            SteelPhysicalMemberPreparation physical, ref string source)
        {
            var standard = input.Code.SectionCode.Standard;
            string sway = SteelSwayMomentFactor.Reference(standard) + "; sway declaration: " + Required("SwayModeSource");
            string table = standard == SectionResistanceStandard.Ntc2018 ? "Circolare2019 TableC4.2.VI" : "EN1993-1-1:2005 TableB.3";
            var cm = new double[3]; var basis = new string[3]; string[] names = { "Cm1", "Cm2", "CmLT" };
            if (!_parameters.TryGetValue("MomentFactorPreparation", out var method))
            {
                for (int plane = 1; plane <= 2; plane++)
                    if (SwayPlane(plane) && _parameters.ContainsKey(names[plane - 1]))
                        throw new ArgumentException("Choose computed or explicit moment factors, not both: plane " + plane
                            + " is declared sway (Cm" + plane + "=0.9); remove Cm" + plane + ".");
                for (int k = 0; k < 3; k++)
                    if (k == 2 || !SwayPlane(k + 1)) { cm[k] = Number(names[k]); basis[k] = "explicit factor, MomentFactorSource"; }
            }
            else
            {
                if ((method != "LinearDiagrams" && method != "ConservativeEnvelope") || names.Any(_parameters.ContainsKey))
                    throw new ArgumentException("Choose computed or explicit moment factors, not both.");
                // Same span qualification as the nonsway preparation: it still sets the other plane and CmLT.
                if (physical.Segment == null || method == "LinearDiagrams" && physical.Segment.Count > 1) FullDeclaredSpan(member);
                if (method == "ConservativeEnvelope")
                {
                    string generic = "generic same-case FEM diagram, conservative Cm=1; exported extrema qualification: " + Required("MomentDiagramCoverageSource");
                    for (int k = 0; k < 3; k++) { cm[k] = 1; basis[k] = generic; }
                }
                else
                {
                    // CmLT keeps the actual linear M1 diagram also when plane 1 is sway; a sway plane 2 reads no M2 diagram.
                    cm[0] = cm[2] = LinearMomentFactor(input, physical, a => a.M1);
                    if (!SwayPlane(2)) cm[1] = LinearMomentFactor(input, physical, a => a.M2);
                    basis[0] = basis[1] = table + ", linear nonsway diagram, same explicit full braced span";
                    basis[2] = table + ", linear M1 diagram, same explicit full braced span";
                }
            }
            for (int plane = 1; plane <= 2; plane++)
                if (SwayPlane(plane)) { cm[plane - 1] = SteelSwayMomentFactor.Value; basis[plane - 1] = sway; }
            var evidence = new List<CheckCalculationValue>();
            for (int k = 0; k < 3; k++)
                evidence.Add(new CheckCalculationValue("Member.MomentFactor." + names[k], cm[k], "1",
                    (k == 2 ? "Lateral-torsional segment, not affected by the sway declaration: " : "Plane " + (k + 1) + ": ") + basis[k]));
            physical.AddEvidence(evidence);
            source += "; " + string.Join("; ", Enumerable.Range(0, 3).Select(k => names[k] + "=" + cm[k].ToString("R", CultureInfo.InvariantCulture) + " (" + basis[k] + ")"));
            return cm;
        }

        /// <summary>Ncr provenance of every declared sway plane: an explicit value or the declared MemberDesignContext
        /// effective length, both caller sway assessments. Automatic fork-segment values never reach this point
        /// (VerifySegments refuses them); without either value the library reports the missing input.</summary>
        private void TraceSwayCriticalForces(PhysicalMemberCheckInput input, SteelVerificationInput common,
            IReadOnlyList<SteelStabilityValue> values, SteelPhysicalMemberPreparation physical)
        {
            var evidence = new List<CheckCalculationValue>();
            for (int plane = 1; plane <= 2; plane++)
            {
                if (!SwayPlane(plane)) continue;
                string key = "Member.Sway.Plane" + plane;
                evidence.Add(new CheckCalculationValue(key, 1, "1", "Declared sway buckling mode: " + Required("SwayModeSource")));
                var value = values.SingleOrDefault(v => v.Quantity == (plane == 1 ? SteelStabilityQuantity.Ncr1 : SteelStabilityQuantity.Ncr2));
                double? length = plane == 1 ? common.Member.BucklingLength1 : common.Member.BucklingLength2;
                if (value != null) evidence.Add(new CheckCalculationValue(key + ".Ncr", value.Value, value.Unit, "Explicit sway Ncr" + plane + ": " + value.Source));
                else if (length.HasValue) evidence.Add(new CheckCalculationValue(key + ".EffectiveLength", length, "mm",
                    "Declared MemberDesignContext effective length, Euler Ncr" + plane + " of the sway mode: " + input.Snapshot.Context.LengthSource));
                else evidence.Add(new CheckCalculationValue(key + ".Ncr", null, null, "Missing: the sway Ncr" + plane + " needs an explicit value or a declared effective length"));
            }
            physical.AddEvidence(evidence);
        }

        /// <summary>CriticalValues idealizes every in-plane span and fork segment with both end translations fixed, i.e. a
        /// nonsway mode. A sway plane 2 is refused (forks fix the lateral translation, also for Mcr). A sway plane 1 keeps
        /// the forks for Ncr2/NcrT/Mcr but needs an explicit Ncr1 or a declared EffectiveLength1; true when it applies.</summary>
        private bool QualifySwaySegments(MemberInputSnapshot snapshot, string mode)
        {
            if (mode != "BeamColumn") return false;
            if (SwayPlane(2))
                throw new SteelPreparationRefusal("SteelMember.Sway.NonswayNcr2", "Plane 2 is declared sway, but CriticalValues splits the member at fork restraints"
                    + " that fix the lateral translation at both segment ends: Ncr2 and Mcr would be nonsway values. Supply the sway Ncr2 and the other"
                    + " critical values explicitly, without CriticalValues.");
            if (!SwayPlane(1)) return false;
            if (!_parameters.ContainsKey("Ncr1.Value") && !snapshot.Context.EffectiveLength1.HasValue)
                throw new SteelPreparationRefusal("SteelMember.Sway.NonswayNcr1", "Plane 1 is declared sway, but CriticalValues would prepare Ncr1 from the in-plane"
                    + " restraint spans with both ends translation-fixed (nonsway). Supply the sway Ncr1 (Ncr1.Value/Unit/Source) or a sway"
                    + " MemberDesignContext.EffectiveLength1 with its LengthSource.");
            return true;
        }
    }
}
