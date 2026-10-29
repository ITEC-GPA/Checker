using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Standard twin L90x90x10 S275 web cleats (SBE-MS5 chapter4 detail, M20 8.8)
    /// connecting one unnotched restrained rolled I/H beam to the WEB of one unstiffened
    /// rolled I/H column, ordinary downward shear only. Column axes and cuts as in the
    /// column-web components: X normal to the web towards the beam, Y up, Z completing the
    /// frame; both cuts with the same +Y face convention at actual column-centroid points.
    /// MeanHeadNutDiameter is dm of EN1993-1-8 Table3.4 for the actual hardware.
    /// NominallyPinnedModelSource records that the global analysis treats the joint as a pin
    /// (beam simply supported, column with the nominal eccentric reaction); TyingSituationSource
    /// records how the separate accidental tying requirement is met. Neither is certified here.</summary>
    public sealed class DoubleAngleCleatColumnWebStandardInput
    {
        public DoubleAngleCleatComponentInput Cleats { get; }
        public ConnectionRolledISection Column { get; }
        public double ColumnFlangeFy { get; }
        public double ColumnFlangeFu { get; }
        public double TopBoltEnd { get; }
        public double BottomBoltEnd { get; }
        public PlanarConnectionActions UpperCut { get; }
        public PlanarConnectionActions LowerCut { get; }
        public double MeanHeadNutDiameter { get; }
        public string DetailSource { get; }
        public string SameCaseCutSource { get; }
        public string NominallyPinnedModelSource { get; }
        public string TyingSituationSource { get; }
        public DoubleAngleCleatColumnWebStandardInput(DoubleAngleCleatComponentInput cleats, ConnectionRolledISection column,
            double columnFlangeFy, double columnFlangeFu, double topBoltEnd, double bottomBoltEnd,
            PlanarConnectionActions upperCut, PlanarConnectionActions lowerCut, double meanHeadNutDiameter,
            string detailSource, string sameCaseCutSource, string nominallyPinnedModelSource, string tyingSituationSource)
        {
            Cleats = cleats ?? throw new ArgumentNullException(nameof(cleats));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            UpperCut = upperCut ?? throw new ArgumentNullException(nameof(upperCut));
            LowerCut = lowerCut ?? throw new ArgumentNullException(nameof(lowerCut));
            ConnectionValidation.Positive(columnFlangeFy, nameof(columnFlangeFy));
            ConnectionValidation.Positive(columnFlangeFu, nameof(columnFlangeFu));
            ConnectionValidation.Positive(topBoltEnd, nameof(topBoltEnd));
            ConnectionValidation.Positive(bottomBoltEnd, nameof(bottomBoltEnd));
            ConnectionValidation.Positive(meanHeadNutDiameter, nameof(meanHeadNutDiameter));
            if (columnFlangeFu < columnFlangeFy) throw new ArgumentException("fu must not be below fy.");
            ColumnFlangeFy = columnFlangeFy; ColumnFlangeFu = columnFlangeFu;
            TopBoltEnd = topBoltEnd; BottomBoltEnd = bottomBoltEnd; MeanHeadNutDiameter = meanHeadNutDiameter;
            DetailSource = detailSource; SameCaseCutSource = sameCaseCutSource;
            NominallyPinnedModelSource = nominallyPinnedModelSource; TyingSituationSource = tyingSituationSource;
        }
    }

    /// <summary>Complete ordinary-shear procedure for the declared detail. A failed load or
    /// detailing check is a failure; an unestablished prerequisite (missing source, outside
    /// the components' field, punching hierarchy) is a missing requirement, never a pass.</summary>
    public sealed class DoubleAngleCleatColumnWebStandardResult
    {
        public DoubleAngleCleatColumnWebStandardInput Input { get; }
        public DoubleAngleCleatComponentResult Cleats { get; }
        /// <summary>Column-web bearing, SCI P358 Check10 strips and concurrent net section.</summary>
        public IReadOnlyList<ConnectionCheck> SupportChecks { get; }
        /// <summary>Procedure detailing rules (nominal-pin qualification) and the EN1993-1-8
        /// Table3.2/3.4 punching hierarchy; the hierarchy has the Applicability role.</summary>
        public IReadOnlyList<ConnectionCheck> ProcedureChecks { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public bool IsStaticResistanceComplete => MissingRequirements.Count == 0;
        public bool HasFailedComponent { get; }
        public ConnectionStatus Status => HasFailedComponent ? ConnectionStatus.Failed
            : IsStaticResistanceComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => IsStaticResistanceComplete;
        /// <summary>Largest load utilization; detailing and applicability ratios are excluded.</summary>
        public double? Utilization => IsStaticResistanceComplete
            ? (double?)ConnectionOutcome.LoadChecks(Checks).Max(c => c.Utilization) : null;
        public string Scope => "Ordinary static vertical shear of the SBE-MS5 chapter4 standard twin L90x90x10 S275 web cleats with M20 8.8 bolts, "
            + "unnotched restrained rolled I/H beam on the web of one unstiffened rolled I/H column, one beam per web face. "
            + "Checks: SBE-MS5 4.1 details and 4.2.1-4.2.3, SCI P358 Check10 on the column web, concurrent column net section, "
            + "EN1993-1-8 Table3.2/3.4 punching hierarchy, SBE-MS5 1.2 nominal-pin qualification. Accidental tying, column member stability and frame analysis are separate.";
        public IReadOnlyList<string> ExternalConditions { get; } = Array.AsReadOnly(new[] {
            "The global model treats the joint as nominally pinned; the column member is designed for its axial force and the nominal moment of the reaction at the web face (tw/2).",
            "Accidental tying (EN1991-1-7 Annex A) is a separate design situation: SBE-MS5 4.3 and the column-web yield-line resistance are not part of this ordinary result.",
            "Bolt tension and prying induced by the nominal moment are covered by the procedure's 0.8 (support bolts) and 1.27 (cleat shear) factors, not computed; the punching hierarchy keeps that treatment valid.",
            "Execution: standard hole clearance, fully threaded M20 8.8, cleats positioned 50 mm below the beam top, 10 mm end projection." });
        internal DoubleAngleCleatColumnWebStandardResult(DoubleAngleCleatColumnWebStandardInput input, DoubleAngleCleatComponentResult cleats,
            IEnumerable<ConnectionCheck> support, IEnumerable<ConnectionCheck> procedure, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<string> missing, bool failed)
        {
            Input = input; Cleats = cleats; SupportChecks = Array.AsReadOnly(support.ToArray());
            ProcedureChecks = Array.AsReadOnly(procedure.ToArray());
            Checks = Array.AsReadOnly(cleats.ComponentAssessment.Checks.Select(c => c.WithId("Cleats." + c.Id))
                .Concat(SupportChecks.Select(c => c.WithId("Support." + c.Id))).Concat(ProcedureChecks.Select(c => c.WithId("Procedure." + c.Id))).ToArray());
            Trace = Array.AsReadOnly(trace.ToArray()); MissingRequirements = Array.AsReadOnly(missing.Distinct().ToArray()); HasFailedComponent = failed;
        }
    }

    /// <summary>Composition of the existing SBE-MS5 cleat components and column-web
    /// components with the remaining items of the procedure for the ordinary situation.
    /// The component calculations are reused unchanged.</summary>
    public static class DoubleAngleCleatColumnWebStandard
    {
        public const string MethodId = "Steel.Connection.DoubleAngleCleat.ColumnWebStandard";
        public static DoubleAngleCleatColumnWebStandardResult Verify(DoubleAngleCleatColumnWebStandardInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var c = p.Cleats; var code = c.Code; var bolt = c.Bolt;
            var web = DoubleAngleCleatColumnWeb.Verify(new DoubleAngleCleatColumnWebInput(c, p.Column, p.ColumnFlangeFy, p.ColumnFlangeFu,
                p.TopBoltEnd, p.BottomBoltEnd, p.UpperCut, p.LowerCut, p.DetailSource, p.SameCaseCutSource));
            var cleats = web.Cleats; var missing = new List<string>(); var procedure = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>();
            foreach (var v in cleats.ComponentAssessment.Trace) trace.Add(new SectionResistanceValue("Cleats." + v.Name, v.Value, v.Unit));
            foreach (var v in web.Support.Trace) trace.Add(new SectionResistanceValue("Support." + v.Name, v.Value, v.Unit));
            foreach (var d in cleats.ComponentAssessment.Diagnostics) missing.Add("Cleats:" + d);
            if (!cleats.ComponentAssessment.IsComplete) missing.Add("Cleats:INCOMPLETE_COMPONENT");
            foreach (var d in web.Support.Diagnostics) missing.Add("Support:" + d);
            if (!web.Support.IsComplete) missing.Add("Support:INCOMPLETE_COMPONENT");
            if (string.IsNullOrWhiteSpace(p.NominallyPinnedModelSource)) missing.Add("NOMINALLY_PINNED_GLOBAL_MODEL_SOURCE_REQUIRED");
            if (string.IsNullOrWhiteSpace(p.TyingSituationSource)) missing.Add("TYING_SITUATION_SOURCE_REQUIRED");
            bool failed = cleats.ComponentAssessment.Status == ConnectionStatus.Failed || web.Support.Status == ConnectionStatus.Failed;

            // SBE-MS5 1.2/4.1 and SCI P358 Check1: the nominal-pin behaviour (rotation capacity
            // and ductility) is qualified by the recommended detail itself, not by a computed
            // rotation. The fixed standard dimensions are shown as explicit detailing checks.
            const string pinRule = "SBE-MS5 1.2 (8/10 mm S275 angles, M20 8.8 behave as nominal pins) and 4.1 recommended details";
            Detail("NominalPin.CleatThickness", c.CleatLeg.Thickness, 10, pinRule, "mm");
            Detail("NominalPin.CleatYieldStrength", c.CleatLeg.Fy, 275, pinRule, "MPa");
            Detail("NominalPin.BoltDiameter", bolt.Diameter, 20, pinRule + "; bolt class 8.8", "mm");
            Detail("NominalPin.EndProjection", c.Gap, 10, "SBE-MS5 4.1 end projection gh approximately 10 mm; P358 Check1 10 mm clearance", "mm");
            Detail("Restraint.CleatTopBelowBeamTop", c.TopOffset, 50, "SCI P358 Check1 note: positioned close to the top flange (50 mm) for positional restraint", "mm");
            // hac>=0.6hb (torsional restraint) is the cleat component's Detail.MinimumHeight.
            // EN1993-1-8 Table3.2/3.4 (NTC 4.2.71): any bolt tension, including the one the
            // procedure's 0.8 factor allows for, must also satisfy Ft,Ed<=Bp,Rd. If the head/nut
            // punching resistance of each ply is not below Ft,Rd, punching cannot govern before the
            // bolt and needs no separate tension value. Otherwise the procedure is not sufficient.
            double ft = BoltResistance.Tension(bolt.Fu, bolt.TensileArea, code.GammaM2Connection);
            trace.Add(new SectionResistanceValue("Procedure.Bolt.TensionResistance", ft, "N"));
            trace.Add(new SectionResistanceValue("Procedure.MeanHeadNutDiameter", p.MeanHeadNutDiameter, "mm"));
            if (p.MeanHeadNutDiameter <= bolt.HoleDiameter) missing.Add("HEAD_NUT_DIAMETER_MUST_COVER_HOLE");
            else
            {
                Hierarchy("CleatLeg", c.CleatLeg);
                Hierarchy("ColumnWeb", new ConnectionPlate(p.Column.ClearWeb, p.Column.WebThickness, p.Column.Fy, p.Column.Fu));
            }
            return new DoubleAngleCleatColumnWebStandardResult(p, cleats, web.Support.Checks, procedure, trace, missing, failed);

            void Detail(string id, double demand, double limit, string reference, string unit)
            {
                var check = new ConnectionCheck(id, demand, limit, reference, unit, ConnectionCheckRole.Detailing);
                procedure.Add(check); if (!check.Passed) failed = true;
            }
            void Hierarchy(string ply, ConnectionPlate plate)
            {
                var punching = BoltPunching.Verify(new BoltPunchingInput(code, plate, ft, bolt.HoleDiameter, p.MeanHeadNutDiameter));
                foreach (var d in punching.Diagnostics) missing.Add("PunchingHierarchy." + ply + ":" + d);
                foreach (var r in punching.Checks)
                {
                    var check = new ConnectionCheck("PunchingHierarchy." + ply, r.Demand, r.Resistance,
                        r.Reference + "; Table3.2: Ft,Rd<=Bp,Rd so that punching cannot precede bolt tension", "N", ConnectionCheckRole.Applicability);
                    procedure.Add(check);
                    trace.Add(new SectionResistanceValue("Procedure." + ply + ".PunchingResistance", r.Resistance, "N"));
                    if (!check.Passed) missing.Add("PUNCHING_MAY_PRECEDE_BOLT_TENSION_IN_" + ply.ToUpperInvariant() + ":ACTUAL_TENSION_AND_PRYING_MODEL_REQUIRED");
                }
            }
        }
    }
}
