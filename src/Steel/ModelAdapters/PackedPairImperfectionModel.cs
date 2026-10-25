using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    public sealed class PackedStationShear
    {
        public int Index { get; }
        public double Station { get; }
        public double TributaryStart { get; }
        public double TributaryEnd { get; }
        public double LongitudinalShear { get; }
        internal PackedStationShear(int index,double x,double start,double end,double force)
        {Index=index;Station=x;TributaryStart=start;TributaryEnd=end;LongitudinalShear=force;}
    }

    /// <summary>Analytical second-order demand for a pin-ended, prismatic packed pair
    /// under constant centroidal compression. Bow e0 sin(pi*x/L); e0=L/150 is the
    /// conservative elastic Table5.1 curve-d imperfection. EN1993-1-1 6.4.4 permits
    /// integral gross stiffness only after its packing conditions are verified.
    /// This is a force model, not a connection resistance or a torsional-mode solver.</summary>
    public sealed class PackedPairImperfectionModel
    {
        public SteelPackingLayout Layout { get; }
        public double InitialBow { get; }
        public double CriticalAssemblyPlane { get; }
        public double CriticalOtherPlane { get; }
        public double MomentAssemblyPlane { get; }
        public double MomentOtherPlane { get; }
        public double ChordCompression { get; }
        public double ChordTension { get; }
        public double ChordAssemblyMoment { get; }
        public double ChordOtherMoment { get; }
        public double ShearFlowPeak { get; }
        public IReadOnlyList<PackedStationShear> Stations { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        private PackedPairImperfectionModel(SteelPackingLayout layout)
        {
            Layout=layout??throw new ArgumentNullException(nameof(layout));var input=layout.Input;
            var a=input.Actions;var member=input.Member;
            if(a.N>=0||a.M1!=0||a.M2!=0||a.V1!=0||a.V2!=0||a.T!=0)
                throw new NotSupportedException("Constant centroidal compression only for the analytical packing model.");
            double length=member.Length;
            if(member.BucklingLength1!=length||member.BucklingLength2!=length)
                throw new NotSupportedException("Explicit pin-ended full-length flexural scheme in both planes required.");
            foreach(var end in new[]{member.StartRestraints,member.EndRestraints})
                if(end!=null&&(end.Translation1==SteelRestraintState.Free||end.Translation2==SteelRestraintState.Free
                    ||end.Rotation1==SteelRestraintState.Fixed||end.Rotation2==SteelRestraintState.Fixed))
                    throw new ArgumentException("End restraints contradict the pin-ended packing demand model.");
            var imperfection=member.Imperfections;
            if(imperfection!=null&&(imperfection.Sway1.GetValueOrDefault()!=0||imperfection.Sway2.GetValueOrDefault()!=0||imperfection.IncludedInAnalysis))
                throw new NotSupportedException("Use first-order centroidal N; frame sway and already included second-order effects are not reinterpreted.");
            var property=input.Section.CreateSectionCopy();
            if(!(property.SectionShape is SectionBuiltUp shape))throw new NotSupportedException("Identical packed C/L pair required.");
            var g=ModelBuiltUpGeometry.Create(shape);var chord=shape.Parts[0].Section;
            bool horizontal=shape.Parts[0].MirrorX!=shape.Parts[1].MirrorX;
            double otherI=horizontal?chord.Jxx:chord.Jyy;
            double assembledI=2*g.InPlaneInertia+g.Area*g.CentroidSpacing*g.CentroidSpacing/2;
            double young=input.Section.ElasticModulus,n=-a.N;
            InitialBow=Math.Max(length/150,Math.Max(Math.Abs(imperfection?.SinusoidalBow1??0),Math.Abs(imperfection?.SinusoidalBow2??0)));
            CriticalAssemblyPlane=Math.PI*Math.PI*young*assembledI/(length*length);
            CriticalOtherPlane=Math.PI*Math.PI*young*2*otherI/(length*length);
            if(n>=Math.Min(CriticalAssemblyPlane,CriticalOtherPlane))
                throw new NotSupportedException("No stable flexural second-order equilibrium at or above Euler load.");
            MomentAssemblyPlane=n*InitialBow/(1-n/CriticalAssemblyPlane);
            MomentOtherPlane=n*InitialBow/(1-n/CriticalOtherPlane);
            double factor=g.Area*g.CentroidSpacing/(2*assembledI);
            double delta=MomentAssemblyPlane*factor;
            ChordCompression=n/2+delta;ChordTension=Math.Max(0,delta-n/2);
            ChordAssemblyMoment=MomentAssemblyPlane*g.InPlaneInertia/assembledI;
            ChordOtherMoment=MomentOtherPlane/2;
            ShearFlowPeak=factor*MomentAssemblyPlane*Math.PI/length;
            var stations=new List<PackedStationShear>();
            for(int j=0;j<layout.Stations.Count;j++)
            {
                double x=layout.Stations[j],start=j==0?0:(layout.Stations[j-1]+x)/2;
                double end=j==layout.Stations.Count-1?length:(x+layout.Stations[j+1])/2;
                double s0=Math.Sin(Math.PI*start/length),s1=Math.Sin(Math.PI*end/length);
                // Integrate |q|, not the signed resultant: no cancellation across the midspan peak.
                double variation=start<length/2&&end>length/2?2-s0-s1:Math.Abs(s1-s0);
                double force=delta*variation;
                SteelInputValidation.Finite(force,"station shear");
                stations.Add(new PackedStationShear(j,x,start,end,force));
            }
            Stations=Array.AsReadOnly(stations.ToArray());
            Trace=Array.AsReadOnly(new[]{V("InitialBow",InitialBow,"mm"),V("NcrAssembly",CriticalAssemblyPlane,"N"),
                V("NcrOther",CriticalOtherPlane,"N"),V("MomentAssembly",MomentAssemblyPlane,"Nmm"),V("MomentOther",MomentOtherPlane,"Nmm"),
                V("ChordCompression",ChordCompression,"N"),V("ChordTension",ChordTension,"N"),V("ChordAssemblyMoment",ChordAssemblyMoment,"Nmm"),
                V("ChordOtherMoment",ChordOtherMoment,"Nmm"),V("ShearFlowPeak",ShearFlowPeak,"N/mm")});
        }
        public static PackedPairImperfectionModel Evaluate(SteelPackingLayout layout)=>new PackedPairImperfectionModel(layout);
        private static SectionResistanceValue V(string n,double v,string unit)
        {SteelInputValidation.Finite(v,n);return new SectionResistanceValue(n,v,unit);}
    }
}
