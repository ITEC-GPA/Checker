using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Members
{
    /// <summary>Independently established design resistances, N. Compression includes
    /// all relevant flexural/torsional modes and lengths; tension includes gross/net
    /// section and connection eccentricity. Source is recorded, never inferred.</summary>
    public sealed class AxialComponentResistances
    {
        public double Compression { get; }
        public double Tension { get; }
        public string Source { get; }
        public AxialComponentResistances(double compression,double tension,string source)
        {
            Ec3AngleCompression.Positive(compression,nameof(compression));
            Ec3AngleCompression.Positive(tension,nameof(tension));
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Resistance provenance is required.",nameof(source));
            Compression=compression;Tension=tension;Source=source;
        }
    }

    public sealed class BuiltUpComponentCheck
    {
        public string Component { get; }
        public string Mechanism { get; }
        public double Demand { get; }
        public double Resistance { get; }
        public double Utilization { get; }
        public string ResistanceSource { get; }
        public bool Passed => Utilization<=1;
        internal BuiltUpComponentCheck(string component,string mechanism,double demand,double resistance,string source)
        {
            Component=component;Mechanism=mechanism;Demand=demand;Resistance=resistance;
            Utilization=Ec3BuiltUpAxialChecks.CompressionUtilization(demand,resistance);ResistanceSource=source;
        }
    }

    public static class BuiltUpComponentChecks
    {
        /// <summary>6.4.2.1 axial checks for chords and web members. Component
        /// successes are NOT a whole-column pass; use response.RequiredChecks to
        /// retain the checks of joints, restraints, end panels and load introduction.
        /// The chord compression resistance must govern all local/global modes.</summary>
        public static IReadOnlyList<BuiltUpComponentCheck> Laced(BuiltUpColumnResponse response,
            AxialComponentResistances chord,AxialComponentResistances diagonal,AxialComponentResistances post=null)
        {
            Evaluated(response);
            if(response.Column.IsBattened)throw new NotSupportedException("Battened chords carry local moments: an axial-only result is insufficient.");
            if(chord==null)throw new ArgumentNullException(nameof(chord));
            if(diagonal==null)throw new ArgumentNullException(nameof(diagonal));
            bool needsPost=response.Column.Lacing!=LacingSystem.ZigZag;
            if(needsPost&&post==null)throw new ArgumentNullException(nameof(post));
            if(!needsPost&&post!=null)throw new ArgumentException("This lacing has no posts.",nameof(post));
            var checks=new List<BuiltUpComponentCheck>();
            Add(checks,"Chord",response.ChordCompression.Value,Math.Max(0,-response.OtherChordForce.Value),chord);
            Add(checks,"Diagonal",response.DiagonalForce.Value,response.DiagonalForce.Value,diagonal);
            if(needsPost)Add(checks,"Post",response.PostForce.Value,response.PostForce.Value,post);
            return checks.AsReadOnly();
        }

        /// <summary>Checks the batten section under the calculated M/V envelope.
        /// bendingResistanceAtActualShear MUST already include M-V interaction and
        /// local/member stability as applicable. Separate joint checks remain required.</summary>
        public static IReadOnlyList<BuiltUpComponentCheck> Batten(BuiltUpColumnResponse response,
            double bendingResistanceAtActualShear,double shearResistance,string source)
        {
            Evaluated(response);
            if(!response.Column.IsBattened)throw new NotSupportedException("The column is not battened.");
            Ec3AngleCompression.Positive(bendingResistanceAtActualShear,nameof(bendingResistanceAtActualShear));
            Ec3AngleCompression.Positive(shearResistance,nameof(shearResistance));
            if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Resistance provenance is required.",nameof(source));
            return Array.AsReadOnly(new[]{
                new BuiltUpComponentCheck("Batten","BendingAtActualShear",response.Batten.Moment,bendingResistanceAtActualShear,source),
                new BuiltUpComponentCheck("Batten","Shear",response.Batten.Shear,shearResistance,source)});
        }

        private static void Add(List<BuiltUpComponentCheck> checks,string name,double compression,double tension,AxialComponentResistances capacity)
        {
            checks.Add(new BuiltUpComponentCheck(name,"Compression",compression,capacity.Compression,capacity.Source));
            checks.Add(new BuiltUpComponentCheck(name,"Tension",tension,capacity.Tension,capacity.Source));
        }
        private static void Evaluated(BuiltUpColumnResponse response)
        {
            if(response==null)throw new ArgumentNullException(nameof(response));
            if(response.Status!=BuiltUpAnalysisStatus.Evaluated)throw new InvalidOperationException("The column is unstable; component checks cannot certify it.");
        }
    }
}
