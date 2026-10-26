using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Immutable numeric local-connection report. Values carry units; source
    /// references, runtime status and external conditions are distinct. It does not
    /// upgrade a connection result to a member/frame verification or export a CAD model.</summary>
    public sealed class StandardConnectionCaseReport
    {
        public string MethodId { get; }
        public string Code { get; }
        public string Scope { get; }
        public ConnectionStatus Status { get; }
        public bool IsComplete { get; }
        public bool CanQualifyWholeMember=>false;
        public IReadOnlyList<SectionResistanceValue> Inputs { get; }
        public IReadOnlyList<SectionResistanceValue> Results { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> ExternalConditions { get; }
        public IReadOnlyList<string> Sources { get; }
        private StandardConnectionCaseReport(string id,ConnectionDesignCode code,string scope,ConnectionStatus status,bool complete,
            IEnumerable<SectionResistanceValue> inputs,IEnumerable<SectionResistanceValue> results,IEnumerable<ConnectionCheck> checks,
            IEnumerable<string> diagnostics,IEnumerable<string> external,IEnumerable<string> sources)
        {
            MethodId=id;Code=code.IsNtc?"NTC2018; supplementary EC3:2005 component models":"EC3:2005; "+code.Plates.ParameterSet;
            Scope=scope;Status=status;IsComplete=complete;Inputs=Array.AsReadOnly(inputs.ToArray());Results=Array.AsReadOnly(results.ToArray());
            Checks=Array.AsReadOnly(checks.ToArray());Diagnostics=Array.AsReadOnly(diagnostics.ToArray());ExternalConditions=Array.AsReadOnly(external.ToArray());Sources=Array.AsReadOnly(sources.ToArray());
        }
        public static StandardConnectionCaseReport Create(EndPlateStandardJointResult r)
        {
            if(r==null)throw new ArgumentNullException(nameof(r));
            var local=r.LocalCase;var m=local.BeamAndMoment.Input;var code=m.Policy.Code;var a=local.Actions;var col=r.ColumnCase.Input;
            var input=Factors(code);input.AddRange(new[]{V("N",a.N,"N"),V("V2",a.V2,"N"),V("M1",a.M1,"Nmm"),
                V("Beam.Height",m.Beam.Height,"mm"),V("Beam.Width",m.Beam.Width,"mm"),V("Beam.WebThickness",m.Beam.WebThickness,"mm"),V("Beam.FlangeThickness",m.Beam.FlangeThickness,"mm"),
                V("Column.Height",m.Column.Height,"mm"),V("Column.Width",m.Column.Width,"mm"),V("Column.WebThickness",m.Column.WebThickness,"mm"),V("Column.FlangeThickness",m.Column.FlangeThickness,"mm"),
                V("Plate.Width",m.EndPlate.Width,"mm"),V("Plate.Thickness",m.EndPlate.Thickness,"mm"),V("Bolt.Diameter",m.Bolt.Diameter,"mm"),V("Bolt.HoleDiameter",m.Bolt.HoleDiameter,"mm"),
                V("Contact.Width",col.ContactWidth,"mm"),V("Contact.Height",col.ContactHeight,"mm"),V("Column.UpperN",col.Upper.N,"N"),V("Column.LowerN",col.Lower.N,"N"),
                V("Column.UpperM1",col.Upper.M1,"Nmm"),V("Column.LowerM1",col.Lower.M1,"Nmm"),V("Column.UpperV2",col.Upper.V2,"N"),V("Column.LowerV2",col.Lower.V2,"N")});
            var values=new List<SectionResistanceValue>();Add("Joint.MomentResistanceAtRetainedShear",r.MomentResistance,"Nmm");
            Add("Joint.InitialStiffness",r.InitialStiffness,"Nmm/rad");Add("Joint.SecantStiffness",r.SecantStiffness,"Nmm/rad");Add("Joint.ModelRotationNotCapacity",r.ModelRotation,"rad");
            Add("Global.AppliedSpring",r.AppliedGlobalSpring,"Nmm/rad");if(r.GlobalSpringMatches.HasValue)values.Add(V("Global.SpringMatches",r.GlobalSpringMatches.Value?1:0,"1"));
            return new StandardConnectionCaseReport("Steel.Connection.EndPlate.Standard",code,"Same-case standard local I/H end-plate joint, single sided N=0. No member stability or rotation capacity.",
                r.Status,r.IsComplete,input,values,r.Checks,r.Diagnostics,r.ExternalConditions,
                new[]{m.ActionCombinationSource,m.ColumnStressSource,col.CombinationSource,col.ContactExecutionSource,"EN1993-1-8:2005 6.2.2/6.2.7.2; SCI P3982.5; STANDARD_NEXT_PLAN.txt"});
            void Add(string id,double? value,string unit){if(value.HasValue)values.Add(V(id,value.Value,unit));}
        }
        public static StandardConnectionCaseReport Create(SteelPackedPairAssessment p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            if(p.ComputedTerminal==null||(p.ComputedPacking==null&&p.ComputedWeldedPacking==null))
                throw new ArgumentException("This report requires typed calculated packing AND terminal results; legacy scalar assessments remain separate.");
            var terminal=p.ComputedTerminal;var code=terminal.Detail.Lap.Code;var model=terminal.Demand;
            bool welded=p.ComputedWeldedPacking!=null;
            var checks=terminal.Checks.Select(c=>new ConnectionCheck("Terminal."+c.Id,c.Demand,c.Resistance,c.Reference,c.Unit))
                .Concat((welded?p.ComputedWeldedPacking.Checks:p.ComputedPacking.Checks).Select(c=>new ConnectionCheck("Packing."+c.Id,c.Demand,c.Resistance,c.Reference,c.Unit))).ToArray();
            var issues=terminal.Diagnostics.Concat(welded?p.ComputedWeldedPacking.Diagnostics:p.ComputedPacking.Diagnostics).ToArray();
            bool complete=terminal.IsComplete&&(welded?p.ComputedWeldedPacking.IsComplete:p.ComputedPacking.IsComplete);
            var status=checks.Any(x=>!x.Passed)?ConnectionStatus.Failed:complete?ConnectionStatus.Passed:ConnectionStatus.NotSupported;
            var input=Factors(code);input.AddRange(new[]{V("Member.N",p.Input.Actions.N,"N"),V("Member.Length",p.Input.Member.Length,"mm"),V("Member.MaximumPitch",p.MaximumPitch,"mm"),
                V("Pair.Gap",p.PackingThickness,"mm"),V("Terminal.LapWidth",terminal.Detail.Lap.Width,"mm"),V("Terminal.LapLength",terminal.Detail.Lap.Length,"mm"),
                V("Terminal.Throat",terminal.Detail.Lap.Throat,"mm"),V("Terminal.FreeLength",terminal.Detail.FreeLength,"mm"),V("Terminal.ChordOverhang",terminal.Detail.ChordOverhang,"mm")});
            var values=new List<SectionResistanceValue>(model.Trace);
            values.Add(V("Terminal.ForcePerChord",terminal.ForcePerChord,"N"));
            if(terminal.ForceResistance.HasValue)values.Add(V("Terminal.ConditionalLocalForceResistance",terminal.ForceResistance.Value,"N"));
            values.Add(V("Packing.MaximumForcePerFace",welded?p.ComputedWeldedPacking.MaximumConnectionDemand:p.ComputedPacking.MaximumConnectionDemand,"N"));
            return new StandardConnectionCaseReport(welded?"Steel.Connection.PackedPair.Welded":"Steel.Connection.PackedPair.BoltedPackingWeldedTerminal",code,
                "Local standard intermediate packings and welded terminals to a coplanar collector. Member values describe demand; this report is not a member stability result.",
                status,complete,input,values,checks,issues,new[]{
                    "Global member compression and chord checks must use the same SteelPackedPairAssessment in the member pipeline; global T/FT modes require separate exclusion.",
                    "Root fixity, unperforated weld footprints, execution and coplanar full-penetration collector attachment must match the supplied detail.",
                    "Conditional local force resistance is not a global critical load. No generic six-action packing, arbitrary brace joint, fatigue or serviceability slip qualification."
                },new[]{p.DetailingSource,p.LocalAnalysisSource,terminal.Detail.SupportAndWeldSource,"STANDARD_NEXT_PLAN.txt; PACKED_PAIR_STANDARD_WELDED_USAGE.txt"});
        }
        private static List<SectionResistanceValue> Factors(ConnectionDesignCode c)=>new List<SectionResistanceValue>{
            V("GammaM0",c.Plates.GammaM0,"1"),V("GammaM2Plate",c.Plates.GammaM2,"1"),V("GammaM2Connection",c.GammaM2Connection,"1")};
        private static SectionResistanceValue V(string id,double value,string unit)=>new SectionResistanceValue(id,value,unit);
    }
}
