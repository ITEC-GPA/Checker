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
        /// <summary>Preferred start of the documented workflow, not necessarily its calculation step.
        /// Null when no API is provided. Coverage is not a verdict on a specific input.</summary>
        public string RecommendedEntryPoint { get; }
        public string Domain { get; }
        public IReadOnlyList<string> ExternalRequirements { get; }
        public IReadOnlyList<string> MissingMechanisms { get; }
        public string Documentation { get; }
        internal ConnectionCapability(string id, ConnectionCapabilityLevel level, string[] entryPoints,
            string domain, string[] external, string[] missing, string documentation, string recommendedEntryPoint = null)
        {
            Id = id; Level = level; EntryPoints = Array.AsReadOnly(entryPoints.ToArray()); Domain = domain;
            ExternalRequirements = Array.AsReadOnly(external.ToArray()); MissingMechanisms = Array.AsReadOnly(missing.ToArray());
            Documentation = documentation;
            RecommendedEntryPoint = recommendedEntryPoint ?? entryPoints.FirstOrDefault();
            if (RecommendedEntryPoint != null && !EntryPoints.Contains(RecommendedEntryPoint))
                throw new ArgumentException("Recommended entry point must be listed among the supported APIs.");
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
                "Aligned welded I beams; double covers on flanges/web; compact covers, explicit SCI StandardBoltedCovers with regular locally class1-3 strips/outstands (including one web column per side), or EffectiveCompression for locally class4 compression at exactly zero cover shear, or opt-in EffectiveCompressionWithLowShear bounded by half resistance and whole-width shear-buckling exclusion; planar N,V,M; balanced faces, equal-thickness independent widths, or explicit IndependentFaceDimensions with actual face offsets, or PackedIndependentFaceDimensions with explicit parallel full-footprint packing layers and serial friction interfaces; A/B/C.",
                new[] { "Actual left/right section checks, longitudinal welds and global model evidence", "Actual required SLS/ULS cases and slip surface/preload evidence" },
                new[] { "Locally class4 compression beyond the bounded low-shear domain, plates outside the selected plate-stability domain, section transitions, tapered or discontinuous packings, general contact/prying", "Mixed-direction block tearing outside the sufficient hierarchy remains unsupported" }, "BRIDGE_SPLICE_APPLICATION_USAGE.txt", "BridgeSpliceApplication.CreateBolted"),
            Item("WeldedIBeamButt", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "WeldedIBeamButtSplice.Verify", "BridgeSpliceApplication.VerifyWelded" },
                "Identical aligned intact welded I sections; three full-penetration butt welds; planar N,V2,M1.",
                new[] { "Both actual section results; execution, penetration, inspection and material evidence" },
                new[] { "Misalignment, partial penetration and discontinuous sections" }, "PHASE_2E_WELDED_BUTT_SPLICE.txt", "BridgeSpliceApplication.VerifyWelded"),
            Item("DoubleAngleCleat", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "DoubleAngleCleatComponents.Verify", "DoubleAngleCleatSupportContact.Verify", "DoubleAngleCleatColumnWeb.Verify" },
                "Twin L90x90x10 cleat components; separate ordinary and tying paths. Supplementary column-web bearing, strip shear and concurrent net-section checks with actual column-cut equilibrium.",
                new[] { "Actual support, same-case physical column cuts, cut-face conventions and execution detail" },
                new[] { "Support out-of-plane response, actual bolt tension/prying/punching, rotation and whole joint", "Concurrent strong-axis shear through the perforated column web and column torsion" }, "DOUBLE_ANGLE_COLUMN_WEB.txt"),
            Item("BraceConnections", ConnectionCapabilityLevel.ComponentsOnly,
                new[] { "GussetTwinFlatTension.Verify", "BraceAngleModelAdapter.FromPair", "BraceAnglePairTerminalPlates.Verify" }, "Twin flat tension, angle/net-section and qualified 2L transfer submodels.",
                new[] { "Physical connected leg, force distribution and support model" },
                new[] { "General compressed brace joint, support introduction and complete local/global stability" }, "PHASE_3C_3_PAIR_BOLTED_IMPLEMENTATION.txt"),
            Item("TwinFlatCoplanarCollector", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "GussetTwinFlatCollector.Verify" }, "Concentric twin-flat tension terminal, central gusset and matching constant-section coplanar full-penetration collector.",
                new[] { "Actual concomitant collector cut, matching geometry, weld execution and material evidence" },
                new[] { "General beam/column T attachment, compression, width/thickness transitions, member stability" }, "CONNECTIONS_PHASES_4_8.txt"),
            Item("AnglePairCoplanarCollector", ConnectionCapabilityLevel.StandardStaticVerification,
                new[] { "BraceAnglePairStandardTension.Verify", "BraceAnglePairBlockHierarchy.Verify" },
                "Conventional symmetric2L category-A tension terminal; actual eccentric in-plane forces; direct blocks or sufficient hierarchy; matching coplanar CJP collector.",
                new[] { "Same-case Model geometry/actions, connected leg, collector cut and CJP execution evidence" },
                new[] { "Explicit contact/prying response, C/2C web-only attachment, T supports, compression, whole-member stability" }, "BRACE_2L_STANDARD_TENSION.txt"),
            Item("PackedPairConnections", ConnectionCapabilityLevel.ComponentsOnly,
                new string[0], "Generic face-action and packing-block submodels of identical 2C/2L pairs. The separately listed StandardBoltedPacking, StandardWeldedPacking and StandardWeldedPackedTerminal provide the bounded complete workflows.",
                new[] { "Concomitant local actions and actual connection geometry; use the dedicated standard entry points for automated compression demand" },
                new[] { "Generic six-action 3D transfer outside the standard compression workflows" }, "PHASE_3D_2_PACKING_STATION.txt"),
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
                new[] { "StandardEndPlate.Verify", "EndPlateNmvGeometry.Prepare", "EndPlateNmvGeometry.PrepareSingleRow", "EndPlateNmv.Verify", "EndPlateNmvStiffness.Evaluate", "StandardConnectionCaseReport.Create" },
                "Single-sided rolled I/H, class1-3 in uniform compression, all rows qualified as T-stubs, closed initial flange contacts; signed concomitant N-M1-V2 and actual column cuts. Supplementary elastic component equilibrium.",
                new[] { "Actual face/centroid references, hardware, full-strength welds on both flanges and contact execution", "Global stiffness needs a separately represented column panel and matching two-DOF beam-face ports; member stability remains external" },
                new[] { "Multiple rows in the opposite flange influence zone or outside the qualified SCI geometry, stiffened/bilateral columns, Class4 and arbitrary3D joints", "Nonlinear N-M rotation law, available rotation capacity, fatigue and whole-joint experimental qualification" }, "END_PLATE_NMV_USAGE.txt"),
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
            string domain, string[] external, string[] missing, string documentation, string recommendedEntryPoint = null)
            => new ConnectionCapability(id, level, entryPoints, domain, external, missing, documentation, recommendedEntryPoint);
    }
}
