using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Software coverage only. Never a verdict on an input, a certification,
    /// or permission to skip the applicability checks of the named calculation.</summary>
    public enum ConnectionCapabilityLevel { StandardStaticVerification, ComponentsOnly, NotImplemented }

    public sealed class ConnectionCapability
    {
        public string Id { get; }
        public ConnectionCapabilityLevel Level { get; }
        public IReadOnlyList<string> EntryPoints { get; }
        public string Domain { get; }
        public IReadOnlyList<string> ExternalRequirements { get; }
        public IReadOnlyList<string> MissingMechanisms { get; }
        public string Documentation { get; }
        internal ConnectionCapability(string id, ConnectionCapabilityLevel level, string[] entryPoints,
            string domain, string[] external, string[] missing, string documentation)
        {
            Id = id; Level = level; EntryPoints = Array.AsReadOnly(entryPoints.ToArray()); Domain = domain;
            ExternalRequirements = Array.AsReadOnly(external.ToArray()); MissingMechanisms = Array.AsReadOnly(missing.ToArray());
            Documentation = documentation;
        }
    }

    /// <summary>EC3:2005 / NTC2018 ordinary static connection coverage. This catalog is
    /// deliberately separate from runtime IsComplete/Passed and member stability.</summary>
    public static class ConnectionCapabilityCatalog
    {
        public const string Scope = "EC3:2005 / NTC2018; ordinary static loads. Member stability, fatigue, fire, seismic ductility, execution certification and arbitrary geometry are not implied.";
        public static IReadOnlyList<ConnectionCapability> All { get; } = Array.AsReadOnly(new[] {
            Item("RectangularDoubleCover", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "DoubleCoverSplice.Verify" }, "Symmetric rectangular plates and two identical covers; concentric tension only; round normal holes.",
                new[] { "Actual materials, hole/serviceability qualification and physical dimensions" },
                new[] { "Compression, bending and general beam/column joint" }, "README.txt"),
            Item("ShortFinPlate", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "FinPlate.Verify" }, "Short SCI detail: S275 100x10 plate, 2-5 M20 in one column, unnotched I/H beam on I/H column web; ordinary shear.",
                new[] { "Qualified global pinned model; no pre-existing support actions in this detail" },
                new[] { "Other support geometries, ordinary axial force, calculated rotation capacity" }, "PHASE_2B_SPEC.txt"),
            Item("FlexibleEndPlateShear", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "FlexibleEndPlate.Verify", "FlexibleEndPlate.VerifyPair" }, "Qualified partial end plate on column web; single or opposed beams; ordinary shear only.",
                new[] { "Physical SCI detail, weld/secondary tension evidence and global model qualifications required by the input" },
                new[] { "Ordinary N+V; general support interaction; this entry does not qualify the separate tying model" }, "PHASE_2D_SPEC.txt"),
            Item("WeldedIBeamDoubleCover", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "WeldedIBeamSpliceStandard.Verify", "BridgeSpliceApplication.CreateBolted" },
                "Aligned welded I beams; compact double covers on flanges/web; planar N,V,M; balanced faces or explicitly selected equal-thickness independent widths; A/B/C.",
                new[] { "Actual left/right section checks, longitudinal welds and global model evidence", "Actual required SLS/ULS cases and slip surface/preload evidence" },
                new[] { "Class4 perforated/slender plates, unequal face thickness, packing, general contact/prying", "Mixed-direction block tearing outside the sufficient hierarchy remains unsupported" }, "BRIDGE_SPLICE_APPLICATION_USAGE.txt"),
            Item("WeldedIBeamButt", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "WeldedIBeamButtSplice.Verify", "BridgeSpliceApplication.VerifyWelded" },
                "Identical aligned intact welded I sections; three full-penetration butt welds; planar N,V2,M1.",
                new[] { "Both actual section results; execution, penetration, inspection and material evidence" },
                new[] { "Misalignment, partial penetration and discontinuous sections" }, "PHASE_2E_WELDED_BUTT_SPLICE.txt"),
            Item("DoubleAngleCleat", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "DoubleAngleCleatComponents.Verify" }, "Qualified twin L90x90x10 cleats, bolts and beam web; separate ordinary and tying paths.",
                new[] { "Actual support and load case" }, new[] { "Complete support mechanism including out-of-plane bolt forces/punching; whole joint" }, "PHASE_2C_SPEC.txt"),
            Item("BraceConnections", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "GussetTwinFlatTension.Verify", "BraceAngleModelAdapter.FromPair" }, "Twin flat tension, angle/net-section and qualified 2L transfer submodels.",
                new[] { "Physical connected leg, force distribution and support model" },
                new[] { "General compressed brace joint, support introduction and complete local/global stability" }, "PHASE_3C_3_PAIR_BOLTED_IMPLEMENTATION.txt"),
            Item("PackedPairConnections", ConnectionCapabilityLevel.ComponentsOnly,
                new string[0], "Qualified face actions, welds and packing-block submodels of identical 2C/2L pairs.",
                new[] { "Concomitant local actions and actual connection geometry" },
                new[] { "Local 3D introduction into chord walls and complete packing station; member demand automation" }, "PHASE_3D_2_PACKING_STATION.txt"),
            Item("MomentEndPlate", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "EndPlateMomentWithBeamSection.Verify", "EndPlateLocalCaseAssessment.Assess", "EndPlateLocalCaseReport.Create" },
                "Rolled I/H beam and unstiffened column, extended plate or specific top-flush CJP detail; N=0 and declared tension side.",
                new[] { "Same-case beam, moment, bolts, welds and initial stiffness", "Column stress, beta, local transfer, contact and hardware evidence" },
                new[] { "End-plate/support net/block with out-of-plane bending and prying", "Complete column/member checks and global-model binding", "Whole-joint moment resistance, secant law and rotation qualification" }, "END_PLATE_STANDARD_CHECKPOINT.txt"),
            Item("GenericSpliceComponents", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "IBeamSpliceComponents.Verify", "WeldedIBeamSpliceEnvelope.Verify" }, "Generic force, bolt and plate component envelopes; distinct from WeldedIBeamSpliceStandard.",
                new[] { "Consistent physical geometry and concomitant load cases" },
                new[] { "Complete net/block/local mechanisms and whole-joint qualification outside the standard route" }, "PHASE_2E_ULTIMATE_ENVELOPE_IMPLEMENTATION.txt"),
            Item("StandardMomentEndPlate", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "EndPlateStandardJoint.Verify", "StandardConnectionCaseReport.Create" },
                "Single-sided unstiffened rolled I/H, two bolt columns, fixed tension side, N=0; actual column cuts/contact and same-case component method.",
                new[] { "Actual hardware/weld/contact execution; real column cuts and restraints; global spring compatibility and member stability" },
                new[] { "Other joint geometries, nonzero axial load, reversed tension side, available rotation capacity and generic mixed-mode fracture" }, "STANDARD_END_PLATE_USAGE.txt"),
            Item("StandardMomentEndPlateNMV", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "EndPlateNmvGeometry.Prepare", "EndPlateNmv.Verify", "EndPlateNmvStiffness.Evaluate", "StandardConnectionCaseReport.Create" },
                "Single-sided rolled I/H, class1-3 in uniform compression, all rows qualified as T-stubs, closed initial flange contacts; signed concomitant N-M1-V2 and actual column cuts. Supplementary elastic component equilibrium.",
                new[] { "Actual face/centroid references, hardware, full-strength welds on both flanges and contact execution", "Global stiffness needs a separately represented column panel and matching two-DOF beam-face ports; member stability remains external" },
                new[] { "Rows near the opposite flange outside the qualified yield-line geometry, Class4 and arbitrary3D joints", "Nonlinear N-M rotation law, available rotation capacity, fatigue and whole-joint experimental qualification" }, "END_PLATE_NMV_USAGE.txt"),
            Item("StandardBoltedPacking", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "SteelBoltedPacking.Verify", "SteelPackedPairAssessment.FromBoltedPacking" },
                "Identical closely spaced C/L; one through bolt per solid packing; longitudinal transfer from pin-ended centroidal compression model.",
                new[] { "Normal filled holes, hardened washers, actual layouts; complete terminal check; independently excluded global T/FT modes" },
                new[] { "Independent opening/prying, arbitrary six-action packing, general beam-column and lacing/battens" }, "../PACKED_BOLTED_STANDARD.txt"),
            Item("StandardWeldedPacking", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "SteelWeldedPacking.Verify", "SteelPackedPairAssessment.FromWeldedPacking" },
                "Identical unperforated hot-finished C/L, solid intermediate packing, two full external weld loops; computed longitudinal transfer and nominal elastic wall bounds.",
                new[] { "Exact flat unperforated weld footprints, physical execution, typed terminal check and global T/FT exclusions" },
                new[] { "General six-action welded packing, contact/prying, lacing/battens and arbitrary local 3D response" }, "PACKED_PAIR_STANDARD_WELDED_USAGE.txt"),
            Item("StandardWeldedPackedTerminal", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "SteelPackedPairTerminal.Verify", "StandardConnectionCaseReport.Create" },
                "Identical C/L in constant centred compression; two-face welded central gusset, full-penetration continuity to a coplanar collector, restrained root.",
                new[] { "Actual root restraint, aligned mid-surfaces, complete penetration/inspection, sound footprints and real overhang" },
                new[] { "T attachment to column flange, generic bolted terminals, arbitrary support geometry, member stability and fatigue" }, "PACKED_PAIR_STANDARD_WELDED_USAGE.txt"),
            Item("ColumnBases", ConnectionCapabilityLevel.NotImplemented, new string[0], "No new standard base-plate/anchor/foundation workflow in this module.",
                new string[0], new[] { "Standard base typology and its complete mechanisms" }, "PLAN_PHASES_1_4.txt"),
            Item("TubularJoints", ConnectionCapabilityLevel.NotImplemented, new string[0], "No new standard CHS/RHS node workflow in this module.",
                new string[0], new[] { "Tubular wall and joint mechanisms" }, "PLAN_PHASES_1_4.txt")
        });

        public static ConnectionCapability Get(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            return All.SingleOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal))
                ?? throw new ArgumentException("Unknown connection capability: " + id, nameof(id));
        }
        private static ConnectionCapability Item(string id, ConnectionCapabilityLevel level, string[] entryPoints,
            string domain, string[] external, string[] missing, string documentation)
            => new ConnectionCapability(id, level, entryPoints, domain, external, missing, documentation);
    }
}
